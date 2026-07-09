using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// One-shot editor tool: replaces the emoji/text icons on the top-left HUD buttons with real
/// sprite Images (Icons_Essential), and standardizes every notification dot to a uniform round
/// red dot at the same size + position. Run via Tools > UI > Rebuild Top-Left Icons, then save.
/// Modifies dot GameObjects in place so existing script references (InboxButton.notificationDot,
/// QuestButtonController.notificationDot, the daily-reward count) stay wired.
/// </summary>
public static class TopLeftIconSetup
{
    // Button name -> icon sprite (all from the cohesive Icons_Essential pack), top to bottom.
    private static readonly (string button, string sprite)[] Map =
    {
        ("InboxButton",            "Assets/Sprites/UI/Icons/Icons_Essential/Letter.png"),
        ("DailyRewardChestButton", "Assets/Sprites/UI/Icons/Icons_Essential/ChestTreasure.png"),
        ("QuestButton",            "Assets/Sprites/UI/Icons/Icons_Essential/Document.png"),
        ("InventoryButton",        "Assets/Sprites/UI/Icons/Icons_Essential/Backpack.png"),
        ("PrevRunStatsButton",     "Assets/Sprites/UI/Icons/Icons_Essential/Monitor.png"),
    };

    private const float IconSize = 60f;                 // uniform icon size inside the 90x90 button
    private const float DotSize = 18f;                  // uniform dot diameter
    private static readonly Vector2 DotPos = new Vector2(-8f, -8f); // top-right, inset
    private static readonly Color DotColor = new Color(0.90f, 0.20f, 0.22f, 1f);

    // Old text/emoji icon child names to clear off each button.
    private static readonly string[] OldIconNames = { "EmojiText", "ChestIcon", "QuestIcon", "PrevStatsText", "Icon" };

    [MenuItem("Tools/UI/Rebuild Top-Left Icons")]
    private static void Rebuild()
    {
        var lockup = GameObject.Find("Canvas/TopLeftLockup");
        if (lockup == null) { Debug.LogError("[TopLeftIcons] Canvas/TopLeftLockup not found."); return; }

        Sprite dotSprite = GetOrCreateDotSprite();

        foreach (var (btnName, spritePath) in Map)
        {
            Transform btn = lockup.transform.Find(btnName);
            if (btn == null) { Debug.LogWarning($"[TopLeftIcons] {btnName} not found."); continue; }

            Sprite icon = LoadSprite(spritePath);
            if (icon == null) { Debug.LogWarning($"[TopLeftIcons] sprite missing: {spritePath}"); continue; }

            SetupButton(btn);
            SetupIcon(btn, icon);
            SetupDot(btn, dotSprite);
        }

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        Debug.Log("[TopLeftIcons] Rebuilt 5 icons + dots. Save the scene (Ctrl+S) to persist.");
    }

    private const float ButtonSize = 90f;

    // Make the button itself a uniform square, icon-only click target (transparent background), so
    // all five align in the layout group with the icon centered — matches the Inbox/Stats style.
    private static void SetupButton(Transform btn)
    {
        var rt = btn.GetComponent<RectTransform>();
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(ButtonSize, ButtonSize);

        var img = btn.GetComponent<Image>();
        if (img != null)
        {
            img.sprite = null;
            img.color = new Color(1f, 1f, 1f, 0f); // invisible, still a raycast target for the Button
            img.raycastTarget = true;
        }
    }

    private static void SetupIcon(Transform btn, Sprite icon)
    {
        // Remove any old text/emoji icon child (and its TMP submeshes).
        foreach (string n in OldIconNames)
        {
            Transform old = btn.Find(n);
            if (old != null) Object.DestroyImmediate(old.gameObject);
        }

        var go = new GameObject("Icon", typeof(RectTransform), typeof(Image));
        go.transform.SetParent(btn, false);
        go.transform.SetSiblingIndex(0); // under the dot

        var img = go.GetComponent<Image>();
        img.sprite = icon;
        img.preserveAspect = true;
        img.raycastTarget = false;

        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = new Vector2(IconSize, IconSize);
    }

    private static void SetupDot(Transform btn, Sprite dotSprite)
    {
        Transform dotT = btn.Find("NotificationDot");
        bool created = dotT == null;

        GameObject dot = created
            ? new GameObject("NotificationDot", typeof(RectTransform), typeof(Image))
            : dotT.gameObject;
        if (created) dot.transform.SetParent(btn, false);

        var img = dot.GetComponent<Image>() ?? dot.AddComponent<Image>();
        img.sprite = dotSprite;
        img.color = DotColor;
        img.raycastTarget = false;
        img.type = Image.Type.Simple;

        var rt = dot.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(1f, 1f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = DotPos;
        rt.sizeDelta = new Vector2(DotSize, DotSize);
        dot.transform.SetAsLastSibling(); // on top of the icon

        // Buttons that had no dot get an inactive placeholder (uniform size/pos, no controller yet).
        if (created) dot.SetActive(false);
    }

    // A soft round white dot sprite, generated once into the project so it persists in the scene.
    private static Sprite GetOrCreateDotSprite()
    {
        const string path = "Assets/Sprites/UI/dot_circle.png";
        var existing = AssetDatabase.LoadAssetAtPath<Sprite>(path);
        if (existing != null) return existing;

        const int size = 64;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        float c = size / 2f, r = c - 1f;
        var px = new Color[size * size];
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(c, c));
                px[y * size + x] = new Color(1f, 1f, 1f, Mathf.Clamp01(r - d + 1f));
            }
        tex.SetPixels(px);
        tex.Apply();
        System.IO.File.WriteAllBytes(path, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);

        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.alphaIsTransparency = true;
        importer.mipmapEnabled = false;
        importer.filterMode = FilterMode.Bilinear;
        importer.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }

    private static Sprite LoadSprite(string path)
    {
        var main = AssetDatabase.LoadAssetAtPath<Sprite>(path);
        if (main != null) return main;
        foreach (Object o in AssetDatabase.LoadAllAssetRepresentationsAtPath(path))
            if (o is Sprite s) return s;
        return null;
    }
}
