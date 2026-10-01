using Oculus.Interaction;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Drives the transcription Scroll View from the two small poke buttons on the
/// Bubble Menu. The view scrolls continuously for as long as a button is held
/// down, rather than jumping a fixed step on each press.
///
/// ISDK reports a poke as a press/release pair: PointableUnityEventWrapper raises
/// WhenSelect when the finger goes into the button and WhenUnselect when it comes
/// back out. "Held" is just the span between those two, so the button events only
/// set a direction and Update does the actual scrolling.
/// </summary>
public class TextScrollControls : MonoBehaviour
{
    [Tooltip("The Scroll View holding the transcription text.")]
    [SerializeField] private ScrollRect _scrollRect;

    [Tooltip("The SCROLL UP poke button (the '^' one on the Bubble Menu).")]
    [SerializeField] private PointableUnityEventWrapper _scrollUpButton;

    [Tooltip("The SCROLL DOWN poke button (the 'V' one on the Bubble Menu).")]
    [SerializeField] private PointableUnityEventWrapper _scrollDownButton;

    [Tooltip("Scroll speed in canvas units per second. This is measured against the " +
             "content height, so a long transcription scrolls at the same visible " +
             "speed as a short one.")]
    [SerializeField] private float _unitsPerSecond = 300f;

    [Tooltip("Temporary: logs each button press and warns when there is nothing to " +
             "scroll. Turn off once scrolling works.")]
    [SerializeField] private bool _logDiagnostics = true;

    // +1 scrolls toward the top of the text, -1 toward the bottom, 0 is nothing held.
    private float _direction;

    // The "nothing to scroll" case is true every frame, so it is logged only once.
    private bool _warnedNothingToScroll;

    private void Awake()
    {
        if (_scrollUpButton != null)
        {
            _scrollUpButton.WhenSelect.AddListener(ScrollUp);
            _scrollUpButton.WhenUnselect.AddListener(StopScrolling);
        }

        if (_scrollDownButton != null)
        {
            _scrollDownButton.WhenSelect.AddListener(ScrollDown);
            _scrollDownButton.WhenUnselect.AddListener(StopScrolling);
        }
    }

    private void OnDestroy()
    {
        if (_scrollUpButton != null)
        {
            _scrollUpButton.WhenSelect.RemoveListener(ScrollUp);
            _scrollUpButton.WhenUnselect.RemoveListener(StopScrolling);
        }

        if (_scrollDownButton != null)
        {
            _scrollDownButton.WhenSelect.RemoveListener(ScrollDown);
            _scrollDownButton.WhenUnselect.RemoveListener(StopScrolling);
        }
    }

    // Letting go of the bubble hides this menu, and a menu that disappears mid-hold
    // never delivers its WhenUnselect. Without this the direction would still be set
    // the next time the menu opens, and the text would scroll on its own.
    private void OnDisable()
    {
        _direction = 0f;
    }

    /// <summary>Scrolls the text toward the TOP for as long as the button is held.</summary>
    public void ScrollUp(PointerEvent evt)
    {
        _direction = 1f;
        if (_logDiagnostics) Debug.Log($"{nameof(TextScrollControls)}: SCROLL UP held.", this);
    }

    /// <summary>Scrolls the text toward the BOTTOM for as long as the button is held.</summary>
    public void ScrollDown(PointerEvent evt)
    {
        _direction = -1f;
        if (_logDiagnostics) Debug.Log($"{nameof(TextScrollControls)}: SCROLL DOWN held.", this);
    }

    /// <summary>Stops scrolling. Hooked to the release event of both buttons.</summary>
    public void StopScrolling(PointerEvent evt)
    {
        _direction = 0f;
        if (_logDiagnostics) Debug.Log($"{nameof(TextScrollControls)}: released.", this);
    }

    private void Update()
    {
        if (_direction == 0f || _scrollRect == null) return;

        RectTransform content = _scrollRect.content;
        RectTransform viewport = _scrollRect.viewport;
        if (content == null || viewport == null) return;

        // A transcription shorter than the window has nothing to scroll, and dividing
        // by that height below would blow up to infinity.
        float scrollableHeight = content.rect.height - viewport.rect.height;
        if (scrollableHeight <= 0f)
        {
            if (_logDiagnostics && !_warnedNothingToScroll)
            {
                _warnedNothingToScroll = true;
                Debug.LogWarning(
                    $"{nameof(TextScrollControls)}: nothing to scroll. Content is " +
                    $"{content.rect.height:0.#} tall and the viewport is {viewport.rect.height:0.#}. " +
                    "The Content needs a ContentSizeFitter so it grows past the window as " +
                    "text is added, otherwise there is never any overflow to scroll through.", this);
            }
            return;
        }

        // verticalNormalizedPosition runs 0 (bottom) to 1 (top), so a fixed step in it
        // would race through a short transcription and crawl through a long one.
        // Dividing by the scrollable height converts a constant units-per-second into
        // that 0-1 space, which keeps the speed the user sees the same either way.
        float delta = (_unitsPerSecond * Time.deltaTime) / scrollableHeight;

        // The Scroll View has inertia and elastic movement enabled, so it keeps
        // applying its own leftover velocity every frame. Clearing it stops that from
        // dragging the position back against the value set just below.
        _scrollRect.velocity = Vector2.zero;
        _scrollRect.verticalNormalizedPosition =
            Mathf.Clamp01(_scrollRect.verticalNormalizedPosition + _direction * delta);
    }
}
