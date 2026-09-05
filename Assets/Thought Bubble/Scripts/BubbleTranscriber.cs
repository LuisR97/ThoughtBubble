using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
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
/// RESUMABLE BY DESIGN. Inference is far slower than real time on Quest — a minute of
/// speech can take many minutes to transcribe — which is longer than a user is likely to
/// keep the headset on. So the audio is cut into chunks and each finished chunk is saved
/// immediately, along with how far through the file we got. A bubble that was interrupted
/// picks up where it stopped on the next launch instead of starting over. Without this a
/// long recording would never finish: every session would redo work it had already done.
///
/// Transcription reads a FINISHED WAV off disk, never the live microphone, so it never
/// contends with <see cref="MicrophoneRecorder"/> for the device.
/// </summary>
public class BubbleTranscriber : MonoBehaviour
{
    public enum State { Idle, Transcribing }

    [Tooltip("The Whisper model host. Loads the weights on Awake and owns the native context.")]
    [SerializeField] private WhisperManager _whisper;

    [Tooltip("Optional. Only used to resolve the last recording when transcribing " +
             "a bubble whose audioFilePath has not been set yet.")]
    [SerializeField] private MicrophoneRecorder _recorder;

    [Tooltip("Seconds of audio handed to the model per chunk, and therefore how often " +
             "progress is saved. 30 is the efficient value: Whisper always processes a " +
             "30-second window internally, so a smaller chunk costs nearly the same and " +
             "just wastes work.")]
    [SerializeField] private float _chunkSeconds = 30f;

    /// <summary>Text of the most recently completed transcription, or null.</summary>
    public string LastTranscription { get; private set; }

    public State CurrentState { get; private set; } = State.Idle;

    /// <summary>The bubble currently being transcribed, or null when idle.</summary>
    public Bubble CurrentBubble { get; private set; }

    public event Action<Bubble> onTranscriptionStarted;
    /// <summary>Raised after every chunk, so UI can show text arriving progressively.</summary>
    public event Action<Bubble, string> onTranscriptionProgress;
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
    /// A bubble that is already partly transcribed resumes rather than restarting.
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

        if (bubble.BubbleData.transcriptionComplete) return;
        if (_pending.Contains(bubble)) return;     // already waiting

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

