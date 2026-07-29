# Inventory Sell Zone Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace the Inventory popup's per-row Sell buttons with a fixed staging zone above the list that sells any held good in x1 / x10 / All quantities for Gold, on a larger panel showing each crop's real produce art.

**Architecture:** A `SellEntry` record (id, name, icon, held count, payout function, sell action) makes the staging zone agnostic to what it sells. `BuildRows()` populates a `Dictionary<string, SellEntry>` and emits tappable rows; the zone lives in UXML *outside* the `ScrollView` so its callbacks register exactly once. Tapping a row only toggles a CSS class and re-renders the zone — it never rebuilds the list, which eliminates the reentrancy hazard of clearing a list from inside one of its own children's click handlers.

**Tech Stack:** Unity 6000.3.9f1, UI Toolkit (UXML/USS), C#, NUnit EditMode tests.

## Global Constraints

- **No emoji in UITK text.** Emoji render invisible on Android. Use sprites or plain words.
- **Gold only.** Every sale in this panel pays Coins via `CurrencyManager.AddCoins`. Wood's Cash sale stays exclusive to the Wood Rack.
- **No proactive git.** This project's rule is commit only when the user asks. Task commit steps are written out but must NOT be run until the user requests it.
- **Logging convention:** `Debug.Log($"[Inventory] …")`.
- Existing chrome must not drift: tan card `rgb(179,154,110)`, brown border `rgb(90,55,20)`, dark text `rgb(54,32,14)`, close sprite `UI_Wood_Cross_Medium.png`.
- Screen is 1080×1920 portrait.

## File Structure

| File | Responsibility |
| --- | --- |
| `Assets/Scripts/EconomyCore/InventoryMath.cs` | **Modify.** Add `JarStackValue` — pure prefix sum over heterogeneous jar values. |
| `Assets/Tests/EditMode/InventoryMathTests.cs` | **Modify.** Cover `JarStackValue`. |
| `Assets/Scripts/Cannery/CanneryManager.cs` | **Modify.** Add `JarValueAt(int)` read accessor. |
| `Assets/UI/InventoryPopupUITK/InventoryPopupUITK.uxml` | **Modify.** Header currency strip + static sell-zone markup outside the ScrollView. |
| `Assets/UI/InventoryPopupUITK/InventoryPopupUITK.uss` | **Modify.** Panel 900px, zone styles, staged-row highlight, new icon classes. |
| `Assets/Scripts/UI/InventoryPopupUITK.cs` | **Rewrite.** `SellEntry` model, staging, stack modes, icon binding. |

---

### Task 1: Jar stack value + Cannery accessor

Jars carry individual `value` fields, so a multi-jar payout must sum rather than multiply. This is the only genuinely new math in the feature, and it is pure — so it gets real tests.

**Files:**
- Modify: `Assets/Scripts/EconomyCore/InventoryMath.cs`
- Modify: `Assets/Scripts/Cannery/CanneryManager.cs:246`
- Test: `Assets/Tests/EditMode/InventoryMathTests.cs`

**Interfaces:**
- Consumes: `ReadyJar.value` (`Assets/Scripts/EconomyCore/ProcessingMath.cs:25-31`), `CanneryManager.state.readyJars` (`List<ReadyJar>`)
- Produces:
  - `public static int InventoryMath.JarStackValue(IReadOnlyList<int> values, int n)`
  - `public int CanneryManager.JarValueAt(int index)`

- [ ] **Step 1: Write the failing tests**

Append to `Assets/Tests/EditMode/InventoryMathTests.cs` (inside the existing `InventoryMathTests` class):

