using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using UnityEngine;
using Whisper;

/// <summary>
/// Turns a bubble's saved recording into text, fully on-device, and writes the result
/// into <see cref="Bubble.Data.transcription"/> so it persists to bubbles.json.
///
/// Speech recognition is Whisper, running through whisper.unity (a Unity wrapper around
/// the native whisper.cpp library). Audio never leaves the headset or the PC.
///
/// Transcription reads a FINISHED WAV off disk, never the live microphone, so it never
/// contends with <see cref="MicrophoneRecorder"/> for the device.
///
/// Inference runs on a background thread inside whisper.unity, so it does not stall
/// rendering — but it is slow (10s+ on Quest for a short clip), which is why the bubble
/// gets <see cref="_pendingLabel"/> immediately and the real text arrives later.
/// Bubbles are transcribed one at a time; whisper.cpp serialises calls internally anyway,
/// and two inferences fighting over the Quest CPU helps nobody.
/// </summary>
public class BubbleTranscriber : MonoBehaviour
{
    public enum State { Idle, Transcribing }

    [Tooltip("The Whisper model host. Loads the weights on Awake and owns the native context.")]
    [SerializeField] private WhisperManager _whisper;

    [Tooltip("Optional. Only used to resolve the last recording when transcribing " +
             "a bubble whose audioFilePath has not been set yet.")]
    [SerializeField] private MicrophoneRecorder _recorder;

    [Tooltip("Placeholder text held in the bubble while it is still being transcribed.")]
    [SerializeField] private string _pendingLabel = "transcribing…";

    /// <summary>Text of the most recently completed transcription, or null.</summary>
    public string LastTranscription { get; private set; }

    public State CurrentState { get; private set; } = State.Idle;

    /// <summary>The bubble currently being transcribed, or null when idle.</summary>
    public Bubble CurrentBubble { get; private set; }

    public event Action<Bubble> onTranscriptionStarted;
    public event Action<Bubble, string> onTranscriptionComplete;
    public event Action<Bubble, string> onTranscriptionFailed;

    private readonly Queue<Bubble> _pending = new Queue<Bubble>();
    private bool _isProcessing;
    private bool _destroyed;

    private void OnDestroy()
    {
        // Unlike a coroutine, an in-flight Task keeps running after this object dies.
        // The flag stops the continuation from touching destroyed Unity objects.
        _destroyed = true;
    }

    /// <summary>
    /// Queue a bubble for transcription. Safe to call repeatedly; returns immediately.
    /// The bubble shows <see cref="_pendingLabel"/> until the model finishes.
    /// </summary>
    public void Enqueue(Bubble bubble)
    {
        if (bubble == null) return;

        if (string.IsNullOrEmpty(bubble.BubbleData.audioFilePath) && _recorder != null)
            bubble.BubbleData.audioFilePath = _recorder.LastRecordingPath;

        if (string.IsNullOrEmpty(bubble.BubbleData.audioFilePath))
        {
            Debug.LogWarning($"{nameof(BubbleTranscriber)}: bubble has no audioFilePath; nothing to transcribe.", bubble);
            return;
        }

        if (_whisper == null)
        {
            Debug.LogError($"{nameof(BubbleTranscriber)}: no WhisperManager assigned.", this);
            return;
        }

        bubble.BubbleData.transcription = _pendingLabel;
        _pending.Enqueue(bubble);

        if (!_isProcessing)
            _ = ProcessQueue();
    }

    /// <summary>
    /// Transcribe a bubble and write the text onto it. Kept as a named method
    /// because this is the shape the rest of the project already expects.
    /// </summary>
    public void ApplyTo(Bubble bubble) => Enqueue(bubble);

    private async Task ProcessQueue()
    {
        _isProcessing = true;

        while (_pending.Count > 0)
        {
            Bubble bubble = _pending.Dequeue();
            if (bubble == null) continue;   // deleted while queued

            await TranscribeOne(bubble);
            if (_destroyed) return;
        }

        CurrentState = State.Idle;
        CurrentBubble = null;
        _isProcessing = false;
    }

