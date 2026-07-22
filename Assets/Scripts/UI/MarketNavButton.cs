using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Map nav button for the Market. Hidden while a run is active (the Market trip is disruptive
/// mid-run) AND while at the Market itself — a cloned "Back to Farm →" twin, anchored bottom-right
/// at the same height, owns the return trip.
/// </summary>
public class MarketNavButton : MapNavButton
{
    [Header("Back Button")]
    [Tooltip("Arrow sprite shown after the 'Back to Farm' text. Uses the same Raven up-arrow as the " +
             "run Speed buttons; the code rotates it 90° clockwise so it points right.")]
    [SerializeField] private Sprite backArrowSprite;

    [Tooltip("Extra width added to the cloned back button so 'Back to Farm' + arrow fit comfortably.")]
    [SerializeField] private float backButtonExtraWidth = 80f;

    private GameObject backButton;

    protected override CameraPanController.Location Target => CameraPanController.Location.Market;

    // The dedicated back button handles the return trip; this button only ever pans TO the Market.
    protected override bool TogglesBackToFarm => false;

    protected override bool ShouldHide(CameraPanController.Location current)
        => (RunManager.Instance != null && RunManager.Instance.IsRunActive)
           || current == CameraPanController.Location.Market;

    protected override void Start()
    {
        base.Start();
        if (panController == null) return;
        BuildBackButton();
        RefreshBackVisibility(panController.CurrentLocation);
    }

    protected override void SubscribeExtra()
    {
        if (RunManager.Instance != null)
        {
            RunManager.Instance.OnRunStarted += OnRunChanged;
            RunManager.Instance.OnRunEnded   += OnRunChanged;

            // Resume race: SaveManager.ResumeRun fires OnRunStarted during load, possibly before
            // we subscribed - re-sync visibility so the button state matches the resumed run.
            if (RunManager.Instance.IsRunActive) OnRunChanged();
        }
        // Swap the pair at pan START, mirroring how this button hides itself.
        if (panController != null) panController.OnPanStarted += RefreshBackVisibility;
    }

    protected override void UnsubscribeExtra()
    {
        if (RunManager.Instance != null)
        {
            RunManager.Instance.OnRunStarted -= OnRunChanged;
            RunManager.Instance.OnRunEnded   -= OnRunChanged;
        }
        if (panController != null) panController.OnPanStarted -= RefreshBackVisibility;
    }

    private void OnRunChanged() => RefreshVisibility();

    /// <summary>
    /// Clone this button into a "Back to Farm →" twin so it inherits the exact nav-button style,
    /// mirrored to the bottom-RIGHT corner at the same height. Shown only while at the Market.
    /// </summary>
    private void BuildBackButton()
    {
        backButton = Instantiate(gameObject, transform.parent);
        backButton.name = "MarketBackToFarmButton";

        // The clone must not behave like a second Market nav button.
        Destroy(backButton.GetComponent<MarketNavButton>());

        Button b = backButton.GetComponent<Button>();
        if (b != null)
        {
            b.onClick.RemoveAllListeners();
            b.onClick.AddListener(OnBackClicked);
        }

        TMP_Text txt = backButton.GetComponentInChildren<TMP_Text>(true);
        if (txt != null)
        {
            // Longer than "🏪 Market": keep it on one line, shrinking to fit if needed.
            // (No "→" glyph — NotoSans lacks U+2192; a Raven arrow sprite sits after the text.)
            txt.text = "Back to Farm";
            txt.textWrappingMode = TextWrappingModes.NoWrap;
            txt.fontSizeMax = txt.fontSize;
            txt.enableAutoSizing = true;

            if (backArrowSprite != null)
            {
                // Inset the label past the arrow (36px wide, 14px from the right edge) so the
                // auto-sized text never slides under it.
                RectTransform lrt = txt.rectTransform;
                lrt.offsetMax = new Vector2(lrt.offsetMax.x - 54f, lrt.offsetMax.y);

                var iconGO = new GameObject("ArrowIcon", typeof(RectTransform), typeof(Image));
                iconGO.transform.SetParent(backButton.transform, false);
                Image icon = iconGO.GetComponent<Image>();
                icon.sprite = backArrowSprite;
                icon.preserveAspect = true;
                icon.raycastTarget = false;
                RectTransform irt = iconGO.GetComponent<RectTransform>();
                irt.anchorMin = new Vector2(1f, 0.5f);
                irt.anchorMax = new Vector2(1f, 0.5f);
                // Center pivot so the rotation below spins the icon in place.
                irt.pivot     = new Vector2(0.5f, 0.5f);
                irt.sizeDelta = new Vector2(36f, 36f);
                irt.anchoredPosition = new Vector2(-32f, 0f);
                // The Raven up-arrow (shared with the Speed buttons) rotated to point right.
                irt.localEulerAngles = new Vector3(0f, 0f, -90f);
            }
        }

        // Mirror this button's bottom-left placement across to the bottom-right corner, widened so
        // the longer "Back to Farm" label fits on one line at full size.
        RectTransform src = GetComponent<RectTransform>();
        RectTransform rt = backButton.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(1f, 0f);
        rt.anchorMax = new Vector2(1f, 0f);
        rt.pivot     = new Vector2(1f, src.pivot.y);
        rt.sizeDelta = new Vector2(src.sizeDelta.x + backButtonExtraWidth, src.sizeDelta.y);
        rt.anchoredPosition = new Vector2(-src.anchoredPosition.x, src.anchoredPosition.y);

        backButton.SetActive(false);
    }

    private void OnBackClicked()
    {
        if (panController != null) panController.PanTo(CameraPanController.Location.Farm);
    }

    private void RefreshBackVisibility(CameraPanController.Location loc)
    {
        if (backButton != null)
            backButton.SetActive(loc == CameraPanController.Location.Market);
    }

    protected override void OnDestroy()
    {
        base.OnDestroy();
        if (backButton != null) Destroy(backButton);
    }
}