```csharp
    [Test]
    public void JarStackValue_SumsLeadingJars()
    {
        var values = new[] { 100, 250, 40, 900 };
        Assert.AreEqual(0,   InventoryMath.JarStackValue(values, 0));
        Assert.AreEqual(100, InventoryMath.JarStackValue(values, 1));
        Assert.AreEqual(350, InventoryMath.JarStackValue(values, 2));
        Assert.AreEqual(390, InventoryMath.JarStackValue(values, 3));
    }

    [Test]
    public void JarStackValue_ClampsAboveCount()
    {
        var values = new[] { 100, 250, 40 };
        Assert.AreEqual(390, InventoryMath.JarStackValue(values, 99));
    }

    [Test]
    public void JarStackValue_NonPositiveOrEmptyPaysNothing()
    {
        var values = new[] { 100, 250 };
        Assert.AreEqual(0, InventoryMath.JarStackValue(values, -5));
        Assert.AreEqual(0, InventoryMath.JarStackValue(new int[0], 3));
        Assert.AreEqual(0, InventoryMath.JarStackValue(null, 3));
    }

    [Test]
    public void JarStackValue_IgnoresNegativeJarValues()
    {
        var values = new[] { 100, -50, 25 };
        Assert.AreEqual(125, InventoryMath.JarStackValue(values, 3));
    }
```

Add to the top of the file if not already present:

```csharp
using System.Collections.Generic;
```

- [ ] **Step 2: Run the tests to verify they fail**

Run EditMode tests via MCP:
```
mcp__UnityMCP__run_tests  mode="EditMode"  test_filter="InventoryMathTests"
```
Fallback if the MCP bridge is unresponsive: write `Temp/run_editmode_tests.request` (see `project_playmode_bridge` memory).

Expected: FAIL — `InventoryMath` does not contain a definition for `JarStackValue` (compile error).

- [ ] **Step 3: Implement `JarStackValue`**

In `Assets/Scripts/EconomyCore/InventoryMath.cs`, add `using System.Collections.Generic;` at the top and this method inside `InventoryMath`:

```csharp
    /// <summary>
    /// Total Coins for selling the first <paramref name="n"/> ready jars. Jars carry individual
    /// values (a tier-3 sauce is worth far more than a tier-1 jam), so a stack sale sums the jars
    /// actually consumed rather than multiplying a unit price. Selling always takes from the front
    /// of the list, so this preview and the real sale agree by construction. Clamped to the list;
    /// a non-positive n, an empty list, or null pays nothing.
    /// </summary>
    public static int JarStackValue(IReadOnlyList<int> values, int n)
    {
        if (values == null || n <= 0) return 0;
        int take = Mathf.Min(n, values.Count);
        int total = 0;
        for (int i = 0; i < take; i++) total += Mathf.Max(0, values[i]);
        return total;
    }
```

- [ ] **Step 4: Add the Cannery read accessor**

In `Assets/Scripts/Cannery/CanneryManager.cs`, directly below `public int ReadyJarCount => state.readyJars.Count;` (line 246):

```csharp
    /// <summary>Coin value of the ready jar at <paramref name="index"/>, or 0 if out of range.
    /// Lets the Inventory preview a multi-jar sale without exposing the mutable jar list.</summary>
    public int JarValueAt(int index)
        => index < 0 || index >= state.readyJars.Count ? 0 : state.readyJars[index].value;
```

- [ ] **Step 5: Run the tests to verify they pass**

Run: `mcp__UnityMCP__run_tests mode="EditMode" test_filter="InventoryMathTests"`
Expected: PASS — all `JarStackValue_*` tests green, and the pre-existing `RawCropCoinValue_*` / `AddToCap_*` tests still green.

Also check the console is clean:
```
mcp__UnityMCP__read_console  types=["error"]
```
Expected: no compile errors.

- [ ] **Step 6: Commit — ONLY IF THE USER HAS ASKED**

```bash
git add Assets/Scripts/EconomyCore/InventoryMath.cs Assets/Scripts/Cannery/CanneryManager.cs Assets/Tests/EditMode/InventoryMathTests.cs
git commit -m "feat(inventory): jar stack value math + Cannery jar value accessor"
```

---

### Task 2: Panel chrome — bigger card, header currency strip, static sell zone

