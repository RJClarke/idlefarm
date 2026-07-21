# Wood Pile Visuals & Wood Cap Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Wood becomes capped at 1000; 4 world-placed log piles near the Wood Rack visually fill in sequence as wood grows, each swapping through 4 sprites by fill percentage; the Inventory popup shows an exact `X / 1,000` count; the Wood Rack popup shows a "Full" warning at cap.

**Architecture:** Pure math (clamp-on-deposit, at-cap check, per-pile fill fraction) lives in the existing `WoodcuttingMath` static class (EconomyCore), unit-tested. `CurrencyManager` gains a `maxWood` cap enforced in `AddWood`. `TreeNode` gains an early-return swing gate mirroring its existing axe-level gate. `LogStackVisual` is one reusable MonoBehaviour (cloned interaction pattern from `WoodRack.cs`) placed on 4 scene GameObjects. Two small UI edits ride on the same `CurrencyManager.MaxWood`/`OnWoodChanged` plumbing.

**Tech Stack:** Unity C# (MonoBehaviour, SpriteRenderer, Collider2D, UI Toolkit), NUnit EditMode tests via the `Temp/run_editmode_tests.request` file bridge.

## Global Constraints

- **Never edit serialized Unity files** (`.unity`, `.prefab`, `.asset`, `.meta`) as text — scene/asset changes go through the gladekit/unity MCP tools.
- **Hard cap (spec §2):** `AddWood` clamps deposits to `maxWood` (default 1000) — never silently drops overflow in a misleading way; a deposit that would overflow is truncated to land exactly at the cap.
- **Chopping refuses at cap (spec §2):** in `TreeNode.HandleTap`, a swing attempted while wood is *already* at the cap is blocked entirely (no hit, no tree damage, no `Shake()`) with a `wm.ShowHint(...)` — reusing the exact hint mechanism the axe-level gate already uses, not a new UI pattern.
- **Sprite thresholds are exact (spec §3), fraction `f`:** `f == 0` → hidden; `0 < f < 0.30` → `Trunk_Big_Vertical_32x32`; `0.30 <= f < 0.60` → `Trunk_Load_Vertical_32x32`; `0.60 <= f < 1.0` → `Trunk_Load_Medium_Vertical_32x32`; `f >= 1.0` → `Trunk_Load_Big_Vertical_32x32`.
- **Per-pile capacity is derived, not hardcoded** (spec §3): `CurrencyManager.Instance.MaxWood / 4`, so it stays correct if the cap tuning ever changes.
- **No emoji in UITK text** — invisible on Android (memory rule).
- `Debug.Log` only for important events; keep LogWarning/LogError.
- EditMode tests: `Assets/Tests/EditMode/`, run via `touch Temp/run_editmode_tests.request`, results in `Temp/editmode_test_results.txt`. Current baseline: **211 passing** — never finish a task with fewer.
- Commit after each task; end commit messages with the Co-Authored-By + Claude-Session trailer used on this branch.
- Back up `gamedata.json` before any play-mode test session (project rule); restore it after.

---

### Task 1: WoodcuttingMath additions — cap clamp, at-cap check, pile fill fraction

**Files:**
- Modify: `Assets/Scripts/EconomyCore/WoodcuttingMath.cs`
- Test: `Assets/Tests/EditMode/WoodcuttingMathTests.cs` (existing file — append new test methods to the existing test class)

**Interfaces:**
- Produces: `static int WoodcuttingMath.ClampToCap(int current, int add, int cap)` (returns the new total, `add <= 0` returns `current` unchanged); `static bool WoodcuttingMath.IsAtCap(int current, int cap)`; `static float WoodcuttingMath.PileFillFraction(int totalWood, int pileIndex, int capacityPerPile)` (0..1). Consumed by Task 2's `CurrencyManager`/`TreeNode` and Task 3's `LogStackVisual`.

- [ ] **Step 1: Write the failing tests**

Append to `Assets/Tests/EditMode/WoodcuttingMathTests.cs` (inside the existing `WoodcuttingMathTests` class — open the file first to find the class body and add these methods alongside the existing `[Test]` methods):

