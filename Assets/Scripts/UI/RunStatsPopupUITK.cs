using UnityEngine;
using UnityEngine.UIElements;

[RequireComponent(typeof(UIDocument))]
public class RunStatsPopupUITK : MonoBehaviour
{
    public static RunStatsPopupUITK Instance { get; private set; }

    [SerializeField] private GameObject prevRunStatsButton; // optional uGUI button (ported from TMP popup)
    private TMPro.TextMeshProUGUI prevRunStatsButtonText;

    private UIDocument document;
    private VisualElement root, popupRoot, ledger, welcomeRow, endRunRow, confirmOverlay;
    private Label title, heroScore, heroReal, bankruptBanner, endedBanner, welcomeAway;
    private Button xButton, endRunButton, confirmCancel, confirmEnd;
    private bool hasStatsToShow;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        document = GetComponent<UIDocument>();
    }

    private void OnEnable() => Cache();

    private void Cache()
    {
        if (document == null) document = GetComponent<UIDocument>();
        root = document != null ? document.rootVisualElement : null;
        if (root == null) return;
        root.pickingMode = PickingMode.Ignore;
        popupRoot = root.Q<VisualElement>("popup-root");
        ledger = root.Q<VisualElement>("ledger");
        title = root.Q<Label>("title");
        heroScore = root.Q<Label>("hero-score");
        heroReal = root.Q<Label>("hero-real");
        bankruptBanner = root.Q<Label>("bankrupt-banner");
        endedBanner = root.Q<Label>("ended-banner");
        welcomeRow = root.Q<VisualElement>("welcome-row");
        welcomeAway = root.Q<Label>("welcome-away");
        endRunRow = root.Q<VisualElement>("end-run-row");
        confirmOverlay = root.Q<VisualElement>("confirm-overlay");
        confirmCancel = root.Q<Button>("confirm-cancel");
        confirmEnd = root.Q<Button>("confirm-end");

        // Top-right ✕ dismisses the modal (replaces the old bottom Close button).
        xButton = root.Q<Button>("x-button");
        xButton?.RegisterCallback<ClickEvent>(_ => Hide());
        root.Q<VisualElement>("backdrop")?.RegisterCallback<ClickEvent>(_ => Hide());

        // End Run now asks first: show the "Are you sure?" overlay instead of ending immediately.
        endRunButton = root.Q<Button>("end-run-button");
        endRunButton?.RegisterCallback<ClickEvent>(_ => ShowConfirm(true));
        confirmCancel?.RegisterCallback<ClickEvent>(_ => ShowConfirm(false));
        // Confirm → end the run. RunManager.EndRun re-invokes Show(), which refreshes this same modal
        // into the "Run Complete" recap (End Run hidden, "manually ended" banner shown).
        confirmEnd?.RegisterCallback<ClickEvent>(_ =>
        {
            ShowConfirm(false);
            if (RunManager.Instance != null && RunManager.Instance.IsRunActive)
                RunManager.Instance.EndRun();
        });
    }

    private void Start()
    {
        if (prevRunStatsButton != null)
        {
            var btn = prevRunStatsButton.GetComponent<UnityEngine.UI.Button>();
            btn?.onClick.AddListener(Show);
            prevRunStatsButtonText = prevRunStatsButton.GetComponentInChildren<TMPro.TextMeshProUGUI>();
            prevRunStatsButton.SetActive(false);
        }
        if (RunManager.Instance != null)
        {
            RunManager.Instance.OnRunStarted += OnRunStarted;
            RunManager.Instance.OnRunEnded += OnRunEnded;

            // Resume race: SaveManager.ResumeRun fires OnRunStarted during load, possibly before
            // this Start() subscribed - catch up so the Run Stats button shows on a resumed run.
            if (RunManager.Instance.IsRunActive) OnRunStarted();
        }
    }

    private void OnDestroy()
    {
        if (RunManager.Instance != null)
        {
            RunManager.Instance.OnRunStarted -= OnRunStarted;
            RunManager.Instance.OnRunEnded -= OnRunEnded;
        }
    }

    private void OnRunStarted()
    {
        if (prevRunStatsButton != null) prevRunStatsButton.SetActive(true);
        if (prevRunStatsButtonText != null) prevRunStatsButtonText.text = "Run Stats";
    }

    private void OnRunEnded()
    {
        hasStatsToShow = true;
        if (prevRunStatsButton != null) prevRunStatsButton.SetActive(true);
        if (prevRunStatsButtonText != null) prevRunStatsButtonText.text = "Prev. Run Stats";
    }

    /// <summary>
    /// Show run stats. During an active run this is the CURRENT run (live duration + an End Run
    /// button); otherwise it's the just-ended / last run's recap.
    /// </summary>
    public void Show() => Show(
        (RunManager.Instance != null && RunManager.Instance.IsRunActive)
            ? RunLedgerData.FromActiveRun()
            : RunLedgerData.FromCurrentRun(),
        null);

    public void Show(RunLedgerData d) => Show(d, null);

    /// <summary>
    /// Show the run stats. When `welcomeAwayText` is non-null, this is the merged "came back AND lost the
    /// run" view: a "Welcome back" header + away-time appears above the score, and the bankruptcy banner
    /// is reworded to "Your run ended while away".
    /// </summary>
    public void Show(RunLedgerData d, string welcomeAwayText)
    {
        if (root == null) Cache();
        if (popupRoot == null) return;

        // Mutual exclusion: never stack with the welcome-back modal.
        OfflineProgressModalUITK.Instance?.HideImmediate();

        bool welcome = !string.IsNullOrEmpty(welcomeAwayText);
        if (welcomeRow != null) welcomeRow.style.display = welcome ? DisplayStyle.Flex : DisplayStyle.None;
        if (welcome && welcomeAway != null) welcomeAway.text = welcomeAwayText;

        // Viewing a live, in-progress run → "Current Run" header + small End Run action at the bottom.
        bool activeRun = !welcome && RunManager.Instance != null && RunManager.Instance.IsRunActive;
        if (endRunRow != null)
            endRunRow.style.display = activeRun ? DisplayStyle.Flex : DisplayStyle.None;

        // A finished, non-bankrupt run was ended by hand → "Run Complete" recap.
        bool manualEnd = !activeRun && !welcome && !d.bankrupt;

        title.text = d.bankrupt ? "Run Over" : (activeRun ? "Current Run" : "Run Complete");
        heroScore.text = d.farmTimeHms;
        heroReal.text = "Real time played · " + d.realTimeHms;
        if (bankruptBanner != null)
        {
            bankruptBanner.style.display = d.bankrupt ? DisplayStyle.Flex : DisplayStyle.None;
            // No emoji: the UITK panel has no emoji fallback on Android (renders as a blank gap).
            bankruptBanner.text = welcome
                ? "Your run ended while away — ran out of seed money"
                : "Bankrupt — ran out of seed money";
        }
        if (endedBanner != null)
            endedBanner.style.display = manualEnd ? DisplayStyle.Flex : DisplayStyle.None;

        // Never reopen already-showing the confirm overlay (e.g. reopened for the recap).
        ShowConfirm(false);

        RunStatsLedgerView.Build(ledger, d, compact: false);

        root.pickingMode = PickingMode.Position;
        popupRoot.style.display = DisplayStyle.Flex;
        popupRoot.schedule.Execute(() => popupRoot.AddToClassList("open")).StartingIn(0);
    }

    /// <summary>Toggle the "Are you sure?" confirmation overlay drawn over the modal.</summary>
    private void ShowConfirm(bool show)
    {
        if (confirmOverlay != null)
            confirmOverlay.style.display = show ? DisplayStyle.Flex : DisplayStyle.None;
    }

    public void Hide()
    {
        ShowConfirm(false);
        if (popupRoot == null) return;
        popupRoot.RemoveFromClassList("open");
        popupRoot.schedule.Execute(() =>
        {
            popupRoot.style.display = DisplayStyle.None;
            if (root != null) root.pickingMode = PickingMode.Ignore;
        }).StartingIn(260);
    }

    /// <summary>Hide instantly (no fade) — used for mutual exclusion when the other reopen modal opens.</summary>
    public void HideImmediate()
    {
        if (root == null) Cache();
        if (popupRoot == null) return;
        popupRoot.RemoveFromClassList("open");
        popupRoot.style.display = DisplayStyle.None;
        if (root != null) root.pickingMode = PickingMode.Ignore;
    }
}
