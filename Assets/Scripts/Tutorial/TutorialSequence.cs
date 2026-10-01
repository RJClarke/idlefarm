using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// One step of a guided tutorial: tooltip text, how it advances (see TutorialAdvance), and an
/// optional spotlight target. The target is a delegate returning a SCREEN-space rect
/// (bottom-left origin, pixels) so a step can track uGUI, UI Toolkit, or world-space objects
/// alike — resolved every frame, so moving/animating targets stay spotlighted.
/// A null/invalid target dims the whole screen and centers the tooltip.
/// </summary>
public class TutorialStep
{
    public string text;
    public TutorialAdvance advance = TutorialAdvance.TapAnywhere;
    public string eventId;                 // required when advance == GameEvent
    public Func<Rect?> getTargetScreenRect;
    public float spotlightPadding = 14f;   // screen px added around the target rect
    /// <summary>Optional perceived dim for this step (0-1); null = the manager's default. How-to
    /// tips over an open menu use a lighter dim so the menu they explain stays readable.</summary>
    public float? dimOpacity;
    /// <summary>Untargeted steps only: sit the card at the bottom of the screen instead of the
    /// middle, so a how-to tip about an open menu doesn't cover the menu it's explaining.</summary>
    public bool cardAtBottom;
}

/// <summary>
/// A named, ordered set of tutorial steps. The id is what the completion ledger persists —
/// once completed it never auto-runs again. Unlock-time tutorials ("You just unlocked the
/// Cannery") are ordinary sequences whose TryStart happens in the unlock code path.
/// </summary>
public class TutorialSequence
{
    public string id;
    public List<TutorialStep> steps = new List<TutorialStep>();
}

/// <summary>
/// Helpers producing screen-space (bottom-left origin, pixel) rect getters for the common
/// target kinds. Use these when authoring sequences.
/// </summary>
public static class TutorialTargets
{
    /// <summary>Fixed normalized viewport rect (x,y,w,h in 0..1, bottom-left origin).</summary>
    public static Func<Rect?> Normalized(float x, float y, float w, float h)
    {
        return () => new Rect(x * Screen.width, y * Screen.height, w * Screen.width, h * Screen.height);
    }

    /// <summary>A uGUI element (works for Screen Space Overlay and Camera canvases).</summary>
    public static Func<Rect?> FromUGUI(RectTransform rt)
    {
        return () =>
        {
            if (rt == null || !rt.gameObject.activeInHierarchy) return null;
            Canvas canvas = rt.GetComponentInParent<Canvas>();
            Camera cam = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay
                ? canvas.worldCamera : null;
            Vector3[] corners = new Vector3[4];
            rt.GetWorldCorners(corners);
            Vector2 min = new Vector2(float.MaxValue, float.MaxValue);
            Vector2 max = new Vector2(float.MinValue, float.MinValue);
            for (int i = 0; i < 4; i++)
            {
                Vector2 p = cam != null
                    ? (Vector2)cam.WorldToScreenPoint(corners[i])
                    : (Vector2)corners[i];
                min = Vector2.Min(min, p);
                max = Vector2.Max(max, p);
            }
            return new Rect(min, max - min);
        };
    }

    /// <summary>A world-space object by bounds (e.g. a building, the lake sign).</summary>
    public static Func<Rect?> FromWorldBounds(Func<Bounds?> getBounds)
    {
        return () =>
        {
            Camera cam = Camera.main;
            Bounds? maybe = getBounds != null ? getBounds() : null;
            if (cam == null || maybe == null) return null;
            Bounds b = maybe.Value;
            Vector2 min = new Vector2(float.MaxValue, float.MaxValue);
            Vector2 max = new Vector2(float.MinValue, float.MinValue);
            for (int i = 0; i < 8; i++)
            {
                Vector3 corner = new Vector3(
                    (i & 1) == 0 ? b.min.x : b.max.x,
                    (i & 2) == 0 ? b.min.y : b.max.y,
                    (i & 4) == 0 ? b.min.z : b.max.z);
                Vector2 p = cam.WorldToScreenPoint(corner);
                min = Vector2.Min(min, p);
                max = Vector2.Max(max, p);
            }
            return new Rect(min, max - min);
        };
    }

    /// <summary>A UI Toolkit element from any runtime panel (converted panel → screen px).</summary>
    public static Func<Rect?> FromUITK(VisualElement element)
    {
        return () =>
        {
            if (element == null || element.panel == null) return null;
            VisualElement panelRoot = element.panel.visualTree;
            Rect panelBound = panelRoot.worldBound;
            if (panelBound.width <= 0f || panelBound.height <= 0f) return null;
            float sx = Screen.width / panelBound.width;
            float sy = Screen.height / panelBound.height;
            Rect wb = element.worldBound;
            // Panel space is top-left origin; screen rects here are bottom-left origin.
            float screenYTop = wb.y * sy;
            return new Rect(wb.x * sx, Screen.height - screenYTop - wb.height * sy,
                            wb.width * sx, wb.height * sy);
        };
    }
}
