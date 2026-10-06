using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;

public enum StoreTab { Featured, Gems, Skins, Boosts }

/// <summary>
/// Tabbed Store (spec 2026-10-05 §3): Featured (Free Gift, Farmer's Pass, Golden Farm Set, Starter Bundle),
/// Gems (bundles), Skins (Animals/Buildings chips, 2-column art grid), Boosts (Harvest Blessing, coming soon).
/// A tab/chip switch rebuilds that tab; everything else updates labels in place (never on a tick).
/// Gem skin purchases need two taps within 3s ("Confirm N gems?"). Self-creating, sort 1150.
/// </summary>
public sealed class StorePopupUITK : MonoBehaviour
{
    private const int SortOrder = 1150;
    private const float ConfirmWindow = 3f;

    private static readonly string[] AnimalTargets = { "chicken", "rooster", "cow", "pig", "farm_dog" };
    private static readonly Dictionary<string, string> GroupNames = new Dictionary<string, string>
    {
        { "chicken", "Chicken" }, { "rooster", "Rooster" }, { "cow", "Cow" }, { "pig", "Pig" }, { "farm_dog", "Dog" }, { "farmhouse", "Farmhouse" },
    };

    private static readonly Color Parchment = new Color(0.98f, 0.93f, 0.82f, 0.88f);
    private static readonly Color GoldWash = new Color(0.99f, 0.88f, 0.62f, 0.95f);

    private static StorePopupUITK instance;
    private static StoreTab lastTab = StoreTab.Featured;
    private static bool skinsShowBuildings;
    public static bool IsOpen => instance != null && instance.isOpen;

    private sealed class ProductView { public string id; public Button buy; public Label owned; public VisualElement panel; public bool hideWhenOwned; }

    private sealed class SkinCardView
    {
        public string id; public SkinDef def;
        public VisualElement card; public Button action; public Label actionText; public VisualElement gemIcon;
        public Label equippedLabel; public Label ribbon;
    }

    private PanelSettings runtimeSettings;
    private VisualElement root, popupRoot;
    private ScrollView content;
    private readonly Dictionary<StoreTab, Button> tabButtons = new Dictionary<StoreTab, Button>();
    private readonly List<ProductView> products = new List<ProductView>();
    private readonly List<SkinCardView> skinCards = new List<SkinCardView>();
    private Label giftStatus, giftReward;
    private Button giftButton;
    private IVisualElementScheduledItem ticker;
    private bool isOpen, subscribed;
    private Action<int> onGems;
    private string armedSkinId;
    private float armedAt;

    // ── Open / close ─────────────────────────────────────────────

    public static void Open() => Open(lastTab);

    public static void Open(StoreTab tab)
    {
        if (instance == null) instance = new GameObject("StorePopupUITK").AddComponent<StorePopupUITK>();
        instance.DoOpen(tab);
    }

    public static void Close() { if (instance != null) instance.DoClose(); }

    private void OnDestroy()
    {
        Unsubscribe();
        if (instance == this) instance = null;
        if (runtimeSettings != null) Destroy(runtimeSettings);
    }

    private void DoOpen(StoreTab tab)
    {
        if (!EnsureBuilt()) return;
        Subscribe();
        isOpen = true;
        root.pickingMode = PickingMode.Position;
        popupRoot.style.display = DisplayStyle.Flex;
        ShowTab(tab);
        ticker?.Resume();
    }

    private void DoClose()
    {
        if (!isOpen) return;
        isOpen = false;
        armedSkinId = null;
        ticker?.Pause();
        popupRoot.style.display = DisplayStyle.None;
        root.pickingMode = PickingMode.Ignore;
    }

    private void Subscribe()
    {
        if (subscribed) return;
        if (StoreManager.Instance != null) StoreManager.Instance.OnEntitlementsChanged += RefreshAll;
        if (FreeGiftManager.Instance != null) FreeGiftManager.Instance.OnStateChanged += RefreshGift;
        if (SkinManager.Instance != null) SkinManager.Instance.OnSkinsChanged += RefreshSkinCards;
        onGems = _ => RefreshSkinCards();
        if (CurrencyManager.Instance != null) CurrencyManager.Instance.OnGemsChanged += onGems;
        subscribed = true;
    }

