# Reputation Phase 2: Town Requests Board Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Three ever-present town delivery quests (Easy/Medium/Hard, independent 8h cooldowns) that consume items via a reusable `DeliveryRequest`, grant Reputation toward skill points, and are visible/actionable through an always-on Market rep bar and a requests popup.

**Architecture:** Pure math + data (`ReputationMath`, `RequestGenerationMath`, `ReputationCore`, `DeliveryRequest`) in EconomyCore, unit-tested. `ReputationManager` singleton wraps the core, rolls quests from a `RequestCatalog` ScriptableObject filtered by live unlock state, and polls slot expiry. `DeliveryService` is a static facade that knows where every requestable item type lives. Both new UI pieces (`ReputationBarUITK`, `TownRequestsPopupUITK`) are fully code-driven UIDocuments in the proven `ToastManager` style — own runtime `PanelSettings`, no UXML/USS assets, no scene wiring beyond placing the component. Spec: `docs/superpowers/specs/2026-07-19-reputation-design.md` (§2 rules, §3 board, §6 architecture).

**Tech Stack:** Unity C# (singleton managers, JsonUtility saves), fully code-driven UI Toolkit (ToastManager pattern), NUnit EditMode tests via the `Temp/run_editmode_tests.request` file bridge.

## Global Constraints

