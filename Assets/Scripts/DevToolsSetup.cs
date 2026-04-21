using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Dev Tools drawer — self-contained. Attach to UI-Test-Canvas.
/// Builds a toggle button + collapsible drawer with dev buttons and info at runtime.
/// </summary>
public class DevToolsSetup : MonoBehaviour
{
    [Header("Font")]
    [SerializeField] private TMP_FontAsset font;

    private GameObject drawerGO;
    private TextMeshProUGUI toggleText;
    private TextMeshProUGUI infoTMP;
    private TextMeshProUGUI helperInfoTMP;
    private bool isOpen = false;
    private int speedIndex = 0; // 0=base, 1=10x, 2=20x, 3=30x
    private static readonly float[] speedOptions = { 0f, 10f, 20f, 30f }; // 0 = use base game speed
    private static readonly string[] speedLabels = { "Speed 10x", "Speed 20x", "Speed 30x", "Normal" };
    private TextMeshProUGUI speedBtnText;

    private void Start()
    {
        BuildUI();
    }

    private void Update()
    {
        if (!isOpen) return;
        UpdateInfoDisplay();
        UpdateHelperDisplay();
    }

    private void BuildUI()
    {
        RectTransform canvas = GetComponent<RectTransform>();
        if (canvas == null) return;

        // Destroy old panels
        Transform oldTest = transform.Find("TestPanel");
        if (oldTest != null) Destroy(oldTest.gameObject);
        Transform oldHelper = transform.Find("HelperPanel");
        if (oldHelper != null) Destroy(oldHelper.gameObject);

        // ── Toggle Button (top-right, 20px from edges) ───────────────────
        GameObject toggleGO = CreateButton("DevToolsToggleBtn", canvas);
        RectTransform toggleRT = toggleGO.GetComponent<RectTransform>();
        toggleRT.anchorMin = new Vector2(1, 1);
        toggleRT.anchorMax = new Vector2(1, 1);
        toggleRT.pivot = new Vector2(1, 1);
        toggleRT.anchoredPosition = new Vector2(-20, -20);
        toggleRT.sizeDelta = new Vector2(200, 50);
        SetButtonColor(toggleGO, new Color(0.15f, 0.15f, 0.15f, 0.85f));
        SetButtonText(toggleGO, "Dev Tools", 18);
        toggleText = toggleGO.GetComponentInChildren<TextMeshProUGUI>();
        toggleGO.GetComponent<Button>().onClick.AddListener(OnToggle);

        // ── Drawer Panel ─────────────────────────────────────────────────
        drawerGO = new GameObject("DevToolsDrawer", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        drawerGO.transform.SetParent(canvas, false);
        drawerGO.layer = 5;

        RectTransform drawerRT = drawerGO.GetComponent<RectTransform>();
        drawerRT.anchorMin = new Vector2(0, 1);
        drawerRT.anchorMax = new Vector2(1, 1);
        drawerRT.pivot = new Vector2(0.5f, 1);
        drawerRT.anchoredPosition = new Vector2(0, -75);
        drawerRT.sizeDelta = new Vector2(-20, 280);

        drawerGO.GetComponent<Image>().color = new Color(0.1f, 0.1f, 0.1f, 0.9f);

        // Vertical layout
        VerticalLayoutGroup vlg = drawerGO.AddComponent<VerticalLayoutGroup>();
        vlg.padding = new RectOffset(10, 10, 10, 10);
        vlg.spacing = 6;
        vlg.childAlignment = TextAnchor.UpperLeft;
        vlg.childControlWidth = true;
        vlg.childControlHeight = false;
        vlg.childForceExpandWidth = true;
        vlg.childForceExpandHeight = false;

        // ── Button Row ───────────────────────────────────────────────────
        GameObject btnRow = new GameObject("ButtonRow", typeof(RectTransform));
        btnRow.transform.SetParent(drawerGO.transform, false);
        btnRow.layer = 5;
        HorizontalLayoutGroup hlg = btnRow.AddComponent<HorizontalLayoutGroup>();
        hlg.spacing = 8;
        hlg.childControlWidth = true;
        hlg.childControlHeight = true;
        hlg.childForceExpandWidth = true;
        hlg.childForceExpandHeight = true;
        LayoutElement rowLE = btnRow.AddComponent<LayoutElement>();
        rowLE.preferredHeight = 45;

        // Speed button — cycles through 1x → 10x → 20x → 30x
        GameObject speedGO = CreateButton("SpeedUpButton", btnRow.GetComponent<RectTransform>());
        SetButtonColor(speedGO, new Color(0.3f, 0.2f, 0.05f, 1f));
        SetButtonText(speedGO, speedLabels[0], 16);
        speedBtnText = speedGO.GetComponentInChildren<TextMeshProUGUI>();
        speedGO.GetComponent<Button>().onClick.AddListener(OnSpeedCycle);

        // Spawn Crow button
        GameObject crowGO = CreateButton("SpawnCrowButton", btnRow.GetComponent<RectTransform>());
        SetButtonColor(crowGO, new Color(0.15f, 0.15f, 0.35f, 1f));
        SetButtonText(crowGO, "Spawn Crow", 16);
        crowGO.GetComponent<Button>().onClick.AddListener(() => {
            if (ThreatWaveManager.Instance != null) ThreatWaveManager.Instance.ForceSpawnCrow();
        });

        // Spawn Deer button
        GameObject deerGO = CreateButton("SpawnDeerButton", btnRow.GetComponent<RectTransform>());
        SetButtonColor(deerGO, new Color(0.2f, 0.1f, 0.05f, 1f));
        SetButtonText(deerGO, "Spawn Deer", 16);
        deerGO.GetComponent<Button>().onClick.AddListener(() => {
            if (ThreatWaveManager.Instance != null) ThreatWaveManager.Instance.ForceSpawnDeer();
        });

        // Force Egg Ready button
        GameObject eggGO = CreateButton("ForceEggButton", btnRow.GetComponent<RectTransform>());
        SetButtonColor(eggGO, new Color(0.45f, 0.35f, 0.05f, 1f));
        SetButtonText(eggGO, "Egg Ready", 16);
        eggGO.GetComponent<Button>().onClick.AddListener(() => {
            if (AnimalManager.Instance != null) AnimalManager.Instance.ForceEggReady();
        });

        // ── Info Text ────────────────────────────────────────────────────
        GameObject infoGO = CreateText("InfoText", drawerGO.GetComponent<RectTransform>(), 14);
        infoTMP = infoGO.GetComponent<TextMeshProUGUI>();
        LayoutElement infoLE = infoGO.AddComponent<LayoutElement>();
        infoLE.preferredHeight = 80;

        // ── Helper Info Text ─────────────────────────────────────────────
        GameObject helperGO = CreateText("HelperInfoText", drawerGO.GetComponent<RectTransform>(), 14);
        helperInfoTMP = helperGO.GetComponent<TextMeshProUGUI>();
        LayoutElement helperLE = helperGO.AddComponent<LayoutElement>();
        helperLE.preferredHeight = 25;

        // Start closed
        drawerGO.SetActive(false);
    }

    // ─────────────────────────────────────────────────────────────────────
    // Handlers
    // ─────────────────────────────────────────────────────────────────────

    private void OnToggle()
    {
        isOpen = !isOpen;
        if (drawerGO != null) drawerGO.SetActive(isOpen);
        if (toggleText != null) toggleText.text = isOpen ? "Close Dev Tools" : "Dev Tools";
    }

    private float BaseSpeed => GameConstants.Instance != null ? GameConstants.Instance.baseGameSpeed : 2f;

    private void OnSpeedCycle()
    {
        speedIndex = (speedIndex + 1) % speedOptions.Length;
        Time.timeScale = speedOptions[speedIndex] > 0f ? speedOptions[speedIndex] : BaseSpeed;
        if (speedBtnText != null) speedBtnText.text = speedLabels[speedIndex];
    }

    // ─────────────────────────────────────────────────────────────────────
    // Info Updates
    // ─────────────────────────────────────────────────────────────────────

    private void UpdateInfoDisplay()
    {
        if (infoTMP == null) return;

        Plant[] allPlants = FindObjectsByType<Plant>(FindObjectsSortMode.None);
        int seeds = 0, sprouts = 0, saplings = 0, harvestable = 0;
        float avgMoisture = 0f;
        int driedOut = 0, rotting = 0;

        foreach (Plant p in allPlants)
        {
            switch (p.CurrentStage)
            {
                case GrowthStage.Seed: seeds++; break;
                case GrowthStage.Sprout: sprouts++; break;
                case GrowthStage.Sapling: saplings++; break;
                case GrowthStage.Harvestable: harvestable++; break;
            }
            avgMoisture += p.CurrentMoisture;
            if (p.IsDriedOut) driedOut++;
            if (p.IsRotting) rotting++;
        }
        if (allPlants.Length > 0) avgMoisture /= allPlants.Length;

        string info = $"<b>FARM</b>  Plants:{allPlants.Length}  S:{seeds} Sp:{sprouts} Sa:{saplings} H:{harvestable}";
        if (rotting > 0) info += $"  <color=orange>Rot:{rotting}</color>";
        info += $"\nMoisture:{avgMoisture:F0}%";
        if (driedOut > 0) info += $"  <color=red>Dried:{driedOut}</color>";

        if (ThreatWaveManager.Instance != null)
            info += $"\n<b>THREATS</b>  Wave:{ThreatWaveManager.Instance.CurrentWave}  Hunger:{ThreatWaveManager.Instance.CurrentHunger:F0}";

        if (speedIndex > 0) info += $"\n<color=yellow>TIME: {Time.timeScale}x</color>";

        infoTMP.text = info;
    }

    private void UpdateHelperDisplay()
    {
        if (helperInfoTMP == null || HelperManager.Instance == null) return;

        int total = HelperManager.Instance.GetHelperCount();
        int idle = HelperManager.Instance.GetIdleHelperCount();
        int tasks = HelperManager.Instance.GetPendingTaskCount();

        string info = $"<b>HELPERS</b>  Total:{total}  Idle:{idle}  Working:{total - idle}";
        if (tasks > 0) info += $"  Tasks:{tasks}";
        helperInfoTMP.text = info;
    }

    // ─────────────────────────────────────────────────────────────────────
    // Cleanup
    // ─────────────────────────────────────────────────────────────────────

    private void OnDestroy()
    {
        Time.timeScale = GameConstants.Instance != null ? GameConstants.Instance.baseGameSpeed : 2f;
    }

    // ─────────────────────────────────────────────────────────────────────
    // Factory Helpers
    // ─────────────────────────────────────────────────────────────────────

    private GameObject CreateButton(string name, RectTransform parent)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);
        go.layer = 5;