    private void Unsubscribe()
    {
        if (!subscribed) return;
        if (StoreManager.Instance != null) StoreManager.Instance.OnEntitlementsChanged -= RefreshAll;
        if (FreeGiftManager.Instance != null) FreeGiftManager.Instance.OnStateChanged -= RefreshGift;
        if (SkinManager.Instance != null) SkinManager.Instance.OnSkinsChanged -= RefreshSkinCards;
        if (CurrencyManager.Instance != null && onGems != null) CurrencyManager.Instance.OnGemsChanged -= onGems;
        subscribed = false;
    }

    // ── Shell (built once) ───────────────────────────────────────

    private bool EnsureBuilt()
    {
        if (root != null) return true;
        root = MonetizationUI.CreateRoot(this, SortOrder, "Store", out runtimeSettings);
        if (root == null) return false;

        popupRoot = new VisualElement { name = "store-root" };
        MonetizationUI.Fill(popupRoot);
        popupRoot.style.alignItems = Align.Center;
        popupRoot.style.justifyContent = Justify.Center;
        popupRoot.style.display = DisplayStyle.None;
        root.Add(popupRoot);

        var backdrop = new VisualElement();
        MonetizationUI.Fill(backdrop);
        backdrop.style.backgroundColor = MonetizationUI.Dim(0.75f);
        backdrop.RegisterCallback<ClickEvent>(_ => DoClose());
        popupRoot.Add(backdrop);

        VisualElement card = MonetizationUI.Card();
        card.style.width = Length.Percent(94);
        card.style.maxWidth = 900;
        card.style.height = Length.Percent(86);
        popupRoot.Add(card);

        var header = new VisualElement();
        header.style.flexDirection = FlexDirection.Row;
        header.style.alignItems = Align.Center;
        header.style.marginBottom = 10;
        Label title = MonetizationUI.PixelText("Store", 1, MonetizationUI.Ink);
        title.style.flexGrow = 1;
        header.Add(title);
        Button close = MonetizationUI.BrownButton("X", DoClose, 30);
        close.name = "store-close";
        header.Add(close);
        card.Add(header);

        var tabs = new VisualElement();
        tabs.style.flexDirection = FlexDirection.Row;
        tabs.style.marginBottom = 12;
        foreach (StoreTab t in Enum.GetValues(typeof(StoreTab)))
        {
            StoreTab captured = t;
            var b = new Button(() => ShowTab(captured)) { text = t.ToString(), name = "store-tab-" + t.ToString().ToLowerInvariant() };
            b.style.flexGrow = 1; b.style.flexBasis = 0;
            b.style.height = 60; b.style.fontSize = 24;
            b.style.unityFontStyleAndWeight = FontStyle.Bold;
            b.style.marginLeft = 3; b.style.marginRight = 3;
            MonetizationUI.NoBorder(b); MonetizationUI.Radius(b, 12);
            tabButtons[t] = b;
            tabs.Add(b);
        }
        card.Add(tabs);

        content = new ScrollView(ScrollViewMode.Vertical) { name = "store-scroll" };
        content.style.flexGrow = 1;
        content.verticalScrollerVisibility = ScrollerVisibility.Auto;
        MonetizationUI.WalnutScroller(content);
        card.Add(content);

        ticker = root.schedule.Execute(Tick).Every(500);
        ticker.Pause();
        return true;
    }

    private void Tick()
    {
        RefreshGift();
        if (armedSkinId != null && Time.unscaledTime - armedAt > ConfirmWindow) { armedSkinId = null; RefreshSkinCards(); }
    }

    private static void StylePill(Button b, bool active)
    {
        b.style.backgroundColor = active ? MonetizationUI.Walnut : Parchment;
        b.style.color = active ? MonetizationUI.Cream : MonetizationUI.Ink;
    }

    // ── Tabs ─────────────────────────────────────────────────────

