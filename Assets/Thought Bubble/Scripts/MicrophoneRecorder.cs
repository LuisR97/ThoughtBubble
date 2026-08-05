using System.IO;
using System.Text;
using System.Collections;
using UnityEngine;
using UnityEngine.Networking;
#if UNITY_ANDROID
using UnityEngine.Android;
#endif

/// <summary>
/// Records the VR headset microphone straight to a WAV file on disk, so recordings
/// can run arbitrarily long (30-60+ min) without holding the whole clip in memory.
///
/// Flow, driven by the Create Bubble flow in BubbleInstantiator:
///   WarmMic()  - opens the mic device ahead of time (desktop/Link only, to hide the
///                ~1s WASAPI device-open freeze; Quest opens at record-press instead).
///   StartRecording() / StopRecording() - hooked to the record / stop poke buttons.
///                Recording streams to disk between these two.
///   CoolMic()  - closes the device when the flow ends (confirm / cancel).
///
/// The finished file's path (relative to persistentDataPath) is available via
/// <see cref="LastRecordingPath"/> and returned by <see cref="SaveLastRecordingToFile"/>.
/// </summary>
public class MicrophoneRecorder : MonoBehaviour
{
    [Tooltip("Microphone device name. Leave empty to use the system default (the headset mic on Quest).")]
    [SerializeField] private string _device = "";
    [Tooltip("Size of the in-memory capture ring buffer in seconds. Small on purpose: audio is drained to disk continuously, so this only needs to cover a few frames of latency.")]
    [SerializeField] private int _bufferSeconds = 10;
    [Tooltip("Preferred sample rate (Hz). Clamped to what the device supports.")]
    [SerializeField] private int _sampleRate = 44100;
    [Tooltip("Optional: play the recording back through this AudioSource when you stop.")]
    [SerializeField] private AudioSource _playbackSource;
    [SerializeField] private bool _playOnStop = false;

    /// <summary>Relative path (under persistentDataPath) of the most recent finished recording, or null.</summary>
    public string LastRecordingPath { get; private set; }

    /// <summary>True while actively recording (streaming to disk).</summary>
    public bool IsRecording { get; private set; }

    private AudioClip _recordingClip;  // small looping capture buffer
    private int _lastReadPos;          // last sample position drained from the ring buffer
    private int _minPumpSamples;       // only drain once this many new samples exist (cuts GC churn)

    // Incremental WAV file writing.
    private FileStream _fileStream;
    private BinaryWriter _fileWriter;
    private int _dataBytes;            // bytes of PCM audio written so far
    private int _capturePeak;          // loudest sample seen this recording (diagnostic)
    private string _currentRelativePath;

    private Coroutine _playbackLoad;

    private string Device => string.IsNullOrEmpty(_device) ? null : _device;

    private void Start()
    {
        // Ask for mic permission up front so it's ready by the time the user records.
        RequestMicPermission();

        // Diagnostic: list the mic devices Unity can see. If the recording is silent,
        // the default (Device == null) is likely the wrong/muted input — set _device
        // to one of these names to force a specific mic.
        Debug.Log($"{nameof(MicrophoneRecorder)}: available mic devices = [{string.Join(" | ", Microphone.devices)}]");
    }

    // ---- Device lifetime -------------------------------------------------

    /// <summary>
    /// Opens the mic device into a looping buffer so the (slow on desktop/WASAPI,
    /// ~instant on Quest) device-open cost is paid up front instead of at record time.
    /// Call when the Create Bubble flow begins. Safe to call repeatedly.
    /// </summary>
    public void WarmMic()
    {
        if (!HasMicPermission())
        {
            RequestMicPermission();
            return;
        }
        if (Microphone.devices.Length == 0)
        {
            Debug.LogError($"{nameof(MicrophoneRecorder)}: no microphone devices found.");
            return;
        }
        if (Microphone.IsRecording(Device)) return; // already warm

        StartMicStream();
        Debug.Log($"{nameof(MicrophoneRecorder)}: mic warmed.");
    }

    /// <summary>
    /// Closes the mic device. Call when the Create Bubble flow ends (bubble confirmed
    /// or cancelled). Finalizes any still-running recording to disk first.
    /// </summary>
    public void CoolMic()
    {
        if (IsRecording) StopRecording();               // finalize the file before closing
        if (Microphone.IsRecording(Device)) Microphone.End(Device);
        _recordingClip = null;
    }

