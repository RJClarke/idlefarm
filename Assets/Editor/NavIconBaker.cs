using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// One-shot scene utility: bakes the bottom-nav icons and the main animal-button icon
/// as REAL saved GameObjects (so they show in the editor with zero runtime cost / no
/// ExecuteAlways). Idempotent — re-running reuses the existing icon child.
/// Run via menu: Farm Game/Bake UI Icons Into Scene.
/// </summary>
public static class NavIconBaker
{
    private const string DoorSprite       = "Assets/Sprites/UI/Icons/Raven/Misc_Door.png";
    private const string HelperSprite     = "Assets/Sprites/UI/Icons/HelperNav.png";
    private const string PickaxeSprite    = "Assets/Sprites/UI/Icons/Raven/Misc_Pickaxe.png";
    private const string AnimalSilhouette = "Assets/Sprites/UI/Icons/CatHead.png";

    [MenuItem("Farm Game/Bake UI Icons Into Scene")]
    public static void Bake()
    {
        int changed = 0;
        changed += BakeNavButton("DrawerCanvas/BottomNavBar/FarmButton", "Farm", DoorSprite);
        changed += BakeNavButton("DrawerCanvas/BottomNavBar/HelpersButton", "Helpers", HelperSprite);
        changed += BakeNavButton("DrawerCanvas/BottomNavBar/EquipmentButton", "Equipment", PickaxeSprite);
        changed += BakeAnimalButton("Canvas/AnimalEquipButton", AnimalSilhouette);

        if (changed > 0)
        {
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            EditorSceneManager.SaveOpenScenes();
        }
        Debug.Log("[NavIconBaker] Baked " + changed + " button icon(s) into the scene.");
    }

    private static int BakeNavButton(string path, string word, string spritePath)
    {
        var go = GameObject.Find(path);
        if (go == null) { Debug.LogWarning("[NavIconBaker] Not found: " + path); return 0; }

        // Icon child (top-center, matches the runtime BottomNav.ApplyButtonIcon layout).
        var iconTf = go.transform.Find("NavIcon") as RectTransform;
        if (iconTf == null)
        {
            var iconGO = new GameObject("NavIcon", typeof(RectTransform), typeof(Image));
            iconTf = iconGO.GetComponent<RectTransform>();
            iconTf.SetParent(go.transform, false);
        }
        iconTf.anchorMin = new Vector2(0.5f, 1f);
        iconTf.anchorMax = new Vector2(0.5f, 1f);
        iconTf.pivot     = new Vector2(0.5f, 1f);
        iconTf.sizeDelta = new Vector2(40f, 40f);
        iconTf.anchoredPosition = new Vector2(0f, -6f);

        var img = iconTf.GetComponent<Image>();
        img.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(spritePath);
        img.raycastTarget = false;
        img.preserveAspect = true;
        img.enabled = true;

        // Label: plain word, bottom-aligned (icon sits above it).
        var label = go.GetComponentInChildren<TextMeshProUGUI>(true);
        if (label != null)
        {
            label.text = word;
            label.alignment = TextAlignmentOptions.Bottom;
        }
        return 1;
    }

    private static int BakeAnimalButton(string path, string spritePath)
    {
        var go = GameObject.Find(path);
        if (go == null) { Debug.LogWarning("[NavIconBaker] Not found: " + path); return 0; }

        var btn = go.GetComponent<AnimalEquipButton>();
        if (btn == null) { Debug.LogWarning("[NavIconBaker] No AnimalEquipButton on " + path); return 0; }

        var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(spritePath);

        // Icon child fills the button minus a small margin.
        var iconTf = go.transform.Find("AnimalIcon") as RectTransform;
        if (iconTf == null)
        {
            var iconGO = new GameObject("AnimalIcon", typeof(RectTransform), typeof(Image));
            iconTf = iconGO.GetComponent<RectTransform>();
            iconTf.SetParent(go.transform, false);
        }
        iconTf.anchorMin = new Vector2(0f, 0f);
        iconTf.anchorMax = new Vector2(1f, 1f);
        iconTf.offsetMin = new Vector2(8f, 8f);
        iconTf.offsetMax = new Vector2(-8f, -8f);

        var img = iconTf.GetComponent<Image>();
        img.sprite = sprite;
        img.raycastTarget = false;
        img.preserveAspect = true;
        img.enabled = true;

        // Wire serialized fields: animalIcon -> this Image, silhouetteSprite -> CatHead.
        var so = new SerializedObject(btn);
        so.FindProperty("animalIcon").objectReferenceValue = img;
        so.FindProperty("silhouetteSprite").objectReferenceValue = sprite;
        so.ApplyModifiedPropertiesWithoutUndo();

        // Hide the "?" emoji text in the editor (runtime UpdateDisplay manages it live).
        var emojiProp = so.FindProperty("emojiText");
        if (emojiProp != null && emojiProp.objectReferenceValue is TextMeshProUGUI emoji && emoji != null)
            emoji.gameObject.SetActive(false);

        return 1;
    }
}