```csharp
    [Test]
    public void ClampToCap_DepositUnderCap_AddsFully()
    {
        Assert.AreEqual(150, WoodcuttingMath.ClampToCap(100, 50, 1000));
    }

    [Test]
    public void ClampToCap_DepositWouldOverflow_TruncatesToCap()
    {
        Assert.AreEqual(1000, WoodcuttingMath.ClampToCap(990, 15, 1000));
    }

    [Test]
    public void ClampToCap_AlreadyAtCap_StaysAtCap()
    {
        Assert.AreEqual(1000, WoodcuttingMath.ClampToCap(1000, 5, 1000));
    }

    [Test]
    public void ClampToCap_NonPositiveAdd_ReturnsCurrentUnchanged()
    {
        Assert.AreEqual(500, WoodcuttingMath.ClampToCap(500, 0, 1000));
        Assert.AreEqual(500, WoodcuttingMath.ClampToCap(500, -10, 1000));
    }

    [Test]
    public void IsAtCap_TrueOnlyAtOrAboveCap()
    {
        Assert.IsFalse(WoodcuttingMath.IsAtCap(999, 1000));
        Assert.IsTrue(WoodcuttingMath.IsAtCap(1000, 1000));
        Assert.IsTrue(WoodcuttingMath.IsAtCap(1001, 1000));
    }

    [Test]
    public void PileFillFraction_FirstPileFillsBeforeSecondStarts()
    {
        // capacityPerPile=250: pile 0 covers wood 0-250, pile 1 covers 250-500, etc.
        Assert.AreEqual(0f, WoodcuttingMath.PileFillFraction(0, 0, 250));
        Assert.AreEqual(0.4f, WoodcuttingMath.PileFillFraction(100, 0, 250), 0.0001f);
        Assert.AreEqual(1f, WoodcuttingMath.PileFillFraction(250, 0, 250));
        Assert.AreEqual(1f, WoodcuttingMath.PileFillFraction(300, 0, 250)); // pile 0 caps at its own 250
        Assert.AreEqual(0f, WoodcuttingMath.PileFillFraction(250, 1, 250)); // pile 1 hasn't started yet
        Assert.AreEqual(0.2f, WoodcuttingMath.PileFillFraction(300, 1, 250), 0.0001f);
    }

    [Test]
    public void PileFillFraction_ZeroCapacity_ReturnsZeroSafely()
    {
        Assert.AreEqual(0f, WoodcuttingMath.PileFillFraction(500, 0, 0));
    }
```

- [ ] **Step 2: Run tests, verify compile-fail (red)** — `touch Temp/run_editmode_tests.request`, check console for `CS0117` (no such member `ClampToCap` etc.) on `WoodcuttingMath`.

- [ ] **Step 3: Implement**

Add to `Assets/Scripts/EconomyCore/WoodcuttingMath.cs`, inside the `WoodcuttingMath` class (anywhere among the other static methods, e.g. after `SwingWood`):

```csharp
    // ── Wood cap (Reputation-adjacent feature: log pile visuals + hard cap) ───────────

    /// <summary>New wood total after depositing `add`, truncated so it never exceeds `cap`.
    /// A non-positive `add` leaves `current` unchanged.</summary>
    public static int ClampToCap(int current, int add, int cap)
    {
        if (add <= 0) return current;
        return Mathf.Min(current + add, cap);
    }

    /// <summary>True once wood is at or above the cap — used to refuse further chopping.</summary>
    public static bool IsAtCap(int current, int cap) => current >= cap;

    /// <summary>0..1 fill fraction for one of several equal-capacity log piles, filling in index
    /// order: pile 0 covers wood [0, capacityPerPile], pile 1 covers (capacityPerPile, 2x], etc.</summary>
    public static float PileFillFraction(int totalWood, int pileIndex, int capacityPerPile)
    {
        if (capacityPerPile <= 0) return 0f;
        int pileFloor = pileIndex * capacityPerPile;
        int fillWithinPile = Mathf.Clamp(totalWood - pileFloor, 0, capacityPerPile);
        return fillWithinPile / (float)capacityPerPile;
    }
```

- [ ] **Step 4: Run tests, verify green.** Expected 220/220 (211 + 9 new).

- [ ] **Step 5: Commit**

```bash
git add Assets/Scripts/EconomyCore/WoodcuttingMath.cs Assets/Tests/EditMode/WoodcuttingMathTests.cs
git commit -m "feat(wood-pile): WoodcuttingMath cap clamp, at-cap check, pile fill fraction"
```

---

### Task 2: Wood cap in CurrencyManager + chop-blocked gate in TreeNode

**Files:**
- Modify: `Assets/Scripts/CurrencyManager.cs:296-301` (`AddWood`)
- Modify: `Assets/Scripts/Woodcutting/TreeNode.cs:134-156` (`HandleTap`)

