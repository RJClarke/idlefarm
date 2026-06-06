using UnityEngine;
using UnityEngine.UIElements;

[RequireComponent(typeof(UIDocument))]
[DefaultExecutionOrder(1000)]
public class ResearchPopupUITK : MonoBehaviour
{
    public static ResearchPopupUITK Instance { get; private set; }

    private UIDocument document;
    private VisualElement root;
    private VisualElement popupRoot;
    private Button closeButton;

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
        bool any = false;
        if (CurrencyManager.Instance != null)
        {
            CurrencyManager.Instance.OnCoinsChanged += OnCurrencyChanged;
            CurrencyManager.Instance.OnGemsChanged += OnCurrencyChanged;
            any = true;
        }
        if (ResearchManager.Instance != null)
        {
            ResearchManager.Instance.OnSlotUnlocked += OnSlotUnlocked;
            any = true;
        }
        eventsSubscribed = any;
    }

    private void UnsubscribeEvents()
    {
        if (!eventsSubscribed) return;
        if (CurrencyManager.Instance != null)
        {
            CurrencyManager.Instance.OnCoinsChanged -= OnCurrencyChanged;
            CurrencyManager.Instance.OnGemsChanged -= OnCurrencyChanged;
        }
        if (ResearchManager.Instance != null)
            ResearchManager.Instance.OnSlotUnlocked -= OnSlotUnlocked;
        eventsSubscribed = false;
    }

    private void OnCurrencyChanged(int _) => MarkDirty();
    private void OnSlotUnlocked(int _) => MarkDirty();

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
        if (root == null) { Debug.LogError("[ResearchPopupUITK] rootVisualElement is null"); return; }

        root.pickingMode = PickingMode.Ignore;

        popupRoot   = root.Q<VisualElement>("popup-root");
        closeButton = root.Q<Button>("close-button");
    }

    private void WireCallbacks()
    {
        if (closeButton != null) closeButton.RegisterCallback<ClickEvent>(_ => Close());
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

    private void Refresh()
    {
        if (root == null) return;
        for (int i = 0; i < ResearchManager.SlotCount; i++)
            RenderSlot(i);
    }

    private void RenderSlot(int slotIndex)
    {
        VisualElement card = root.Q<VisualElement>($"slot-{slotIndex}");
        if (card == null) return;

        card.Clear();
        card.RemoveFromClassList("slot-card--unlocked-empty");
        card.RemoveFromClassList("slot-card--locked");
        card.RemoveFromClassList("slot-card--affordable");

        ResearchManager mgr = ResearchManager.Instance;
        if (mgr == null) return;

        bool unlocked = mgr.IsSlotUnlocked(slotIndex);
        ResearchManager.SlotDefinition def = mgr.GetSlotDef(slotIndex);

        Label statusLabel = new Label();
        statusLabel.AddToClassList("slot-status");

        Label actionLabel = new Label();
        actionLabel.AddToClassList("slot-action");

        if (unlocked)
        {
            card.AddToClassList("slot-card--unlocked-empty");
            statusLabel.text = "No Active Research";
            actionLabel.text = "Tap to assign";
        }
        else if (def != null)
        {
            statusLabel.text = "Locked";
            switch (def.unlockType)
            {
                case ResearchManager.SlotUnlockType.Coins:
                    actionLabel.text = $"{def.costAmount} coins to unlock";
                    break;
                case ResearchManager.SlotUnlockType.Gems:
                    actionLabel.text = $"{def.costAmount} gems to unlock";
                    break;
                case ResearchManager.SlotUnlockType.Research:
                    actionLabel.text = "Research to unlock";
                    break;
            }

            bool canAfford = mgr.CanUnlockSlot(slotIndex);
            card.AddToClassList(canAfford ? "slot-card--affordable" : "slot-card--locked");

            if (canAfford)
            {
                int capturedIndex = slotIndex;
                card.RegisterCallback<ClickEvent>(_ =>
                {
                    ResearchManager.Instance?.TryUnlockSlot(capturedIndex);
                });
                WirePressedFeedback(card, "slot-card--pressed");
            }
        }

        card.Add(statusLabel);
        card.Add(actionLabel);
    }

    private static void WirePressedFeedback(VisualElement ve, string pressedClass)
    {
        ve.RegisterCallback<PointerDownEvent>(_ => ve.AddToClassList(pressedClass));
        ve.RegisterCallback<PointerUpEvent>(_ => ve.RemoveFromClassList(pressedClass));
        ve.RegisterCallback<PointerLeaveEvent>(_ => ve.RemoveFromClassList(pressedClass));
        ve.RegisterCallback<PointerCancelEvent>(_ => ve.RemoveFromClassList(pressedClass));
    }
}
