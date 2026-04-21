using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class SettingsMenuPanel : MenuPanel
{
    [SerializeField] private TMP_FontAsset font;

    private void Start()
    {
        BuildUI();
    }

    private void BuildUI()
    {
        // Row: [Toggle]  Visualize numbers
        GameObject rowGO = new GameObject("FloatingNumbersRow", typeof(RectTransform));
        rowGO.transform.SetParent(transform, false);
        rowGO.layer = 5;

        HorizontalLayoutGroup hlg = rowGO.AddComponent<HorizontalLayoutGroup>();
        hlg.spacing = 12;
        hlg.childAlignment = TextAnchor.MiddleLeft;
        hlg.childControlWidth = false;
        hlg.childControlHeight = true;
        hlg.childForceExpandWidth = false;
        hlg.childForceExpandHeight = false;

        LayoutElement rowLE = rowGO.AddComponent<LayoutElement>();
        rowLE.preferredHeight = 44;

        // Toggle
        GameObject toggleGO = new GameObject("Toggle", typeof(RectTransform), typeof(Toggle));
        toggleGO.transform.SetParent(rowGO.transform, false);
        toggleGO.layer = 5;
        RectTransform toggleRT = toggleGO.GetComponent<RectTransform>();
        toggleRT.sizeDelta = new Vector2(36, 36);

        // Toggle background
        GameObject bgGO = new GameObject("Background", typeof(RectTransform), typeof(Image));
        bgGO.transform.SetParent(toggleGO.transform, false);
        bgGO.layer = 5;
        RectTransform bgRT = bgGO.GetComponent<RectTransform>();
        bgRT.anchorMin = Vector2.zero;
        bgRT.anchorMax = Vector2.one;
        bgRT.sizeDelta = Vector2.zero;
        bgGO.GetComponent<Image>().color = new Color(0.3f, 0.3f, 0.3f, 1f);

        // Checkmark
        GameObject checkGO = new GameObject("Checkmark", typeof(RectTransform), typeof(Image));
        checkGO.transform.SetParent(bgGO.transform, false);
        checkGO.layer = 5;
        RectTransform checkRT = checkGO.GetComponent<RectTransform>();
        checkRT.anchorMin = new Vector2(0.1f, 0.1f);
        checkRT.anchorMax = new Vector2(0.9f, 0.9f);
        checkRT.sizeDelta = Vector2.zero;
        checkGO.GetComponent<Image>().color = new Color(0.298f, 0.686f, 0.314f); // green

        Toggle toggle = toggleGO.GetComponent<Toggle>();
        toggle.targetGraphic = bgGO.GetComponent<Image>();
        toggle.graphic = checkGO.GetComponent<Image>();
        toggle.isOn = SettingsManager.ShowFloatingNumbers;
        toggle.onValueChanged.AddListener(val => SettingsManager.ShowFloatingNumbers = val);

        // Label
        GameObject labelGO = new GameObject("Label", typeof(RectTransform));
        labelGO.transform.SetParent(rowGO.transform, false);
        labelGO.layer = 5;

        TextMeshProUGUI tmp = labelGO.AddComponent<TextMeshProUGUI>();
        tmp.text = "Visualize numbers";
        tmp.fontSize = 28;
        tmp.color = new Color(0.9f, 0.9f, 0.9f);
        tmp.alignment = TextAlignmentOptions.MidlineLeft;
        if (font != null) tmp.font = font;

        LayoutElement labelLE = labelGO.AddComponent<LayoutElement>();
        labelLE.flexibleWidth = 1;
    }
}
