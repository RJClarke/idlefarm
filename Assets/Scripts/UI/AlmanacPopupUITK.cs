using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// The Farmer's Almanac (spec docs/superpowers/specs/2026-09-28-farmers-almanac-design.md): a book of
/// every Crop, piece of Equipment, Animal and Pest. List view (tabs + icon grid, "???" for locked)
/// and entry view (tags, "why pick this" blurb, stats as total + where the bonus came from, pest
/// appetite bars, notes). Pages are rebuilt from live data on every open, so upgrades show at once.
///
/// Fully code-built and self-bootstrapping — no scene object needed. It borrows the Barn board's
/// art and panel settings (BarnPopupUITK) so the two boards match, and sorts above the other
/// popups so it can open on top of the seed picker from a packet's "i".
/// </summary>
public class AlmanacPopupUITK : MonoBehaviour
{
    public static AlmanacPopupUITK Instance { get; private set; }

    private const int SortOrder = 1100;
    private static readonly Color TitleBrown = new Color(0.373f, 0.275f, 0.149f);
    private static readonly Color Ink = new Color(0.12f, 0.08f, 0.05f);
    private static readonly Color Muted = new Color(0.36f, 0.28f, 0.18f);
    private static readonly Color ChipFill = new Color(0.55f, 0.40f, 0.22f, 0.9f);
    private static readonly Color ChipText = new Color(1f, 0.95f, 0.85f);
    private static readonly Color TabOn = new Color(0.45f, 0.30f, 0.14f);
    private static readonly Color TabOff = new Color(0.45f, 0.30f, 0.14f, 0.35f);
    private static readonly Color TabTextOn = new Color(0.29f, 0.17f, 0.08f);   // dark ink on the cream inset
    private static readonly Color TabTextOff = new Color(1f, 0.93f, 0.80f);     // cream on the dark inset
    private const float PixelScale = 4f; // one notebook-sprite pixel = 4 UI px
    private static readonly Color TileFill = new Color(1f, 1f, 1f, 0.35f);
    private static readonly Color BarBack = new Color(0f, 0f, 0f, 0.18f);
    private static readonly Color BarLow = new Color(0.36f, 0.62f, 0.30f);
    private static readonly Color BarHigh = new Color(0.78f, 0.30f, 0.22f);

    private static readonly (AlmanacKind kind, string label)[] Tabs =
    {
        (AlmanacKind.Crop, "Crops"), (AlmanacKind.Equipment, "Equipment"),
        (AlmanacKind.Animal, "Animals"), (AlmanacKind.Pest, "Pests"),
    };

    private UIDocument document;
    private PanelSettings runtimePanelSettings;
    private VisualElement root, popupRoot, listView, entryView, entryPage, grid, tabRow;
    private AlmanacArt art;
    private FloatingTooltip tooltip;
    private ScrollView gridScroll, entryScroll;
    private Label hintLabel;
    private Button backButton;
    private AlmanacKind currentTab = AlmanacKind.Crop;
    private bool built, isOpen;
    public bool IsOpen => isOpen;

