# Run Stats + Welcome-Back UITK Redesign (Plan 3 of 3) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace the flat TMP Run Stats popup and the thin welcome-back modal with the approved "Ledger" UI Toolkit surfaces — a dense per-crop / per-cause Run Stats recap and two outcome-driven welcome-back modals (Continue vs Run-ended) — all fed by one shared `RunLedgerData` DTO.

**Architecture:** A `RunLedgerData` DTO (Assembly-CSharp) is the single seam: built either from the live `RunStats`/`RunManager` (`FromCurrentRun`) or from a simulated `OfflineRunOutcome` (`FromOffline`). A static `RunStatsLedgerView` builds the shared ledger sections (Economy / Harvested itemized / Losses / Defense) into any container. Three surfaces consume it: a new `RunStatsPopupUITK` (full recap, replaces the TMP `RunStatsPopup`) and the redesigned `OfflineProgressModalUITK` with green "Continue" and red "Run-ended" variants. Follows the project's UITK popup conventions (RunewoodPanelSettings, modal-root/backdrop/card, cache→wire→subscribe lifecycle).

**Tech Stack:** Unity UI Toolkit (UXML/USS), C# MonoBehaviour controllers, shared `RunewoodPanelSettings`, `TimeFormat` (EconomyCore).

**Spec:** `docs/superpowers/specs/2026-06-16-offline-run-simulator-and-stats-redesign-design.md`
**Approved mockup:** `.superpowers/brainstorm/.../final-set.html` (ledger; CTA bottom; money-before-coins; itemized harvests + losses).
**Depends on:** Plan 1 (`TimeFormat`, sim types) + Plan 2 (`RunStats` per-crop/lightning + `IngestOfflineResult`, `OfflineRunOutcome`, gated `OfflineProgressManager`). Build after both.

---

## Conventions & verification (read first)

UITK + scene wiring is not unit-testable here. Verify each task by: (1) compile via `refresh_unity`
(force, all) + `read_console` filtered to the file (zero errors); (2) a final **play-mode visual smoke
test** driving each surface and capturing screenshots via MCP. Apply the project UITK gotchas:
- Panel root `pickingMode = Ignore` by default, `Position` only while open (clicks pass through otherwise).
- Operate show/hide on the **popup root** (`modal-root`/`popup-root`), not the panel root.
- One-tick delay before adding the `open` class so the transition fires.
- Link USS via `<Style src="..."/>` at the top of each UXML.
- Crop icons: `element.style.backgroundImage = new StyleBackground(sprite);` (Sprite overload).
- All UIDocuments share `Assets/Settings/RunewoodPanelSettings.asset`.

