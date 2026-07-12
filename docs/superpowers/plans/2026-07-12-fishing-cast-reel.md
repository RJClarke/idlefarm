# Fishing — Active Cast & Tap-to-Reel Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace the placeholder tap-to-cast fishing with an active interaction — press-and-hold a vertical charge meter to 2D-aim a cast, watch a real bobber land in the water, tap to reel it back, with the bite bubble anchored above the bobber and a placeholder whirlpool hotspot that speeds up bites when you land in it.

**Architecture:** Extend the existing pure `FishingMath` (reel/aim math, unit-tested) and the `FishingManager` scene singleton (cast/reel/hotspot state, save/load). `LakeNode` becomes the interaction hub (gesture → `Cast`/`Reel`/`SetInHotspot`, and the spatial source of truth for cast origin/range). A `ChargeMeter` renders the vertical Craftpix bar; a `FishingLineVisual` renders bobber/line/bite-bubble; a `WhirlpoolManager` owns the placeholder hotspot. Bite timing stays UtcNow-anchored so it resolves offline; whirlpools are present-only.

**Tech Stack:** Unity 6000.3, C#, NUnit EditMode tests (Unity Test Runner), LeanTween, TextMesh Pro, GladeKit/Unity-MCP for live scene wiring. Input System (new) only.

## Global Constraints

- **Design source:** `docs/superpowers/specs/2026-07-12-fishing-cast-reel-design.md` — implement to it.
- **Input:** new Input System only (`Keyboard.current`/`Mouse.current`/`Touchscreen.current`), never legacy `Input`.
- **Tests:** pure logic only in `Assets/Tests/EditMode/` (no MonoBehaviour instantiation), mirroring `FishingMathTests`. MonoBehaviours are compile- + playtest-verified.
- **Do NOT hand-edit** `.unity`/`.prefab`/`.asset`/`.meta` files — corrupts them. Scene/prefab/import changes go through Unity MCP (`mcp__unity-mcp__*` / `mcp__gladekit-unity__*`) or are listed as manual editor steps.
- **Debug.Log** only for important events (cast, bite, catch, retrieve); `LogWarning`/`LogError` freely.
- **Bite timing** stays UtcNow-anchored (`DateTime.UtcNow.Ticks`) so offline resolution keeps working.
- **Placeholder art** throughout; final art later.
- **Branch:** `feat/run-ender-economy` (all Pantry Economy work lives here).
- **Commit** after every task.

---

## File Structure

| File | Change | Responsibility |
|---|---|---|
| `Assets/Scripts/EconomyCore/FishingMath.cs` | Modify | + `ReelTapsForPower`, `PointInCircle` (pure) |
| `Assets/Tests/EditMode/FishingMathTests.cs` | Modify | + tests for the two new pure functions |
| `Assets/Scripts/GameData.cs` | Modify | + 5 fishing cast/reel save fields |
| `Assets/Scripts/Fishing/FishingManager.cs` | Modify | `Cast(power,dir)`, `Reel()`, `SetInHotspot()`, hotspot bite, save fields |
| `Assets/Sprites/UI/UI_Craftpix/Fishing/` | Create | Extracted meter sprites + generated placeholder bobber/reticle/whirlpool |
| `Assets/Scripts/Fishing/ChargeMeter.cs` | Create | Vertical track+fill+tick renderer, driven by LakeNode |
| `Assets/Scripts/Fishing/FishingLineVisual.cs` | Create | Bobber + line + bite bubble + agitation, reads FishingManager + LakeNode geometry |
| `Assets/Scripts/Fishing/LakeNode.cs` | Modify | Interaction hub: charge gesture, reel taps, aim/landing, whirlpool tracking; spatial source of truth |
| `Assets/Scripts/Fishing/WhirlpoolManager.cs` | Create | Placeholder hotspot: spawn/despawn/reservoir/`IsInside`/`ConsumeFish` |
| Scene `SampleScene.unity` | Modify (MCP) | Place pole prop, meter, bobber/line, reticle; wire WhirlpoolManager water refs, castOrigin, maxCastRange |

---

### Task 1: FishingMath — reel-taps + point-in-circle (pure, TDD)

**Files:**
- Modify: `Assets/Scripts/EconomyCore/FishingMath.cs`
- Test: `Assets/Tests/EditMode/FishingMathTests.cs`

**Interfaces:**
- Produces:
  - `int FishingMath.ReelTapsForPower(int minTaps, int maxTaps, float power01)`
  - `bool FishingMath.PointInCircle(Vector2 center, float radius, Vector2 p)`

- [ ] **Step 1: Write the failing tests**

Add to `FishingMathTests.cs`:

```csharp
    [Test]
    public void ReelTapsForPower_ScalesWithPowerAndClamps()
    {
        Assert.AreEqual(3,  FishingMath.ReelTapsForPower(3, 10, 0f));    // short cast → min
        Assert.AreEqual(10, FishingMath.ReelTapsForPower(3, 10, 1f));    // max cast → max
        Assert.AreEqual(7,  FishingMath.ReelTapsForPower(3, 10, 0.57f)); // lerp 3..10 @0.57 ≈ 6.99 → 7
        Assert.AreEqual(3,  FishingMath.ReelTapsForPower(3, 10, -5f));   // power clamps low
        Assert.AreEqual(10, FishingMath.ReelTapsForPower(3, 10, 9f));    // power clamps high
        Assert.AreEqual(1,  FishingMath.ReelTapsForPower(0, 0, 0f));     // never below 1 (always retrievable)
    }

    [Test]
    public void PointInCircle_InsideBoundaryOutside()
    {
        var c = new UnityEngine.Vector2(5f, 5f);
        Assert.IsTrue(FishingMath.PointInCircle(c, 2f, new UnityEngine.Vector2(5f, 6f)));    // inside
        Assert.IsTrue(FishingMath.PointInCircle(c, 2f, new UnityEngine.Vector2(5f, 7f)));    // exactly on edge (dist == 2)
        Assert.IsFalse(FishingMath.PointInCircle(c, 2f, new UnityEngine.Vector2(5f, 7.5f))); // outside
    }
```