Layout only. At the end of this task the panel is visibly larger and shows a permanently-empty, non-functional sell zone. Wiring comes in Task 3. Splitting here means a reviewer can reject the visual proportions without touching the logic.

**Files:**
- Modify: `Assets/UI/InventoryPopupUITK/InventoryPopupUITK.uxml`
- Modify: `Assets/UI/InventoryPopupUITK/InventoryPopupUITK.uss`

**Interfaces:**
- Produces (element names Task 3 queries by `root.Q<T>("…")`):
  `currency-strip`, `cs-coins`, `cs-gems`, `cs-money`, `sell-zone`, `zone-icon`,
  `zone-title`, `stack-row`, `stack-1`, `stack-10`, `stack-all`, `zone-confirm`.
  Existing names kept: `popup-root`, `backdrop`, `popup-container`, `header`,
  `header-title`, `close-button`, `inventory-list`.
- Produces (USS classes Task 3 adds/removes at runtime):
  `inv-row--sellable`, `inv-row--staged`, `stack-btn--active`, `zone-icon--empty`,
  `inv-icon--eggs`, `inv-icon--jars`, `inv-icon--fish-perch`, `inv-icon--fish-bass`,
  `inv-icon--fish-pike`, `inv-icon--smoked`.

- [ ] **Step 1: Replace the UXML**

Overwrite `Assets/UI/InventoryPopupUITK/InventoryPopupUITK.uxml` with:

```xml
<?xml version="1.0" encoding="utf-8"?>
<ui:UXML xmlns:ui="UnityEngine.UIElements" xmlns:uie="UnityEditor.UIElements" editor-extension-mode="False">
    <Style src="InventoryPopupUITK.uss" />
    <ui:VisualElement name="popup-root" class="popup-root" style="display: none;">
        <ui:VisualElement name="backdrop" class="backdrop" picking-mode="Position" />
        <ui:VisualElement name="popup-container" class="popup-container">
            <ui:VisualElement name="header" class="header">
                <ui:Label name="header-title" text="Inventory" class="header-title" />
                <ui:VisualElement name="currency-strip" class="currency-strip">
                    <ui:VisualElement class="cs-icon cs-icon--coins" />
                    <ui:Label name="cs-coins" text="0" class="cs-value" />
                    <ui:VisualElement class="cs-icon cs-icon--gems" />
                    <ui:Label name="cs-gems" text="0" class="cs-value" />
                    <ui:VisualElement class="cs-icon cs-icon--money" />
                    <ui:Label name="cs-money" text="0" class="cs-value" />
                </ui:VisualElement>
                <ui:Button name="close-button" class="close-button" text="" />
            </ui:VisualElement>

            <ui:VisualElement name="sell-zone" class="sell-zone">
                <ui:VisualElement name="zone-icon" class="zone-icon zone-icon--empty" picking-mode="Ignore" />
                <ui:Label name="zone-title" text="Tap an item to sell it" class="zone-title" />
                <ui:VisualElement name="stack-row" class="stack-row">
                    <ui:Button name="stack-1" text="x1" class="stack-btn" />
                    <ui:Button name="stack-10" text="x10" class="stack-btn" />
                    <ui:Button name="stack-all" text="All" class="stack-btn" />
                </ui:VisualElement>
                <ui:Button name="zone-confirm" text="Sell for Gold" class="zone-confirm" />
            </ui:VisualElement>

            <ui:ScrollView name="inventory-list" class="inv-list" />
        </ui:VisualElement>
    </ui:VisualElement>
</ui:UXML>
```

- [ ] **Step 2: Replace the USS**

Overwrite `Assets/UI/InventoryPopupUITK/InventoryPopupUITK.uss` with:

