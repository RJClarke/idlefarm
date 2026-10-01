# Toast Reskin: Parchment Top Banners + Bottom Processing Toasts

Date: 2026-07-29
Branch: `feat/run-ender-economy`

## Problem

Two complaints, one root cause — the toast system has no thematic grounding.

1. The Smokehouse's "N fish finished smoking" arrives as a **top** banner, but the bottom
   plank toast (used by fish catches) reads as more thematic for that kind of event.
2. The **top** banners are a flat dark card with a gold or green border — generic mobile-UI
   chrome that looks nothing like the rest of the game's pixel-art wood-and-parchment
   vocabulary. They are also taller than they need to be (118px for two short lines).

## Decisions

| Question | Decision |
| --- | --- |
| Top toast art | Parchment panel 9-sliced from `Main_tiles_58`'s two-tone bevelled panel |
| Top toast layout | Icon left; title/subtitle wrap — one line (88px) when short, two (~112px) when long |
| Bottom smoked-fish toast | Existing `UI_Wood_Frame_Horizontal_02` plank, same as fish catches |
| `Decorative_cracks` chipping | **Skipped.** See rationale below |
| Scope | Both Smokehouse toasts + both Cannery toasts move to the bottom; every top toast is reskinned |
| Research toast icon | Per-branch icons, curated for all 7 branches, shared with the research picker |

### Why the cracks are skipped

`Decorative_cracks.png` is a set of **tileable edge strips**, not decals. A wide toast
stretches the 9-slice's top/bottom edge regions roughly 12x horizontally, which turns crack
detail into horizontal smears. Cracks can only work baked into the four corners (the
unstretched regions), which means generating and maintaining a derived art asset for detail
that reads as noise at toast size. Not worth it now; revisit if the parchment shows up at
larger sizes (e.g. a full popup frame), where the corners are big enough to carry it.

### Why a dedicated PNG rather than referencing `Main_tiles_58`

`Main_tiles.png` is sliced into 75 sprites, and the rect named `Main_tiles_58` (196, 29,
71x80) contains the 48x41 panel **plus unrelated green tick marks** in the same rect. 9-slice
borders authored on that rect would be meaningless. The panel is therefore extracted to its
own single-sprite PNG.

## Architecture

### 1. Panel sprite

New asset: `Assets/Sprites/UI/UI_Craftpix/Panel_Parchment_9Slice.png` (48x41).

Import settings — the "make the texture high quality" fix. `Main_tiles.png` currently ships
with `filterMode: 1` (Bilinear), `textureCompression: 1` (Normal), `spriteMeshType: 1`
(Tight); all three are wrong for scaled-up 9-sliced pixel art. The new asset uses:

- `spriteMode: 1` (Single), `spriteMeshType: 0` (Full Rect — required for 9-slice)
- `filterMode: 0` (Point), `textureCompression: 0` (Uncompressed)
- `alphaIsTransparency: 1`, `mipmaps: 0`
- 9-slice border L/T/R/B = 5

At runtime `-unity-slice-scale: 3` gives a 15px visual border at the 1080 reference width.
Border regions scale by integer nearest sampling, so no PNG upscaling is needed. If UITK does
not honour the importer-authored border on a code-assigned `backgroundImage`, fall back to
explicit inline `unitySliceLeft/Right/Top/Bottom` — the pattern the existing catch-toast plank
already uses (`ToastManager.cs:210-215`).

### 2. Top toast layout

```
+-------------------------------------------------+
| [icon 52]  Research Complete   Compost Lv 3     |   88px
+-------------------------------------------------+

+---------------------------------------+
| [icon 52]  Research Complete          |
|            Compost Level 3            |   ~112px
+---------------------------------------+
```

Structure: `toast` (row) -> `icon` Image + `text` container. The text container is
`flex-direction: row; flex-wrap: wrap`, and the toast is `width: auto; max-width: 95%`. Short
content therefore sits on one line; when title + subtitle exceed the max width the subtitle
wraps to a second line. No length special-casing in code.

Colour: title walnut `#3E2A16` bold 34px, subtitle muted brown `#6E5634` 28px.

