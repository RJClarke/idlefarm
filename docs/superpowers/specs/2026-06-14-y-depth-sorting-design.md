# Y-Based Depth Sorting (2.5D) — Design

**Date:** 2026-06-14
**Branch:** feat/run-ender-economy (gameplay polish)
**Status:** BUILT (compiles clean, boots without errors; depth behavior pending playtest)

> **Build notes:** Done as one cohesive entity-band migration (not phased) — banding within a
> single layer means movers + statics + crops must move together or movers leap above
> un-migrated props. `YSort` gained an **auto foot-anchor from sprite bounds** (`autoFoot`,
> default on) so pivots don't matter. Static scene props are handled by **`YSortBootstrap`**
> (resolves roots `Environment` / `MarketBuildings` / `GreenhouseBuilding` by name at startup
> and adds a static YSort to every SpriteRenderer under them). Lightning bumped to 6000.
> Foot anchors may need per-type tuning after playtest.

## Goal

Entities (helper, animals, deer, crows, trees, buildings, equipment, crops) currently
each have a **fixed** `sortingOrder`, so e.g. the helper always draws over trees
regardless of position. Make render order depend on **world Y** — lower on screen draws
in front — so things occlude naturally as they move around the playspace.

Decisions made: **YSort component** approach (not camera transparency-axis); **crows are
Y-sorted like everything else** (no separate Air layer).

## Sorting model — bands within the single "Default" layer

The project has only the `Default` sorting layer. Rather than add layers (and re-tag every
object), we partition the `sortingOrder` integer into bands:

| Band | Range | Who |
|------|-------|-----|
| Ground | < 1000 | tiles, soil, moisture overlay, crop ground shadows (unchanged, already ~5) |
| **Entities (Y-sorted)** | ~1000–3000 | helper, animals, deer, crows, trees, buildings, equipment, grown crops |
| Weather / VFX | > 5000 | lightning, on-top effects (bumped up if currently below the band) |

Screen-space overlay canvases (rain overlay, floating text, UI) render after the camera
regardless, so they're unaffected.

### YSort component

```
sortingOrder = ENTITY_BASE - Mathf.RoundToInt((transform.position.y + footOffset) * PRECISION)
```
- `ENTITY_BASE = 2000`, `PRECISION = 10` → 0.1-unit resolution; entity orders land ~1000–3000
  for a ±100 Y range. Constants live in a shared `SortingBands` static.
- `footOffset` (serialized, per-object): shifts the sort anchor to the entity's **base/feet**
  so tall sprites (trees, buildings) occlude correctly. Default 0.
- **Movers** update in `LateUpdate`; **statics** (trees/buildings) compute once in `Start`
  (serialized `isStatic` flag) to avoid per-frame cost.
- Multi-renderer entities: YSort drives the renderers under it via
  `GetComponentsInChildren<SpriteRenderer>`, preserving each child's *relative* offset
  (so a dropped egg/gem keeps its existing order delta to the body).

## Phased rollout (verify each phase before the next)

1. **Component + main movers** — `YSort` + apply to UniversalHelper, AnimalVisual (animals),
   AnimalThreat (deer/crows), FarmDog. These are the obvious offenders. Replace their
   hardcoded `sortingOrder`s. Bump lightning above the entity band if needed.
2. **Static world objects** — trees, buildings, rocks (scene SpriteRenderers), ShopBuilding,
   ConstructionGate. Attach `YSort` with `isStatic` + a foot offset.
3. **Equipment + crops + drops** — ScarecrowVisual, SprinklerVisual, Plant/PlantVisuals,
   egg/gem drops. Fold into the entity band; tune foot offsets.

## Edge cases

- **Foot anchor:** centered pivots sort by center; `footOffset` compensates per type
  (negative offset moves the anchor down to the feet). Tuned visually per entity in play.
- **Drops (egg/gem):** keep their current relative delta to the animal body via the
  multi-renderer relative-offset handling, or their own YSort if unparented.
- **Big buildings:** large foot offset so an entity standing in front (lower Y) draws over.
- **Resolution/overflow:** `sortingOrder` is a 32-bit int; the band math stays well within range.

## Verification

In play, move the helper around a tree: below the tree it draws in front; above it, behind.
Animals/deer weave correctly past trees and each other. Crops/buildings occlude as expected.
No weather/UI regressions.

## Out of scope (v1)

Per-pixel/!overlap sorting, isometric tilemaps, dynamic shadow sorting.