**Interfaces:**
- Consumes: `WoodcuttingMath.ClampToCap`, `WoodcuttingMath.IsAtCap` (Task 1).
- Produces: `public int MaxWood => maxWood;` on `CurrencyManager`, consumed by Task 3's `LogStackVisual` and Task 4's UI edits.

- [ ] **Step 1: Add the cap field and clamp AddWood**

In `Assets/Scripts/CurrencyManager.cs`, find the `[Header("Current Currency Values")]` block (near the top, alongside `currentWood`) and add a paired starting/cap field. Locate:

```csharp
    [SerializeField] private int currentWood = 0; // Woodcutting resource
```

Add immediately after it:

```csharp
    [SerializeField] private int maxWood = 1000; // Log-pile visual cap (spec: wood-pile-visuals-design)
```

Add a public getter near the other currency properties (alongside `public int Wood => currentWood;`):

```csharp
    public int MaxWood => maxWood;
```

Replace the `AddWood` method body:

```csharp
    public void AddWood(int amount)
    {
        if (amount <= 0) return;
        currentWood = WoodcuttingMath.ClampToCap(currentWood, amount, maxWood);
        OnWoodChanged?.Invoke(currentWood);
    }
```

- [ ] **Step 2: Add the swing gate in TreeNode.HandleTap**

In `Assets/Scripts/Woodcutting/TreeNode.cs`, in `HandleTap()`, insert a new early-return immediately after the existing axe-level gate (`if (!WoodcuttingMath.CanFell(...))` block) and before `int stage = ...`:

```csharp
        if (!WoodcuttingMath.CanFell(data.requiredAxeLevel, axe))
        {
            // Too hard for this axe: point the player at the fix, no chop progress. Levels are shown
            // 1-based to the player (bought axe = Lv 1), so a requiredAxeLevel of N reads as "level N+1".
            if (wm != null) wm.ShowHint(transform.position, $"Upgrade axe to level {data.requiredAxeLevel + 1}");
            return;
        }

        // Wood storage is full: refuse the swing entirely (no hit, no tree damage) rather than
        // waste chops against a tree for nothing once there's nowhere for the wood to go.
        if (CurrencyManager.Instance != null && WoodcuttingMath.IsAtCap(CurrencyManager.Instance.Wood, CurrencyManager.Instance.MaxWood))
        {
            if (wm != null) wm.ShowHint(transform.position, "Wood storage full!");
            return;
        }

        int stage = WoodcuttingMath.StageIndex(GrowthFraction(), data.stageCount);
```

- [ ] **Step 3: Compile check** — `mcp__unity-mcp__refresh_unity` (compile: request, force, wait_for_ready), `read_console` for errors.

- [ ] **Step 4: Run full test suite** — expect 220/220 (no new tests this task; regression guard).

- [ ] **Step 5: Commit**

```bash
git add Assets/Scripts/CurrencyManager.cs Assets/Scripts/Woodcutting/TreeNode.cs
git commit -m "feat(wood-pile): enforce 1000 wood cap, block chopping when full"
```

---

### Task 3: LogStackVisual + 4 world piles

**Files:**
- Create: `Assets/Scripts/Woodcutting/LogStackVisual.cs`

**Interfaces:**
- Consumes: `WoodcuttingMath.PileFillFraction` (Task 1), `CurrencyManager.MaxWood`/`Wood`/`OnWoodChanged` (Task 2), `WoodRackPopupUITK.Instance.Open()` (existing).
- Produces: nothing new (self-contained world prop, 4 scene instances differing only by `pileIndex`).

- [ ] **Step 1: Implement**