    private void ShowTab(StoreTab tab)
    {
        lastTab = tab;
        armedSkinId = null;
        foreach (var kv in tabButtons) StylePill(kv.Value, kv.Key == tab);
        content.Clear();
        products.Clear();
        skinCards.Clear();
        giftStatus = null; giftReward = null; giftButton = null;

        StoreCatalogSO catalog = StoreCatalogSO.Instance;
        switch (tab)
        {
            case StoreTab.Featured:
                BuildGiftCard(content);
                Product(catalog, StoreDefaults.PassId, p => BuildPassCard(content, p));
                Product(catalog, SkinDefaults.GoldenSet, p => BuildSetSpotlight(content, p));
                Product(catalog, StoreDefaults.StarterId, p => BuildProductRow(content, p, hideWhenOwned: true));
                break;
            case StoreTab.Gems:
                foreach (StoreProductDef p in catalog.products) if (p != null && p.section == StoreSection.Gems) BuildBundleRow(content, p);
                break;
            case StoreTab.Skins:
                BuildSkinsTab(content);
                break;
            case StoreTab.Boosts:
                foreach (StoreProductDef p in catalog.products) if (p != null && p.section == StoreSection.Boosts) BuildProductRow(content, p, hideWhenOwned: false);
                SectionTitle(content, "Coming soon");
                foreach (StoreProductDef p in catalog.products) if (p != null && p.section == StoreSection.ComingSoon) BuildSoonRow(content, p);
                break;
        }
        BuildFooter(content);
        content.scrollOffset = Vector2.zero;
        RefreshAll();
    }

    private static void Product(StoreCatalogSO catalog, string id, Action<StoreProductDef> build)
    {
        StoreProductDef p = catalog.Get(id);
        if (p != null && !p.comingSoon) build(p);
    }

    // ── Shared builders ──────────────────────────────────────────

    private static VisualElement Panel(Color? bg = null)
    {
        var e = new VisualElement();
        e.style.backgroundColor = bg ?? Parchment;
        MonetizationUI.Radius(e, 14);
        e.style.paddingLeft = 20; e.style.paddingRight = 20; e.style.paddingTop = 16; e.style.paddingBottom = 16;
        e.style.marginBottom = 14;
        e.style.flexDirection = FlexDirection.Row;
        e.style.alignItems = Align.Center;
        return e;
    }

    private static void SectionTitle(VisualElement parent, string text)
    {
        Label l = MonetizationUI.PixelText(text, 1, MonetizationUI.Ink);
        l.style.marginTop = 8; l.style.marginBottom = 8;
        parent.Add(l);
    }

    private static VisualElement Column()
    {
        var col = new VisualElement();
        col.style.flexGrow = 1; col.style.flexShrink = 1; col.style.marginLeft = 16;
        return col;
    }

    private Button PriceButton(string productId, int fontSize = 28)
    {
        Button b = MonetizationUI.BrownButton("", () => StoreManager.Instance?.Purchase(productId), fontSize);
        b.name = "store-buy-" + productId;
        b.style.minWidth = 170;
        return b;
    }

    private static Label OwnedLabel()
    {
        Label l = MonetizationUI.Text("Owned", 28, MonetizationUI.Walnut, bold: true);
        l.style.unityTextAlign = TextAnchor.MiddleCenter;
        l.style.display = DisplayStyle.None;
        return l;
    }

    private void Track(string id, Button buy, Label owned, VisualElement panel, bool hideWhenOwned) =>
        products.Add(new ProductView { id = id, buy = buy, owned = owned, panel = panel, hideWhenOwned = hideWhenOwned });

    // ── Featured ─────────────────────────────────────────────────

    private void BuildGiftCard(VisualElement parent)
    {
        VisualElement panel = Panel();
        panel.Add(MonetizationUI.Icon(MonetizationUI.ChestIcon, 110));
        VisualElement col = Column();
        col.Add(MonetizationUI.Text("Free Gift", 34, MonetizationUI.Ink, bold: true));
        giftReward = MonetizationUI.Text("", 26, MonetizationUI.Ink);
        col.Add(giftReward);
        giftStatus = MonetizationUI.Text("", 26, MonetizationUI.Muted);
        col.Add(giftStatus);
        panel.Add(col);
        giftButton = MonetizationUI.BrownButton("Open", () => FreeGiftManager.Instance?.RequestClaim(), 28);
        giftButton.name = "store-gift-button";
        giftButton.style.minWidth = 170;
        panel.Add(giftButton);
        parent.Add(panel);
    }