```css
/* Inventory — held goods + a staging zone that sells any of them for Gold. Shares the tan/brown
   card chrome of the other popups (Wood Rack, Research). */

.popup-root {
    position: absolute;
    left: 0; right: 0; top: 0; bottom: 0;
    align-items: center;
    justify-content: center;
    opacity: 0;
    transition: opacity 0.2s ease-out;
    display: none;
}

.popup-root.open { opacity: 1; }

.backdrop {
    position: absolute;
    left: 0; right: 0; top: 0; bottom: 0;
    background-color: rgba(0, 0, 0, 0.55);
}

.popup-container {
    width: 92%;
    max-width: 900px;
    min-width: 320px;
    background-color: rgb(179, 154, 110);
    border-color: rgb(90, 55, 20);
    border-left-width: 3px; border-right-width: 3px; border-top-width: 3px; border-bottom-width: 3px;
    border-top-left-radius: 18px; border-top-right-radius: 18px;
    border-bottom-left-radius: 18px; border-bottom-right-radius: 18px;
    overflow: hidden;
    scale: 0.92 0.92;
    transition: scale 0.22s ease-out;
    flex-direction: column;
}

.popup-root.open .popup-container { scale: 1 1; }

/* ─── Header: title | currency strip | close ─── */

.header {
    flex-direction: row;
    align-items: center;
    justify-content: space-between;
    padding-top: 18px; padding-bottom: 14px;
    padding-left: 24px; padding-right: 16px;
    border-bottom-width: 2px;
    border-bottom-color: rgba(90, 55, 20, 0.4);
}

.header-title { -unity-font-style: bold; font-size: 36px; color: rgb(54, 32, 14); }

.currency-strip {
    flex-direction: row;
    align-items: center;
    flex-grow: 1;
    justify-content: flex-end;
    margin-right: 16px;
}

.cs-icon {
    width: 32px; height: 32px;
    margin-left: 18px; margin-right: 6px;
    flex-shrink: 0;
    -unity-background-image-scale-mode: scale-to-fit;
}

.cs-value { font-size: 24px; -unity-font-style: bold; color: rgb(54, 32, 14); }

.cs-icon--coins { background-image: url("project://database/Assets/Sprites/UI/Icons/Icons_Essential/Coin.png"); }
.cs-icon--gems  { background-image: url("project://database/Assets/Sprites/UI/Icons/Icons_Essential/Gem.png"); }
.cs-icon--money { background-image: url("project://database/Assets/Sprites/UI/Icons/Icons_Essential/Cash.png"); }

.close-button {
    width: 56px; height: 56px;
    background-color: rgba(0, 0, 0, 0);
    background-image: url("project://database/Assets/Sprites/UI/UI_Wood/UI_Wood_Cross_Medium.png");
    -unity-background-image-scale-mode: scale-to-fit;
    border-left-width: 0; border-right-width: 0; border-top-width: 0; border-bottom-width: 0;
    padding: 0;
    color: rgba(0, 0, 0, 0);
    flex-shrink: 0;
}
.close-button:hover { opacity: 0.85; }

/* ─── Sell zone (fixed, outside the ScrollView) ─── */

.sell-zone {
    height: 340px;
    flex-shrink: 0;
    align-items: center;
    justify-content: center;
    padding: 14px 24px 18px 24px;
    background-color: rgba(90, 55, 20, 0.18);
    border-bottom-width: 2px;
    border-bottom-color: rgba(90, 55, 20, 0.4);
}

.zone-icon {
    width: 96px; height: 96px;
    flex-shrink: 0;
    -unity-background-image-scale-mode: scale-to-fit;
}

.zone-icon--empty {
    background-image: url("project://database/Assets/Sprites/UI/Icons/Cute/RpgThings/Crate_OpenEmpty.png");
    opacity: 0.4;
}

.zone-title {
    font-size: 30px;
    -unity-font-style: bold;
    color: rgb(54, 32, 14);
    margin-top: 6px;
    margin-bottom: 10px;
}

.stack-row {
    flex-direction: row;
    justify-content: center;
    margin-bottom: 10px;
}

.stack-btn {
    width: 130px;
    height: 56px;
    margin-left: 6px; margin-right: 6px;
    font-size: 26px;
    -unity-font-style: bold;
    background-color: rgb(160, 132, 92);
    color: rgb(245, 235, 215);
    border-radius: 10px;
    border-width: 0;
}

.stack-btn--active {
    background-color: rgb(122, 92, 55);
    border-width: 3px;
    border-color: rgb(255, 236, 190);
}

.stack-btn:disabled { opacity: 0.35; }

.zone-confirm {
    width: 100%;
    max-width: 520px;
    height: 68px;
    font-size: 28px;
    -unity-font-style: bold;
    background-color: rgb(96, 128, 66);
    color: rgb(245, 235, 215);
    border-radius: 10px;
    border-width: 0;
}

.zone-confirm:disabled { opacity: 0.35; }

/* ─── List ─── */

.inv-list { flex-grow: 1; max-height: 900px; padding: 16px 22px 22px 22px; }

.inv-row {
    flex-direction: row;
    align-items: center;
    padding: 12px 16px;
    margin-bottom: 10px;
    background-color: rgb(255, 250, 238);
    border-radius: 14px;
    border-left-width: 5px;
    border-left-color: rgba(90, 55, 20, 0.35);
}

.inv-row--sellable:hover { background-color: rgb(255, 244, 220); }

.inv-row--staged {
    background-color: rgb(238, 248, 226);
    border-left-color: rgb(96, 128, 66);
    border-left-width: 10px;
}

.inv-icon {
    width: 48px; height: 48px;
    margin-right: 14px;
    flex-shrink: 0;
    -unity-background-image-scale-mode: scale-to-fit;
}

.inv-name { flex-grow: 1; font-size: 28px; -unity-font-style: bold; color: rgb(54, 32, 14); }

.inv-value {
    font-size: 30px; -unity-font-style: bold;
    color: rgb(54, 32, 14);
    min-width: 90px;
    -unity-text-align: middle-right;
}

.inv-section {
    -unity-font-style: bold;
    font-size: 26px;
    color: rgb(92, 70, 40);
    margin-top: 14px;
    margin-bottom: 4px;
}

/* ─── Per-item icons ─── */

.inv-icon--compost    { background-image: url("project://database/Assets/Sprites/UI/Icons/Icons_Essential/Compost.png"); }
.inv-icon--wood       { background-image: url("project://database/Assets/Sprites/UI/Icons/Cute/RpgResources/Log.png"); }
.inv-icon--eggs       { background-image: url("project://database/Assets/Sprites/UI/Icons/Cute/Food/Egg.png"); }
.inv-icon--jars       { background-image: url("project://database/Assets/Sprites/UI/Icons/Cute/RpgThings/Jug_Orange.png"); }
.inv-icon--fish-perch { background-image: url("project://database/Assets/Sprites/UI/Icons/Cute/Fishing/Fish_Perch.png"); }
.inv-icon--fish-bass  { background-image: url("project://database/Assets/Sprites/UI/Icons/Cute/Fishing/Fish_BassGreen.png"); }
.inv-icon--fish-pike  { background-image: url("project://database/Assets/Sprites/UI/Icons/Cute/Fishing/Fish_GreenPike.png"); }

/* No smoked-fish art in the packs — an amber tint over the raw fish reads as cured. */
.inv-icon--smoked { -unity-background-image-tint-color: rgb(188, 132, 74); }
```

