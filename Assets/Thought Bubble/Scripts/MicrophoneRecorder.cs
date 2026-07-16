using System.IO;
using System.Text;
using UnityEngine;
#if UNITY_ANDROID
using UnityEngine.Android;
#endif

/// <summary>
/// Records the VR headset microphone between StartRecording() and StopRecording().
/// Wire those two methods to your 'Start Recording' / 'Stop Recording' poke buttons
/// (PointableUnityEventWrapper.WhenSelect). After stopping, the finished clip is
/// available to other scripts via <see cref="LastRecording"/>.
/// </summary>
public class MicrophoneRecorder : MonoBehaviour
{
    [Tooltip("Microphone device name. Leave empty to use the system default (the headset mic on Quest).")]
    [SerializeField] private string _device = "";
    [Tooltip("Maximum recording length in seconds. Recording auto-stops at this cap.")]
    [SerializeField] private int _maxLengthSeconds = 300;
    [Tooltip("Preferred sample rate (Hz). Clamped to what the device supports.")]
    [SerializeField] private int _sampleRate = 44100;
    [Tooltip("Optional: play the recording back through this AudioSource when you stop.")]
    [SerializeField] private AudioSource _playbackSource;
    [SerializeField] private bool _playOnStop = false;

    /// <summary>The most recent finished recording (null until something is recorded).</summary>
    public AudioClip LastRecording { get; private set; }

    /// <summary>True while actively recording.</summary>
    public bool IsRecording { get; private set; }

    private AudioClip _recordingClip;
    private string Device => string.IsNullOrEmpty(_device) ? null : _device;

    private void Start()
    {
        // Ask for mic permission up front so it's ready by the time the user records.
        RequestMicPermission();
    }

    /// <summary>Begin recording. Hook this to the 'Start Recording' button's WhenSelect.</summary>
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

