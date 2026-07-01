using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Wood rack sell panel. Shows current Wood and two exits:
/// - Sell for Cash (Money) — enabled only during an active run.
/// - Sell for Gold (Coins) — always enabled.
/// Stack controls (x1 / x10 / All) choose how much a sell button moves.
/// Lifecycle mirrors CarpenterPopupUITK.
/// </summary>
[RequireComponent(typeof(UIDocument))]
[DefaultExecutionOrder(1000)]
public class WoodRackPopupUITK : MonoBehaviour
{
    public static WoodRackPopupUITK Instance { get; private set; }

    [Header("Pricing")]
    [SerializeField] private int cashPricePerWood = 4;   // Money per wood, in-run only
    [SerializeField] private int goldPricePerWood = 1;   // Coins per wood, anytime

    public int CashPricePerWood => cashPricePerWood;
    public int GoldPricePerWood => goldPricePerWood;

    private WoodcuttingMath.StackMode stackMode = WoodcuttingMath.StackMode.One;

    private UIDocument document;
    private VisualElement root;
    private VisualElement popupRoot;
    private Label headerLabel;
    private Button closeButton;
    private Label woodCount;
    private Button stack1, stack10, stackAll;
    private Button sellCash, sellGold;
    private Label cashHint;

    private bool isOpen;
    private bool eventsSubscribed;
    private bool refreshPending;
    public bool IsOpen => isOpen;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        document = GetComponent<UIDocument>();
    }

    private void OnEnable()
    {
        CacheElements();
        WireCallbacks();
        TrySubscribeEvents();
    }

    private void Start()
    {
        if (root == null) { CacheElements(); WireCallbacks(); }
    }

    private void OnDisable() => UnsubscribeEvents();

    private void TrySubscribeEvents()
    {
        if (eventsSubscribed) return;
        if (CurrencyManager.Instance != null)
        {
            CurrencyManager.Instance.OnWoodChanged += OnWoodChanged;
            eventsSubscribed = true;
        }
        if (RunManager.Instance != null)
        {
            RunManager.Instance.OnRunStarted += OnRunStateChanged;
            RunManager.Instance.OnRunEnded += OnRunStateChanged;
        }
    }

    private void UnsubscribeEvents()
    {
        if (CurrencyManager.Instance != null)
            CurrencyManager.Instance.OnWoodChanged -= OnWoodChanged;
        if (RunManager.Instance != null)
        {
            RunManager.Instance.OnRunStarted -= OnRunStateChanged;
            RunManager.Instance.OnRunEnded -= OnRunStateChanged;
        }
        eventsSubscribed = false;
    }

    private void OnWoodChanged(int _) => MarkDirty();
    private void OnRunStateChanged() => MarkDirty();

    private void MarkDirty()
    {
        if (!isOpen || refreshPending || root == null) return;
        refreshPending = true;
        root.schedule.Execute(() =>
        {
            refreshPending = false;
            if (isOpen) Refresh();
        }).StartingIn(150);
    }

    private void CacheElements()
    {
        root = document.rootVisualElement;
        if (root == null) { Debug.LogError("[WoodRackPopupUITK] rootVisualElement is null"); return; }

        root.pickingMode = PickingMode.Ignore;

        popupRoot   = root.Q<VisualElement>("popup-root");
        headerLabel = root.Q<Label>("header-title");
        closeButton = root.Q<Button>("close-button");
        woodCount   = root.Q<Label>("wood-count");
        stack1      = root.Q<Button>("stack-1");
        stack10     = root.Q<Button>("stack-10");
        stackAll    = root.Q<Button>("stack-all");
        sellCash    = root.Q<Button>("sell-cash");
        sellGold    = root.Q<Button>("sell-gold");
        cashHint    = root.Q<Label>("cash-hint");

        if (headerLabel != null) headerLabel.text = "Wood Rack";
    }

    private void WireCallbacks()
    {
        if (closeButton != null) closeButton.RegisterCallback<ClickEvent>(_ => Close());
        if (stack1 != null)   stack1.RegisterCallback<ClickEvent>(_ => SetStack(WoodcuttingMath.StackMode.One));
        if (stack10 != null)  stack10.RegisterCallback<ClickEvent>(_ => SetStack(WoodcuttingMath.StackMode.Ten));
        if (stackAll != null) stackAll.RegisterCallback<ClickEvent>(_ => SetStack(WoodcuttingMath.StackMode.All));
        if (sellCash != null) sellCash.RegisterCallback<ClickEvent>(_ => SellForCash());
        if (sellGold != null) sellGold.RegisterCallback<ClickEvent>(_ => SellForGold());
    }

    private void SetStack(WoodcuttingMath.StackMode mode)
    {
        stackMode = mode;
        Refresh();
    }

    public void Open()
    {
        if (isOpen) return;
        isOpen = true;
        TrySubscribeEvents();
        if (root != null) root.pickingMode = PickingMode.Position;
        if (popupRoot != null)
        {
            popupRoot.style.display = DisplayStyle.Flex;
            popupRoot.schedule.Execute(() => popupRoot.AddToClassList("open")).StartingIn(0);
        }
        Refresh();
    }

    public void Close()
    {
        if (!isOpen) return;
        isOpen = false;
        if (popupRoot == null) return;
        popupRoot.RemoveFromClassList("open");
        popupRoot.schedule.Execute(() =>
        {
            if (isOpen) return;
            popupRoot.style.display = DisplayStyle.None;
            if (root != null) root.pickingMode = PickingMode.Ignore;
        }).StartingIn(260);
    }

    private void SellForCash()
    {
        var cm = CurrencyManager.Instance;
        bool inRun = RunManager.Instance != null && RunManager.Instance.IsRunActive;
        if (cm == null || !inRun) return;
        int amount = WoodcuttingMath.ResolveStackAmount(stackMode, cm.Wood);
        if (amount <= 0) return;
        if (!cm.SpendWood(amount)) return;
        cm.AddMoney(WoodcuttingMath.SellValue(amount, cashPricePerWood));
    }

    private void SellForGold()
    {
        var cm = CurrencyManager.Instance;
        if (cm == null) return;
        int amount = WoodcuttingMath.ResolveStackAmount(stackMode, cm.Wood);
        if (amount <= 0) return;
        if (!cm.SpendWood(amount)) return;
        cm.AddCoins(WoodcuttingMath.SellValue(amount, goldPricePerWood));
    }

    private void Refresh()
    {
        var cm = CurrencyManager.Instance;
        int wood = cm != null ? cm.Wood : 0;
        bool inRun = RunManager.Instance != null && RunManager.Instance.IsRunActive;
        int amount = WoodcuttingMath.ResolveStackAmount(stackMode, wood);

        if (woodCount != null) woodCount.text = $"Wood: {wood}";

        SetStackActive(stack1, stackMode == WoodcuttingMath.StackMode.One);
        SetStackActive(stack10, stackMode == WoodcuttingMath.StackMode.Ten);
        SetStackActive(stackAll, stackMode == WoodcuttingMath.StackMode.All);

        if (sellGold != null)
            sellGold.text = $"Sell for Gold  +{WoodcuttingMath.SellValue(amount, goldPricePerWood)}";

        if (sellCash != null)
        {
            sellCash.SetEnabled(inRun && amount > 0);
            sellCash.text = $"Sell for Cash  +{WoodcuttingMath.SellValue(amount, cashPricePerWood)}";
        }
        if (cashHint != null)
            cashHint.style.display = inRun ? DisplayStyle.None : DisplayStyle.Flex;

        if (sellGold != null) sellGold.SetEnabled(amount > 0);
    }

    private static void SetStackActive(Button b, bool active)
    {
        if (b == null) return;
        if (active) b.AddToClassList("stack-btn--active");
        else b.RemoveFromClassList("stack-btn--active");
    }
}
