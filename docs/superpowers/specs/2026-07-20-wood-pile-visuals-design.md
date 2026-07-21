# Wood Pile Visuals & Wood Cap — Design

**Date:** 2026-07-20
**Status:** Approved by user in brainstorm; ready for implementation planning

## 1. Overview

Wood becomes a capped resource (1000, tunable), and that fill level is represented visually in the world as 4 growing log piles near the Wood Rack — the piles ARE the at-a-glance readout the user asked for, no numeric HUD needed there. Two small UI additions round it out: an exact `X / 1,000` count in the Inventory popup, and a "Full" warning state in the Wood Rack popup.

## 2. Cap mechanics

- `CurrencyManager` gains `[SerializeField] private int maxWood = 1000;` and `public int MaxWood => maxWood;`.
- `AddWood(int amount)` clamps `currentWood` to `maxWood` — a deposit that would overflow is truncated, never silently dropped in a way that misleads (e.g. wood at 990 + a 15-wood swing deposits only 10, landing exactly at 1000).
- **Hard cap on chopping:** in `TreeNode`'s swing handler, alongside the existing axe-level gate (`WoodcuttingMath.CanFell` check), add an early check: if `CurrencyManager.Instance.Wood >= CurrencyManager.Instance.MaxWood`, the swing is blocked entirely — no hit registers, no tree damage, no `Shake()` — and `wm.ShowHint(transform.position, "Wood storage full!")` fires, reusing the exact hint mechanism the axe-level gate already uses. This keeps every swing meaningful right up to the last log, then cleanly refuses instead of wasting hits against a tree for nothing.
- This clamp-on-deposit + gate-on-swing combination means: swings that would only partially overflow still happily land and fill you to exactly the cap; only a swing attempted while *already* at the cap is refused.

## 3. LogStackVisual (world piles)

One reusable component (`Assets/Scripts/Woodcutting/LogStackVisual.cs`), added to 4 world GameObjects placed near the Wood Rack (which sits at world position (31, -41) in `FarmMain.unity`).

- Each instance is configured with `[SerializeField] private int pileIndex;` (0–3, inspector-set per GameObject).
- Per-pile capacity is computed, not hardcoded: `CurrencyManager.Instance.MaxWood / 4` (250 today; stays correct if the cap tuning changes later — one source of truth).
- Each pile's fill: `Mathf.Clamp(totalWood - pileIndex * capacityPerPile, 0, capacityPerPile)`, expressed as a 0–1 fraction of that pile's own capacity.
- Sprite selection by fraction `f` (`Assets/Sprites/Buildings/Wood/`, all four already exist as assets), boundaries fully explicit:
  - `f == 0`: `SpriteRenderer.enabled = false` (pile hasn't started — nothing to show, nothing to click)
  - `0 < f < 0.30`: `Trunk_Big_Vertical_32x32`
  - `0.30 <= f < 0.60`: `Trunk_Load_Vertical_32x32`
  - `0.60 <= f < 1.0`: `Trunk_Load_Medium_Vertical_32x32`
  - `f >= 1.0`: `Trunk_Load_Big_Vertical_32x32`
- Subscribes to `CurrencyManager.OnWoodChanged`; recomputes and swaps sprite on every change. Piles fill strictly in order — pile 1 shows nothing until pile 0 is at its own 250 cap.
- **Interaction:** clonable from `WoodRack.cs`'s press/release tap-feedback pattern (scale-down on press, release tween, `UITapBlocker.PointerOverUI` guard) — clicking an active (non-hidden) pile opens `WoodRackPopupUITK.Instance.Open()`, same destination as tapping the Rack itself. A hidden (0-fill) pile has no collider interaction — nothing to tap.

## 4. UI additions

- **Inventory popup (`InventoryPopupUITK`, Phase 1's mega-list):** the existing Wood sell row's value label changes from `cm.Wood.ToString("N0")` to `$"{cm.Wood:N0} / {cm.MaxWood:N0}"`. No other change — same sell control, same in-run-only enable logic.
- **Wood Rack popup (`WoodRackPopupUITK`):** the existing `woodCount` label (currently `$"{wood}"`) gets a "Full" state when `wood >= cm.MaxWood`: text becomes `$"{wood} (Full)"` and a new USS class `.wood-count--full` (warning red/orange, distinct from the current brown `.wood-count` color) is toggled on. Clears automatically the next time wood drops below the cap (driven by the same refresh that already runs off `OnWoodChanged`).

## 5. Scope notes (deliberate)

- No numeric cap readout on the Wood Rack popup itself beyond the new "Full" state — the user explicitly said not needed there.
- Pile world placement is approximate/placeholder (near the existing Wood Rack at (31,-41), spaced in a row) — exact offsets tuned visually during implementation via screenshot check, same as the Barn prop's placement in Phase 3.
- No change to `WoodcuttingMath` or any pure economy math — this is presentation + a clamp, not a rebalance.