When verifying timing/animation via MCP, set `Application.runInBackground = true` (Editor pauses the player
loop while unfocused — see gotcha #10).

---

## File Structure

- Create: `Assets/Scripts/UI/RunLedgerData.cs` — DTO + `FromCurrentRun` / `FromOffline` builders.
- Create: `Assets/Scripts/UI/RunStatsLedgerView.cs` — static UITK section builders (shared by all surfaces).
- Create: `Assets/UI/RunStatsPopupUITK/RunStatsPopupUITK.uxml` + `.uss`
- Create: `Assets/Scripts/UI/RunStatsPopupUITK.cs` — controller (replaces TMP `RunStatsPopup`).
- Modify: `Assets/UI/OfflineProgressModalUITK/OfflineProgressModalUITK.uxml` + `.uss` — outcome variants + breakdown container + two CTAs.
- Modify: `Assets/Scripts/UI/OfflineProgressModalUITK.cs` — `OpenContinue` / `OpenEnded` APIs.
- Modify: `Assets/Scripts/OfflineProgressManager.cs` — call the new modal/stats APIs (replaces the Plan-2 placeholder calls).
- Modify: `Assets/Scripts/RunManager.cs` — `EndRun` shows `RunStatsPopupUITK` instead of TMP (the ONLY code
  caller; the "Run Stats"/"Prev. Run Stats" button is a serialized scene field on the popup itself, not in `RunUI`).
- Delete (last): TMP `Assets/Scripts/RunStatsPopup.cs` + its scene GameObject, after callers are switched.

---

## Task 1: `RunLedgerData` DTO + builders

The single data seam every surface renders from. Built from live run state or a simulated offline outcome.

**Files:**
- Create: `Assets/Scripts/UI/RunLedgerData.cs`

- [ ] **Step 1: Implement the DTO + builders**

```csharp
// Assets/Scripts/UI/RunLedgerData.cs
using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>One itemized harvested-crop row (icon + name + count).</summary>
public struct LedgerCropRow { public Sprite sprite; public string name; public int count; }

/// <summary>
/// Render-ready data for the ledger surfaces (Run Stats popup + both welcome-back modals).
/// Built once from either the live run (FromCurrentRun) or a simulated offline outcome (FromOffline).
/// </summary>
public class RunLedgerData
{
    public string farmTimeHms = "0s";
    public string realTimeHms = "0s";
    public bool bankrupt;
    public bool offlineTaxApplied;

    public int moneyEarned, moneySpentOnBags, coinsBanked, compostGained, resumeMoney;
    public bool hasResumeMoney; // true on the survived-offline path (shows "Money now")

    public readonly List<LedgerCropRow> harvested = new List<LedgerCropRow>();
    public int totalHarvested;

    public int eatenByDeer, eatenByCrows, struckByLightning, driedUp, rotted;
    public int deerRepelled, crowsRepelled;
    public bool hasDefense; // false offline (defense not simulated) -> hide the section

    /// <summary>Build from the just-ended live run (RunStats + RunManager).</summary>
    public static RunLedgerData FromCurrentRun()
    {
        var d = new RunLedgerData();
        var rm = RunManager.Instance;
        var rs = RunStats.Instance;
        if (rm != null)
        {
            d.farmTimeHms = TimeFormat.Hms(rm.LastRunSurvivedSeconds);
            d.realTimeHms = TimeFormat.Hms(rm.LastRunRealSeconds);
            d.bankrupt = rm.LastRunEndedBankrupt;
        }
        if (rs != null)
        {
            d.moneyEarned = rs.MoneyEarned;
            d.coinsBanked = rs.CoinsBanked;
            d.eatenByDeer = rs.PlantsEatenByDeer;
            d.eatenByCrows = rs.PlantsEatenByCrows;
            d.struckByLightning = rs.PlantsStruckByLightning;
            d.driedUp = rs.PlantsDehydrated;
            d.rotted = rs.CropsDecayed;
            d.deerRepelled = rs.DeerRepelledByFence;
            d.crowsRepelled = rs.CrowsRepelledByScarecrow;
            d.hasDefense = true;
            foreach (var kv in rs.HarvestedByCrop)
                AddCrop(d, kv.Key, kv.Value);
            d.totalHarvested = rs.CropsHarvested;
        }
        return d;
    }

    /// <summary>Build from a simulated offline outcome (already taxed payouts).</summary>
    public static RunLedgerData FromOffline(OfflineRunOutcome o, TimeSpan gap)
    {
        var d = new RunLedgerData { offlineTaxApplied = true, hasDefense = false };
        d.farmTimeHms = TimeFormat.Hms(o.result.finalFarmSeconds);
        d.realTimeHms = TimeFormat.Hms((float)gap.TotalSeconds);
        d.bankrupt = o.result.bankrupt;
        d.moneyEarned = o.result.moneyEarned;
        d.moneySpentOnBags = o.result.moneySpentOnBags;
        d.coinsBanked = o.taxedCoins;
        d.compostGained = o.compostGranted;
        d.resumeMoney = o.taxedResumeMoney;
        d.hasResumeMoney = !o.result.bankrupt;
        d.eatenByDeer = o.result.eatenByDeer;
        d.eatenByCrows = o.result.eatenByCrows;
        d.struckByLightning = o.result.struckByLightning;
        d.driedUp = o.result.driedUp;
        d.rotted = o.result.rotted;
        foreach (var kv in o.harvestedByCrop) AddCrop(d, kv.Key, kv.Value);
        d.totalHarvested = o.result.TotalHarvested;
        return d;
    }

    private static void AddCrop(RunLedgerData d, CropData crop, int count)
    {
        if (crop == null || count <= 0) return;
        d.harvested.Add(new LedgerCropRow
        {
            sprite = crop.cropSprite != null ? crop.cropSprite : crop.seedPacketSprite,
            name = crop.cropName,
            count = count
        });
    }
}
```

- [ ] **Step 2: Verify it compiles**

`refresh_unity` (force, all) → `read_console` filter `RunLedgerData`. Expected: no errors. (`RunStats` props
`MoneyEarned`/`CoinsBanked`/`PlantsEatenByDeer`/`PlantsStruckByLightning`/`HarvestedByCrop` come from Plan 2.)

- [ ] **Step 3: Commit**

```bash
git add Assets/Scripts/UI/RunLedgerData.cs Assets/Scripts/UI/RunLedgerData.cs.meta
git commit -m "feat(ui): RunLedgerData DTO (live + offline builders) for ledger surfaces"
```

---

## Task 2: `RunStatsLedgerView` — shared section builder

Builds the ledger sections into any container, so all three surfaces share one renderer (DRY). USS classes
match Task 3's stylesheet (and reuse the existing `stat-row`/`section-title` names from the welcome-back USS).

**Files:**
- Create: `Assets/Scripts/UI/RunStatsLedgerView.cs`

- [ ] **Step 1: Implement the builder**

```csharp
// Assets/Scripts/UI/RunStatsLedgerView.cs
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Builds the shared "ledger" sections (Economy / Harvested / Losses / Defense) from a RunLedgerData into
/// a container VisualElement. Used by RunStatsPopupUITK and both welcome-back modal variants.
/// `compact` trims the full recap to the lighter "Continue" summary (no Defense, no spent-on-bags line).
/// </summary>
public static class RunStatsLedgerView
{
    public static void Build(VisualElement container, RunLedgerData d, bool compact)
    {
        container.Clear();

        // Economy
        var econ = Section(container, "💰 Economy");
        if (d.hasResumeMoney) Row(econ, "Money now", "$" + d.resumeMoney.ToString("N0"), null);
        else if (!compact)
        {
            Row(econ, "Money earned", "$" + d.moneyEarned.ToString("N0"), null);
            if (d.moneySpentOnBags > 0) Row(econ, "Spent on seed bags", "−$" + d.moneySpentOnBags.ToString("N0"), "neg");
        }
        Row(econ, "🪙 Coins banked", "+" + d.coinsBanked.ToString("N0"), "coin");
        if (d.compostGained > 0) Row(econ, "🌱 Compost gained", "+" + d.compostGained.ToString("N0"), "pos");
        if (d.offlineTaxApplied) Row(econ, "after 30% offline tax", "applied", "dim");

        // Harvested (itemized)
        var harv = Section(container, "🌾 Harvested");
        if (d.harvested.Count == 0) Row(harv, "Nothing harvested", "0", "dim");
        foreach (var c in d.harvested) CropRow(harv, c);
        Row(harv, "Total harvested", d.totalHarvested.ToString("N0"), "total");

        // Losses (itemized by cause)
        var loss = Section(container, "☠️ Losses");
        Row(loss, "🦌 Eaten by deer", d.eatenByDeer.ToString(), "neg");
        Row(loss, "🐦 Eaten by crows", d.eatenByCrows.ToString(), "neg");
        Row(loss, "⚡ Struck by lightning", d.struckByLightning.ToString(), "neg");
        Row(loss, "🏜️ Dried up", d.driedUp.ToString(), "neg");
        Row(loss, "🍂 Rotted", d.rotted.ToString(), "neg");

        // Defense (live only)
        if (!compact && d.hasDefense)
        {
            var def = Section(container, "🛡️ Defense");
            Row(def, "Deer repelled (fence)", d.deerRepelled.ToString(), null);
            Row(def, "Crows repelled (scarecrow)", d.crowsRepelled.ToString(), null);
        }
    }

    private static VisualElement Section(VisualElement parent, string title)
    {
        var header = new Label(title); header.AddToClassList("section-title");
        parent.Add(header);
        var body = new VisualElement(); body.AddToClassList("section");
        parent.Add(body);
        return body;
    }

    private static void Row(VisualElement parent, string label, string value, string valueMod)
    {
        var row = new VisualElement(); row.AddToClassList("stat-row");
        if (valueMod == "total") row.AddToClassList("stat-row--total");
        if (valueMod == "coin")  row.AddToClassList("stat-row--coin");
        var l = new Label(label); l.AddToClassList("stat-row__label");
        var v = new Label(value); v.AddToClassList("stat-row__value");
        if (valueMod == "neg") v.AddToClassList("stat-row__value--negative");
        if (valueMod == "pos" || valueMod == "coin") v.AddToClassList("stat-row__value--positive");
        if (valueMod == "dim") { row.AddToClassList("stat-row--dim"); }
        row.Add(l); row.Add(v);
        parent.Add(row);
    }

    private static void CropRow(VisualElement parent, LedgerCropRow c)
    {
        var row = new VisualElement(); row.AddToClassList("stat-row");
        var left = new VisualElement(); left.AddToClassList("crop-row__left");
        var icon = new VisualElement(); icon.AddToClassList("crop-row__icon");
        if (c.sprite != null) icon.style.backgroundImage = new StyleBackground(c.sprite);
        var name = new Label(c.name); name.AddToClassList("stat-row__label");
        left.Add(icon); left.Add(name);
        var v = new Label(c.count.ToString("N0")); v.AddToClassList("stat-row__value");
        row.Add(left); row.Add(v);
        parent.Add(row);
    }
}
```

- [ ] **Step 2: Verify it compiles**

`refresh_unity` (force, all) → `read_console` filter `RunStatsLedgerView`. Expected: no errors.

- [ ] **Step 3: Commit**

```bash
git add Assets/Scripts/UI/RunStatsLedgerView.cs Assets/Scripts/UI/RunStatsLedgerView.cs.meta
git commit -m "feat(ui): shared ledger section builder (economy/harvested/losses/defense)"
```

---

## Task 3: Run Stats popup UXML + USS

The full-recap surface. Hero (Farm Time · Score + Real time played), bankruptcy banner, a scrollable
ledger container the builder fills, and a bottom Close CTA.

**Files:**
- Create: `Assets/UI/RunStatsPopupUITK/RunStatsPopupUITK.uxml`
- Create: `Assets/UI/RunStatsPopupUITK/RunStatsPopupUITK.uss`

- [ ] **Step 1: Write the UXML**

```xml
<!-- Assets/UI/RunStatsPopupUITK/RunStatsPopupUITK.uxml -->
<ui:UXML xmlns:ui="UnityEngine.UIElements" editor-extension-mode="False">
    <Style src="RunStatsPopupUITK.uss" />
    <ui:VisualElement name="popup-root" class="popup-root" style="display: none;">
        <ui:VisualElement name="backdrop" class="modal-backdrop" />
        <ui:VisualElement name="card" class="modal-card">
            <ui:Label name="title" text="Run Over" class="modal-title" />
            <ui:VisualElement class="hero">
                <ui:Label text="FARM TIME · SCORE" class="hero__label" />
                <ui:Label name="hero-score" text="0s" class="hero__score" />
                <ui:Label name="hero-real" text="Real time played · 0s" class="hero__real" />
            </ui:VisualElement>
            <ui:Label name="bankrupt-banner" text="💸 Bankrupt — ran out of seed money" class="bankrupt-banner" style="display:none;" />
            <ui:ScrollView name="ledger-scroll" class="ledger-scroll">
                <ui:VisualElement name="ledger" class="ledger" />
            </ui:ScrollView>
            <ui:Button name="close-button" text="Close" class="cta cta--primary" />
        </ui:VisualElement>
    </ui:VisualElement>
</ui:UXML>
```

- [ ] **Step 2: Write the USS** (ledger styles shared in spirit with the welcome-back USS; kept local here)

```css
/* Assets/UI/RunStatsPopupUITK/RunStatsPopupUITK.uss */
.popup-root { position: absolute; top:0; left:0; right:0; bottom:0; align-items: center; justify-content: center; }
.modal-backdrop { position: absolute; top:0; left:0; right:0; bottom:0; background-color: rgba(0,0,0,0.85); }
.modal-card {
    width: 92%; max-height: 88%;
    background-color: rgb(44,36,23); border-color: rgb(110,86,50);
    border-width: 4px; border-radius: 22px;
    padding: 22px; flex-direction: column;
}
.modal-title { font-size: 36px; -unity-font-style: bold; color: rgb(245,233,200); -unity-text-align: middle-center; margin-bottom: 10px; }

.hero { background-color: rgb(34,26,14); border-color: rgb(90,74,48); border-width: 1px; border-radius: 14px; padding: 14px; margin-bottom: 12px; align-items: center; }
.hero__label { font-size: 16px; color: rgb(255,215,0); -unity-font-style: bold; letter-spacing: 2px; }
.hero__score { font-size: 40px; -unity-font-style: bold; color: rgb(255,255,255); }
.hero__real { font-size: 16px; color: rgb(138,125,96); margin-top: 2px; }

.bankrupt-banner { background-color: rgb(90,31,28); color: rgb(255,180,168); -unity-text-align: middle-center; -unity-font-style: bold; font-size: 18px; padding: 8px; border-radius: 10px; margin-bottom: 12px; white-space: normal; }

.ledger-scroll { flex-grow: 1; margin-bottom: 12px; }
.ledger { flex-direction: column; }

.section-title { font-size: 20px; -unity-font-style: bold; color: rgb(255,220,140); margin-top: 8px; margin-bottom: 6px; border-bottom-width: 2px; border-bottom-color: rgb(80,64,40); padding-bottom: 4px; }
.section { flex-direction: column; margin-bottom: 6px; }
.stat-row { flex-direction: row; justify-content: space-between; align-items: center; padding-top: 5px; padding-bottom: 5px; }
.stat-row--total { border-top-width: 1px; border-top-color: rgba(255,240,210,0.18); padding-top: 8px; margin-top: 2px; }
.stat-row--coin { background-color: rgb(51,41,15); border-radius: 8px; padding-left: 8px; padding-right: 8px; }
.stat-row--dim { opacity: 0.6; }
.stat-row__label { color: rgb(216,201,166); font-size: 20px; }
.stat-row__value { color: rgb(255,255,255); font-size: 21px; -unity-font-style: bold; }
.stat-row__value--positive { color: rgb(155,216,78); }
.stat-row__value--negative { color: rgb(255,140,122); }

.crop-row__left { flex-direction: row; align-items: center; }
.crop-row__icon { width: 28px; height: 28px; margin-right: 10px; background-size: 28px 28px; -unity-background-scale-mode: scale-to-fit; }

.cta { padding-top: 13px; padding-bottom: 13px; font-size: 22px; -unity-font-style: bold; -unity-text-align: middle-center; border-radius: 12px; border-width: 2px; }
.cta--primary { background-color: rgb(90,138,48); color: rgb(255,255,255); border-color: rgb(120,180,90); }
.cta--primary:hover { background-color: rgb(105,158,60); }
.cta--gold { background-color: rgb(107,90,30); color: rgb(255,255,255); border-color: rgb(150,124,40); }
.cta--ghost { background-color: rgba(0,0,0,0); color: rgb(168,152,120); font-size: 18px; border-color: rgba(0,0,0,0); }
```

- [ ] **Step 3: Verify import**

`refresh_unity` (force, assets) → `read_console`. Expected: no UXML/USS import errors. (Visual layout is
verified in the Task 8 smoke test; tune paddings then.)

- [ ] **Step 4: Commit**

```bash
git add Assets/UI/RunStatsPopupUITK/
git commit -m "feat(ui): RunStatsPopupUITK uxml + uss (ledger recap)"
```

---

## Task 4: `RunStatsPopupUITK` controller + scene wiring + caller switch

Controller that opens/closes, fills the hero + ledger, and ports the "Run Stats"/"Prev. Run Stats" button
behavior from the old TMP `RunStatsPopup`. Then switch callers.

**Files:**
- Create: `Assets/Scripts/UI/RunStatsPopupUITK.cs`
- Modify: `Assets/Scripts/RunManager.cs` (EndRun)
- Scene: add a `UIDocument` GameObject for this popup; move the existing run-stats uGUI button reference onto it.

- [ ] **Step 1: Write the controller**

```csharp
// Assets/Scripts/UI/RunStatsPopupUITK.cs
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
```

> The `prevRunStatsButton` field + the run-started/ended label swap port the behavior from the old TMP
> `RunStatsPopup` (see its `OnRunStarted`/`OnRunEnded`). Re-use the SAME uGUI button GameObject from the scene
> by dragging it onto this field, then remove the old script (Task 8).

- [ ] **Step 2: Scene wiring**

Create a GameObject `RunStatsPopupUITK` with a `UIDocument` whose **Source Asset** = the new UXML and
**Panel Settings** = `Assets/Settings/RunewoodPanelSettings.asset`, add the `RunStatsPopupUITK` component,
and drag the existing run-stats uGUI button onto `prevRunStatsButton`. (Mirror an existing UITK popup
GameObject's setup, e.g. the OfflineProgressModal's.) Do this via MCP `manage_gameobject`/`manage_components`
or by hand in the Editor.

- [ ] **Step 3: Switch callers**

In `Assets/Scripts/RunManager.cs:258` (`EndRun`), replace the only code caller:

```csharp
        // before (line 258):
        // if (RunStatsPopup.Instance != null) RunStatsPopup.Instance.Show();
        if (RunStatsPopupUITK.Instance != null) RunStatsPopupUITK.Instance.Show();
```

No `RunUI` change is needed (confirmed: `RunUI` does not reference `RunStatsPopup`; the button is a serialized
field on the popup component, re-pointed in the scene during Step 2).

- [ ] **Step 4: Verify it compiles**

`refresh_unity` (force, all) → `read_console` filter `RunStatsPopupUITK`, then `RunManager`.
Expected: no errors. (Old `RunStatsPopup.cs` still exists until Task 8; both compiling is fine.)

- [ ] **Step 5: Commit**

```bash
git add Assets/Scripts/UI/RunStatsPopupUITK.cs Assets/Scripts/UI/RunStatsPopupUITK.cs.meta Assets/Scripts/RunManager.cs Assets/Scenes/SampleScene.unity
git commit -m "feat(ui): RunStatsPopupUITK controller + scene wiring; switch callers"
```

---

## Task 5: Welcome-back modal — outcome variants in UXML/USS

Extend the existing modal to: a hero (variant-colored), a breakdown container the ledger builder fills, a
sub-action (above) + main CTA (bottom). Keep the existing loading bar + cow/research sections for the
no-active-run case.

**Files:**
- Modify: `Assets/UI/OfflineProgressModalUITK/OfflineProgressModalUITK.uxml`
- Modify: `Assets/UI/OfflineProgressModalUITK/OfflineProgressModalUITK.uss`

- [ ] **Step 1: Add hero + breakdown + CTA block to the UXML**

Insert after the `time-away` label and before `loading-row`:

```xml
            <ui:VisualElement name="outcome-hero" class="hero" style="display:none;">
                <ui:Label name="hero-label" text="" class="hero__label" />
                <ui:Label name="hero-headline" text="" class="hero__score" />
                <ui:Label name="hero-sub" text="" class="hero__real" />
            </ui:VisualElement>
            <ui:ScrollView name="breakdown-scroll" class="ledger-scroll" style="display:none;">
                <ui:VisualElement name="breakdown" class="ledger" />
            </ui:ScrollView>
```

Then wrap the EXISTING legacy content — the `loading-row`, the Currency `section-title`+`section`, the
Research `section-title`+`section`, and `boost-summary` — in a single named container so the outcome variants
can hide it with one toggle. Add this opening tag immediately before `loading-row` and a closing `</ui:VisualElement>`
immediately after `boost-summary`:

```xml
            <ui:VisualElement name="legacy-sections">
                <!-- existing loading-row, Currency section, Research section, boost-summary stay here -->
            </ui:VisualElement>
```

Replace the single `continue-button` with a CTA stack (sub-action above, main CTA below):

```xml
            <ui:VisualElement name="cta-stack" class="cta-stack">
                <ui:Button name="secondary-button" text="" class="cta cta--ghost" style="display:none;" />
                <ui:Button name="continue-button" text="Continue" class="cta cta--primary" />
            </ui:VisualElement>
```

- [ ] **Step 2: Add the matching USS** (append; reuse `.hero*`/`.ledger*`/`.stat-row*`/`.cta*` from Task 3's
  USS by copying those rules into this file, plus the variant colors)

```css
/* hero variants */
.hero--green { background-color: rgb(38,51,28); border-color: rgb(70,97,46); }
.hero--green .hero__label { color: rgb(155,216,78); }
.hero--green .hero__score { font-size: 24px; }
.hero--red { background-color: rgb(58,32,28); border-color: rgb(110,49,40); }
.hero--red .hero__label { color: rgb(255,156,138); }
.hero--red .hero__score { font-size: 22px; }
.cta-stack { flex-direction: column; margin-top: 10px; }
.cta { padding-top: 13px; padding-bottom: 13px; font-size: 22px; -unity-font-style: bold; -unity-text-align: middle-center; border-radius: 12px; border-width: 2px; margin-top: 6px; }
.cta--primary { background-color: rgb(90,138,48); color: rgb(255,255,255); border-color: rgb(120,180,90); }
.cta--gold { background-color: rgb(107,90,30); color: rgb(255,255,255); border-color: rgb(150,124,40); }
.cta--ghost { background-color: rgba(0,0,0,0); color: rgb(168,152,120); font-size: 18px; border-color: rgba(0,0,0,0); }
.hero { border-radius: 14px; padding: 14px; margin-bottom: 12px; align-items: center; border-width: 1px; }
.hero__label { font-size: 16px; -unity-font-style: bold; letter-spacing: 1px; }
.hero__score { font-size: 24px; -unity-font-style: bold; color: rgb(255,255,255); }
.hero__real { font-size: 15px; color: rgb(150,150,150); margin-top: 2px; }
.ledger-scroll { max-height: 320px; margin-bottom: 8px; }
.ledger { flex-direction: column; }
.stat-row--total { border-top-width: 1px; border-top-color: rgba(255,240,210,0.18); padding-top: 8px; }
.stat-row--coin { background-color: rgb(51,41,15); border-radius: 8px; padding-left: 8px; padding-right: 8px; }
.stat-row--dim { opacity: 0.6; }
.crop-row__left { flex-direction: row; align-items: center; }
.crop-row__icon { width: 26px; height: 26px; margin-right: 8px; -unity-background-scale-mode: scale-to-fit; }
```

> The existing `continue-button` USS rule stays; the new `.cta--primary` is an alias-styled equivalent. Keep
> both class names on the button (`class="cta cta--primary continue-button"`) if you want the old hover rule,
> or drop `continue-button` from USS. Pick one in Step 1's markup; don't leave both styling the same property.

- [ ] **Step 3: Verify import**

`refresh_unity` (force, assets) → `read_console`. Expected: no import errors.

- [ ] **Step 4: Commit**

```bash
git add Assets/UI/OfflineProgressModalUITK/
git commit -m "feat(ui): welcome-back modal hero variants + breakdown + CTA stack"
```

---

## Task 6: `OfflineProgressModalUITK` — `OpenContinue` / `OpenEnded`

Add two outcome-specific open APIs that fill the hero, build the breakdown via the shared view, and set the
CTAs. Keep the legacy `Open(gap, cowCompost, report)` for the no-active-run case.

**Files:**
- Modify: `Assets/Scripts/UI/OfflineProgressModalUITK.cs`

- [ ] **Step 1: Cache the new elements**

In `Cache()` add:

```csharp
        outcomeHero   = root.Q<VisualElement>("outcome-hero");
        heroLabel     = root.Q<Label>("hero-label");
        heroHeadline  = root.Q<Label>("hero-headline");
        heroSub       = root.Q<Label>("hero-sub");
        breakdownScroll = root.Q<ScrollView>("breakdown-scroll");
        breakdown     = root.Q<VisualElement>("breakdown");
        secondaryButton = root.Q<Button>("secondary-button");
        if (secondaryButton != null) secondaryButton.RegisterCallback<ClickEvent>(_ => OnSecondary());
```

Add the fields + an `Action onSecondary` near the other fields:

```csharp
    private VisualElement outcomeHero, breakdown;
    private ScrollView breakdownScroll;
    private Label heroLabel, heroHeadline, heroSub;
    private Button secondaryButton;
    private System.Action onSecondary;
    private void OnSecondary() { onSecondary?.Invoke(); }
```

- [ ] **Step 2: Add the two outcome APIs**

```csharp
    /// <summary>Run survived offline — green hero, light summary, "Continue the Run" CTA.</summary>
    public void OpenContinue(System.TimeSpan gap, RunLedgerData d, string farmAdvancedHms, string nowHms, System.Action onContinue)
    {
        if (root == null) Cache();
        if (modalRoot == null) return;
        PrepCommon(gap);
        ShowHero("green", "Your run is still going",
            "Farm Time +" + farmAdvancedHms, "now " + nowHms + " · ran at max speed while away");
        RunStatsLedgerView.Build(breakdown, d, compact: true);
        breakdownScroll.style.display = DisplayStyle.Flex;

        SetCtas(mainText: "▶  Continue the Run", mainGold: false,
                secondaryText: "See full breakdown",
                onMain: onContinue,
                onSecondaryAction: () => { Close(); RunStatsPopupUITK.Instance?.Show(d); });
        Reveal();
    }

    /// <summary>Run ended offline — red hero, breakdown, "View Full Run Stats" CTA.</summary>
    public void OpenEnded(System.TimeSpan gap, RunLedgerData d, System.Action onNewRun)
    {
        if (root == null) Cache();
        if (modalRoot == null) return;
        PrepCommon(gap);
        ShowHero("red", "💸 Your run ended while away",
            "Bankrupt at " + d.farmTimeHms, "ran out of seed money · final score " + d.farmTimeHms);
        RunStatsLedgerView.Build(breakdown, d, compact: true);
        breakdownScroll.style.display = DisplayStyle.Flex;

        SetCtas(mainText: "📊  View Full Run Stats", mainGold: true,
                secondaryText: "Start a new run",
                onMain: () => { Close(); RunStatsPopupUITK.Instance?.Show(d); },
                onSecondaryAction: () => { Close(); onNewRun?.Invoke(); });
        Reveal();
    }

    private void PrepCommon(System.TimeSpan gap)
    {
        if (timeAwayLabel != null) timeAwayLabel.text = $"You were away for {FormatGap(gap)}";
        // hide the legacy currency/research/loading sections for the outcome variants
        SetLegacySectionsVisible(false);
    }

    private void ShowHero(string variant, string label, string headline, string sub)
    {
        if (outcomeHero == null) return;
        outcomeHero.style.display = DisplayStyle.Flex;
        outcomeHero.RemoveFromClassList("hero--green");
        outcomeHero.RemoveFromClassList("hero--red");
        outcomeHero.AddToClassList(variant == "red" ? "hero--red" : "hero--green");
        heroLabel.text = label; heroHeadline.text = headline; heroSub.text = sub;
    }

    private void SetCtas(string mainText, bool mainGold, string secondaryText,
                         System.Action onMain, System.Action onSecondaryAction)
    {
        continueButton.text = mainText;
        continueButton.RemoveFromClassList("cta--gold");
        continueButton.AddToClassList(mainGold ? "cta--gold" : "cta--primary");
        continueButton.SetEnabled(true);
        // rebind the main button (clear previous handler by replacing the callback target)
        mainAction = onMain;
        secondaryButton.text = secondaryText;
        secondaryButton.style.display = DisplayStyle.Flex;
        onSecondary = onSecondaryAction;
    }

    private void Reveal()
    {
        if (root != null) root.pickingMode = PickingMode.Position;
        modalRoot.style.display = DisplayStyle.Flex;
    }
```

Add a `mainAction` field and route the existing continue-button click through it:

```csharp
    private System.Action mainAction;
    // In Cache(), where the continue button is wired, replace the close handler with:
    //   continueButton.RegisterCallback<ClickEvent>(_ => { if (continueButton.enabledSelf) (mainAction ?? (System.Action)Close).Invoke(); });
```

Cache the `legacy-sections` container (Task 5 wraps the cow/research/loading content in it) and add the exact
toggle helper. In `Cache()` add `legacySections = root.Q<VisualElement>("legacy-sections");`, add the field
`private VisualElement legacySections;`, and add:

```csharp
    private void SetLegacySectionsVisible(bool visible)
    {
        if (legacySections != null) legacySections.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
    }
```

The legacy `Open(gap, cowCompost, report)` path must call `SetLegacySectionsVisible(true)` and hide the new
outcome chrome (`outcomeHero.style.display = None; breakdownScroll.style.display = None; secondaryButton.style.display = None;`).
The two outcome APIs call `SetLegacySectionsVisible(false)` (done inside `PrepCommon`).

> Keep the existing load-animation `Open(gap, cowCompost, report)` working for the **no-active-run** path
> (called by `OfflineProgressManager` when `!pendingRunActive`). The outcome APIs skip the load animation
> (the numbers are already final); if you want the count-up flourish, it can be added later.

- [ ] **Step 3: Verify it compiles**

`refresh_unity` (force, all) → `read_console` filter `OfflineProgressModalUITK`. Expected: no errors.

- [ ] **Step 4: Commit**

```bash
git add Assets/Scripts/UI/OfflineProgressModalUITK.cs
git commit -m "feat(ui): welcome-back OpenContinue/OpenEnded outcome variants"
```

---

## Task 7: Rewire `OfflineProgressManager` to the new surfaces

Replace the Plan-2 placeholder calls (existing `Open(...)` / `RunStatsPopup.Show()`) with the new outcome
APIs, passing a `RunLedgerData` built from the outcome.

**Files:**
- Modify: `Assets/Scripts/OfflineProgressManager.cs`

- [ ] **Step 1: Build ledger data and call the variant APIs**

In `ShowWithGap`, in the **bankrupt** branch, after `FinalizeOfflineBankruptcy(...)`, replace
`RunStatsPopup.Instance.Show()` with:

```csharp
            var ledger = RunLedgerData.FromOffline(outcome, gap);
            if (OfflineProgressModalUITK.Instance != null)
                OfflineProgressModalUITK.Instance.OpenEnded(gap, ledger,
                    onNewRun: () => SeedSelectionPopup.Instance?.Show());
```

In the **survived** branch, replace the existing `OfflineProgressModalUITK.Instance.Open(...)` with:

```csharp
            var ledger = RunLedgerData.FromOffline(outcome, gap);
            string farmAdvanced = TimeFormat.Hms(outcome.result.finalFarmSeconds - pendingRunFarmSeconds);
            string nowHms = TimeFormat.Hms(outcome.result.finalFarmSeconds);
            if (OfflineProgressModalUITK.Instance != null)
                OfflineProgressModalUITK.Instance.OpenContinue(gap, ledger, farmAdvanced, nowHms,
                    onContinue: () => OfflineProgressModalUITK.Instance.CloseFromButton());
```

Add a tiny public `CloseFromButton()` to the modal that just calls its private `Close()` (so the manager can
pass a close action without exposing internals), OR pass `null` and let the main button default to `Close`
(the `mainAction ?? Close` fallback in Task 6 already handles null — prefer passing `null`):

```csharp
                OfflineProgressModalUITK.Instance.OpenContinue(gap, ledger, farmAdvanced, nowHms, onContinue: null);
```

> Using `onContinue: null` relies on the Task-6 `mainAction ?? Close` fallback, so no extra public method is
> needed. The no-active-run path keeps calling the legacy `Open(gap, cowCompost, researchReport)`.

- [ ] **Step 2: Verify it compiles**

`refresh_unity` (force, all) → `read_console` filter `OfflineProgressManager`. Expected: no errors.

- [ ] **Step 3: Commit**

```bash
git add Assets/Scripts/OfflineProgressManager.cs
git commit -m "feat(offline): route reopen outcomes to redesigned welcome-back modals"
```

---

## Task 8: Remove the TMP Run Stats popup + full play-mode smoke test

**Files:**
- Delete: `Assets/Scripts/RunStatsPopup.cs` (+ `.meta`)
- Scene: remove the old TMP RunStatsPopup GameObject (or repurpose its uGUI button onto the new controller).

- [ ] **Step 1: Confirm no remaining references to the old class**

Search the project for `RunStatsPopup` (without `UITK`). Expected: only the file being deleted. If `RunUI` or
others still reference it, repoint to `RunStatsPopupUITK` first (Task 4 Step 3 should have covered them).

- [ ] **Step 2: Delete the old script + scene object**

Delete `Assets/Scripts/RunStatsPopup.cs`. In the scene, delete the old popup GameObject (its canvas/TMP
hierarchy). Keep the uGUI "Run Stats" button if it's now wired to `RunStatsPopupUITK.prevRunStatsButton`.

- [ ] **Step 3: Compile clean**

`refresh_unity` (force, all) → `read_console`. Expected: no errors, no "RunStatsPopup not found" references.

- [ ] **Step 4: Play-mode visual smoke test (all three surfaces)**

Set `Application.runInBackground = true`. Then:
1. **Live end-of-run:** start a run, force bankruptcy (or use a dev end-run), confirm the **RunStatsPopupUITK**
   shows the hero (`Farm Time · Score` in `h m s`), bankruptcy banner, itemized harvested crops with icons,
   per-cause losses, defense section, Close works. Screenshot via `manage_editor`/MCP capture.
2. **Offline survived:** with an affordable active run, click the Plan-2 **Force Offline Sim (2h)** dev button;
   confirm the **green Continue** modal: "Farm Time +Δ", money-now/coins/compost, compact harvested+losses,
   "Continue the Run" bottom + "See full breakdown" above (which opens the full stats).
3. **Offline ended:** force bankruptcy offline (e.g. set money low before the dev button); confirm the **red
   Run-ended** modal and that "View Full Run Stats" opens the populated `RunStatsPopupUITK`.
Confirm no exceptions in `read_console`. Stop play mode when done.

- [ ] **Step 5: Commit**

```bash
git add -A
git commit -m "chore(ui): remove TMP RunStatsPopup; UITK ledger surfaces verified"
```

---

## Done criteria for Plan 3

- [ ] All three surfaces render the ledger from `RunLedgerData` with `h m s` time, `Farm Time`/`Real time
      played` labels, itemized harvested crops (sprites), per-cause losses, money-before-coins, CTA-on-bottom.
- [ ] Reopen flow shows the correct variant (green Continue / red Run-ended) and the ended path's CTA opens the
      full stats.
- [ ] Old TMP `RunStatsPopup` is gone; no dangling references; clean compile.
- [ ] Full EditMode suite still green (Plans 1–2 unaffected: 43 tests).
- [ ] No exceptions in the play-mode smoke test.

## Notes / scope

- Defense (repelled) counts show only for live runs (`hasDefense`); offline hides that section by design.
- Styling values here are first-pass; expect to tune paddings/sizes in-editor during the smoke test.
- The offline outcome variants skip the count-up load animation (final numbers shown immediately); the
  animation can be re-added later if desired.
- `RunStatsLedgerView` and the welcome-back USS intentionally share class names (`stat-row`, `section-title`,
  `hero*`, `cta*`); each UIDocument scopes its own queries, so the duplicated rules don't collide at runtime.
