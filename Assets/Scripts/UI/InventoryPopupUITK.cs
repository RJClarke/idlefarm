using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Inventory mega-list: every good the player holds, plus a fixed staging zone that sells any of
/// them for Gold in x1 / x10 / All quantities (Reputation Phase 1, spec §5.3). Tapping a row stages
/// it; the zone's stack buttons and confirm do the selling. Everything here pays Coins — Wood's
/// Cash sale stays exclusive to the Wood Rack. Live-updates off manager events while open.
/// Lifecycle mirrors WoodRackPopupUITK.
///
/// The zone is declared in UXML OUTSIDE the ScrollView on purpose: BuildRows() calls list.Clear()
/// on every currency event, and UITK's Clear() does not detach an element's own callbacks, so a
/// zone rebuilt inside the list would accumulate a handler per coin tick.
/// </summary>
[RequireComponent(typeof(UIDocument))]
[DefaultExecutionOrder(1000)]
public class InventoryPopupUITK : MonoBehaviour
{
    public static InventoryPopupUITK Instance { get; private set; }

    /// <summary>One sellable line item. The zone renders whatever it is handed, so a new sellable
    /// good means one more AddSellRow call in BuildRows — the zone itself never changes.</summary>
    private sealed class SellEntry
    {
        public string Id;             // stable across rebuilds: "crop:Carrot", "eggs", "wood", "fish:raw:1", "jars"
        public string Name;
        public Sprite Sprite;         // runtime sprite (crops); null when IconClass is used
        public string IconClass;      // space-separated USS classes; null when Sprite is used
        public int Held;
        public string ValueText;      // optional row-value override (Wood shows "222 / 1,000")
        public Func<int, int> Payout; // Gold for selling n
        public Action<int> Sell;      // sell n
    }

    /// <summary>Every icon class the zone icon can carry, so it can be reset before re-skinning.</summary>
    private static readonly string[] AllIconClasses =
    {
        "zone-icon--empty", "inv-icon--compost", "inv-icon--wood", "inv-icon--eggs",
        "inv-icon--jars", "inv-icon--fish-perch", "inv-icon--fish-bass",
        "inv-icon--fish-pike", "inv-icon--fish-perch-smoked",
        "inv-icon--fish-bass-smoked", "inv-icon--fish-pike-smoked"
    };

    [Header("Data")]
    [SerializeField] private CropDatabase cropDatabase;

    [Header("Pricing")]
    [Tooltip("Gold paid per Compost sold from the Inventory. Compost is a research-boost currency " +
             "first and a sink second, so this is deliberately a token rate — it exists to drain " +
             "an overflowing pile, not to be a viable income source.")]
    [SerializeField] private int compostSellCoins = 1;

    private UIDocument document;
    private VisualElement root, popupRoot, backdrop, sellZone, zoneIcon;
    private Button closeButton, stack1, stack10, stackAll, zoneConfirm;
    private Label zoneTitle, csCoins, csGems, csMoney;
    private ScrollView list;

    private readonly Dictionary<string, SellEntry> entries = new Dictionary<string, SellEntry>();
    private readonly Dictionary<string, VisualElement> rowsById = new Dictionary<string, VisualElement>();

    private string stagedId;
    private WoodcuttingMath.StackMode stackMode = WoodcuttingMath.StackMode.One;

    private bool isOpen;
    private bool eventsSubscribed;
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

    private void CacheElements()
    {
        root = document.rootVisualElement;
        if (root == null) { Debug.LogError("[Inventory] rootVisualElement is null"); return; }

        root.pickingMode = PickingMode.Ignore;
        popupRoot   = root.Q<VisualElement>("popup-root");
        backdrop    = root.Q<VisualElement>("backdrop");
        closeButton = root.Q<Button>("close-button");
        list        = root.Q<ScrollView>("inventory-list");

        csCoins     = root.Q<Label>("cs-coins");
        csGems      = root.Q<Label>("cs-gems");
        csMoney     = root.Q<Label>("cs-money");

        sellZone    = root.Q<VisualElement>("sell-zone");
        zoneIcon    = root.Q<VisualElement>("zone-icon");
        zoneTitle   = root.Q<Label>("zone-title");
        stack1      = root.Q<Button>("stack-1");
        stack10     = root.Q<Button>("stack-10");
        stackAll    = root.Q<Button>("stack-all");
        zoneConfirm = root.Q<Button>("zone-confirm");
    }

