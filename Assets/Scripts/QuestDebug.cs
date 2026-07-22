#if UNITY_EDITOR || DEVELOPMENT_BUILD
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Quest/daily debug drawer. Built at runtime as uGUI on the SAME canvas as the Dev Tools drawer
/// (UI-Test-Canvas) so it sorts like every other HUD element — i.e. game popups/menus draw OVER it.
/// (The old version used IMGUI/OnGUI, which always renders on top of every canvas regardless of Z.)
/// Sits on the right edge just below the Dev Tools toggle; both expand downward.
/// </summary>
public class QuestDebug : MonoBehaviour
{
    [Header("Font (optional — defaults to TMP default)")]
    [SerializeField] private TMP_FontAsset font;

    private GameObject drawerGO;
    private TextMeshProUGUI toggleText;
    private TextMeshProUGUI statusText;
    private bool isOpen = false;

    private static Sprite cachedPillSprite;

    // Placement mirrors DevToolsSetup so the two drawers line up on the right edge.
    private const float DRAWER_WIDTH = 260f;
    private const float TOGGLE_HEIGHT = 46f;
    private const float BTN_HEIGHT = 46f;
    private const float MARGIN = 20f;
    private const float CURRENCY_CLEARANCE = 300f;
    // Below the Dev Tools toggle (which sits at MARGIN+CURRENCY_CLEARANCE, TOGGLE_HEIGHT tall).
    private const float TOGGLE_Y = MARGIN + CURRENCY_CLEARANCE + TOGGLE_HEIGHT + 8f;
    private const int BTN_FONT = 19;

    private void Start()
    {
        BuildUI();
    }

    private void Update()
    {
        if (!isOpen) return;
        UpdateStatus();
    }

