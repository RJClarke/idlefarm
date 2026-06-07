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

    private static Sprite cachedPillSprite;
    private int speedIndex = 0; // 0=base, 1=10x, 2=20x, 3=30x
    private static readonly float[] speedOptions = { 0f, 10f, 20f, 30f }; // 0 = use base game speed
    // Labels describe the CURRENT active speed at each index.
    private static readonly string[] speedLabels = { "Speed: Normal", "Speed: 10x", "Speed: 20x", "Speed: 30x" };
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

        // Shared sizing — vertical drawer along the right edge.
        const float DRAWER_WIDTH = 240f;
        const float BTN_HEIGHT = 36f;
        const float TOGGLE_HEIGHT = 40f;
        const float MARGIN = 20f;
        const int BTN_FONT = 20;
        Color btnBg = new Color(0.96f, 0.96f, 0.96f, 1f);
        Color btnText = Color.black;

        // ── Toggle Button (top-right) ─────────────────────────────────────
        GameObject toggleGO = CreateButton("DevToolsToggleBtn", canvas);
        RectTransform toggleRT = toggleGO.GetComponent<RectTransform>();
        toggleRT.anchorMin = new Vector2(1, 1);
        toggleRT.anchorMax = new Vector2(1, 1);
        toggleRT.pivot = new Vector2(1, 1);
        toggleRT.anchoredPosition = new Vector2(-MARGIN, -MARGIN);
        toggleRT.sizeDelta = new Vector2(DRAWER_WIDTH, TOGGLE_HEIGHT);
        ApplyPillStyle(toggleGO, btnBg, btnText);
        SetButtonText(toggleGO, "▼ Dev Tools", 17);
        toggleText = toggleGO.GetComponentInChildren<TextMeshProUGUI>();
        if (toggleText != null) toggleText.fontStyle = FontStyles.Bold;
        toggleGO.GetComponent<Button>().onClick.AddListener(OnToggle);

        // ── Drawer Panel (drops down below the toggle, right-aligned) ────
        drawerGO = new GameObject("DevToolsDrawer", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        drawerGO.transform.SetParent(canvas, false);
        drawerGO.layer = 5;

        RectTransform drawerRT = drawerGO.GetComponent<RectTransform>();
        drawerRT.anchorMin = new Vector2(1, 1);
        drawerRT.anchorMax = new Vector2(1, 1);
        drawerRT.pivot = new Vector2(1, 1);
        drawerRT.anchoredPosition = new Vector2(-MARGIN, -(MARGIN + TOGGLE_HEIGHT + 6f));
        drawerRT.sizeDelta = new Vector2(DRAWER_WIDTH, 320f);

        drawerGO.GetComponent<Image>().color = new Color(0.08f, 0.08f, 0.08f, 0.72f);

        // Vertical layout — each button is one row, full width.
        VerticalLayoutGroup vlg = drawerGO.AddComponent<VerticalLayoutGroup>();
        vlg.padding = new RectOffset(12, 12, 12, 12);
        vlg.spacing = 6;
        vlg.childAlignment = TextAnchor.UpperLeft;
        vlg.childControlWidth = true;
        vlg.childControlHeight = true;   // respect LayoutElement.preferredHeight on each button
        vlg.childForceExpandWidth = true;
        vlg.childForceExpandHeight = false;

        GameObject crowGO = CreatePillButton("SpawnCrowButton", "Spawn Crow", btnBg, btnText, BTN_FONT, BTN_HEIGHT);
        crowGO.GetComponent<Button>().onClick.AddListener(() => {
            if (ThreatWaveManager.Instance != null) ThreatWaveManager.Instance.ForceSpawnCrow();
        });

        GameObject deerGO = CreatePillButton("SpawnDeerButton", "Spawn Deer", btnBg, btnText, BTN_FONT, BTN_HEIGHT);
        deerGO.GetComponent<Button>().onClick.AddListener(() => {
            if (ThreatWaveManager.Instance != null) ThreatWaveManager.Instance.ForceSpawnDeer();
        });

        GameObject eggGO = CreatePillButton("ForceEggButton", "Force Egg/Gem Ready", btnBg, btnText, BTN_FONT, BTN_HEIGHT);
        eggGO.GetComponent<Button>().onClick.AddListener(() => {
            if (AnimalManager.Instance != null) AnimalManager.Instance.ForcePassiveReady();
        });

        GameObject finishResearchGO = CreatePillButton("FinishResearchButton", "Finish Research", btnBg, btnText, BTN_FONT, BTN_HEIGHT);
        finishResearchGO.GetComponent<Button>().onClick.AddListener(() => {
            if (ResearchManager.Instance != null) ResearchManager.Instance.DebugFinishCurrentResearches();
        });

        // Speed button at the BOTTOM — shows the currently-active speed in its label.
        GameObject speedGO = CreatePillButton("SpeedUpButton", speedLabels[speedIndex], btnBg, btnText, BTN_FONT, BTN_HEIGHT);
        speedBtnText = speedGO.GetComponentInChildren<TextMeshProUGUI>();
        speedGO.GetComponent<Button>().onClick.AddListener(OnSpeedCycle);

        // ── Info Text ────────────────────────────────────────────────────
        GameObject infoGO = CreateText("InfoText", drawerGO.GetComponent<RectTransform>(), 15);
        infoTMP = infoGO.GetComponent<TextMeshProUGUI>();
        AddPreferredHeight(infoGO, 90f);

        // ── Helper Info Text ─────────────────────────────────────────────
        GameObject helperGO = CreateText("HelperInfoText", drawerGO.GetComponent<RectTransform>(), 15);
        helperInfoTMP = helperGO.GetComponent<TextMeshProUGUI>();
        AddPreferredHeight(helperGO, 30f);

        // Start closed so the game view isn't blocked.
        drawerGO.SetActive(false);
    }

    private static void AddPreferredHeight(GameObject go, float height)
    {
        LayoutElement le = go.GetComponent<LayoutElement>();
        if (le == null) le = go.AddComponent<LayoutElement>();
        le.preferredHeight = height;
        le.minHeight = height;
    }

    /// <summary>
    /// Build a simple light pill button (rounded-rect sprite + dark text).
    /// </summary>
    private GameObject CreatePillButton(string name, string label, Color bg, Color text, int font, float height)
    {
        GameObject go = CreateButton(name, drawerGO.GetComponent<RectTransform>());
        ApplyPillStyle(go, bg, text);
        SetButtonText(go, label, font);
        AddPreferredHeight(go, height);
        return go;
    }

    private static void ApplyPillStyle(GameObject btn, Color bg, Color text)
    {
        Image img = btn.GetComponent<Image>();
        if (img != null)
        {
            img.sprite = GetPillSprite();
            img.type = Image.Type.Sliced;
            img.color = bg;
            img.pixelsPerUnitMultiplier = 1f;
        }
        TextMeshProUGUI tmp = btn.GetComponentInChildren<TextMeshProUGUI>();
        if (tmp != null)
        {
            tmp.color = text;
            tmp.alignment = TextAlignmentOptions.Center;
        }
    }

    /// <summary>
    /// Procedural rounded-rect (pill) sprite with built-in 9-slice borders.
    /// One static instance is shared by every dev-tools button.
    /// </summary>
    private static Sprite GetPillSprite()
    {
        if (cachedPillSprite != null) return cachedPillSprite;

        const int W = 32, H = 32, R = 5;
        Texture2D tex = new Texture2D(W, H, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        tex.wrapMode = TextureWrapMode.Clamp;

        Color[] px = new Color[W * H];
        for (int y = 0; y < H; y++)
        {
            for (int x = 0; x < W; x++)
            {
                float dx = 0f, dy = 0f;
                if (x < R) dx = R - 0.5f - x;
                else if (x >= W - R) dx = x - (W - R) + 0.5f;
                if (y < R) dy = R - 0.5f - y;
                else if (y >= H - R) dy = y - (H - R) + 0.5f;

                float dist = Mathf.Sqrt(dx * dx + dy * dy);
                float alpha = Mathf.Clamp01(R - dist + 0.5f);
                px[y * W + x] = new Color(1f, 1f, 1f, alpha);
            }
        }
        tex.SetPixels(px);
        tex.Apply();

        cachedPillSprite = Sprite.Create(
            tex,
            new Rect(0, 0, W, H),
            new Vector2(0.5f, 0.5f),
            W,
            0,
            SpriteMeshType.FullRect,
            new Vector4(R, R, R, R));
        cachedPillSprite.name = "DevToolsPill";
        return cachedPillSprite;
    }

    // ─────────────────────────────────────────────────────────────────────
    // Handlers
    // ─────────────────────────────────────────────────────────────────────

    private void OnToggle()
    {
        isOpen = !isOpen;
        if (drawerGO != null) drawerGO.SetActive(isOpen);
        if (toggleText != null) toggleText.text = isOpen ? "▲ Dev Tools" : "▼ Dev Tools";
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