    // Registered exactly once — the zone is static UXML, never rebuilt.
    private void WireCallbacks()
    {
        if (closeButton != null) closeButton.RegisterCallback<ClickEvent>(_ => Close());
        if (backdrop != null)    backdrop.RegisterCallback<ClickEvent>(_ => Close());

        if (stack1 != null)   stack1.RegisterCallback<ClickEvent>(_ => SetStack(WoodcuttingMath.StackMode.One));
        if (stack10 != null)  stack10.RegisterCallback<ClickEvent>(_ => SetStack(WoodcuttingMath.StackMode.Ten));
        if (stackAll != null) stackAll.RegisterCallback<ClickEvent>(_ => SetStack(WoodcuttingMath.StackMode.All));

        if (zoneConfirm != null) zoneConfirm.RegisterCallback<ClickEvent>(_ => Confirm());
    }

    private void TrySubscribeEvents()
    {
        if (eventsSubscribed || CurrencyManager.Instance == null) return;
        var cm = CurrencyManager.Instance;
        cm.OnCoinsChanged   += OnAnyChanged;
        cm.OnGemsChanged    += OnAnyChanged;
        cm.OnMoneyChanged   += OnAnyChanged;
        cm.OnWoodChanged    += OnAnyChanged;
        cm.OnCompostChanged += OnAnyChanged;
        if (ItemInventoryManager.Instance != null) ItemInventoryManager.Instance.OnChanged += OnInvChanged;
        if (PantryManager.Instance != null)        PantryManager.Instance.OnChanged += OnInvChanged;
        if (CanneryManager.Instance != null)       CanneryManager.Instance.OnChanged += OnInvChanged;
        eventsSubscribed = true;
    }

    private void UnsubscribeEvents()
    {
        if (!eventsSubscribed || CurrencyManager.Instance == null) { eventsSubscribed = false; return; }
        var cm = CurrencyManager.Instance;
        cm.OnCoinsChanged   -= OnAnyChanged;
        cm.OnGemsChanged    -= OnAnyChanged;
        cm.OnMoneyChanged   -= OnAnyChanged;
        cm.OnWoodChanged    -= OnAnyChanged;
        cm.OnCompostChanged -= OnAnyChanged;
        if (ItemInventoryManager.Instance != null) ItemInventoryManager.Instance.OnChanged -= OnInvChanged;
        if (PantryManager.Instance != null)        PantryManager.Instance.OnChanged -= OnInvChanged;
        if (CanneryManager.Instance != null)       CanneryManager.Instance.OnChanged -= OnInvChanged;
        eventsSubscribed = false;
    }

    private void OnAnyChanged(int _) { if (isOpen) Refresh(); }
    private void OnInvChanged()      { if (isOpen) Refresh(); }

    public void Open()
    {
        if (isOpen) return;
        isOpen = true;
        TrySubscribeEvents();
        if (root != null) root.pickingMode = PickingMode.Position;
        Refresh();
        if (popupRoot != null)
        {
            popupRoot.style.display = DisplayStyle.Flex;
            popupRoot.schedule.Execute(() => popupRoot.AddToClassList("open")).StartingIn(0);
        }
    }

    public void Close()
    {
        if (!isOpen) return;
        isOpen = false;
        // Reset staging so the panel never reopens already armed on "All".
        stagedId = null;
        stackMode = WoodcuttingMath.StackMode.One;
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
        BuildRows();
        RenderCurrencyStrip();
        RenderSellZone();
    }

    // ── Staging ──────────────────────────────────────────────────────────

    /// <summary>Stage a row. Deliberately does NOT rebuild the list: it only moves a CSS class and
    /// re-renders the zone, so we never clear the ScrollView from inside one of its children's
    /// click handlers.</summary>
    private void Stage(string id)
    {
        if (stagedId == id) return;
        if (stagedId != null && rowsById.TryGetValue(stagedId, out var prev))
            prev.RemoveFromClassList("inv-row--staged");
        stagedId = id;
        if (rowsById.TryGetValue(id, out var next))
            next.AddToClassList("inv-row--staged");
        RenderSellZone();
    }