- [ ] **Step 2: Run tests to verify they fail**

Run EditMode tests (Unity Test Runner → EditMode, filter `FishingMathTests`, or via MCP `mcpforunity://tests`).
Expected: FAIL — `FishingMath` does not contain `ReelTapsForPower` / `PointInCircle`.

- [ ] **Step 3: Implement the two functions**

Add to `FishingMath.cs` (it already has `using UnityEngine;`):

```csharp
    /// <summary>
    /// Reel taps to bring a cast back to shore: scales linearly with cast power so a long cast is
    /// more work than a short one (spec — reel effort scales with distance). Clamped to
    /// [minTaps, maxTaps] and never below 1, so a line is always retrievable.
    /// </summary>
    public static int ReelTapsForPower(int minTaps, int maxTaps, float power01)
    {
        int taps = Mathf.RoundToInt(Mathf.Lerp(minTaps, maxTaps, Mathf.Clamp01(power01)));
        return Mathf.Max(1, taps);
    }

    /// <summary>True when p lies within radius of center — used to test whether the bobber sits
    /// inside a whirlpool. Boundary counts as inside.</summary>
    public static bool PointInCircle(Vector2 center, float radius, Vector2 p)
        => (p - center).sqrMagnitude <= radius * radius;
```

- [ ] **Step 4: Run tests to verify they pass**

Run EditMode tests filtered to `FishingMathTests`. Expected: PASS (all, including the 4 pre-existing).

- [ ] **Step 5: Commit**

```bash
git add "Assets/Scripts/EconomyCore/FishingMath.cs" "Assets/Tests/EditMode/FishingMathTests.cs"
git commit -m "feat(fishing): reel-taps + point-in-circle pure math"
```

---

### Task 2: GameData — cast/reel save fields

**Files:**
- Modify: `Assets/Scripts/GameData.cs:64` (after `fishingPendingTier`)

**Interfaces:**
- Produces (new serialized fields on `GameData`): `float fishingCastPower01`, `float fishingCastDirX`, `float fishingCastDirY`, `int fishingReelTapsTotal`, `int fishingReelTapsRemaining`.

- [ ] **Step 1: Add the fields**

In `GameData.cs`, immediately after the line `public int fishingPendingTier;        // tier on the line when state == Bite`:

```csharp
    // Fishing active cast (2026-07-12): power/direction + reel progress for the in-flight line.
    public float fishingCastPower01;      // meter fill at release (0..1)
    public float fishingCastDirX;         // unit aim direction x (dimensionless)
    public float fishingCastDirY;         // unit aim direction y
    public int fishingReelTapsTotal;      // taps to reel this cast fully in
    public int fishingReelTapsRemaining;  // taps left before the bobber reaches shore
```

- [ ] **Step 2: Verify compile**

Run: MCP `mcp__unity-mcp__refresh_unity` then read `mcpforunity://editor/state` / console (or Unity recompiles on focus). Expected: no compile errors. (No behavior yet — fields default to 0, which reads as an Idle line.)

- [ ] **Step 3: Commit**

```bash
git add "Assets/Scripts/GameData.cs"
git commit -m "feat(fishing): GameData fields for cast power/dir + reel progress"
```

---

### Task 3: FishingManager — cast(power,dir), reel, hotspot bite, save

**Files:**
- Modify: `Assets/Scripts/Fishing/FishingManager.cs`

**Interfaces:**
- Consumes: `FishingMath.ReelTapsForPower`, `FishingMath.RollBiteSeconds`, `FishingMath.RollFishTier`; `GameData` fields from Task 2.
- Produces:
  - `bool Cast(float power01, Vector2 dir)` (replaces the old parameterless `Cast()`)
  - `bool Reel()`
  - `void SetInHotspot(bool inside)`
  - getters: `float CastPower01`, `Vector2 CastDir`, `int ReelTapsRemaining`, `int ReelTapsTotal`, `float ReelProgress01`, `bool CaughtFromHotspot`
  - unchanged: `CastState State`, `bool HasBite`, `bool HasPole`, `int PendingTier`, `OnChanged`

- [ ] **Step 1: Add fields + tuning**

In `FishingManager.cs`, after the existing tuning headers add:

```csharp
    [Header("Cast / Reel (spec 2026-07-12)")]
    [Tooltip("Reel taps for the shortest cast.")]
    [SerializeField] private int minReelTaps = 3;
    [Tooltip("Reel taps for a full-power cast.")]
    [SerializeField] private int maxReelTaps = 10;

    [Header("Whirlpool")]
    [Tooltip("Average seconds to a bite while the bobber sits inside a whirlpool (fast, not instant).")]
    [SerializeField] private float hotspotBiteAvgSeconds = 20f;
```

In the private-fields block (near `pendingTier`) add:

```csharp
    private float castPower01;
    private Vector2 castDir = Vector2.up;
    private int reelTapsTotal;
    private int reelTapsRemaining;
    private bool inHotspot;
    private bool caughtFromHotspot;
```

In the public getters block (near `PendingTier`) add:

```csharp
    public float CastPower01 => castPower01;
    public Vector2 CastDir => castDir;
    public int ReelTapsRemaining => reelTapsRemaining;
    public int ReelTapsTotal => reelTapsTotal;
    public float ReelProgress01 => reelTapsTotal > 0 ? (float)reelTapsRemaining / reelTapsTotal : 0f;
    public bool CaughtFromHotspot => caughtFromHotspot;
```

- [ ] **Step 2: Replace `Cast()` and add bite-roll helpers**

Replace the entire existing `Cast()` method (currently at ~line 97) with:

