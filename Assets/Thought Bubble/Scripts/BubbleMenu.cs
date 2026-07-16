using Oculus.Interaction;
using UnityEngine;

public class BubbleMenu : MonoBehaviour
{
    public ScenePropReference scenePropReference;
    public PointableUnityEventWrapper playAudioButton, pauseAudioButton, rewindAudioButton;
    private PokeInteractableToggle playAudioButtonToggle, pauseAudioButtonToggle, rewindAudioButtonToggle;
    //TODO add reference to the delete button and text scroll with audio transcription

    void Awake()
    {
        playAudioButtonToggle = playAudioButton.gameObject.GetComponent<PokeInteractableToggle>();
        pauseAudioButtonToggle = pauseAudioButton.gameObject.GetComponent<PokeInteractableToggle>();
        rewindAudioButtonToggle = rewindAudioButton.gameObject.GetComponent<PokeInteractableToggle>();

        playAudioButton.WhenSelect.AddListener(PlayOrResumeAudio);
        pauseAudioButton.WhenSelect.AddListener(PauseAudio);
        rewindAudioButton.WhenSelect.AddListener(RewindAudio);
    }
    void Start()
    {
        scenePropReference = ScenePropReference.Instance;
    }

    void OnEnable()
    {
        playAudioButtonToggle.Enable();
        pauseAudioButtonToggle.Disable();
        rewindAudioButtonToggle.Disable();
    }

    void OnDestroy()
    {
        playAudioButton.WhenSelect.RemoveListener(PlayOrResumeAudio);
        pauseAudioButton.WhenSelect.RemoveListener(PauseAudio);
        rewindAudioButton.WhenSelect.RemoveListener(RewindAudio);
    }

    public void PlayOrResumeAudio(PointerEvent evt)
    {
        // If no bubble is currently grabbed
        if (scenePropReference.currentBubbleBeingGrabbed == null)
        {
            Debug.LogWarning("No bubble is currently being grabbed.");
            return;
        }

        //for whatever reason if there is no audio file path for the current bubble
        string audioFilePath = scenePropReference.currentBubbleBeingGrabbed.BubbleData.audioFilePath;
        if (string.IsNullOrEmpty(audioFilePath))
        {
            Debug.LogWarning("No audio file path found for the current bubble.");
            return;
        }

        AudioSource source = scenePropReference.audioSource;

        //Audio is already playing so dont do anything
        if (source.isPlaying)
            return;

        // A clip is loaded but not playing → it was paused, so resume from that point.
        // (Release clears the clip, so a non-null clip always belongs to this bubble.)
        if (source.clip != null)
        {
            source.UnPause();
            if (source.isPlaying)
                return;
        }

        // Nothing loaded (fresh grab) → load this bubble's recording and play from the start.
        scenePropReference.microphoneRecorder.PlayRecording(audioFilePath);
    }

    public void PauseAudio(PointerEvent evt)
    {
        scenePropReference.audioSource.Pause();
    }

    public void RewindAudio(PointerEvent evt)
    {
        scenePropReference.audioSource.time = 0f;
        PauseAudio(evt);
    }

    //TODO add a method for resetting all the buttons

    
}