    /// <summary>Forget the last recording so a fresh Create Bubble flow starts clean
    /// (confirming without recording produces no audio, not the previous bubble's).</summary>
    public void DiscardLastRecording()
    {
        LastRecordingPath = null;
    }

    // Opens the device into a small looping ring buffer. Shared by WarmMic() and the
    // StartRecording() fallback (Quest, where the mic isn't warmed ahead of time).
    private void StartMicStream()
    {
        int rate = ClampToDeviceRate(_sampleRate);
        var sw = System.Diagnostics.Stopwatch.StartNew();
        _recordingClip = Microphone.Start(Device, true, Mathf.Max(1, _bufferSeconds), rate);
        sw.Stop();
        Debug.Log($"{nameof(MicrophoneRecorder)}: Microphone.Start took {sw.ElapsedMilliseconds} ms on '{Device ?? "(default)"}' ({rate} Hz).");
    }

    // ---- Recording -------------------------------------------------------

    /// <summary>
    /// Begin streaming a recording to disk. The mic device is normally opened ahead of
    /// time by <see cref="WarmMic"/>; if it isn't (Quest), it's opened here.
    /// Hook this to the 'Start Recording' button's WhenSelect.
    /// </summary>
    public void StartRecording()
    {
        if (IsRecording) return;

        if (!HasMicPermission())
        {
            Debug.LogWarning($"{nameof(MicrophoneRecorder)}: microphone permission not granted yet; requesting. Try again once granted.");
            RequestMicPermission();
            return;
        }

        if (Microphone.devices.Length == 0)
        {
            Debug.LogError($"{nameof(MicrophoneRecorder)}: no microphone devices found.");
            return;
        }

        // Reuse the warm stream if WarmMic() already opened the device (no freeze);
        // otherwise open it now (Quest / fallback path).
        if (!Microphone.IsRecording(Device))
        {
            StartMicStream();
        }
        if (_recordingClip == null)
        {
            Debug.LogError($"{nameof(MicrophoneRecorder)}: failed to open mic stream.");
            return;
        }

        if (!OpenWavFile(_recordingClip.channels, _recordingClip.frequency))
            return;

        _minPumpSamples = Mathf.Max(1, _recordingClip.frequency / 4); // drain in ~0.25s chunks
        _lastReadPos = Microphone.GetPosition(Device);                // start writing from "now"
        _capturePeak = 0;
        IsRecording = true;
        Debug.Log($"{nameof(MicrophoneRecorder)}: recording started -> {_currentRelativePath}");
    }

    /// <summary>
    /// Stop recording: drain the last samples, finalize the WAV header, and close the
    /// file. Leaves the mic device open (CoolMic closes it). The finished path is in
    /// <see cref="LastRecordingPath"/>. Hook this to the 'Stop Recording' button.
    /// </summary>
    public void StopRecording()
    {
        if (!IsRecording) return;

        PumpMic(force: true); // capture whatever is left in the ring buffer
        IsRecording = false;

        FinalizeWavFile();
        LastRecordingPath = _currentRelativePath;

        float seconds = _recordingClip != null && _recordingClip.frequency > 0
            ? _dataBytes / 2f / _recordingClip.channels / _recordingClip.frequency
            : 0f;
        Debug.Log($"{nameof(MicrophoneRecorder)}: saved recording, {seconds:0.00}s -> {LastRecordingPath}");

        // Diagnostic: if the mic delivered near-silence, the file is fine but empty of
        // sound — the input was covered, muted, or owned by another app (e.g. PC/Link
        // holding the Quest mic while the standalone app also tries to use it).
        if (_capturePeak <= 4)
            Debug.LogWarning($"{nameof(MicrophoneRecorder)}: captured near-silence (peak {_capturePeak}/32767) — mic delivered no audio.");
        else
            Debug.Log($"{nameof(MicrophoneRecorder)}: capture peak {_capturePeak}/32767.");

        if (_playOnStop && !string.IsNullOrEmpty(LastRecordingPath))
            PlayRecording(LastRecordingPath);
    }

    private void Update()
    {
        if (IsRecording) PumpMic(force: false);
    }