```csharp
    /// <summary>Cast the line if idle and a pole is owned. Power sets reel effort + landing distance;
    /// dir is the aim direction (unit). Rolls a baseline bite time (UtcNow-anchored).</summary>
    public bool Cast(float power01, Vector2 dir)
    {
        if (!hasPole || state != CastState.Idle) return false;
        castPower01 = Mathf.Clamp01(power01);
        castDir = dir.sqrMagnitude > 1e-6f ? dir.normalized : Vector2.up;
        reelTapsTotal = FishingMath.ReelTapsForPower(minReelTaps, maxReelTaps, castPower01);
        reelTapsRemaining = reelTapsTotal;
        inHotspot = false;
        caughtFromHotspot = false;
        RollBaselineBite();
        state = CastState.Waiting;
        Debug.Log($"[Fishing] Cast power={castPower01:0.00} taps={reelTapsTotal}.");
        OnChanged?.Invoke();
        return true;
    }

    private void RollBaselineBite()
    {
        long now = DateTime.UtcNow.Ticks;
        double secs = FishingMath.RollBiteSeconds(CurrentTier().biteAvgSeconds, UnityEngine.Random.value);
        castUtcTicks = now;
        biteReadyUtcTicks = now + (long)(secs * TimeSpan.TicksPerSecond);
    }

    private void RollHotspotBite()
    {
        long now = DateTime.UtcNow.Ticks;
        double secs = FishingMath.RollBiteSeconds(hotspotBiteAvgSeconds, UnityEngine.Random.value);
        biteReadyUtcTicks = now + (long)(secs * TimeSpan.TicksPerSecond);
    }
```

- [ ] **Step 3: Add `SetInHotspot` and `Reel`; update `TransitionToBite`/`Collect`**

Add these methods (near `Collect`):

```csharp
    /// <summary>Bobber entered/left a live whirlpool (called by LakeNode). Re-anchors the bite:
    /// entering grants a fast bite; leaving before biting reverts to baseline. No-op once biting.</summary>
    public void SetInHotspot(bool inside)
    {
        if (state != CastState.Waiting || inside == inHotspot) return;
        inHotspot = inside;
        if (inside) RollHotspotBite(); else RollBaselineBite();
        OnChanged?.Invoke();
    }

    /// <summary>Pull the bobber one step toward shore. Valid only while Waiting/Bite. Reaching shore
    /// lands a biting fish (Collect) or retrieves an empty line. Returns true if a step was consumed.</summary>
    public bool Reel()
    {
        if (state != CastState.Waiting && state != CastState.Bite) return false;
        if (reelTapsRemaining > 0) reelTapsRemaining--;
        if (reelTapsRemaining > 0) { OnChanged?.Invoke(); return true; }
        if (state == CastState.Bite) Collect(); else RetrieveEmpty();
        return true;
    }

    private void RetrieveEmpty()
    {
        ClearLine();
        state = CastState.Idle;
        Debug.Log("[Fishing] Line retrieved (no fish).");
        OnChanged?.Invoke();
    }

    private void ClearLine()
    {
        castUtcTicks = 0; biteReadyUtcTicks = 0; pendingTier = 0;
        castPower01 = 0f; castDir = Vector2.up; reelTapsTotal = 0; reelTapsRemaining = 0;
        inHotspot = false; caughtFromHotspot = false;
    }
```

In `TransitionToBite()`, set the hotspot flag at the moment of bite — change it to:

```csharp
    private void TransitionToBite()
    {
        pendingTier = FishingMath.RollFishTier(CurrentTier().weights, UnityEngine.Random.value);
        caughtFromHotspot = inHotspot;
        state = CastState.Bite;
        Debug.Log($"[Fishing] Bite: {FishTiers.Name(pendingTier)} on the line.");
        OnChanged?.Invoke();
    }
```

In `Collect()`, replace the manual field resets (`pendingTier = 0; castUtcTicks = 0; biteReadyUtcTicks = 0;`) with a call to `ClearLine();` so all cast/reel fields reset together. The method becomes:

```csharp
    public int Collect()
    {
        if (state != CastState.Bite) return 0;
        int tier = pendingTier;
        if (PantryManager.Instance != null) PantryManager.Instance.AddRaw(tier);
        ClearLine();
        state = CastState.Idle;
        Debug.Log($"[Fishing] Collected {FishTiers.Name(tier)}.");
        OnChanged?.Invoke();
        return tier;
    }
```

- [ ] **Step 4: Extend save capture/load**

In `CaptureTo(GameData d)` add:

```csharp
        d.fishingCastPower01 = castPower01;
        d.fishingCastDirX = castDir.x;
        d.fishingCastDirY = castDir.y;
        d.fishingReelTapsTotal = reelTapsTotal;
        d.fishingReelTapsRemaining = reelTapsRemaining;
```

In `LoadFrom(GameData d)` add (before the offline catch-up block):

```csharp
        castPower01 = Mathf.Clamp01(d.fishingCastPower01);
        castDir = new Vector2(d.fishingCastDirX, d.fishingCastDirY);
        if (castDir.sqrMagnitude < 1e-6f) castDir = Vector2.up; else castDir.Normalize();
        reelTapsTotal = Mathf.Max(0, d.fishingReelTapsTotal);
        reelTapsRemaining = Mathf.Clamp(d.fishingReelTapsRemaining, 0, Mathf.Max(0, reelTapsTotal));
```

- [ ] **Step 5: Verify compile + find stale callers**

Run: `grep -rn "\.Cast()" Assets/Scripts` — the only caller is `LakeNode.HandleClick` (reworked in Task 7). It won't compile until Task 7; that's expected. Confirm no OTHER caller of parameterless `Cast()` exists. Recompile via MCP; the only error should be in `LakeNode.cs`.

- [ ] **Step 6: Commit**

```bash
git add "Assets/Scripts/Fishing/FishingManager.cs"
git commit -m "feat(fishing): Cast(power,dir), Reel(), SetInHotspot() + save fields"
```

> Note: `LakeNode.cs` is intentionally left broken until Task 7. If you need a green compile between tasks, do Task 7 immediately after this one.

---

### Task 4: Extract + generate placeholder sprites

**Files:**
- Create: `Assets/Sprites/UI/UI_Craftpix/Fishing/meter_track.png`, `meter_fill.png`, `meter_tick.png`, `bobber.png`, `reticle.png`, `whirlpool.png`

**Interfaces:**
- Produces: six placeholder PNGs the visual components will reference.

