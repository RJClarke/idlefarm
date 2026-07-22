# Town Requests Board — Redesign + Authored Content

Date: 2026-07-21
Status: BUILT (218/218 EditMode tests pass; play-verified)
Branch: `feat/run-ender-economy`

## Problem

The Town Requests board read as generic/templated: flat cream cards, each with a coloured left
stripe (green/gold/red) for difficulty. Requests were procedurally rolled from abstract templates
("AnyUnlockedCrop, 30–60"), always a single item, with no character and no sense of who was asking
or why. The reward was shown but the player still had to parse a bare sentence to decide.

Reference the user liked: a contract board where each row has a portrait, a request sentence, and
stacked per-item progress bars, actionable without opening anything.

## Visual design — "pinned notes on a wood board"

- **Board**: `UI_Wood/UI_Wood_Frame_Standard_02` 9-sliced, cork-brown interior fallback.
  Title `TOWN REQUESTS` + close ✕.
- **Each request is a pinned paper note**: `UI_Book/UI_NoteBook_Frame01a` 9-sliced, cream.
  - Slight alternating tilt (±0.7°) with a coloured pin dot at top-centre.
  - **Difficulty is a stamped badge top-right** (EASY / MEDIUM / HARD, outlined, rotated −4°) —
    this *replaces* the coloured left stripe that made the old design feel templated.
  - **Left anchor**: the primary item's icon in a soft frame. Sized and placed so an NPC portrait
    can drop straight in later without relayout.
  - Requester name + blurb.
  - **One row per requested item**: icon, name, have/need progress bar, `held / need` count.
    Turns green when a line is satisfied.
  - **Footer**: `REWARD  +N Reputation` inline on the left; `Submit` (enabled only when every line
    is satisfied) and a quieter `Skip  N◆` on the right.
- **Cooldown state**: `COMPLETED` stamp + "Next request in mm:ss".

All art is optional — every sprite field falls back to a solid colour, so the board renders
correctly even with nothing wired.

## Content — authored request pool

Generation moved from procedural templates to a **hand-authored pool** so requests have character.
Content lives in code (`TownRequestContent.cs`), not the `RequestCatalog` asset, because the
existing asset would ignore new serialized fields and hand-editing `.asset` files is unsafe.

- **12 Easy** (1 item), **12 Medium** (2 items), **10 Hard** (2–3 items), **+2 premium outliers**.
- Cast: Marta (baker), Sal (tavern cook), Widow Bree (jam-maker), Old Finch (fisherman), Cormac
  (carpenter), Bram (blacksmith), Nella (innkeeper), Doc Hollis (apothecary), Rosie, Gus, Tam,
  Meg, Otis, Clara, Pip, Junie, Hettie, Fern.
- A request is only offered once **every** item in it is obtainable (wood→axe, raw fish→pole,
  smoked→smokehouse built, crops→in `CropDatabase`).
- Weighted pick; premium outliers carry weight 1 vs 10.

### Balance model

Every item is priced in **V** (gold-equivalent), derived from the game's own sell values:

- Crops: `harvestValue × 1.5` (Money), converted at **4 Money = 1 Gold** (wood sells for either
  4 Money or 1 Gold, which fixes the exchange rate).
- Wood 1, **Compost 1** (sells at a loss — its real value is research speed), Egg 25.
- Fish raw: Perch 100, Bass 400, Northern Pike 2000.
- Fish smoked: Smoked Perch 300, Smoked Bass 1400.

Band targets and achieved spread:

| Band | Target | Achieved |
|------|--------|----------|
| Easy | ~100V | 90–105V (avg 95) |
| Medium | ~300V | 268–302V (avg 289) |
| Hard | ~750V | 694–760V (avg 722) |

**Scarcity rule**: a scarce item appears only alone, beside a common filler, or with its own kind.
Tiers — 🟢 common: Wood, Carrot, Radish, Tomato, Corn, Green Beans, Compost, Smoked Perch;
🟡 moderate: Eggs, Strawberry, Blueberry, Peppers, Perch, Bass; 🔴 scarce: Northern Pike,
Smoked Bass.

**Premium outliers**: a single Northern Pike (2000V) or Smoked Bass (1400V) already exceeds the
Hard band and quantity cannot go below 1. Rather than distort them, they roll rarely (weight 1)
and pay proportionally via `rewardMultiplier` (2.7× and 1.9×).

## Code changes

| File | Change |
|------|--------|
| `Reputation/TownRequestContent.cs` | **New.** The authored pool + balance notes. |
| `Reputation/RequestCatalog.cs` | Added `AuthoredLine` / `AuthoredRequest` types. Template pools kept as fallback. |
| `Reputation/ReputationManager.cs` | `RollSlot` now tries the authored pool first (`TryRollAuthored`, weighted + unlock-filtered, multi-item, reward multiplier); old template path preserved as `RollFromTemplates`. Added `IsRequestAvailable` / `IsItemAvailable` / `IsCropUnlocked`. |
| `Reputation/DeliveryService.cs` | Added public `HeldCount(...)`; `CanFulfillLine` now delegates to it so the bar and the Submit button can never disagree. |
| `UI/TownRequestsPopupUITK.cs` | Rewritten: board + notes + stamps + multi-item bars + inline reward + Submit. `RefreshLive()` updates bars/counts/Submit state in place each frame instead of rebuilding. |

Multi-item fulfilment needed no change — `DeliveryService` already looped over all lines.

## Non-goals (v1)

- **NPC portraits** — deferred; the left anchor holds the primary item icon and reserves the spot.
- No new art; the bulletin-board feel is composed from existing wood frame + paper note sprites.

## Verification

- 218/218 EditMode tests pass.
- Play-verified: authored requests roll with blurbs, multi-item rows render (2- and 3-item Hard),
  bars fill and turn green at have≥need (Wood 300/300), Submit correctly disabled while short.
- Note: pre-existing saves keep their old single-item requests until those slots re-roll.
