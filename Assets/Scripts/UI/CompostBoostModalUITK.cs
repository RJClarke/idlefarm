using System;
using Research;
using UnityEngine;
using UnityEngine.UIElements;

[RequireComponent(typeof(UIDocument))]
[DefaultExecutionOrder(1100)]
public class CompostBoostModalUITK : MonoBehaviour
{
    public static CompostBoostModalUITK Instance { get; private set; }

    /// <summary>Boost token pricing: (multiplier, durationSecs, compostCost).</summary>
    private static readonly (float multiplier, float durationSecs, int cost)[] Tokens = new[]
    {
        (2f,  4f * 3600f,   50),
        (3f,  4f * 3600f,  150),
        (4f,  4f * 3600f,  400),
        (2f, 12f * 3600f,  120),
        (3f, 12f * 3600f,  360),
        (4f, 12f * 3600f, 1000),
    };

    private UIDocument document;
    private VisualElement root;
    private VisualElement modalRoot;
    private VisualElement boostList;
    private VisualElement autoBuyList;
    private VisualElement activeBanner;
    private Label activeBannerText;
    private Label compostBalanceLabel;
    private Label autoBuyCurrentLabel;
    private Button closeButton;
    private VisualElement backdrop;
    private IVisualElementScheduledItem refreshTicker;