- [ ] **Step 1: Extract the Craftpix meter sprites + generate placeholders**

Run this script (PIL is available). It slices the meter from `Bars.png` (rects verified in the spec) and paints simple placeholder circles/ring/disc for bobber/reticle/whirlpool:

```bash
cd "C:/Users/rjcla/IdleFarm - Silo" && python -c "
from PIL import Image, ImageDraw
import os
src=Image.open('Assets/Sprites/UI/UI_Craftpix/Bars.png').convert('RGBA')
out='Assets/Sprites/UI/UI_Craftpix/Fishing'; os.makedirs(out, exist_ok=True)
src.crop((242,913,242+11,913+45)).save(f'{out}/meter_track.png')   # gold vertical frame
src.crop((84,957,84+52,957+6)).save(f'{out}/meter_fill.png')       # solid green fill (rotate/fill in UI)
# 1x3 white tick (tint red in-scene)
tick=Image.new('RGBA',(1,3),(255,255,255,255)); tick.save(f'{out}/meter_tick.png')
def disc(size, fill, ring=None):
    im=Image.new('RGBA',(size,size),(0,0,0,0)); d=ImageDraw.Draw(im)
    d.ellipse([0,0,size-1,size-1], fill=fill, outline=ring)
    return im
disc(8,(220,60,60,255),(30,20,20,255)).save(f'{out}/bobber.png')            # red/white-ish bobber
r=Image.new('RGBA',(16,16),(0,0,0,0)); ImageDraw.Draw(r).ellipse([1,1,14,14],outline=(255,240,120,255)); r.save(f'{out}/reticle.png')
disc(32,(30,50,110,150),(60,90,170,220)).save(f'{out}/whirlpool.png')       # dark-blue semi-transparent
print('wrote', os.listdir(out))
"
```

- [ ] **Step 2: Import as pixel sprites**

Refresh the asset DB (`mcp__unity-mcp__refresh_unity`). For each new PNG set import settings via MCP `mcp__unity-mcp__manage_asset` (or manually in the Inspector): **Texture Type = Sprite (2D and UI)**, **Sprite Mode = Single**, **Filter = Point (no filter)**, **Compression = None**, **Pixels Per Unit = 32**. For `meter_fill.png` set **Pivot = Bottom**. Verify each shows as a Sprite:
Run: `mcp__gladekit-unity__check_asset_exists` on `Assets/Sprites/UI/UI_Craftpix/Fishing/meter_track.png`. Expected: exists, type Sprite.

- [ ] **Step 3: Commit**

```bash
git add "Assets/Sprites/UI/UI_Craftpix/Fishing"
git commit -m "chore(fishing): extract meter sprites + placeholder bobber/reticle/whirlpool"
```

---

### Task 5: ChargeMeter component (vertical track + fill + tick)

**Files:**
- Create: `Assets/Scripts/Fishing/ChargeMeter.cs`

**Interfaces:**
- Produces:
  - `void ChargeMeter.Show()` / `void Hide()`
  - `void ChargeMeter.SetFill(float fill01)`
  - `void ChargeMeter.SetTick(float tick01)`

- [ ] **Step 1: Write the component**

```csharp
using UnityEngine;

/// <summary>
/// A vertical charge meter for the fishing cast: a gold Craftpix frame (track) with a green fill
/// that grows bottom→top and a red target tick. Pure view — LakeNode drives it via SetFill/SetTick
/// while the player holds to charge. Built from three SpriteRenderers under this object; the fill
/// renderer must use a bottom-pivot sprite so scaling its localScale.y fills upward.
/// </summary>
public class ChargeMeter : MonoBehaviour
{
    [SerializeField] private SpriteRenderer track;  // meter_track (gold frame)
    [SerializeField] private SpriteRenderer fill;   // meter_fill (green, bottom pivot)
    [SerializeField] private SpriteRenderer tick;   // meter_tick (tinted red)

    [Tooltip("World height of the fillable interior at fill=1 (tune to sit inside the frame).")]
    [SerializeField] private float interiorHeight = 1.2f;
    [Tooltip("Local Y of the interior bottom (where fill starts and tick=0 sits).")]
    [SerializeField] private float interiorBottomY = -0.6f;

    private void Awake() => Hide();

    public void Show()
    {
        gameObject.SetActive(true);
        SetFill(0f);
    }

    public void Hide() => gameObject.SetActive(false);

    /// <summary>Fill 0..1 grows the green bar upward from the interior bottom.</summary>
    public void SetFill(float fill01)
    {
        if (fill == null) return;
        float f = Mathf.Clamp01(fill01);
        var s = fill.transform.localScale; s.y = f; fill.transform.localScale = s;
        var p = fill.transform.localPosition; p.y = interiorBottomY; fill.transform.localPosition = p;
    }

    /// <summary>Position the target tick at tick01 up the interior (0 = bottom, 1 = top).</summary>
    public void SetTick(float tick01)
    {
        if (tick == null) return;
        bool show = tick01 > 0.0001f && tick01 < 0.9999f;
        tick.enabled = show;
        var p = tick.transform.localPosition;
        p.y = interiorBottomY + Mathf.Clamp01(tick01) * interiorHeight;
        tick.transform.localPosition = p;
    }
}
```

> The fill sprite is authored bottom-pivot (Task 4), so `localScale.y = fill01` grows it from the interior bottom. `interiorHeight`/`interiorBottomY` are tuned in-scene (Task 9) so the green sits inside the gold frame; `meter_fill` should also be pre-scaled in-scene so its full height ≈ `interiorHeight`.

- [ ] **Step 2: Verify compile**

Recompile via MCP. Expected: no errors.

- [ ] **Step 3: Commit**

```bash
git add "Assets/Scripts/Fishing/ChargeMeter.cs"
git commit -m "feat(fishing): ChargeMeter vertical track/fill/tick view"
```

---

### Task 6: FishingLineVisual (bobber + line + bite bubble + agitation)

**Files:**
- Create: `Assets/Scripts/Fishing/FishingLineVisual.cs`