    // Drains newly captured samples from the ring buffer and appends them to the WAV
    // file. 'force' ignores the batching threshold (used on stop to flush the tail).
    private void PumpMic(bool force)
    {
        if (_recordingClip == null || _fileWriter == null) return;
        if (!Microphone.IsRecording(Device)) return;

        int bufferSamples = _recordingClip.samples; // per-channel length of the ring buffer
        int pos = Microphone.GetPosition(Device);
        int newCount = pos - _lastReadPos;
        if (newCount < 0) newCount += bufferSamples; // wrapped past the end
        if (newCount <= 0) return;
        if (!force && newCount < _minPumpSamples) return;
        if (newCount > bufferSamples) newCount = bufferSamples; // safety: a huge frame stall

        int channels = _recordingClip.channels;
        float[] chunk = new float[newCount * channels];
        if (_lastReadPos + newCount <= bufferSamples)
        {
            _recordingClip.GetData(chunk, _lastReadPos); // contiguous
        }
        else
        {
            // Slice wrapped past the end: read the tail, then the head.
            int tail = bufferSamples - _lastReadPos;
            float[] tailData = new float[tail * channels];
            _recordingClip.GetData(tailData, _lastReadPos);
            float[] headData = new float[(newCount - tail) * channels];
            _recordingClip.GetData(headData, 0);
            System.Array.Copy(tailData, 0, chunk, 0, tailData.Length);
            System.Array.Copy(headData, 0, chunk, tailData.Length, headData.Length);
        }

        WritePcm(chunk);
        _lastReadPos = pos;
    }

    // ---- WAV file writing ------------------------------------------------

    private bool OpenWavFile(int channels, int rate)
    {
        try
        {
            string fileName = $"{System.Guid.NewGuid():N}.wav";
            _currentRelativePath = Path.Combine("recordings", fileName);
            string absolute = Path.Combine(Application.persistentDataPath, _currentRelativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(absolute));

            _fileStream = new FileStream(absolute, FileMode.Create, FileAccess.Write);
            _fileWriter = new BinaryWriter(_fileStream);
            _dataBytes = 0;
            WriteWavHeader(channels, rate); // sizes are placeholders, patched on finalize
            return true;
        }
        catch (System.Exception e)
        {
            Debug.LogError($"{nameof(MicrophoneRecorder)}: failed to open recording file: {e.Message}");
            _fileWriter = null;
            _fileStream = null;
            return false;
        }
    }

    private void WriteWavHeader(int channels, int rate)
    {
        const short bitsPerSample = 16;
        int blockAlign = channels * (bitsPerSample / 8);
        int byteRate = rate * blockAlign;

        _fileWriter.Write(Encoding.ASCII.GetBytes("RIFF"));
        _fileWriter.Write(0);                       // RIFF chunk size (patched on finalize)
        _fileWriter.Write(Encoding.ASCII.GetBytes("WAVE"));

        _fileWriter.Write(Encoding.ASCII.GetBytes("fmt "));
        _fileWriter.Write(16);                      // fmt chunk size (PCM)
        _fileWriter.Write((short)1);                // audio format: 1 = PCM
        _fileWriter.Write((short)channels);
        _fileWriter.Write(rate);
        _fileWriter.Write(byteRate);
        _fileWriter.Write((short)blockAlign);
        _fileWriter.Write(bitsPerSample);

        _fileWriter.Write(Encoding.ASCII.GetBytes("data"));
        _fileWriter.Write(0);                       // data chunk size (patched on finalize)
    }

    private void WritePcm(float[] samples)
    {
        for (int i = 0; i < samples.Length; i++)
        {
            // float [-1, 1] -> 16-bit signed PCM, clamped to avoid overflow.
            short s = (short)Mathf.Clamp(Mathf.RoundToInt(samples[i] * short.MaxValue), short.MinValue, short.MaxValue);
            _fileWriter.Write(s);
            int a = s < 0 ? -s : s;
            if (a > _capturePeak) _capturePeak = a;
        }
        _dataBytes += samples.Length * 2;
    }