- [ ] **Step 3: Verify it compiles and imports clean**

```
mcp__UnityMCP__refresh_unity
mcp__UnityMCP__read_console  types=["error","warning"]
```
Expected: no errors. **Specifically watch for "Unknown property" / "Cannot resolve url" USS warnings** — every `url()` above must resolve. If one fails, the sprite path is wrong; verify with `mcp__UnityMCP__manage_asset` before editing further.

Note: the old USS used `-unity-background-scale-mode`; the correct Unity 6 property is `-unity-background-image-scale-mode`. If Unity warns about the new name, revert those specific lines to the old spelling — but do not change anything else.

- [ ] **Step 4: Commit — ONLY IF THE USER HAS ASKED**

```bash
git add Assets/UI/InventoryPopupUITK/InventoryPopupUITK.uxml Assets/UI/InventoryPopupUITK/InventoryPopupUITK.uss
git commit -m "feat(inventory): larger panel, header currency strip, sell-zone markup"
```

---

### Task 3: Wire the sell zone

**Files:**
- Rewrite: `Assets/Scripts/UI/InventoryPopupUITK.cs`

**Interfaces:**
- Consumes: `InventoryMath.JarStackValue` and `CanneryManager.JarValueAt` (Task 1); all element names and USS classes from Task 2.
- Consumes (existing, verified):
  - `CurrencyManager.Instance` — `.Coins`, `.Gems`, `.Money`, `.Wood`, `.MaxWood`, `.Compost`, `.AddCoins(int)`, `.SpendWood(int) → bool`, events `OnCoinsChanged/OnGemsChanged/OnMoneyChanged/OnWoodChanged/OnCompostChanged` (all `Action<int>`)
  - `ItemInventoryManager.Instance` — `.Eggs`, `.EggSellCoins`, `.GetCrop(string) → int`, `.RawCoinValue(CropData) → int`, `.TrySpendCrop(string,int) → bool`, `.TrySpendEggs(int) → bool`, event `OnChanged` (`Action`)
  - `PantryManager.Instance` — `.GetRaw(int) → int`, `.GetSmoked(int) → int`, `.TotalRaw`, `.TotalSmoked`, event `OnChanged`
  - `SmokehouseManager.Instance` — `.RawValue(int) → int`, `.SmokedValue(int) → int`, `.TrySellRaw(int) → bool`, `.TrySellSmoked(int) → bool`
  - `CanneryManager.Instance` — `.ReadyJarCount`, `.TrySellJar(int) → bool`
  - `WoodRackPopupUITK.Instance.GoldPricePerWood → int`
  - `WoodcuttingMath.StackMode` `{One, Ten, All}`, `.ResolveStackAmount(StackMode,int) → int`, `.SellValue(int,int) → int`
  - `FishTiers.Count` (3), `.Name(int)`, `.SmokedName(int)`
  - `CropData.cropName`, `.cropSprite`; `CropDatabase.allCrops`