**Interfaces:**
- Consumes: `FishingManager` (`State`, `HasBite`, `CastPower01`, `CastDir`, `ReelProgress01`); `LakeNode.CastOrigin`, `LakeNode.MaxCastRange`, `LakeNode.CurrentBobberWorldPos()` (defined in Task 7 — this task compiles against those members).
- Produces: `void FishingLineVisual.SetAgitated(bool on)` (LakeNode toggles when the bobber is inside a whirlpool).

- [ ] **Step 1: Write the component**

```csharp
using UnityEngine;

/// <summary>
/// Renders the in-flight fishing line: a bobber in the water, a line from the pole to it, and the
/// bite bubble (fish icon) above it. Pure view — reads FishingManager state and LakeNode geometry
/// each frame. The bobber "agitates" (spins) while inside a whirlpool as the player's cue.
/// Replaces LakeNode's old fixed-offset bite indicator.
/// </summary>
[RequireComponent(typeof(LineRenderer))]
public class FishingLineVisual : MonoBehaviour
{
    [SerializeField] private LakeNode lake;              // geometry source
    [SerializeField] private SpriteRenderer bobber;      // bobber.png
    [SerializeField] private LineRenderer line;          // pole → bobber
    [SerializeField] private Vector3 bubbleOffset = new Vector3(0f, 0.8f, 0f);

    private WorldHintPopup biteIndicator;
    private bool biteShown;
    private bool agitated;
    private float spin;

    public void SetAgitated(bool on) => agitated = on;

    private void Reset() => line = GetComponent<LineRenderer>();

    private void Update()
    {
        var fm = FishingManager.Instance;
        bool cast = fm != null && (fm.State == FishingManager.CastState.Waiting || fm.State == FishingManager.CastState.Bite);

        if (bobber != null) bobber.enabled = cast;
        if (line != null) line.enabled = cast;

        if (!cast) { HideBubble(); return; }

        Vector3 pos = lake != null ? lake.CurrentBobberWorldPos() : transform.position;
        if (bobber != null)
        {
            bobber.transform.position = pos;
            if (agitated) { spin += 540f * Time.deltaTime; bobber.transform.rotation = Quaternion.Euler(0, 0, spin); }
            else bobber.transform.rotation = Quaternion.identity;
        }
        if (line != null && lake != null)
        {
            line.positionCount = 2;
            line.SetPosition(0, lake.CastOrigin);
            line.SetPosition(1, pos);
        }
        SyncBubble(fm.HasBite, pos + bubbleOffset);
    }

    private void SyncBubble(bool biting, Vector3 at)
    {
        if (biting && !biteShown)
        {
            HideBubble();
            biteIndicator = WorldHintPopup.Create(at, "🐟");
            biteShown = true;
        }
        else if (biting && biteShown && biteIndicator != null)
        {
            biteIndicator.transform.position = at; // follow the bobber as it reels
        }
        else if (!biting && biteShown) HideBubble();
    }

    private void HideBubble()
    {
        if (biteIndicator != null) Destroy(biteIndicator.gameObject);
        biteIndicator = null; biteShown = false;
    }
}
```

> `WorldHintPopup.Create` self-destructs after `holdSeconds`+`fadeSeconds`; for a persistent bite bubble that follows the bobber, either (a) accept it re-creating when it fades, or (b) in a follow-up add a `persistent` flag to `WorldHintPopup`. For this placeholder pass, option (a) is fine — the bubble reappears each time it fades while `HasBite` holds. If it flickers annoyingly during playtest, add the flag then.

- [ ] **Step 2: Verify compile**

`FishingLineVisual` references `LakeNode.CastOrigin` / `MaxCastRange` / `CurrentBobberWorldPos()`. Do Task 7 before compiling, or stub those members first. Recompile after Task 7. Expected: no errors.

- [ ] **Step 3: Commit** (after Task 7 compiles green)

```bash
git add "Assets/Scripts/Fishing/FishingLineVisual.cs"
git commit -m "feat(fishing): FishingLineVisual bobber/line/bite-bubble view"
```

---

### Task 7: LakeNode — charge gesture, reel taps, 2D aim, geometry

**Files:**
- Modify: `Assets/Scripts/Fishing/LakeNode.cs`

**Interfaces:**
- Consumes: `FishingManager.Cast(power,dir)`, `Reel()`, `SetInHotspot()`, `CastPower01`, `CastDir`, `ReelProgress01`, `CaughtFromHotspot`; `ChargeMeter`; `WhirlpoolManager` (Task 8 — reference is optional/nullable so LakeNode compiles first).
- Produces (spatial source of truth, consumed by FishingLineVisual + WhirlpoolManager):
  - `Vector3 LakeNode.CastOrigin` (world)
  - `float LakeNode.MaxCastRange`
  - `Vector3 LakeNode.CurrentBobberWorldPos()`

- [ ] **Step 1: Add geometry + component refs + charge state**

Add serialized fields:

```csharp
    [Header("Cast Geometry")]
    [Tooltip("Shore point casts fly out from (the pole). Landing = origin + dir × power × maxCastRange.")]
    [SerializeField] private Transform castOrigin;
    [Tooltip("World distance a full-power cast reaches.")]
    [SerializeField] private float maxCastRange = 6f;

    [Header("Cast UI / Visual")]
    [SerializeField] private ChargeMeter chargeMeter;
    [SerializeField] private FishingLineVisual lineVisual;
    [SerializeField] private SpriteRenderer reticle;        // reticle.png, shown while charging
    [SerializeField] private float chargeFillSeconds = 1.2f;

    [Header("Whirlpool (optional)")]
    [SerializeField] private WhirlpoolManager whirlpool;    // may be null until Task 8 wired

    // charge gesture state
    private bool charging;
    private float chargeT;              // 0..1 fill
    private Vector2 aimDir = Vector2.up;
    private float aimTargetDist;        // world distance to the pressed spot (for the tick)
```

Add public geometry accessors:

```csharp
    public Vector3 CastOrigin => castOrigin != null ? castOrigin.position : transform.position;
    public float MaxCastRange => maxCastRange;

    /// <summary>Current bobber world position: along the cast ray, retreating toward the origin as it reels.</summary>
    public Vector3 CurrentBobberWorldPos()
    {
        var fm = FishingManager.Instance;
        if (fm == null) return CastOrigin;
        float dist = fm.CastPower01 * maxCastRange * fm.ReelProgress01;
        Vector3 dir = new Vector3(fm.CastDir.x, fm.CastDir.y, 0f);
        return CastOrigin + dir * dist;
    }
```

- [ ] **Step 2: Replace `Update` pointer flow with state-aware gesture**

Rework `Update()` so Idle = charge gesture, Waiting/Bite = reel tap + whirlpool tracking. Replace the body of `Update()` with:

```csharp
    private void Update()
    {
        TrackWhirlpool();
        if (lineVisual == null || FishingManager.Instance == null) { /* still handle input */ }

        if (!TryReadPointer(out Vector2 screenPos, out bool justPressed, out bool justReleased, out bool held))
        { if (charging && !held) CancelCharge(); return; }
        if (UITapBlocker.PointerOverUI(screenPos)) { CancelCharge(); CancelPress(); return; }

        var fm = FishingManager.Instance;
        bool interact = CanInteract() && fm != null;

        if (interact && fm.State == FishingManager.CastState.Idle)
            HandleChargeGesture(screenPos, justPressed, justReleased, held);
        else
            HandleReelGesture(screenPos, justPressed, justReleased);
    }
```

- [ ] **Step 3: Add the charge, reel, and whirlpool-tracking handlers**

```csharp
    private void HandleChargeGesture(Vector2 screenPos, bool justPressed, bool justReleased, bool held)
    {
        var fm = FishingManager.Instance;
        if (justPressed && !charging && PointerHitsSelf(screenPos))
        {
            if (!fm.HasPole) { fm.ShowNoPoleHint(transform.position); return; }
            charging = true; chargeT = 0f;
            Vector3 spot = PointerWorld(screenPos);
            Vector3 to = spot - CastOrigin;
            aimDir = ((Vector2)to).sqrMagnitude > 1e-6f ? ((Vector2)to).normalized : Vector2.up;
            aimTargetDist = Mathf.Min(to.magnitude, maxCastRange);
            if (reticle != null) { reticle.enabled = true; reticle.transform.position = CastOrigin + (Vector3)(aimDir * aimTargetDist); }
            if (chargeMeter != null) { chargeMeter.Show(); chargeMeter.SetTick(maxCastRange > 0 ? aimTargetDist / maxCastRange : 0f); }
            return;
        }
        if (charging && held)
        {
            chargeT = Mathf.Clamp01(chargeT + Time.deltaTime / Mathf.Max(0.01f, chargeFillSeconds));
            if (chargeMeter != null) chargeMeter.SetFill(chargeT);
            return;
        }
        if (charging && justReleased)
        {
            float power = chargeT;
            EndCharge();
            fm.Cast(power, aimDir);
        }
    }

    private void HandleReelGesture(Vector2 screenPos, bool justPressed, bool justReleased)
    {
        var fm = FishingManager.Instance;
        if (fm == null || !CanInteract()) return;
        // press-feedback + reel on release-over-self (mirrors the old tap flow)
        if (justPressed && !isPressed && PointerHitsSelf(screenPos))
        { isPressed = true; spriteRenderer.color = pressTint * baseColor; DoTween(baseScale * pressScale, pressDuration); return; }
        if (justReleased && isPressed)
        {
            bool overSelf = PointerHitsSelf(screenPos);
            CancelPress();
            if (overSelf) fm.Reel();
        }
    }

    private void TrackWhirlpool()
    {
        var fm = FishingManager.Instance;
        if (fm == null) return;
        bool cast = fm.State == FishingManager.CastState.Waiting || fm.State == FishingManager.CastState.Bite;
        bool inside = false;
        if (cast && whirlpool != null)
            inside = whirlpool.IsInside(CurrentBobberWorldPos());
        fm.SetInHotspot(inside);
        if (lineVisual != null) lineVisual.SetAgitated(inside && cast);

        // consume a whirlpool fish on the rising edge of a hotspot bite
        bool biting = cast && fm.HasBite;
        if (biting && !hotspotBiteConsumed && fm.CaughtFromHotspot)
        { if (whirlpool != null) whirlpool.ConsumeFish(); hotspotBiteConsumed = true; }
        if (!biting) hotspotBiteConsumed = false;
    }

    private void CancelCharge()
    {
        if (!charging) return;
        EndCharge();
    }

    private void EndCharge()
    {
        charging = false; chargeT = 0f;
        if (chargeMeter != null) chargeMeter.Hide();
        if (reticle != null) reticle.enabled = false;
    }
```

Add the tracking field near the other private fields:

```csharp
    private bool hotspotBiteConsumed;
```

Add a pointer→world helper (used by the charge gesture):

```csharp
    private Vector3 PointerWorld(Vector2 screenPos)
    {
        Camera cam = Camera.main;
        if (cam == null) return CastOrigin;
        return cam.ScreenToWorldPoint(new Vector3(screenPos.x, screenPos.y, -cam.transform.position.z));
    }
```

- [ ] **Step 4: Remove the dead bite-indicator + old HandleClick**

Delete the old `SyncBiteIndicator()` method, the `biteIndicator`/`biteShown` fields, and the old `HandleClick()` (bite/collect/cast logic now lives in the gesture handlers + FishingLineVisual). Keep `CanInteract`, `PointerHitsSelf`, `TryReadPointer`, `CancelPress`, `DoTween`, press-feedback fields, and `bobberOffset` (now only used as a fallback; safe to leave). Ensure `Awake` no longer references removed fields.

- [ ] **Step 5: Verify compile (with Task 6 present)**

Recompile via MCP. `WhirlpoolManager` is referenced but created in Task 8 — to compile now, either implement Task 8 first or temporarily comment the `whirlpool` field + its two uses. Preferred order: **Task 8 before Task 7's final compile.** Expected after both: no errors.

- [ ] **Step 6: Commit** (once green with Tasks 6 + 8)

