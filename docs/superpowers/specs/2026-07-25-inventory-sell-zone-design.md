# Inventory Sell Zone — Design

**Date:** 2026-07-25
**Branch:** `feat/run-ender-economy`
**Touches:** `Assets/Scripts/UI/InventoryPopupUITK.cs`, `Assets/UI/InventoryPopupUITK/{uxml,uss}`, `Assets/Scripts/Cannery/CanneryManager.cs`, `Assets/Scripts/EconomyCore/InventoryMath.cs`, `Assets/Tests/EditMode/InventoryMathTests.cs`

## Problem

The Inventory popup is too small (caps at 680px wide with a 640px list), shows a
generic Coin icon for every Harvest row instead of the crop's own art, and can only
sell in one quantity. A half-built inline slider panel exists
(`InventoryPopupUITK.cs:250-288`) but is effectively invisible: tapping Sell expands
it, and any currency event immediately rebuilds the list and collapses it again.

## Approach

A **staging zone** pinned above the list. Tapping an item row loads it into the zone;
the zone carries `x1 / x10 / All` sticky mode buttons and a single confirm button —
the same interaction as the Wood Rack.

Rejected alternative: a per-row slider (Chad's Galactic Mining Empire style). A
horizontal slider inside a vertically-scrolling `ScrollView` fights the scroller for
the drag gesture on touch, and it forces every row taller, lengthening the list this
change is meant to tighten. The staging zone keeps all fiddly input in a fixed,
non-scrolling region and reuses `WoodcuttingMath.StackMode` verbatim.

Everything in scope already pays **Coins** ("Gold" in UI copy). No economy work is
required — this is UI plus two small accessors.

## 1. Chrome and layout

| Element | Now | After |
| --- | --- | --- |
| `.popup-container` width | `82%`, `max-width: 680px` | `92%`, `max-width: 900px` |
| `.inv-list` height | `max-height: 640px` | `max-height: 900px`, `flex-grow: 1` |
| Sell zone | — | new, ~340px, fixed |

Header becomes three-part: **title** (left) · **currency strip** · **close** (right).
The strip is Coin / Gem / Cash as icon+value pairs, replacing the deleted
`Currencies` list section — ~60px instead of the ~200px three rows consumed. Neither
Coins, Gems nor Cash is sellable, and the top bar already displays all three, so they
have no business occupying list rows.

## 2. Sell zone

### Empty state (the state the popup opens in)

Ghosted `Cute/RpgThings/Crate_OpenEmpty.png` at 40% opacity, label
`Tap an item to sell it`, with `[x1][x10][All]` and the confirm button **rendered but
disabled**. Disabled-and-visible is deliberate: the controls are what teach the
interaction, and an empty box teaches nothing.

### Staged state

- 96px item icon
- `<Name> · <n> held`
- `[x1][x10][All]` sticky toggle; the active one carries `--active`
- Green confirm: `Sell for Gold +<payout>`

### Behavior

- Tapping any sellable row stages it. Tapping a different row swaps the staged item.
  Re-tapping the staged row is a no-op (no unstage — there is nothing to protect and
  an accidental clear is pure annoyance).
- `stackMode` **persists across staging swaps**, so several crops can be sold at `All`
  without re-selecting the mode. It **resets to `x1` on Close**, so the panel never
  reopens already armed on `All`.
- Confirm sells `WoodcuttingMath.ResolveStackAmount(stackMode, held)`.
- Confirm is disabled whenever the resolved amount is 0.
- After a sale, if `held` reaches 0 the zone reverts to Empty and the row leaves the
  list.

## 3. Rows

Per-row `Sell` buttons are **removed** — the row itself is the tap target. This
reclaims the horizontal space the buttons ate and deletes the unreachable inline
slider panel. The staged row carries `.inv-row--staged` (brighter fill, green left
border) so it is obvious which item the zone is acting on.

Sections: **Resources** / **Harvest** / **Pantry**.

| Row | Icon source | Payout | Sell call |
| --- | --- | --- | --- |
| Wood | `RpgResources/Log.png` (unchanged) | `GoldPricePerWood` × n | `SpendWood(n)` → `AddCoins` |
| Compost | `Icons_Essential/Compost.png` (unchanged) | — | read-only, no tap affordance |
| Crops ×9 | **`CropData.cropSprite`** (runtime `Sprite`) | `RawCoinValue(crop)` × n | `TrySpendCrop` → `AddCoins` |
| Eggs | `Cute/Food/Egg.png` | `EggSellCoins` × n | `TrySpendEggs` → `AddCoins` |
| Raw fish ×3 | `Cute/Fishing/Fish_Perch \| Fish_BassGreen \| Fish_GreenPike` | `SmokehouseManager.RawValue(tier)` × n | `TrySellRaw(tier)` ×n |
| Smoked fish ×3 | same sprites + amber `-unity-background-image-tint-color` | `SmokedValue(tier)` × n | `TrySellSmoked(tier)` ×n |
| Jars | `RpgThings/Jug_Orange.png` (placeholder) | sum of first n jar values | `TrySellJar(0)` ×n |

`cropSprite` is already populated on all nine `Crop_*.asset` files. The current
generic coin icon is only `AddSellRow` hardcoding `"inv-icon--coins"`.

Smoked fish reuse the raw sprite under an amber tint rather than requiring new art;
the pack has no smoked-fish icon. `Compost` loses its permanently-greyed Sell button,
which currently reads as broken rather than as intentional.

## 4. Code shape

A single entry type makes the zone agnostic to what it is selling:

```csharp
private sealed class SellEntry
{
    public string Id;              // "crop:Carrot", "eggs", "wood", "fish:raw:1", "jars"
    public string Name;
    public Sprite Sprite;          // runtime sprite (crops); null otherwise
    public string IconClass;       // USS class (wood, eggs, fish, jars); null for crops
    public int Held;
    public Func<int, int> Payout;  // Gold for selling n
    public Action<int> Sell;       // sell n
}
```

`BuildRows()` populates `Dictionary<string, SellEntry>` and emits the rows.
`RenderSellZone()` looks up `stagedId` in that dictionary and redraws the zone.

**The sell zone is declared in UXML, outside the `ScrollView`.** `BuildRows()` calls
`list.Clear()` on every currency event; a zone built inside the list would
re-register its button callbacks on every coin tick. This is the exact failure mode
recorded in the research-boost double-menu fix — `Clear()` removes children but an
element keeps its own callbacks, so rebuilt controls accumulate handlers. Zone
callbacks register once, in `WireCallbacks()`.

### New API

- `CanneryManager.JarValueAt(int index)` — read accessor. Jar values are
  heterogeneous (`state.readyJars[i].value`), so the payout must sum rather than
  multiply. Only `ReadyJarCount` is exposed today.
- `InventoryMath.JarStackValue(IReadOnlyList<int> values, int n)` — pure prefix sum.

Fish sell one unit per call (`TrySellRaw(tier)` returns `bool`), so `Sell` loops n
times and breaks early on `false`.

`WoodRackPopupUITK.GoldPricePerWood` is read defensively (`Instance != null`), exactly
as the existing code reads `CashPricePerWood`. If it resolves to 0 the Wood row is
not stageable.

## 5. Testing

New `InventoryMathTests` cases for `JarStackValue`:

- `n = 0` → 0
- `n` greater than `values.Count` → total of all values
- negative `n` → 0
- empty list → 0
- normal case, mixed values → correct prefix sum

All other sell math routes through `WoodcuttingMath.ResolveStackAmount` and
`SellValue`, already covered by `WoodcuttingMathTests`.

Manual verification: play mode, screenshot the popup in Empty and Staged states, sell
a partial stack and a full stack, confirm the row disappears at 0 and the zone reverts.

## Out of scope / flagged

- **Jar sell order** is list order (oldest first). The payout preview sums those same
  jars, so preview and result cannot disagree.
- **`GoldPricePerWood` lives on `WoodRackPopupUITK`** — economy data on a UI
  component. Read as-is here; relocating it is a separate change.
- **`Jug_Orange.png`** is the closest existing sprite to a preserve jar. Swappable
  when real jar art arrives.
- Wood's **Cash** sale stays exclusive to the Wood Rack (in-run only). This panel is
  Gold-only.
