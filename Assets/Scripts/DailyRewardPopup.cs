using UnityEngine;
using UnityEngine.UIElements;

[RequireComponent(typeof(UIDocument))]
public class DailyRewardPopup : MonoBehaviour
{
    public static DailyRewardPopup Instance { get; private set; }

    [Header("Templates")]
    [SerializeField] private VisualTreeAsset dayCellTemplate;

    [Header("Chest button (opens this popup)")]
    [SerializeField] private UnityEngine.UI.Button chestButton;
    [SerializeField] private GameObject notificationDot;

    // ── UI Toolkit refs ─────────────────────────────────────────
    private UIDocument document;
    private VisualElement root;
    private VisualElement popupRoot;
    private VisualElement popupFrame;
    private VisualElement backdrop;
    private Button closeButton;
    private Label weeklyBonusLabel;
    private VisualElement calendarRow;

    private readonly VisualElement[] dayCells = new VisualElement[7];
    private bool isOpen;
    private bool hasCheckedDot;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        document = GetComponent<UIDocument>();
    }

    private void OnEnable()
    {
        CacheElements();
        if (root != null) WireCallbacks();
    }

    private void Start()
    {
        if (chestButton != null) chestButton.onClick.AddListener(Show);
        if (notificationDot != null) notificationDot.SetActive(false);
    }

    private void OnDestroy()
    {
        if (chestButton != null) chestButton.onClick.RemoveListener(Show);
    }

    private void Update()
    {
        if (!hasCheckedDot && DailyRewardManager.Instance != null)
        {
            hasCheckedDot = true;
            UpdateNotificationDot();
        }
    }

    private void CacheElements()
    {
        if (document == null) return;
        root = document.rootVisualElement;
        if (root == null) return;

        root.pickingMode = PickingMode.Ignore;

        popupRoot        = root.Q<VisualElement>("popup-root");
        popupFrame       = root.Q<VisualElement>("popup-frame");
        backdrop         = root.Q<VisualElement>("backdrop");
        closeButton      = root.Q<Button>("close-button");
        weeklyBonusLabel = root.Q<Label>("weekly-bonus");
        calendarRow      = root.Q<VisualElement>("calendar-row");
    }

    private void WireCallbacks()
    {
        if (closeButton != null) closeButton.clicked += Hide;
        if (backdrop != null) backdrop.RegisterCallback<ClickEvent>(_ => Hide());
    }

    // ── Public API ─────────────────────────────────────────────

    public void Show()
    {
        if (isOpen) return;
        isOpen = true;

        BuildCalendar();

        if (root != null) root.pickingMode = PickingMode.Position;
        if (popupRoot != null)
        {
            popupRoot.style.display = DisplayStyle.Flex;
            popupRoot.schedule.Execute(() => popupRoot.AddToClassList("open")).StartingIn(0);
        }
    }

    public void Hide()
    {
        if (!isOpen) return;
        isOpen = false;
        if (popupRoot != null)
        {
            popupRoot.RemoveFromClassList("open");
            popupRoot.schedule.Execute(() =>
            {
                if (isOpen) return;
                popupRoot.style.display = DisplayStyle.None;
                if (root != null) root.pickingMode = PickingMode.Ignore;
            }).StartingIn(260);
        }
    }

    // ── Handlers ───────────────────────────────────────────────

    private void OnClaimClicked()
    {
        if (DailyRewardManager.Instance == null) return;
        if (DailyRewardManager.Instance.ClaimToday())
        {
            BuildCalendar();
            UpdateNotificationDot();
        }
    }

    private void UpdateNotificationDot()
    {
        if (notificationDot == null) return;
        bool show = DailyRewardManager.Instance != null && DailyRewardManager.Instance.CanClaimToday;
        notificationDot.SetActive(show);
    }

    // ── Render ─────────────────────────────────────────────────

    private void BuildCalendar()
    {
        if (DailyRewardManager.Instance == null || calendarRow == null || dayCellTemplate == null) return;

        for (int i = 0; i < 7; i++)
        {
            if (dayCells[i] != null) dayCells[i].RemoveFromHierarchy();
            dayCells[i] = null;
        }

        int[] rewards = DailyRewardManager.Instance.DailyRewards;
        int todayIndex = DailyRewardManager.Instance.GetTodayIndex();

        for (int i = 0; i < 7; i++)
        {
            DayStatus status = DailyRewardManager.Instance.GetDayStatus(i);
            int reward = (i < rewards.Length) ? rewards[i] : 0;
            int gem = DailyRewardManager.Instance.GetDailyGemReward(i);

            TemplateContainer cell = dayCellTemplate.Instantiate();
            VisualElement cellRoot = cell.Q(className: "day-cell") ?? cell.contentContainer;

            Label dayName    = cell.Q<Label>("day-name");
            Label coinAmount = cell.Q<Label>("coin-amount");
            Label gemAmount  = cell.Q<Label>("gem-amount");
            Label statusLbl  = cell.Q<Label>("status-label");

            if (dayName != null) dayName.text = DailyRewardManager.Instance.GetDayName(i);
            if (coinAmount != null) coinAmount.text = reward.ToString();
            if (gemAmount != null)
            {
                if (gem > 0)
                {
                    gemAmount.text = "◆ " + gem;
                    gemAmount.style.display = DisplayStyle.Flex;
                }
                else
                {
                    gemAmount.style.display = DisplayStyle.None;
                }
            }

            cellRoot.RemoveFromClassList("day-cell--claimed");
            cellRoot.RemoveFromClassList("day-cell--today");
            cellRoot.RemoveFromClassList("day-cell--missed");
            cellRoot.RemoveFromClassList("day-cell--upcoming");

            switch (status)
            {
                case DayStatus.Claimed:
                    cellRoot.AddToClassList("day-cell--claimed");
                    if (statusLbl != null) statusLbl.text = "✓";
                    break;
                case DayStatus.Available:
                    cellRoot.AddToClassList("day-cell--today");
                    if (statusLbl != null) statusLbl.text = "Claim!";
                    // Today cell is the daily claim target.
                    cellRoot.RegisterCallback<ClickEvent>(_ => OnClaimClicked());
                    cellRoot.pickingMode = PickingMode.Position;
                    // Manual pressed-state toggling — :active doesn't fire on plain VisualElements.
                    VisualElement pressTarget = cellRoot;
                    pressTarget.RegisterCallback<PointerDownEvent>(_ => pressTarget.AddToClassList("day-cell--pressed"));
                    pressTarget.RegisterCallback<PointerUpEvent>(_ => pressTarget.RemoveFromClassList("day-cell--pressed"));
                    pressTarget.RegisterCallback<PointerLeaveEvent>(_ => pressTarget.RemoveFromClassList("day-cell--pressed"));
                    break;
                case DayStatus.Missed:
                    cellRoot.AddToClassList("day-cell--missed");
                    if (statusLbl != null) statusLbl.text = "Missed";
                    break;
                case DayStatus.Upcoming:
                    cellRoot.AddToClassList("day-cell--upcoming");
                    if (statusLbl != null) statusLbl.text = "";
                    break;
            }

            calendarRow.Add(cell);
            dayCells[i] = cell;
        }

        if (weeklyBonusLabel != null)
        {
            int claimed = DailyRewardManager.Instance.ClaimedCount;
            int weeklyGemBonus = DailyRewardManager.Instance.WeeklyGemBonus;
            int bonus = DailyRewardManager.Instance.WeeklyBonusReward;
            string gemSuffix = weeklyGemBonus > 0 ? $" & +{weeklyGemBonus} gems" : "";
            weeklyBonusLabel.text = DailyRewardManager.Instance.EarnedWeeklyBonus
                ? $"<color=#A56A1E>Weekly Bonus Earned! +{bonus} Coins{gemSuffix}</color>"
                : $"Claim all 7 days for +{bonus} bonus coins{gemSuffix} ({claimed}/7)";
        }
    }
}
