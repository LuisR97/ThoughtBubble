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
