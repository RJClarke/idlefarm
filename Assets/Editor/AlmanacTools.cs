using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Editor helpers for the Farmer's Almanac (spec 2026-09-28-farmers-almanac-design.md).
/// Asset edits go through SerializedObject so Unity's in-memory copies never disagree with disk.
/// </summary>
public static class AlmanacTools
{
    // First-draft crop personalities (deer appetite, crow appetite, thirst). Retune in the Inspector;
    // re-running this menu overwrites those three fields on all nine crops with these drafts.
    private static readonly Dictionary<string, (float deer, float crow, float thirst)> Drafts =
        new Dictionary<string, (float, float, float)>
        {
            { "Radish",       (1.00f, 0.50f, 0.80f) },
            { "Carrot",       (1.50f, 0.50f, 1.00f) },
            { "Corn",         (0.75f, 1.50f, 1.25f) },
            { "Tomato",       (1.25f, 1.00f, 1.25f) },
            { "Strawberry",   (1.00f, 1.50f, 1.00f) },
            { "Blueberry",    (0.75f, 1.50f, 0.90f) },
            { "Green Beans",  (1.50f, 0.75f, 0.80f) },
            { "Green Pepper", (0.50f, 0.75f, 1.00f) },
            { "Red Pepper",   (0.25f, 0.50f, 1.25f) },
        };

    private const string BookIconPath = "Assets/Sprites/UI/Icons/Raven/Misc_BookBlue.png";

    /// <summary>Bakes the HUD Almanac button as a real scene object (visible in edit mode): a copy of
    /// the mailbox button in Canvas/TopLeftLockup with its mail logic + red dot stripped, the book
    /// icon, and an AlmanacButton. Idempotent — re-running updates the existing button.</summary>
    [MenuItem("Farm Game/Almanac/Bake HUD Button")]
    public static void BakeHudButton()
    {
        var inbox = Object.FindFirstObjectByType<InboxButton>(FindObjectsInactive.Include);
        if (inbox == null) { Debug.LogError("[Almanac] No InboxButton in the open scene to copy."); return; }
        Transform stack = inbox.transform.parent;

        Transform existing = stack.Find("AlmanacButton");
        GameObject go = existing != null ? existing.gameObject : Object.Instantiate(inbox.gameObject, stack);
        go.name = "AlmanacButton";
        Undo.RegisterCreatedObjectUndo(go, "Bake Almanac Button");

        foreach (var ib in go.GetComponents<InboxButton>()) Object.DestroyImmediate(ib);
        Transform dot = go.transform.Find("NotificationDot");
        if (dot != null) Object.DestroyImmediate(dot.gameObject);
        if (go.GetComponent<AlmanacButton>() == null) go.AddComponent<AlmanacButton>();

        // Swap only the envelope icon (Icons_Essential/Letter) — the wood slot frame is a sibling child.
        var book = AssetDatabase.LoadAssetAtPath<Sprite>(BookIconPath);
        foreach (var img in go.GetComponentsInChildren<UnityEngine.UI.Image>(true))
            if (book != null && img.sprite != null && (img.sprite.texture.name == "Letter" || img.sprite == book))
            { img.sprite = book; img.preserveAspect = true; }

        go.transform.SetSiblingIndex(inbox.transform.GetSiblingIndex() + 1);
        EditorUtility.SetDirty(go);
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(go.scene);
        UnityEditor.SceneManagement.EditorSceneManager.SaveScene(go.scene);
        Debug.Log("[Almanac] Baked AlmanacButton into " + go.scene.name + ".");
    }

    [MenuItem("Farm Game/Almanac/Apply Crop Trait Drafts")]
    public static void ApplyCropTraitDrafts()
    {
        int applied = 0;
        foreach (string guid in AssetDatabase.FindAssets("t:CropData"))
        {
            var crop = AssetDatabase.LoadAssetAtPath<CropData>(AssetDatabase.GUIDToAssetPath(guid));
            if (crop == null || !Drafts.TryGetValue(crop.cropName, out var d)) continue;
            var so = new SerializedObject(crop);
            so.FindProperty("deerAppetite").floatValue = d.deer;
            so.FindProperty("crowAppetite").floatValue = d.crow;
            so.FindProperty("moistureDepletionRate").floatValue = d.thirst;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(crop);
            applied++;
        }
        AssetDatabase.SaveAssets();
        Debug.Log($"[Almanac] Applied crop trait drafts to {applied}/{Drafts.Count} crops.");
    }

    // ── Almanac art (Resources/AlmanacArt.asset) ─────────────────

    private const string ArtPath = "Assets/Resources/AlmanacArt.asset";
    private const string BookDir = "Assets/Sprites/UI/UI_Book/UI_NoteBook_";

    /// <summary>Creates or refreshes the Almanac's art asset: Munro title and tab fonts, notebook tab
    /// sprites (Toggle01a off / 01b on), a paper page (Frame09a), and deer/crow portraits taken from
    /// the first frame of their idle animations. Also sets those UI sprites to Point filtering so the
    /// pixel art stays crisp when 9-sliced up. Idempotent.</summary>
    [MenuItem("Farm Game/Almanac/Build Art Asset")]
    public static void BuildArtAsset()
    {
        var art = AssetDatabase.LoadAssetAtPath<AlmanacArt>(ArtPath);
        if (art == null)
        {
            art = ScriptableObject.CreateInstance<AlmanacArt>();
            AssetDatabase.CreateAsset(art, ArtPath);
        }

        art.titleFont = AssetDatabase.LoadAssetAtPath<UnityEngine.TextCore.Text.FontAsset>("Assets/Fonts/UITK SDF/Munro Pixel 41.asset");
        art.tabFont = AssetDatabase.LoadAssetAtPath<UnityEngine.TextCore.Text.FontAsset>("Assets/Fonts/UITK SDF/Munro Pixel 31.asset");
        art.tabOff = PixelSprite(BookDir + "Toggle01a.png");
        art.tabOn = PixelSprite(BookDir + "Toggle01b.png");
        art.tabSlice = 4;
        art.page = PixelSprite(BookDir + "Frame09a.png");
        art.pageSlice = 3;
        art.deerIcon = FirstFrame("Assets/Sprites/Animations/DeerM_Idle.anim");
        art.crowIcon = FirstFrame("Assets/Sprites/Animations/Crow_Idle.anim");

        EditorUtility.SetDirty(art);
        AssetDatabase.SaveAssets();
        Debug.Log($"[Almanac] Art asset built: font={art.titleFont != null} tabs={art.tabOff != null}/{art.tabOn != null} " +
                  $"page={art.page != null} deer={art.deerIcon != null} crow={art.crowIcon != null}");
    }

    private static Sprite PixelSprite(string path)
    {
        var importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer != null && importer.filterMode != FilterMode.Point)
        {
            importer.filterMode = FilterMode.Point;
            importer.SaveAndReimport();
        }
        foreach (Object o in AssetDatabase.LoadAllAssetsAtPath(path))
            if (o is Sprite s) return s;
        return null;
    }

    private static Sprite FirstFrame(string clipPath)
    {
        var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPath);
        if (clip == null) return null;
        foreach (EditorCurveBinding b in AnimationUtility.GetObjectReferenceCurveBindings(clip))
        {
            ObjectReferenceKeyframe[] keys = AnimationUtility.GetObjectReferenceCurve(clip, b);
            if (keys != null && keys.Length > 0 && keys[0].value is Sprite s) return s;
        }
        return null;
    }
}