    private void BuildPassCard(VisualElement parent, StoreProductDef p)
    {
        VisualElement panel = Panel(GoldWash);
        panel.style.flexDirection = FlexDirection.Column;
        panel.style.alignItems = Align.Stretch;
        var top = new VisualElement();
        top.style.flexDirection = FlexDirection.Row;
        top.style.alignItems = Align.Center;
        top.style.marginBottom = 14;
        top.Add(MonetizationUI.PassIcon(140, p.gems));   // no-ads art + "+500" gem badge
        VisualElement col = Column();
        col.style.marginLeft = 26;
        col.Add(MonetizationUI.PixelText(p.displayName, 1, MonetizationUI.Ink));
        Label desc = MonetizationUI.Text(p.description, 30, MonetizationUI.Walnut, bold: true);
        desc.style.marginTop = 6;
        col.Add(desc);
        top.Add(col);
        panel.Add(top);
        Button buy = PriceButton(p.id, 32);
        Label owned = OwnedLabel();
        panel.Add(buy); panel.Add(owned);
        Track(p.id, buy, owned, panel, false);
        parent.Add(panel);
    }

    private void BuildSetSpotlight(VisualElement parent, StoreProductDef p)
    {
        VisualElement panel = Panel(GoldWash);
        panel.style.flexDirection = FlexDirection.Column;
        panel.style.alignItems = Align.Stretch;
        panel.Add(MonetizationUI.PixelText(p.displayName, 1, MonetizationUI.Ink));
        var row = new VisualElement();
        row.style.flexDirection = FlexDirection.Row;
        row.style.justifyContent = Justify.SpaceAround;
        row.style.marginTop = 10; row.style.marginBottom = 10;
        foreach (string id in p.grantsSkinIds ?? new string[0]) row.Add(MonetizationUI.Icon(SkinArt.Preview(SkinCatalogSO.Instance.Get(id)), 130));
        panel.Add(row);
        Label desc = MonetizationUI.Text(p.description, 26, MonetizationUI.Ink);
        desc.style.marginBottom = 12;
        panel.Add(desc);
        Button buy = PriceButton(p.id, 32);
        Label owned = OwnedLabel();
        panel.Add(buy); panel.Add(owned);
        Track(p.id, buy, owned, panel, false);
        parent.Add(panel);
    }

    /// <summary>Sets, Starter Bundle and boosts: icon (or the first granted skin), name + description, price.</summary>
    private void BuildProductRow(VisualElement parent, StoreProductDef p, bool hideWhenOwned)
    {
        VisualElement panel = Panel();
        Sprite icon = p.icon;
        if (icon == null && p.grantsSkinIds != null && p.grantsSkinIds.Length > 0) icon = SkinArt.Preview(SkinCatalogSO.Instance.Get(p.grantsSkinIds[0]));
        panel.Add(MonetizationUI.Icon(icon, 96));
        VisualElement col = Column();
        col.Add(MonetizationUI.Text(p.displayName, 30, MonetizationUI.Ink, bold: true));
        col.Add(MonetizationUI.Text(p.description, 24, MonetizationUI.Muted));
        panel.Add(col);
        Button buy = PriceButton(p.id);
        Label owned = OwnedLabel();
        panel.Add(buy); panel.Add(owned);
        Track(p.id, buy, owned, panel, hideWhenOwned);
        parent.Add(panel);
    }

    // ── Gems / Boosts ────────────────────────────────────────────

    private void BuildBundleRow(VisualElement parent, StoreProductDef p)
    {
        VisualElement panel = Panel();
        panel.Add(MonetizationUI.Icon(p.icon, 84));
        VisualElement col = Column();
        col.Add(MonetizationUI.Text(p.displayName, 30, MonetizationUI.Ink, bold: true));
        col.Add(MonetizationUI.Text($"{p.gems:N0} gems", 26, MonetizationUI.Ink));
        panel.Add(col);
        if (!string.IsNullOrEmpty(p.ribbon)) panel.Add(Ribbon(p.ribbon, absolute: false));
        Button buy = PriceButton(p.id);
        panel.Add(buy);
        Track(p.id, buy, null, panel, false);
        parent.Add(panel);
    }

