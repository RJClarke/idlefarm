using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// A small world-space hint box that appears at a point, holds briefly, then fades out and
/// destroys itself. Lives in the world (not the HUD) so it sits at the object it was spawned on —
/// the Woods uses it to say "You need to buy an axe first." above a tapped tree.
///
/// The look is a <b>prefab</b> at <c>Resources/WorldHintPopup</c> (a "style carrier"): the canvas,
/// rounded 9-slice background, and label are built at runtime from the serialized fields below, so
/// tuning the font, colors, size, padding, and timing is a one-place inspector edit on the prefab.
/// <see cref="Create"/> instantiates that prefab; if it's missing it falls back to a code-built
/// instance with these same defaults, so the hint never hard-breaks.
/// </summary>
public class WorldHintPopup : MonoBehaviour
{
    [Header("Text")]
    [Tooltip("Pixel font for the hint. Pick one from Assets/Fonts/UITK SDF (a different face than the " +
             "Name-the-Farm banner, which uses Munro). Left null falls back to the TMP default.")]
    [SerializeField] private TMP_FontAsset font;
    [SerializeField] private float fontSize = 28f;
    [SerializeField] private Color textColor = new Color32(0xF0, 0xE6, 0xD6, 0xFF); // warm off-white

    [Header("Box")]
    [SerializeField] private Color boxFill = new Color32(0x1C, 0x11, 0x0A, 0xF2);   // near-black dark brown
    [SerializeField] private Color boxStroke = new Color32(0x5A, 0x37, 0x14, 0xFF); // game UI brown
    [SerializeField] private int cornerRadius = 4;
    [SerializeField] private int borderThickness = 4;
    [Tooltip("Inner horizontal padding around the text (px, before world scale).")]
    [SerializeField] private int paddingHorizontal = 20;
    [Tooltip("Inner vertical padding around the text (px, before world scale).")]
    [SerializeField] private int paddingVertical = 14;

    [Header("Placement & timing")]
    [SerializeField] private float worldScale = 0.035f;
    [Tooltip("Vertical offset above the spawn point so the box floats over the tapped object.")]
    [SerializeField] private float yOffset = 1.4f;
    [SerializeField] private float holdSeconds = 1.4f;
    [SerializeField] private float fadeSeconds = 0.6f;

    private CanvasGroup group;

    /// <summary>Build a hint at worldPos. By default it holds, fades, and self-destroys. Pass
    /// persistent = true for a bubble the caller owns and repositions (e.g. the fishing bite
    /// indicator that must stay above the bobber until the fish is reeled in); it never fades or
    /// self-destroys, so the caller must Destroy it. Instantiates the Resources/WorldHintPopup
    /// prefab so its serialized style drives the look; falls back to a bare, default-styled
    /// instance if the prefab is absent.</summary>
    public static WorldHintPopup Create(Vector3 worldPos, string text, bool persistent = false)
        => Spawn(worldPos, text, null, null, null, persistent);

    /// <summary>Icon bubble variant: a sprite instead of text, with optional fill/stroke override —
    /// e.g. the fishing bite bubble (cream fill, charcoal stroke, the hooked fish's icon). Styled
    /// as a true speech bubble: extra corner rounding plus a pointy tail off the bottom.</summary>
    public static WorldHintPopup CreateIcon(Vector3 worldPos, Sprite icon,
        Color? fill = null, Color? stroke = null, bool persistent = false)
        => Spawn(worldPos, null, icon, fill, stroke, persistent);

    private static WorldHintPopup Spawn(Vector3 worldPos, string text, Sprite icon,
        Color? fill, Color? stroke, bool persistent)
    {
        WorldHintPopup prefab = Resources.Load<WorldHintPopup>("WorldHintPopup");
        WorldHintPopup hint = prefab != null
            ? Instantiate(prefab)
            : new GameObject("WorldHintPopup").AddComponent<WorldHintPopup>();
        hint.gameObject.name = "WorldHintPopup";

        if (fill.HasValue) hint.boxFill = fill.Value;
        if (stroke.HasValue) hint.boxStroke = stroke.Value;
        if (icon != null) hint.cornerRadius = 10; // speech bubble reads rounder than the hint box

        hint.Build(text, icon);
        // Non-persistent hints lift by yOffset to float over the tapped object; a persistent hint
        // sits exactly where the owner places it (the owner repositions it every frame).
        hint.transform.position = persistent ? worldPos : worldPos + new Vector3(0f, hint.yOffset, 0f);
        if (persistent) { if (hint.group != null) hint.group.alpha = 1f; }
        else hint.Play();
        return hint;
    }

    private void Build(string text, Sprite icon = null)
    {
        var canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.sortingOrder = 500; // draw above world sprites (trees, terrain)

        group = gameObject.AddComponent<CanvasGroup>();

        // Root sizes itself to the text (+ padding) and carries the dark rounded background with
        // a brown stroke (colors are baked into the sprite, so tint stays white).
        var bg = gameObject.AddComponent<Image>();
        bg.sprite = BuildBoxSprite();
        bg.type = Image.Type.Sliced;
        bg.color = Color.white;

        var layout = gameObject.AddComponent<HorizontalLayoutGroup>();
        layout.padding = new RectOffset(paddingHorizontal, paddingHorizontal, paddingVertical, paddingVertical);
        layout.childAlignment = TextAnchor.MiddleCenter;

        var fitter = gameObject.AddComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        var rt = (RectTransform)transform;
        rt.localScale = Vector3.one * worldScale;

        if (icon != null)
        {
            // Icon bubble: a sprite instead of text (e.g. the hooked fish above the bobber).
            var iconGO = new GameObject("Icon");
            iconGO.transform.SetParent(transform, false);
            var img = iconGO.AddComponent<Image>();
            img.sprite = icon;
            img.preserveAspect = true;
            img.raycastTarget = false;
            var le = iconGO.AddComponent<LayoutElement>();
            le.preferredWidth = 44f;
            le.preferredHeight = 44f;

            AddSpeechTail();
            return;
        }

        var labelGO = new GameObject("Label");
        labelGO.transform.SetParent(transform, false);
        var label = labelGO.AddComponent<TextMeshProUGUI>();
        if (font != null) label.font = font;
        label.text = text;
        label.fontSize = fontSize;
        label.color = textColor;
        label.alignment = TextAlignmentOptions.Center;
        label.enableWordWrapping = false;
        label.raycastTarget = false;
    }