    private void SetStack(WoodcuttingMath.StackMode mode)
    {
        stackMode = mode;
        RenderSellZone();
    }

    private void Confirm()
    {
        if (stagedId == null || !entries.TryGetValue(stagedId, out SellEntry e)) return;
        int n = WoodcuttingMath.ResolveStackAmount(stackMode, e.Held);
        if (n <= 0) return;
        int payout = e.Payout(n);
        e.Sell(n);
        Debug.Log($"[Inventory] Sold {n} x {e.Name} for {payout} Gold.");
        // Managers fire OnChanged too; Refresh is idempotent, and this covers goods whose sell path
        // only raises CurrencyManager int events.
        Refresh();
    }

    // ── Rendering ────────────────────────────────────────────────────────

    private void RenderCurrencyStrip()
    {
        var cm = CurrencyManager.Instance;
        if (cm == null) return;
        if (csCoins != null) csCoins.text = cm.Coins.ToString("N0");
        if (csGems  != null) csGems.text  = cm.Gems.ToString("N0");
        if (csMoney != null) csMoney.text = cm.Money.ToString("N0");
    }

    private void RenderSellZone()
    {
        if (sellZone == null) return;

        SellEntry e = null;
        if (stagedId != null) entries.TryGetValue(stagedId, out e);
        if (e != null && e.Held <= 0) e = null;
        if (e == null) stagedId = null;

        SetStackActive(stack1,   stackMode == WoodcuttingMath.StackMode.One);
        SetStackActive(stack10,  stackMode == WoodcuttingMath.StackMode.Ten);
        SetStackActive(stackAll, stackMode == WoodcuttingMath.StackMode.All);

        bool staged = e != null;
        if (stack1 != null)   stack1.SetEnabled(staged);
        if (stack10 != null)  stack10.SetEnabled(staged);
        if (stackAll != null) stackAll.SetEnabled(staged);

        if (!staged)
        {
            // Controls stay visible-but-disabled: they are what teaches the interaction.
            ApplyIcon(zoneIcon, null, "zone-icon--empty");
            if (zoneTitle != null)   zoneTitle.text = "Tap an item to sell it";
            if (zoneConfirm != null) { zoneConfirm.text = "Sell for Coins"; zoneConfirm.SetEnabled(false); }
            return;
        }

        ApplyIcon(zoneIcon, e.Sprite, e.IconClass);
        if (zoneTitle != null) zoneTitle.text = $"{e.Name}   {e.Held:N0} held";

        int n = WoodcuttingMath.ResolveStackAmount(stackMode, e.Held);
        if (zoneConfirm != null)
        {
            zoneConfirm.text = n > 0 ? $"Sell {n:N0} for {e.Payout(n):N0} Coins" : "Sell for Coins";
            zoneConfirm.SetEnabled(n > 0);
        }
    }

    private static void SetStackActive(Button b, bool active)
    {
        if (b == null) return;
        if (active) b.AddToClassList("stack-btn--active");
        else b.RemoveFromClassList("stack-btn--active");
    }

    /// <summary>Skin an icon element from either a runtime Sprite (crops) or USS classes. Clears
    /// both channels first so a reused element (the zone icon) never keeps its previous look.</summary>
    private static void ApplyIcon(VisualElement el, Sprite sprite, string iconClasses)
    {
        if (el == null) return;
        for (int i = 0; i < AllIconClasses.Length; i++) el.RemoveFromClassList(AllIconClasses[i]);
        el.style.backgroundImage = StyleKeyword.Null;
        el.style.unityBackgroundImageTintColor = StyleKeyword.Null;
        el.style.opacity = StyleKeyword.Null;

        if (sprite != null) { el.style.backgroundImage = new StyleBackground(sprite); return; }
        if (string.IsNullOrEmpty(iconClasses)) return;
        foreach (string c in iconClasses.Split(' '))
            if (!string.IsNullOrEmpty(c)) el.AddToClassList(c);
    }