    private static Label Ribbon(string text, bool absolute)
    {
        Label ribbon = MonetizationUI.Text(text, 22, MonetizationUI.Cream, bold: true);
        ribbon.style.backgroundColor = MonetizationUI.Walnut;
        MonetizationUI.Radius(ribbon, 8);
        ribbon.style.paddingLeft = 10; ribbon.style.paddingRight = 10; ribbon.style.paddingTop = 4; ribbon.style.paddingBottom = 4;
        ribbon.style.marginRight = 12;
        if (absolute) { ribbon.style.position = Position.Absolute; ribbon.style.top = 8; ribbon.style.right = 0; }
        return ribbon;
    }

    private static void BuildSoonRow(VisualElement parent, StoreProductDef p)
    {
        VisualElement panel = Panel();
        panel.style.opacity = 0.6f;
        var col = new VisualElement();
        col.style.flexGrow = 1; col.style.flexShrink = 1;
        col.Add(MonetizationUI.Text(p.displayName, 30, MonetizationUI.Ink, bold: true));
        col.Add(MonetizationUI.Text(p.description, 24, MonetizationUI.Muted));
        panel.Add(col);
        panel.Add(MonetizationUI.Text("Soon", 26, MonetizationUI.Muted, bold: true));
        parent.Add(panel);
    }

    private static void BuildFooter(VisualElement parent)
    {
        var footer = new VisualElement();
        footer.style.alignItems = Align.Center;
        footer.style.marginTop = 10; footer.style.marginBottom = 20;
        Button restore = new Button(() => StoreManager.Instance?.Restore()) { text = "Restore Purchases", name = "store-restore" };
        restore.style.backgroundColor = Color.clear;
        MonetizationUI.NoBorder(restore);
        restore.style.color = MonetizationUI.Walnut;
        restore.style.fontSize = 26;
        restore.style.unityFontStyleAndWeight = FontStyle.Bold;
        footer.Add(restore);
        string fine = MonetizationServices.IsTestStore ? "Test store. No real charges." : "Prices in your local currency.";
        footer.Add(MonetizationUI.Text(fine, 22, MonetizationUI.Muted));
        parent.Add(footer);
    }

    // ── Skins ────────────────────────────────────────────────────

    private void BuildSkinsTab(VisualElement parent)
    {
        var chips = new VisualElement();
        chips.style.flexDirection = FlexDirection.Row;
        chips.style.marginBottom = 10;
        Button animals = new Button(() => { skinsShowBuildings = false; ShowTab(StoreTab.Skins); }) { text = "Animals", name = "store-chip-animals" };
        Button buildings = new Button(() => { skinsShowBuildings = true; ShowTab(StoreTab.Skins); }) { text = "Buildings", name = "store-chip-buildings" };
        foreach (Button b in new[] { animals, buildings })
        {
            b.style.height = 48; b.style.fontSize = 22; b.style.unityFontStyleAndWeight = FontStyle.Bold;
            b.style.paddingLeft = 22; b.style.paddingRight = 22; b.style.marginRight = 8;
            MonetizationUI.NoBorder(b); MonetizationUI.Radius(b, 24);
            chips.Add(b);
        }
        StylePill(animals, !skinsShowBuildings);
        StylePill(buildings, skinsShowBuildings);
        parent.Add(chips);

        StoreCatalogSO catalog = StoreCatalogSO.Instance;
        if (!skinsShowBuildings)
        {
            foreach (string target in AnimalTargets) BuildSkinGroup(parent, target);
            SectionTitle(parent, "Sets");
            Product(catalog, SkinDefaults.GoldenSet, p => BuildProductRow(parent, p, hideWhenOwned: false));
            Product(catalog, SkinDefaults.PuppySet, p => BuildProductRow(parent, p, hideWhenOwned: false));
        }
        else
        {
            BuildSkinGroup(parent, "farmhouse");
            SectionTitle(parent, "Sets");
            Product(catalog, SkinDefaults.GoldenSet, p => BuildProductRow(parent, p, hideWhenOwned: false));
        }
    }