```bash
git add "Assets/Scripts/Fishing/LakeNode.cs"
git commit -m "feat(fishing): LakeNode charge gesture + reel taps + 2D aim + geometry"
```

---

### Task 8: WhirlpoolManager (placeholder hotspot)

**Files:**
- Create: `Assets/Scripts/Fishing/WhirlpoolManager.cs`

**Interfaces:**
- Consumes: `FishingMath.PointInCircle`; `LakeNode.CastOrigin`, `LakeNode.MaxCastRange`; a water `Collider2D`.
- Produces:
  - `bool WhirlpoolManager.IsInside(Vector3 worldPoint)`
  - `void WhirlpoolManager.ConsumeFish()`

- [ ] **Step 1: Write the component**

```csharp
using UnityEngine;

/// <summary>
/// Owns the placeholder whirlpool hotspot (spec 2026-07-12). One at a time: spawns at a random
/// reachable water point (inside the water collider AND within cast range), lives a while, then
/// despawns and waits before the next. Holds 2–4 fish; each hotspot bite consumes one; empty →
/// despawn. Present-only, not persisted — a fast bite already earned lives in the cast's saved
/// bite time. LakeNode queries IsInside each frame and calls ConsumeFish on a hotspot bite.
/// </summary>
public class WhirlpoolManager : MonoBehaviour
{
    [SerializeField] private LakeNode lake;
    [Tooltip("Water outline used to keep spawns on the water (the painted Tilemap_water CompositeCollider2D).")]
    [SerializeField] private Collider2D waterCollider;
    [SerializeField] private SpriteRenderer circle;         // whirlpool.png, toggled on spawn

    [Header("Tuning")]
    [SerializeField] private float radius = 1.1f;
    [SerializeField] private Vector2 lifetimeSecondsRange = new Vector2(90f, 150f);
    [SerializeField] private Vector2 gapSecondsRange = new Vector2(180f, 360f);
    [SerializeField] private int minFish = 2;
    [SerializeField] private int maxFish = 4;

    private bool active;
    private Vector2 center;
    private int fishRemaining;
    private float timer;

    public bool IsInside(Vector3 worldPoint)
        => active && FishingMath.PointInCircle(center, radius, worldPoint);

    public void ConsumeFish()
    {
        if (!active) return;
        fishRemaining--;
        if (fishRemaining <= 0) Despawn();
    }

    private void Awake() { if (circle != null) circle.enabled = false; timer = Random.Range(gapSecondsRange.x, gapSecondsRange.y); }

    private void Update()
    {
        timer -= Time.deltaTime;
        if (timer > 0f) return;
        if (active) Despawn(); else Spawn();
    }

    private void Spawn()
    {
        if (lake == null) { timer = gapSecondsRange.x; return; }
        if (!TryPickPoint(out center)) { timer = 2f; return; } // retry shortly
        active = true;
        fishRemaining = Random.Range(minFish, maxFish + 1);
        timer = Random.Range(lifetimeSecondsRange.x, lifetimeSecondsRange.y);
        if (circle != null)
        {
            circle.transform.position = new Vector3(center.x, center.y, circle.transform.position.z);
            circle.transform.localScale = Vector3.one * (radius * 2f); // sprite is unit-ish; tune in scene
            circle.enabled = true;
        }
        Debug.Log($"[Whirlpool] Spawned with {fishRemaining} fish at {center}.");
    }

    private void Despawn()
    {
        active = false;
        if (circle != null) circle.enabled = false;
        timer = Random.Range(gapSecondsRange.x, gapSecondsRange.y);
    }

    // Rejection-sample a point within cast range of the origin that lands on the water.
    private bool TryPickPoint(out Vector2 p)
    {
        Vector2 origin = lake.CastOrigin;
        float maxR = lake.MaxCastRange;
        for (int i = 0; i < 24; i++)
        {
            float ang = Random.value * Mathf.PI * 2f;
            float r = Mathf.Sqrt(Random.value) * maxR;         // uniform in disc
            p = origin + new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * r;
            if (waterCollider == null || waterCollider.OverlapPoint(p)) return true;
        }
        p = origin; return false;
    }
}
```

- [ ] **Step 2: Verify compile**

Recompile via MCP (with Tasks 3, 6, 7 present). Expected: no errors across the Fishing folder.

- [ ] **Step 3: Commit**

```bash
git add "Assets/Scripts/Fishing/WhirlpoolManager.cs"
git commit -m "feat(fishing): WhirlpoolManager placeholder hotspot + reservoir"
```

---

### Task 9: Scene wiring (Unity MCP)

**Files:**
- Modify (via MCP, never by hand): `Assets/Scenes/SampleScene.unity`

**Interfaces:**
- Consumes: all components above; the existing `FishingManager` scene object, the Lake object hosting `LakeNode`, and the painted `Tilemap_water` collider (`LakeNode.waterHitSource`).

- [ ] **Step 1: Inspect the current Lake objects**

Use `mcp__gladekit-unity__get_scene_hierarchy` / `find_game_objects` to locate the Lake `GameObject` (has `LakeNode`), the `Tilemap_water` (CompositeCollider2D), and `FishingManager`. Record their names/paths.

- [ ] **Step 2: Create child objects under the Lake**

Via `mcp__gladekit-unity__create_game_object` (children of the Lake object):
- `Pole` — SpriteRenderer (placeholder rod sprite or a small marker); its Transform is the `castOrigin`.
- `ChargeMeter` — empty with `ChargeMeter` component; three SpriteRenderer children: `Track` (meter_track), `Fill` (meter_fill, bottom-pivot), `Tick` (meter_tick, tinted red `(220,40,40)`). Position it left of the water, near the pole. Assign the three renderers into `ChargeMeter`'s fields; tune `interiorHeight`/`interiorBottomY` and the Fill child's base scale so the green sits inside the gold frame.
- `FishingLine` — has `LineRenderer` + `FishingLineVisual`; a `Bobber` SpriteRenderer child (bobber.png). Set the LineRenderer material to a simple sprite/unlit, width ~0.05, sortingOrder above water. Assign `lake`, `bobber`, `line`.
- `Reticle` — SpriteRenderer (reticle.png), starts disabled.
- `Whirlpool` — has `WhirlpoolManager`; a `Circle` SpriteRenderer child (whirlpool.png), starts disabled.