```csharp
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// One of 4 world-placed log piles near the Wood Rack, each representing an equal share of the
/// wood cap (spec: wood-pile-visuals-design §3). Fills in index order — pile 1 shows nothing
/// until pile 0 is full — and swaps through 4 sprites by fill fraction. Clickable exactly like
/// the Wood Rack itself (opens the same sell popup); interaction/press-feedback pattern cloned
/// from WoodRack.cs. A pile with zero fill is hidden and not interactable.
/// </summary>
[RequireComponent(typeof(SpriteRenderer))]
[RequireComponent(typeof(Collider2D))]
public class LogStackVisual : MonoBehaviour
{
    [Tooltip("0-3. Pile 0 fills first; capacity per pile = CurrencyManager.MaxWood / 4.")]
    [SerializeField] private int pileIndex;

    [Header("Sprites by fill fraction")]
    [SerializeField] private Sprite lowSprite;    // 0 < f < 0.30
    [SerializeField] private Sprite midSprite;    // 0.30 <= f < 0.60
    [SerializeField] private Sprite highSprite;   // 0.60 <= f < 1.0
    [SerializeField] private Sprite fullSprite;   // f >= 1.0

    [Header("Press Feedback")]
    [SerializeField] private float pressScale = 0.94f;
    [SerializeField] private float pressDuration = 0.08f;
    [SerializeField] private float releaseDuration = 0.18f;
    [SerializeField] private Color pressTint = new Color(0.78f, 0.78f, 0.78f, 1f);

    private const int PileCount = 4;

    private SpriteRenderer spriteRenderer;
    private Collider2D ownCollider;
    private Vector3 baseScale;
    private Color baseColor;
    private int scaleTweenId = -1;
    private bool isPressed;
    private bool interactable;

    private void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        ownCollider = GetComponent<Collider2D>();
        baseScale = transform.localScale;
        baseColor = spriteRenderer.color;
    }

    private void Start()
    {
        Refresh();
        if (CurrencyManager.Instance != null)
            CurrencyManager.Instance.OnWoodChanged += OnWoodChanged;
    }

    private void OnDestroy()
    {
        if (CurrencyManager.Instance != null)
            CurrencyManager.Instance.OnWoodChanged -= OnWoodChanged;
    }

    private void OnWoodChanged(int _) => Refresh();

    private void Refresh()
    {
        var cm = CurrencyManager.Instance;
        int capacityPerPile = cm != null ? cm.MaxWood / PileCount : 0;
        float fraction = cm != null ? WoodcuttingMath.PileFillFraction(cm.Wood, pileIndex, capacityPerPile) : 0f;

        interactable = fraction > 0f;
        spriteRenderer.enabled = interactable;
        ownCollider.enabled = interactable;
        if (!interactable) return;

        spriteRenderer.sprite = fraction >= 1f ? fullSprite
            : fraction >= 0.60f ? highSprite
            : fraction >= 0.30f ? midSprite
            : lowSprite;
    }

    private void Update()
    {
        if (!interactable) return;
        if (!TryReadPointer(out Vector2 screenPos, out bool justPressed, out bool justReleased, out bool held))
            return;

        if (UITapBlocker.PointerOverUI(screenPos)) { CancelPress(); return; }

        if (justPressed && !isPressed && CanInteract() && PointerHitsSelf(screenPos))
        {
            isPressed = true;
            spriteRenderer.color = pressTint * baseColor;
            DoTween(baseScale * pressScale, pressDuration);
            return;
        }
        if (held && isPressed && !PointerHitsSelf(screenPos)) { CancelPress(); return; }
        if (justReleased && isPressed)
        {
            bool overSelf = PointerHitsSelf(screenPos);
            CancelPress();
            if (overSelf && CanInteract()) HandleClick();
        }
    }

    private static bool TryReadPointer(out Vector2 screenPos, out bool justPressed, out bool justReleased, out bool held)
    {
        screenPos = default; justPressed = false; justReleased = false; held = false;
        if (Touchscreen.current != null && Touchscreen.current.primaryTouch.press.isPressed)
        {
            screenPos = Touchscreen.current.primaryTouch.position.ReadValue();
            justPressed = Touchscreen.current.primaryTouch.press.wasPressedThisFrame;
            held = true;
            return true;
        }
        if (Touchscreen.current != null && Touchscreen.current.primaryTouch.press.wasReleasedThisFrame)
        {
            screenPos = Touchscreen.current.primaryTouch.position.ReadValue();
            justReleased = true;
            return true;
        }
        if (Mouse.current != null)
        {
            screenPos = Mouse.current.position.ReadValue();
            justPressed = Mouse.current.leftButton.wasPressedThisFrame;
            justReleased = Mouse.current.leftButton.wasReleasedThisFrame;
            held = Mouse.current.leftButton.isPressed;
            return justPressed || justReleased || held;
        }
        return false;
    }

    private bool PointerHitsSelf(Vector2 screenPos)
    {
        Camera cam = Camera.main;
        if (cam == null || ownCollider == null) return false;
        Vector3 world = cam.ScreenToWorldPoint(new Vector3(screenPos.x, screenPos.y, -cam.transform.position.z));
        return Physics2D.OverlapPoint(world) == ownCollider;
    }

    private void CancelPress()
    {
        if (!isPressed) return;
        isPressed = false;
        spriteRenderer.color = baseColor;
        DoTween(baseScale, releaseDuration);
    }

    private bool CanInteract()
    {
        CameraPanController pan = Camera.main != null ? Camera.main.GetComponent<CameraPanController>() : null;
        return pan == null || !pan.IsPanning;
    }

    private void HandleClick()
    {
        if (WoodRackPopupUITK.Instance != null) WoodRackPopupUITK.Instance.Open();
    }

    private void DoTween(Vector3 target, float duration)
    {
        if (scaleTweenId != -1) LeanTween.cancel(scaleTweenId);
        scaleTweenId = LeanTween.scale(gameObject, target, duration)
            .setEase(LeanTweenType.easeOutQuad)
            .setOnComplete(() => scaleTweenId = -1)
            .id;
    }
}
```

