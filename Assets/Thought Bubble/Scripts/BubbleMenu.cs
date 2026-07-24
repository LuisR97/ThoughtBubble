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
    private float elapsedTime;
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
        elapsedTime = 0f;
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
        if (isAudioPlaying)
        {
            AudioSource source = scenePropReference.audioSource;
            isAudioPlaying = source.isPlaying;

            // The clip can be cleared (e.g. the bubble was released) before this flag
            // flips to false — bail out this frame rather than deref a null clip.
            if (source.clip == null)
                return;

            elapsedTime += Time.deltaTime;
            int minutes = Mathf.FloorToInt(elapsedTime / 60f);
            int seconds = Mathf.FloorToInt(elapsedTime % 60f);
            timeCounterText.text = string.Format("{0}:{1:00}", minutes, seconds);

            float clipLength = source.clip.length;
            int clipMinutes = Mathf.FloorToInt(clipLength / 60f);
            int clipSeconds = Mathf.FloorToInt(clipLength % 60f);
            audioLengthText.text = string.Format("{0}:{1:00}", clipMinutes, clipSeconds);

            if(!isAudioFinishedPlaying && !isAudioPlaying && WithinThreshold(elapsedTime, clipLength, 0.5f))
            {
                isAudioFinishedPlaying = true;
                onAudioFinishedPlaying?.Invoke();
                elapsedTime = 0f;
            }
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
        elapsedTime = 0f;
        timeCounterText.text = "0:00";
        PauseAudio(evt);
        isAudioPlaying = false;
        isAudioFinishedPlaying = false;
    }

    private bool WithinThreshold(float a, float b, float threshold)
    {
        return Mathf.Abs(a - b) < threshold;
    }

    //TODO add a method for resetting all the buttons


    
}