    // Patches the two size fields in the header with the real totals, then closes.
    private void FinalizeWavFile()
    {
        if (_fileWriter == null) return;
        try
        {
            _fileWriter.Flush();
            _fileStream.Seek(4, SeekOrigin.Begin);
            _fileWriter.Write(36 + _dataBytes);     // RIFF chunk size
            _fileStream.Seek(40, SeekOrigin.Begin);
            _fileWriter.Write(_dataBytes);          // data chunk size
            _fileWriter.Flush();
        }
        catch (System.Exception e)
        {
            Debug.LogError($"{nameof(MicrophoneRecorder)}: failed to finalize recording file: {e.Message}");
        }
        finally
        {
            _fileWriter.Dispose();                  // also closes _fileStream
            _fileWriter = null;
            _fileStream = null;
        }
    }

    /// <summary>Returns the finished recording's relative path (the file is already on
    /// disk). Store the result in Bubble.Data.audioFilePath.</summary>
    public string SaveLastRecordingToFile()
    {
        if (string.IsNullOrEmpty(LastRecordingPath))
            Debug.LogWarning($"{nameof(MicrophoneRecorder)}: no recording to save.");
        return LastRecordingPath;
    }

    // ---- Playback (streamed from disk) -----------------------------------

    public void PlayRecording(string relativePath)
    {
        if (_playbackSource == null)
        {
            Debug.LogWarning($"{nameof(MicrophoneRecorder)}: no playback AudioSource assigned.");
            return;
        }

        string absolute = Path.Combine(Application.persistentDataPath, relativePath);
        if (!File.Exists(absolute))
        {
            Debug.LogWarning($"{nameof(MicrophoneRecorder)}: file not found: {absolute}");
            return;
        }

        if (_playbackLoad != null) StopCoroutine(_playbackLoad);
        _playbackLoad = StartCoroutine(LoadAndPlayRoutine(absolute));
    }

    public void PlayLastRecording()
    {
        if (string.IsNullOrEmpty(LastRecordingPath))
        {
            Debug.LogWarning($"{nameof(MicrophoneRecorder)}: no recording to play.");
            return;
        }
        PlayRecording(LastRecordingPath);
    }

    // Streams the clip from disk (streamAudio = true) so a long recording isn't loaded
    // whole into memory just to play it.
    private IEnumerator LoadAndPlayRoutine(string absolutePath)
    {
        string uri = new System.Uri(absolutePath).AbsoluteUri;
        using (UnityWebRequest www = UnityWebRequestMultimedia.GetAudioClip(uri, AudioType.WAV))
        {
            ((DownloadHandlerAudioClip)www.downloadHandler).streamAudio = true;
            yield return www.SendWebRequest();

            if (www.result != UnityWebRequest.Result.Success)
            {
                Debug.LogWarning($"{nameof(MicrophoneRecorder)}: failed to load {absolutePath}: {www.error}");
                _playbackLoad = null;
                yield break;
            }

            AudioClip clip = DownloadHandlerAudioClip.GetContent(www);
            clip.name = "StreamedRecording";
            _playbackSource.clip = clip;
            _playbackSource.Play();
        }
        _playbackLoad = null;
    }

    // ---- Lifecycle safety ------------------------------------------------

    private void OnApplicationPause(bool paused)
    {
        // Headset removed / app backgrounded mid-recording: finalize the file so it
        // isn't left with an unpatched (invalid) header.
        if (paused && IsRecording) StopRecording();
    }

    private void OnDisable()
    {
        if (IsRecording) StopRecording();
        else FinalizeWavFile(); // close any dangling handle
        if (Microphone.IsRecording(Device)) Microphone.End(Device);
    }

    // ---- Helpers ---------------------------------------------------------

    private int ClampToDeviceRate(int desired)
    {
        Microphone.GetDeviceCaps(Device, out int min, out int max);
        if (min == 0 && max == 0) return desired; // 0/0 = any rate supported
        return Mathf.Clamp(desired, min, max);
    }

    private bool HasMicPermission()
    {
#if UNITY_ANDROID
        return Permission.HasUserAuthorizedPermission(Permission.Microphone);
#else
        return true; // Editor / desktop: no runtime request needed
#endif
    }

    private void RequestMicPermission()
    {
#if UNITY_ANDROID
        if (!Permission.HasUserAuthorizedPermission(Permission.Microphone))
            Permission.RequestUserPermission(Permission.Microphone);
#endif
    }
}