    private int targetSlotIndex = -1;
    private bool isOpen;
    private bool subscribed;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        document = GetComponent<UIDocument>();
    }

    private void OnEnable()  { CacheAndWire(); TrySubscribe(); }
    private void OnDisable() => Unsubscribe();
    private void Start()     { if (root == null) CacheAndWire(); }

    private void CacheAndWire()
    {
        root = document.rootVisualElement;
        if (root == null) return;
        root.pickingMode = PickingMode.Ignore;

        modalRoot          = root.Q<VisualElement>("modal-root");
        boostList          = root.Q<VisualElement>("boost-list");
        autoBuyList        = root.Q<VisualElement>("auto-buy-list");
        activeBanner       = root.Q<VisualElement>("active-banner");
        activeBannerText   = root.Q<Label>("active-banner-text");
        compostBalanceLabel = root.Q<Label>("compost-balance");
        autoBuyCurrentLabel = root.Q<Label>("auto-buy-current");
        closeButton        = root.Q<Button>("modal-close");
        backdrop           = root.Q<VisualElement>("modal-backdrop");

        if (closeButton != null) closeButton.RegisterCallback<ClickEvent>(_ => Close());
        if (backdrop != null)    backdrop.RegisterCallback<ClickEvent>(_ => Close());
    }

    private void TrySubscribe()
    {
        if (subscribed) return;
        if (CurrencyManager.Instance != null)
        {
            CurrencyManager.Instance.OnCompostChanged += OnCompostChanged;
            subscribed = true;
        }
        if (ResearchManager.Instance != null)
        {
            ResearchManager.Instance.OnSlotStateChanged += OnSlotStateChanged;
            subscribed = true;
        }
    }

    private void Unsubscribe()
    {
        if (!subscribed) return;
        if (CurrencyManager.Instance != null) CurrencyManager.Instance.OnCompostChanged -= OnCompostChanged;
        if (ResearchManager.Instance != null) ResearchManager.Instance.OnSlotStateChanged -= OnSlotStateChanged;
        subscribed = false;
    }

    private void OnCompostChanged(int _) { if (isOpen) Rebuild(); }
    private void OnSlotStateChanged(int slotIndex) { if (isOpen && slotIndex == targetSlotIndex) Rebuild(); }

    public void Open(int slotIndex)
    {
        if (root == null) CacheAndWire();
        TrySubscribe();
        targetSlotIndex = slotIndex;
        isOpen = true;
        if (root != null) root.pickingMode = PickingMode.Position;
        if (modalRoot != null) modalRoot.style.display = DisplayStyle.Flex;
        Rebuild();
        refreshTicker?.Pause();
        refreshTicker = root?.schedule.Execute(() => { if (isOpen) Rebuild(); }).Every(1000);
    }

    public void Close()
    {
        isOpen = false;
        targetSlotIndex = -1;
        refreshTicker?.Pause();
        refreshTicker = null;
        if (modalRoot != null) modalRoot.style.display = DisplayStyle.None;
        if (root != null) root.pickingMode = PickingMode.Ignore;
    }

    private void Rebuild()
    {
        if (boostList == null) return;

        int compostBalance = CurrencyManager.Instance != null ? CurrencyManager.Instance.Compost : 0;
        if (compostBalanceLabel != null) compostBalanceLabel.text = $"{compostBalance:N0}";

        ResearchSlotState slot = ResearchManager.Instance != null
            ? ResearchManager.Instance.GetSlot(targetSlotIndex)
            : null;
        DateTime now = DateTime.UtcNow;
        bool boostActive = slot != null && slot.HasActiveBoost(now);

        // Active boost banner
        if (activeBanner != null)
        {
            if (boostActive)
            {
                double leftSecs = (slot.boostExpiresUtcTicks - now.Ticks) / (double)TimeSpan.TicksPerSecond;
                activeBannerText.text = $"Active: {slot.boostMultiplier:F0}× — {FormatRemaining(leftSecs)} left";
                activeBanner.style.display = DisplayStyle.Flex;
            }
            else
            {
                activeBanner.style.display = DisplayStyle.None;
            }
        }

        // Purchase rows
        boostList.Clear();
        foreach (var token in Tokens)
        {
            var row = new VisualElement(); row.AddToClassList("boost-row");
            string hrs = (token.durationSecs / 3600f).ToString("F0");
            var label = new Label($"{token.multiplier:F0}× for {hrs} hr"); label.AddToClassList("boost-row__label");
            var cost  = BuildCompostCost($"{token.cost}", "boost-row__cost");
            row.Add(label); row.Add(cost);

            bool affordable = compostBalance >= token.cost;
            bool clickable  = affordable && !boostActive;

            if (!clickable) row.AddToClassList("boost-row--disabled");
            if (clickable)
            {
                var captured = token;
                row.RegisterCallback<ClickEvent>(_ =>
                {
                    if (ResearchManager.Instance != null &&
                        ResearchManager.Instance.TryApplyBoost(targetSlotIndex, captured.multiplier, captured.durationSecs, captured.cost))
                    {
                        // Stay open so the user can configure auto-buy now that the boost is running.
                        Rebuild();
                    }
                });
            }
            boostList.Add(row);
        }

        // Auto-buy section
        BuildAutoBuyList(slot);
    }

    private void BuildAutoBuyList(ResearchSlotState slot)
    {
        if (autoBuyList == null) return;
        autoBuyList.Clear();

        bool hasAuto = slot != null && slot.HasAutoBuy;
        if (autoBuyCurrentLabel != null)
        {
            autoBuyCurrentLabel.text = hasAuto
                ? $"Currently: {slot.autoBuyMultiplier:F0}× for {(slot.autoBuyDurationSecs / 3600f):F0} hr ({slot.autoBuyCost} compost)"
                : "Off";
        }

        // "Off" row first
        var offRow = new VisualElement(); offRow.AddToClassList("auto-buy-row");
        var offLabel = new Label("Off — don't auto-buy"); offLabel.AddToClassList("auto-buy-row__label");
        offRow.Add(offLabel);
        if (!hasAuto) offRow.AddToClassList("auto-buy-row--selected");
        offRow.RegisterCallback<ClickEvent>(_ =>
        {
            ResearchManager.Instance?.SetAutoBuyBoost(targetSlotIndex, 0f, 0f, 0);
            Rebuild();
        });
        autoBuyList.Add(offRow);

        foreach (var token in Tokens)
        {
            var row = new VisualElement(); row.AddToClassList("auto-buy-row");
            string hrs = (token.durationSecs / 3600f).ToString("F0");
            var label = new Label($"{token.multiplier:F0}× for {hrs} hr"); label.AddToClassList("auto-buy-row__label");
            var cost  = BuildCompostCost($"{token.cost}", "auto-buy-row__cost");
            row.Add(label); row.Add(cost);

            bool selected = hasAuto
                            && Mathf.Approximately(slot.autoBuyMultiplier,   token.multiplier)
                            && Mathf.Approximately(slot.autoBuyDurationSecs, token.durationSecs)
                            && slot.autoBuyCost == token.cost;
            if (selected) row.AddToClassList("auto-buy-row--selected");

            var captured = token;
            row.RegisterCallback<ClickEvent>(_ =>
            {
                ResearchManager.Instance?.SetAutoBuyBoost(targetSlotIndex, captured.multiplier, captured.durationSecs, captured.cost);
                Rebuild();
            });
            autoBuyList.Add(row);
        }
    }

    private static string FormatRemaining(double secs)
    {
        if (secs < 0) secs = 0;
        long total = (long)secs;
        long hrs  = total / 3600;  total -= hrs  * 3600;
        long mins = total / 60;    total -= mins * 60;
        long s    = total;
        if (hrs > 0)  return $"{hrs}h {mins}m {s}s";
        if (mins > 0) return $"{mins}m {s}s";
        return $"{s}s";
    }

    /// <summary>Cost chip: amount text + a compost icon (UI Toolkit has no inline sprites).</summary>
    private static VisualElement BuildCompostCost(string amount, string costClass)
    {
        var chip = new VisualElement();
        chip.AddToClassList(costClass);
        chip.Add(new Label(amount));
        var icon = new VisualElement();
        icon.AddToClassList("currency-icon");
        icon.AddToClassList("currency-icon--compost");
        chip.Add(icon);
        return chip;
    }
}