**The gold/green accent text is removed.** Gold on cream is illegible — the same finding is
already recorded in the catch toast's comment at `ToastManager.cs:238`. `ToastKind` is
retained in the API but its only remaining responsibility is selecting the fallback icon, so
toast *kind* is now communicated by the icon rather than by colour.

**Emoji are stripped from toast titles** (`ToastManager.cs:388`, `:395`,
`NewContentTracker.cs:209`). Emoji are invisible in UITK text on Android
(`project_offline_reopen_ux`), and the sprite icon now does that job correctly.

Stacking (max 3 + queue), the coroutine animation, and all timings are unchanged.

API: `Show(string title, string subtitle, ToastKind kind = Success, Sprite icon = null)`.
The new parameter is trailing and optional, so all existing call sites compile unchanged.

### 3. Research branch icons

`ResearchData` has no icon field, but it does have `branchID` with exactly 7 values:
`soil | helper | plant | animals | equipment | weather | meta`.

New `ResearchBranchIcons` ScriptableObject — a list of `{ branchID, Sprite }` — with the asset
at `Assets/Resources/Research/BranchIcons.asset`, resolved through `Resources.Load`. This
matches the existing `Resources.LoadAll<ResearchData>("Research")` convention
(`ResearchManager.cs:93`), so there is **no scene wiring**. A static
`ResearchBranchIcons.For(string branchID)` returns the sprite or null.

Two consumers share that one mapping so they cannot drift:

- `ToastManager.OnResearchLeveledUp` resolves `For(rd.branchID)`.
- `ResearchPopupUITK.BuildPicker` (`:583`) restructures `.picker-section` from
  `title + count` into `icon + (title, count)` column, with accompanying USS.

Icon curation: all 7 come from `Assets/Sprites/UI/Icons/Icons_Essential/`, a single coherent
16px set, which avoids the style clash of mixing packs. `soil` inherits `Compost.png` — already
this game's compost icon in three USS files (`CompostBoostModalUITK`, `InventoryPopupUITK`,
`OfflineProgressModalUITK`), so the branch matches existing UI for free.

| branch | icon | alternates considered |
| --- | --- | --- |
| `soil` | `Compost.png` | FlowerPot, Basket, Wheat |
| `helper` | `Team.png` | HelperNav, Backpack, Luggage |
| `plant` | `Wheat.png` | Flower, Flower2, FlowerPot |
| `animals` | `PetBowl.png` | CatHead, Cute/Food/Egg, Basket_04 |
| `equipment` | `Hammer.png` | Wrench, Wrench2, Gear |
| `weather` | `Cloud.png` | Sun, Lightbulb |
| `meta` | `Gear.png` | Book, Trophy, Lightbulb |

To swap one: reassign that row's `icon` on `Assets/Resources/Research/BranchIcons.asset`. Both
the picker and the toast follow, since they read the same table.

Note on scale: the picker renders icons at 32px and the toast at 48px. Six of the seven sources
are 16px, so 2x/3x integer scaling keeps them crisp; `Compost.png` is 32px and therefore lands
on 1x in the picker and a non-integer 1.5x in the toast. That was accepted in exchange for
matching the compost icon already used elsewhere in the game.

### 4. Bottom processing toasts

Four call sites move from `Show` to `ShowCatch`:

| Site | Message |
| --- | --- |
| `SmokehouseManager.cs:128` | "Smoked fish ready!" (live) |
| `SmokehouseManager.cs:342` | "N fish finished smoking while you were away!" (offline) |
| `CanneryManager.cs:114` | "Preserves ready!" (live) |
| `CanneryManager.cs:309` | "N jar(s) finished while you were away!" (offline) |

Each manager gains one serialized `Sprite readyIcon` field (2 scene fields total).
Smokehouse uses `Fish_Perch_Smoked.png` (already on disk, currently untracked); Cannery uses a
jar icon.

`ShowCatch` is fire-and-forget with no queue, unlike `Show`. That is acceptable here:
`bottomStack` is a column container, so a simultaneous Smokehouse + Cannery reopen stacks the
two toasts vertically instead of overlapping. No queue work required.

## Error handling

- Missing panel sprite -> the existing flat-dark-card fallback path is retained, so a failed
  import degrades rather than crashes.
- `ResearchBranchIcons.For` on an unknown or empty `branchID` returns null; a null icon simply
  omits the `Image` child, which the layout already tolerates.
