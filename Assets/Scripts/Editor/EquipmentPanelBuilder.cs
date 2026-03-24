using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEditor;

/// <summary>
/// Editor-only script to build the Equipment panel UI hierarchy in the scene.
/// Run once from Tools > Build Equipment Panel, then delete this script if desired.
/// </summary>
public class EquipmentPanelBuilder
{
    private static readonly Color panelBgColor = new Color(0.722f, 0.675f, 0.529f, 1f);
    private static readonly Color tileBgColor = new Color(0.890f, 0.847f, 0.792f, 0.4f);
    private static readonly Color textColor = new Color(0f, 0f, 0f, 1f);
    private static readonly Color mutedTextColor = new Color(0f, 0f, 0f, 0.5f);

    [MenuItem("Tools/Build Equipment Panel")]
    public static void Build()
    {
        // Find the EquipmentPanel in the scene
        GameObject panel = GameObject.Find("DrawerCanvas/DrawerContainer/MenuContent/EquipmentPanel");
        if (panel == null)
        {
            Debug.LogError("EquipmentPanel not found!");
            return;
        }

        // Clear existing children
        for (int i = panel.transform.childCount - 1; i >= 0; i--)
            Object.DestroyImmediate(panel.transform.GetChild(i).gameObject);

        int uiLayer = panel.layer;

        // Configure panel's VerticalLayoutGroup
        var vlg = panel.GetComponent<VerticalLayoutGroup>();
        if (vlg == null) vlg = panel.AddComponent<VerticalLayoutGroup>();
        vlg.spacing = 12f;
        vlg.padding = new RectOffset(8, 8, 8, 8);
        vlg.childForceExpandWidth = true;
        vlg.childForceExpandHeight = false;
        vlg.childControlWidth = true;
        vlg.childControlHeight = true;

        // Add ContentSizeFitter so panel grows to fit cards
        var csf = panel.GetComponent<ContentSizeFitter>();
        if (csf == null) csf = panel.AddComponent<ContentSizeFitter>();
        csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        csf.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;

        // Build 3 equipment cards
        string[] names = { "Scarecrow", "Fence", "Sprinkler" };
        string[] icons = { "\U0001F9D1", "\U0001F9F1", "\U0001F4A7" };

        // Upgrade row info per equipment: [title, desc] x 3
        string[,] scarecrowRows = {
            { "Scarecrow Range", "Increases the scarecrow's interception radius." },
            { "Scarecrow Charges", "Increases crows repelled per cycle before cooldown." },
            { "Scarecrow Recovery", "Reduces time between scarecrow repel cycles." }
        };
        string[,] fenceRows = {
            { "Fence Length", "Extends fence coverage further along zone edges." },
            { "Fence Capacity", "Repel more deer per cycle before cooldown." },
            { "Fence Cooldown", "Reduces time between fence repel cycles." }
        };
        string[,] sprinklerRows = {
            { "Sprinkler Range", "Increases the sprinkler's watering radius." },
            { "Water Pressure", "Increases moisture added per second while active." },
            { "Sprinkler Cooldown", "Reduces time between sprinkler cycles." }
        };
        string[][,] allRows = { scarecrowRows, fenceRows, sprinklerRows };

        for (int e = 0; e < 3; e++)
        {
            BuildEquipmentCard(panel.transform, uiLayer, names[e], icons[e], allRows[e]);
        }

        // Mark scene dirty
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
            UnityEngine.SceneManagement.SceneManager.GetActiveScene());
        Debug.Log("Equipment Panel UI built successfully!");
    }

    private static void BuildEquipmentCard(Transform parent, int layer, string equipName, string icon, string[,] rows)
    {
        // Card container with HorizontalLayoutGroup (Icon + Rows side by side)
        GameObject card = CreateUI($"Card_{equipName}", parent, layer);
        var cardLE = card.AddComponent<LayoutElement>();
        cardLE.preferredHeight = 210f;
        cardLE.flexibleWidth = 1f;
        Image cardBg = card.AddComponent<Image>();
        cardBg.color = panelBgColor;

        var cardHLG = card.AddComponent<HorizontalLayoutGroup>();
        cardHLG.spacing = 10f;
        cardHLG.padding = new RectOffset(10, 10, 10, 10);
        cardHLG.childForceExpandWidth = false;
        cardHLG.childForceExpandHeight = true;
        cardHLG.childControlWidth = true;
        cardHLG.childControlHeight = true;
        cardHLG.childAlignment = TextAnchor.MiddleLeft;

        // Icon
        GameObject iconObj = CreateUI("Icon", card.transform, layer);
        var iconLE = iconObj.AddComponent<LayoutElement>();
        iconLE.preferredWidth = 60f;
        iconLE.preferredHeight = 60f;
        iconLE.flexibleWidth = 0;
        iconLE.flexibleHeight = 0;
        Image iconBg = iconObj.AddComponent<Image>();
        iconBg.color = new Color(0.890f, 0.847f, 0.792f, 0.6f);

        GameObject iconText = CreateUI("IconText", iconObj.transform, layer);
        Stretch(iconText);
        var iconTMP = iconText.AddComponent<TextMeshProUGUI>();
        iconTMP.text = icon;
        iconTMP.fontSize = 32;
        iconTMP.alignment = TextAlignmentOptions.Center;
        iconTMP.color = textColor;

        // Rows container
        GameObject rowsObj = CreateUI("Rows", card.transform, layer);
        var rowsVLG = rowsObj.AddComponent<VerticalLayoutGroup>();
        rowsVLG.spacing = 4f;
        rowsVLG.childForceExpandWidth = true;
        rowsVLG.childForceExpandHeight = false;
        rowsVLG.childControlWidth = true;
        rowsVLG.childControlHeight = true;
        var rowsLE = rowsObj.AddComponent<LayoutElement>();
        rowsLE.flexibleWidth = 1f;

        // 3 upgrade rows
        for (int r = 0; r < 3; r++)
        {
            BuildUpgradeRow(rowsObj.transform, layer, rows[r, 0], rows[r, 1], $"{equipName}_Row{r}");
        }
    }

    private static void BuildUpgradeRow(Transform parent, int layer, string title, string desc, string rowName)
    {
        GameObject row = CreateUI(rowName, parent, layer);
        var rowVLG = row.AddComponent<VerticalLayoutGroup>();
        rowVLG.spacing = 1f;
        rowVLG.childForceExpandWidth = true;
        rowVLG.childForceExpandHeight = false;
        rowVLG.childControlWidth = true;
        rowVLG.childControlHeight = true;

        // Title
        GameObject titleObj = CreateUI("Title", row.transform, layer);
        var titleTMP = titleObj.AddComponent<TextMeshProUGUI>();
        titleTMP.text = title;
        titleTMP.fontSize = 13;
        titleTMP.fontStyle = FontStyles.Bold;
        titleTMP.color = textColor;
        var titleLE = titleObj.AddComponent<LayoutElement>();
        titleLE.preferredHeight = 16f;

        // Description
        GameObject descObj = CreateUI("Desc", row.transform, layer);
        var descTMP = descObj.AddComponent<TextMeshProUGUI>();
        descTMP.text = desc;
        descTMP.fontSize = 10;
        descTMP.color = mutedTextColor;
        var descLE = descObj.AddComponent<LayoutElement>();
        descLE.preferredHeight = 13f;

        // Tiles container
        GameObject tiles = CreateUI("Tiles", row.transform, layer);
        var tilesHLG = tiles.AddComponent<HorizontalLayoutGroup>();
        tilesHLG.spacing = 4f;
        tilesHLG.childForceExpandWidth = false;
        tilesHLG.childForceExpandHeight = false;
        tilesHLG.childControlWidth = true;
        tilesHLG.childControlHeight = true;
        tilesHLG.childAlignment = TextAnchor.MiddleLeft;
        var tilesLE = tiles.AddComponent<LayoutElement>();
        tilesLE.preferredHeight = 44f;

        // 5 tiles
        for (int t = 0; t < 5; t++)
        {
            BuildTile(tiles.transform, layer, t + 1);
        }
    }

    private static void BuildTile(Transform parent, int layer, int level)
    {
        GameObject tile = CreateUI($"Tile_Lv{level}", parent, layer);
        var tileLE = tile.AddComponent<LayoutElement>();
        tileLE.preferredWidth = 44f;
        tileLE.preferredHeight = 44f;
        Image tileBg = tile.AddComponent<Image>();
        tileBg.color = tileBgColor;
        tile.AddComponent<Button>().transition = Selectable.Transition.None;

        var tileVLG = tile.AddComponent<VerticalLayoutGroup>();
        tileVLG.spacing = 0f;
        tileVLG.padding = new RectOffset(2, 2, 2, 2);
        tileVLG.childForceExpandWidth = true;
        tileVLG.childForceExpandHeight = false;
        tileVLG.childControlWidth = true;
        tileVLG.childControlHeight = true;
        tileVLG.childAlignment = TextAnchor.MiddleCenter;

        // Level label
        GameObject lvlObj = CreateUI("LevelLabel", tile.transform, layer);
        var lvlTMP = lvlObj.AddComponent<TextMeshProUGUI>();
        lvlTMP.text = $"Lv {level}";
        lvlTMP.fontSize = 8;
        lvlTMP.alignment = TextAlignmentOptions.Center;
        lvlTMP.color = mutedTextColor;
        var lvlLE = lvlObj.AddComponent<LayoutElement>();
        lvlLE.preferredHeight = 10f;

        // Bonus label
        GameObject bonusObj = CreateUI("BonusLabel", tile.transform, layer);
        var bonusTMP = bonusObj.AddComponent<TextMeshProUGUI>();
        bonusTMP.text = "---";
        bonusTMP.fontSize = 9;
        bonusTMP.fontStyle = FontStyles.Bold;
        bonusTMP.alignment = TextAlignmentOptions.Center;
        bonusTMP.color = mutedTextColor;
        var bonusLE = bonusObj.AddComponent<LayoutElement>();
        bonusLE.preferredHeight = 12f;

        // Cost label
        GameObject costObj = CreateUI("CostLabel", tile.transform, layer);
        var costTMP = costObj.AddComponent<TextMeshProUGUI>();
        costTMP.text = "";
        costTMP.fontSize = 8;
        costTMP.alignment = TextAlignmentOptions.Center;
        costTMP.color = mutedTextColor;
        var costLE = costObj.AddComponent<LayoutElement>();
        costLE.preferredHeight = 10f;
    }

    private static GameObject CreateUI(string name, Transform parent, int layer)
    {
        GameObject obj = new GameObject(name, typeof(RectTransform));
        obj.transform.SetParent(parent, false);
        obj.layer = layer;
        return obj;
    }

    private static void Stretch(GameObject obj)
    {
        RectTransform rt = obj.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }
}
