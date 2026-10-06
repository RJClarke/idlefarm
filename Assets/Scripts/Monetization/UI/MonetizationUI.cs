using System;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>Code-built UITK helpers shared by the Store, chest reveal, pass pitch and dev overlay.
/// Same approach as the Almanac: clone the Barn's PanelSettings, reuse its board frame and fonts.
/// Game browns and gold only (no blue, no accent stripes).</summary>
public static class MonetizationUI
{
    public static readonly Color Walnut = new Color(0.35f, 0.22f, 0.10f);
    public static readonly Color WalnutDark = new Color(0.24f, 0.15f, 0.07f);
    public static readonly Color Cream = new Color(0.98f, 0.93f, 0.82f);
    public static readonly Color Gold = new Color(0.93f, 0.71f, 0.27f);
    public static readonly Color Ink = new Color(0.23f, 0.15f, 0.08f);
    public static readonly Color Muted = new Color(0.50f, 0.44f, 0.38f);

    /// <summary>Adds a UIDocument to <paramref name="host"/> at <paramref name="sortOrder"/> and returns its
    /// full-screen root (picking Ignore until a popup opens). Null when the Barn panel settings are missing.</summary>
    public static VisualElement CreateRoot(MonoBehaviour host, int sortOrder, string name, out PanelSettings runtimeSettings)
    {
        runtimeSettings = null;
        BarnPopupUITK barn = BarnPopupUITK.Instance;
        PanelSettings source = barn != null ? barn.SourcePanelSettings : null;
        if (source == null) { Debug.LogWarning($"[{name}] No panel settings to clone (BarnPopupUITK missing)."); return null; }

        runtimeSettings = UnityEngine.Object.Instantiate(source);
        runtimeSettings.name = name + "PanelSettings (runtime)";
        runtimeSettings.sortingOrder = sortOrder;
        var document = host.gameObject.AddComponent<UIDocument>();
        document.enabled = false;
        document.panelSettings = runtimeSettings;
        document.enabled = true;

        VisualElement root = document.rootVisualElement;
        root.pickingMode = PickingMode.Ignore;
        root.style.height = Length.Percent(100);
        return root;
    }

    public static void Fill(VisualElement e)
    {
        e.style.position = Position.Absolute;
        e.style.left = 0; e.style.right = 0; e.style.top = 0; e.style.bottom = 0;
    }

    public static void Radius(VisualElement e, float r)
    {
        e.style.borderTopLeftRadius = r; e.style.borderTopRightRadius = r;
        e.style.borderBottomLeftRadius = r; e.style.borderBottomRightRadius = r;
    }

    public static void NoBorder(VisualElement e)
    {
        e.style.borderTopWidth = 0; e.style.borderBottomWidth = 0;
        e.style.borderLeftWidth = 0; e.style.borderRightWidth = 0;
    }

    /// <summary>The Barn's 9-sliced wood board with its interior wash, or a plain parchment block.</summary>
    public static VisualElement Card()
    {
        var card = new VisualElement();
        BarnPopupUITK barn = BarnPopupUITK.Instance;
        int border = barn != null ? Mathf.RoundToInt(barn.FrameSlice * barn.FrameSliceScale) : 0;
        card.style.paddingLeft = border + 22; card.style.paddingRight = border + 22;
        card.style.paddingTop = border + 20; card.style.paddingBottom = border + 20;
        if (barn != null && barn.BoardFrame != null)
        {
            card.style.backgroundImage = new StyleBackground(barn.BoardFrame);
            card.style.unitySliceLeft = barn.FrameSlice; card.style.unitySliceRight = barn.FrameSlice;
            card.style.unitySliceTop = barn.FrameSlice; card.style.unitySliceBottom = barn.FrameSlice;
            card.style.unitySliceScale = barn.FrameSliceScale;
            if (barn.InteriorWash.a > 0f)
            {
                var wash = new VisualElement { pickingMode = PickingMode.Ignore };
                wash.style.position = Position.Absolute;
                wash.style.left = border; wash.style.right = border; wash.style.top = border; wash.style.bottom = border;
                wash.style.backgroundColor = barn.InteriorWash;
                Radius(wash, 6);
                card.Add(wash);
            }
        }
        else
        {
            card.style.backgroundColor = new Color(0.70f, 0.60f, 0.43f);
            Radius(card, 14);
        }
        return card;
    }

    public static Button BrownButton(string text, Action onClick, int fontSize = 30)
    {
        var b = new Button(onClick) { text = text };
        b.style.fontSize = fontSize;
        b.style.unityFontStyleAndWeight = FontStyle.Bold;
        b.style.color = Cream;
        b.style.backgroundColor = Walnut;
        NoBorder(b);
        Radius(b, 12);
        b.style.height = fontSize * 2.1f;
        b.style.paddingLeft = 22; b.style.paddingRight = 22;
        b.style.marginLeft = 0; b.style.marginRight = 0;
        return b;
    }

    public static Label Text(string text, int size, Color color, bool bold = false)
    {
        var l = new Label(text);
        l.style.fontSize = size;
        l.style.color = color;
        l.style.whiteSpace = WhiteSpace.Normal;
        if (bold) l.style.unityFontStyleAndWeight = FontStyle.Bold;
        l.pickingMode = PickingMode.Ignore;
        return l;
    }