- [ ] **Step 1: Rewrite the file**

Overwrite `Assets/Scripts/UI/InventoryPopupUITK.cs` with:

```csharp
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
        "inv-icon--fish-pike", "inv-icon--smoked"
    };

    [Header("Data")]
    [SerializeField] private CropDatabase cropDatabase;

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
        e.Sell(n);
        Debug.Log($"[Inventory] Sold {n} x {e.Name} for {e.Payout(n)} Gold.");
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
            if (zoneConfirm != null) { zoneConfirm.text = "Sell for Gold"; zoneConfirm.SetEnabled(false); }
            return;
        }

        ApplyIcon(zoneIcon, e.Sprite, e.IconClass);
        if (zoneTitle != null) zoneTitle.text = $"{e.Name}   {e.Held:N0} held";

        int n = WoodcuttingMath.ResolveStackAmount(stackMode, e.Held);
        if (zoneConfirm != null)
        {
            zoneConfirm.text = n > 0 ? $"Sell {n:N0}   +{e.Payout(n):N0} Gold" : "Sell for Gold";
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

    private static string FishIconClass(int tier)
    {
        switch (tier)
        {
            case 1:  return "inv-icon--fish-perch";
            case 2:  return "inv-icon--fish-bass";
            default: return "inv-icon--fish-pike";
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

        // Compost has no sell price — read-only, and no dead button to look broken.
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

            if (pantry != null)
                for (int tier = 1; tier <= FishTiers.Count; tier++)
                {
                    int t = tier;                       // capture per iteration, not the loop variable
                    string fishClass = FishIconClass(t);

                    int raw = pantry.GetRaw(t);
                    if (raw > 0)
                    {
                        int rawValue = smoke != null ? smoke.RawValue(t) : 0;
                        if (rawValue > 0)
                            AddSellRow(new SellEntry
                            {
                                Id = "fish:raw:" + t, Name = FishTiers.Name(t),
                                IconClass = fishClass, Held = raw,
                                Payout = n => rawValue * n,
                                Sell = n => { for (int i = 0; i < n; i++) if (!smoke.TrySellRaw(t)) break; }
                            });
                        else
                            AddPlainRow(fishClass, FishTiers.Name(t), raw.ToString("N0"));
                    }

                    int smoked = pantry.GetSmoked(t);
                    if (smoked > 0)
                    {
                        int smokedValue = smoke != null ? smoke.SmokedValue(t) : 0;
                        if (smokedValue > 0)
                            AddSellRow(new SellEntry
                            {
                                Id = "fish:smoked:" + t, Name = FishTiers.SmokedName(t),
                                IconClass = fishClass + " inv-icon--smoked", Held = smoked,
                                Payout = n => smokedValue * n,
                                Sell = n => { for (int i = 0; i < n; i++) if (!smoke.TrySellSmoked(t)) break; }
                            });
                        else
                            AddPlainRow(fishClass + " inv-icon--smoked", FishTiers.SmokedName(t), smoked.ToString("N0"));
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

    private void AddPlainRow(string iconClasses, string name, string valueText)
        => MakeRow(iconClasses, null, name, valueText);

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
```