    // Smoked rows use the dedicated cured art rather than a tint over the raw sprite, matching
    // the Smokehouse panel.
    private static string FishIconClass(int tier, bool smoked = false)
    {
        switch (tier)
        {
            case 1:  return smoked ? "inv-icon--fish-perch-smoked" : "inv-icon--fish-perch";
            case 2:  return smoked ? "inv-icon--fish-bass-smoked"  : "inv-icon--fish-bass";
            default: return smoked ? "inv-icon--fish-pike-smoked"  : "inv-icon--fish-pike";
        }
    }

    // ── Row building ─────────────────────────────────────────────────────

    private void BuildRows()
    {
        if (list == null) return;
        list.Clear();
        entries.Clear();
        rowsById.Clear();

        var cm = CurrencyManager.Instance;
        if (cm == null) return;
        var inv = ItemInventoryManager.Instance;

        // ── Resources ──
        AddSection("Resources");

        int woodGold = WoodRackPopupUITK.Instance != null ? WoodRackPopupUITK.Instance.GoldPricePerWood : 0;
        string woodText = $"{cm.Wood:N0} / {cm.MaxWood:N0}";
        if (woodGold > 0 && cm.Wood > 0)
            AddSellRow(new SellEntry
            {
                Id = "wood", Name = "Wood", IconClass = "inv-icon--wood",
                Held = cm.Wood, ValueText = woodText,
                Payout = n => WoodcuttingMath.SellValue(n, woodGold),
                Sell = n => { if (cm.SpendWood(n)) cm.AddCoins(WoodcuttingMath.SellValue(n, woodGold)); }
            });
        else
            AddPlainRow("inv-icon--wood", "Wood", woodText);

        if (compostSellCoins > 0 && cm.Compost > 0)
            AddSellRow(new SellEntry
            {
                Id = "compost", Name = "Compost", IconClass = "inv-icon--compost", Held = cm.Compost,
                Payout = n => compostSellCoins * n,
                Sell = n => { if (cm.SpendCompost(n)) cm.AddCoins(compostSellCoins * n); }
            });
        else
            AddPlainRow("inv-icon--compost", "Compost", cm.Compost.ToString("N0"));

        // ── Harvest ──
        if (inv != null)
        {
            bool anyHarvest = inv.Eggs > 0;
            if (cropDatabase != null)
                foreach (CropData crop in cropDatabase.allCrops)
                    if (crop != null && inv.GetCrop(crop.cropName) > 0) { anyHarvest = true; break; }

            if (anyHarvest)
            {
                AddSection("Harvest");
                if (cropDatabase != null)
                    foreach (CropData crop in cropDatabase.allCrops)
                    {
                        if (crop == null) continue;
                        int count = inv.GetCrop(crop.cropName);
                        if (count <= 0) continue;
                        int per = inv.RawCoinValue(crop);
                        string cropName = crop.cropName;
                        AddSellRow(new SellEntry
                        {
                            Id = "crop:" + cropName, Name = cropName,
                            Sprite = crop.cropSprite, Held = count,
                            Payout = n => per * n,
                            Sell = n => { if (inv.TrySpendCrop(cropName, n)) cm.AddCoins(per * n); }
                        });
                    }

                if (inv.Eggs > 0)
                    AddSellRow(new SellEntry
                    {
                        Id = "eggs", Name = "Eggs", IconClass = "inv-icon--eggs", Held = inv.Eggs,
                        Payout = n => inv.EggSellCoins * n,
                        Sell = n => { if (inv.TrySpendEggs(n)) cm.AddCoins(inv.EggSellCoins * n); }
                    });
            }
        }

        // ── Pantry ──
        var pantry = PantryManager.Instance;
        var smoke = SmokehouseManager.Instance;
        var cannery = CanneryManager.Instance;
        int jarCount = cannery != null ? cannery.ReadyJarCount : 0;
        bool anyPantry = pantry != null && (pantry.TotalRaw > 0 || pantry.TotalSmoked > 0);

        if (anyPantry || jarCount > 0)
        {
            AddSection("Pantry");

            // Every fish type is always listed, at zero or not, so the full set is visible as a
            // checklist rather than appearing one row at a time. Raw first, then smoked, so the
            // two kinds read as blocks instead of interleaving Perch / Smoked Perch / Bass / …
            if (pantry != null)
            {
                for (int tier = 1; tier <= FishTiers.Count; tier++)
                {
                    int t = tier;                       // capture per iteration, not the loop variable
                    int raw = pantry.GetRaw(t);
                    int rawValue = smoke != null ? smoke.RawValue(t) : 0;
                    if (raw > 0 && rawValue > 0)
                        AddSellRow(new SellEntry
                        {
                            Id = "fish:raw:" + t, Name = FishTiers.Name(t),
                            IconClass = FishIconClass(t), Held = raw,
                            Payout = n => rawValue * n,
                            Sell = n => { for (int i = 0; i < n; i++) if (!smoke.TrySellRaw(t)) break; }
                        });
                    else
                        AddPlainRow(FishIconClass(t), FishTiers.Name(t), raw.ToString("N0"), raw <= 0);
                }

                for (int tier = 1; tier <= FishTiers.Count; tier++)
                {
                    int t = tier;
                    int smoked = pantry.GetSmoked(t);
                    int smokedValue = smoke != null ? smoke.SmokedValue(t) : 0;
                    if (smoked > 0 && smokedValue > 0)
                        AddSellRow(new SellEntry
                        {
                            Id = "fish:smoked:" + t, Name = FishTiers.SmokedName(t),
                            IconClass = FishIconClass(t, true), Held = smoked,
                            Payout = n => smokedValue * n,
                            Sell = n => { for (int i = 0; i < n; i++) if (!smoke.TrySellSmoked(t)) break; }
                        });
                    else
                        AddPlainRow(FishIconClass(t, true), FishTiers.SmokedName(t),
                                    smoked.ToString("N0"), smoked <= 0);
                }
            }

            if (jarCount > 0 && cannery != null)
            {
                // Snapshot values now: selling always takes index 0, so the preview and the sale
                // consume the same jars in the same order.
                var jarValues = new List<int>(jarCount);
                for (int i = 0; i < jarCount; i++) jarValues.Add(cannery.JarValueAt(i));
                AddSellRow(new SellEntry
                {
                    Id = "jars", Name = "Jars", IconClass = "inv-icon--jars", Held = jarCount,
                    Payout = n => InventoryMath.JarStackValue(jarValues, n),
                    Sell = n => { for (int i = 0; i < n; i++) if (!cannery.TrySellJar(0)) break; }
                });
            }
        }
    }