- Missing `BranchIcons.asset` -> `For` returns null for everything; toasts and picker render
  text-only.

## Testing

**Correction to the original plan: the `ResearchBranchIcons.For` EditMode test is not
buildable.** `Assets/Tests/EditMode/IdleFarm.EditModeTests.asmdef` declares
`"references": ["IdleFarm.EconomyCore"]` with `overrideReferences: true` and
`autoReferenced: false`. Every existing test targets EconomyCore, and an asmdef assembly cannot
reference the predefined `Assembly-CSharp` where `ResearchBranchIcons` lives. Reaching it would
mean moving Unity-dependent UI code into EconomyCore purely to test a dictionary build with two
null guards — a bad trade. The class is therefore covered by play-mode observation, not a unit
test. Its test hooks (`SetAssetForTests`, `CreateForTests`) remain, so the test becomes trivial
to add if research code ever moves into an asmdef.

What was actually verified:

- **Compilation: passing.** `Assembly-CSharp` built with Unity's own Roslyn
  (`Editor/Data/DotNetSdkRoslyn/csc.dll`) against Unity's own response file
  (`Library/Bee/artifacts/1300b0aE.dag/Assembly-CSharp.rsp`, plus the new source file),
  exit 0, `error CS` count 0, and no new warning from any of the six changed files.
- **The 9-slice source: verified by pixel inspection.** Fill begins at exactly x/y = 4, every
  border band is a solid run along its stretch axis, and the centre is one flat colour
  (`#E5D6A1`), so a border of 5 stretches without artifacts in either direction.
- **Scene references: verified structurally.** All six added fields resolve to real
  guid/fileID pairs read from the target `.meta` files, using the `fileID: 21300000`
  single-sprite convention already used 301 times in `FarmMain.unity`.

Verified in engine on 2026-07-30, once the editor recovered:

- **EditMode suite: 243/243 passed**, 0 failed, 0 skipped.
- **Import settings confirmed by reflection:** `spriteMode=Single`, `border=(5,5,5,5)`,
  `filter=Point`, `meshType=FullRect`, `compression=Uncompressed`, texture `48x40 RGBA32`.
- **All 6 scene references** wired and read back non-null; the two `readyIcon` fields confirmed
  non-null on the *live* manager instances in play mode.
- **`BranchIcons.asset` resolves all 7 branches** to real sprites via `Resources.Load`.
- **Top toasts** render on parchment at three different hug-widths, correct icons, legible ink.
- **Bottom toasts** render on the plank with the right icons and, when fired together, stack
  vertically rather than overlapping — confirming the no-queue reasoning above.
- **Picker section headers: 7 of 7 carry an icon** (counted by walking the visual tree).
- No exceptions in the console from any of this.

### Bug found and fixed during play verification

The first in-engine pass exposed a layout bug the renderer preview could not: a **short** toast
("New Unlock!" / "Horse") split its title across two lines and broke the subtitle mid-word into
"Hors" / "e".

Cause: a `flex-wrap: wrap` container reports its intrinsic width as its **widest single child**,
not the sum of its children — because wrapping means it may break between them. An auto-width
toast therefore hugged to less than its own text, and labels with `whiteSpace: Normal` responded
by breaking internally, including mid-word for a single token.

Fix: both labels are now `WhiteSpace.NoWrap` and the text container is `flexShrink = 0`. Breaks
can then only happen *between* title and subtitle, never inside a word. This is safe because the
longest real content — `Compost Bay: Conversion Efficiency Level 10`, the longest of all 53
research `displayName`s at a two-digit level — was measured in engine and fits on one line at
88px, so the wrap path is a fallback rather than the norm.

Remaining: `ScreenCapture.CaptureScreenshot` is the right tool for these captures —
`look_at_game_view` is world-camera only and does not capture overlay UI
(`project_gladekit_screenshot_broken`). `gamedata.json` was backed up to
`gamedata.backup-2026-07-30-toastreskin.json` before the session
(`project_dev_save_reset_2026_07_14`) and was unchanged in size afterwards.

## Out of scope

- Reskinning the bottom catch toast (it keeps the plank).
- Baked crack detail.
- Per-research (as opposed to per-branch) icons.
- Reskinning popups or any other UITK surface with the parchment panel.
