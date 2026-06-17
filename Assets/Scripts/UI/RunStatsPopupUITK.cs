using UnityEngine;
using UnityEngine.UIElements;

[RequireComponent(typeof(UIDocument))]
public class RunStatsPopupUITK : MonoBehaviour
{
    public static RunStatsPopupUITK Instance { get; private set; }

    [SerializeField] private GameObject prevRunStatsButton; // optional uGUI button (ported from TMP popup)
    private TMPro.TextMeshProUGUI prevRunStatsButtonText;

    private UIDocument document;
    private VisualElement root, popupRoot, ledger;
    private Label title, heroScore, heroReal, bankruptBanner;
    private Button closeButton;
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
        closeButton = root.Q<Button>("close-button");
        closeButton?.RegisterCallback<ClickEvent>(_ => Hide());
        root.Q<VisualElement>("backdrop")?.RegisterCallback<ClickEvent>(_ => Hide());
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

    /// <summary>Show the last run's stats (live or ingested-offline).</summary>
    public void Show() => Show(RunLedgerData.FromCurrentRun());

    public void Show(RunLedgerData d)
    {
        if (root == null) Cache();
        if (popupRoot == null) return;

        title.text = d.bankrupt ? "Run Over" : "Run Stats";
        heroScore.text = d.farmTimeHms;
        heroReal.text = "Real time played · " + d.realTimeHms;
        bankruptBanner.style.display = d.bankrupt ? DisplayStyle.Flex : DisplayStyle.None;
        RunStatsLedgerView.Build(ledger, d, compact: false);

        root.pickingMode = PickingMode.Position;
        popupRoot.style.display = DisplayStyle.Flex;
        popupRoot.schedule.Execute(() => popupRoot.AddToClassList("open")).StartingIn(0);
    }

    public void Hide()
    {
        if (popupRoot == null) return;
        popupRoot.RemoveFromClassList("open");
        popupRoot.schedule.Execute(() =>
        {
            popupRoot.style.display = DisplayStyle.None;
            if (root != null) root.pickingMode = PickingMode.Ignore;
        }).StartingIn(260);
    }
}