    private async Task TranscribeOne(Bubble bubble)
    {
        string absolute = Path.Combine(Application.persistentDataPath, bubble.BubbleData.audioFilePath);

        if (!File.Exists(absolute))
        {
            Fail(bubble, $"recording not found at {absolute}");
            return;
        }

        // Read off the main thread: a long recording is megabytes, and decoding it to
        // floats in one frame would show up as a hitch in the headset.
        var wav = await Task.Run(() => ReadWav(absolute));
        if (_destroyed) return;

        if (!wav.ok)
        {
            Fail(bubble, $"could not read {Path.GetFileName(absolute)}: {wav.error}");
            return;
        }

        CurrentState = State.Transcribing;
        CurrentBubble = bubble;
        onTranscriptionStarted?.Invoke(bubble);

        // whisper.unity downmixes to mono and resamples to 16 kHz for us, so whatever the
        // mic actually gave us will work — but see MicrophoneRecorder, which now asks for
        // 16 kHz up front so that conversion is a no-op.
        WhisperResult result = await _whisper.GetTextAsync(wav.samples, wav.frequency, wav.channels);

        // The transcriber or the bubble may have been destroyed while the model ran.
        if (_destroyed) return;
        if (bubble == null) return;

        if (result == null)
        {
            Fail(bubble, "Whisper returned no result — check that the model weights loaded.");
            return;
        }

        // Whisper pads its output with leading spaces and emits markers like [BLANK_AUDIO]
        // for silence; neither belongs in a saved thought.
        string text = result.Result?.Trim();
        if (string.IsNullOrEmpty(text) || text == "[BLANK_AUDIO]")
        {
            Fail(bubble, "no speech detected in recording");
            return;
        }

        bubble.BubbleData.transcription = text;
        LastTranscription = text;
        // Logged so the result is verifiable before any UI exists to show it —
        // on the headset, read it with: adb logcat -s Unity
        Debug.Log($"{nameof(BubbleTranscriber)}: transcribed \"{text}\"", bubble);
        onTranscriptionComplete?.Invoke(bubble, text);
    }

    private void Fail(Bubble bubble, string message)
    {
        Debug.LogWarning($"{nameof(BubbleTranscriber)}: {message}", bubble);
        if (bubble != null)
            bubble.BubbleData.transcription = null;
        onTranscriptionFailed?.Invoke(bubble, message);
    }

    /// <summary>
    /// Reads a 16-bit PCM WAV into normalised float samples, interleaved by channel —
    /// the layout whisper.unity expects. Walks the chunk list rather than assuming a
    /// fixed 44-byte header, so it still works if the header ever gains a chunk.
    /// </summary>
    private static (bool ok, float[] samples, int frequency, int channels, string error)
        ReadWav(string path)
    {
        try
        {
            byte[] bytes = File.ReadAllBytes(path);
            if (bytes.Length < 12 ||
                BitConverter.ToUInt32(bytes, 0) != 0x46464952 ||   // "RIFF"
                BitConverter.ToUInt32(bytes, 8) != 0x45564157)     // "WAVE"
                return (false, null, 0, 0, "not a RIFF/WAVE file");

            int frequency = 0;
            int channels = 0;
            int bitsPerSample = 0;
            int dataOffset = -1;
            int dataLength = 0;

            int pos = 12;
            while (pos + 8 <= bytes.Length)
            {
                uint chunkId = BitConverter.ToUInt32(bytes, pos);
                int chunkSize = BitConverter.ToInt32(bytes, pos + 4);
                int body = pos + 8;

                if (chunkSize < 0) break;   // malformed; stop rather than loop forever

                if (chunkId == 0x20746D66 && body + 16 <= bytes.Length)      // "fmt "
                {
                    channels = BitConverter.ToInt16(bytes, body + 2);
                    frequency = BitConverter.ToInt32(bytes, body + 4);
                    bitsPerSample = BitConverter.ToInt16(bytes, body + 14);
                }
                else if (chunkId == 0x61746164)                              // "data"
                {
                    dataOffset = body;
                    dataLength = Mathf.Min(chunkSize, bytes.Length - body);
                }

                pos = body + chunkSize + (chunkSize & 1); // chunks are word-aligned
            }

            if (dataOffset < 0 || frequency <= 0 || channels <= 0)
                return (false, null, 0, 0, "missing fmt or data chunk");

            if (bitsPerSample != 16)
                return (false, null, 0, 0, $"expected 16-bit PCM, found {bitsPerSample}-bit");

            int count = dataLength / 2;
            var samples = new float[count];
            for (int i = 0; i < count; i++)
                samples[i] = BitConverter.ToInt16(bytes, dataOffset + i * 2) / 32768f;

            return (true, samples, frequency, channels, null);
        }
        catch (Exception e)
        {
            return (false, null, 0, 0, e.Message);
        }
    }
}
