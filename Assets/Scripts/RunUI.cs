using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Displays run information and handles end run button
/// </summary>
public class RunUI : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private TextMeshProUGUI runTimerText;
    [SerializeField] private Button startRunButton;
    [SerializeField] private Button endRunButton;
    [SerializeField] private Button equipFieldsButton;

    [Header("Display Settings")]
    [SerializeField] private bool showTimer = true;

    [Header("Speed Stepper Icons (optional)")]
    [Tooltip("If assigned, the speed-up (+) button shows this arrow sprite instead of a '+' glyph.")]
    [SerializeField] private Sprite speedUpArrow;
    [Tooltip("If assigned, the speed-down (-) button shows this arrow sprite instead of a '-' glyph.")]
    [SerializeField] private Sprite speedDownArrow;

    private bool hasShownInitialEquipment = false;
    private CameraPanController panController;

    private void Start()
    {
        if (startRunButton != null)
            startRunButton.onClick.AddListener(OnStartRunButtonClicked);

        if (endRunButton != null)
            endRunButton.onClick.AddListener(OnEndRunButtonClicked);

        if (equipFieldsButton != null)
            equipFieldsButton.onClick.AddListener(OnEquipFieldsClicked);

        BuildGameSpeedStepper();
        UpdateButtonStates();
    }

    // Timer-throttle cache: the run timer only shows whole seconds, so we avoid
    // rebuilding the string and touching TMP (mesh regen) on every frame.
    private int lastShownSecond = -1;
    private bool lastRunActive = false;
    private bool timerInitialized = false;

    private void Update()
    {
        // Show saved equipment on first frame after all singletons are ready
        if (!hasShownInitialEquipment && SeedSelectionPopup.Instance != null
            && EquipmentManager.Instance != null && FarmGrid.Instance != null
            && HelperManager.Instance != null)
        {
            hasShownInitialEquipment = true;
            ShowSavedEquipment();
            HelperManager.Instance.ShowHomeScreenHelpers();
        }

        // Update timer display only when the shown value actually changes.
        if (showTimer && runTimerText != null && RunManager.Instance != null)
        {
            bool runActive = RunManager.Instance.IsRunActive;
            if (runActive)
            {
                int sec = Mathf.FloorToInt(RunManager.Instance.CurrentRunDuration);
                if (!timerInitialized || !lastRunActive || sec != lastShownSecond)
                {
                    runTimerText.text = "Run Time: " + RunManager.Instance.GetFormattedRunDuration();
                    lastShownSecond = sec;
                }
            }
            else if (!timerInitialized || lastRunActive)
            {
                runTimerText.text = "No Active Run";
            }
            lastRunActive = runActive;
            timerInitialized = true;
        }

        // Update button states
        UpdateButtonStates();
    }

    private void UpdateButtonStates()
    {
        if (RunManager.Instance == null) return;

        bool inRun     = RunManager.Instance.IsRunActive;
        bool atMarket  = IsAtMarket();
        bool showHome  = !inRun && !atMarket;

        if (startRunButton != null)
            startRunButton.gameObject.SetActive(showHome);

        if (endRunButton != null)
            endRunButton.gameObject.SetActive(inRun);

        // Equip Fields only visible on home screen (pre-run), and never at Market.
        if (equipFieldsButton != null)
            equipFieldsButton.gameObject.SetActive(showHome);
    }

    private bool IsAtMarket()
    {
        if (panController == null)
        {
            if (Camera.main == null) return false;
            panController = Camera.main.GetComponent<CameraPanController>();
        }
        return panController != null && panController.CurrentLocation == CameraPanController.Location.Market;
    }

    // ── Game Speed Stepper (under the run timer) ──────────────────────────
    private TextMeshProUGUI speedLabel;
    private Image speedLabelBg;
    private Button speedDownBtn;
    private Button speedUpBtn;

    private void BuildGameSpeedStepper()
    {
        if (runTimerText == null) return;

        var container = new GameObject("GameSpeedStepper", typeof(RectTransform));
        container.transform.SetParent(runTimerText.rectTransform, false);
        var crt = container.GetComponent<RectTransform>();
        crt.anchorMin = new Vector2(0.5f, 0f);
        crt.anchorMax = new Vector2(0.5f, 0f);
        crt.pivot = new Vector2(0.5f, 1f);
        crt.anchoredPosition = new Vector2(0f, -34f);
        crt.sizeDelta = new Vector2(300f, 48f);

        speedDownBtn = CreateSpeedButton(container.transform, "-", 0f, 4f, () => OnSpeedStep(-1), speedDownArrow);
        speedUpBtn   = CreateSpeedButton(container.transform, "+", 1f, -4f, () => OnSpeedStep(1), speedUpArrow);

        // Center label with a dark pill behind it.
        var lblGO = new GameObject("SpeedLabel", typeof(RectTransform), typeof(Image));
        lblGO.transform.SetParent(container.transform, false);
        var lblImg = lblGO.GetComponent<Image>();
        lblImg.raycastTarget = false;
        speedLabelBg = lblImg; // colored per-tier in UpdateSpeedStepper
        var lrt = lblGO.GetComponent<RectTransform>();
        lrt.anchorMin = new Vector2(0.5f, 0.5f);
        lrt.anchorMax = new Vector2(0.5f, 0.5f);
        lrt.pivot = new Vector2(0.5f, 0.5f);
        lrt.sizeDelta = new Vector2(150f, 44f);
        lrt.anchoredPosition = Vector2.zero;

        var txtGO = new GameObject("text", typeof(RectTransform));
        txtGO.transform.SetParent(lblGO.transform, false);
        speedLabel = txtGO.AddComponent<TextMeshProUGUI>();
        speedLabel.alignment = TextAlignmentOptions.Center;
        speedLabel.fontSize = 26;
        speedLabel.fontStyle = FontStyles.Bold;
        speedLabel.color = Color.white; // recolored per-tier in UpdateSpeedStepper
        speedLabel.raycastTarget = false;
        var trt = txtGO.GetComponent<RectTransform>();
        trt.anchorMin = Vector2.zero; trt.anchorMax = Vector2.one;
        trt.offsetMin = Vector2.zero; trt.offsetMax = Vector2.zero;

        UpdateSpeedStepper();
    }

    private Button CreateSpeedButton(Transform parent, string glyph, float anchorX, float xOffset, System.Action onClick, Sprite icon = null)
    {
        var go = new GameObject("SpeedBtn", typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);
        go.GetComponent<Image>().color = new Color(0.20f, 0.20f, 0.20f, 0.88f);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(anchorX, 0.5f);
        rt.anchorMax = new Vector2(anchorX, 0.5f);
        rt.pivot = new Vector2(anchorX, 0.5f);
        rt.sizeDelta = new Vector2(64f, 44f);
        rt.anchoredPosition = new Vector2(xOffset, 0f);

        if (icon != null)
        {
            // Pixel-art arrow icon centered on the button (replaces the +/- glyph).
            var iGO = new GameObject("icon", typeof(RectTransform), typeof(Image));
            iGO.transform.SetParent(go.transform, false);
            var iImg = iGO.GetComponent<Image>();
            iImg.sprite = icon;
            iImg.raycastTarget = false;
            iImg.preserveAspect = true;
            var irt = iGO.GetComponent<RectTransform>();
            irt.anchorMin = new Vector2(0.5f, 0.5f);
            irt.anchorMax = new Vector2(0.5f, 0.5f);
            irt.pivot = new Vector2(0.5f, 0.5f);
            irt.sizeDelta = new Vector2(30f, 30f);
            irt.anchoredPosition = Vector2.zero;
        }
        else
        {
            var tGO = new GameObject("t", typeof(RectTransform));
            tGO.transform.SetParent(go.transform, false);
            var tmp = tGO.AddComponent<TextMeshProUGUI>();
            tmp.text = glyph;
            tmp.fontSize = 30;
            tmp.fontStyle = FontStyles.Bold;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = Color.white;
            tmp.raycastTarget = false;
            var trt = tGO.GetComponent<RectTransform>();
            trt.anchorMin = Vector2.zero; trt.anchorMax = Vector2.one;
            trt.offsetMin = Vector2.zero; trt.offsetMax = Vector2.zero;
        }

        go.GetComponent<Button>().onClick.AddListener(() => onClick());
        return go.GetComponent<Button>();
    }

    private void OnSpeedStep(int dir)
    {
        if (!GameSpeedControl.Step(dir)) return;            // clamped — no wrap
        if (RunManager.Instance != null) RunManager.Instance.RefreshGameSpeed();
        UpdateSpeedStepper();
    }

    private void UpdateSpeedStepper()
    {
        if (speedLabel != null)   speedLabel.text = "Speed " + GameSpeedControl.Label;
        if (speedDownBtn != null) speedDownBtn.interactable = !GameSpeedControl.AtMin;
        if (speedUpBtn != null)   speedUpBtn.interactable = !GameSpeedControl.AtMax;

        // Yellow pill + dark text on the dev-only 10/20/30× tiers; normal dark pill for 1–4×.
        bool dev = GameSpeedControl.IsDevSpeed;
        if (speedLabelBg != null) speedLabelBg.color = dev ? new Color(0.95f, 0.82f, 0.15f, 0.95f) : new Color(0f, 0f, 0f, 0.5f);
        if (speedLabel != null)   speedLabel.color   = dev ? new Color(0.2f, 0.15f, 0f) : Color.white;
    }

    private GameObject choosePlantsMsg;

    private void OnStartRunButtonClicked()
    {
        // Block starting a run with nothing planted — nudge the player to equip first.
        if (SeedSelectionPopup.Instance != null && !SeedSelectionPopup.Instance.HasAnyCropEquipped())
        {
            ShowChoosePlantsMessage();
            return;
        }

        if (RunManager.Instance != null)
            RunManager.Instance.StartNewRun();
    }

    /// <summary>Pops a brief "Choose your plants!" banner above the Equip Fields button.</summary>
    private void ShowChoosePlantsMessage()
    {
        if (equipFieldsButton == null) return;
        if (choosePlantsMsg != null) Destroy(choosePlantsMsg);

        var go = new GameObject("ChoosePlantsMsg", typeof(RectTransform), typeof(CanvasGroup));
        go.transform.SetParent(equipFieldsButton.transform, false);
        choosePlantsMsg = go;

        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 1f);
        rt.anchorMax = new Vector2(0.5f, 1f);
        rt.pivot = new Vector2(0.5f, 0f);
        rt.anchoredPosition = new Vector2(0f, 14f);
        rt.sizeDelta = new Vector2(380f, 70f);

        var bg = go.AddComponent<Image>();
        bg.color = new Color(0.62f, 0.12f, 0.12f, 0.94f);
        bg.raycastTarget = false;

        var txtGo = new GameObject("text", typeof(RectTransform));
        txtGo.transform.SetParent(go.transform, false);
        var txt = txtGo.AddComponent<TextMeshProUGUI>();
        txt.text = "Choose your plants!";
        txt.fontSize = 32;
        txt.fontStyle = FontStyles.Bold;
        txt.alignment = TextAlignmentOptions.Center;
        txt.color = Color.white;
        txt.raycastTarget = false;
        var trt = txtGo.GetComponent<RectTransform>();
        trt.anchorMin = Vector2.zero; trt.anchorMax = Vector2.one;
        trt.offsetMin = Vector2.zero; trt.offsetMax = Vector2.zero;

        var cg = go.GetComponent<CanvasGroup>();
        go.transform.localScale = Vector3.one * 0.6f;
        LeanTween.scale(go, Vector3.one, 0.2f).setEaseOutBack().setIgnoreTimeScale(true);
        LeanTween.alphaCanvas(cg, 0f, 0.4f).setDelay(1.3f).setIgnoreTimeScale(true);
        Destroy(go, 1.8f);
    }

    private void OnEndRunButtonClicked()
    {
        if (RunManager.Instance != null)
            RunManager.Instance.EndRun();
    }

    private void OnEquipFieldsClicked()
    {
        if (SeedSelectionPopup.Instance != null)
        {
            SeedSelectionPopup.Instance.OnSelectionSaved += OnFieldsSaved;
            SeedSelectionPopup.Instance.Show();
        }
    }

    private void OnFieldsSaved()
    {
        if (SeedSelectionPopup.Instance != null)
            SeedSelectionPopup.Instance.OnSelectionSaved -= OnFieldsSaved;

        // Refresh home screen equipment visuals
        ShowSavedEquipment();
    }

    /// <summary>
    /// Load saved equipment assignments and show visuals on home screen.
    /// </summary>
    private void ShowSavedEquipment()
    {
        if (SeedSelectionPopup.Instance != null)
            SeedSelectionPopup.Instance.LoadAndApplySavedSelections();

        if (EquipmentManager.Instance != null)
            EquipmentManager.Instance.ShowHomeScreenEquipment();
    }

    private void OnDestroy()
    {
        if (startRunButton != null)
            startRunButton.onClick.RemoveListener(OnStartRunButtonClicked);
        if (endRunButton != null)
            endRunButton.onClick.RemoveListener(OnEndRunButtonClicked);
        if (equipFieldsButton != null)
            equipFieldsButton.onClick.RemoveListener(OnEquipFieldsClicked);
    }
}