    private void AddSection(string title)
    {
        var lbl = new Label(title);
        lbl.AddToClassList("inv-section");
        list.Add(lbl);
    }

    private VisualElement MakeRow(string iconClasses, Sprite sprite, string name, string valueText)
    {
        var row = new VisualElement(); row.AddToClassList("inv-row");

        var icon = new VisualElement(); icon.AddToClassList("inv-icon");
        icon.pickingMode = PickingMode.Ignore;
        ApplyIcon(icon, sprite, iconClasses);

        var nameLbl = new Label(name); nameLbl.AddToClassList("inv-name");
        nameLbl.pickingMode = PickingMode.Ignore;

        var valLbl = new Label(valueText); valLbl.AddToClassList("inv-value");
        valLbl.pickingMode = PickingMode.Ignore;

        row.Add(icon); row.Add(nameLbl); row.Add(valLbl);
        list.Add(row);
        return row;
    }

    /// <summary><paramref name="dimmed"/> marks a type the player holds none of — the row still
    /// lists so the full set stays visible, but reads as absent rather than available.</summary>
    private void AddPlainRow(string iconClasses, string name, string valueText, bool dimmed = false)
    {
        var row = MakeRow(iconClasses, null, name, valueText);
        if (dimmed) row.AddToClassList("inv-row--empty");
    }

    private void AddSellRow(SellEntry e)
    {
        entries[e.Id] = e;
        var row = MakeRow(e.IconClass, e.Sprite, e.Name, e.ValueText ?? e.Held.ToString("N0"));
        row.AddToClassList("inv-row--sellable");
        if (e.Id == stagedId) row.AddToClassList("inv-row--staged");
        rowsById[e.Id] = row;

        string id = e.Id;
        row.RegisterCallback<ClickEvent>(evt => { Stage(id); evt.StopPropagation(); });
    }
}