    private Sprite BuildBoxSprite() => BuildBoxSprite(boxFill, boxStroke, cornerRadius, borderThickness);

    // A pixel-art rounded rect filled with `fill` and a `stroke` ring, 9-sliced (corner region
    // preserved, 2px middle stretches) so it scales to any content size without distorting the
    // border. Public + static so other speech-bubble UI (LakeBiteAlert) shares the exact look.
    public static Sprite BuildBoxSprite(Color32 fill, Color32 stroke, int cornerRadius, int borderThickness)
    {
        int radius = Mathf.Max(0, cornerRadius);
        int border = Mathf.Max(1, borderThickness);
        int corner = radius + border + 1;   // 9-slice corner region must contain the rounded stroke
        int size = corner * 2 + 2;          // + a 2px stretchable middle

        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp,
        };
        var px = new Color32[size * size];
        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            // Distance from the rounded-rect boundary via the standard inset-rect SDF: clamp the
            // pixel into the rect inset by `radius`, then measure how far it sits from that core.
            float pxc = x + 0.5f, pyc = y + 0.5f;
            float cx = Mathf.Clamp(pxc, radius, size - radius);
            float cy = Mathf.Clamp(pyc, radius, size - radius);
            float d = Mathf.Sqrt((pxc - cx) * (pxc - cx) + (pyc - cy) * (pyc - cy));

            px[y * size + x] = d > radius ? new Color32(0, 0, 0, 0)      // outside the rounded corner
                             : d > radius - border ? stroke              // stroke ring
                             : fill;                                     // interior
        }
        tex.SetPixels32(px);
        tex.Apply();

        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0,
                             SpriteMeshType.FullRect, new Vector4(corner, corner, corner, corner));
    }

    // The speech-bubble tail: a small down-pointing triangle hung under the box's bottom-center.
    // Its top rows are pure fill and overlap the box's bottom stroke band (drawn after it, so on
    // top), which visually opens the border and merges tail + box into one bubble.
    private void AddSpeechTail()
    {
        var tailGO = new GameObject("Tail");
        tailGO.transform.SetParent(transform, false);
        var img = tailGO.AddComponent<Image>();
        img.sprite = BuildTailSprite();
        img.raycastTarget = false;
        // Decoration only — must not participate in the size-fitted layout group.
        var le = tailGO.AddComponent<LayoutElement>();
        le.ignoreLayout = true;

        var rt = (RectTransform)tailGO.transform;
        rt.anchorMin = new Vector2(0.5f, 0f);
        rt.anchorMax = new Vector2(0.5f, 0f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.sizeDelta = new Vector2(24f, 16f);
        // Overlap the box's stroke so the tail's fill rows open the border.
        rt.anchoredPosition = new Vector2(0f, borderThickness);
    }

    private Sprite BuildTailSprite()
        => BuildTailSprite(boxFill, boxStroke, borderThickness, pointRight: false, length: 16, breadth: 24);

    /// <summary>
    /// A pixel-art speech-bubble tail: a triangle pointing down (or right) whose base rows/columns
    /// are pure fill — overlap them onto the box's stroke band and the border visually opens into
    /// the tail. Static so other speech-bubble UI (LakeBiteAlert) shares the exact look.
    /// </summary>
    public static Sprite BuildTailSprite(Color32 fill, Color32 stroke, int borderThickness,
        bool pointRight, int length, int breadth)
    {
        int border = Mathf.Max(1, borderThickness);
        int w = pointRight ? length : breadth;
        int h = pointRight ? breadth : length;

        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp,
        };
        var px = new Color32[w * h];
        for (int y = 0; y < h; y++)
        for (int x = 0; x < w; x++)
        {
            // Distance from the tip (bottom row for Down, rightmost column for Right) and
            // across the taper axis; the half-breadth shrinks toward the tip.
            int fromTip = pointRight ? (w - 1 - x) : y;
            float across = pointRight ? Mathf.Abs(y + 0.5f - h * 0.5f) : Mathf.Abs(x + 0.5f - w * 0.5f);
            float halfBreadth = (fromTip + 1f) * (breadth * 0.5f) / length;
            bool inside = across <= halfBreadth;
            bool strokePx = inside && (across > halfBreadth - border || fromTip < border);
            px[y * w + x] = !inside ? new Color32(0, 0, 0, 0) : strokePx ? stroke : fill;
        }
        tex.SetPixels32(px);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect);
    }

    private void Play()
    {
        if (group == null) return;
        group.alpha = 1f;
        LeanTween.value(gameObject, 1f, 0f, fadeSeconds)
            .setDelay(holdSeconds)
            .setOnUpdate((float a) => { if (group != null) group.alpha = a; })
            .setOnComplete(() => { if (this != null) Destroy(gameObject); });
    }
}
