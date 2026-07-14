# Per-Zone Run Stats with Currency Iconography — Design

**Date:** 2026-07-14
**Status:** Approved by user (per-zone cards confirmed via Q&A)
**Branch:** `feat/run-ender-economy`

## Goal

Every run-stats surface — Current Run popup, Prev. Run Stats popup, and both welcome-back
variants (run survived / run failed while away) — shows the same itemized, icon-rich ledger:

1. **Economy** section using the real currency icon sprites (cash / coins / compost, gems when
   needed) instead of "$" and text tags.
2. **Fields**: a 2x2 grid of per-ZONE cards mirroring the farm layout. Each card shows that
   zone's crop (icon + name), harvested count, what it was worth (money + coins, with icons),
   its five loss causes (deer / crows / lightning / dried / rotted), and — for gear equipped on
   that zone — Fence: deer repelled, Scarecrow: crows repelled, Sprinkler: plants watered.
3. **Animals** section: dog (deer chased off) and cow (compost gained, plants eaten), with small
   animal sprites sized like the crop icons.
4. Clearer vertical separation between sections.

Non-goal: redesigning the modal chrome (headers, hero score, banners stay as-is).

## Why this is cheap

All three surfaces already render through one shared pair: `RunLedgerData` (render-ready data,
built from live `RunStats` or the offline sim) + `RunStatsLedgerView.Build(container, data,
compact)`. The offline simulator already iterates per-zone with per-zone loss accumulators — it
just sums them into totals at the end. All live recording funnels are single-site.

## Data model

### RunStats (live)

New nested class, one instance per zone (indexed by FarmGrid `ZoneID`):

```csharp
public class ZoneStats {
    public CropData crop;            // set on first plant/harvest in the zone this run
    public int harvested;
    public int moneyEarned;          // harvest cash from this zone
    public int coinsBanked;          // harvest coins from this zone
    public int eatenByDeer, eatenByCrows, struckByLightning, driedUp, rotted;
    public int deerRepelledByFence, crowsRepelledByScarecrow, wateredBySprinkler;
    public bool hasFence, hasScarecrow, hasSprinkler;  // gear equipped this run
}
```

`RunStats` keeps `Dictionary<int, ZoneStats>` (or fixed array) plus NEW aggregate animal
counters: `DeerChasedByDog`, `PlantsEatenByCow`, `CompostFromCow`. All existing aggregate
counters stay — quests and other consumers read them; per-zone is additive.

### Recording sites (all verified single-site)

| Stat | Site |
|---|---|
| 5 death causes per zone | `Plant.Die(cause)` — Plant.cs ~453; plant knows `parentTile.ZoneID` + its CropData |
| harvested / money / coins per zone | `Plant.Harvest` — Plant.cs ~301 already computes `zone` for multipliers |
| fence deer repel per zone | EquipmentManager.cs ~528 (zone-keyed state) |
| scarecrow crow repel per zone | EquipmentManager.cs ~453 + ~480 (zone-keyed state) |
| sprinkler waterings per zone | `EquipmentManager.WaterPlantsInRange(state)` — count each plant actually watered |
| gear-equipped flags | set when run starts / equipment placed for the run |
| deer chased by dog | FarmDog.cs ~204 `deer.ForceRepel()` after a successful chase |
| cow plants eaten + compost | `Cow.EatPlant` — Cow.cs ~135 (adds compost lump, destroys plant) |

Note: cow-eaten plants do NOT count in the zone loss lines (they're not a threat loss; they pay
compost). They appear only in the Animals section, as designed with the user.

### Offline simulator

`OfflineRunSimulator` already keeps per-zone accumulators (`accDeer[z]`, etc.) over
`ctx.zones[z]`. Change: the result (`OfflineRunResult`) additionally records, per zone index:
harvested, moneyEarned, coinsBanked, eatenByDeer, eatenByCrows, struckByLightning, driedUp,
rotted, plus the zone's crop id. Aggregate fields stay for compatibility. Defense, sprinkler,
dog, and cow are NOT simulated offline — those lines are simply absent from offline-built
ledgers (existing `hasDefense` pattern generalizes to per-line presence).

