using Oculus.Voice.Dictation;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Speech-to-text for bubble recordings, via Meta's Voice SDK dictation.
/// Dictation transcribes a LIVE mic stream, so this runs alongside
/// <see cref="MicrophoneRecorder"/> rather than reading the saved .wav afterwards:
/// call StartTranscribing()/StopTranscribing() from the same buttons that start and
/// stop recording, then read <see cref="LastTranscription"/> when the bubble is created.
/// </summary>
public class BubbleTranscriber : MonoBehaviour
{
    [Tooltip("The AppDictationExperience in the scene. Add one via Meta > Voice SDK if missing.")]
    [SerializeField] private AppDictationExperience _dictation;

    [Tooltip("Fires once with the settled text when dictation finishes.")]
    public UnityEvent<string> onFullTranscription;

    /// <summary>The most recent completed transcription (empty until something is transcribed).</summary>
    public string LastTranscription { get; private set; } = "";

    /// <summary>True while dictation is listening.</summary>
    public bool IsTranscribing => _dictation != null && _dictation.Active;

    private void OnEnable()
    {
        if (_dictation == null)
        {
            Debug.LogError($"{nameof(BubbleTranscriber)}: no AppDictationExperience assigned.", this);
            return;
        }

        _dictation.DictationEvents.OnFullTranscription.AddListener(HandleFull);
    }

    private void OnDisable()
    {
        if (_dictation == null) return;

        _dictation.DictationEvents.OnFullTranscription.RemoveListener(HandleFull);
    }

    /// <summary>Begin listening. Hook this to the 'Start Recording' button alongside the recorder.</summary>
    public void StartTranscribing()
    {
        if (_dictation == null || IsTranscribing) return;

        LastTranscription = "";   // clear the previous bubble's text before a new take
        _dictation.Activate();
        Debug.Log($"{nameof(BubbleTranscriber)}: dictation started.");
    }

    /// <summary>
    /// Stop listening. The final text arrives asynchronously in OnFullTranscription a
    /// moment later, so don't read <see cref="LastTranscription"/> on the very next line.
    /// </summary>
    public void StopTranscribing()
    {
        if (_dictation == null || !IsTranscribing) return;

        _dictation.Deactivate();  // stops the mic but still delivers the pending final result
        Debug.Log($"{nameof(BubbleTranscriber)}: dictation stopped.");
    }

    /// <summary>Abandon the in-progress transcription without waiting for a result.</summary>
    public void CancelTranscribing()
    {
        if (_dictation == null || !IsTranscribing) return;

        _dictation.Cancel();
        LastTranscription = "";
    }

    /// <summary>Writes the last transcription onto a bubble so it persists with the rest of its data.</summary>
    public void ApplyTo(Bubble bubble)
    {
        if (bubble == null) return;
        bubble.BubbleData.transcription = LastTranscription;
    }

    private void HandleFull(string text)
    {
        LastTranscription = text;
        Debug.Log($"{nameof(BubbleTranscriber)}: transcribed \"{text}\".");
        onFullTranscription?.Invoke(text);
    }
}