        int rate = ClampToDeviceRate(_sampleRate);
        _recordingClip = Microphone.Start(Device, false, _maxLengthSeconds, rate);
        IsRecording = true;
        Debug.Log($"{nameof(MicrophoneRecorder)}: recording started ({rate} Hz).");
    }

    /// <summary>
    /// Stop recording, trim to the actual captured length, and store it in
    /// <see cref="LastRecording"/>. Hook this to the 'Stop Recording' button.
    /// </summary>
    public void StopRecording()
    {
        if (!IsRecording) return;

        int position = Microphone.GetPosition(Device); // how many samples were captured
        Microphone.End(Device);
        IsRecording = false;

        if (_recordingClip == null || position <= 0)
        {
            Debug.LogWarning($"{nameof(MicrophoneRecorder)}: nothing was recorded.");
            return;
        }

        // Microphone.Start allocates the full _maxLengthSeconds up front; copy only
        // the samples actually recorded into a right-sized clip so there's no
        // trailing silence.
        float[] samples = new float[position * _recordingClip.channels];
        _recordingClip.GetData(samples, 0);

        AudioClip trimmed = AudioClip.Create("MicRecording", position, _recordingClip.channels,
                                             _recordingClip.frequency, false);
        trimmed.SetData(samples, 0);
        LastRecording = trimmed;

        Debug.Log($"{nameof(MicrophoneRecorder)}: saved recording, {trimmed.length:0.00}s.");

        if (_playOnStop && _playbackSource != null)
        {
            _playbackSource.clip = LastRecording;
            _playbackSource.Play();
        }
    }

    public string SaveLastRecordingToFile()
    {
        if (LastRecording == null)
        {
            Debug.LogWarning($"{nameof(MicrophoneRecorder)}: no recording to save.");
            return null;
        }

        string fileName = $"{System.Guid.NewGuid():N}.wav";
        string relative = Path.Combine("recordings", fileName);
        string absolute = Path.Combine(Application.persistentDataPath, relative);

        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(absolute)); // makes recordings/ if missing
            File.WriteAllBytes(absolute, EncodeToWav(LastRecording));
            Debug.Log($"{nameof(MicrophoneRecorder)}: saved recording to {absolute}");
            return relative; // store this in Bubble.Data.audioFilePath
        }
        catch (System.Exception e)
        {
            Debug.LogError($"{nameof(MicrophoneRecorder)}: failed to save recording: {e.Message}");
            return null;
        }
    }

    /// <summary>Encodes an AudioClip's samples as a 16-bit PCM WAV byte array.</summary>
    private static byte[] EncodeToWav(AudioClip clip)
    {
        float[] samples = new float[clip.samples * clip.channels];
        clip.GetData(samples, 0);

        int channels = clip.channels;
        int sampleRate = clip.frequency;
        const int bitsPerSample = 16;
        int blockAlign = channels * (bitsPerSample / 8);
        int byteRate = sampleRate * blockAlign;
        int dataSize = samples.Length * (bitsPerSample / 8);

        using MemoryStream stream = new MemoryStream(44 + dataSize);
        using BinaryWriter writer = new BinaryWriter(stream);

        // RIFF header
        writer.Write(Encoding.ASCII.GetBytes("RIFF"));
        writer.Write(36 + dataSize);              // total file size minus 8 bytes
        writer.Write(Encoding.ASCII.GetBytes("WAVE"));

        // fmt chunk
        writer.Write(Encoding.ASCII.GetBytes("fmt "));
        writer.Write(16);                          // fmt chunk size (16 for PCM)
        writer.Write((short)1);                    // audio format: 1 = PCM
        writer.Write((short)channels);
        writer.Write(sampleRate);
        writer.Write(byteRate);
        writer.Write((short)blockAlign);
        writer.Write((short)bitsPerSample);

        // data chunk
        writer.Write(Encoding.ASCII.GetBytes("data"));
        writer.Write(dataSize);
        foreach (float sample in samples)
        {
            // Convert float [-1, 1] to 16-bit signed PCM, clamped to avoid overflow.
            short s = (short)Mathf.Clamp(Mathf.RoundToInt(sample * short.MaxValue), short.MinValue, short.MaxValue);
            writer.Write(s);
        }

        writer.Flush();
        return stream.ToArray();
    }

    /// <summary>Decodes a 16-bit PCM WAV byte array (as written by EncodeToWav) into an AudioClip.</summary>
    private static AudioClip DecodeWav(byte[] wav)
    {
        using MemoryStream stream = new MemoryStream(wav);
        using BinaryReader reader = new BinaryReader(stream);

        // RIFF header
        if (Encoding.ASCII.GetString(reader.ReadBytes(4)) != "RIFF") return null;
        reader.ReadInt32();                                              // file size
        if (Encoding.ASCII.GetString(reader.ReadBytes(4)) != "WAVE") return null;

        short channels = 1;
        int sampleRate = 44100;
        short bitsPerSample = 16;
        byte[] pcm = null;

        // Walk the chunks; grab 'fmt ' (format info) and 'data' (the samples).
        while (stream.Position + 8 <= stream.Length)
        {
            string chunkId = Encoding.ASCII.GetString(reader.ReadBytes(4));
            int chunkSize = reader.ReadInt32();

            if (chunkId == "fmt ")
            {
                reader.ReadInt16();                 // audio format (1 = PCM)
                channels = reader.ReadInt16();
                sampleRate = reader.ReadInt32();
                reader.ReadInt32();                 // byte rate
                reader.ReadInt16();                 // block align
                bitsPerSample = reader.ReadInt16();
                if (chunkSize > 16) reader.ReadBytes(chunkSize - 16); // skip any extra fmt bytes
            }
            else if (chunkId == "data")
            {
                pcm = reader.ReadBytes(chunkSize);
            }
            else
            {
                reader.ReadBytes(chunkSize);        // skip unknown chunk
            }
        }

        if (pcm == null || bitsPerSample != 16 || channels < 1)
            return null;

        // 16-bit PCM -> float [-1, 1]
        int totalSamples = pcm.Length / 2;
        float[] samples = new float[totalSamples];
        for (int i = 0; i < totalSamples; i++)
        {
            short s = (short)(pcm[i * 2] | (pcm[i * 2 + 1] << 8)); // little-endian
            samples[i] = s / (float)short.MaxValue;
        }

        AudioClip clip = AudioClip.Create("LoadedRecording", totalSamples / channels, channels, sampleRate, false);
        clip.SetData(samples, 0);
        return clip;
    }

    public void PlayRecording(string relativePath)
    {
        string absolute = Path.Combine(Application.persistentDataPath, relativePath);
        if (!File.Exists(absolute))
        {
            Debug.LogWarning($"{nameof(MicrophoneRecorder)}: file not found: {absolute}");
            return;
        }

        byte[] wavData = File.ReadAllBytes(absolute);
        AudioClip clip = DecodeWav(wavData);
        if (clip == null)
        {
            Debug.LogWarning($"{nameof(MicrophoneRecorder)}: failed to decode WAV: {absolute}");
            return;
        }

        if (_playbackSource == null)
        {
            Debug.LogWarning($"{nameof(MicrophoneRecorder)}: no playback AudioSource assigned.");
            return;
        }

        _playbackSource.clip = clip;
        _playbackSource.Play();
    }

    public void PlayLastRecording()
    {
        if (LastRecording == null)
        {
            Debug.LogWarning($"{nameof(MicrophoneRecorder)}: no recording to play.");
            return;
        }
        if (_playbackSource == null)
        {
            Debug.LogWarning($"{nameof(MicrophoneRecorder)}: no playback AudioSource assigned.");
            return;
        }
        _playbackSource.clip = LastRecording;
        _playbackSource.Play();
    }

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