- **Never edit serialized Unity files** (`.unity`, `.prefab`, `.asset`, `.meta`) as text — scene/asset changes go through the gladekit/unity MCP tools.
- **No emoji in UITK text** — invisible on Android (memory rule). Icons are text glyphs (e.g. "×") or USS/inline-style colors, not sprites, for these code-driven components.
- **Binding economy rules (spec §2):** Fulfilling a request always deducts items via `DeliveryService` before granting rep — never grant rep on a failed spend. Skip costs Gems, never blocks on affordability silently (button must reflect affordability).
- `Debug.Log` only for important events (fulfill, skip, point awarded); keep LogWarning/LogError.
- EditMode tests: `Assets/Tests/EditMode/`, run via `touch Temp/run_editmode_tests.request`, results in `Temp/editmode_test_results.txt`. Current baseline: **185 passing** (post Phase 1) — never finish a task with fewer.
- Commit after each task; end commit messages with the Co-Authored-By + Claude-Session trailer used on this branch.
- Deviations from spec made for v1 scope (documented here, not hidden): **jars excluded** from board requests (no per-crop "spend N jars" API exists yet on `CanneryManager`); **single item type per request** in v1 (spec's "and/or two item types" resolved to "or"); crop pool draws from `CropDatabase.startingCrops` only (no generalized crop-unlock system exists yet to query); egg requests always eligible (no dedicated "has ever owned an egg animal" check).

---

### Task 1: ReputationMath (EconomyCore pure math)

**Files:**
- Create: `Assets/Scripts/EconomyCore/ReputationMath.cs`
- Test: `Assets/Tests/EditMode/ReputationMathTests.cs`

**Interfaces:**
- Produces: `static int ReputationMath.PointCost(int pointNumber)`; `static void ReputationMath.ApplyGain(int barBefore, int pointsBefore, int gain, out int barAfter, out int pointsAwarded)`; `static int ReputationMath.SkipCost(int consecutiveSkips)`. Consumed by Task 3's `ReputationCore`.

- [ ] **Step 1: Write the failing tests**

```csharp
using NUnit.Framework;

public class ReputationMathTests
{
    [Test]
    public void PointCost_MatchesSpecNumbers()
    {
        Assert.AreEqual(100, ReputationMath.PointCost(1));
        Assert.AreEqual(460, ReputationMath.PointCost(25));
        Assert.AreEqual(2335, ReputationMath.PointCost(150));
    }

    [Test]
    public void ApplyGain_ExactBoundaryAwardsOnePointAndZeroesBar()
    {
        ReputationMath.ApplyGain(0, 0, 100, out int barAfter, out int awarded);
        Assert.AreEqual(0, barAfter);
        Assert.AreEqual(1, awarded);
    }

    [Test]
    public void ApplyGain_LargeGainAwardsMultiplePoints()
    {
        // point1=100, point2=115, point3=130 -> 345 exactly clears 3 points
        ReputationMath.ApplyGain(0, 0, 345, out int barAfter, out int awarded);
        Assert.AreEqual(0, barAfter);
        Assert.AreEqual(3, awarded);
    }

    [Test]
    public void ApplyGain_PartialGainAwardsNoPoints()
    {
        ReputationMath.ApplyGain(0, 0, 60, out int barAfter, out int awarded);
        Assert.AreEqual(60, barAfter);
        Assert.AreEqual(0, awarded);
    }

    [Test]
    public void ApplyGain_ContinuesFromExistingProgress()
    {
        // Already 90 rep into point 1 (needs 100). +20 crosses it with 10 left over.
        ReputationMath.ApplyGain(90, 0, 20, out int barAfter, out int awarded);
        Assert.AreEqual(10, barAfter);
        Assert.AreEqual(1, awarded);
    }

    [Test]
    public void SkipCost_FollowsLadderAndCaps()
    {
        Assert.AreEqual(25, ReputationMath.SkipCost(0));
        Assert.AreEqual(50, ReputationMath.SkipCost(1));
        Assert.AreEqual(100, ReputationMath.SkipCost(2));
        Assert.AreEqual(150, ReputationMath.SkipCost(3));
        Assert.AreEqual(250, ReputationMath.SkipCost(4));
        Assert.AreEqual(250, ReputationMath.SkipCost(9)); // capped
    }
}
```

- [ ] **Step 2: Run tests, verify compile-fail (red)** — `touch Temp/run_editmode_tests.request`, check `Temp/editmode_test_results.txt` / console for `CS0103: ReputationMath`.

- [ ] **Step 3: Implement**

```csharp
using UnityEngine;

/// <summary>
/// Pure reputation math (Reputation Phase 2, spec §3.4/§3.2): the lifetime point-cost curve and
/// the consecutive-skip cost ladder. No Unity scene dependency.
/// </summary>
public static class ReputationMath
{
    private const int BaseCost = 85;
    private const int CostPerPoint = 15;

    /// <summary>Rep cost of the Nth lifetime point (1-based).</summary>
    public static int PointCost(int pointNumber) => BaseCost + CostPerPoint * Mathf.Max(1, pointNumber);

    /// <summary>
    /// Applies a rep gain to the bar, awarding as many points as it covers. Extra progress
    /// beyond the last point carries forward as <paramref name="barAfter"/>.
    /// </summary>
    public static void ApplyGain(int barBefore, int pointsBefore, int gain, out int barAfter, out int pointsAwarded)
    {
        int bar = Mathf.Max(0, barBefore) + Mathf.Max(0, gain);
        int points = Mathf.Max(0, pointsBefore);
        int awarded = 0;
        int cost = PointCost(points + 1);
        while (bar >= cost)
        {
            bar -= cost;
            points++;
            awarded++;
            cost = PointCost(points + 1);
        }
        barAfter = bar;
        pointsAwarded = awarded;
    }

    private static readonly int[] SkipLadder = { 25, 50, 100, 150, 250 };

    /// <summary>Gem cost of the next skip, given how many skips have happened since the last fulfill.</summary>
    public static int SkipCost(int consecutiveSkips)
        => SkipLadder[Mathf.Clamp(consecutiveSkips, 0, SkipLadder.Length - 1)];
}
```

- [ ] **Step 4: Run tests, verify green.** Expected 191/191 (185 + 6 new).

- [ ] **Step 5: Commit**

```bash
git add Assets/Scripts/EconomyCore/ReputationMath.cs Assets/Tests/EditMode/ReputationMathTests.cs
git commit -m "feat(reputation): ReputationMath point-cost curve + skip ladder"
```

---

### Task 2: RequestGenerationMath (EconomyCore pure math)

**Files:**
- Create: `Assets/Scripts/EconomyCore/RequestGenerationMath.cs`
- Test: `Assets/Tests/EditMode/RequestGenerationMathTests.cs`

**Interfaces:**
- Produces: `struct RequestItemOption { string itemId; int minCount; int maxCount; int weight; }`; `static bool RequestGenerationMath.TryRoll(RequestItemOption[] pool, System.Random rng, out string itemId, out int count)`. Consumed by Task 5's `ReputationManager.BuildPool`/`RollSlot`.

- [ ] **Step 1: Write the failing tests**

```csharp
using NUnit.Framework;

public class RequestGenerationMathTests
{
    [Test]
    public void TryRoll_EmptyPool_ReturnsFalse()
    {
        bool ok = RequestGenerationMath.TryRoll(new RequestItemOption[0], new System.Random(1), out _, out _);
        Assert.IsFalse(ok);
    }

    [Test]
    public void TryRoll_SingleOption_AlwaysPicksIt()
    {
        var pool = new[] { new RequestItemOption { itemId = "Blueberry", minCount = 10, maxCount = 20, weight = 1 } };
        bool ok = RequestGenerationMath.TryRoll(pool, new System.Random(42), out string id, out int count);
        Assert.IsTrue(ok);
        Assert.AreEqual("Blueberry", id);
        Assert.GreaterOrEqual(count, 10);
        Assert.LessOrEqual(count, 20);
    }

    [Test]
    public void TryRoll_ZeroWeightItem_NeverSelected()
    {
        var pool = new[]
        {
            new RequestItemOption { itemId = "Never", minCount = 1, maxCount = 1, weight = 0 },
            new RequestItemOption { itemId = "Always", minCount = 1, maxCount = 1, weight = 10 },
        };
        for (int seed = 0; seed < 200; seed++)
        {
            RequestGenerationMath.TryRoll(pool, new System.Random(seed), out string id, out _);
            Assert.AreEqual("Always", id);
        }
    }

    [Test]
    public void TryRoll_SameSeed_IsDeterministic()
    {
        var pool = new[]
        {
            new RequestItemOption { itemId = "A", minCount = 1, maxCount = 100, weight = 1 },
            new RequestItemOption { itemId = "B", minCount = 1, maxCount = 100, weight = 1 },
            new RequestItemOption { itemId = "C", minCount = 1, maxCount = 100, weight = 1 },
        };
        RequestGenerationMath.TryRoll(pool, new System.Random(7), out string id1, out int c1);
        RequestGenerationMath.TryRoll(pool, new System.Random(7), out string id2, out int c2);
        Assert.AreEqual(id1, id2);
        Assert.AreEqual(c1, c2);
    }

    [Test]
    public void TryRoll_CountNeverBelowMin()
    {
        var pool = new[] { new RequestItemOption { itemId = "X", minCount = 5, maxCount = 5, weight = 1 } };
        RequestGenerationMath.TryRoll(pool, new System.Random(3), out _, out int count);
        Assert.AreEqual(5, count);
    }
}
```

- [ ] **Step 2: Run tests, verify compile-fail (red).**

- [ ] **Step 3: Implement**

```csharp
using UnityEngine;

/// <summary>One weighted, concrete item choice a request slot can roll (Reputation Phase 2, spec §3.3).</summary>
public struct RequestItemOption
{
    public string itemId;
    public int minCount;
    public int maxCount;
    public int weight;
}

/// <summary>
/// Pure weighted-roll math for Town Requests generation. The caller (ReputationManager) is
/// responsible for filtering the pool down to what the player can currently produce before
/// calling this — this class only picks among options it's handed.
/// </summary>
public static class RequestGenerationMath
{
    public static bool TryRoll(RequestItemOption[] pool, System.Random rng, out string itemId, out int count)
    {
        itemId = null;
        count = 0;
        if (pool == null || pool.Length == 0 || rng == null) return false;

        int totalWeight = 0;
        for (int i = 0; i < pool.Length; i++) totalWeight += Mathf.Max(0, pool[i].weight);
        if (totalWeight <= 0) return false;

        int roll = rng.Next(0, totalWeight);
        int acc = 0;
        for (int i = 0; i < pool.Length; i++)
        {
            acc += Mathf.Max(0, pool[i].weight);
            if (roll >= acc) continue;

            itemId = pool[i].itemId;
            int min = Mathf.Max(1, pool[i].minCount);
            int max = Mathf.Max(min, pool[i].maxCount);
            count = rng.Next(min, max + 1); // System.Random.Next max is exclusive
            return true;
        }
        return false;
    }
}
```

- [ ] **Step 4: Run tests, verify green.** Expected 196/196.

- [ ] **Step 5: Commit**

```bash
git add Assets/Scripts/EconomyCore/RequestGenerationMath.cs Assets/Tests/EditMode/RequestGenerationMathTests.cs
git commit -m "feat(reputation): RequestGenerationMath weighted item roll"
```

---

### Task 3: ReputationCore (delivery data + slot/rep state, EconomyCore)

**Files:**
- Create: `Assets/Scripts/EconomyCore/ReputationCore.cs`
- Test: `Assets/Tests/EditMode/ReputationCoreTests.cs`

**Interfaces:**
- Consumes: `ReputationMath` (Task 1).
- Produces: `class DeliveryLineItem { string itemId; int count; }`; `class DeliveryRequest { DeliveryLineItem[] items; int repReward; string requesterName; string flavorText; }`; `class ReputationCore` with `int BarProgress`, `int PointsEarned`, `int UnspentPoints`, `int ConsecutiveSkips` (all get-only), `DeliveryRequest GetSlotRequest(int slot)`, `bool IsSlotOnCooldown(int slot, long nowUtcTicks)`, `long GetSlotCooldownEndUtcTicks(int slot)`, `void SetSlotRequest(int slot, DeliveryRequest request)`, `int AddRep(int gain)` (returns points awarded), `void OnFulfilled(int slot, long nowUtcTicks, long cooldownTicks)`, `void OnSkipped()`, `int NextSkipCost()`, `void Import(int bar, int points, int unspent, int skips, DeliveryRequest[] requests, long[] cooldowns)`, `DeliveryRequest[] ExportRequests()`, `long[] ExportCooldowns()`. Consumed by Task 5's `ReputationManager` and Task 4's save wiring.

- [ ] **Step 1: Write the failing tests**

```csharp
using NUnit.Framework;

public class ReputationCoreTests
{
    private static DeliveryRequest MakeRequest(string itemId = "Blueberry", int count = 10, int reward = 30)
        => new DeliveryRequest
        {
            items = new[] { new DeliveryLineItem { itemId = itemId, count = count } },
            repReward = reward,
            requesterName = "Marta",
            flavorText = "",
        };

    [Test]
    public void AddRep_AwardsPointsViaReputationMath()
    {
        var core = new ReputationCore();
        int awarded = core.AddRep(100); // exactly point 1's cost
        Assert.AreEqual(1, awarded);
        Assert.AreEqual(1, core.PointsEarned);
        Assert.AreEqual(1, core.UnspentPoints);
        Assert.AreEqual(0, core.BarProgress);
    }

    [Test]
    public void SetSlotRequest_ThenGetSlotRequest_RoundTrips()
    {
        var core = new ReputationCore();
        var req = MakeRequest();
        core.SetSlotRequest(0, req);
        Assert.AreEqual(req, core.GetSlotRequest(0));
        Assert.IsFalse(core.IsSlotOnCooldown(0, System.DateTime.UtcNow.Ticks));
    }

    [Test]
    public void OnFulfilled_ClearsRequestStartsCooldownResetsSkips()
    {
        var core = new ReputationCore();
        core.SetSlotRequest(1, MakeRequest());
        core.OnSkipped();
        core.OnSkipped();
        Assert.AreEqual(2, core.ConsecutiveSkips);

        long now = 1_000_000L;
        long cooldownTicks = System.TimeSpan.FromHours(8).Ticks;
        core.OnFulfilled(1, now, cooldownTicks);

        Assert.IsNull(core.GetSlotRequest(1));
        Assert.AreEqual(0, core.ConsecutiveSkips);
        Assert.IsTrue(core.IsSlotOnCooldown(1, now));
        Assert.IsFalse(core.IsSlotOnCooldown(1, now + cooldownTicks + 1));
    }

    [Test]
    public void OnSkipped_IncrementsAndDrivesNextSkipCost()
    {
        var core = new ReputationCore();
        Assert.AreEqual(25, core.NextSkipCost());
        core.OnSkipped();
        Assert.AreEqual(50, core.NextSkipCost());
        core.OnSkipped();
        Assert.AreEqual(100, core.NextSkipCost());
    }

    [Test]
    public void ExportRequests_NeverNull_EmptySlotBecomesSentinel()
    {
        var core = new ReputationCore();
        var exported = core.ExportRequests();
        Assert.AreEqual(3, exported.Length);
        for (int i = 0; i < 3; i++)
        {
            Assert.IsNotNull(exported[i]);
            Assert.IsNotNull(exported[i].items);
            Assert.AreEqual(0, exported[i].items.Length);
        }
    }

    [Test]
    public void Import_SentinelEmptyRequest_BecomesNullInMemory()
    {
        var core = new ReputationCore();
        var sentinel = new DeliveryRequest { items = new DeliveryLineItem[0], repReward = 0, requesterName = "", flavorText = "" };
        core.Import(0, 0, 0, 0, new[] { sentinel, sentinel, sentinel }, new long[] { 0, 0, 0 });
        Assert.IsNull(core.GetSlotRequest(0));
    }

    [Test]
    public void ExportImport_RoundTripsActiveRequestAndCooldown()
    {
        var core = new ReputationCore();
        var req = MakeRequest("Tomato", 5, 60);
        core.SetSlotRequest(2, req);
        core.AddRep(250);

        var requests = core.ExportRequests();
        var cooldowns = core.ExportCooldowns();

        var core2 = new ReputationCore();
        core2.Import(core.BarProgress, core.PointsEarned, core.UnspentPoints, core.ConsecutiveSkips, requests, cooldowns);

        Assert.AreEqual("Tomato", core2.GetSlotRequest(2).items[0].itemId);
        Assert.AreEqual(5, core2.GetSlotRequest(2).items[0].count);
        Assert.AreEqual(core.PointsEarned, core2.PointsEarned);
        Assert.AreEqual(core.UnspentPoints, core2.UnspentPoints);
    }

    [Test]
    public void Import_NullArrays_IsSafeAndEmpty()
    {
        var core = new ReputationCore();
        core.Import(0, 0, 0, 0, null, null);
        for (int i = 0; i < 3; i++)
        {
            Assert.IsNull(core.GetSlotRequest(i));
            Assert.IsFalse(core.IsSlotOnCooldown(i, System.DateTime.UtcNow.Ticks));
        }
    }
}
```

- [ ] **Step 2: Run tests, verify compile-fail (red).**

- [ ] **Step 3: Implement**

```csharp
using System;
using UnityEngine;

/// <summary>One (itemId, count) line of a delivery request.</summary>
[Serializable]
public class DeliveryLineItem
{
    public string itemId;
    public int count;
}

/// <summary>
/// A reusable "deliver these item stacks -> reward" requirement (Reputation Phase 2, spec §3.5).
/// Board slots are the only producer today; future NPC fulfillment quests construct the same
/// struct and fulfill it through the same DeliveryService, without touching board code.
/// </summary>
[Serializable]
public class DeliveryRequest
{
    public DeliveryLineItem[] items;
    public int repReward;
    public string requesterName;
    public string flavorText;
}

/// <summary>
/// Pure reputation + board-slot state: rep bar progress, lifetime points, unspent points, the
/// consecutive-skip counter, and the 3 fixed-difficulty slots (0=Easy,1=Medium,2=Hard). Rolling
/// new requests and spending items are NOT this class's job (they need live Unity singletons for
/// unlock state and item stores) — this only stores/transitions state given the outcome.
/// Spec: 2026-07-19-reputation-design.md §3.2/§3.4.
/// </summary>
public class ReputationCore
{
    public int BarProgress { get; private set; }
    public int PointsEarned { get; private set; }
    public int UnspentPoints { get; private set; }
    public int ConsecutiveSkips { get; private set; }

    private readonly DeliveryRequest[] slotRequests = new DeliveryRequest[3];
    private readonly long[] slotCooldownEndUtcTicks = new long[3];

    public DeliveryRequest GetSlotRequest(int slot) => slotRequests[slot];
    public long GetSlotCooldownEndUtcTicks(int slot) => slotCooldownEndUtcTicks[slot];
    public bool IsSlotOnCooldown(int slot, long nowUtcTicks) => slotCooldownEndUtcTicks[slot] > nowUtcTicks;

    /// <summary>Sets an active request on a slot and clears any cooldown (used for initial rolls, re-rolls, and skips).</summary>
    public void SetSlotRequest(int slot, DeliveryRequest request)
    {
        slotRequests[slot] = request;
        slotCooldownEndUtcTicks[slot] = 0;
    }

    public int AddRep(int gain)
    {
        ReputationMath.ApplyGain(BarProgress, PointsEarned, gain, out int barAfter, out int awarded);
        BarProgress = barAfter;
        PointsEarned += awarded;
        UnspentPoints += awarded;
        return awarded;
    }

    /// <summary>Call after the caller has already spent the request's items successfully.</summary>
    public void OnFulfilled(int slot, long nowUtcTicks, long cooldownTicks)
    {
        slotRequests[slot] = null;
        slotCooldownEndUtcTicks[slot] = nowUtcTicks + cooldownTicks;
        ConsecutiveSkips = 0;
    }

    public void OnSkipped() => ConsecutiveSkips++;

    public int NextSkipCost() => ReputationMath.SkipCost(ConsecutiveSkips);

    public void Import(int barProgress, int pointsEarned, int unspentPoints, int consecutiveSkips,
        DeliveryRequest[] requests, long[] cooldowns)
    {
        BarProgress = Mathf.Max(0, barProgress);
        PointsEarned = Mathf.Max(0, pointsEarned);
        UnspentPoints = Mathf.Max(0, unspentPoints);
        ConsecutiveSkips = Mathf.Max(0, consecutiveSkips);
        for (int i = 0; i < 3; i++)
        {
            DeliveryRequest r = (requests != null && i < requests.Length) ? requests[i] : null;
            slotRequests[i] = (r != null && r.items != null && r.items.Length > 0) ? r : null;
            slotCooldownEndUtcTicks[i] = (cooldowns != null && i < cooldowns.Length) ? cooldowns[i] : 0;
        }
    }

    /// <summary>Never null (JsonUtility-safe): an inactive slot exports as an empty-items sentinel.</summary>
    public DeliveryRequest[] ExportRequests()
    {
        var result = new DeliveryRequest[3];
        for (int i = 0; i < 3; i++)
            result[i] = slotRequests[i] ?? new DeliveryRequest { items = new DeliveryLineItem[0], repReward = 0, requesterName = "", flavorText = "" };
        return result;
    }

    public long[] ExportCooldowns() => (long[])slotCooldownEndUtcTicks.Clone();
}
```

- [ ] **Step 4: Run tests, verify green.** Expected 204/204 (196 + 8 new).

- [ ] **Step 5: Commit**

```bash
git add Assets/Scripts/EconomyCore/ReputationCore.cs Assets/Tests/EditMode/ReputationCoreTests.cs
git commit -m "feat(reputation): ReputationCore delivery-request + slot state"
```

---

### Task 4: RequestCatalog data asset + ReputationManager + save wiring

**Files:**
- Create: `Assets/Scripts/Reputation/RequestCatalog.cs`
- Create: `Assets/Scripts/Reputation/ReputationManager.cs`
- Modify: `Assets/Scripts/GameData.cs` (fields + constructor + `RequestSlotSave` class)
- Modify: `Assets/Scripts/SaveManager.cs` (capture/load, alongside the Phase 1 `ItemInventoryManager` lines)
- Modify: `Assets/Scripts/AutoSaveManager.cs` (subscribe/unsubscribe `OnChanged`)
- Test: `Assets/Tests/EditMode/GameDataReputationSerializationTests.cs` (JsonUtility null-safety belt-and-suspenders check)

**Interfaces:**
- Consumes: `ReputationCore`/`DeliveryRequest` (Task 3), `RequestGenerationMath` (Task 2), `ItemInventoryManager` (Phase 1), `CropDatabase`, `FishingManager.HasPole`, `SmokehouseManager.IsBuilt`, `WoodcuttingManager.HasAxe`, `FishTiers.Count`.
- Produces: singleton `ReputationManager.Instance`; `event Action OnChanged`; `event Action<int> OnPointsAwarded`; `int BarProgress`, `int PointsEarned`, `int UnspentPoints`, `int NextPointCost`, `float BarProgress01`; `DeliveryRequest GetSlotRequest(int slot)`; `bool IsSlotOnCooldown(int slot)`; `long GetSlotCooldownRemainingSeconds(int slot)`; `int NextSkipCost`; `bool TryFulfill(int slot)`; `bool TrySkip(int slot)`; `CaptureTo(GameData)`/`LoadFrom(GameData)`. Consumed by Task 5's UI and Task 6's `DeliveryService` caller.

- [ ] **Step 1: Write the failing serialization test**

```csharp
using NUnit.Framework;
using UnityEngine;

public class GameDataReputationSerializationTests
{
    [Test]
    public void GameData_WithEmptyRepSlots_RoundTripsThroughJsonUtility()
    {
        var data = new GameData();
        data.repBarProgress = 42;
        data.repPointsEarned = 3;
        data.repUnspentPoints = 1;
        data.repConsecutiveSkips = 2;
        data.repSlots = new[]
        {
            new RequestSlotSave { request = new DeliveryRequest { items = new DeliveryLineItem[0], repReward = 0, requesterName = "", flavorText = "" }, cooldownEndUtcTicks = 12345L },
            new RequestSlotSave { request = new DeliveryRequest { items = new[] { new DeliveryLineItem { itemId = "Blueberry", count = 40 } }, repReward = 30, requesterName = "Marta", flavorText = "" }, cooldownEndUtcTicks = 0L },
            new RequestSlotSave { request = new DeliveryRequest { items = new DeliveryLineItem[0], repReward = 0, requesterName = "", flavorText = "" }, cooldownEndUtcTicks = 0L },
        };

        string json = JsonUtility.ToJson(data);
        var loaded = JsonUtility.FromJson<GameData>(json);

        Assert.AreEqual(42, loaded.repBarProgress);
        Assert.AreEqual(3, loaded.repSlots.Length);
        Assert.AreEqual(12345L, loaded.repSlots[0].cooldownEndUtcTicks);
        Assert.AreEqual("Blueberry", loaded.repSlots[1].request.items[0].itemId);
        Assert.AreEqual(40, loaded.repSlots[1].request.items[0].count);
    }
}
```

- [ ] **Step 2: Run tests, verify compile-fail (red)** — `RequestSlotSave`/`GameData.repSlots` don't exist yet.

- [ ] **Step 3: Add GameData fields**

In `Assets/Scripts/GameData.cs`, after the Phase 1 `cropStacks`/`eggStack`/`collectModeOn` block, add:

```csharp
    // Reputation (Reputation Phase 2). Bar progress/points persist; 3 board slots by fixed
    // difficulty (0=Easy,1=Medium,2=Hard). A slot's request is a sentinel empty-items
    // DeliveryRequest while on cooldown (never null — see ReputationCore.ExportRequests).
    public int repBarProgress;
    public int repPointsEarned;
    public int repUnspentPoints;
    public int repConsecutiveSkips;
    public RequestSlotSave[] repSlots;
```

In the default constructor, after `cropStacks = new ItemStackEntry[0];`, add:

```csharp
        repSlots = new RequestSlotSave[0];
```

At the bottom of the file, after the `ItemStackEntry` class, add:

```csharp
/// <summary>One Town Requests board slot's saved state (Reputation Phase 2).</summary>
[Serializable]
public class RequestSlotSave
{
    public DeliveryRequest request;
    public long cooldownEndUtcTicks;
}
```

- [ ] **Step 4: Create the RequestCatalog data asset class**

`Assets/Scripts/Reputation/RequestCatalog.cs`:

```csharp
using UnityEngine;

public enum RequestItemKind { SpecificId, AnyUnlockedCrop, AnyRawFish, AnySmokedFish }

/// <summary>One catalog line: a category of requestable item plus its count range and pick weight.</summary>
[System.Serializable]
public struct RequestItemTemplate
{
    public RequestItemKind kind;
    [Tooltip("Used only when kind == SpecificId: \"egg\", \"wood\", or \"compost\".")]
    public string specificId;
    public int minCount;
    public int maxCount;
    public int weight;
}

/// <summary>
/// Town Requests generation data (Reputation Phase 2, spec §3.3): per-difficulty item template
/// pools, reward bases, and requester names. ReputationManager expands templates into concrete
/// RequestItemOptions at roll time, filtered by live unlock state.
/// </summary>
[CreateAssetMenu(menuName = "Farm Game/Reputation/Request Catalog", order = 20)]
public class RequestCatalog : ScriptableObject
{
    [Header("Reward base (spec §3.3) + variance")]
    public int easyRepBase = 30;
    public int mediumRepBase = 60;
    public int hardRepBase = 90;
    [Range(0f, 1f)] public float rewardVariance = 0.2f;

    [Header("Item template pools per difficulty")]
    public RequestItemTemplate[] easyPool = new[]
    {
        new RequestItemTemplate { kind = RequestItemKind.AnyUnlockedCrop, minCount = 10, maxCount = 25, weight = 6 },
        new RequestItemTemplate { kind = RequestItemKind.SpecificId, specificId = "egg", minCount = 2, maxCount = 5, weight = 2 },
        new RequestItemTemplate { kind = RequestItemKind.SpecificId, specificId = "wood", minCount = 15, maxCount = 30, weight = 1 },
        new RequestItemTemplate { kind = RequestItemKind.SpecificId, specificId = "compost", minCount = 10, maxCount = 20, weight = 1 },
    };
    public RequestItemTemplate[] mediumPool = new[]
    {
        new RequestItemTemplate { kind = RequestItemKind.AnyUnlockedCrop, minCount = 30, maxCount = 60, weight = 5 },
        new RequestItemTemplate { kind = RequestItemKind.AnyRawFish, minCount = 3, maxCount = 6, weight = 3 },
        new RequestItemTemplate { kind = RequestItemKind.SpecificId, specificId = "wood", minCount = 40, maxCount = 70, weight = 1 },
        new RequestItemTemplate { kind = RequestItemKind.SpecificId, specificId = "compost", minCount = 25, maxCount = 45, weight = 1 },
    };
    public RequestItemTemplate[] hardPool = new[]
    {
        new RequestItemTemplate { kind = RequestItemKind.AnyUnlockedCrop, minCount = 70, maxCount = 140, weight = 4 },
        new RequestItemTemplate { kind = RequestItemKind.AnyRawFish, minCount = 6, maxCount = 12, weight = 3 },
        new RequestItemTemplate { kind = RequestItemKind.AnySmokedFish, minCount = 2, maxCount = 5, weight = 2 },
        new RequestItemTemplate { kind = RequestItemKind.SpecificId, specificId = "wood", minCount = 90, maxCount = 150, weight = 1 },
    };

    public string[] requesterNames = { "Marta", "Old Finch", "Sal", "Widow Bree", "Pip", "Cormac" };
}
```

- [ ] **Step 5: Create ReputationManager**

`Assets/Scripts/Reputation/ReputationManager.cs`:

```csharp
using System;
using UnityEngine;

/// <summary>
/// Town Requests board owner: rolls/expires the 3 fixed-difficulty slots, tracks rep/points via
/// ReputationCore, and exposes Fulfill/Skip. Spec: 2026-07-19-reputation-design.md §3.
/// </summary>
public class ReputationManager : MonoBehaviour
{
    public static ReputationManager Instance { get; private set; }

    [SerializeField] private RequestCatalog catalog;
    [SerializeField] private CropDatabase cropDatabase;
    [SerializeField] private double cooldownHours = 8.0;

    private readonly ReputationCore core = new ReputationCore();
    private long CooldownTicks => (long)(cooldownHours * TimeSpan.TicksPerHour);

    public event Action OnChanged;
    public event Action<int> OnPointsAwarded;

    public int BarProgress => core.BarProgress;
    public int PointsEarned => core.PointsEarned;
    public int UnspentPoints => core.UnspentPoints;
    public int NextPointCost => ReputationMath.PointCost(core.PointsEarned + 1);
    public float BarProgress01 => NextPointCost <= 0 ? 0f : Mathf.Clamp01((float)core.BarProgress / NextPointCost);
    public int NextSkipCost => core.NextSkipCost();

    public DeliveryRequest GetSlotRequest(int slot) => core.GetSlotRequest(slot);
    public bool IsSlotOnCooldown(int slot) => core.IsSlotOnCooldown(slot, DateTime.UtcNow.Ticks);
    public double GetSlotCooldownRemainingSeconds(int slot)
    {
        long remainingTicks = core.GetSlotCooldownEndUtcTicks(slot) - DateTime.UtcNow.Ticks;
        return remainingTicks <= 0 ? 0 : remainingTicks / (double)TimeSpan.TicksPerSecond;
    }

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void Update()
    {
        long now = DateTime.UtcNow.Ticks;
        for (int slot = 0; slot < 3; slot++)
        {
            if (core.GetSlotRequest(slot) == null && !core.IsSlotOnCooldown(slot, now))
                RollSlot(slot);
        }
    }

    public bool TryFulfill(int slot)
    {
        DeliveryRequest request = core.GetSlotRequest(slot);
        if (request == null || !DeliveryService.CanFulfill(request)) return false;
        if (!DeliveryService.TryFulfill(request)) return false;

        core.OnFulfilled(slot, DateTime.UtcNow.Ticks, CooldownTicks);
        int awarded = core.AddRep(request.repReward);
        Debug.Log($"[Reputation] Fulfilled slot {slot} (+{request.repReward} rep)");
        OnChanged?.Invoke();
        if (awarded > 0) OnPointsAwarded?.Invoke(awarded);
        return true;
    }

    public bool TrySkip(int slot)
    {
        DeliveryRequest request = core.GetSlotRequest(slot);
        if (request == null) return false;
        int cost = core.NextSkipCost();
        if (CurrencyManager.Instance == null || !CurrencyManager.Instance.SpendGems(cost)) return false;

        core.OnSkipped();
        core.SetSlotRequest(slot, null); // Update() rolls a fresh one immediately (no cooldown)
        Debug.Log($"[Reputation] Skipped slot {slot} for {cost} gems");
        OnChanged?.Invoke();
        return true;
    }

    private void RollSlot(int slot)
    {
        if (catalog == null) return;
        RequestItemTemplate[] templates = slot == 0 ? catalog.easyPool : slot == 1 ? catalog.mediumPool : catalog.hardPool;
        int repBase = slot == 0 ? catalog.easyRepBase : slot == 1 ? catalog.mediumRepBase : catalog.hardRepBase;

        RequestItemOption[] pool = BuildPool(templates);
        var rng = new System.Random();
        if (!RequestGenerationMath.TryRoll(pool, rng, out string itemId, out int count)) return;

        float varianceRoll = 1f + UnityEngine.Random.Range(-catalog.rewardVariance, catalog.rewardVariance);
        int reward = Mathf.Max(1, Mathf.RoundToInt(repBase * varianceRoll));
        string requester = catalog.requesterNames != null && catalog.requesterNames.Length > 0
            ? catalog.requesterNames[UnityEngine.Random.Range(0, catalog.requesterNames.Length)]
            : "A neighbor";

        var request = new DeliveryRequest
        {
            items = new[] { new DeliveryLineItem { itemId = itemId, count = count } },
            repReward = reward,
            requesterName = requester,
            flavorText = "",
        };
        core.SetSlotRequest(slot, request);
        OnChanged?.Invoke();
    }

    private RequestItemOption[] BuildPool(RequestItemTemplate[] templates)
    {
        var options = new System.Collections.Generic.List<RequestItemOption>();
        if (templates == null) return options.ToArray();

        foreach (RequestItemTemplate t in templates)
        {
            switch (t.kind)
            {
                case RequestItemKind.SpecificId:
                    if (IsSpecificIdAvailable(t.specificId))
                        options.Add(new RequestItemOption { itemId = t.specificId, minCount = t.minCount, maxCount = t.maxCount, weight = t.weight });
                    break;
                case RequestItemKind.AnyUnlockedCrop:
                    if (cropDatabase != null)
                        foreach (CropData crop in cropDatabase.startingCrops)
                            if (crop != null)
                                options.Add(new RequestItemOption { itemId = crop.cropName, minCount = t.minCount, maxCount = t.maxCount, weight = t.weight });
                    break;
                case RequestItemKind.AnyRawFish:
                    if (FishingManager.Instance != null && FishingManager.Instance.HasPole)
                        for (int tier = 1; tier <= FishTiers.Count; tier++)
                            options.Add(new RequestItemOption { itemId = "fish_raw_" + tier, minCount = t.minCount, maxCount = t.maxCount, weight = t.weight });
                    break;
                case RequestItemKind.AnySmokedFish:
                    if (SmokehouseManager.Instance != null && SmokehouseManager.Instance.IsBuilt)
                        for (int tier = 1; tier <= FishTiers.Count; tier++)
                            options.Add(new RequestItemOption { itemId = "fish_smoked_" + tier, minCount = t.minCount, maxCount = t.maxCount, weight = t.weight });
                    break;
            }
        }
        return options.ToArray();
    }

    private static bool IsSpecificIdAvailable(string id)
    {
        if (id == "wood") return WoodcuttingManager.Instance != null && WoodcuttingManager.Instance.HasAxe;
        return true; // "egg", "compost" — always available in v1 (spec §9 deviation)
    }

    public void CaptureTo(GameData d)
    {
        d.repBarProgress = core.BarProgress;
        d.repPointsEarned = core.PointsEarned;
        d.repUnspentPoints = core.UnspentPoints;
        d.repConsecutiveSkips = core.ConsecutiveSkips;

        DeliveryRequest[] requests = core.ExportRequests();
        long[] cooldowns = core.ExportCooldowns();
        d.repSlots = new RequestSlotSave[3];
        for (int i = 0; i < 3; i++)
            d.repSlots[i] = new RequestSlotSave { request = requests[i], cooldownEndUtcTicks = cooldowns[i] };
    }

    public void LoadFrom(GameData d)
    {
        var requests = new DeliveryRequest[3];
        var cooldowns = new long[3];
        if (d.repSlots != null)
            for (int i = 0; i < Mathf.Min(3, d.repSlots.Length); i++)
            {
                requests[i] = d.repSlots[i]?.request;
                cooldowns[i] = d.repSlots[i]?.cooldownEndUtcTicks ?? 0;
            }
        core.Import(d.repBarProgress, d.repPointsEarned, d.repUnspentPoints, d.repConsecutiveSkips, requests, cooldowns);
        OnChanged?.Invoke();
    }
}
```

- [ ] **Step 6: Wire SaveManager**

In `Assets/Scripts/SaveManager.cs`, alongside the Phase 1 line `if (ItemInventoryManager.Instance != null) ItemInventoryManager.Instance.CaptureTo(data);`, add:

```csharp
        if (ReputationManager.Instance != null) ReputationManager.Instance.CaptureTo(data);
```

Alongside the matching `LoadFrom` line, add:

```csharp
                if (ReputationManager.Instance != null)
                    ReputationManager.Instance.LoadFrom(data);
```

- [ ] **Step 7: Wire AutoSaveManager**

Alongside the Phase 1 `ItemInventoryManager.Instance.OnChanged += OnVoid;` line, add:

```csharp
        if (ReputationManager.Instance != null)
            ReputationManager.Instance.OnChanged += OnVoid;
```

And the matching `-= OnVoid;` in the unsubscribe block.

- [ ] **Step 8: Create the RequestCatalog asset + ReputationManager scene GameObject** (MCP, after compiling — see Step 9)

Deferred to Step 9's compile-then-execute_code sequence (the asset type and manager component must exist and compile before they can be instantiated).

- [ ] **Step 9: Compile, create asset + scene objects, run tests**

1. `mcp__unity-mcp__refresh_unity` (compile: request, mode: force, wait_for_ready: true).
2. Check `read_console` for errors — fix any before proceeding.
3. Via `execute_code`, create the catalog asset (field initializers already populate sensible defaults; this just persists an instance):
   ```csharp
   var catalog = ScriptableObject.CreateInstance<RequestCatalog>();
   UnityEditor.AssetDatabase.CreateAsset(catalog, "Assets/Data/Reputation/RequestCatalog.asset");
   UnityEditor.AssetDatabase.SaveAssets();
   return "created";
   ```
   (Create the `Assets/Data/Reputation/` folder first via `execute_code` + `AssetDatabase.IsValidFolder`/`CreateFolder` if it doesn't exist, or via the gladekit `create_folder` tool.)
4. Via `execute_code`, create the `ReputationManager` GameObject, add the component, and wire `catalog` + `cropDatabase` (find the same `CropDatabase` asset used by `InventoryPopupUITK` in Phase 1) via `SerializedObject`, then `MarkSceneDirty` + `SaveOpenScenes`.
5. `touch Temp/run_editmode_tests.request`; expect **205/205** (204 + 1 new).

- [ ] **Step 10: Commit**

```bash
git add Assets/Scripts/Reputation/RequestCatalog.cs Assets/Scripts/Reputation/ReputationManager.cs Assets/Tests/EditMode/GameDataReputationSerializationTests.cs Assets/Scripts/GameData.cs Assets/Scripts/SaveManager.cs Assets/Scripts/AutoSaveManager.cs Assets/Scenes/FarmMain.unity Assets/Data/Reputation/RequestCatalog.asset
git commit -m "feat(reputation): RequestCatalog + ReputationManager with save wiring"
```

---

### Task 5: DeliveryService

**Files:**
- Create: `Assets/Scripts/Reputation/DeliveryService.cs`

**Interfaces:**
- Consumes: `ItemInventoryManager` (Phase 1: `Eggs`, `GetCrop`, `TrySpendEggs`, `TrySpendCrop`), `CurrencyManager` (`Wood`, `Compost`, `SpendWood`, `SpendCompost`), `PantryManager` (`GetRaw`, `GetSmoked`, `SpendRaw`, `SpendSmoked`), `DeliveryRequest`/`DeliveryLineItem` (Task 3).
- Produces: `static bool DeliveryService.CanFulfill(DeliveryRequest request)`; `static bool DeliveryService.TryFulfill(DeliveryRequest request)`. Consumed by Task 4's `ReputationManager.TryFulfill`.

No unit tests for this task (it only glues together live singletons — same precedent as Phase 1's harvest diversion hooks). Exercised in Task 7's play-mode smoke test.

- [ ] **Step 1: Implement**

```csharp
using UnityEngine;

/// <summary>
/// The single facade that knows where every requestable item type lives (Reputation Phase 2,
/// spec §3.5/§6). Board slots and future NPC fulfillment quests both go through this — neither
/// needs to know whether an item is a crop stack, a Pantry count, or a CurrencyManager resource.
/// v1 item id convention: crop names as-is, "egg", "wood", "compost", "fish_raw_&lt;tier&gt;",
/// "fish_smoked_&lt;tier&gt;". Jars are out of scope for v1 (spec §9 — no per-crop jar-count API).
/// </summary>
public static class DeliveryService
{
    public static bool CanFulfill(DeliveryRequest request)
    {
        if (request?.items == null || request.items.Length == 0) return false;
        foreach (DeliveryLineItem item in request.items)
            if (!CanFulfillLine(item)) return false;
        return true;
    }

    public static bool TryFulfill(DeliveryRequest request)
    {
        if (!CanFulfill(request)) return false;
        foreach (DeliveryLineItem item in request.items)
            SpendLine(item);
        return true;
    }

    private static bool CanFulfillLine(DeliveryLineItem item)
    {
        if (item == null || string.IsNullOrEmpty(item.itemId) || item.count <= 0) return false;
        string id = item.itemId;

        if (id == "egg") return ItemInventoryManager.Instance != null && ItemInventoryManager.Instance.Eggs >= item.count;
        if (id == "wood") return CurrencyManager.Instance != null && CurrencyManager.Instance.Wood >= item.count;
        if (id == "compost") return CurrencyManager.Instance != null && CurrencyManager.Instance.Compost >= item.count;
        if (id.StartsWith("fish_raw_"))
            return TryParseTier(id, "fish_raw_", out int rawTier) && PantryManager.Instance != null && PantryManager.Instance.GetRaw(rawTier) >= item.count;
        if (id.StartsWith("fish_smoked_"))
            return TryParseTier(id, "fish_smoked_", out int smokedTier) && PantryManager.Instance != null && PantryManager.Instance.GetSmoked(smokedTier) >= item.count;

        return ItemInventoryManager.Instance != null && ItemInventoryManager.Instance.GetCrop(id) >= item.count;
    }

    private static void SpendLine(DeliveryLineItem item)
    {
        string id = item.itemId;

        if (id == "egg") { ItemInventoryManager.Instance.TrySpendEggs(item.count); return; }
        if (id == "wood") { CurrencyManager.Instance.SpendWood(item.count); return; }
        if (id == "compost") { CurrencyManager.Instance.SpendCompost(item.count); return; }
        if (id.StartsWith("fish_raw_") && TryParseTier(id, "fish_raw_", out int rawTier))
        {
            for (int i = 0; i < item.count; i++) PantryManager.Instance.SpendRaw(rawTier);
            return;
        }
        if (id.StartsWith("fish_smoked_") && TryParseTier(id, "fish_smoked_", out int smokedTier))
        {
            for (int i = 0; i < item.count; i++) PantryManager.Instance.SpendSmoked(smokedTier);
            return;
        }
        ItemInventoryManager.Instance.TrySpendCrop(id, item.count);
    }

    private static bool TryParseTier(string id, string prefix, out int tier)
        => int.TryParse(id.Substring(prefix.Length), out tier);
}
```

- [ ] **Step 2: Compile check** — `refresh_unity` (compile: request, force), `read_console` for errors.

- [ ] **Step 3: Run full test suite** — expect 205/205 (no new tests, regression guard).

- [ ] **Step 4: Commit**

```bash
git add Assets/Scripts/Reputation/DeliveryService.cs
git commit -m "feat(reputation): DeliveryService item-location facade"
```

---

### Task 6: RequestItemDisplay + ReputationBarUITK (persistent Market bar)

**Files:**
- Create: `Assets/Scripts/Reputation/RequestItemDisplay.cs`
- Create: `Assets/Scripts/UI/ReputationBarUITK.cs`

**Interfaces:**
- Consumes: `ReputationManager` (Task 4), `CameraPanController.Location`/`OnPanStarted`/`OnPanCompleted`/`CurrentLocation` (existing), `FishTiers.Name`/`SmokedName` (existing).
- Produces: `static string RequestItemDisplay.Name(string itemId)` (consumed by Task 7's popup too); singleton `ReputationBarUITK.Instance` with `void ShowBoard()`-triggering click (opens `TownRequestsPopupUITK`, built in Task 7 — this task only builds the bar and leaves the click handler calling `TownRequestsPopupUITK.Instance?.Open()`, which no-ops safely until Task 7 exists).

- [ ] **Step 1: Implement RequestItemDisplay**

```csharp
/// <summary>Maps a DeliveryLineItem.itemId (spec §6 convention) to a human-readable name for UI.</summary>
public static class RequestItemDisplay
{
    public static string Name(string itemId)
    {
        if (string.IsNullOrEmpty(itemId)) return "?";
        if (itemId == "egg") return "Eggs";
        if (itemId == "wood") return "Wood";
        if (itemId == "compost") return "Compost";
        if (itemId.StartsWith("fish_raw_") && int.TryParse(itemId.Substring("fish_raw_".Length), out int rawTier))
            return FishTiers.Name(rawTier);
        if (itemId.StartsWith("fish_smoked_") && int.TryParse(itemId.Substring("fish_smoked_".Length), out int smokedTier))
            return FishTiers.SmokedName(smokedTier);
        return itemId; // crop name as-is
    }
}
```

- [ ] **Step 2: Implement ReputationBarUITK**

`Assets/Scripts/UI/ReputationBarUITK.cs`:

```csharp
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Always-on horizontal rep bar pinned to the top of the Market view (Reputation Phase 2, spec
/// §3.4). Fully code-driven UIDocument (ToastManager pattern) — no UXML/USS assets, no scene
/// wiring beyond placing this component. Tapping it opens the Town Requests popup.
/// </summary>
[DefaultExecutionOrder(1050)]
public class ReputationBarUITK : MonoBehaviour
{
    public static ReputationBarUITK Instance { get; private set; }
    private const int SORT_ORDER = 900;

    private UIDocument document;
    private PanelSettings runtimePanelSettings;
    private VisualElement barRoot;
    private VisualElement fill;
    private Label progressLabel;
    private Label pointsLabel;

    private CameraPanController panController;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        document = GetComponent<UIDocument>();
        if (document == null) document = gameObject.AddComponent<UIDocument>();

        runtimePanelSettings = ScriptableObject.CreateInstance<PanelSettings>();
        runtimePanelSettings.name = "ReputationBarPanelSettings (runtime)";
        runtimePanelSettings.scaleMode = PanelScaleMode.ScaleWithScreenSize;
        runtimePanelSettings.referenceResolution = new Vector2Int(1080, 1920);
        runtimePanelSettings.match = 0.5f;
        runtimePanelSettings.sortingOrder = SORT_ORDER;

        document.enabled = false;
        document.panelSettings = runtimePanelSettings;
        document.enabled = true;
    }

    private void Start()
    {
        Build();
        RefreshVisibility();
        Refresh();

        if (ReputationManager.Instance != null)
            ReputationManager.Instance.OnChanged += Refresh;

        CameraPanController pan = GetPanController();
        if (pan != null)
        {
            pan.OnPanStarted += OnLocationChanging;
            pan.OnPanCompleted += OnLocationChanging;
        }
    }

    private void OnDestroy()
    {
        if (ReputationManager.Instance != null)
            ReputationManager.Instance.OnChanged -= Refresh;
        if (panController != null)
        {
            panController.OnPanStarted -= OnLocationChanging;
            panController.OnPanCompleted -= OnLocationChanging;
        }
        if (Instance == this) Instance = null;
        if (runtimePanelSettings != null) Destroy(runtimePanelSettings);
    }

    private CameraPanController GetPanController()
    {
        if (panController == null && Camera.main != null)
            panController = Camera.main.GetComponent<CameraPanController>();
        return panController;
    }

    private void OnLocationChanging(CameraPanController.Location _) => RefreshVisibility();

    private void RefreshVisibility()
    {
        if (barRoot == null) return;
        CameraPanController pan = GetPanController();
        bool atMarket = pan != null && pan.CurrentLocation == CameraPanController.Location.Market;
        barRoot.style.display = atMarket ? DisplayStyle.Flex : DisplayStyle.None;
    }

    private void Build()
    {
        VisualElement root = document.rootVisualElement;
        if (root == null) { Debug.LogWarning("[ReputationBarUITK] rootVisualElement null."); return; }
        root.pickingMode = PickingMode.Ignore;

        barRoot = new VisualElement { name = "rep-bar-root" };
        barRoot.style.position = Position.Absolute;
        barRoot.style.top = 20;
        barRoot.style.left = Length.Percent(10);
        barRoot.style.right = Length.Percent(10);
        barRoot.style.height = 64;
        barRoot.style.backgroundColor = new Color(0.11f, 0.12f, 0.13f, 0.9f);
        barRoot.style.borderTopLeftRadius = 16; barRoot.style.borderTopRightRadius = 16;
        barRoot.style.borderBottomLeftRadius = 16; barRoot.style.borderBottomRightRadius = 16;
        barRoot.style.paddingLeft = 6; barRoot.style.paddingRight = 14;
        barRoot.style.flexDirection = FlexDirection.Row;
        barRoot.style.alignItems = Align.Center;
        barRoot.pickingMode = PickingMode.Position;
        barRoot.RegisterCallback<ClickEvent>(_ => TownRequestsPopupUITK.Instance?.Open());
        root.Add(barRoot);

        VisualElement track = new VisualElement { name = "rep-bar-track" };
        track.style.flexGrow = 1;
        track.style.height = 24;
        track.style.marginLeft = 10; track.style.marginRight = 10;
        track.style.backgroundColor = new Color(0f, 0f, 0f, 0.35f);
        track.style.borderTopLeftRadius = 12; track.style.borderTopRightRadius = 12;
        track.style.borderBottomLeftRadius = 12; track.style.borderBottomRightRadius = 12;
        track.style.overflow = Overflow.Hidden;
        barRoot.Add(track);

        fill = new VisualElement { name = "rep-bar-fill" };
        fill.style.height = Length.Percent(100);
        fill.style.width = Length.Percent(0);
        fill.style.backgroundColor = new Color(0.78f, 0.35f, 0.85f); // purple: reputation
        track.Add(fill);

        progressLabel = new Label();
        progressLabel.style.position = Position.Absolute;
        progressLabel.style.left = 0; progressLabel.style.right = 0; progressLabel.style.top = 0; progressLabel.style.bottom = 0;
        progressLabel.style.color = Color.white;
        progressLabel.style.fontSize = 18;
        progressLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
        track.Add(progressLabel);

        pointsLabel = new Label();
        pointsLabel.style.color = new Color(1f, 0.84f, 0f);
        pointsLabel.style.fontSize = 20;
        pointsLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
        pointsLabel.style.display = DisplayStyle.None;
        barRoot.Add(pointsLabel);
    }

    private void Refresh()
    {
        if (fill == null || ReputationManager.Instance == null) return;
        var rm = ReputationManager.Instance;
        fill.style.width = Length.Percent(rm.BarProgress01 * 100f);
        progressLabel.text = $"Reputation  {rm.BarProgress} / {rm.NextPointCost}";
        if (rm.UnspentPoints > 0)
        {
            pointsLabel.text = $"Points: {rm.UnspentPoints}";
            pointsLabel.style.display = DisplayStyle.Flex;
        }
        else
        {
            pointsLabel.style.display = DisplayStyle.None;
        }
    }
}
```

- [ ] **Step 3: Compile check + full test run** — 205/205, zero console errors.

- [ ] **Step 4: Create scene GameObject** (MCP `execute_code`): `new GameObject("ReputationBarUITK"); go.AddComponent<ReputationBarUITK>();` then `MarkSceneDirty` + `SaveOpenScenes`.

- [ ] **Step 5: Commit**

```bash
git add Assets/Scripts/Reputation/RequestItemDisplay.cs Assets/Scripts/UI/ReputationBarUITK.cs Assets/Scenes/FarmMain.unity
git commit -m "feat(reputation): persistent Market rep bar (code-driven UITK)"
```

---

### Task 7: TownRequestsPopupUITK

**Files:**
- Create: `Assets/Scripts/UI/TownRequestsPopupUITK.cs`

**Interfaces:**
- Consumes: `ReputationManager` (Task 4: `GetSlotRequest`, `IsSlotOnCooldown`, `GetSlotCooldownRemainingSeconds`, `NextSkipCost`, `TryFulfill`, `TrySkip`, `OnChanged`), `DeliveryService.CanFulfill` (Task 5), `RequestItemDisplay.Name` (Task 6), `TimeFormat.Hms` (existing), `ToastManager.Show` (existing).
- Produces: singleton `TownRequestsPopupUITK.Instance` with `void Open()` / `void Close()` / `bool IsOpen`. Consumed by Task 6's bar click handler (forward reference, resolved once this exists).

- [ ] **Step 1: Implement**

`Assets/Scripts/UI/TownRequestsPopupUITK.cs`:

```csharp
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// The Town Requests board popup: 3 fixed-difficulty slots (Easy/Medium/Hard), each showing the
/// current request, a Fulfill button (enabled only when affordable), a Skip button (costs Gems,
/// escalating), or a cooldown countdown once fulfilled. Fully code-driven UIDocument (ToastManager
/// pattern). Spec: 2026-07-19-reputation-design.md §3.
/// </summary>
[DefaultExecutionOrder(1050)]
public class TownRequestsPopupUITK : MonoBehaviour
{
    public static TownRequestsPopupUITK Instance { get; private set; }
    private const int SORT_ORDER = 1000;
    private static readonly string[] DifficultyNames = { "Easy", "Medium", "Hard" };
    private static readonly Color[] DifficultyColors =
    {
        new Color(0.45f, 0.75f, 0.4f),   // Easy - green
        new Color(0.85f, 0.72f, 0.25f),  // Medium - gold
        new Color(0.8f, 0.35f, 0.3f),    // Hard - red
    };

    private UIDocument document;
    private PanelSettings runtimePanelSettings;
    private VisualElement root;
    private VisualElement popupRoot;
    private VisualElement backdrop;
    private VisualElement slotsColumn;
    private bool isOpen;
    public bool IsOpen => isOpen;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        document = GetComponent<UIDocument>();
        if (document == null) document = gameObject.AddComponent<UIDocument>();

        runtimePanelSettings = ScriptableObject.CreateInstance<PanelSettings>();
        runtimePanelSettings.name = "TownRequestsPanelSettings (runtime)";
        runtimePanelSettings.scaleMode = PanelScaleMode.ScaleWithScreenSize;
        runtimePanelSettings.referenceResolution = new Vector2Int(1080, 1920);
        runtimePanelSettings.match = 0.5f;
        runtimePanelSettings.sortingOrder = SORT_ORDER;

        document.enabled = false;
        document.panelSettings = runtimePanelSettings;
        document.enabled = true;
    }

    private void Start()
    {
        Build();
        if (ReputationManager.Instance != null)
            ReputationManager.Instance.OnChanged += OnChanged;
    }

    private void OnDestroy()
    {
        if (ReputationManager.Instance != null)
            ReputationManager.Instance.OnChanged -= OnChanged;
        if (Instance == this) Instance = null;
        if (runtimePanelSettings != null) Destroy(runtimePanelSettings);
    }

    private void Update()
    {
        // Countdown labels tick every second; only touch the DOM while actually open.
        if (isOpen) RefreshCountdownsOnly();
    }

    private void OnChanged() { if (isOpen) BuildSlots(); }

    public void Open()
    {
        if (isOpen || popupRoot == null) return;
        isOpen = true;
        root.pickingMode = PickingMode.Position;
        BuildSlots();
        popupRoot.style.display = DisplayStyle.Flex;
    }

    public void Close()
    {
        if (!isOpen) return;
        isOpen = false;
        if (popupRoot != null) popupRoot.style.display = DisplayStyle.None;
        if (root != null) root.pickingMode = PickingMode.Ignore;
    }

    private void Build()
    {
        root = document.rootVisualElement;
        if (root == null) { Debug.LogWarning("[TownRequestsPopupUITK] rootVisualElement null."); return; }
        root.pickingMode = PickingMode.Ignore;

        popupRoot = new VisualElement { name = "town-requests-root" };
        popupRoot.style.position = Position.Absolute;
        popupRoot.style.left = 0; popupRoot.style.right = 0; popupRoot.style.top = 0; popupRoot.style.bottom = 0;
        popupRoot.style.alignItems = Align.Center;
        popupRoot.style.justifyContent = Justify.Center;
        popupRoot.style.display = DisplayStyle.None;
        root.Add(popupRoot);

        backdrop = new VisualElement { name = "town-requests-backdrop" };
        backdrop.style.position = Position.Absolute;
        backdrop.style.left = 0; backdrop.style.right = 0; backdrop.style.top = 0; backdrop.style.bottom = 0;
        backdrop.style.backgroundColor = new Color(0f, 0f, 0f, 0.55f);
        backdrop.pickingMode = PickingMode.Position;
        backdrop.RegisterCallback<ClickEvent>(_ => Close());
        popupRoot.Add(backdrop);

        VisualElement card = new VisualElement { name = "town-requests-card" };
        card.style.width = Length.Percent(86);
        card.style.maxWidth = 680;
        card.style.backgroundColor = new Color(0.70f, 0.60f, 0.43f);
        card.style.borderTopLeftRadius = 18; card.style.borderTopRightRadius = 18;
        card.style.borderBottomLeftRadius = 18; card.style.borderBottomRightRadius = 18;
        card.style.paddingLeft = 20; card.style.paddingRight = 20;
        card.style.paddingTop = 16; card.style.paddingBottom = 20;
        popupRoot.Add(card);

        VisualElement header = new VisualElement { name = "header" };
        header.style.flexDirection = FlexDirection.Row;
        header.style.justifyContent = Justify.SpaceBetween;
        header.style.alignItems = Align.Center;
        header.style.marginBottom = 12;
        card.Add(header);

        Label title = new Label("Town Requests");
        title.style.fontSize = 34;
        title.style.unityFontStyleAndWeight = FontStyle.Bold;
        title.style.color = new Color(0.21f, 0.13f, 0.06f);
        header.Add(title);

        Button closeBtn = new Button(Close) { text = "×" }; // "×"
        closeBtn.style.width = 48; closeBtn.style.height = 48;
        closeBtn.style.fontSize = 30;
        closeBtn.style.unityFontStyleAndWeight = FontStyle.Bold;
        closeBtn.style.backgroundColor = new Color(0f, 0f, 0f, 0f);
        closeBtn.style.borderTopWidth = 0; closeBtn.style.borderBottomWidth = 0;
        closeBtn.style.borderLeftWidth = 0; closeBtn.style.borderRightWidth = 0;
        closeBtn.style.color = new Color(0.21f, 0.13f, 0.06f);
        header.Add(closeBtn);

        slotsColumn = new VisualElement { name = "slots" };
        slotsColumn.style.flexDirection = FlexDirection.Column;
        card.Add(slotsColumn);
    }

    private void BuildSlots()
    {
        if (slotsColumn == null || ReputationManager.Instance == null) return;
        slotsColumn.Clear();
        for (int slot = 0; slot < 3; slot++)
            slotsColumn.Add(BuildSlotCard(slot));
    }

    private VisualElement BuildSlotCard(int slot)
    {
        var rm = ReputationManager.Instance;
        VisualElement card = new VisualElement { name = "slot-" + slot };
        card.style.backgroundColor = new Color(1f, 0.98f, 0.93f);
        card.style.borderTopLeftRadius = 12; card.style.borderTopRightRadius = 12;
        card.style.borderBottomLeftRadius = 12; card.style.borderBottomRightRadius = 12;
        card.style.borderLeftWidth = 6;
        card.style.borderLeftColor = DifficultyColors[slot];
        card.style.paddingLeft = 16; card.style.paddingRight = 16;
        card.style.paddingTop = 12; card.style.paddingBottom = 12;
        card.style.marginBottom = 14;

        Label diffLabel = new Label(DifficultyNames[slot]);
        diffLabel.style.fontSize = 20;
        diffLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
        diffLabel.style.color = DifficultyColors[slot];
        card.Add(diffLabel);

        bool onCooldown = rm.IsSlotOnCooldown(slot);
        DeliveryRequest request = rm.GetSlotRequest(slot);

        if (onCooldown || request == null)
        {
            Label cooldownLabel = new Label();
            cooldownLabel.name = "cooldown-label";
            cooldownLabel.style.fontSize = 24;
            cooldownLabel.style.color = new Color(0.35f, 0.28f, 0.2f);
            cooldownLabel.style.marginTop = 6;
            cooldownLabel.text = $"Completed! Next request in {TimeFormat.Hms((float)rm.GetSlotCooldownRemainingSeconds(slot))}";
            card.Add(cooldownLabel);
            return card;
        }

        DeliveryLineItem lineItem = request.items[0];
        Label flavor = new Label($"{request.requesterName} needs {lineItem.count} {RequestItemDisplay.Name(lineItem.itemId)}");
        flavor.style.fontSize = 24;
        flavor.style.color = new Color(0.15f, 0.1f, 0.05f);
        flavor.style.whiteSpace = WhiteSpace.Normal;
        flavor.style.marginTop = 4;
        card.Add(flavor);

        Label reward = new Label($"Reward: {request.repReward} Reputation");
        reward.style.fontSize = 20;
        reward.style.color = new Color(0.4f, 0.3f, 0.15f);
        reward.style.marginTop = 2; reward.style.marginBottom = 10;
        card.Add(reward);

        VisualElement buttonRow = new VisualElement();
        buttonRow.style.flexDirection = FlexDirection.Row;
        buttonRow.style.justifyContent = Justify.SpaceBetween;
        card.Add(buttonRow);

        bool canFulfill = DeliveryService.CanFulfill(request);
        Button fulfillBtn = new Button(() => OnFulfillClicked(slot, request)) { text = "Fulfill" };
        fulfillBtn.style.flexGrow = 1;
        fulfillBtn.style.marginRight = 8;
        fulfillBtn.style.height = 48;
        fulfillBtn.style.backgroundColor = canFulfill ? new Color(0.36f, 0.62f, 0.32f) : new Color(0.6f, 0.6f, 0.6f);
        fulfillBtn.style.color = Color.white;
        fulfillBtn.SetEnabled(canFulfill);
        buttonRow.Add(fulfillBtn);

        int skipCost = rm.NextSkipCost;
        Button skipBtn = new Button(() => OnSkipClicked(slot)) { text = $"Skip ({skipCost} gems)" };
        skipBtn.style.flexGrow = 1;
        skipBtn.style.height = 48;
        skipBtn.style.backgroundColor = new Color(0.55f, 0.45f, 0.35f);
        skipBtn.style.color = Color.white;
        buttonRow.Add(skipBtn);

        return card;
    }

    private void OnFulfillClicked(int slot, DeliveryRequest request)
    {
        int reward = request.repReward;
        if (ReputationManager.Instance.TryFulfill(slot))
            ToastManager.Show("Request Fulfilled", $"+{reward} Reputation", ToastManager.ToastKind.Success);
    }

    private void OnSkipClicked(int slot)
    {
        ReputationManager.Instance.TrySkip(slot);
    }

    private void RefreshCountdownsOnly()
    {
        if (slotsColumn == null || ReputationManager.Instance == null) return;
        var rm = ReputationManager.Instance;
        for (int slot = 0; slot < 3; slot++)
        {
            if (!rm.IsSlotOnCooldown(slot)) continue;
            VisualElement card = slotsColumn.Q<VisualElement>("slot-" + slot);
            Label cooldownLabel = card?.Q<Label>("cooldown-label");
            if (cooldownLabel != null)
                cooldownLabel.text = $"Completed! Next request in {TimeFormat.Hms((float)rm.GetSlotCooldownRemainingSeconds(slot))}";
        }
    }
}
```

- [ ] **Step 2: Compile check** — `refresh_unity` (compile: request, force), `read_console` for errors. Fix any (note: `TownRequestsPopupUITK.Instance` referenced by Task 6's bar now resolves).

- [ ] **Step 3: Run full test suite** — expect 205/205.

- [ ] **Step 4: Create scene GameObject** (MCP `execute_code`): `new GameObject("TownRequestsPopupUITK"); go.AddComponent<TownRequestsPopupUITK>();` then `MarkSceneDirty` + `SaveOpenScenes`.

- [ ] **Step 5: Commit**

```bash
git add Assets/Scripts/UI/TownRequestsPopupUITK.cs Assets/Scenes/FarmMain.unity
git commit -m "feat(reputation): Town Requests popup — fulfill/skip/cooldown"
```

---

### Task 8: Play-mode smoke test

**Files:** none (verification only; fix-forward anything found, then commit fixes).

- [ ] **Step 1: Back up the play save.** Get `Application.persistentDataPath` via `execute_code`; PowerShell-copy `gamedata.json` to a dated backup (skip if none exists).

- [ ] **Step 2: Enter play mode** via `touch Temp/enter_play_mode.request`; confirm `EditorApplication.isPlaying == true` via `execute_code`.

- [ ] **Step 3: Drive the loop via `execute_code`:**
  1. Assert `ReputationManager.Instance != null`; wait a frame (or call `Update` indirectly by re-checking) and assert all 3 slots have a non-null `GetSlotRequest` (Easy/Medium/Hard auto-rolled).
  2. Grant enough of the Easy slot's requested item directly (e.g. if it's a crop, `ItemInventoryManager.Instance.AddCrop(itemId, count, out _)`; if "wood", `CurrencyManager.Instance.AddWood(count)`; if "egg", `ItemInventoryManager.Instance.AddEggs(count, out _)`) so `DeliveryService.CanFulfill` is true.
  3. Call `ReputationManager.Instance.TryFulfill(0)`; assert it returns true, `BarProgress`/`PointsEarned` increased appropriately, the slot's request is now null, and `IsSlotOnCooldown(0)` is true.
  4. Assert the consumed item's stack decreased by exactly the requested count and no currency/Money was granted (rep-only, per spec §2).
  5. Grant enough Gems (`CurrencyManager.Instance.AddGems(1000)` if short), call `ReputationManager.Instance.TrySkip(1)`; assert it returns true, Gems decreased by 25 (first skip), and the Medium slot now has a **different or re-rolled** request with no cooldown.
  6. Open the popup (`TownRequestsPopupUITK.Instance.Open()`), pan the camera to Market if not already there (`Camera.main.GetComponent<CameraPanController>().PanToMarket()`), screenshot via `ScreenCapture.CaptureScreenshot`, `Read` the image to verify the bar + popup render (rep bar visible at Market, 3 slot cards, one showing a cooldown message for slot 0).
  7. Save/reload: `SaveManager.Instance.SaveGame()`, exit play, re-enter, assert `ReputationManager.Instance.PointsEarned`/`BarProgress`/slot states match pre-exit values.

- [ ] **Step 4: Exit play mode**, check console for zero new exceptions (ignore the known pre-existing `UnityBridge`/`generators-beta.ai.unity.com` noise).

- [ ] **Step 5: Restore the real save** from the Step 1 backup; delete temp screenshots.

- [ ] **Step 6: Commit any fixes**

```bash
git add -A Assets/Scripts Assets/Tests
git commit -m "fix(reputation): play-mode smoke test fixes"
```