        GameObject textGO = new GameObject("Text", typeof(RectTransform), typeof(CanvasRenderer));
        textGO.transform.SetParent(go.transform, false);
        textGO.layer = 5;
        TextMeshProUGUI tmp = textGO.AddComponent<TextMeshProUGUI>();
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = Color.white;
        if (font != null) tmp.font = font;

        RectTransform textRT = textGO.GetComponent<RectTransform>();
        textRT.anchorMin = Vector2.zero;
        textRT.anchorMax = Vector2.one;
        textRT.offsetMin = Vector2.zero;
        textRT.offsetMax = Vector2.zero;

        return go;
    }

    private GameObject CreateText(string name, RectTransform parent, float size)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer));
        go.transform.SetParent(parent, false);
        go.layer = 5;
        TextMeshProUGUI tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.fontSize = size;
        tmp.color = Color.white;
        tmp.richText = true;
        if (font != null) tmp.font = font;
        return go;
    }

    private static void SetButtonColor(GameObject btn, Color c)
    {
        Image img = btn.GetComponent<Image>();
        if (img != null) img.color = c;
    }

    private static void SetButtonText(GameObject btn, string text, float size)
    {
        TextMeshProUGUI tmp = btn.GetComponentInChildren<TextMeshProUGUI>();
        if (tmp != null) { tmp.text = text; tmp.fontSize = size; }
    }
}
