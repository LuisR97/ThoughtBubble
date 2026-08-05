using Oculus.Interaction;
using UnityEngine;
using TMPro;
using UnityEngine.Events;

public class BubbleMenu : MonoBehaviour
{
    private ScenePropReference scenePropReference; //TODO make sure this reference still works after being made private
    public PointableUnityEventWrapper playAudioButton, pauseAudioButton, rewindAudioButton;
    private PokeInteractableToggle playAudioButtonToggle, pauseAudioButtonToggle, rewindAudioButtonToggle;
    //TODO add reference to the delete button and text scroll with audio transcription
    public TMP_Text audioLengthText, timeCounterText;
    private bool isAudioPlaying;
    private bool isAudioFinishedPlaying = false;
    public UnityEvent onAudioFinishedPlaying;

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
        isAudioPlaying = false;
        isAudioFinishedPlaying = false;
        timeCounterText.text = "0:00";
        audioLengthText.text = "0:00";
    }

    void OnDestroy()
    {
        playAudioButton.WhenSelect.RemoveListener(PlayOrResumeAudio);
        pauseAudioButton.WhenSelect.RemoveListener(PauseAudio);
        rewindAudioButton.WhenSelect.RemoveListener(RewindAudio);
    }

    void Update()
    {
        AudioSource source = scenePropReference.audioSource;

        // Nothing loaded yet — a streamed recording may still be loading from disk,
        // or the bubble was released (which clears the clip). Wait for it.
        if (source == null || source.clip == null)
            return;

        // Length is known as soon as the clip is loaded, whether or not it's playing yet.
        audioLengthText.text = FormatTime(source.clip.length);

        if (source.isPlaying)
        {
            isAudioPlaying = true;
            isAudioFinishedPlaying = false;
            timeCounterText.text = FormatTime(source.time);
        }
        else if (isAudioPlaying && !isAudioFinishedPlaying)
        {
            // Was playing and has now stopped on its own → finished. (A user pause goes
            // through PauseAudio, which clears isAudioPlaying, so we don't land here for pauses.)
            isAudioPlaying = false;
            isAudioFinishedPlaying = true;
            timeCounterText.text = FormatTime(source.clip.length);
            onAudioFinishedPlaying?.Invoke();
        }
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
        {
            return;
        }

        // A clip is loaded but not playing → it was paused, so resume from that point.
        // (Release clears the clip, so a non-null clip always belongs to this bubble.)
        if (source.clip != null)
        {
            source.UnPause();
            isAudioPlaying = true;
            if (source.isPlaying)
            {
                return;
            }
        }

        // Nothing loaded (fresh grab) → load this bubble's recording and play from the start.
        scenePropReference.microphoneRecorder.PlayRecording(audioFilePath);
        isAudioPlaying = true;
        isAudioFinishedPlaying = false;
    }

    public void PauseAudio(PointerEvent evt)
    {
        scenePropReference.audioSource.Pause();
        isAudioPlaying = false;
    }

    public void RewindAudio(PointerEvent evt)
    {
        scenePropReference.audioSource.time = 0f;
        timeCounterText.text = "0:00";
        PauseAudio(evt);
        isAudioPlaying = false;
        isAudioFinishedPlaying = false;
    }

    private string FormatTime(float seconds)
    {
        int minutes = Mathf.FloorToInt(seconds / 60f);
        int secs = Mathf.FloorToInt(seconds % 60f);
        return string.Format("{0}:{1:00}", minutes, secs);
    }

    //TODO add a method for resetting all the buttons


    
}
