using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// The little dark help bubble that floats above whatever you tapped (Barn milestones, Almanac
/// pills). Add it to a popup's root so it can float over the card edges without being clipped.
/// Tapping the same thing again hides it; it also hides itself after long enough to read.
/// </summary>
public class FloatingTooltip
{
    public const float MaxWidth = 440f;

    private readonly VisualElement host;
    private readonly VisualElement box;
    private readonly Label label;
    private VisualElement anchor;
    private IVisualElementScheduledItem hideTimer;

    public FloatingTooltip(VisualElement host, string name = "floating-tooltip")
    {
        this.host = host;
        box = new VisualElement { name = name };
        box.style.position = Position.Absolute;
        box.style.display = DisplayStyle.None;
        box.style.maxWidth = MaxWidth;
        box.style.paddingLeft = 22; box.style.paddingRight = 22;
        box.style.paddingTop = 16; box.style.paddingBottom = 16;
        box.style.backgroundColor = new Color(0.16f, 0.10f, 0.05f, 0.95f);
        box.style.borderTopLeftRadius = 14; box.style.borderTopRightRadius = 14;
        box.style.borderBottomLeftRadius = 14; box.style.borderBottomRightRadius = 14;
        // Centres on the anchor with its bottom edge at the anchor's top, without measuring itself
        // first (translate percentages are relative to the tooltip's own size).
        box.style.translate = new StyleTranslate(new Translate(Length.Percent(-50), Length.Percent(-100)));
        box.pickingMode = PickingMode.Ignore;
        host.Add(box);

        label = new Label();
        label.style.color = Color.white;
        label.style.fontSize = 24;
        label.style.whiteSpace = WhiteSpace.Normal;
        label.style.unityTextAlign = TextAnchor.MiddleCenter;
        box.Add(label);
    }

    public bool IsShowing => box.style.display == DisplayStyle.Flex;

    /// <summary>Shows the bubble above <paramref name="target"/>, or hides it if it's already
    /// showing for that same element.</summary>
    public void Toggle(VisualElement target, string text)
    {
        if (anchor == target && IsShowing) { Hide(); return; }

        anchor = target;
        label.text = text;
        box.style.display = DisplayStyle.Flex;
        box.BringToFront();

        Vector2 topCentre = new Vector2(target.worldBound.center.x, target.worldBound.yMin);
        Vector2 local = host.WorldToLocal(topCentre);
        // Keep the centred bubble on screen for things near either edge. Clamps against the max
        // width because the bubble's real width isn't laid out yet on the frame it appears.
        float halfMax = MaxWidth * 0.5f + 12f;
        float hostWidth = host.layout.width;
        if (!float.IsNaN(hostWidth) && hostWidth > halfMax * 2f)
            local.x = Mathf.Clamp(local.x, halfMax, hostWidth - halfMax);
        box.style.left = local.x;
        box.style.top = local.y - 10f;

        hideTimer?.Pause();
        // Longer help stays up longer, so it can actually be read before it goes.
        long hideMs = Mathf.Clamp(1500 + text.Length * 45, 3500, 9000);
        hideTimer = box.schedule.Execute(Hide).StartingIn(hideMs);
    }

    public void Hide()
    {
        box.style.display = DisplayStyle.None;
        anchor = null;
    }
}