    private void BuildSkinGroup(VisualElement parent, string target)
    {
        SectionTitle(parent, GroupNames.TryGetValue(target, out string n) ? n : target);
        var grid = new VisualElement();
        grid.style.flexDirection = FlexDirection.Row;
        grid.style.flexWrap = Wrap.Wrap;
        grid.style.justifyContent = Justify.SpaceBetween;
        grid.Add(BuildSkinCard(SkinDefaults.ClassicId(target), null, "Classic", SkinArt.ClassicPreview(target)));
        IEnumerable<SkinDef> skins = (SkinCatalogSO.Instance.skins ?? new SkinDef[0])
            .Where(s => s != null && s.target == target).OrderBy(s => s.sortOrder);
        foreach (SkinDef s in skins) grid.Add(BuildSkinCard(s.id, s, s.displayName, SkinArt.Preview(s)));
        parent.Add(grid);
    }

    private VisualElement BuildSkinCard(string id, SkinDef def, string name, Sprite preview)
    {
        var view = new SkinCardView { id = id, def = def };
        var card = new VisualElement { name = "skin-card-" + id };
        card.style.width = Length.Percent(48.5f);
        card.style.marginBottom = 12;
        card.style.backgroundColor = Parchment;
        MonetizationUI.Radius(card, 14);
        card.style.borderTopWidth = 4; card.style.borderBottomWidth = 4; card.style.borderLeftWidth = 4; card.style.borderRightWidth = 4;
        card.style.paddingTop = 12; card.style.paddingBottom = 12; card.style.paddingLeft = 10; card.style.paddingRight = 10;
        card.style.alignItems = Align.Center;

        VisualElement art = MonetizationUI.Icon(preview, 124);
        card.Add(art);
        Label title = MonetizationUI.Text(name, 24, MonetizationUI.Ink, bold: true);
        title.style.unityTextAlign = TextAnchor.MiddleCenter;
        title.style.marginTop = 6; title.style.marginBottom = 8;
        card.Add(title);

        view.ribbon = Ribbon("Set", absolute: true);
        card.Add(view.ribbon);

        view.equippedLabel = MonetizationUI.Text("Equipped", 24, MonetizationUI.Walnut, bold: true);
        view.equippedLabel.style.height = 52;
        view.equippedLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
        card.Add(view.equippedLabel);

        view.action = MonetizationUI.BrownButton("", () => OnSkinAction(view), 24);
        view.action.name = "skin-" + id;
        view.action.style.width = Length.Percent(92);
        view.action.style.flexDirection = FlexDirection.Row;
        view.action.style.justifyContent = Justify.Center;
        view.action.style.alignItems = Align.Center;
        view.actionText = MonetizationUI.Text("", 24, MonetizationUI.Cream, bold: true);
        view.action.Add(view.actionText);
        view.gemIcon = MonetizationUI.Icon(MonetizationArt.Instance != null ? MonetizationArt.Instance.gemIcon : null, 28);
        view.gemIcon.style.marginLeft = 6;
        view.action.Add(view.gemIcon);
        card.Add(view.action);

        view.card = card;
        skinCards.Add(view);
        return card;
    }

    private void OnSkinAction(SkinCardView v)
    {
        SkinManager sm = SkinManager.Instance;
        if (sm == null || sm.IsEquipped(v.id)) return;
        if (sm.IsOwned(v.id)) { sm.Equip(v.id); return; }
        if (v.def != null && v.def.IsSetOnly) { StoreManager.Instance?.Purchase(v.def.setId); return; }

        if (armedSkinId != v.id || Time.unscaledTime - armedAt > ConfirmWindow)
        {
            armedSkinId = v.id;           // first tap arms; a stray tap never spends 1,000+ gems
            armedAt = Time.unscaledTime;
            RefreshSkinCards();
            return;
        }
        armedSkinId = null;
        SkinBuyResult r = sm.TryBuy(v.id);
        if (r == SkinBuyResult.NotEnoughGems) Toast("Not enough gems.");
        else if (r == SkinBuyResult.Ok) Toast(v.def.displayName + " equipped!");
        RefreshSkinCards();
    }