- [ ] **Step 2: Verify it compiles**

```
mcp__UnityMCP__refresh_unity
mcp__UnityMCP__read_console  types=["error"]
```
Expected: no compile errors.

Likely first-attempt failures and their fixes:
- `CanneryManager.OnChanged` not found → drop the three Cannery subscribe/unsubscribe lines; the event was verified to exist at `CanneryManager.cs:44`, so this should not fire.
- `StyleKeyword.Null` not assignable to `StyleBackground` → use `el.style.backgroundImage = new StyleBackground(); ` instead.

- [ ] **Step 3: Re-run the EditMode suite for regressions**

```
mcp__UnityMCP__run_tests  mode="EditMode"
```
Expected: the full suite passes (baseline is 154/154 per project memory — confirm the count did not drop).

- [ ] **Step 4: Commit — ONLY IF THE USER HAS ASKED**

```bash
git add Assets/Scripts/UI/InventoryPopupUITK.cs
git commit -m "feat(inventory): staging sell zone with x1/x10/All, real produce icons"
```

---

### Task 4: Play-mode visual verification

The project rule is to verify own work visually rather than asking the user to check. `look_at_game_view` captures the world camera only — UITK overlay UI does **not** appear in it. Use `ScreenCapture.CaptureScreenshot` via `execute_code`, per the fishing-polish memory.

**Files:** none — verification only.

- [ ] **Step 1: Back up the save**

Project memory records a dev-save wipe incident. Before entering play mode:

```bash
cp "$USERPROFILE/AppData/LocalLow/DefaultCompany/IdleFarm - Silo/gamedata.json" "$USERPROFILE/AppData/LocalLow/DefaultCompany/IdleFarm - Silo/gamedata.json.bak-inventory"
```
If the path differs, locate it first — do not skip this step.

- [ ] **Step 2: Enter play mode and open the popup**

```
mcp__UnityMCP__manage_editor  action="play"
```
Then via `execute_code`: `InventoryPopupUITK.Instance.Open();`

- [ ] **Step 3: Capture the Empty state**

Via `execute_code`:
```csharp
ScreenCapture.CaptureScreenshot("Temp/inv_empty.png");
```
Wait one frame, then `Read` the PNG.

Confirm: panel is visibly wider than before; header shows title + three currency icons + close; zone shows a faded empty crate, "Tap an item to sell it", and greyed-out x1/x10/All + confirm; **crop rows show produce art, not coins**.

- [ ] **Step 4: Capture the Staged state**

