using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// Tap = one step; press-and-hold = jump straight to the end. Used by the run-timer speed stepper
/// so the dev fast-forward tiers (10/20/30×) stay one gesture away without adding a second control.
///
/// Timing is deliberately on <see cref="Time.unscaledTime"/>: this button drives Time.timeScale, so
/// scaled time would make the hold threshold arrive 30× sooner at the top of the ladder.
/// </summary>
[RequireComponent(typeof(RectTransform))]
public class HoldRepeatButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
{
    /// <summary>Seconds of hold before it stops being a tap and becomes a jump.</summary>
    public float holdSeconds = 0.4f;

    public System.Action OnTap;   // fired on release, only if the hold threshold wasn't reached
    public System.Action OnHold;  // fired once, the moment the threshold is crossed

    private bool pressed;
    private bool jumped;
    private float downTime;

    private void Update()
    {
        if (!pressed || jumped) return;
        if (Time.unscaledTime - downTime < holdSeconds) return;
        jumped = true;              // fire once per press, not every frame
        OnHold?.Invoke();
    }

    public void OnPointerDown(PointerEventData _)
    {
        pressed = true;
        jumped = false;
        downTime = Time.unscaledTime;
    }

    public void OnPointerUp(PointerEventData _)
    {
        if (pressed && !jumped) OnTap?.Invoke();
        pressed = false;
    }

    // Dragging off the button cancels — no tap, no jump.
    public void OnPointerExit(PointerEventData _) => pressed = false;

    private void OnDisable() { pressed = false; jumped = false; }
}
