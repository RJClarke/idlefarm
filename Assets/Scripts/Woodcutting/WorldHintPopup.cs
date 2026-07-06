using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// A small world-space hint box that appears at a point, holds briefly, then fades out and
/// destroys itself. Lives in the world (not the HUD) so it sits at the object it was spawned on —
/// the Woods uses it to say "You need to buy an axe first." above a tapped tree.
///
/// Fully self-constructing (see <see cref="Create"/>): a world-space Canvas with a dark, rounded
/// 9-sliced background that auto-sizes to the text. No prefab or scene wiring required; the spawner
/// destroys the previous instance to "refresh" it on a repeated tap.
/// </summary>
public class WorldHintPopup : MonoBehaviour
{
    private CanvasGroup group;

    /// <summary>Build a hint at worldPos (+offset), show text, hold, fade out, then self-destroy.</summary>
    public static WorldHintPopup Create(Vector3 worldPos, string text,
        float holdSeconds = 1.4f, float fadeSeconds = 0.6f, float worldScale = 0.035f)
    {
        var go = new GameObject("AxeHintPopup");
        var hint = go.AddComponent<WorldHintPopup>();
        hint.Build(text, worldScale);
        go.transform.position = worldPos + new Vector3(0f, 1.4f, 0f);
        hint.Play(holdSeconds, fadeSeconds);
        return hint;
    }

    private void Build(string text, float worldScale)
    {
        var canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.sortingOrder = 500; // draw above world sprites (trees, terrain)

        group = gameObject.AddComponent<CanvasGroup>();

        // Root sizes itself to the text (+ padding) and carries the dark rounded background with
        // a 1px brown stroke (colors are baked into the sprite, so tint stays white).
        var bg = gameObject.AddComponent<Image>();
        bg.sprite = BoxSprite();
        bg.type = Image.Type.Sliced;
        bg.color = Color.white;

        var layout = gameObject.AddComponent<HorizontalLayoutGroup>();
        layout.padding = new RectOffset(20, 20, 14, 14);
        layout.childAlignment = TextAnchor.MiddleCenter;

        var fitter = gameObject.AddComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        var rt = (RectTransform)transform;
        rt.localScale = Vector3.one * worldScale;

        var labelGO = new GameObject("Label");
        labelGO.transform.SetParent(transform, false);
        var label = labelGO.AddComponent<TextMeshProUGUI>();
        label.text = text;
        label.fontSize = 28f;
        label.color = new Color32(0xF0, 0xE6, 0xD6, 0xFF); // warm off-white
        label.alignment = TextAlignmentOptions.Center;
        label.enableWordWrapping = false;
        label.raycastTarget = false;
    }

    // Procedurally built once, then reused: a pixel-art rounded rect (2px corner radius) filled dark
    // brown with a 1px brown stroke, 9-sliced (corner region preserved, 2px middle stretches) so it
    // scales to any text width without distorting the border or corners.
    private static Sprite cachedBox;
    private static Sprite BoxSprite()
    {
        if (cachedBox != null) return cachedBox;

        const int radius = 4;   // corner radius, px
        const int border = 4;   // stroke thickness, px
        int corner = radius + border + 1;   // 9-slice corner region must contain the rounded stroke
        int size = corner * 2 + 2;          // + a 2px stretchable middle
        Color32 fill = new Color32(0x1C, 0x11, 0x0A, 0xF2);   // near-black dark brown
        Color32 stroke = new Color32(0x5A, 0x37, 0x14, 0xFF); // game UI brown

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

        cachedBox = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0,
                                  SpriteMeshType.FullRect, new Vector4(corner, corner, corner, corner));
        return cachedBox;
    }

    private void Play(float holdSeconds, float fadeSeconds)
    {
        group.alpha = 1f;
        LeanTween.value(gameObject, 1f, 0f, fadeSeconds)
            .setDelay(holdSeconds)
            .setOnUpdate((float a) => { if (group != null) group.alpha = a; })
            .setOnComplete(() => { if (this != null) Destroy(gameObject); });
    }
}