`RunStats.IngestOfflineResult` widens to also ingest the per-zone arrays so the post-offline
"Prev. Run Stats" popup matches the welcome-back modal exactly.

## RunLedgerData

Flat loss/defense fields are superseded by:

```csharp
public class LedgerZoneCard {
    public int zoneId;                 // grid position: 2x2 ordered to mirror the farm
    public Sprite cropSprite; public string cropName;
    public int harvested, moneyEarned, coinsBanked;
    public int eatenByDeer, eatenByCrows, struckByLightning, driedUp, rotted;
    public int? deerRepelled, crowsRepelled, wateredBySprinkler; // null = gear absent/not simulated → line hidden
}
public readonly List<LedgerZoneCard> zones;
// Animals (null/0 = row hidden):
public int deerChasedByDog; public bool hasDog;
public int compostFromCow, plantsEatenByCow; public bool hasCow;
public Sprite dogSprite, cowSprite;   // small icons from the animal data/registry
```

Builders: `FromCurrentRun`/`FromActiveRun` read `RunStats.ZoneStats`; `FromOffline` reads the
new per-zone arrays (equipment/animal fields null → hidden). Aggregate loss fields remain
derivable (sum of zones) for any legacy consumer, but the view stops using them.

## RunStatsLedgerView

- **Economy**: replace the `"$"` string and `"coin"` value-tag with icon elements using the
  shared currency classes (`.currency-icon--money/coins/compost/gems`, background sprites
  `Assets/Sprites/UI/Icons/Icons_Essential/Cash.png`, `Coin.png`, `Gem.png`,
  `Cyberpunk/Plants/Seedling_Dirt.png` — same as TopBarUITK.uss). Classes get copied into
  RunStatsPopup + OfflineProgressModal USS (USS is per-panel; that duplication already exists
  across four other panels). Economy also gains a `Total harvested` row (moves here from the
  removed Harvested section, so the headline number survives).
- **Fields section** (replaces Harvested + Losses): container with `flex-direction: row;
  flex-wrap: wrap`; each card ~48% width so two per row → 2x2. Cards ordered by zoneId to
  mirror the farm layout. Card content, top to bottom:
  - header: crop icon + crop name (+ subtle "Zone N")
  - `Harvested  N`
  - `Worth  [cash] X   [coin] Y`
  - loss lines (label + red count; zero-count lines render dimmed so the card shape is stable
    and scannable)
  - equipment lines only when non-null: `Fence — deer repelled N`, `Scarecrow — crows repelled
    N`, `Sprinkler — plants watered N`
  - A zone with no card data (locked / never planted) renders nothing.
- **Animals section** (after Fields): rows with a small (crop-icon-sized) dog/cow sprite:
  `Dog — deer chased off N`, `Cow — plants eaten N` and `Cow — compost gained [compost] N`.
  Section hidden when neither animal is present (always hidden offline).
- **Spacing**: increase `.section-title` top margin / section bottom margin in both USS files
  so sections separate clearly at a glance.
- `compact` mode (welcome-back "run survived" summary) keeps Economy + Fields cards but drops
  equipment/animal lines — same building blocks, fewer rows.

## Testing

EditMode tests:
- Simulator per-zone accumulation: totals == sum(zones); per-zone losses land on the right zone
  for asymmetric contexts (different crops/tile counts per zone).
- `RunLedgerData.FromOffline` maps per-zone arrays → cards, hides equipment/animal lines.
- `RunLedgerData` from a populated RunStats: zone cards, equipment lines present only when
  flagged, animal rows only when counts/flags set.

Play-mode verification: drive each of the four surfaces via the editor (ScreenCapture) as in
prior sessions.

## Risks / notes

- `RunStats` is a singleton with public counters used elsewhere — additive changes only.
- Offline sim zone list comes from `OfflineRunContextBuilder`; its zone order must be the
  FarmGrid zone id order for the cards to mirror the farm (verify in plan).
- The welcome-back modal (`OfflineProgressModalUITK`) also has bespoke "legacy sections" —
  they already delegate ledger rendering to `RunStatsLedgerView`; only USS additions needed.
- No emoji anywhere in UITK text (Android renders them invisible) — icons are sprite
  background VisualElements, never glyphs.