- [ ] **Step 2: Compile check** — `refresh_unity` (compile: request, force), `read_console` for errors.

- [ ] **Step 3: Run full test suite** — expect 220/220.

- [ ] **Step 4: Create the 4 scene GameObjects** (MCP `execute_code`), positioned in a row near the Wood Rack at (31, -41):

```csharp
var scene = UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene();
if (scene.name != "FarmMain") return "WRONG SCENE OPEN: " + scene.name;
if (UnityEngine.Object.FindFirstObjectByType<LogStackVisual>() != null) return "already exists";

var low = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/Buildings/Wood/Trunk_Big_Vertical_32x32.png");
var mid = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/Buildings/Wood/Trunk_Load_Vertical_32x32.png");
var high = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/Buildings/Wood/Trunk_Load_Medium_Vertical_32x32.png");
var full = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/Buildings/Wood/Trunk_Load_Big_Vertical_32x32.png");
if (low == null || mid == null || high == null || full == null) return "one or more Trunk sprites not found";

string report = "";
for (int i = 0; i < 4; i++)
{
    var go = new GameObject("LogStack_" + i);
    var sr = go.AddComponent<SpriteRenderer>();
    var col = go.AddComponent<BoxCollider2D>();
    var vis = go.AddComponent<LogStackVisual>();
    go.transform.position = new Vector3(34f + i * 1.5f, -41f, 0f); // row beside the Wood Rack at (31,-41)

    var so = new UnityEditor.SerializedObject(vis);
    so.FindProperty("pileIndex").intValue = i;
    so.FindProperty("lowSprite").objectReferenceValue = low;
    so.FindProperty("midSprite").objectReferenceValue = mid;
    so.FindProperty("highSprite").objectReferenceValue = high;
    so.FindProperty("fullSprite").objectReferenceValue = full;
    so.ApplyModifiedProperties();
    report += $"pile{i} at {go.transform.position}\n";
}

UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);
UnityEditor.SceneManagement.EditorSceneManager.SaveOpenScenes();
return report;
```

Placement is approximate — verify via screenshot in Task 5 and nudge positions/spacing if piles overlap the Rack or each other; re-run `MarkSceneDirty`/`SaveOpenScenes` after any adjustment.

- [ ] **Step 5: Commit**

```bash
git add Assets/Scripts/Woodcutting/LogStackVisual.cs Assets/Scenes/FarmMain.unity
git commit -m "feat(wood-pile): LogStackVisual + 4 world piles beside the Wood Rack"
```

---

### Task 4: Inventory "X / 1,000" row + Wood Rack "Full" warning

**Files:**
- Modify: `Assets/Scripts/UI/InventoryPopupUITK.cs:155-158` (wood row) and `:231-246` (`AddSellRow`)
- Modify: `Assets/Scripts/UI/WoodRackPopupUITK.cs:191-198` (`Refresh`)
- Modify: `Assets/UI/WoodRackPopupUITK/WoodRackPopupUITK.uss` (new `.wood-count--full` class)

**Interfaces:**
- Consumes: `CurrencyManager.MaxWood` (Task 2).
- Produces: nothing new.

- [ ] **Step 1: Add an optional value-text override to AddSellRow**

In `Assets/Scripts/UI/InventoryPopupUITK.cs`, change the `AddSellRow` signature and the value-label line:

```csharp
    private void AddSellRow(string rowId, string iconClass, string name, int count,
        bool enabled, System.Func<int, string> payoutLabel, System.Action<int> onSell, string valueTextOverride = null)
    {
        var row = new VisualElement(); row.AddToClassList("inv-row");
        var icon = new VisualElement(); icon.AddToClassList("inv-icon"); icon.AddToClassList(iconClass);
        icon.pickingMode = PickingMode.Ignore;
        var nameLbl = new Label(name); nameLbl.AddToClassList("inv-name");
        var valLbl = new Label(valueTextOverride ?? count.ToString("N0")); valLbl.AddToClassList("inv-value");
```