    private void BuildUI()
    {
        GameObject canvasGO = GameObject.Find("UI-Test-Canvas");
        RectTransform canvas = canvasGO != null ? canvasGO.GetComponent<RectTransform>() : null;
        if (canvas == null)
        {
            Canvas any = FindFirstObjectByType<Canvas>();
            canvas = any != null ? any.GetComponent<RectTransform>() : null;
        }
        if (canvas == null) { Debug.LogWarning("[QuestDebug] No canvas found to host the drawer."); return; }

        Color btnBg = new Color(0.96f, 0.96f, 0.96f, 1f);
        Color btnText = Color.black;
        Color dangerBg = new Color(0.82f, 0.30f, 0.28f, 1f);
        Color dangerText = Color.white;

        // ── Toggle button (top-right, below Dev Tools) ──────────────────────
        GameObject toggleGO = CreateButton("QuestDebugToggleBtn", canvas);
        RectTransform toggleRT = toggleGO.GetComponent<RectTransform>();
        toggleRT.anchorMin = new Vector2(1, 1);
        toggleRT.anchorMax = new Vector2(1, 1);
        toggleRT.pivot = new Vector2(1, 1);
        toggleRT.anchoredPosition = new Vector2(-MARGIN, -TOGGLE_Y);
        toggleRT.sizeDelta = new Vector2(DRAWER_WIDTH, TOGGLE_HEIGHT);
        ApplyPillStyle(toggleGO, btnBg, btnText);
        SetButtonText(toggleGO, "▼ Quest Debug", 17);
        toggleText = toggleGO.GetComponentInChildren<TextMeshProUGUI>();
        if (toggleText != null) toggleText.fontStyle = FontStyles.Bold;
        toggleGO.GetComponent<Button>().onClick.AddListener(OnToggle);

        // ── Drawer panel (drops below the toggle) ───────────────────────────
        drawerGO = new GameObject("QuestDebugDrawer", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        drawerGO.transform.SetParent(canvas, false);
        drawerGO.layer = canvasGO != null ? canvasGO.layer : 5;

        RectTransform drawerRT = drawerGO.GetComponent<RectTransform>();
        drawerRT.anchorMin = new Vector2(1, 1);
        drawerRT.anchorMax = new Vector2(1, 1);
        drawerRT.pivot = new Vector2(1, 1);
        drawerRT.anchoredPosition = new Vector2(-MARGIN, -(TOGGLE_Y + TOGGLE_HEIGHT + 6f));
        drawerRT.sizeDelta = new Vector2(DRAWER_WIDTH, 400f);
        drawerGO.GetComponent<Image>().color = new Color(0.08f, 0.08f, 0.08f, 0.85f);

        VerticalLayoutGroup vlg = drawerGO.AddComponent<VerticalLayoutGroup>();
        vlg.padding = new RectOffset(12, 12, 12, 12);
        vlg.spacing = 6;
        vlg.childAlignment = TextAnchor.UpperCenter;
        vlg.childControlWidth = true;
        vlg.childControlHeight = true;
        vlg.childForceExpandWidth = true;
        vlg.childForceExpandHeight = false;

        MakeButton("Spawn Quest(s)",       btnBg,    btnText,    () => QuestManager.Instance?.DebugForceDrop());
        MakeButton("Complete All Quests",  btnBg,    btnText,    () => QuestManager.Instance?.DebugCompleteAll());
        MakeButton("Claim All Completed",  btnBg,    btnText,    () => QuestManager.Instance?.DebugClaimAll());
        MakeButton("Max Week Progress",    btnBg,    btnText,    () => QuestManager.Instance?.DebugClaimAllMilestones());
        MakeButton("Reset All Progress",   dangerBg, dangerText, () => QuestManager.Instance?.DebugResetAllProgress());
        MakeButton("Reset Daily",          dangerBg, dangerText, () => DailyRewardManager.Instance?.DebugResetDaily());

        GameObject statusGO = CreateText("QuestDebugStatus", drawerRT, 15);
        statusText = statusGO.GetComponent<TextMeshProUGUI>();
        statusText.color = new Color(1f, 0.92f, 0.45f);
        statusText.fontStyle = FontStyles.Bold;
        AddPreferredHeight(statusGO, 28f);

        drawerGO.SetActive(false);
    }

    private void OnToggle()
    {
        isOpen = !isOpen;
        if (drawerGO != null) drawerGO.SetActive(isOpen);
        if (toggleText != null) toggleText.text = isOpen ? "▲ Quest Debug" : "▼ Quest Debug";
        if (isOpen) UpdateStatus();
    }

    private void UpdateStatus()
    {
        if (statusText == null || QuestManager.Instance == null) return;
        int week = QuestManager.Instance.QuestsCompletedThisWeek;
        int active = QuestManager.Instance.ActiveQuestCount;
        statusText.text = $"Week: {week}/40    Active: {active}/10";
    }

    // ── Builders ────────────────────────────────────────────────────────────

    private void MakeButton(string label, Color bg, Color text, System.Action onClick)
    {
        GameObject go = CreateButton("QDBtn_" + label, drawerGO.GetComponent<RectTransform>());
        ApplyPillStyle(go, bg, text);
        SetButtonText(go, label, BTN_FONT);
        AddPreferredHeight(go, BTN_HEIGHT);
        go.GetComponent<Button>().onClick.AddListener(() => onClick());
    }

    private static void AddPreferredHeight(GameObject go, float height)
    {
        LayoutElement le = go.GetComponent<LayoutElement>();
        if (le == null) le = go.AddComponent<LayoutElement>();
        le.preferredHeight = height;
        le.minHeight = height;
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

        cachedPillSprite = Sprite.Create(tex, new Rect(0, 0, W, H), new Vector2(0.5f, 0.5f), W, 0,
            SpriteMeshType.FullRect, new Vector4(R, R, R, R));
        cachedPillSprite.name = "QuestDebugPill";
        return cachedPillSprite;
    }

    private GameObject CreateButton(string name, RectTransform parent)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);
        go.layer = parent.gameObject.layer;

        GameObject textGO = new GameObject("Text", typeof(RectTransform), typeof(CanvasRenderer));
        textGO.transform.SetParent(go.transform, false);
        textGO.layer = go.layer;
        TextMeshProUGUI tmp = textGO.AddComponent<TextMeshProUGUI>();
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = Color.black;
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
        go.layer = parent.gameObject.layer;
        TextMeshProUGUI tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.fontSize = size;
        tmp.color = Color.white;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.richText = true;
        if (font != null) tmp.font = font;
        return go;
    }

    private static void SetButtonText(GameObject btn, string text, float size)
    {
        TextMeshProUGUI tmp = btn.GetComponentInChildren<TextMeshProUGUI>();
        if (tmp != null) { tmp.text = text; tmp.fontSize = size; }
    }
}
#endif