Grant test stock and stage a crop via `execute_code`:
```csharp
ItemInventoryManager.Instance.AddCrop("Carrot", 25, out _);
```
Then click the Carrot row (or call `Stage` indirectly by re-opening and tapping). Capture `Temp/inv_staged.png` and read it.

Confirm: Carrot row is highlighted green-left-border; zone shows the carrot sprite, "Carrot 25 held", x1 active, confirm reads `Sell 1   +N Gold`.

- [ ] **Step 5: Exercise the sale**

Tap `All`, confirm the button text updates to `Sell 25   +<25×N> Gold`, press it. Confirm: Coins rise by exactly that amount, the Carrot row disappears, and the zone reverts to the empty crate.

Check the console:
```
mcp__UnityMCP__read_console  types=["error","exception"]
```
Expected: none.

- [ ] **Step 6: Exit play mode**

```
mcp__UnityMCP__manage_editor  action="stop"
```

- [ ] **Step 7: Report**

Report to the user with the two screenshots attached via `SendUserFile`. State plainly what was verified and what was not (e.g. fish/jar rows are only reachable with Pantry stock — say so if they were not exercised).

---

## Self-Review

**Spec coverage:**

| Spec section | Task |
| --- | --- |
| §1 panel 92% / 900px, list 900px | Task 2 |
| §1 header currency strip replacing Currencies section | Task 2 (markup) + Task 3 (`RenderCurrencyStrip`) |
| §2 empty state, crate, disabled-but-visible controls | Task 2 (USS) + Task 3 (`RenderSellZone`) |
| §2 staged state, 96px icon, "held", sticky toggle, green confirm | Task 3 |
| §2 stackMode persists across swaps, resets on Close | Task 3 (`Close`, `SetStack`) |
| §2 confirm disabled at 0; row leaves list at 0 | Task 3 (`RenderSellZone`, `Confirm`) |
| §3 per-row Sell buttons removed, row is the tap target | Task 3 (`AddSellRow`) |
| §3 icon table (crops/eggs/fish/smoked/jars/wood/compost) | Task 2 (classes) + Task 3 (`ApplyIcon`, `FishIconClass`) |
| §3 Compost read-only, no dead button | Task 3 (`AddPlainRow`) |
| §4 `SellEntry`, dictionary, zone outside ScrollView | Tasks 2 + 3 |
| §4 `JarValueAt`, `JarStackValue` | Task 1 |
| §4 fish loop breaks early on false | Task 3 |
| §4 `GoldPricePerWood` read defensively | Task 3 (`BuildRows`) |
| §5 `JarStackValue` tests | Task 1 |
| §5 manual play-mode verification | Task 4 |

No gaps.

**Placeholder scan:** No TBD/TODO. Every code step carries complete, runnable code. The only "placeholder" is `Jug_Orange.png` as jar art, which is a deliberate, spec-flagged asset choice, not a plan gap.

**Type consistency:** `SellEntry` field names (`Id`, `Name`, `Sprite`, `IconClass`, `Held`, `ValueText`, `Payout`, `Sell`) are used identically in `BuildRows`, `AddSellRow`, `RenderSellZone` and `Confirm`. `ApplyIcon(VisualElement, Sprite, string)` has one signature, called from `MakeRow` and `RenderSellZone`. `JarStackValue(IReadOnlyList<int>, int)` is declared in Task 1 and called in Task 3 with a `List<int>`, which satisfies `IReadOnlyList<int>`. UXML element names in Task 2 match every `root.Q<T>("…")` in Task 3. USS classes emitted in Task 3 all exist in Task 2's stylesheet.

**One deviation from the spec, deliberate:** the spec wrote the confirm label as `Sell for Gold +30`. The plan uses `Sell {n}   +{payout} Gold`, which shows both the count and the value — the thing the user explicitly liked about the rejected slider option ("constantly shows x___ how many it's going to sell, and how much it's going to be worth"). Flag this in the final report so it can be reverted if unwanted.
