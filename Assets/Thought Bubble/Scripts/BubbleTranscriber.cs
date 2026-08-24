using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

/// <summary>
/// Turns a bubble's saved recording into text, fully on-device, and writes the
/// result into <see cref="Bubble.Data.transcription"/> so it persists to bubbles.json.
///
/// This is the PIPELINE only. The actual speech model is not implemented yet — it
/// sits behind <see cref="ITranscriptionBackend"/> so the plumbing (queueing,
/// frame-spreading, state, persistence) can be built and tested before Unity
/// Inference Engine and the Whisper ONNX model are added to the project.
///
/// Today it runs with <see cref="StubTranscriptionBackend"/>, which returns
/// placeholder text. Swapping in the real model means writing one new class and
/// assigning it in <see cref="ResolveBackend"/> — nothing else here changes.
///
/// Transcription reads a FINISHED WAV off disk, never the live microphone, so it
/// never contends with MicrophoneRecorder for the device.
/// </summary>
public class BubbleTranscriber : MonoBehaviour
{
    public enum State { Idle, Transcribing }

    [Tooltip("Optional. Only used to resolve the last recording when transcribing " +
             "a bubble whose audioFilePath has not been set yet.")]
    [SerializeField] private MicrophoneRecorder _recorder;

    [Tooltip("Frames to yield between model steps. Inference must never run in one " +
             "blocking call — a multi-second freeze in VR is nauseating.")]
    [SerializeField] private int _framesBetweenSteps = 1;

    [Tooltip("Placeholder text shown while a bubble is still being transcribed.")]
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
    private ITranscriptionBackend _backend;
    private Coroutine _worker;

    private void Awake()
    {
        _backend = ResolveBackend();
    }

    /// <summary>
    /// Picks the speech backend. Once Unity Inference Engine and Whisper tiny are
    /// in the project, return the real implementation from here instead.
    /// </summary>
    private ITranscriptionBackend ResolveBackend()
    {
        // TODO(whisper): return new WhisperTranscriptionBackend(modelAsset);
        return new StubTranscriptionBackend();
    }

    /// <summary>
    /// Queue a bubble for transcription. Safe to call repeatedly; bubbles are
    /// processed one at a time so two models never run concurrently on the Quest.
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

        bubble.BubbleData.transcription = _pendingLabel;
        _pending.Enqueue(bubble);

        if (_worker == null)
            _worker = StartCoroutine(ProcessQueue());
    }

    /// <summary>
    /// Transcribe a bubble and write the text onto it. Kept as a named method
    /// because this is the shape the rest of the project already expects.
    /// </summary>
    public void ApplyTo(Bubble bubble) => Enqueue(bubble);

    private IEnumerator ProcessQueue()
    {
        while (_pending.Count > 0)
        {
            Bubble bubble = _pending.Dequeue();
            if (bubble == null) continue;   // deleted while queued

            yield return TranscribeOne(bubble);
        }

        CurrentState = State.Idle;
        CurrentBubble = null;
        _worker = null;
    }

    private IEnumerator TranscribeOne(Bubble bubble)
    {
        string relative = bubble.BubbleData.audioFilePath;
        string absolute = Path.Combine(Application.persistentDataPath, relative);

        if (!File.Exists(absolute))
        {
            Fail(bubble, $"recording not found at {absolute}");
            yield break;
        }

        CurrentState = State.Transcribing;
        CurrentBubble = bubble;
        onTranscriptionStarted?.Invoke(bubble);

        string result = null;
        string error = null;

        // The backend yields between steps so the decode loop is spread across
        // frames instead of blocking. Whisper tiny on a Quest 3 is expected to run
        // slower than real time, so this may take longer than the clip itself.
        yield return _backend.Transcribe(
            absolute,
            _framesBetweenSteps,
            text => result = text,
            message => error = message);

        if (error != null)
        {
            Fail(bubble, error);
            yield break;
        }

        // The bubble may have been deleted while the model was running.
        if (bubble == null) yield break;

        bubble.BubbleData.transcription = result;
        LastTranscription = result;
        onTranscriptionComplete?.Invoke(bubble, result);
    }

    private void Fail(Bubble bubble, string message)
    {
        Debug.LogWarning($"{nameof(BubbleTranscriber)}: {message}", bubble);
        if (bubble != null)
            bubble.BubbleData.transcription = null;
        onTranscriptionFailed?.Invoke(bubble, message);
    }
}

/// <summary>
/// A speech-to-text implementation. Kept as an interface so the pipeline above can
/// be finished and tested before the real model exists.
/// </summary>
public interface ITranscriptionBackend
{
    /// <summary>
    /// Transcribe a WAV file. Must yield periodically rather than blocking —
    /// call onDone with the text, or onError with a reason.
    /// </summary>
    IEnumerator Transcribe(string absoluteWavPath, int framesBetweenSteps,
                           Action<string> onDone, Action<string> onError);
}

/// <summary>
/// Placeholder backend. Returns fixed text after a short delay so the queue,
/// state machine, events and JSON persistence can be exercised end to end
/// without a model in the project.
/// </summary>
public class StubTranscriptionBackend : ITranscriptionBackend
{
    public IEnumerator Transcribe(string absoluteWavPath, int framesBetweenSteps,
                                  Action<string> onDone, Action<string> onError)
    {
        // Stand-in for the real work: log-mel preprocessing, encoder pass, then an
        // autoregressive decode loop. Each of those yields between steps.
        for (int step = 0; step < 10; step++)
        {
            for (int f = 0; f < Mathf.Max(1, framesBetweenSteps); f++)
                yield return null;
        }

        long bytes = 0;
        try { bytes = new FileInfo(absoluteWavPath).Length; }
        catch (Exception e) { onError?.Invoke(e.Message); yield break; }

        onDone?.Invoke($"[stub transcription of {Path.GetFileName(absoluteWavPath)}, {bytes / 1024} KB]");
    }
}
