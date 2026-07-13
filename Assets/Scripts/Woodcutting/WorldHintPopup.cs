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

    /// <summary>Build a hint at worldPos, show text, hold, fade out, then self-destroy. Instantiates the
    /// Resources/WorldHintPopup prefab so its serialized style drives the look; falls back to a bare,
    /// default-styled instance if the prefab is absent.</summary>
    /// <summary>Build a hint at worldPos. By default it holds, fades, and self-destroys. Pass
    /// persistent = true for a bubble the caller owns and repositions (e.g. the fishing bite
    /// indicator that must stay above the bobber until the fish is reeled in); it never fades or
    /// self-destroys, so the caller must Destroy it.</summary>
    public static WorldHintPopup Create(Vector3 worldPos, string text, bool persistent = false)
    {
        WorldHintPopup prefab = Resources.Load<WorldHintPopup>("WorldHintPopup");
        WorldHintPopup hint = prefab != null
            ? Instantiate(prefab)
            : new GameObject("WorldHintPopup").AddComponent<WorldHintPopup>();
        hint.gameObject.name = "AxeHintPopup";

        hint.Build(text);
        // Non-persistent hints lift by yOffset to float over the tapped object; a persistent hint
        // sits exactly where the owner places it (the owner repositions it every frame).
        hint.transform.position = persistent ? worldPos : worldPos + new Vector3(0f, hint.yOffset, 0f);
        if (persistent) { if (hint.group != null) hint.group.alpha = 1f; }
        else hint.Play();
        return hint;
    }

    private void Build(string text)
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

    // A pixel-art rounded rect filled with boxFill and a boxStroke ring, 9-sliced (corner region
    // preserved, 2px middle stretches) so it scales to any text width without distorting the border.
    private Sprite BuildBoxSprite()
    {
        int radius = Mathf.Max(0, cornerRadius);
        int border = Mathf.Max(1, borderThickness);
        int corner = radius + border + 1;   // 9-slice corner region must contain the rounded stroke
        int size = corner * 2 + 2;          // + a 2px stretchable middle
        Color32 fill = boxFill;
        Color32 stroke = boxStroke;

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