- [ ] **Step 3: Wire references**

Via `mcp__gladekit-unity__set_script_component_property` / `set_object_reference`:
- `LakeNode`: `castOrigin`=Pole transform, `maxCastRange`≈6, `chargeMeter`, `lineVisual`, `reticle`, `whirlpool`.
- `FishingLineVisual`: `lake`=LakeNode, `bobber`, `line`.
- `WhirlpoolManager`: `lake`=LakeNode, `waterCollider`=Tilemap_water's CompositeCollider2D, `circle`=Circle renderer.
- `ChargeMeter`: `track`/`fill`/`tick`.

Set sorting orders so bobber/line/whirlpool draw above the water sprite and below `WorldHintPopup` (which uses sortingOrder 500).

- [ ] **Step 4: Play-mode smoke test**

Enter play mode (`mcp__unity-mcp__manage_editor`), pan to the Lake, and verify via console logs + `get_runtime_events`:
1. With no pole → press water shows the "buy a pole" hint.
2. Buy a pole (dev menu / grant), then press-hold on water → meter shows, fills, tick sits at the pressed distance; release → `[Fishing] Cast` logs, bobber appears out in the water, line drawn.
3. Tap water repeatedly → bobber steps toward the pole; reaching it with no bite logs `[Fishing] Line retrieved`.
4. Temporarily lower `hotspotBiteAvgSeconds` and `gapSecondsRange` (e.g. 3s/5s) to force a whirlpool; cast into it → bobber agitates, `[Fishing] Bite` within ~30s, reel in → fish banked, `[Whirlpool]` fish decremented; deplete it → despawns. Restore tuning after.

Stop play mode.

- [ ] **Step 5: Save the scene + commit**

Save via `mcp__gladekit-unity__save_scene`. Then:

```bash
git add "Assets/Scenes/SampleScene.unity" "Assets/Prefabs" -A
git commit -m "feat(fishing): wire cast/reel/whirlpool objects into the Lake scene"
```

---

### Task 10: Full verification pass

**Files:** none (verification only)

- [ ] **Step 1: Run the full EditMode suite**

Run all EditMode tests (Unity Test Runner → Run All, or MCP `mcpforunity://tests`). Expected: all pass, including the pre-existing fishing tests and the two new `FishingMath` tests.

- [ ] **Step 2: Save/offline round-trip**

In play mode: cast a line (leave it Waiting), force a save (`SaveManager.SaveGame()` via dev menu or app-pause). Stop, re-enter play, load. Confirm the bobber restores at its cast distance with the same reel progress, and a Waiting line whose bite time has passed shows a bite (offline catch-up). Cast into a whirlpool, earn a fast bite, save mid-reel, reload → the fast bite (baked into `biteReadyUtcTicks`) persists; no whirlpool is simulated offline.

- [ ] **Step 3: Look at it**

Screenshot/observe the cast→charge→bobber→reel→bite loop and the whirlpool at the Lake (per the "look without asking" workflow). Confirm the meter sits inside the gold frame, the tick reads clearly, and the agitated bobber cue is visible inside the whirlpool.

- [ ] **Step 4: Final commit (if any tuning changed)**

```bash
git add -A
git commit -m "chore(fishing): tuning + verification pass for cast/reel/whirlpool"
```

---

## Self-Review

**Spec coverage:**
- Press-hold vertical charge meter → Tasks 5, 7, 9. ✓
- 2D aim (touch dir + meter distance, target tick) → Task 7 (`HandleChargeGesture`), Task 5 (`SetTick`). ✓
- Bobber lands in water along the cast ray → Task 7 (`CurrentBobberWorldPos`), Task 6. ✓
- Tap-to-reel, effort scales with distance → Task 1 (`ReelTapsForPower`), Task 3 (`Reel`), Task 7 (`HandleReelGesture`). ✓
- Reel empty = penalty-free retrieve; reel bite = bank → Task 3 (`RetrieveEmpty`/`Collect`). ✓
- Bite bubble above/following bobber → Task 6 (`SyncBubble`). ✓
- Fixed pole origin → Task 7 (`castOrigin`), Task 9 (Pole). ✓
- Whirlpool: dynamic position-based boost, reel-into overshoot → Task 7 (`TrackWhirlpool` per-frame `SetInHotspot`), Task 3 (`SetInHotspot` re-anchor). ✓
- Whirlpool fast-not-instant (~<30s) → Task 3 (`hotspotBiteAvgSeconds`=20 via `RollBiteSeconds`). ✓
- Whirlpool 2–4 fish, consume, despawn → Task 8. ✓
- Reachable spawn (inside water ∧ within range), present-only, no persistence → Task 8. ✓
- Save fields + offline-safe bite → Tasks 2, 3, 10. ✓
- Craftpix meter art + placeholder bobber/reticle/whirlpool → Task 4. ✓
- Meter pinned left, off the water → Task 9. ✓

**Placeholder scan:** No "TBD"/"handle edge cases" — every code step has real code. The two soft spots (WorldHintPopup follow-flicker; in-scene tuning of meter interior) are called out with concrete fallbacks, not left vague.

**Type consistency:** `Cast(float, Vector2)`, `Reel()`, `SetInHotspot(bool)`, `CastPower01`/`CastDir`/`ReelProgress01`/`CaughtFromHotspot`, `IsInside(Vector3)`, `ConsumeFish()`, `CastOrigin`/`MaxCastRange`/`CurrentBobberWorldPos()`, `ChargeMeter.Show/Hide/SetFill/SetTick`, `FishingLineVisual.SetAgitated` — names match across all tasks that reference them.

**Ordering note:** Tasks 3 → 7 break the parameterless `Cast()` caller mid-stream; Tasks 6/7/8 have mutual references. Green-compile checkpoints land after Task 8 (all Fishing scripts present). This is stated in each affected task's verify step.