    private static void Toast(string message) =>
        ToastManager.Show(message, null, ToastManager.ToastKind.Success, MonetizationUI.ChestIcon);

    // ── Refresh (labels only) ────────────────────────────────────

    private void RefreshAll()
    {
        if (root == null) return;
        StoreManager sm = StoreManager.Instance;
        bool busy = sm != null && sm.Busy;
        foreach (ProductView p in products)
        {
            bool owned = sm != null && sm.IsProductOwned(p.id);
            if (p.hideWhenOwned && p.panel != null) p.panel.style.display = owned ? DisplayStyle.None : DisplayStyle.Flex;
            p.buy.style.display = owned ? DisplayStyle.None : DisplayStyle.Flex;
            if (p.owned != null) p.owned.style.display = owned ? DisplayStyle.Flex : DisplayStyle.None;
            p.buy.text = sm != null ? sm.PriceFor(p.id) : "";
            p.buy.SetEnabled(!busy);
        }
        RefreshGift();
        RefreshSkinCards();
    }

    private void RefreshSkinCards()
    {
        SkinManager sk = SkinManager.Instance;
        StoreManager sm = StoreManager.Instance;
        int gems = CurrencyManager.Instance != null ? CurrencyManager.Instance.Gems : 0;
        foreach (SkinCardView v in skinCards)
        {
            bool owned = sk != null && sk.IsOwned(v.id);
            bool equipped = sk != null && sk.IsEquipped(v.id);
            bool setOnly = !owned && v.def != null && v.def.IsSetOnly;
            Color edge = equipped ? MonetizationUI.Gold : Color.clear;
            v.card.style.borderTopColor = edge; v.card.style.borderBottomColor = edge;
            v.card.style.borderLeftColor = edge; v.card.style.borderRightColor = edge;
            v.equippedLabel.style.display = equipped ? DisplayStyle.Flex : DisplayStyle.None;
            v.action.style.display = equipped ? DisplayStyle.None : DisplayStyle.Flex;
            v.ribbon.style.display = setOnly ? DisplayStyle.Flex : DisplayStyle.None;
            if (equipped) continue;

            bool showGem = false;
            bool enabled = true;
            if (owned) v.actionText.text = "Equip";
            else if (setOnly) { v.actionText.text = sm != null ? sm.PriceFor(v.def.setId) : ""; enabled = sm == null || !sm.Busy; }
            else if (armedSkinId == v.id) v.actionText.text = StoreCopy.ConfirmGems(v.def != null ? v.def.gemPrice : 0);
            else
            {
                int price = v.def != null ? v.def.gemPrice : 0;
                v.actionText.text = price.ToString("N0");
                showGem = true;
                enabled = gems >= price;
            }
            v.gemIcon.style.display = showGem ? DisplayStyle.Flex : DisplayStyle.None;
            v.action.SetEnabled(enabled);
        }
    }

    private void RefreshGift()
    {
        if (giftStatus == null) return;
        FreeGiftManager m = FreeGiftManager.Instance;
        GiftStatus s = m != null ? m.Status : GiftStatus.Locked;
        if (m != null) giftReward.text = $"{m.GemsPerClaim} gems + {m.CoinReward:N0} coins";
        switch (s)
        {
            case GiftStatus.Locked:
                giftStatus.text = "Help the town to unlock it.";
                giftButton.SetEnabled(false); giftButton.text = "Locked"; break;
            case GiftStatus.Ready:
                giftStatus.text = "Ready!";
                giftButton.SetEnabled(true); giftButton.text = m.NextClaimNeedsAd ? "Watch" : "Open"; break;
            case GiftStatus.Cooldown:
                giftStatus.text = "Next gift in " + MonetizationUI.Mmss(m.SecondsUntilReady);
                giftButton.SetEnabled(false); giftButton.text = "Wait"; break;
            case GiftStatus.Capped:
                giftStatus.text = "That's all for today.";
                giftButton.SetEnabled(false); giftButton.text = "Tomorrow"; break;
        }
    }
}