    // Self-bootstrap per scene: AfterSceneLoad only fires for the first scene (Splash), whose copy
    // dies on the switch to FarmMain — so also create one on every later scene load.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        UnityEngine.SceneManagement.SceneManager.sceneLoaded -= OnSceneLoaded;
        UnityEngine.SceneManagement.SceneManager.sceneLoaded += OnSceneLoaded;
        EnsureInstance();
    }

    private static void OnSceneLoaded(UnityEngine.SceneManagement.Scene scene, UnityEngine.SceneManagement.LoadSceneMode mode) => EnsureInstance();

    private static void EnsureInstance()
    {
        if (Instance != null || FindFirstObjectByType<AlmanacPopupUITK>() != null) return;
        new GameObject("AlmanacPopupUITK").AddComponent<AlmanacPopupUITK>();
    }

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
        if (runtimePanelSettings != null) Destroy(runtimePanelSettings);
    }

    // ── Public API ───────────────────────────────────────────────

    public void Open() => Open(currentTab);

    public void Open(AlmanacKind tab)
    {
        if (!EnsureBuilt()) return;
        isOpen = true;
        root.pickingMode = PickingMode.Position;
        popupRoot.style.display = DisplayStyle.Flex;
        ShowList(tab);
        OnboardingTutorials.OnMenuOpened("tip_almanac"); // one-time how-to (new players)
    }

    /// <summary>Open straight to one page (seed-packet / equipment / animal "i" badges).</summary>
    public void OpenEntry(AlmanacKind kind, string id)
    {
        Open(kind);
        AlmanacEntry entry = AlmanacCatalog.Find(kind, id);
        if (entry != null && entry.unlocked) ShowEntry(entry); // a masked crop never opens its page
    }

    public void Close()
    {
        if (!isOpen) return;
        isOpen = false;
        tooltip?.Hide();
        popupRoot.style.display = DisplayStyle.None;
        root.pickingMode = PickingMode.Ignore;
    }

    // ── Build ────────────────────────────────────────────────────

    private bool EnsureBuilt()
    {
        if (built) return true;
        BarnPopupUITK barn = BarnPopupUITK.Instance;
        PanelSettings source = barn != null ? barn.SourcePanelSettings : null;
        if (source == null) { Debug.LogWarning("[Almanac] No panel settings to clone (BarnPopupUITK missing)."); return false; }

        runtimePanelSettings = Instantiate(source);
        runtimePanelSettings.name = "AlmanacPanelSettings (runtime)";
        runtimePanelSettings.sortingOrder = SortOrder;
        document = gameObject.AddComponent<UIDocument>();
        document.enabled = false;
        document.panelSettings = runtimePanelSettings;
        document.enabled = true;

        root = document.rootVisualElement;
        root.pickingMode = PickingMode.Ignore;
        root.style.height = Length.Percent(100);
        BuildShell(barn);
        built = true;
        return true;
    }

    private void BuildShell(BarnPopupUITK barn)
    {
        art = AlmanacArt.Instance;
        var titleFont = art != null && art.titleFont != null ? art.titleFont : barn.TitleFont;

        popupRoot = new VisualElement { name = "almanac-root" };
        Fill(popupRoot);
        popupRoot.style.alignItems = Align.Center;
        popupRoot.style.justifyContent = Justify.Center;
        popupRoot.style.display = DisplayStyle.None;
        root.Add(popupRoot);

        var backdrop = new VisualElement();
        Fill(backdrop);
        backdrop.style.backgroundColor = new Color(0f, 0f, 0f, 0.8f);
        backdrop.RegisterCallback<ClickEvent>(_ => Close());
        popupRoot.Add(backdrop);

        var card = new VisualElement { name = "almanac-card" };
        card.style.width = Length.Percent(94);
        card.style.maxWidth = 860;
        card.style.height = Length.Percent(86);
        int border = Mathf.RoundToInt(barn.FrameSlice * barn.FrameSliceScale);
        card.style.paddingLeft = border + 24; card.style.paddingRight = border + 24;
        card.style.paddingTop = border + 22; card.style.paddingBottom = border + 22;
        if (barn.BoardFrame != null)
        {
            card.style.backgroundImage = new StyleBackground(barn.BoardFrame);
            card.style.unitySliceLeft = barn.FrameSlice; card.style.unitySliceRight = barn.FrameSlice;
            card.style.unitySliceTop = barn.FrameSlice; card.style.unitySliceBottom = barn.FrameSlice;
            card.style.unitySliceScale = barn.FrameSliceScale;
        }
        else card.style.backgroundColor = new Color(0.70f, 0.60f, 0.43f);
        popupRoot.Add(card);

        if (barn.InteriorWash.a > 0f)
        {
            var wash = new VisualElement { pickingMode = PickingMode.Ignore };
            wash.style.position = Position.Absolute;
            wash.style.left = border; wash.style.right = border; wash.style.top = border; wash.style.bottom = border;
            wash.style.backgroundColor = barn.InteriorWash;
            Radius(wash, 6);
            card.Add(wash);
        }

        // Header: back (entry view only), title, close.
        var header = Row();
        header.style.marginBottom = 14;
        card.Add(header);

        backButton = new Button(() => ShowList(currentTab)) { text = "< Back" };
        backButton.style.fontSize = 26;
        backButton.style.unityFontStyleAndWeight = FontStyle.Bold;
        backButton.style.color = ChipText;
        backButton.style.backgroundColor = TabOn;
        NoBorder(backButton); Radius(backButton, 12);
        backButton.style.height = 54; backButton.style.paddingLeft = 18; backButton.style.paddingRight = 18;
        backButton.style.marginRight = 14;
        header.Add(backButton);

        var title = new Label("Farmer's Almanac");
        title.style.flexGrow = 1; title.style.flexShrink = 1;
        title.style.fontSize = 46;
        title.style.color = TitleBrown;
        // Pixel font drawn as-is: faux bold smears a pixel face into mush.
        title.style.unityFontStyleAndWeight = FontStyle.Normal;
        title.style.whiteSpace = WhiteSpace.NoWrap; // pixel fonts measure narrower than they draw
        if (titleFont != null) title.style.unityFontDefinition = new StyleFontDefinition(titleFont);
        header.Add(title);

        var close = new Button(Close) { text = barn.CloseIcon != null ? "" : "X" };
        close.style.width = 58; close.style.height = 58; close.style.flexShrink = 0;
        close.style.fontSize = 34; close.style.color = TitleBrown;
        close.style.backgroundColor = Color.clear;
        NoBorder(close);
        if (barn.CloseIcon != null)
        {
            close.style.backgroundImage = new StyleBackground(barn.CloseIcon);
            close.style.backgroundSize = new StyleBackgroundSize(new BackgroundSize(BackgroundSizeType.Contain));
        }
        header.Add(close);

        // List view: tabs + grid + hint line for locked pages.
        listView = new VisualElement();
        listView.style.flexGrow = 1;
        card.Add(listView);

        tabRow = Row();
        tabRow.style.marginTop = 32; // room to breathe between the title and the tabs
        listView.Add(tabRow);
        foreach (var (kind, label) in Tabs)
        {
            var tab = new Button(() => ShowList(kind)) { text = label, userData = kind };
            tab.style.flexGrow = 1; tab.style.flexBasis = 0;
            tab.style.height = 64; tab.style.fontSize = 24;
            tab.style.paddingBottom = 8; // the sprite's inset sits above centre (thick bottom rim)
            tab.style.marginLeft = 4; tab.style.marginRight = 4;
            tab.style.whiteSpace = WhiteSpace.NoWrap;
            if (titleFont != null) tab.style.unityFontDefinition = new StyleFontDefinition(titleFont);
            NoBorder(tab);
            if (art != null && art.tabOff != null) Slice(tab, art.tabSlice);
            else { Radius(tab, 12); tab.style.unityFontStyleAndWeight = FontStyle.Bold; }
            tabRow.Add(tab);
        }

        gridScroll = new ScrollView(ScrollViewMode.Vertical);
        gridScroll.style.flexGrow = 1;
        StyleScrollbar(gridScroll);
        gridScroll.style.marginTop = 32;
        listView.Add(gridScroll);
        grid = new VisualElement();
        grid.style.flexDirection = FlexDirection.Row;
        grid.style.flexWrap = Wrap.Wrap;
        grid.style.justifyContent = Justify.SpaceBetween;
        gridScroll.Add(grid);

        hintLabel = new Label();
        hintLabel.style.fontSize = 24;
        hintLabel.style.color = Muted;
        hintLabel.style.whiteSpace = WhiteSpace.Normal;
        hintLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
        hintLabel.style.marginTop = 10;
        hintLabel.style.minHeight = 34;
        listView.Add(hintLabel);

        // Entry view: one sheet of notebook paper holding the whole page, so it reads as a page
        // of the book rather than text on the board.
        entryPage = new VisualElement { name = "almanac-page" };
        entryPage.style.flexGrow = 1;
        entryPage.style.display = DisplayStyle.None;
        if (art != null && art.page != null)
        {
            entryPage.style.backgroundImage = new StyleBackground(art.page);
            Slice(entryPage, art.pageSlice);
        }
        else { entryPage.style.backgroundColor = new Color(0.99f, 0.96f, 0.89f); Radius(entryPage, 8); }
        // Only the paper's outline as padding, so the scrollbar runs the full height flush to its
        // right edge; the page's own margins live on the scrolled content instead.
        entryPage.style.paddingTop = 4; entryPage.style.paddingBottom = 4;
        entryPage.style.paddingLeft = 4; entryPage.style.paddingRight = 4;
        card.Add(entryPage);

        entryScroll = new ScrollView(ScrollViewMode.Vertical);
        entryScroll.style.flexGrow = 1;
        StyleScrollbar(entryScroll);
        entryPage.Add(entryScroll);
        entryView = entryScroll.contentContainer;
        entryView.style.paddingTop = 30; entryView.style.paddingBottom = 14;
        entryView.style.paddingLeft = 24; entryView.style.paddingRight = 18;

        // Tap a pill to learn what it means: the same help bubble as the Barn's milestones.
        tooltip = new FloatingTooltip(popupRoot, "almanac-tooltip");
        entryScroll.verticalScroller.valueChanged += _ => tooltip.Hide(); // don't leave it floating over moved content
    }

    // ── List ─────────────────────────────────────────────────────

    private void ShowList(AlmanacKind tab)
    {
        currentTab = tab;
        tooltip?.Hide();
        backButton.style.display = DisplayStyle.None;
        listView.style.display = DisplayStyle.Flex;
        entryPage.style.display = DisplayStyle.None;
        hintLabel.text = "Tap a page to read it.";

        foreach (var child in tabRow.Children())
        {
            bool on = (AlmanacKind)child.userData == tab;
            if (art != null && art.tabOff != null && art.tabOn != null)
            {
                child.style.backgroundImage = new StyleBackground(on ? art.tabOn : art.tabOff);
                child.style.backgroundColor = Color.clear;
                child.style.color = on ? TabTextOn : TabTextOff;
            }
            else
            {
                child.style.backgroundColor = on ? TabOn : TabOff;
                child.style.color = ChipText;
            }
        }

        grid.Clear();
        List<AlmanacEntry> entries = AlmanacCatalog.Build(tab);
        foreach (AlmanacEntry e in entries) grid.Add(Tile(e));
        // Pad the last row so SpaceBetween doesn't stretch a lone tile to the far edge.
        for (int i = entries.Count % 3 == 0 ? 3 : entries.Count % 3; i < 3; i++) grid.Add(TileSpacer());
        if (entries.Count == 0) hintLabel.text = "Nothing here yet.";
        gridScroll.scrollOffset = Vector2.zero;
    }

    private VisualElement Tile(AlmanacEntry e)
    {
        var tile = new VisualElement();
        TileBox(tile);
        tile.style.backgroundColor = TileFill;
        tile.style.paddingTop = 12; tile.style.paddingBottom = 10;
        tile.style.alignItems = Align.Center;
        Radius(tile, 14);

        var icon = new VisualElement();
        icon.style.width = 110; icon.style.height = 110;
        if (e.icon != null) icon.style.backgroundImage = new StyleBackground(e.icon);
        icon.style.backgroundSize = new StyleBackgroundSize(new BackgroundSize(BackgroundSizeType.Contain));
        if (!e.unlocked) icon.style.unityBackgroundImageTintColor = new Color(0f, 0f, 0f, 0.75f); // silhouette
        icon.pickingMode = PickingMode.Ignore;
        tile.Add(icon);

        var name = new Label(e.unlocked ? e.name : "???");
        name.style.fontSize = 24;
        name.style.unityFontStyleAndWeight = FontStyle.Bold;
        name.style.color = Ink;
        name.style.marginTop = 6;
        name.style.unityTextAlign = TextAnchor.MiddleCenter;
        // Two-word names may wrap between words; a single word ("Blueberry") never wraps — UITK
        // measures it narrower than it draws and would break it mid-word ("Blueberr / y").
        string shown = e.unlocked ? e.name : "???";
        name.style.whiteSpace = shown != null && shown.Contains(" ") ? WhiteSpace.Normal : WhiteSpace.NoWrap;
        name.pickingMode = PickingMode.Ignore;
        tile.Add(name);

        tile.RegisterCallback<ClickEvent>(_ =>
        {
            if (e.unlocked) ShowEntry(AlmanacCatalog.Find(e.kind, e.id) ?? e);
            else hintLabel.text = e.unlockHint ?? "Not discovered yet.";
        });
        return tile;
    }

    private static VisualElement TileSpacer()
    {
        var s = new VisualElement { pickingMode = PickingMode.Ignore };
        TileBox(s);
        return s;
    }

    private static void TileBox(VisualElement v)
    {
        v.style.width = Length.Percent(31.5f);
        v.style.marginBottom = 14;
    }

    // ── Entry ────────────────────────────────────────────────────

    private void ShowEntry(AlmanacEntry e)
    {
        backButton.style.display = DisplayStyle.Flex;
        listView.style.display = DisplayStyle.None;
        entryPage.style.display = DisplayStyle.Flex;
        entryView.Clear();
        tooltip?.Hide();
        entryScroll.scrollOffset = Vector2.zero;

        // Icon + name, tags underneath.
        var top = Row();
        top.style.alignItems = Align.FlexStart;
        top.style.marginBottom = 10;
        entryView.Add(top);

        var icon = new VisualElement();
        icon.style.width = 130; icon.style.height = 130; icon.style.flexShrink = 0;
        icon.style.marginRight = 18;
        if (e.icon != null) icon.style.backgroundImage = new StyleBackground(e.icon);
        else icon.style.display = DisplayStyle.None; // no art (pests): let the name sit flush left
        icon.style.backgroundSize = new StyleBackgroundSize(new BackgroundSize(BackgroundSizeType.Contain));
        top.Add(icon);

        var titleCol = new VisualElement();
        titleCol.style.flexGrow = 1; titleCol.style.flexShrink = 1;
        top.Add(titleCol);

        var name = new Label(e.name);
        name.style.fontSize = 40;
        name.style.unityFontStyleAndWeight = FontStyle.Bold;
        name.style.color = TitleBrown;
        name.style.whiteSpace = WhiteSpace.Normal;
        titleCol.Add(name);

        if (e.pills.Count > 0)
        {
            var chips = Row();
            chips.style.flexWrap = Wrap.Wrap;
            chips.style.marginTop = 6;
            titleCol.Add(chips);
            foreach (Pill pill in e.pills) chips.Add(PillChip(pill));
        }

        if (!string.IsNullOrWhiteSpace(e.blurb))
        {
            var blurb = new Label(e.blurb);
            blurb.style.fontSize = 27;
            blurb.style.color = Ink;
            blurb.style.whiteSpace = WhiteSpace.Normal;
            blurb.style.unityFontStyleAndWeight = FontStyle.Italic;
            blurb.style.marginBottom = e.facts.Count > 0 ? 8 : 18;
            entryView.Add(blurb);
        }

        if (e.facts.Count > 0)
        {
            var facts = new VisualElement();
            facts.style.marginBottom = 16;
            foreach (string f in e.facts)
            {
                var line = new Label("- " + f);
                line.style.fontSize = 24;
                line.style.color = Ink;
                line.style.whiteSpace = WhiteSpace.Normal;
                line.style.marginBottom = 4;
                facts.Add(line);
            }
            entryView.Add(facts);
        }

        if (e.stats.Count > 0)
        {
            entryView.Add(Section(e.kind == AlmanacKind.Pest ? "How it eats" : "Stats"));
            foreach (StatLine s in e.stats) entryView.Add(StatRow(s));
        }

        if (e.stages.Count > 0)
        {
            entryView.Add(Section(e.timelineTitle));
            entryView.Add(Timeline(e));
        }

        if (e.appetites.Count > 0)
        {
            entryView.Add(Section(e.kind == AlmanacKind.Pest ? "Favorite crops" : "Pests"));
            foreach (var (label, appetite) in e.appetites) entryView.Add(AppetiteRow(label, appetite));
        }

        if (e.notes.Count > 0)
        {
            entryView.Add(Section(e.kind == AlmanacKind.Pest ? "By growth stage" : "Good to know"));
            foreach (string note in e.notes)
            {
                var n = new Label("- " + note);
                n.style.fontSize = 24;
                n.style.color = Muted;
                n.style.whiteSpace = WhiteSpace.Normal;
                n.style.marginBottom = 6;
                entryView.Add(n);
            }
        }
    }

    // Pill colours, one per type (like Pokemon types): dark fills so white text always reads.
    private static Color PillColor(PillKind k) => k switch
    {
        PillKind.QuickGrower => Hex(0x2f6b2a),
        PillKind.SlowGrower => Hex(0x5c5a2c),
        PillKind.Regrows => Hex(0x2f7d6b),
        PillKind.Sturdy => Hex(0x5d5f66),
        PillKind.Fragile => Hex(0xa2496a),
        PillKind.Thirsty => Hex(0x24508f),
        PillKind.DroughtHardy => Hex(0x3f86c4),
        PillKind.Cannable => Hex(0x8e2f3f),
        PillKind.GreatCompost or PillKind.MakesCompost => Hex(0x5a4122),
        PillKind.Deer or PillKind.DeerDamage => Hex(0x7a4a22),
        PillKind.Crow or PillKind.CrowDamage => Hex(0x3a3a3e),
        PillKind.GivesCoins => Hex(0xa07514),
        PillKind.GivesGems => Hex(0x6a3fa0),
        PillKind.Guard => Hex(0x4a5a2a),
        PillKind.WatersCrops => Hex(0x2e6fae),
        _ => ChipFill,
    };
    private static readonly Color PillHot = new Color(0.75f, 0.25f, 0.17f);   // pest hits harder than normal
    private static readonly Color PillCool = new Color(0.25f, 0.54f, 0.23f);  // pest hits softer

    private static Color Hex(int rgb) =>
        new Color(((rgb >> 16) & 0xff) / 255f, ((rgb >> 8) & 0xff) / 255f, (rgb & 0xff) / 255f);

    /// <summary>A type pill; pest-damage pills are two-part ("Deer | 175%", red above 100%,
    /// green below). Tapping one shows what it means.</summary>
    private VisualElement PillChip(Pill p)
    {
        var chip = new VisualElement();
        chip.style.flexDirection = FlexDirection.Row;
        chip.style.marginRight = 8; chip.style.marginBottom = 8;
        chip.style.flexShrink = 0;
        chip.style.overflow = Overflow.Hidden;
        Radius(chip, 16);

        chip.Add(PillPart(p.label, PillColor(p.kind)));
        if (!string.IsNullOrEmpty(p.value))
            chip.Add(PillPart(p.value, AlmanacPills.Heat(p) > 0 ? PillHot : AlmanacPills.Heat(p) < 0 ? PillCool : ChipFill));

        string help = AlmanacPills.Help(p);
        chip.RegisterCallback<ClickEvent>(evt => { tooltip?.Toggle(chip, help); evt.StopPropagation(); });
        return chip;
    }

    private static Label PillPart(string text, Color fill)
    {
        var part = new Label(text);
        part.style.fontSize = 21;
        part.style.unityFontStyleAndWeight = FontStyle.Bold;
        part.style.color = Color.white;
        part.style.backgroundColor = fill;
        part.style.paddingLeft = 12; part.style.paddingRight = 12;
        part.style.paddingTop = 4; part.style.paddingBottom = 4;
        part.style.whiteSpace = WhiteSpace.NoWrap;
        part.pickingMode = PickingMode.Ignore;
        return part;
    }

    private static Label Section(string text)
    {
        var l = new Label(text);
        l.style.fontSize = 28;
        l.style.unityFontStyleAndWeight = FontStyle.Bold;
        l.style.color = TitleBrown;
        l.style.marginTop = 8; l.style.marginBottom = 8;
        return l;
    }

    /// <summary>"Money per harvest ........ $37" with a small "$25 base  +$5 Research ..." line under it.</summary>
    private static VisualElement StatRow(StatLine s)
    {
        var box = new VisualElement();
        box.style.marginBottom = 7;
        box.style.paddingBottom = 6;
        box.style.borderBottomWidth = 1;
        box.style.borderBottomColor = new Color(0.36f, 0.28f, 0.18f, 0.25f);

        var row = Row();
        box.Add(row);
        var label = new Label(s.label);
        label.style.flexGrow = 1; label.style.flexShrink = 1;
        label.style.fontSize = 24;
        label.style.color = Ink;
        label.style.whiteSpace = WhiteSpace.Normal;
        row.Add(label);

        var total = new Label(StatText.Value(s.total, s.format));
        total.style.fontSize = 26;
        total.style.unityFontStyleAndWeight = FontStyle.Bold;
        total.style.color = Ink;
        total.style.flexShrink = 0;
        total.style.marginLeft = 12;
        row.Add(total);

        if (s.HasBonus)
        {
            var parts = new List<string> { StatText.Value(s.baseValue, s.format) + " base" };
            parts.AddRange(s.Bonuses.Select(b => b.source == "Minimum"
                ? "limited to " + StatText.Value(s.total, s.format)
                : StatText.Delta(b.delta, s.format) + " " + b.source));
            var detail = new Label(string.Join("   ", parts));
            detail.style.fontSize = 20;
            detail.style.color = Muted;
            detail.style.whiteSpace = WhiteSpace.Normal;
            detail.style.marginTop = 2;
            box.Add(detail);
        }
        return box;
    }

    // ── Growth timeline ──────────────────────────────────────────

    private const float StageIcon = 104f;
    private static readonly Color TimelineLine = new Color(0.36f, 0.28f, 0.18f, 0.45f);

    /// <summary>Stage nodes (sprite, name, note) joined by lines; each gap can carry a label
    /// underneath (crop pages: how long that step takes).</summary>
    private static VisualElement Timeline(AlmanacEntry e)
    {
        var wrap = new VisualElement();
        wrap.style.marginBottom = 14;

        var row = Row();
        row.style.alignItems = Align.FlexStart;
        wrap.Add(row);
        for (int i = 0; i < e.stages.Count; i++)
        {
            if (i > 0) row.Add(TimelineGap(i - 1 < e.stageGaps.Count ? e.stageGaps[i - 1] : null));
            row.Add(TimelineNode(e.stages[i]));
        }

        if (!string.IsNullOrEmpty(e.timelineFooter))
        {
            var foot = new Label(e.timelineFooter);
            foot.style.fontSize = 21;
            foot.style.unityFontStyleAndWeight = FontStyle.Italic;
            foot.style.color = Muted;
            foot.style.whiteSpace = WhiteSpace.Normal;
            foot.style.marginTop = 8;
            wrap.Add(foot);
        }
        return wrap;
    }

    private static VisualElement TimelineNode(TimelineStage s)
    {
        var node = new VisualElement();
        node.style.width = 124; node.style.flexShrink = 0;
        node.style.alignItems = Align.Center;

        var icon = new VisualElement();
        icon.style.width = StageIcon; icon.style.height = StageIcon;
        if (s.sprite != null)
        {
            icon.style.backgroundImage = new StyleBackground(s.sprite);
            icon.style.backgroundSize = new StyleBackgroundSize(new BackgroundSize(BackgroundSizeType.Contain));
        }
        node.Add(icon);

        var name = new Label(s.name);
        name.style.fontSize = 21;
        name.style.unityFontStyleAndWeight = FontStyle.Bold;
        name.style.color = Ink;
        name.style.unityTextAlign = TextAnchor.MiddleCenter;
        // One line for every name and note, so the notes under all four stages share a baseline.
        name.style.whiteSpace = WhiteSpace.NoWrap;
        name.style.marginTop = 4;
        node.Add(name);

        if (!string.IsNullOrEmpty(s.note))
        {
            var note = new Label(s.note);
            note.style.fontSize = 20;
            note.style.unityFontStyleAndWeight = s.heat >= 0f ? FontStyle.Bold : FontStyle.Normal;
            note.style.color = s.heat >= 0f ? Color.Lerp(BarLow, BarHigh, s.heat) : Muted;
            note.style.unityTextAlign = TextAnchor.MiddleCenter;
            note.style.whiteSpace = WhiteSpace.NoWrap;
            note.style.marginTop = 2;
            node.Add(note);
        }
        return node;
    }

    private static VisualElement TimelineGap(string text)
    {
        var gap = new VisualElement();
        gap.style.flexGrow = 1; gap.style.flexBasis = 0;
        gap.style.alignItems = Align.Center;
        // Reach under the neighbouring nodes so the line meets the sprites, not empty column space.
        gap.style.marginLeft = -12; gap.style.marginRight = -12;

        var line = new VisualElement();
        line.style.height = 4;
        line.style.alignSelf = Align.Stretch;
        line.style.marginTop = StageIcon / 2f - 2f;
        line.style.backgroundColor = TimelineLine;
        Radius(line, 2);
        gap.Add(line);

        if (!string.IsNullOrEmpty(text))
        {
            var t = new Label(text);
            t.style.fontSize = 21;
            t.style.unityFontStyleAndWeight = FontStyle.Bold;
            t.style.color = Muted;
            t.style.whiteSpace = WhiteSpace.NoWrap;
            t.style.marginTop = 8;
            gap.Add(t);
        }
        return gap;
    }

    private static VisualElement AppetiteRow(string label, float appetite)
    {
        var row = Row();
        row.style.marginBottom = 10;

        var name = new Label(label);
        name.style.width = Length.Percent(30);
        name.style.fontSize = 26;
        name.style.color = Ink;
        row.Add(name);

        var track = new VisualElement();
        track.style.flexGrow = 1;
        track.style.height = 22;
        track.style.backgroundColor = BarBack;
        Radius(track, 11);
        track.style.overflow = Overflow.Hidden;
        row.Add(track);

        float fill = CropTraits.AppetiteFill(appetite);
        var bar = new VisualElement();
        bar.style.width = Length.Percent(fill * 100f);
        bar.style.height = Length.Percent(100);
        bar.style.backgroundColor = Color.Lerp(BarLow, BarHigh, fill);
        Radius(bar, 11);
        track.Add(bar);

        var word = new Label($"{CropTraits.AppetiteWord(appetite)}  (x{appetite.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture)})");
        word.style.width = Length.Percent(34);
        word.style.fontSize = 22;
        word.style.color = Muted;
        word.style.unityTextAlign = TextAnchor.MiddleRight;
        row.Add(word);
        return row;
    }

    // ── "i" badge for other menus ────────────────────────────────

    // Shared with the Farm Level (Barn) info badges, so "i" always means "tap for details". Walnut with
    // cream text: the same browns as the mailbox buttons, not a generic UI blue.
    public static readonly Color InfoBadgeColor = new Color(0.36f, 0.227f, 0.094f);
    public static readonly Color InfoBadgeText  = new Color(1f, 0.94f, 0.82f);

    /// <summary>A small round "i" that opens this entry's Almanac page. Absolutely positioned in a
    /// corner of its parent; swallows its own press so the tile underneath isn't selected too.</summary>
    public static Button InfoBadge(AlmanacKind kind, string id, bool topLeft = false, float size = 34f)
    {
        var badge = new Button(() => { if (Instance != null) Instance.OpenEntry(kind, id); }) { text = "i" };
        badge.name = "almanac-info";
        badge.style.position = Position.Absolute;
        badge.style.top = 3;
        if (topLeft) badge.style.left = 3; else badge.style.right = 3;
        badge.style.width = size; badge.style.height = size;
        badge.style.paddingLeft = 0; badge.style.paddingRight = 0; badge.style.paddingTop = 0; badge.style.paddingBottom = 0;
        badge.style.marginLeft = 0; badge.style.marginRight = 0; badge.style.marginTop = 0; badge.style.marginBottom = 0;
        badge.style.fontSize = size * 0.6f;
        badge.style.unityFontStyleAndWeight = FontStyle.BoldAndItalic;
        badge.style.color = InfoBadgeText;
        badge.style.backgroundColor = InfoBadgeColor;
        badge.style.borderTopWidth = 2; badge.style.borderBottomWidth = 2;
        badge.style.borderLeftWidth = 2; badge.style.borderRightWidth = 2;
        badge.style.borderTopColor = InfoBadgeText; badge.style.borderBottomColor = InfoBadgeText;
        badge.style.borderLeftColor = InfoBadgeText; badge.style.borderRightColor = InfoBadgeText;
        Radius(badge, size / 2f);
        badge.RegisterCallback<PointerDownEvent>(e => e.StopPropagation(), TrickleDown.NoTrickleDown);
        badge.RegisterCallback<ClickEvent>(e => e.StopPropagation());
        return badge;
    }

    // ── Style helpers ────────────────────────────────────────────

    private static VisualElement Row()
    {
        var r = new VisualElement();
        r.style.flexDirection = FlexDirection.Row;
        r.style.alignItems = Align.Center;
        return r;
    }

    private static void Fill(VisualElement v)
    {
        v.style.position = Position.Absolute;
        v.style.left = 0; v.style.right = 0; v.style.top = 0; v.style.bottom = 0;
    }

    private static void Radius(VisualElement v, float r)
    {
        v.style.borderTopLeftRadius = r; v.style.borderTopRightRadius = r;
        v.style.borderBottomLeftRadius = r; v.style.borderBottomRightRadius = r;
    }

    private static readonly Color ScrollTrack = new Color(0.29f, 0.20f, 0.11f, 0.22f);
    private static readonly Color ScrollThumb = new Color(0.45f, 0.30f, 0.14f, 0.85f);
    private const float ScrollWidth = 10f;

    /// <summary>A slim brown scrollbar: dark rounded path, rounded thumb, no arrow buttons.
    /// Only shows when the content is taller than the view.</summary>
    private static void StyleScrollbar(ScrollView scroll)
    {
        scroll.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
        scroll.verticalScrollerVisibility = ScrollerVisibility.Auto;
        Scroller bar = scroll.verticalScroller;
        bar.lowButton.style.display = DisplayStyle.None;
        bar.highButton.style.display = DisplayStyle.None;
        bar.style.width = ScrollWidth; bar.style.minWidth = ScrollWidth;
        bar.style.backgroundColor = Color.clear;
        NoBorder(bar);

        Slider slider = bar.slider;
        slider.style.marginLeft = 0; slider.style.marginRight = 0;
        slider.style.marginTop = 0; slider.style.marginBottom = 0;
        slider.style.flexGrow = 1;

        VisualElement tracker = slider.Q(className: "unity-base-slider__tracker");
        if (tracker != null)
        {
            tracker.style.backgroundColor = ScrollTrack;
            NoBorder(tracker); Radius(tracker, ScrollWidth / 2f);
            tracker.style.left = 0; tracker.style.right = 0; tracker.style.width = StyleKeyword.Auto;
        }
        VisualElement dragger = slider.Q(className: "unity-base-slider__dragger");
        if (dragger != null)
        {
            dragger.style.backgroundColor = ScrollThumb;
            NoBorder(dragger); Radius(dragger, ScrollWidth / 2f);
            dragger.style.left = 0; dragger.style.right = 0; dragger.style.width = ScrollWidth;
        }
        VisualElement draggerBorder = slider.Q(className: "unity-base-slider__dragger-border");
        if (draggerBorder != null) draggerBorder.style.display = DisplayStyle.None;
    }

    /// <summary>9-slice a notebook sprite at the Almanac's pixel scale.</summary>
    private static void Slice(VisualElement v, int border)
    {
        v.style.unitySliceLeft = border; v.style.unitySliceRight = border;
        v.style.unitySliceTop = border; v.style.unitySliceBottom = border;
        v.style.unitySliceScale = PixelScale;
    }

    private static void NoBorder(VisualElement v)
    {
        v.style.borderTopWidth = 0; v.style.borderBottomWidth = 0;
        v.style.borderLeftWidth = 0; v.style.borderRightWidth = 0;
    }
}
