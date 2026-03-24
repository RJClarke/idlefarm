using UnityEngine;
using UnityEngine.UI;
using UnityEditor;

/// <summary>
/// Editor script to add vertical scrolling to the MarketPanel
/// and create a Sprinkler unlock button.
/// Run from Tools > Fix Market Panel Scroll.
/// </summary>
public class MarketPanelBuilder
{
    [MenuItem("Tools/Fix Market Panel Scroll")]
    public static void Fix()
    {
        GameObject panel = GameObject.Find("DrawerCanvas/DrawerContainer/MenuContent/MarketPanel");
        if (panel == null)
        {
            Debug.LogError("MarketPanel not found!");
            return;
        }

        int uiLayer = panel.layer;
        RectTransform panelRT = panel.GetComponent<RectTransform>();

        // Give panel enough height to fill the drawer area
        panelRT.sizeDelta = new Vector2(panelRT.sizeDelta.x, 1200f);

        // Remove VLG from panel (will move to Content)
        var existingVLG = panel.GetComponent<VerticalLayoutGroup>();
        RectOffset oldPadding = existingVLG != null ? existingVLG.padding : new RectOffset(20, 20, 20, 20);
        float oldSpacing = existingVLG != null ? existingVLG.spacing : 16f;
        if (existingVLG != null) Object.DestroyImmediate(existingVLG);

        // Create Viewport
        GameObject viewport = CreateUI("Viewport", panel.transform, uiLayer);
        Stretch(viewport);
        Image vpImg = viewport.AddComponent<Image>();
        vpImg.color = new Color(1, 1, 1, 0); // transparent
        var mask = viewport.AddComponent<RectMask2D>();

        // Create Content
        GameObject content = CreateUI("Content", viewport.transform, uiLayer);
        RectTransform contentRT = content.GetComponent<RectTransform>();
        contentRT.anchorMin = new Vector2(0, 1);
        contentRT.anchorMax = new Vector2(1, 1);
        contentRT.pivot = new Vector2(0.5f, 1f);
        contentRT.sizeDelta = new Vector2(0, 0); // CSF will drive height

        var contentVLG = content.AddComponent<VerticalLayoutGroup>();
        contentVLG.spacing = oldSpacing;
        contentVLG.padding = oldPadding;
        contentVLG.childForceExpandWidth = true;
        contentVLG.childForceExpandHeight = false;
        contentVLG.childControlWidth = true;
        contentVLG.childControlHeight = true;

        var contentCSF = content.AddComponent<ContentSizeFitter>();
        contentCSF.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        contentCSF.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;

        // Reparent existing buttons to Content (collect first to avoid modifying during iteration)
        var children = new System.Collections.Generic.List<Transform>();
        foreach (Transform child in panel.transform)
        {
            if (child.gameObject != viewport) children.Add(child);
        }
        foreach (Transform child in children)
        {
            child.SetParent(content.transform, false);
        }

        // Add ScrollRect to panel
        ScrollRect sr = panel.AddComponent<ScrollRect>();
        sr.horizontal = false;
        sr.vertical = true;
        sr.movementType = ScrollRect.MovementType.Elastic;
        sr.elasticity = 0.1f;
        sr.viewport = viewport.GetComponent<RectTransform>();
        sr.content = contentRT;
        sr.scrollSensitivity = 20f;

        // Now add Sprinkler button (duplicate structure from ScarecrowButton)
        AddSprinklerButton(content.transform, uiLayer);

        // Also add Blueberry button if it doesn't exist
        bool hasBlueberry = false;
        foreach (Transform child in content.transform)
        {
            if (child.name == "BlueberryButton") { hasBlueberry = true; break; }
        }
        if (!hasBlueberry)
            AddUnlockButton(content.transform, uiLayer, "BlueberryButton", "Blueberry_Unlock");

        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
            UnityEngine.SceneManagement.SceneManager.GetActiveScene());
        Debug.Log("MarketPanel scroll fix applied!");
    }

    private static void AddSprinklerButton(Transform parent, int layer)
    {
        // Check if already exists
        foreach (Transform child in parent)
        {
            if (child.name == "SprinklerButton") return;
        }

        AddUnlockButton(parent, layer, "SprinklerButton", "Sprinkler_Unlock");
    }

    private static void AddUnlockButton(Transform parent, int layer, string buttonName, string unlockAssetName)
    {
        // Find unlock data asset
        string[] guids = AssetDatabase.FindAssets($"t:UnlockData {unlockAssetName}");
        UnlockData unlockData = null;
        if (guids.Length > 0)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[0]);
            unlockData = AssetDatabase.LoadAssetAtPath<UnlockData>(path);
        }

        if (unlockData == null)
        {
            Debug.LogWarning($"Could not find UnlockData asset: {unlockAssetName}");
            return;
        }

        // Create button object
        GameObject btn = CreateUI(buttonName, parent, layer);
        Image btnImg = btn.AddComponent<Image>();
        btnImg.type = Image.Type.Sliced;
        btnImg.color = Color.white;
        Button btnComp = btn.AddComponent<Button>();
        btnComp.targetGraphic = btnImg;
        var le = btn.AddComponent<LayoutElement>();
        le.preferredHeight = 120f;
        le.layoutPriority = 1;

        // IconImage
        GameObject iconImageObj = CreateUI("IconImage", btn.transform, layer);
        RectTransform iconRT = iconImageObj.GetComponent<RectTransform>();
        iconRT.anchorMin = new Vector2(0, 0);
        iconRT.anchorMax = new Vector2(0, 1);
        iconRT.pivot = new Vector2(0, 0.5f);
        iconRT.anchoredPosition = new Vector2(10, 0);
        iconRT.sizeDelta = new Vector2(80, -20);
        Image iconImg = iconImageObj.AddComponent<Image>();
        iconImg.preserveAspect = true;

        // IconText (emoji fallback)
        GameObject iconTextObj = CreateUI("IconText", btn.transform, layer);
        RectTransform iconTextRT = iconTextObj.GetComponent<RectTransform>();
        iconTextRT.anchorMin = new Vector2(0, 0);
        iconTextRT.anchorMax = new Vector2(0, 1);
        iconTextRT.pivot = new Vector2(0, 0.5f);
        iconTextRT.anchoredPosition = new Vector2(10, 0);
        iconTextRT.sizeDelta = new Vector2(80, -20);
        var iconTMP = iconTextObj.AddComponent<TMPro.TextMeshProUGUI>();
        iconTMP.text = unlockData.icon;
        iconTMP.fontSize = 36;
        iconTMP.alignment = TMPro.TextAlignmentOptions.Center;

        // Content area (title + description on top row, status + cost on bottom)
        GameObject contentObj = CreateUI("Content", btn.transform, layer);
        RectTransform contentObjRT = contentObj.GetComponent<RectTransform>();
        contentObjRT.anchorMin = new Vector2(0, 0);
        contentObjRT.anchorMax = new Vector2(1, 1);
        contentObjRT.pivot = new Vector2(0.5f, 0.5f);
        contentObjRT.offsetMin = new Vector2(100, 5);
        contentObjRT.offsetMax = new Vector2(-10, -5);

        var contentHLG = contentObj.AddComponent<HorizontalLayoutGroup>();
        contentHLG.childForceExpandWidth = true;
        contentHLG.childForceExpandHeight = true;
        contentHLG.childControlWidth = true;
        contentHLG.childControlHeight = true;

        var contentCSF = contentObj.AddComponent<ContentSizeFitter>();
        contentCSF.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;

        // Left column (Title + Description)
        GameObject leftCol = CreateUI("LeftColumn", contentObj.transform, layer);
        var leftVLG = leftCol.AddComponent<VerticalLayoutGroup>();
        leftVLG.childForceExpandWidth = true;
        leftVLG.childForceExpandHeight = false;
        leftVLG.childControlWidth = true;
        leftVLG.childControlHeight = true;
        leftVLG.childAlignment = UnityEngine.TextAnchor.MiddleLeft;
        leftVLG.spacing = 2;
        var leftLE = leftCol.AddComponent<LayoutElement>();
        leftLE.flexibleWidth = 2;

        GameObject titleObj = CreateUI("Title", leftCol.transform, layer);
        var titleTMP = titleObj.AddComponent<TMPro.TextMeshProUGUI>();
        titleTMP.text = unlockData.displayName;
        titleTMP.fontSize = 18;
        titleTMP.fontStyle = TMPro.FontStyles.Bold;
        titleTMP.color = Color.black;
        var titleLE = titleObj.AddComponent<LayoutElement>();
        titleLE.preferredHeight = 24;

        GameObject descObj = CreateUI("Description", leftCol.transform, layer);
        var descTMP = descObj.AddComponent<TMPro.TextMeshProUGUI>();
        descTMP.text = unlockData.lockedDescription;
        descTMP.fontSize = 12;
        descTMP.color = new Color(0, 0, 0, 0.6f);
        var descLE = descObj.AddComponent<LayoutElement>();
        descLE.preferredHeight = 16;

        // Right column (Status + Cost)
        GameObject rightCol = CreateUI("RightColumn", contentObj.transform, layer);
        var rightVLG = rightCol.AddComponent<VerticalLayoutGroup>();
        rightVLG.childForceExpandWidth = true;
        rightVLG.childForceExpandHeight = false;
        rightVLG.childControlWidth = true;
        rightVLG.childControlHeight = true;
        rightVLG.childAlignment = UnityEngine.TextAnchor.MiddleRight;
        rightVLG.spacing = 2;
        var rightLE = rightCol.AddComponent<LayoutElement>();
        rightLE.flexibleWidth = 1;

        GameObject statusObj = CreateUI("Status", rightCol.transform, layer);
        var statusTMP = statusObj.AddComponent<TMPro.TextMeshProUGUI>();
        statusTMP.text = "LOCKED";
        statusTMP.fontSize = 14;
        statusTMP.alignment = TMPro.TextAlignmentOptions.Right;
        statusTMP.color = new Color(0.7f, 0.7f, 0.7f);
        var statusLE = statusObj.AddComponent<LayoutElement>();
        statusLE.preferredHeight = 20;

        GameObject costObj = CreateUI("Cost", rightCol.transform, layer);
        var costTMP = costObj.AddComponent<TMPro.TextMeshProUGUI>();
        costTMP.text = $"UNLOCK: {unlockData.coinCost}";
        costTMP.fontSize = 14;
        costTMP.alignment = TMPro.TextAlignmentOptions.Right;
        costTMP.color = Color.black;
        var costLE = costObj.AddComponent<LayoutElement>();
        costLE.preferredHeight = 20;

        // Add UnlockButton component and wire references
        UnlockButton unlockBtn = btn.AddComponent<UnlockButton>();
        // Set serialized fields via SerializedObject
        var so = new SerializedObject(unlockBtn);
        so.FindProperty("unlockData").objectReferenceValue = unlockData;
        so.FindProperty("button").objectReferenceValue = btnComp;
        so.FindProperty("iconImage").objectReferenceValue = iconImg;
        so.FindProperty("iconText").objectReferenceValue = iconTMP;
        so.FindProperty("titleText").objectReferenceValue = titleTMP;
        so.FindProperty("descriptionText").objectReferenceValue = descTMP;
        so.FindProperty("statusText").objectReferenceValue = statusTMP;
        so.FindProperty("costText").objectReferenceValue = costTMP;
        so.ApplyModifiedProperties();

        Debug.Log($"Added {buttonName} to Market panel");
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