        // Fire-and-forget: nobody awaits this method, so an escaping exception would
        // vanish silently AND leave _isProcessing stuck true, jamming the queue forever.
        try
        {
            while (_pending.Count > 0)
            {
                Bubble bubble = _pending.Dequeue();
                if (bubble == null) continue;   // deleted while queued

                await TranscribeOne(bubble);
                if (_destroyed) return;
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"{nameof(BubbleTranscriber)}: transcription loop failed: {e}");
        }
        finally
        {
            CurrentState = State.Idle;
            CurrentBubble = null;
            _isProcessing = false;
        }
    }

    private async Task TranscribeOne(Bubble bubble)
    {
        Bubble.Data data = bubble.BubbleData;
        string absolute = Path.Combine(Application.persistentDataPath, data.audioFilePath);

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

        int samplesPerSecond = wav.frequency * wav.channels;
        double totalSeconds = (double)wav.samples.Length / samplesPerSecond;

        CurrentState = State.Transcribing;
        CurrentBubble = bubble;
        onTranscriptionStarted?.Invoke(bubble);

        // Resume point. Clamped in case the audio file was replaced by a shorter one.
        double offset = Mathf.Clamp((float)data.transcribedSeconds, 0f, (float)totalSeconds);

        // Nothing banked yet, so whatever text is on the bubble is stale — an old
        // "transcribing…" placeholder from before chunking, or a run that never saved a
        // chunk. Start clean instead of appending real speech onto a leftover marker.
        if (offset <= 0) data.transcription = null;

        var text = new StringBuilder(data.transcription ?? string.Empty);

        if (offset > 0)
            Debug.Log($"{nameof(BubbleTranscriber)}: resuming at {offset:0.0}s of {totalSeconds:0.0}s.", bubble);

        while (offset < totalSeconds - 0.05)
        {
            double chunkLength = Math.Min(_chunkSeconds, totalSeconds - offset);
            bool isFinalChunk = offset + chunkLength >= totalSeconds - 0.05;

            int from = (int)(offset * samplesPerSecond);
            int count = Math.Min((int)(chunkLength * samplesPerSecond), wav.samples.Length - from);
            if (count <= 0) break;

            float[] slice = new float[count];
            Array.Copy(wav.samples, from, slice, 0, count);

            var sw = System.Diagnostics.Stopwatch.StartNew();
            WhisperResult result = await _whisper.GetTextAsync(slice, wav.frequency, wav.channels);
            sw.Stop();

            // The transcriber or the bubble may have been destroyed while the model ran.
            if (_destroyed) return;
            if (bubble == null) return;

            if (result == null)
            {
                Fail(bubble, "Whisper returned no result — check that the model weights loaded.");
                return;
            }

            // How much audio this chunk actually accounted for. Resuming on a segment
            // boundary rather than a hard cut keeps us from slicing through a word —
            // it is the same trick whisper.cpp uses internally to walk a long file.
            double consumed = chunkLength;
            if (!isFinalChunk && result.Segments != null && result.Segments.Count > 0)
            {
                double lastEnd = result.Segments[result.Segments.Count - 1].End.TotalSeconds;
                // Only trust it if it leaves us meaningfully forward; otherwise a short
                // or empty segment list would stall the loop on the same chunk forever.
                if (lastEnd > chunkLength * 0.5 && lastEnd < chunkLength)
                    consumed = lastEnd;
            }

            string chunkText = StripNonSpeechMarkers(result.Result);
            if (!string.IsNullOrEmpty(chunkText))
            {
                if (text.Length > 0) text.Append(' ');
                text.Append(chunkText);
            }

            offset += consumed;
            if (isFinalChunk) offset = totalSeconds;

            // Persist after every chunk. This is the whole point of chunking: if the
            // headset comes off now, this much is permanently banked.
            data.transcription = text.ToString();
            data.transcribedSeconds = offset;
            data.transcriptionComplete = offset >= totalSeconds - 0.05;

            Debug.Log($"{nameof(BubbleTranscriber)}: chunk done in {sw.ElapsedMilliseconds} ms " +
                      $"({consumed:0.0}s audio) — {offset:0.0}/{totalSeconds:0.0}s transcribed.", bubble);

            Save();
            onTranscriptionProgress?.Invoke(bubble, data.transcription);
        }

        data.transcriptionComplete = true;
        Save();

        LastTranscription = data.transcription;
        Debug.Log($"{nameof(BubbleTranscriber)}: finished — \"{data.transcription}\"", bubble);
        onTranscriptionComplete?.Invoke(bubble, data.transcription);
    }

    /// <summary>
    /// Writes bubbles.json now rather than waiting for the app to pause. Chunk progress
    /// is worthless if it only reaches disk on a clean exit.
    /// </summary>
    private void Save()
    {
        SavedBubbleData saved = ScenePropReference.Instance != null
            ? ScenePropReference.Instance.savedBubbles
            : null;
        if (saved != null) saved.SaveToFile();
    }

    /// <summary>
    /// Strips Whisper's non-speech markers — [BLANK_AUDIO], [Music], [Laughter] and the
    /// like. Whisper was trained on subtitled video, so it writes these stage directions
    /// when it hears sound that isn't speech. They are not something the user said, so
    /// they don't belong in a saved thought. Nobody speaks square brackets, which makes
    /// anything inside them safe to drop; real speech either side of a marker is kept.
    /// </summary>
    private static string StripNonSpeechMarkers(string text)
    {
        if (string.IsNullOrEmpty(text)) return text;

        string stripped = Regex.Replace(text, @"\[[^\]]*\]", " ");
        return Regex.Replace(stripped, @"\s+", " ").Trim();
    }

    private void Fail(Bubble bubble, string message)
    {
        Debug.LogWarning($"{nameof(BubbleTranscriber)}: {message}", bubble);
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

            int bitsPerSample = 0, frequency = 0, channels = 0;
            int dataOffset = -1, dataLength = 0;

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
                    dataLength = Math.Min(chunkSize, bytes.Length - body);
                }

                pos = body + chunkSize + (chunkSize & 1); // chunks are word-aligned
            }

            if (dataOffset < 0 || frequency <= 0 || channels <= 0)
                return (false, null, 0, 0, "missing fmt or data chunk");
            if (bitsPerSample != 16)
                return (false, null, 0, 0, $"expected 16-bit PCM, found {bitsPerSample}-bit");

            int count = dataLength / 2;
            float[] samples = new float[count];
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