    public static VisualElement Icon(Sprite sprite, float size)
    {
        var e = new VisualElement { pickingMode = PickingMode.Ignore };
        e.style.width = size; e.style.height = size;
        e.style.flexShrink = 0;
        if (sprite != null)
        {
            e.style.backgroundImage = new StyleBackground(sprite);
            e.style.backgroundSize = new BackgroundSize(BackgroundSizeType.Contain);
        }
        return e;
    }

    public static UnityEngine.TextCore.Text.FontAsset TitleFont
    {
        get
        {
            MonetizationArt art = MonetizationArt.Instance;
            if (art != null && art.numberFont != null) return art.numberFont;
            return BarnPopupUITK.Instance != null ? BarnPopupUITK.Instance.TitleFont : null;
        }
    }

    /// <summary>The closed-chest sprite, for toasts about the gift or the Store.</summary>
    public static Sprite ChestIcon => MonetizationArt.Instance != null ? MonetizationArt.Instance.chestClosed : null;

    /// <summary>Black backdrop that LOOKS <paramref name="perceived"/> darker. UI alpha blends in linear
    /// light, so a raw 0.78 only darkens ~40% (memory: linear-colour dim gotcha; same maths as TutorialManager).</summary>
    public static Color Dim(float perceived)
    {
        float a = perceived;
        if (QualitySettings.activeColorSpace == ColorSpace.Linear) a = 1f - Mathf.Pow(Mathf.Clamp01(1f - perceived), 2.2f);
        return new Color(0f, 0f, 0f, a);
    }

    /// <summary>Applies a pixel font. Pixel fonts must never get faux bold (it smears the glyphs).</summary>
    public static void Font(VisualElement e, UnityEngine.TextCore.Text.FontAsset f)
    {
        if (f == null) return;
        e.style.unityFontDefinition = new StyleFontDefinition(f);
        e.style.unityFontStyleAndWeight = FontStyle.Normal;
    }

    /// <summary>A label in the title pixel font at a whole multiple of its bake size (41 for Munro Pixel 41),
    /// the only sizes where it draws crisp.</summary>
    public static Label PixelText(string text, int multiple, Color color)
    {
        UnityEngine.TextCore.Text.FontAsset f = TitleFont;
        int bake = f != null && f.faceInfo.pointSize > 0 ? Mathf.RoundToInt(f.faceInfo.pointSize) : 41;
        Label l = Text(text, bake * Mathf.Max(1, multiple), color);
        Font(l, f);
        return l;
    }

    /// <summary>The Inbox's slim walnut scrollbar (InboxPopupUITK.uss), applied in code: no arrow buttons,
    /// a faint track, and a rounded walnut thumb.</summary>
    public static void WalnutScroller(ScrollView scroll)
    {
        Scroller s = scroll.verticalScroller;
        s.style.width = 10; s.style.marginLeft = 6;
        NoBorder(s);
        s.style.backgroundColor = Color.clear;
        s.lowButton.style.display = DisplayStyle.None;
        s.highButton.style.display = DisplayStyle.None;
        VisualElement tracker = s.slider.Q(className: "unity-base-slider__tracker");
        if (tracker != null)
        {
            NoBorder(tracker); Radius(tracker, 5);
            tracker.style.backgroundColor = new Color(90f / 255f, 55f / 255f, 20f / 255f, 0.18f);
        }
        VisualElement dragger = s.slider.Q(className: "unity-base-slider__dragger");
        if (dragger != null)
        {
            NoBorder(dragger); Radius(dragger, 5);
            dragger.style.left = 0; dragger.style.right = 0; dragger.style.width = 10;
            dragger.style.backgroundColor = new Color(120f / 255f, 90f / 255f, 45f / 255f);
        }
    }

    /// <summary>The Farmer's Pass art (no-ads icon, or the user's passArt) with a "+500" gem badge in the corner,
    /// so the bonus gems read in the imagery, not just the copy.</summary>
    public static VisualElement PassIcon(float size, int gems)
    {
        var box = new VisualElement { pickingMode = PickingMode.Ignore };
        box.style.width = size; box.style.height = size; box.style.flexShrink = 0;
        MonetizationArt art = MonetizationArt.Instance;
        Sprite hero = art != null && art.passArt != null ? art.passArt : ChestIcon;
        VisualElement icon = Icon(hero, size);
        box.Add(icon);
        if (gems <= 0) return box;

        var badge = new VisualElement { pickingMode = PickingMode.Ignore };
        badge.style.position = Position.Absolute;
        badge.style.right = -size * 0.08f; badge.style.bottom = -size * 0.04f;
        badge.style.flexDirection = FlexDirection.Row;
        badge.style.alignItems = Align.Center;
        badge.style.backgroundColor = Walnut;
        Radius(badge, size * 0.12f);
        badge.style.paddingLeft = 6; badge.style.paddingRight = 10; badge.style.paddingTop = 2; badge.style.paddingBottom = 2;
        badge.Add(Icon(art != null ? art.gemIcon : null, size * 0.26f));
        Label amount = Text("+" + gems.ToString("N0"), Mathf.RoundToInt(size * 0.2f), Cream, bold: true);
        amount.style.marginLeft = 2;
        badge.Add(amount);
        box.Add(badge);
        return box;
    }

    public static string Mmss(double seconds)
    {
        int s = Mathf.Max(0, Mathf.CeilToInt((float)seconds));
        return $"{s / 60:00}:{s % 60:00}";
    }
}