(Only the `private void AddSellRow(...)` line and the `var valLbl = ...` line change; everything else in the method body stays as-is.)

- [ ] **Step 2: Pass the wood cap into the wood row's call**

In the same file, change the wood row call:

```csharp
        AddSellRow("wood", "inv-icon--wood", "Wood", cm.Wood,
            enabled: inRun && woodPrice > 0 && cm.Wood > 0,
            payoutLabel: n => $"+{WoodcuttingMath.SellValue(n, woodPrice)} Cash",
            onSell: n => { if (cm.SpendWood(n)) cm.AddMoney(WoodcuttingMath.SellValue(n, woodPrice)); },
            valueTextOverride: $"{cm.Wood:N0} / {cm.MaxWood:N0}");
```

- [ ] **Step 3: Add the Wood Rack "Full" warning state**

In `Assets/Scripts/UI/WoodRackPopupUITK.cs`, in `Refresh()`, change:

```csharp
        if (woodCount != null) woodCount.text = $"{wood}"; // Log icon precedes it (see .wood-icon)
```

to:

```csharp
        bool isFull = cm != null && wood >= cm.MaxWood;
        if (woodCount != null)
        {
            woodCount.text = isFull ? $"{wood} (Full)" : $"{wood}";
            woodCount.EnableInClassList("wood-count--full", isFull);
        }
```

- [ ] **Step 4: Add the warning USS class**

Append to `Assets/UI/WoodRackPopupUITK/WoodRackPopupUITK.uss` (near the existing `.wood-count` rule, ~line 110):

```css
.wood-count--full {
    color: rgb(178, 42, 30);
}
```

- [ ] **Step 5: Compile check + full test run** — zero errors, 220/220.

- [ ] **Step 6: Commit**

```bash
git add Assets/Scripts/UI/InventoryPopupUITK.cs Assets/Scripts/UI/WoodRackPopupUITK.cs Assets/UI/WoodRackPopupUITK/WoodRackPopupUITK.uss
git commit -m "feat(wood-pile): Inventory X/1000 wood count + Wood Rack Full warning"
```

---

### Task 5: Play-mode smoke test

**Files:** none (verification only; fix-forward anything found, then commit fixes).

- [ ] **Step 1: Back up the play save.**

- [ ] **Step 2: Enter play mode**; confirm `CurrencyManager.Instance.MaxWood == 1000` and 4 `LogStackVisual` instances exist in the scene.

- [ ] **Step 3: Drive the loop via `execute_code`:**
  1. `CurrencyManager.Instance.AddWood(100)` — assert `Wood == 100`, pile 0 shows `lowSprite` (fraction 0.4 → actually mid-range; verify against the exact threshold math: 100/250=0.4 → `midSprite` per `0.30 <= f < 0.60`), piles 1-3 still hidden (`SpriteRenderer.enabled == false`).
  2. `AddWood(900)` more (total 1000) — assert `Wood == 1000`, all 4 piles show `fullSprite`, `IsAtCap` true.
  3. `AddWood(50)` again — assert `Wood` stays `1000` (overflow truncated, not 1050).
  4. Find a `TreeNode` in the Woods (or use the existing dev-menu wood grant instead if spawning a tree headlessly is impractical) and confirm `HandleTap()` at full wood produces a `wm.ShowHint` call and no wood/hit change — this can be checked via `Debug.Log`/console inspection or by asserting `Wood` is unchanged after the call.
  5. Pan to Farm/Woods (wherever the Wood Rack sits), screenshot to confirm all 4 piles render distinctly and don't overlap the Rack or each other; adjust positions via `execute_code` + re-save if needed.
  6. Open `WoodRackPopupUITK` and `InventoryPopupUITK`, screenshot both: Rack shows `"1000 (Full)"` in the warning color; Inventory shows `"1,000 / 1,000"`.
  7. `CurrencyManager.Instance.SpendWood(500)` — assert Rack's warning clears (back to normal color, no "(Full)" suffix) and 2 piles revert toward lower sprites.

- [ ] **Step 4: Exit play mode**, check console for zero new exceptions.

- [ ] **Step 5: Restore the real save**; delete temp screenshots.

- [ ] **Step 6: Commit any fixes**

```bash
git add -A Assets/Scripts Assets/Tests Assets/Scenes Assets/UI
git commit -m "fix(wood-pile): play-mode smoke test fixes"
```
