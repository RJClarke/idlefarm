# Free Gift Chest, Farmer's Pass & Store — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Ship the Free Gift chest (an opt-in rewarded-ad timer reward that is the town's gift), the $9.99 Farmer's Pass, and a scrollable Store, all running on stand-in ad/store services, with LevelPlay and Unity IAP adapters stubbed behind compile defines.

**Architecture:** Pure rules live in `EconomyCore` (`FreeGiftCore`, `PurchaseLedgerCore`, `StoreDefaults`) and are EditMode-tested. Two self-bootstrapping singletons, `FreeGiftManager` and `StoreManager`, talk to `IAdService`/`IStoreService` through the `MonetizationServices` factory. In Editor/Dev builds that factory returns fakes with a UITK dev overlay. All new UI is code-built UI Toolkit (the Almanac pattern: clone the Barn's PanelSettings), except the HUD Gift button. That button is uGUI, baked into the scene by cloning `EggClaimButton` (the Almanac "Bake HUD Button" pattern).

**Tech Stack:** Unity 6000.3, C#, UI Toolkit (runtime, code-built), uGUI + TextMeshPro (HUD button), LeanTween (`LeanTween.value` drives the UITK tweens), NUnit EditMode tests (`IdleFarm.EditModeTests` → `IdleFarm.EconomyCore`).

**Spec:** `docs/superpowers/specs/2026-10-04-free-gift-and-store-design.md`

## Global Constraints

- **No commits.** The user commits only when asked (memory rule). Each task ends with a **Checkpoint** (compile + tests), not a commit.
- **Copy is short and plain** (memory rule). Use exactly these strings:
  - Letter subject *"A little thank-you"*.
  - Tip *"A gift every 30 minutes. This one's on us!"*
  - No-fill toast *"No gift right now. Try again soon."*
  - Pass banner *"Farmer's Pass unlocked!"*
  - Store pass description *"The Farmer's Pass: your Free Gift chest, ready to open the moment it's ready. No ads, ever. Plus 500 gems."*
- **No emoji or surrogate characters in any UITK/TMP text.** They're invisible on Android, and `NarrativeDefaultsTests` asserts this.
- **Colours are game browns, walnut and gold.** Never blue buttons, and never a coloured one-side accent border (memory rule).
- **Never rebuild a list or grid on a tick.** Timers update label `.text` only (memory rule: rebuilding eats taps).
- **Fakes compile only under `UNITY_EDITOR || DEVELOPMENT_BUILD`.** A release build without `LEVELPLAY`/`UNITY_PURCHASING` uses the `Unavailable*` services and can never grant a purchase.
- **Input System only** (`UnityEngine.InputSystem`). This plan needs no direct input polling.
- **Logging:** `Debug.Log` only for important events (claims, purchases, deliveries). `LogWarning`/`LogError` freely.
- **Values from the spec:**
  - Gift: cooldown 30 min, daily cap 10, 10 gems/claim, pitch after 3 lifetime **ad** claims.
  - Coin anchors (Farm Level → coins): 0→30, 10→100, 25→600, 50→4,000, 100→6,000, 175→7,500.
  - Rounding: linear interpolation, rounded to the nearest 10 above 100.
  - Pass: `farmers_pass`, NonConsumable, $9.99, +500 gems.
  - Bundles:

    | ID | Name | Gems | Price | Ribbon |
    |---|---|---|---|---|
    | `gems_handful` | Handful | 100 | $0.99 | — |
    | `gems_pouch` | Pouch | 550 | $4.99 | +10% |
    | `gems_sack` | Sack | 1,200 | $9.99 | +20% |
    | `gems_chest` | Chest | 2,600 | $19.99 | +30% |
    | `gems_vault` | Vault | 7,000 | $49.99 | +40% |

  - Prices: Chicken 120 gems, Research slot 2 200 gems.
- **Farm Level** for the coin curve = `ReputationManager.Instance.PointsEarned`. It's monotonic and equals the Barn's "X / 175" once points are spent.
- **Unlock** = narrative flag `first_request_done` (`ReputationManager.WelcomeBasketFlag`) **or** `PointsEarned >= 1`.
- **Free chest** = the first claim of the local day (`claimsToday == 0`). This covers the welcome chest automatically.
- **Sort orders:** Store 1150, Chest reveal 1400, Pass pitch 1450, dev overlay 1900. For reference: toasts are 2000 and the tutorial 2500, both above these.

### Standard procedures (referenced by every task)

**Compile:** the Unity editor must be open on the project.
```bash
cd "/c/Users/rjcla/IdleFarm - Silo"
touch Temp/compile_marker; touch Temp/refresh.request
sleep 20
ls -l --time-style=+%s Library/ScriptAssemblies/Assembly-CSharp.dll Temp/compile_marker
```
Expected: the dll mtime is newer than `compile_marker`. If it isn't, read the errors with GladeKit `get_unity_console_logs` (filter Error) and fix them. If the editor hangs, use the memory recipe "Compile without Unity" (Unity's Roslyn + `Library/Bee/artifacts/*.rsp`).

**EditMode tests:**
```bash
cd "/c/Users/rjcla/IdleFarm - Silo"
rm -f Temp/editmode_test_results.txt; touch Temp/run_editmode_tests.request
for i in $(seq 1 60); do [ -f Temp/editmode_test_results.txt ] && break; sleep 3; done
head -40 Temp/editmode_test_results.txt
```
Expected: a `RESULT: Passed passed=N ...` line. Any failing test is listed after it.

**Run an editor menu:** `printf '%s' "Farm Game/Monetization/Build Assets" > Temp/menu.request` (UIDriveBridge). Confirm with `get_unity_console_logs`.

## Review Focus

1. **App backgrounded or killed after an ad completes but before the chest is tapped open:** the reward must still be granted and saved. *(Task 5: `ChestRevealUITK.OnApplicationPause` fires the pending grant. Task 12 step 9 verifies.)*
2. **A second reveal requested while one is showing** (a purchase delivered, or a restore re-delivering, while the gift chest is open): it must queue, not replace. Each `onOpened` fires exactly once. *(Task 5 queue + `grantFired` guard. Task 12 step 7 verifies.)*
3. **Mashing the HUD Gift button or the Store Watch button** while an ad or chest is already up: no second ad, no second claim. *(Task 6 `busy` guard. Task 12 step 3 verifies.)*
4. **Local midnight passes between claims, or a claim lands on a new day while capped:** the count resets, the first chest of the new day is free, and nothing gets stuck. *(Task 1 test `NewDay_ResetsCountAndFreeChest`.)*
5. **Restoring or re-delivering the Pass** (same or different transaction id) on a save that already owns it: no second +500 gems. *(Task 2 test `ShouldGrant_PassAlreadyOwned_NoGems`.)*

---

### Task 1: FreeGiftCore (pure rules)

**Files:**
- Create: `Assets/Scripts/EconomyCore/FreeGiftCore.cs`
- Test: `Assets/Tests/EditMode/FreeGiftCoreTests.cs`

**Interfaces:**
- Consumes: `OfflineClock.ForwardGapSeconds(long lastUtcTicks, long nowUtcTicks)` (existing, EconomyCore).
- Produces:
  - `struct GiftCoinAnchor { int level; int coins; GiftCoinAnchor(int level, int coins) }`
  - `enum GiftStatus { Locked, Ready, Cooldown, Capped }`
  - `class FreeGiftRules { double cooldownSeconds=1800; int dailyCap=10; int gemsPerClaim=10; int pitchAfterAdClaims=3; GiftCoinAnchor[] coinAnchors; static GiftCoinAnchor[] DefaultAnchors }`
  - `class FreeGiftCore`:
    - `FreeGiftCore(FreeGiftRules)`, `Rules`
    - `long LastClaimUtcTicks`, `string ClaimsDate`, `int ClaimsOnSavedDate`, `int LifetimeAdClaims`, `bool PitchShown`
    - `static string LocalDateKey(DateTime local)`
    - `int ClaimsToday(string today)`, `bool IsFreeChest(string today)`
    - `double SecondsUntilReady(long nowUtcTicks)`, `void HealClock(long nowUtcTicks)`
    - `GiftStatus Status(bool unlocked, long nowUtcTicks, string today)`
    - `bool RecordClaim(long nowUtcTicks, string today, bool viaAd)` (returns "show the pitch now")
    - `void Import(long lastClaimUtcTicks, string claimsDate, int claimsOnDate, int lifetimeAdClaims, bool pitchShown)`
    - `void MarkPitchShown()`
    - `static int CoinsForLevel(GiftCoinAnchor[] anchors, int level)`, `static int RoundGift(double raw)`

- [ ] **Step 1: Write the failing tests**

```csharp
// Assets/Tests/EditMode/FreeGiftCoreTests.cs
using System;
using NUnit.Framework;

public class FreeGiftCoreTests
{
    private const string Day1 = "2026-10-04";
    private const string Day2 = "2026-10-05";
    private static readonly long T0 = new DateTime(2026, 10, 4, 12, 0, 0, DateTimeKind.Utc).Ticks;
    private static long Min(double m) => (long)(m * TimeSpan.TicksPerMinute);

    private static FreeGiftCore NewCore() => new FreeGiftCore(new FreeGiftRules());

    [Test]
    public void FirstEverClaim_IsReadyAndFree()
    {
        var c = NewCore();
        Assert.AreEqual(GiftStatus.Ready, c.Status(true, T0, Day1));
        Assert.IsTrue(c.IsFreeChest(Day1));
        Assert.AreEqual(0, c.SecondsUntilReady(T0));
    }

    [Test]
    public void Locked_WhenNotUnlocked()
    {
        Assert.AreEqual(GiftStatus.Locked, NewCore().Status(false, T0, Day1));
    }

    [Test]
    public void AfterClaim_CooldownThirtyMinutes_ThenReady()
    {
        var c = NewCore();
        c.RecordClaim(T0, Day1, viaAd: false);
        Assert.AreEqual(GiftStatus.Cooldown, c.Status(true, T0 + Min(29), Day1));
        Assert.AreEqual(60, c.SecondsUntilReady(T0 + Min(29)), 0.001);
        Assert.AreEqual(GiftStatus.Ready, c.Status(true, T0 + Min(30), Day1));
    }

    [Test]
    public void SecondClaimOfDay_IsNotFree()
    {
        var c = NewCore();
        c.RecordClaim(T0, Day1, viaAd: false);
        Assert.IsFalse(c.IsFreeChest(Day1));
        Assert.AreEqual(1, c.ClaimsToday(Day1));
    }

    [Test]
    public void DailyCap_TenClaims_ThenCapped()
    {
        var c = NewCore();
        for (int i = 0; i < 10; i++) c.RecordClaim(T0 + Min(30 * i), Day1, viaAd: true);
        Assert.AreEqual(GiftStatus.Capped, c.Status(true, T0 + Min(30 * 10), Day1));
    }

    [Test]
    public void NewDay_ResetsCountAndFreeChest()
    {
        var c = NewCore();
        for (int i = 0; i < 10; i++) c.RecordClaim(T0 + Min(30 * i), Day1, viaAd: true);
        long nextDay = T0 + Min(60 * 14);
        Assert.AreEqual(GiftStatus.Ready, c.Status(true, nextDay, Day2));
        Assert.IsTrue(c.IsFreeChest(Day2));
        Assert.AreEqual(0, c.ClaimsToday(Day2));
        c.RecordClaim(nextDay, Day2, viaAd: false);
        Assert.AreEqual(1, c.ClaimsToday(Day2));
        Assert.AreEqual(0, c.ClaimsToday(Day1)); // the saved date moved on; old-day counts are gone
    }

    [Test]
    public void ClockRolledBack_CooldownNotShortened_AndHealReanchors()
    {
        var c = NewCore();
        c.RecordClaim(T0, Day1, viaAd: false);
        long earlier = T0 - Min(120);
        Assert.AreEqual(1800, c.SecondsUntilReady(earlier), 0.001); // full cooldown, never less
        c.HealClock(earlier);
        Assert.AreEqual(earlier, c.LastClaimUtcTicks);
        Assert.AreEqual(GiftStatus.Ready, c.Status(true, earlier + Min(30), Day1));
    }

    [Test]
    public void HealClock_NoOpWhenClockIsForward()
    {
        var c = NewCore();
        c.RecordClaim(T0, Day1, viaAd: false);
        c.HealClock(T0 + Min(5));
        Assert.AreEqual(T0, c.LastClaimUtcTicks);
    }

    [Test]
    public void Pitch_FiresOnceOnThirdAdClaim()
    {
        var c = NewCore();
        Assert.IsFalse(c.RecordClaim(T0, Day1, viaAd: true));
        Assert.IsFalse(c.RecordClaim(T0 + Min(30), Day1, viaAd: true));
        Assert.IsTrue(c.RecordClaim(T0 + Min(60), Day1, viaAd: true));
        Assert.IsTrue(c.PitchShown);
        Assert.IsFalse(c.RecordClaim(T0 + Min(90), Day1, viaAd: true));
    }

    [Test]
    public void Pitch_FreeAndPassClaimsDoNotCount()
    {
        var c = NewCore();
        for (int i = 0; i < 5; i++) Assert.IsFalse(c.RecordClaim(T0 + Min(30 * i), Day1, viaAd: false));
        Assert.AreEqual(0, c.LifetimeAdClaims);
        Assert.IsFalse(c.PitchShown);
    }

    [Test]
    public void ImportRoundTrip_RestoresState()
    {
        var c = NewCore();
        c.Import(T0, Day1, 4, 7, true);
        Assert.AreEqual(T0, c.LastClaimUtcTicks);
        Assert.AreEqual(4, c.ClaimsToday(Day1));
        Assert.AreEqual(7, c.LifetimeAdClaims);
        Assert.IsTrue(c.PitchShown);
    }

    [Test]
    public void Import_NullDateAndNegativeCounts_AreSafe()
    {
        var c = NewCore();
        c.Import(-5, null, -3, -1, false);
        Assert.AreEqual(0, c.ClaimsToday(Day1));
        Assert.AreEqual(0, c.LifetimeAdClaims);
        Assert.AreEqual(GiftStatus.Ready, c.Status(true, T0, Day1));
    }

    [TestCase(-5, 30)]
    [TestCase(0, 30)]
    [TestCase(5, 65)]
    [TestCase(10, 100)]
    [TestCase(17, 330)]   // 100 + 500 * 7/15 = 333.3 -> nearest 10
    [TestCase(25, 600)]
    [TestCase(50, 4000)]
    [TestCase(75, 5000)]
    [TestCase(175, 7500)]
    [TestCase(400, 7500)]
    public void CoinsForLevel_InterpolatesAndClamps(int level, int expected)
    {
        Assert.AreEqual(expected, FreeGiftCore.CoinsForLevel(FreeGiftRules.DefaultAnchors, level));
    }

    [Test]
    public void CoinsForLevel_EmptyAnchors_IsZero()
    {
        Assert.AreEqual(0, FreeGiftCore.CoinsForLevel(new GiftCoinAnchor[0], 10));
        Assert.AreEqual(0, FreeGiftCore.CoinsForLevel(null, 10));
    }

    [Test]
    public void LocalDateKey_IsInvariantIsoDate()
    {
        Assert.AreEqual("2026-01-09", FreeGiftCore.LocalDateKey(new DateTime(2026, 1, 9, 23, 59, 0)));
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run the **EditMode tests** procedure. Expected: compile error, "The type or namespace name 'FreeGiftCore' could not be found".

- [ ] **Step 3: Implement FreeGiftCore**

```csharp
// Assets/Scripts/EconomyCore/FreeGiftCore.cs
using System;
using System.Globalization;

/// <summary>One point on the Free Gift coin curve: at this Overall Farm Level the chest pays this many Coins.</summary>
[Serializable]
public struct GiftCoinAnchor
{
    public int level;
    public int coins;
    public GiftCoinAnchor(int level, int coins) { this.level = level; this.coins = coins; }
}

public enum GiftStatus { Locked, Ready, Cooldown, Capped }

/// <summary>Free Gift tunables as plain data, so FreeGiftCore stays testable. FreeGiftTuning (SO) builds one.</summary>
public sealed class FreeGiftRules
{
    public double cooldownSeconds = 1800;
    public int dailyCap = 10;
    public int gemsPerClaim = 10;
    public int pitchAfterAdClaims = 3;
    public GiftCoinAnchor[] coinAnchors = DefaultAnchors;

    /// <summary>~12% of an hour's Coins early, tapering to ~2% late (spec §3.1). Sorted by level.</summary>
    public static GiftCoinAnchor[] DefaultAnchors => new[]
    {
        new GiftCoinAnchor(0, 30), new GiftCoinAnchor(10, 100), new GiftCoinAnchor(25, 600),
        new GiftCoinAnchor(50, 4000), new GiftCoinAnchor(100, 6000), new GiftCoinAnchor(175, 7500),
    };
}

/// <summary>
/// The Free Gift chest's rules: 30-min cooldown from the last claim (one charge, no stacking),
/// a per-local-day cap, the day's first chest is free, and a one-time Farmer's Pass pitch after
/// the Nth ad-watched claim. Pure: callers pass UTC ticks and a local "yyyy-MM-dd" day key.
/// Clock-back never shortens the cooldown (OfflineClock); clock-forward is accepted (single-player).
/// </summary>
public sealed class FreeGiftCore
{
    private readonly FreeGiftRules rules;
    private int claimsOnDate;

    public FreeGiftRules Rules => rules;
    public long LastClaimUtcTicks { get; private set; }
    public string ClaimsDate { get; private set; } = "";
    public int ClaimsOnSavedDate => claimsOnDate;
    public int LifetimeAdClaims { get; private set; }
    public bool PitchShown { get; private set; }

    public FreeGiftCore(FreeGiftRules rules) { this.rules = rules ?? new FreeGiftRules(); }

    public static string LocalDateKey(DateTime local) => local.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    public int ClaimsToday(string today) => ClaimsDate == today ? claimsOnDate : 0;

    /// <summary>The first chest of each local day needs no ad (this also makes the welcome chest free).</summary>
    public bool IsFreeChest(string today) => ClaimsToday(today) == 0;

    public double SecondsUntilReady(long nowUtcTicks)
    {
        if (LastClaimUtcTicks <= 0) return 0;
        // Clock set back past the last claim -> ForwardGap is 0 -> the full cooldown applies.
        double elapsed = OfflineClock.ForwardGapSeconds(LastClaimUtcTicks, nowUtcTicks);
        return Math.Max(0, rules.cooldownSeconds - elapsed);
    }

    /// <summary>Clock moved back past the last claim: re-anchor so the cooldown runs from now.</summary>
    public void HealClock(long nowUtcTicks)
    {
        if (LastClaimUtcTicks > nowUtcTicks) LastClaimUtcTicks = nowUtcTicks;
    }

    public GiftStatus Status(bool unlocked, long nowUtcTicks, string today)
    {
        if (!unlocked) return GiftStatus.Locked;
        if (ClaimsToday(today) >= rules.dailyCap) return GiftStatus.Capped;
        return SecondsUntilReady(nowUtcTicks) > 0 ? GiftStatus.Cooldown : GiftStatus.Ready;
    }

    /// <summary>Records one claim. Returns true exactly once: when this ad claim should trigger the
    /// Farmer's Pass pitch (also marks it shown). Free and pass claims never count toward it.</summary>
    public bool RecordClaim(long nowUtcTicks, string today, bool viaAd)
    {
        if (ClaimsDate != today) { ClaimsDate = today ?? ""; claimsOnDate = 0; }
        claimsOnDate++;
        LastClaimUtcTicks = nowUtcTicks;
        if (!viaAd) return false;
        LifetimeAdClaims++;
        if (PitchShown || LifetimeAdClaims < rules.pitchAfterAdClaims) return false;
        PitchShown = true;
        return true;
    }

    public void MarkPitchShown() => PitchShown = true;

    public void Import(long lastClaimUtcTicks, string claimsDate, int claimsOnDate, int lifetimeAdClaims, bool pitchShown)
    {
        LastClaimUtcTicks = Math.Max(0, lastClaimUtcTicks);
        ClaimsDate = claimsDate ?? "";
        this.claimsOnDate = Math.Max(0, claimsOnDate);
        LifetimeAdClaims = Math.Max(0, lifetimeAdClaims);
        PitchShown = pitchShown;
    }

    /// <summary>Coins at an Overall Farm Level: linear between anchors (sorted by level), clamped
    /// at both ends, rounded to the nearest 10 above 100.</summary>
    public static int CoinsForLevel(GiftCoinAnchor[] anchors, int level)
    {
        if (anchors == null || anchors.Length == 0) return 0;
        if (level <= anchors[0].level) return anchors[0].coins;
        GiftCoinAnchor last = anchors[anchors.Length - 1];
        if (level >= last.level) return last.coins;
        for (int i = 1; i < anchors.Length; i++)
        {
            if (level > anchors[i].level) continue;
            GiftCoinAnchor a = anchors[i - 1], b = anchors[i];
            double t = (level - a.level) / (double)Math.Max(1, b.level - a.level);
            return RoundGift(a.coins + (b.coins - a.coins) * t);
        }
        return last.coins;
    }

    public static int RoundGift(double raw)
    {
        if (raw <= 100) return (int)Math.Round(raw, MidpointRounding.AwayFromZero);
        return (int)(Math.Round(raw / 10.0, MidpointRounding.AwayFromZero) * 10);
    }
}
```

- [ ] **Step 4: Run the tests to verify they pass**

Run the **Compile** then **EditMode tests** procedures. Expected: `RESULT: Passed`, with every `FreeGiftCoreTests` case passing.

- [ ] **Step 5: Checkpoint.** Compile is clean and the suite is green. Don't commit.

---

### Task 2: Purchase ledger, store defaults, and service contracts

**Files:**
- Create: `Assets/Scripts/EconomyCore/PurchaseLedgerCore.cs`
- Create: `Assets/Scripts/EconomyCore/StoreDefaults.cs`
- Create: `Assets/Scripts/Monetization/Services/MonetizationContracts.cs`
- Test: `Assets/Tests/EditMode/PurchaseLedgerCoreTests.cs`, `Assets/Tests/EditMode/StoreDefaultsTests.cs`

**Interfaces:**
- Produces (EconomyCore):
  - `class PurchaseLedgerCore { bool IsDelivered(string); bool TryMarkDelivered(string); string[] Export(); void Import(string[]); static bool ShouldGrant(bool ledgerIsNew, bool isNonConsumable, bool alreadyOwned) }`
  - `enum ProductKind { Consumable, NonConsumable }`, `enum StoreSection { Pass, Gems, ComingSoon }`
  - `[Serializable] class StoreProductDef { string id; string displayName; ProductKind kind; StoreSection section; int gems; string fallbackPrice; string description; string ribbon; bool comingSoon; Sprite icon; }`
  - `static class StoreDefaults { const string PassId = "farmers_pass"; const string PassDescription; static StoreProductDef[] Products }`
- Produces (Monetization):
  - `enum AdFailReason { NotReady, Closed, Error }`
  - `interface IAdService { bool IsReady { get; } void ShowRewarded(Action onRewarded, Action<AdFailReason> onFailed); }`
  - `enum PurchaseOutcome { Success, Cancelled, Failed, Unavailable }`
  - `struct PurchaseResult { string productId; string transactionId; PurchaseOutcome outcome; }`
  - `interface IStoreService { bool IsInitialized { get; } event Action<PurchaseResult> OnPendingPurchase; void Initialize(Action<bool> onDone); string GetPriceString(string productId); void Purchase(string productId, Action<PurchaseResult> onResult); void ConfirmDelivered(string transactionId); void Restore(Action<bool> onDone); bool? IsOwned(string productId); }`

- [ ] **Step 1: Write the failing tests**

```csharp
// Assets/Tests/EditMode/PurchaseLedgerCoreTests.cs
using NUnit.Framework;

public class PurchaseLedgerCoreTests
{
    [Test]
    public void FirstDelivery_IsNew_DuplicateIsNot()
    {
        var l = new PurchaseLedgerCore();
        Assert.IsTrue(l.TryMarkDelivered("tx-1"));
        Assert.IsFalse(l.TryMarkDelivered("tx-1"));
        Assert.IsTrue(l.IsDelivered("tx-1"));
    }

    [Test]
    public void EmptyTransactionId_AlwaysDeliverable_NeverRecorded()
    {
        var l = new PurchaseLedgerCore();
        Assert.IsTrue(l.TryMarkDelivered(""));
        Assert.IsTrue(l.TryMarkDelivered(null));
        Assert.AreEqual(0, l.Export().Length);
    }

    [Test]
    public void ExportImport_RoundTrips_AndSkipsBlanks()
    {
        var l = new PurchaseLedgerCore();
        l.TryMarkDelivered("b"); l.TryMarkDelivered("a");
        var copy = new PurchaseLedgerCore();
        copy.Import(new[] { "b", "a", "", null });
        CollectionAssert.AreEqual(new[] { "a", "b" }, copy.Export());
        Assert.IsFalse(copy.TryMarkDelivered("a"));
    }

    [Test]
    public void Import_Null_IsEmpty()
    {
        var l = new PurchaseLedgerCore();
        l.Import(null);
        Assert.AreEqual(0, l.Export().Length);
    }

    [Test]
    public void ShouldGrant_NewConsumable_Grants() =>
        Assert.IsTrue(PurchaseLedgerCore.ShouldGrant(true, isNonConsumable: false, alreadyOwned: false));

    [Test]
    public void ShouldGrant_DuplicateTransaction_NoGrant() =>
        Assert.IsFalse(PurchaseLedgerCore.ShouldGrant(false, isNonConsumable: false, alreadyOwned: false));

    [Test]
    public void ShouldGrant_PassAlreadyOwned_NoGems() =>
        Assert.IsFalse(PurchaseLedgerCore.ShouldGrant(true, isNonConsumable: true, alreadyOwned: true));

    [Test]
    public void ShouldGrant_FirstPass_Grants() =>
        Assert.IsTrue(PurchaseLedgerCore.ShouldGrant(true, isNonConsumable: true, alreadyOwned: false));
}
```

```csharp
// Assets/Tests/EditMode/StoreDefaultsTests.cs
using System.Linq;
using NUnit.Framework;

public class StoreDefaultsTests
{
    [Test]
    public void ProductIds_AreUnique()
    {
        var ids = StoreDefaults.Products.Select(p => p.id).ToArray();
        Assert.AreEqual(ids.Length, ids.Distinct().Count());
    }

    [Test]
    public void Pass_IsTheOnlyNonConsumable_With500Gems()
    {
        var nonCons = StoreDefaults.Products.Where(p => p.kind == ProductKind.NonConsumable).ToArray();
        Assert.AreEqual(1, nonCons.Length);
        Assert.AreEqual(StoreDefaults.PassId, nonCons[0].id);
        Assert.AreEqual(500, nonCons[0].gems);
        Assert.AreEqual("$9.99", nonCons[0].fallbackPrice);
        Assert.AreEqual(StoreDefaults.PassDescription, nonCons[0].description);
    }

    [Test]
    public void GemBundles_MatchSpecAndAscend()
    {
        var bundles = StoreDefaults.Products.Where(p => p.section == StoreSection.Gems).ToArray();
        CollectionAssert.AreEqual(new[] { "gems_handful", "gems_pouch", "gems_sack", "gems_chest", "gems_vault" }, bundles.Select(b => b.id).ToArray());
        CollectionAssert.AreEqual(new[] { 100, 550, 1200, 2600, 7000 }, bundles.Select(b => b.gems).ToArray());
        CollectionAssert.AreEqual(new[] { "$0.99", "$4.99", "$9.99", "$19.99", "$49.99" }, bundles.Select(b => b.fallbackPrice).ToArray());
    }

    [Test]
    public void ComingSoon_HasFourDisabledTeasers()
    {
        var soon = StoreDefaults.Products.Where(p => p.section == StoreSection.ComingSoon).ToArray();
        Assert.AreEqual(4, soon.Length);
        Assert.IsTrue(soon.All(p => p.comingSoon));
    }

    [Test]
    public void Copy_HasNoEmojiOrSurrogates()
    {
        foreach (var p in StoreDefaults.Products)
            foreach (string s in new[] { p.displayName, p.description, p.ribbon })
                if (s != null) foreach (char c in s) Assert.IsFalse(char.IsSurrogate(c), $"{p.id}: {s}");
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run the **EditMode tests** procedure. Expected: compile errors for `PurchaseLedgerCore`, `StoreDefaults`, `ProductKind`.

- [ ] **Step 3: Implement**

```csharp
// Assets/Scripts/EconomyCore/PurchaseLedgerCore.cs
using System;
using System.Collections.Generic;

/// <summary>
/// Remembers which store transactions were already delivered, so a purchase the store re-sends
/// (app killed between grant and confirm, or a restore) never grants twice. Saved in GameData.
/// </summary>
public sealed class PurchaseLedgerCore
{
    private readonly HashSet<string> delivered = new HashSet<string>(StringComparer.Ordinal);

    public bool IsDelivered(string transactionId) =>
        !string.IsNullOrEmpty(transactionId) && delivered.Contains(transactionId);

    /// <summary>True the first time (grant), false for a re-delivery. An empty id can't be
    /// de-duplicated, so it is always deliverable and never recorded.</summary>
    public bool TryMarkDelivered(string transactionId)
    {
        if (string.IsNullOrEmpty(transactionId)) return true;
        return delivered.Add(transactionId);
    }

    public string[] Export()
    {
        var ids = new string[delivered.Count];
        delivered.CopyTo(ids);
        Array.Sort(ids, StringComparer.Ordinal);
        return ids;
    }

    public void Import(string[] ids)
    {
        delivered.Clear();
        if (ids == null) return;
        foreach (string id in ids) if (!string.IsNullOrEmpty(id)) delivered.Add(id);
    }

    /// <summary>Whether a delivery grants its gems. A permanent unlock the save already owns
    /// grants nothing again, even under a new transaction id (iOS restores mint new ids).</summary>
    public static bool ShouldGrant(bool ledgerIsNew, bool isNonConsumable, bool alreadyOwned) =>
        ledgerIsNew && !(isNonConsumable && alreadyOwned);
}
```

```csharp
// Assets/Scripts/EconomyCore/StoreDefaults.cs
using System;
using UnityEngine;

public enum ProductKind { Consumable, NonConsumable }
public enum StoreSection { Pass, Gems, ComingSoon }

/// <summary>One Store product. The catalog asset (Resources/StoreCatalog) holds these; the starting
/// values come from StoreDefaults, seeded by Farm Game > Monetization > Build Assets.</summary>
[Serializable]
public class StoreProductDef
{
    public string id;
    public string displayName;
    public ProductKind kind;
    public StoreSection section;
    public int gems;
    [Tooltip("Shown until the real store reports a localized price.")]
    public string fallbackPrice;
    [TextArea(2, 4)] public string description;
    [Tooltip("Small corner ribbon, e.g. \"+20%\". Empty = none.")]
    public string ribbon;
    public bool comingSoon;
    public Sprite icon;
}

/// <summary>Starting Store catalog (spec §6). Product ids must match the store consoles in phase 2.</summary>
public static class StoreDefaults
{
    public const string PassId = "farmers_pass";
    public const string PassDescription =
        "The Farmer's Pass: your Free Gift chest, ready to open the moment it's ready. No ads, ever. Plus 500 gems.";

    public static StoreProductDef[] Products => new[]
    {
        new StoreProductDef { id = PassId, displayName = "Farmer's Pass", kind = ProductKind.NonConsumable,
            section = StoreSection.Pass, gems = 500, fallbackPrice = "$9.99", description = PassDescription },

        Bundle("gems_handful", "Handful", 100, "$0.99", ""),
        Bundle("gems_pouch", "Pouch", 550, "$4.99", "+10%"),
        Bundle("gems_sack", "Sack", 1200, "$9.99", "+20%"),
        Bundle("gems_chest", "Chest", 2600, "$19.99", "+30%"),
        Bundle("gems_vault", "Vault", 7000, "$49.99", "+40%"),

        Soon("soon_harvest_blessing", "Harvest Blessing", "Bigger harvests, forever."),
        Soon("soon_animal_skins", "Animal Skins", "New looks for your animals."),
        Soon("soon_building_skins", "Building Skins", "Dress up the farm."),
        Soon("soon_farm_themes", "Farm Themes", "A whole new feel."),
    };

    private static StoreProductDef Bundle(string id, string name, int gems, string price, string ribbon) =>
        new StoreProductDef { id = id, displayName = name, kind = ProductKind.Consumable, section = StoreSection.Gems,
            gems = gems, fallbackPrice = price, ribbon = ribbon, description = "" };

    private static StoreProductDef Soon(string id, string name, string description) =>
        new StoreProductDef { id = id, displayName = name, kind = ProductKind.NonConsumable, section = StoreSection.ComingSoon,
            comingSoon = true, description = description, fallbackPrice = "", ribbon = "" };
}
```

```csharp
// Assets/Scripts/Monetization/Services/MonetizationContracts.cs
using System;

public enum AdFailReason { NotReady, Closed, Error }

/// <summary>Rewarded video ads. onRewarded fires only when the reward is earned (ad finished);
/// onFailed(Closed) when the player closes early, onFailed(NotReady) when there is no ad to show.</summary>
public interface IAdService
{
    bool IsReady { get; }
    void ShowRewarded(Action onRewarded, Action<AdFailReason> onFailed);
}

public enum PurchaseOutcome { Success, Cancelled, Failed, Unavailable }

public struct PurchaseResult
{
    public string productId;
    public string transactionId;
    public PurchaseOutcome outcome;
}

/// <summary>Real-money store. Delivery contract: grant + save, THEN ConfirmDelivered(transactionId).
/// Purchases the store re-sends (interrupted, restored) arrive via OnPendingPurchase.</summary>
public interface IStoreService
{
    bool IsInitialized { get; }
    event Action<PurchaseResult> OnPendingPurchase;
    void Initialize(Action<bool> onDone);
    /// <summary>Localized price, or null when unknown (caller shows the catalog fallback).</summary>
    string GetPriceString(string productId);
    void Purchase(string productId, Action<PurchaseResult> onResult);
    void ConfirmDelivered(string transactionId);
    void Restore(Action<bool> onDone);
    /// <summary>True/false from the store account; null when the store can't say (offline).</summary>
    bool? IsOwned(string productId);
}
```

- [ ] **Step 4: Run the tests to verify they pass**

Run **Compile** then **EditMode tests**. Expected: `RESULT: Passed`.

- [ ] **Step 5: Checkpoint.** Don't commit.

---

### Task 3: Data assets, the build-assets menu, and the gem price changes

**Files:**
- Create: `Assets/Scripts/Monetization/FreeGiftTuning.cs`
- Create: `Assets/Scripts/Monetization/StoreCatalogSO.cs`
- Create: `Assets/Scripts/Monetization/MonetizationArt.cs`
- Create: `Assets/Editor/MonetizationTools.cs`
- Modify: `Assets/Scripts/ResearchManager.cs:31` (code default 100 → 200)
- Generated by menu: `Assets/Resources/FreeGiftTuning.asset`, `Assets/Resources/StoreCatalog.asset`, `Assets/Resources/MonetizationArt.asset`. Edited by menu: `Assets/Data/Animals/Animal_Chicken.asset` (`gemCost` 120), `Assets/Scenes/FarmMain.unity` (ResearchManager `slotDefs[1].costAmount` 200).

**Interfaces:**
- Consumes: `FreeGiftRules`, `GiftCoinAnchor`, `StoreProductDef`, `StoreDefaults` (Tasks 1-2).
- Produces:
  - `FreeGiftTuning : ScriptableObject { static FreeGiftTuning Instance; FreeGiftRules ToRules(); GiftCoinAnchor[] coinAnchors; }`. `Instance` is never null: it falls back to an in-memory default.
  - `StoreCatalogSO : ScriptableObject { static StoreCatalogSO Instance; StoreProductDef[] products; StoreProductDef Get(string id); }`. `Instance` falls back to `StoreDefaults.Products`.
  - `MonetizationArt : ScriptableObject { static MonetizationArt Instance; Sprite chestClosed, chestOpen, gemIcon, coinIcon, passArt; FontAsset numberFont; }`
  - Menus: `Farm Game/Monetization/Build Assets`, `Farm Game/Monetization/Apply Gem Prices`.

- [ ] **Step 1: Write the ScriptableObjects**

```csharp
// Assets/Scripts/Monetization/FreeGiftTuning.cs
using UnityEngine;

/// <summary>Free Gift knobs (spec §3). Resources/FreeGiftTuning, built by Farm Game > Monetization > Build Assets.
/// Re-tune in the balance pass; anchors are kept sorted by level.</summary>
public class FreeGiftTuning : ScriptableObject
{
    [Min(1)] public float cooldownMinutes = 30f;
    [Min(1)] public int dailyCap = 10;
    [Min(0)] public int gemsPerClaim = 10;
    [Min(1)] public int pitchAfterAdClaims = 3;
    [Tooltip("Overall Farm Level -> Coins per chest. Linear between points, clamped at the ends.")]
    public GiftCoinAnchor[] coinAnchors = FreeGiftRules.DefaultAnchors;

    private static FreeGiftTuning cached;
    public static FreeGiftTuning Instance
    {
        get
        {
            if (cached != null) return cached;
            cached = Resources.Load<FreeGiftTuning>("FreeGiftTuning");
            if (cached == null)
            {
                Debug.LogWarning("[FreeGift] Resources/FreeGiftTuning missing; using defaults. Run Farm Game > Monetization > Build Assets.");
                cached = CreateInstance<FreeGiftTuning>();
            }
            return cached;
        }
    }

    public FreeGiftRules ToRules() => new FreeGiftRules
    {
        cooldownSeconds = cooldownMinutes * 60.0,
        dailyCap = dailyCap,
        gemsPerClaim = gemsPerClaim,
        pitchAfterAdClaims = pitchAfterAdClaims,
        coinAnchors = coinAnchors,
    };

    private void OnValidate()
    {
        if (coinAnchors != null) System.Array.Sort(coinAnchors, (a, b) => a.level.CompareTo(b.level));
    }
}
```

```csharp
// Assets/Scripts/Monetization/StoreCatalogSO.cs
using UnityEngine;

/// <summary>The Store's products (spec §6). Resources/StoreCatalog; new products are data, not code.
/// Copy is edited here. Build Assets only appends missing ids, never overwrites.</summary>
public class StoreCatalogSO : ScriptableObject
{
    public StoreProductDef[] products = new StoreProductDef[0];

    private static StoreCatalogSO cached;
    public static StoreCatalogSO Instance
    {
        get
        {
            if (cached != null) return cached;
            cached = Resources.Load<StoreCatalogSO>("StoreCatalog");
            if (cached == null)
            {
                Debug.LogWarning("[Store] Resources/StoreCatalog missing; using StoreDefaults. Run Farm Game > Monetization > Build Assets.");
                cached = CreateInstance<StoreCatalogSO>();
                cached.products = StoreDefaults.Products;
            }
            return cached;
        }
    }

    public StoreProductDef Get(string id)
    {
        if (products == null || string.IsNullOrEmpty(id)) return null;
        foreach (StoreProductDef p in products) if (p != null && p.id == id) return p;
        return null;
    }
}
```

```csharp
// Assets/Scripts/Monetization/MonetizationArt.cs
using UnityEngine;

/// <summary>Art for the Free Gift chest, Store and pitch. Resources/MonetizationArt so the self-bootstrapping
/// UI needs no scene wiring. Swap the chest sprites for a sack/basket/animation later.</summary>
public class MonetizationArt : ScriptableObject
{
    [Header("Chest")]
    public Sprite chestClosed;
    public Sprite chestOpen;

    [Header("Currency icons")]
    public Sprite gemIcon;
    public Sprite coinIcon;

    [Header("Store")]
    [Tooltip("Hero art for the Farmer's Pass card and pitch (user will supply). Falls back to the closed chest.")]
    public Sprite passArt;

    [Tooltip("Pixel font for the big reward numbers and titles (UITK TextCore FontAsset).")]
    public UnityEngine.TextCore.Text.FontAsset numberFont;

    private static MonetizationArt cached;
    public static MonetizationArt Instance =>
        cached != null ? cached : (cached = Resources.Load<MonetizationArt>("MonetizationArt"));
}
```

- [ ] **Step 2: Write the editor menus**

```csharp
// Assets/Editor/MonetizationTools.cs
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>Farm Game > Monetization: build the Resources assets, apply the gem price changes,
/// and (Task 8) bake the HUD Gift button. All idempotent; runnable via Temp/menu.request.</summary>
public static class MonetizationTools
{
    private const string TuningPath = "Assets/Resources/FreeGiftTuning.asset";
    private const string CatalogPath = "Assets/Resources/StoreCatalog.asset";
    private const string ArtPath = "Assets/Resources/MonetizationArt.asset";

    private const string ChestClosedPath = "Assets/Sprites/UI/Icons/Cute/RpgThings/Chest_Red.png";
    private const string ChestOpenPath = "Assets/Sprites/UI/Icons/Cute/RpgThings/Chest_GoldOpen.png";
    private const string GemIconPath = "Assets/Sprites/UI/Icons/Icons_Essential/Gem.png";
    private const string CoinIconPath = "Assets/Sprites/UI/Icons/Icons_Essential/Coin.png";
    private const string ChickenPath = "Assets/Data/Animals/Animal_Chicken.asset";

    [MenuItem("Farm Game/Monetization/Build Assets")]
    public static void BuildAssets()
    {
        var tuning = AssetDatabase.LoadAssetAtPath<FreeGiftTuning>(TuningPath);
        if (tuning == null) { tuning = ScriptableObject.CreateInstance<FreeGiftTuning>(); AssetDatabase.CreateAsset(tuning, TuningPath); }

        var catalog = AssetDatabase.LoadAssetAtPath<StoreCatalogSO>(CatalogPath);
        if (catalog == null) { catalog = ScriptableObject.CreateInstance<StoreCatalogSO>(); AssetDatabase.CreateAsset(catalog, CatalogPath); }
        var products = new List<StoreProductDef>(catalog.products ?? new StoreProductDef[0]);
        int added = 0;
        foreach (StoreProductDef d in StoreDefaults.Products)
            if (products.TrueForAll(p => p == null || p.id != d.id)) { products.Add(d); added++; }
        Sprite gem = LoadSprite(GemIconPath), chestClosed = LoadSprite(ChestClosedPath);
        foreach (StoreProductDef p in products)
        {
            if (p == null || p.icon != null) continue;
            if (p.section == StoreSection.Gems) p.icon = gem;
            else if (p.section == StoreSection.Pass) p.icon = chestClosed;
        }
        catalog.products = products.ToArray();
        EditorUtility.SetDirty(catalog);

        var art = AssetDatabase.LoadAssetAtPath<MonetizationArt>(ArtPath);
        if (art == null) { art = ScriptableObject.CreateInstance<MonetizationArt>(); AssetDatabase.CreateAsset(art, ArtPath); }
        if (art.chestClosed == null) art.chestClosed = chestClosed;
        if (art.chestOpen == null) art.chestOpen = LoadSprite(ChestOpenPath);
        if (art.gemIcon == null) art.gemIcon = gem;
        if (art.coinIcon == null) art.coinIcon = LoadSprite(CoinIconPath);
        if (art.numberFont == null)
        {
            var almanac = Resources.Load<AlmanacArt>("AlmanacArt");
            if (almanac != null) art.numberFont = almanac.titleFont;
        }
        EditorUtility.SetDirty(art);

        AssetDatabase.SaveAssets();
        Debug.Log($"[Monetization] Built assets: tuning, catalog (+{added} products), art " +
                  $"(chest {(art.chestClosed != null ? "ok" : "MISSING")}/{(art.chestOpen != null ? "ok" : "MISSING")}, " +
                  $"font {(art.numberFont != null ? "ok" : "MISSING")}).");
    }

    [MenuItem("Farm Game/Monetization/Apply Gem Prices")]
    public static void ApplyGemPrices()
    {
        var chicken = AssetDatabase.LoadAssetAtPath<AnimalData>(ChickenPath);
        if (chicken != null)
        {
            var so = new SerializedObject(chicken);
            so.FindProperty("gemCost").intValue = 120;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(chicken);
        }
        else Debug.LogError("[Monetization] Chicken asset not found at " + ChickenPath);

        var research = Object.FindFirstObjectByType<ResearchManager>(FindObjectsInactive.Include);
        if (research != null)
        {
            var so = new SerializedObject(research);
            SerializedProperty defs = so.FindProperty("slotDefs");
            for (int i = 0; i < defs.arraySize; i++)
            {
                SerializedProperty d = defs.GetArrayElementAtIndex(i);
                if (d.FindPropertyRelative("unlockType").enumValueIndex == (int)ResearchManager.SlotUnlockType.Gems)
                    d.FindPropertyRelative("costAmount").intValue = 200;
            }
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorSceneManager.MarkSceneDirty(research.gameObject.scene);
            EditorSceneManager.SaveScene(research.gameObject.scene);
        }
        else Debug.LogError("[Monetization] No ResearchManager in the open scene.");

        AssetDatabase.SaveAssets();
        Debug.Log("[Monetization] Applied gem prices: Chicken 120, research gem slot 200.");
    }

    private static Sprite LoadSprite(string path)
    {
        Sprite s = AssetDatabase.LoadAssetAtPath<Sprite>(path);
        if (s == null)
            foreach (Object o in AssetDatabase.LoadAllAssetsAtPath(path))
                if (o is Sprite sp) { s = sp; break; }
        if (s == null) Debug.LogWarning("[Monetization] Sprite not found: " + path);
        return s;
    }
}
```

Note: check that `ResearchManager.SlotUnlockType` is public. `ResearchPopupUITK.cs:347` uses `ResearchManager.SlotUnlockType.Gems`, so it is.

- [ ] **Step 3: Update the ResearchManager code default**

In `Assets/Scripts/ResearchManager.cs`, change line 31 from
`new SlotDefinition { unlockType = SlotUnlockType.Gems,  costAmount = 100 },`
to
`new SlotDefinition { unlockType = SlotUnlockType.Gems,  costAmount = 200 },`

- [ ] **Step 4: Compile, then run both menus**

Run **Compile**. Make sure `FarmMain.unity` is the open scene: GladeKit `open_scene` `Assets/Scenes/FarmMain.unity` if needed. Then:
```bash
cd "/c/Users/rjcla/IdleFarm - Silo"
printf '%s' "Farm Game/Monetization/Build Assets" > Temp/menu.request; sleep 6
printf '%s' "Farm Game/Monetization/Apply Gem Prices" > Temp/menu.request; sleep 6
grep -n "gemCost" Assets/Data/Animals/Animal_Chicken.asset
grep -n "slotDefs" -A6 Assets/Scenes/FarmMain.unity | grep costAmount
ls Assets/Resources/FreeGiftTuning.asset Assets/Resources/StoreCatalog.asset Assets/Resources/MonetizationArt.asset
```
Expected:
- `gemCost: 120`.
- The second `costAmount` line reads `200`.
- All three assets exist.
- The console shows `[Monetization] Built assets ... chest ok/ok, font ok`.

- [ ] **Step 5: Checkpoint.** Run **EditMode tests**: still green. Don't commit.

---

### Task 4: Services — fakes, unavailable, stubs, factory, dev overlay, UI helpers

**Files:**
- Create: `Assets/Scripts/Monetization/UI/MonetizationUI.cs`
- Create: `Assets/Scripts/Monetization/Services/MonetizationServices.cs`
- Create: `Assets/Scripts/Monetization/Services/UnavailableServices.cs`
- Create: `Assets/Scripts/Monetization/Services/FakeAdService.cs`
- Create: `Assets/Scripts/Monetization/Services/FakeStoreService.cs`
- Create: `Assets/Scripts/Monetization/Services/MonetizationDevOverlay.cs`
- Create: `Assets/Scripts/Monetization/Services/LevelPlayAdService.cs`
- Create: `Assets/Scripts/Monetization/Services/UnityIapStoreService.cs`

**Interfaces:**
- Consumes: `IAdService`, `IStoreService`, `PurchaseResult`, `PurchaseOutcome`, `AdFailReason` (Task 2); `StoreCatalogSO`, `MonetizationArt` (Task 3); `BarnPopupUITK.Instance.SourcePanelSettings/BoardFrame/FrameSlice/FrameSliceScale/InteriorWash/TitleFont` (existing).
- Produces:
  - `static class MonetizationServices { IAdService Ads; IStoreService Store; bool IsTestStore; }`
  - `FakeAdService.SimulateNoFill` (static bool, dev only), `FakeStoreService.DevClearAccount()` (dev only)
  - `static class MonetizationUI`:
    - Colours: `Walnut, WalnutDark, Cream, Gold, Ink, Muted`
    - `VisualElement CreateRoot(MonoBehaviour host, int sortOrder, string name, out PanelSettings runtimeSettings)`
    - Layout/style: `void Fill(VisualElement)`, `void Radius(VisualElement, float)`, `void NoBorder(VisualElement)`
    - Element builders: `VisualElement Card()`, `Button BrownButton(string text, Action onClick, int fontSize = 30)`, `Label Text(string text, int size, Color color, bool bold = false)`, `VisualElement Icon(Sprite sprite, float size)`
    - Fonts: `void Font(VisualElement e, FontAsset f)`, `FontAsset TitleFont`
    - `string Mmss(double seconds)`

- [ ] **Step 1: Write the shared UI helpers**

```csharp
// Assets/Scripts/Monetization/UI/MonetizationUI.cs
using System;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>Code-built UITK helpers shared by the Store, chest reveal, pass pitch and dev overlay.
/// Same approach as the Almanac: clone the Barn's PanelSettings, reuse its board frame and fonts.
/// Game browns and gold only (no blue, no accent stripes).</summary>
public static class MonetizationUI
{
    public static readonly Color Walnut = new Color(0.35f, 0.22f, 0.10f);
    public static readonly Color WalnutDark = new Color(0.24f, 0.15f, 0.07f);
    public static readonly Color Cream = new Color(0.98f, 0.93f, 0.82f);
    public static readonly Color Gold = new Color(0.93f, 0.71f, 0.27f);
    public static readonly Color Ink = new Color(0.23f, 0.15f, 0.08f);
    public static readonly Color Muted = new Color(0.50f, 0.44f, 0.38f);

    /// <summary>Adds a UIDocument to <paramref name="host"/> at <paramref name="sortOrder"/> and returns its
    /// full-screen root (picking Ignore until a popup opens). Null when the Barn panel settings are missing.</summary>
    public static VisualElement CreateRoot(MonoBehaviour host, int sortOrder, string name, out PanelSettings runtimeSettings)
    {
        runtimeSettings = null;
        BarnPopupUITK barn = BarnPopupUITK.Instance;
        PanelSettings source = barn != null ? barn.SourcePanelSettings : null;
        if (source == null) { Debug.LogWarning($"[{name}] No panel settings to clone (BarnPopupUITK missing)."); return null; }

        runtimeSettings = UnityEngine.Object.Instantiate(source);
        runtimeSettings.name = name + "PanelSettings (runtime)";
        runtimeSettings.sortingOrder = sortOrder;
        var document = host.gameObject.AddComponent<UIDocument>();
        document.enabled = false;
        document.panelSettings = runtimeSettings;
        document.enabled = true;

        VisualElement root = document.rootVisualElement;
        root.pickingMode = PickingMode.Ignore;
        root.style.height = Length.Percent(100);
        return root;
    }

    public static void Fill(VisualElement e)
    {
        e.style.position = Position.Absolute;
        e.style.left = 0; e.style.right = 0; e.style.top = 0; e.style.bottom = 0;
    }

    public static void Radius(VisualElement e, float r)
    {
        e.style.borderTopLeftRadius = r; e.style.borderTopRightRadius = r;
        e.style.borderBottomLeftRadius = r; e.style.borderBottomRightRadius = r;
    }

    public static void NoBorder(VisualElement e)
    {
        e.style.borderTopWidth = 0; e.style.borderBottomWidth = 0;
        e.style.borderLeftWidth = 0; e.style.borderRightWidth = 0;
    }

    /// <summary>The Barn's 9-sliced wood board with its interior wash, or a plain parchment block.</summary>
    public static VisualElement Card()
    {
        var card = new VisualElement();
        BarnPopupUITK barn = BarnPopupUITK.Instance;
        int border = barn != null ? Mathf.RoundToInt(barn.FrameSlice * barn.FrameSliceScale) : 0;
        card.style.paddingLeft = border + 22; card.style.paddingRight = border + 22;
        card.style.paddingTop = border + 20; card.style.paddingBottom = border + 20;
        if (barn != null && barn.BoardFrame != null)
        {
            card.style.backgroundImage = new StyleBackground(barn.BoardFrame);
            card.style.unitySliceLeft = barn.FrameSlice; card.style.unitySliceRight = barn.FrameSlice;
            card.style.unitySliceTop = barn.FrameSlice; card.style.unitySliceBottom = barn.FrameSlice;
            card.style.unitySliceScale = barn.FrameSliceScale;
            if (barn.InteriorWash.a > 0f)
            {
                var wash = new VisualElement { pickingMode = PickingMode.Ignore };
                wash.style.position = Position.Absolute;
                wash.style.left = border; wash.style.right = border; wash.style.top = border; wash.style.bottom = border;
                wash.style.backgroundColor = barn.InteriorWash;
                Radius(wash, 6);
                card.Add(wash);
            }
        }
        else
        {
            card.style.backgroundColor = new Color(0.70f, 0.60f, 0.43f);
            Radius(card, 14);
        }
        return card;
    }

    public static Button BrownButton(string text, Action onClick, int fontSize = 30)
    {
        var b = new Button(onClick) { text = text };
        b.style.fontSize = fontSize;
        b.style.unityFontStyleAndWeight = FontStyle.Bold;
        b.style.color = Cream;
        b.style.backgroundColor = Walnut;
        NoBorder(b);
        Radius(b, 12);
        b.style.height = fontSize * 2.1f;
        b.style.paddingLeft = 22; b.style.paddingRight = 22;
        b.style.marginLeft = 0; b.style.marginRight = 0;
        return b;
    }

    public static Label Text(string text, int size, Color color, bool bold = false)
    {
        var l = new Label(text);
        l.style.fontSize = size;
        l.style.color = color;
        l.style.whiteSpace = WhiteSpace.Normal;
        if (bold) l.style.unityFontStyleAndWeight = FontStyle.Bold;
        l.pickingMode = PickingMode.Ignore;
        return l;
    }

    public static VisualElement Icon(Sprite sprite, float size)
    {
        var e = new VisualElement { pickingMode = PickingMode.Ignore };
        e.style.width = size; e.style.height = size;
        e.style.flexShrink = 0;
        if (sprite != null)
        {
            e.style.backgroundImage = new StyleBackground(sprite);
            e.style.backgroundSize = new BackgroundSize(BackgroundSizeType.Contain);
        }
        return e;
    }

    public static UnityEngine.TextCore.Text.FontAsset TitleFont
    {
        get
        {
            MonetizationArt art = MonetizationArt.Instance;
            if (art != null && art.numberFont != null) return art.numberFont;
            return BarnPopupUITK.Instance != null ? BarnPopupUITK.Instance.TitleFont : null;
        }
    }

    public static void Font(VisualElement e, UnityEngine.TextCore.Text.FontAsset f)
    {
        if (f != null) e.style.unityFontDefinition = new StyleFontDefinition(f);
    }

    public static string Mmss(double seconds)
    {
        int s = Mathf.Max(0, Mathf.CeilToInt((float)seconds));
        return $"{s / 60:00}:{s % 60:00}";
    }
}
```

- [ ] **Step 2: Write the factory, the unavailable services, and the stubs**

```csharp
// Assets/Scripts/Monetization/Services/MonetizationServices.cs
using UnityEngine;

/// <summary>
/// Picks the ad and store services once per play session.
///   Device build with the real SDK define -> real adapter (phase 2).
///   Editor / Development build             -> fakes (dev overlay; no real money, ever).
///   Release build without the SDK          -> Unavailable* (never grants anything).
/// </summary>
public static class MonetizationServices
{
    private static IAdService ads;
    private static IStoreService store;

    public static IAdService Ads => ads ??= CreateAds();
    public static IStoreService Store => store ??= CreateStore();

    /// <summary>True when purchases go to the fake test store (shown as a footnote in the Store).</summary>
    public static bool IsTestStore
    {
        get
        {
#if UNITY_PURCHASING && !UNITY_EDITOR
            return false;
#elif UNITY_EDITOR || DEVELOPMENT_BUILD
            return true;
#else
            return false;
#endif
        }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() { ads = null; store = null; }

    private static IAdService CreateAds()
    {
#if LEVELPLAY && !UNITY_EDITOR
        return new LevelPlayAdService();
#elif UNITY_EDITOR || DEVELOPMENT_BUILD
        return new FakeAdService();
#else
        return new UnavailableAdService();
#endif
    }

    private static IStoreService CreateStore()
    {
#if UNITY_PURCHASING && !UNITY_EDITOR
        return new UnityIapStoreService();
#elif UNITY_EDITOR || DEVELOPMENT_BUILD
        return new FakeStoreService();
#else
        return new UnavailableStoreService();
#endif
    }
}
```

```csharp
// Assets/Scripts/Monetization/Services/UnavailableServices.cs
using System;

/// <summary>Release builds without an ad SDK: never ready, never rewards.</summary>
public sealed class UnavailableAdService : IAdService
{
    public bool IsReady => false;
    public void ShowRewarded(Action onRewarded, Action<AdFailReason> onFailed) => onFailed?.Invoke(AdFailReason.NotReady);
}

/// <summary>Release builds without a store SDK: every purchase is Unavailable; nothing is ever granted.</summary>
public sealed class UnavailableStoreService : IStoreService
{
    public bool IsInitialized => false;
    public event Action<PurchaseResult> OnPendingPurchase { add { } remove { } }
    public void Initialize(Action<bool> onDone) => onDone?.Invoke(false);
    public string GetPriceString(string productId) => null;
    public void Purchase(string productId, Action<PurchaseResult> onResult) =>
        onResult?.Invoke(new PurchaseResult { productId = productId, outcome = PurchaseOutcome.Unavailable });
    public void ConfirmDelivered(string transactionId) { }
    public void Restore(Action<bool> onDone) => onDone?.Invoke(false);
    public bool? IsOwned(string productId) => null;
}
```

```csharp
// Assets/Scripts/Monetization/Services/LevelPlayAdService.cs
#if LEVELPLAY
using System;

/// <summary>
/// PHASE 2 STUB, compiled only when the LEVELPLAY define is set (after installing the Ads Mediation
/// package). Implement with the unity:levelplay-unity-integration skill:
///   - initialize LevelPlay with the app key, then load a rewarded ad unit;
///   - IsReady = the rewarded unit's loaded state;
///   - ShowRewarded: call onRewarded from the "ad rewarded" callback (NOT on close),
///     onFailed(Closed) when it closes without a reward, onFailed(NotReady) when nothing is loaded;
///   - reload the next ad after each show.
/// Until then it behaves like UnavailableAdService.
/// </summary>
public sealed class LevelPlayAdService : IAdService
{
    public bool IsReady => false;
    public void ShowRewarded(Action onRewarded, Action<AdFailReason> onFailed) => onFailed?.Invoke(AdFailReason.NotReady);
}
#endif
```

```csharp
// Assets/Scripts/Monetization/Services/UnityIapStoreService.cs
#if UNITY_PURCHASING
using System;

/// <summary>
/// PHASE 2 STUB, compiled only when UNITY_PURCHASING is defined (Unity IAP installed). Implement with
/// the unity:implement-in-app-purchases skill:
///   - Initialize: register every StoreCatalogSO product (Consumable / NonConsumable) and init IAP;
///   - GetPriceString: the product's localized price string;
///   - Purchase/ProcessPurchase: validate the receipt locally (CrossPlatformValidator), raise the result
///     (or OnPendingPurchase for re-sent purchases) and leave it PENDING; StoreManager grants + saves,
///     then calls ConfirmDelivered(transactionId) -> ConfirmPendingPurchase;
///   - Restore: Google restores automatically on init; iOS needs RestoreTransactions;
///   - IsOwned: hasReceipt for NonConsumables, null before init.
/// Until then it behaves like UnavailableStoreService.
/// </summary>
public sealed class UnityIapStoreService : IStoreService
{
    public bool IsInitialized => false;
    public event Action<PurchaseResult> OnPendingPurchase { add { } remove { } }
    public void Initialize(Action<bool> onDone) => onDone?.Invoke(false);
    public string GetPriceString(string productId) => null;
    public void Purchase(string productId, Action<PurchaseResult> onResult) =>
        onResult?.Invoke(new PurchaseResult { productId = productId, outcome = PurchaseOutcome.Unavailable });
    public void ConfirmDelivered(string transactionId) { }
    public void Restore(Action<bool> onDone) => onDone?.Invoke(false);
    public bool? IsOwned(string productId) => null;
}
#endif
```

- [ ] **Step 3: Write the fakes and the dev overlay**

```csharp
// Assets/Scripts/Monetization/Services/FakeAdService.cs
#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;

/// <summary>Editor/Dev stand-in for rewarded ads: a 3-second "Test Ad" overlay with Close early.</summary>
public sealed class FakeAdService : IAdService
{
    /// <summary>Settings > Dev toggle: pretend the ad network has nothing to show.</summary>
    public static bool SimulateNoFill;

    public bool IsReady => !SimulateNoFill;

    public void ShowRewarded(Action onRewarded, Action<AdFailReason> onFailed)
    {
        if (SimulateNoFill) { onFailed?.Invoke(AdFailReason.NotReady); return; }
        MonetizationDevOverlay.ShowAd(completed =>
        {
            if (completed) onRewarded?.Invoke();
            else onFailed?.Invoke(AdFailReason.Closed);
        });
    }
}
#endif
```

```csharp
// Assets/Scripts/Monetization/Services/FakeStoreService.cs
#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Editor/Dev stand-in store: a "[Test Store]" confirm dialog (Confirm / Fail / Cancel).
/// Owned permanent unlocks live in PlayerPrefs as the fake "store account", so Restore and the
/// launch-time ownership check behave like a real store after the save is wiped.</summary>
public sealed class FakeStoreService : IStoreService
{
    private const string OwnedKey = "fake_store_owned";

    public bool IsInitialized { get; private set; }
    public event Action<PurchaseResult> OnPendingPurchase;

    public void Initialize(Action<bool> onDone) { IsInitialized = true; onDone?.Invoke(true); }

    public string GetPriceString(string productId) => null; // Store shows the catalog fallback

    public void Purchase(string productId, Action<PurchaseResult> onResult)
    {
        StoreProductDef def = StoreCatalogSO.Instance.Get(productId);
        string title = def != null ? def.displayName : productId;
        string price = def != null ? def.fallbackPrice : "";
        MonetizationDevOverlay.ShowPurchase(title, price, outcome =>
        {
            var result = new PurchaseResult { productId = productId, outcome = outcome };
            if (outcome == PurchaseOutcome.Success)
            {
                result.transactionId = "fake-" + Guid.NewGuid().ToString("N");
                if (def != null && def.kind == ProductKind.NonConsumable) AddOwned(productId);
            }
            onResult?.Invoke(result);
        });
    }

    public void ConfirmDelivered(string transactionId) { }

    public void Restore(Action<bool> onDone)
    {
        foreach (string id in Owned())
            OnPendingPurchase?.Invoke(new PurchaseResult { productId = id, transactionId = "fake-restore-" + id, outcome = PurchaseOutcome.Success });
        onDone?.Invoke(true);
    }

    public bool? IsOwned(string productId) => Owned().Contains(productId);

    /// <summary>Settings > Dev: forget every fake purchase (with StoreManager.DevResetPass).</summary>
    public static void DevClearAccount() { PlayerPrefs.DeleteKey(OwnedKey); PlayerPrefs.Save(); }

    private static HashSet<string> Owned()
    {
        string raw = PlayerPrefs.GetString(OwnedKey, "");
        return new HashSet<string>(raw.Split(new[] { '|' }, StringSplitOptions.RemoveEmptyEntries));
    }

    private static void AddOwned(string id)
    {
        HashSet<string> owned = Owned();
        owned.Add(id);
        PlayerPrefs.SetString(OwnedKey, string.Join("|", owned));
        PlayerPrefs.Save();
    }
}
#endif
```

```csharp
// Assets/Scripts/Monetization/Services/MonetizationDevOverlay.cs
#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>The fake ad + fake purchase screens (dev only). Self-creating; sort 1900 sits above popups
/// and below toasts (2000).</summary>
public sealed class MonetizationDevOverlay : MonoBehaviour
{
    private const int SortOrder = 1900;
    private const float AdSeconds = 3f;

    private static MonetizationDevOverlay instance;
    private PanelSettings runtimeSettings;
    private VisualElement root, layer;

    private static MonetizationDevOverlay Ensure()
    {
        if (instance != null) return instance;
        instance = new GameObject("MonetizationDevOverlay").AddComponent<MonetizationDevOverlay>();
        return instance;
    }

    private void OnDestroy()
    {
        if (instance == this) instance = null;
        if (runtimeSettings != null) Destroy(runtimeSettings);
    }

    private bool EnsureRoot()
    {
        if (root != null) return true;
        root = MonetizationUI.CreateRoot(this, SortOrder, "MonetizationDevOverlay", out runtimeSettings);
        return root != null;
    }

    /// <summary>3-second fake ad. done(true) when it runs out, done(false) on Close early.</summary>
    public static void ShowAd(Action<bool> done)
    {
        MonetizationDevOverlay o = Ensure();
        if (!o.EnsureRoot()) { done?.Invoke(true); return; } // no UI available: behave like a finished ad
        o.Open();

        var panel = o.Panel();
        panel.Add(MonetizationUI.Text("Test Ad", 44, MonetizationUI.Cream, bold: true));
        Label count = MonetizationUI.Text(AdSeconds.ToString("0"), 120, MonetizationUI.Gold, bold: true);
        panel.Add(count);

        bool finished = false;
        IVisualElementScheduledItem tick = null;
        float start = Time.unscaledTime;
        void Finish(bool completed)
        {
            if (finished) return;
            finished = true;
            tick?.Pause();
            o.CloseLayer();
            done?.Invoke(completed);
        }
        Button close = MonetizationUI.BrownButton("Close early", () => Finish(false), 28);
        close.style.marginTop = 30;
        panel.Add(close);

        tick = count.schedule.Execute(() =>
        {
            float left = AdSeconds - (Time.unscaledTime - start);
            count.text = Mathf.CeilToInt(Mathf.Max(0f, left)).ToString();
            if (left <= 0f) Finish(true);
        }).Every(100);
    }

    /// <summary>"[Test Store] Buy X for $Y?" with Confirm / Fail / Cancel.</summary>
    public static void ShowPurchase(string title, string price, Action<PurchaseOutcome> done)
    {
        MonetizationDevOverlay o = Ensure();
        if (!o.EnsureRoot()) { done?.Invoke(PurchaseOutcome.Unavailable); return; }
        o.Open();

        var panel = o.Panel();
        panel.Add(MonetizationUI.Text("[Test Store]", 30, MonetizationUI.Gold, bold: true));
        Label question = MonetizationUI.Text($"Buy {title} for {price}?", 36, MonetizationUI.Cream);
        question.style.unityTextAlign = TextAnchor.MiddleCenter;
        question.style.marginTop = 12; question.style.marginBottom = 26;
        panel.Add(question);

        bool answered = false;
        void Answer(PurchaseOutcome outcome)
        {
            if (answered) return;
            answered = true;
            o.CloseLayer();
            done?.Invoke(outcome);
        }
        var row = new VisualElement();
        row.style.flexDirection = FlexDirection.Row;
        row.style.justifyContent = Justify.Center;
        Button confirm = MonetizationUI.BrownButton("Confirm", () => Answer(PurchaseOutcome.Success), 28);
        Button fail = MonetizationUI.BrownButton("Fail", () => Answer(PurchaseOutcome.Failed), 28);
        Button cancel = MonetizationUI.BrownButton("Cancel", () => Answer(PurchaseOutcome.Cancelled), 28);
        fail.style.marginLeft = 14; cancel.style.marginLeft = 14;
        row.Add(confirm); row.Add(fail); row.Add(cancel);
        panel.Add(row);
    }

    private void Open()
    {
        CloseLayer();
        layer = new VisualElement();
        MonetizationUI.Fill(layer);
        layer.style.backgroundColor = new Color(0f, 0f, 0f, 0.92f);
        layer.style.alignItems = Align.Center;
        layer.style.justifyContent = Justify.Center;
        root.Add(layer);
        root.pickingMode = PickingMode.Position;
    }

    private VisualElement Panel()
    {
        var panel = new VisualElement();
        panel.style.alignItems = Align.Center;
        panel.style.paddingLeft = 40; panel.style.paddingRight = 40;
        panel.style.paddingTop = 36; panel.style.paddingBottom = 36;
        panel.style.backgroundColor = MonetizationUI.WalnutDark;
        MonetizationUI.Radius(panel, 18);
        panel.style.maxWidth = 900;
        layer.Add(panel);
        return panel;
    }

    private void CloseLayer()
    {
        if (layer != null) { layer.RemoveFromHierarchy(); layer = null; }
        if (root != null) root.pickingMode = PickingMode.Ignore;
    }
}
#endif
```

- [ ] **Step 4: Compile**

Run **Compile**. Expected: no errors. The `#if LEVELPLAY` and `#if UNITY_PURCHASING` files compile to nothing.

- [ ] **Step 5: Checkpoint.** Run **EditMode tests**: green. Don't commit.

---

### Task 5: ChestRevealUITK (the 3-tap chest)

**Files:**
- Create: `Assets/Scripts/Monetization/UI/RaysElement.cs`
- Create: `Assets/Scripts/Monetization/UI/ChestRevealUITK.cs`
- Modify: `Assets/Scripts/UI/TopBarUITK.cs` (add `TryGetRowCenterNormalized`)

**Interfaces:**
- Consumes: `MonetizationUI`, `MonetizationArt` (Tasks 3-4); `TopBarUITK.Instance` (existing).
- Produces:
  - `enum RewardCurrency { Coins, Gems }`
  - `struct RewardLine { RewardCurrency currency; int amount; RewardLine(RewardCurrency, int) }`
  - `class ChestRevealRequest { List<RewardLine> lines; string banner; Action onOpened; Action onClosed; }`
  - `ChestRevealUITK`:
    - `static void Show(ChestRevealRequest)`
    - `static bool IsShowing`
    - `static event Action<string> OnFeedback` with `"tap"`, `"burst"`, `"collect"`. These are SFX/haptics hooks.
  - `TopBarUITK.TryGetRowCenterNormalized(string rowName, out Vector2 normalized) : bool`, where `rowName` is `"row-gems"` or `"row-coins"`. Normalized = 0..1 from the top-left.

- [ ] **Step 1: Add the top-bar target helper**

Add to `Assets/Scripts/UI/TopBarUITK.cs` inside the class (after `Cache()`):

```csharp
    /// <summary>Centre of a currency row ("row-gems", "row-coins") as 0..1 of the screen from the
    /// top-left, so another panel can fly a reward into it whatever its own scale.</summary>
    public bool TryGetRowCenterNormalized(string rowName, out Vector2 normalized)
    {
        normalized = default;
        if (root == null) return false;
        VisualElement row = root.Q<VisualElement>(rowName);
        Rect panel = root.worldBound;
        if (row == null || panel.width <= 0f || panel.height <= 0f) return false;
        Vector2 c = row.worldBound.center;
        normalized = new Vector2((c.x - panel.x) / panel.width, (c.y - panel.y) / panel.height);
        return true;
    }
```

- [ ] **Step 2: Write the rays element**

```csharp
// Assets/Scripts/Monetization/UI/RaysElement.cs
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>Warm light rays drawn as wedges with Painter2D. Rotate the element via style.rotate.</summary>
public sealed class RaysElement : VisualElement
{
    public int rayCount = 12;
    public Color color = new Color(1f, 0.86f, 0.45f, 0.35f);

    public RaysElement()
    {
        pickingMode = PickingMode.Ignore;
        generateVisualContent += Draw;
    }

    private void Draw(MeshGenerationContext ctx)
    {
        Rect r = contentRect;
        if (r.width <= 0f) return;
        Vector2 c = r.center;
        float radius = Mathf.Min(r.width, r.height) * 0.5f;
        float half = Mathf.PI / rayCount * 0.45f;
        Painter2D p = ctx.painter2D;
        p.fillColor = color;
        for (int i = 0; i < rayCount; i++)
        {
            float a = i * Mathf.PI * 2f / rayCount;
            p.BeginPath();
            p.MoveTo(c);
            p.LineTo(c + new Vector2(Mathf.Cos(a - half), Mathf.Sin(a - half)) * radius);
            p.LineTo(c + new Vector2(Mathf.Cos(a + half), Mathf.Sin(a + half)) * radius);
            p.ClosePath();
            p.Fill();
        }
    }
}
```

- [ ] **Step 3: Write ChestRevealUITK**

```csharp
// Assets/Scripts/Monetization/UI/ChestRevealUITK.cs
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

public enum RewardCurrency { Coins, Gems }

public struct RewardLine
{
    public RewardCurrency currency;
    public int amount;
    public RewardLine(RewardCurrency currency, int amount) { this.currency = currency; this.amount = amount; }
}

/// <summary>One chest to open: the rewards to show, an optional banner, a grant hook that fires once
/// when the chest bursts open (or early if the app is backgrounded first), and a close hook.</summary>
public sealed class ChestRevealRequest
{
    public List<RewardLine> lines = new List<RewardLine>();
    public string banner;
    public Action onOpened;
    public Action onClosed;
}

/// <summary>
/// The Free Gift / purchase chest (spec §5). Closed chest drops in. Taps 1-2 shake it, tap 3 bursts it
/// open: sprite swaps, rays spin, icons spray, BIG numbers count up. The bottom button opens it in one
/// press ("Claim") and closes it in the next ("Collect"), so mashing is 2 presses (~1s).
/// Requests queue; each onOpened fires exactly once. Self-creating, sort 1400.
/// </summary>
public sealed class ChestRevealUITK : MonoBehaviour
{
    private const int SortOrder = 1400;
    private const float ChestSize = 256f;     // the 32px sprite at 8x
    private const float CollectGuard = 0.25f; // ignore the same mash that opened it

    public static event Action<string> OnFeedback;

    private static ChestRevealUITK instance;
    public static bool IsShowing => instance != null && instance.current != null;

    private readonly Queue<ChestRevealRequest> queue = new Queue<ChestRevealRequest>();
    private ChestRevealRequest current;
    private bool opened, closing, grantFired;
    private int taps;
    private float openedAt;

    private PanelSettings runtimeSettings;
    private VisualElement root, layer, stage, chest, glow, linesColumn;
    private RaysElement rays;
    private Label banner, hint;
    private Button actionButton;
    private IVisualElementScheduledItem spin;

    public static void Show(ChestRevealRequest request)
    {
        if (request == null) return;
        if (instance == null) instance = new GameObject("ChestRevealUITK").AddComponent<ChestRevealUITK>();
        instance.queue.Enqueue(request);
        if (instance.current == null) instance.Next();
    }

    private void OnDestroy()
    {
        FireGrant(); // never lose a granted-at-open reward to a scene unload
        if (instance == this) instance = null;
        if (runtimeSettings != null) Destroy(runtimeSettings);
    }

    // Backgrounding with an unopened chest: grant now so a killed app can't eat the reward.
    private void OnApplicationPause(bool paused) { if (paused) FireGrant(); }
    private void OnApplicationQuit() => FireGrant();

    private void FireGrant()
    {
        if (current == null || grantFired) return;
        grantFired = true;
        try { current.onOpened?.Invoke(); }
        catch (Exception e) { Debug.LogException(e); }
    }

    private bool EnsureBuilt()
    {
        if (root != null) return true;
        root = MonetizationUI.CreateRoot(this, SortOrder, "ChestReveal", out runtimeSettings);
        if (root == null) return false;

        layer = new VisualElement();
        MonetizationUI.Fill(layer);
        layer.style.backgroundColor = new Color(0f, 0f, 0f, 0.78f);
        layer.style.alignItems = Align.Center;
        layer.style.justifyContent = Justify.Center;
        layer.style.display = DisplayStyle.None;
        root.Add(layer);

        banner = MonetizationUI.Text("", 44, MonetizationUI.Gold, bold: true);
        MonetizationUI.Font(banner, MonetizationUI.TitleFont);
        banner.style.unityTextAlign = TextAnchor.MiddleCenter;
        banner.style.marginBottom = 20;
        layer.Add(banner);

        stage = new VisualElement();
        stage.style.width = 640; stage.style.height = 520;
        stage.style.alignItems = Align.Center;
        stage.style.justifyContent = Justify.Center;
        layer.Add(stage);

        rays = new RaysElement();
        rays.style.position = Position.Absolute;
        rays.style.width = 640; rays.style.height = 640;
        rays.style.left = 0; rays.style.top = -60;
        stage.Add(rays);

        glow = new VisualElement { pickingMode = PickingMode.Ignore };
        glow.style.position = Position.Absolute;
        glow.style.width = 360; glow.style.height = 360;
        glow.style.left = 140; glow.style.top = 80;
        glow.style.backgroundColor = new Color(1f, 0.85f, 0.4f, 0.35f);
        MonetizationUI.Radius(glow, 180);
        stage.Add(glow);

        chest = new VisualElement();
        chest.style.width = ChestSize; chest.style.height = ChestSize;
        chest.style.backgroundSize = new BackgroundSize(BackgroundSizeType.Contain);
        chest.RegisterCallback<ClickEvent>(_ => OnChestTapped());
        stage.Add(chest);

        hint = MonetizationUI.Text("Tap to open!", 34, MonetizationUI.Cream, bold: true);
        hint.style.marginTop = 6;
        layer.Add(hint);

        linesColumn = new VisualElement();
        linesColumn.style.alignItems = Align.Center;
        linesColumn.style.minHeight = 240;
        layer.Add(linesColumn);

        actionButton = MonetizationUI.BrownButton("Claim", OnButton, 36);
        actionButton.style.minWidth = 360;
        actionButton.style.marginTop = 24;
        layer.Add(actionButton);

        // Tapping the dim backdrop behaves like the chest (opens, then collects).
        layer.RegisterCallback<ClickEvent>(e => { if (e.target == layer) OnChestTapped(); });
        return true;
    }

    private void Next()
    {
        current = queue.Count > 0 ? queue.Dequeue() : null;
        if (current == null) { Hide(); return; }
        if (!EnsureBuilt()) { FireGrant(); current.onClosed?.Invoke(); current = null; Next(); return; }

        opened = false; closing = false; grantFired = false; taps = 0;
        MonetizationArt art = MonetizationArt.Instance;
        chest.style.backgroundImage = art != null && art.chestClosed != null
            ? new StyleBackground(art.chestClosed) : new StyleBackground(StyleKeyword.None);
        chest.style.rotate = new Rotate(new Angle(0));
        chest.style.scale = new Scale(Vector2.one);
        rays.style.opacity = 0; glow.style.opacity = 0;
        banner.text = current.banner ?? "";
        banner.style.display = string.IsNullOrEmpty(current.banner) ? DisplayStyle.None : DisplayStyle.Flex;
        hint.style.display = DisplayStyle.Flex;
        linesColumn.Clear();
        actionButton.text = "Claim";
        layer.style.opacity = 1;
        layer.style.display = DisplayStyle.Flex;
        root.pickingMode = PickingMode.Position;

        // Drop in from above with a bounce.
        LeanTween.cancel(gameObject);
        LeanTween.value(gameObject, -420f, 0f, 0.45f).setEaseOutBounce().setIgnoreTimeScale(true)
            .setOnUpdate((float y) => chest.style.translate = new Translate(0, y));
    }

    private void OnChestTapped()
    {
        if (current == null || closing) return;
        if (opened) { if (Time.unscaledTime - openedAt > CollectGuard) Collect(); return; }
        taps++;
        OnFeedback?.Invoke("tap");
        if (taps >= 3) Open(); else Shake(taps);
    }

    private void OnButton()
    {
        if (current == null || closing) return;
        if (!opened) Open();
        else if (Time.unscaledTime - openedAt > CollectGuard) Collect();
    }

    private void Shake(int strength)
    {
        float amp = strength == 1 ? 7f : 14f;
        LeanTween.value(gameObject, 0f, 1f, 0.3f).setIgnoreTimeScale(true).setOnUpdate((float t) =>
        {
            chest.style.rotate = new Rotate(new Angle(Mathf.Sin(t * Mathf.PI * 6f) * amp * (1f - t)));
            float squash = 1f + 0.08f * Mathf.Sin(t * Mathf.PI) * strength;
            chest.style.scale = new Scale(new Vector2(squash, 2f - squash));
        });
        if (strength == 2) LeanTween.value(gameObject, 0f, 0.6f, 0.3f).setIgnoreTimeScale(true).setOnUpdate((float a) => glow.style.opacity = a);
    }

    private void Open()
    {
        opened = true;
        openedAt = Time.unscaledTime;
        OnFeedback?.Invoke("burst");
        FireGrant();

        MonetizationArt art = MonetizationArt.Instance;
        if (art != null && art.chestOpen != null) chest.style.backgroundImage = new StyleBackground(art.chestOpen);
        chest.style.rotate = new Rotate(new Angle(0));
        hint.style.display = DisplayStyle.None;
        actionButton.text = "Collect";

        LeanTween.value(gameObject, 1.35f, 1f, 0.35f).setEaseOutBack().setIgnoreTimeScale(true)
            .setOnUpdate((float s) => chest.style.scale = new Scale(new Vector2(s, s)));
        rays.style.opacity = 1; glow.style.opacity = 0.8f;
        float angle = 0f;
        spin?.Pause();
        spin = rays.schedule.Execute(() => { angle += 1.2f; rays.style.rotate = new Rotate(new Angle(angle)); }).Every(16);

        Spray(art);
        for (int i = 0; i < current.lines.Count; i++) AddLine(current.lines[i], i * 0.12f, art);
    }

    private void Spray(MonetizationArt art)
    {
        for (int i = 0; i < 10; i++)
        {
            Sprite s = art == null ? null : (i % 2 == 0 ? art.coinIcon : art.gemIcon);
            VisualElement p = MonetizationUI.Icon(s, 54);
            p.style.position = Position.Absolute;
            p.style.left = 293; p.style.top = 233;
            stage.Add(p);
            float a = UnityEngine.Random.Range(-160f, -20f) * Mathf.Deg2Rad;
            Vector2 to = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * UnityEngine.Random.Range(180f, 290f);
            LeanTween.value(gameObject, 0f, 1f, 0.7f).setEaseOutQuad().setIgnoreTimeScale(true)
                .setOnUpdate((float t) =>
                {
                    p.style.translate = new Translate(to.x * t, to.y * t + 260f * t * t);
                    p.style.opacity = 1f - t;
                })
                .setOnComplete(() => p.RemoveFromHierarchy());
        }
    }

    private void AddLine(RewardLine line, float delay, MonetizationArt art)
    {
        var row = new VisualElement();
        row.style.flexDirection = FlexDirection.Row;
        row.style.alignItems = Align.Center;
        row.style.marginTop = 8;
        row.style.scale = new Scale(Vector2.zero);
        Sprite icon = art == null ? null : (line.currency == RewardCurrency.Gems ? art.gemIcon : art.coinIcon);
        row.Add(MonetizationUI.Icon(icon, 84));
        Label amount = MonetizationUI.Text("+0", 96, MonetizationUI.Cream, bold: true);
        MonetizationUI.Font(amount, MonetizationUI.TitleFont);
        amount.style.marginLeft = 16;
        row.Add(amount);
        row.userData = line;
        linesColumn.Add(row);

        LeanTween.value(gameObject, 0f, 1f, 0.3f).setDelay(delay).setEaseOutBack().setIgnoreTimeScale(true)
            .setOnUpdate((float s) => row.style.scale = new Scale(new Vector2(s, s)));
        LeanTween.value(gameObject, 0f, line.amount, 0.6f).setDelay(delay).setEaseOutCubic().setIgnoreTimeScale(true)
            .setOnUpdate((float v) => amount.text = "+" + Mathf.RoundToInt(v).ToString("N0"))
            .setOnComplete(() => amount.text = "+" + line.amount.ToString("N0"));
    }

    private void Collect()
    {
        closing = true;
        OnFeedback?.Invoke("collect");
        Rect panel = root.worldBound;
        foreach (VisualElement row in linesColumn.Children())
        {
            if (!(row.userData is RewardLine line)) continue;
            string rowName = line.currency == RewardCurrency.Gems ? "row-gems" : "row-coins";
            Vector2 delta = new Vector2(0, -600);
            if (TopBarUITK.Instance != null && TopBarUITK.Instance.TryGetRowCenterNormalized(rowName, out Vector2 n))
            {
                Vector2 target = new Vector2(panel.x + n.x * panel.width, panel.y + n.y * panel.height);
                delta = target - row.worldBound.center;
            }
            VisualElement r = row;
            LeanTween.value(gameObject, 0f, 1f, 0.35f).setEaseInCubic().setIgnoreTimeScale(true).setOnUpdate((float t) =>
            {
                r.style.translate = new Translate(delta.x * t, delta.y * t);
                float s = 1f - 0.7f * t;
                r.style.scale = new Scale(new Vector2(s, s));
            });
        }
        LeanTween.value(gameObject, 1f, 0f, 0.15f).setDelay(0.3f).setIgnoreTimeScale(true)
            .setOnUpdate((float a) => layer.style.opacity = a)
            .setOnComplete(Finish);
    }

    private void Finish()
    {
        spin?.Pause();
        ChestRevealRequest done = current;
        current = null;
        layer.style.display = DisplayStyle.None;
        root.pickingMode = PickingMode.Ignore;
        try { done?.onClosed?.Invoke(); } catch (Exception e) { Debug.LogException(e); }
        Next();
    }

    private void Hide()
    {
        if (layer != null) layer.style.display = DisplayStyle.None;
        if (root != null) root.pickingMode = PickingMode.Ignore;
    }
}
```

- [ ] **Step 4: Compile**

Run **Compile**. Expected: no errors.

- [ ] **Step 5: Checkpoint.** Run **EditMode tests**: green. The chest is play-verified in Task 12. Don't commit.

---

### Task 6: FreeGiftManager, save fields, letter + tip copy, unlock hook

**Files:**
- Create: `Assets/Scripts/Monetization/FreeGiftManager.cs`
- Modify: `Assets/Scripts/GameData.cs` (gift fields)
- Modify: `Assets/Scripts/SaveManager.cs:164` (capture) and `:263` (load)
- Modify: `Assets/Scripts/Reputation/ReputationManager.cs:85` (raise `welcome_basket_done`)
- Modify: `Assets/Scripts/EconomyCore/NarrativeDefaults.cs` (letter `town_gift` + tip `tip_town_gift`)
- Modify: `Assets/Scripts/Tutorial/OnboardingTutorials.cs:85` (inbox-close hook)
- Modify: `Assets/Scripts/Narrative/NarrativeDirector.cs:9` (doc comment: add `welcome_basket_done`)
- Test: `Assets/Tests/EditMode/NarrativeDefaultsTests.cs` (add one test)

**Interfaces:**
- Consumes: `FreeGiftCore`, `GiftStatus`, `FreeGiftRules` (Task 1); `FreeGiftTuning` (Task 3); `MonetizationServices.Ads`, `AdFailReason` (Tasks 2, 4); `ChestRevealUITK`, `ChestRevealRequest`, `RewardLine`, `RewardCurrency` (Task 5); `StoreManager.Instance.HasFarmersPass` (Task 8). Until Task 8 lands, reference it through `FreeGiftManager.PassOwned`, which Task 8 wires. See Step 4.
- Produces:
  - `FreeGiftManager`:
    - `static Instance`
    - `event Action OnStateChanged`
    - State: `GiftStatus Status`, `bool IsUnlocked`, `double SecondsUntilReady`, `bool NextClaimNeedsAd`
    - Rewards: `int GemsPerClaim`, `int CoinReward`, `static int CoinsAtLevel(int level)`
    - Actions: `void RequestClaim()`, `bool TryShowIntro()`, `void NotifyChanged()`, `void MarkPitchShown()`
    - Save: `void CaptureTo(GameData)`, `void LoadFrom(GameData)`
    - Dev: `void DevResetGift()`, `void DevMakeReady()`
    - Pass hook: `static Func<bool> PassOwned`
  - Constants: `FreeGiftManager.IntroTipId = "tip_town_gift"`, `FreeGiftManager.LetterId = "town_gift"`, `FreeGiftManager.UnlockEvent = "welcome_basket_done"`
  - GameData: `giftLastClaimUtcTicks`, `giftClaimsTodayDate`, `giftClaimsToday`, `giftLifetimeAdClaims`, `passPitchShown`

- [ ] **Step 1: Write the failing copy test**

Add to `Assets/Tests/EditMode/NarrativeDefaultsTests.cs` (inside the class):

```csharp
    [Test]
    public void TownGift_LetterAndTip_Exist()
    {
        var letter = NarrativeDefaults.Letters.FirstOrDefault(l => l.id == "town_gift");
        Assert.IsNotNull(letter);
        Assert.AreEqual("welcome_basket_done", letter.triggerEvent);
        Assert.AreEqual("A little thank-you", letter.subject);
        Assert.IsFalse(letter.newPlayersOnly);
        var tip = NarrativeDefaults.Tips.FirstOrDefault(t => t.id == "tip_town_gift");
        Assert.IsNotNull(tip);
        Assert.AreEqual("A gift every 30 minutes. This one's on us!", tip.text);
    }
```

Run **EditMode tests**. Expected: FAIL, `TownGift_LetterAndTip_Exist` (letter is null).

- [ ] **Step 2: Add the copy**

In `Assets/Scripts/EconomyCore/NarrativeDefaults.cs`, add to the `Letters` array (after the `long_run_3h` entry):

```csharp
        new LetterDef { id = "town_gift", triggerEvent = "welcome_basket_done",
            senderName = Mayor, subject = "A little thank-you",
            body = "Dear {farmName},\n\nThe whole town loved your basket. We'll leave a little something at your farm now and then.\n\n- Mayor Bramble" },
```

and to the `Tips` array:

```csharp
        new TipDef { id = "tip_town_gift", when = "Free Gift chest first unlocks (after the Welcome Basket letter is read)",
            text = "A gift every 30 minutes. This one's on us!" },
```

Run **EditMode tests**. Expected: PASS, including the existing `NarrativeDefaultsTests` (cast, no surrogates, no " Gold").

- [ ] **Step 3: Add the save fields**

In `Assets/Scripts/GameData.cs`, after `public string lastCompostClaimTime;` add:

```csharp
    // Free Gift chest (monetization v1). Zero values = a fresh, ready gift on old saves.
    public long giftLastClaimUtcTicks;
    public string giftClaimsTodayDate;
    public int giftClaimsToday;
    public int giftLifetimeAdClaims;
    public bool passPitchShown;
```

- [ ] **Step 4: Write FreeGiftManager**

```csharp
// Assets/Scripts/Monetization/FreeGiftManager.cs
using System;
using UnityEngine;

/// <summary>
/// The town's Free Gift chest (spec §3). Unlocks when the Welcome Basket is delivered; then one chest
/// every 30 min, max 10 a day. The day's first chest is free, others need a rewarded ad unless the player
/// owns the Farmer's Pass. Rewards: 10 gems + Coins by Overall Farm Level. Grants at chest open (or on
/// backgrounding), then saves. Self-bootstrapping per scene, like the Almanac.
/// </summary>
public sealed class FreeGiftManager : MonoBehaviour
{
    public const string IntroTipId = "tip_town_gift";
    public const string LetterId = "town_gift";
    public const string UnlockEvent = "welcome_basket_done";
    private const string NoFillToast = "No gift right now. Try again soon.";

    public static FreeGiftManager Instance { get; private set; }

    /// <summary>Whether the Farmer's Pass is owned. StoreManager sets this (Task 8); false until then.</summary>
    public static Func<bool> PassOwned = () => false;

    public event Action OnStateChanged;

    private FreeGiftCore core;
    private bool busy;          // an ad or chest for this claim is in flight
    private bool pendingPitch;  // show the pass pitch when the chest closes
    private bool subscribed;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        UnityEngine.SceneManagement.SceneManager.sceneLoaded -= OnSceneLoaded;
        UnityEngine.SceneManagement.SceneManager.sceneLoaded += OnSceneLoaded;
        EnsureInstance();
    }

    private static void OnSceneLoaded(UnityEngine.SceneManagement.Scene s, UnityEngine.SceneManagement.LoadSceneMode m) => EnsureInstance();

    // Only scenes with a save (FarmMain): sceneLoaded runs after Awake, before Start, so this exists
    // before CurrencyManager.Start triggers SaveManager.LoadGame.
    private static void EnsureInstance()
    {
        if (Instance != null || SaveManager.Instance == null) return;
        new GameObject("FreeGiftManager").AddComponent<FreeGiftManager>();
    }

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        core = new FreeGiftCore(FreeGiftTuning.Instance.ToRules());
    }

    private void Start() => TrySubscribe();

    private void OnDestroy()
    {
        if (subscribed && ReputationManager.Instance != null) ReputationManager.Instance.OnRequestFulfilled -= NotifyChanged;
        if (Instance == this) Instance = null;
    }

    private void TrySubscribe()
    {
        if (subscribed || ReputationManager.Instance == null) return;
        ReputationManager.Instance.OnRequestFulfilled += NotifyChanged;
        subscribed = true;
    }

    /// <summary>Re-evaluate listeners (unlock, pass bought, load).</summary>
    public void NotifyChanged() => OnStateChanged?.Invoke();

    // ── State ────────────────────────────────────────────────────

    private static long NowUtc => DateTime.UtcNow.Ticks;
    private static string Today => FreeGiftCore.LocalDateKey(DateTime.Now);

    public bool IsUnlocked
    {
        get
        {
            NarrativeManager nm = NarrativeManager.Instance;
            ReputationManager rm = ReputationManager.Instance;
            return (nm != null && nm.HasFired(ReputationManager.WelcomeBasketFlag)) || (rm != null && rm.PointsEarned >= 1);
        }
    }

    public GiftStatus Status
    {
        get
        {
            long now = NowUtc;
            core.HealClock(now);
            return core.Status(IsUnlocked, now, Today);
        }
    }

    public double SecondsUntilReady => core.SecondsUntilReady(NowUtc);
    public bool NextClaimNeedsAd => !PassOwned() && !core.IsFreeChest(Today);
    public int GemsPerClaim => core.Rules.gemsPerClaim;
    public int CoinReward => CoinsAtLevel(ReputationManager.Instance != null ? ReputationManager.Instance.PointsEarned : 0);
    public static int CoinsAtLevel(int level) => FreeGiftCore.CoinsForLevel(FreeGiftTuning.Instance.coinAnchors, level);

    // ── Claim flow ───────────────────────────────────────────────

    /// <summary>HUD button / Store card. Free chest or pass: straight to the chest. Otherwise an ad first.</summary>
    public void RequestClaim()
    {
        TrySubscribe();
        if (busy || ChestRevealUITK.IsShowing || Status != GiftStatus.Ready) return;
        int gems = GemsPerClaim, coins = CoinReward;

        if (!NextClaimNeedsAd) { OpenChest(viaAd: false, gems, coins); return; }

        busy = true;
        MonetizationServices.Ads.ShowRewarded(
            onRewarded: () => OpenChest(viaAd: true, gems, coins),
            onFailed: reason =>
            {
                busy = false;
                if (reason == AdFailReason.NotReady || reason == AdFailReason.Error) ToastManager.Show(NoFillToast);
                OnStateChanged?.Invoke();
            });
    }

    private void OpenChest(bool viaAd, int gems, int coins)
    {
        busy = true;
        var request = new ChestRevealRequest();
        request.lines.Add(new RewardLine(RewardCurrency.Gems, gems));
        if (coins > 0) request.lines.Add(new RewardLine(RewardCurrency.Coins, coins));
        request.onOpened = () => Grant(viaAd, gems, coins);
        request.onClosed = () =>
        {
            busy = false;
            OnStateChanged?.Invoke();
            if (pendingPitch) { pendingPitch = false; PassPitchUITK.Show(); }
        };
        ChestRevealUITK.Show(request);
    }

    private void Grant(bool viaAd, int gems, int coins)
    {
        long now = NowUtc;
        core.HealClock(now);
        CurrencyManager cm = CurrencyManager.Instance;
        if (cm != null) { cm.AddGems(gems); if (coins > 0) cm.AddCoins(coins); }
        if (core.RecordClaim(now, Today, viaAd)) pendingPitch = true;
        Debug.Log($"[FreeGift] Claimed +{gems} gems, +{coins} coins ({(viaAd ? "ad" : "free")}); today {core.ClaimsToday(Today)}/{core.Rules.dailyCap}");
        SaveManager.Instance?.SaveGame();
        OnStateChanged?.Invoke();
    }

    // ── Intro (letter -> tooltip on the button) ─────────────────

    /// <summary>Spotlights the HUD Gift button once: after the Mayor's thank-you letter is read, or right
    /// away for saves that unlocked before this shipped (they never get the letter).</summary>
    /// <returns>True once the intro is done or just started (callers stop retrying).</returns>
    public bool TryShowIntro()
    {
        if (TutorialManager.IsCompleted(IntroTipId)) return true;
        if (!IsUnlocked || TutorialManager.IsActive || LetterUnread()) return false;
        if (InboxPopupUITK.Instance != null && InboxPopupUITK.Instance.IsOpen) return false; // wait for the inbox to close
        FreeGiftButton button = FreeGiftButton.Instance;
        if (button == null || !button.IsShown) return false;
        var seq = new TutorialSequence { id = IntroTipId };
        seq.steps.Add(new TutorialStep
        {
            text = OnboardingTutorials.Text(IntroTipId),
            advance = TutorialAdvance.TargetPressed,
            getTargetScreenRect = TutorialTargets.FromUGUI(button.Rect),
        });
        return TutorialManager.TryStart(seq);
    }

    private static bool LetterUnread()
    {
        InboxManager inbox = InboxManager.Instance;
        if (inbox == null) return false;
        foreach (InboxEntry e in inbox.Entries)
            if (e != null && e.letterId == LetterId && !e.read) return true;
        return false;
    }

    // ── Save ─────────────────────────────────────────────────────

    public void CaptureTo(GameData d)
    {
        d.giftLastClaimUtcTicks = core.LastClaimUtcTicks;
        d.giftClaimsTodayDate = core.ClaimsDate;
        d.giftClaimsToday = core.ClaimsOnSavedDate;
        d.giftLifetimeAdClaims = core.LifetimeAdClaims;
        d.passPitchShown = core.PitchShown;
    }

    public void LoadFrom(GameData d)
    {
        core.Import(d.giftLastClaimUtcTicks, d.giftClaimsTodayDate, d.giftClaimsToday, d.giftLifetimeAdClaims, d.passPitchShown);
        core.HealClock(NowUtc);
        OnStateChanged?.Invoke();
    }

    // ── Dev ──────────────────────────────────────────────────────

    public void DevResetGift()
    {
        core.Import(0, "", 0, 0, false);
        SaveManager.Instance?.SaveGame();
        OnStateChanged?.Invoke();
    }

    public void DevMakeReady()
    {
        core.Import(0, core.ClaimsDate, core.ClaimsOnSavedDate, core.LifetimeAdClaims, core.PitchShown);
        OnStateChanged?.Invoke();
    }

    /// <summary>The pitch was shown (or the pass bought): never pitch again.</summary>
    public void MarkPitchShown()
    {
        core.MarkPitchShown();
        pendingPitch = false;
    }
}
```

This references `FreeGiftButton` (Task 7) and `PassPitchUITK` (Task 10). To keep this task compiling on its own, create minimal placeholders now. Tasks 7 and 10 replace them with the full files.

```csharp
// Assets/Scripts/Monetization/UI/FreeGiftButton.cs  (temporary: Task 7 replaces this file)
using UnityEngine;
public class FreeGiftButton : MonoBehaviour
{
    public static FreeGiftButton Instance { get; private set; }
    public RectTransform Rect => (RectTransform)transform;
    public bool IsShown => false;
}
// (The InboxPopupUITK.IsOpen check in TryShowIntro uses the existing public InboxPopupUITK.Instance / IsOpen.)
```

```csharp
// Assets/Scripts/Monetization/UI/PassPitchUITK.cs  (temporary: Task 10 replaces this file)
using UnityEngine;
public sealed class PassPitchUITK : MonoBehaviour
{
    public static void Show() { FreeGiftManager.Instance?.MarkPitchShown(); }
}
```

- [ ] **Step 5: Wire save, unlock and inbox hooks**

`Assets/Scripts/SaveManager.cs`, after line 164 (`FarmSkillsManager ... CaptureTo(data);`):
```csharp
        if (FreeGiftManager.Instance != null) FreeGiftManager.Instance.CaptureTo(data);
```
After line 263 (`FarmSkillsManager.Instance.LoadFrom(data);`):
```csharp
                if (FreeGiftManager.Instance != null)
                    FreeGiftManager.Instance.LoadFrom(data);
```

`Assets/Scripts/Reputation/ReputationManager.cs:85`, change
```csharp
        if (request.isWelcomeBasket) NarrativeManager.Instance?.MarkFired(WelcomeBasketFlag);
```
to
```csharp
        if (request.isWelcomeBasket)
        {
            NarrativeManager.Instance?.MarkFired(WelcomeBasketFlag);
            NarrativeDirector.Raise(FreeGiftManager.UnlockEvent); // the town's thank-you letter: the Free Gift unlocks
        }
```

`Assets/Scripts/Tutorial/OnboardingTutorials.cs`, `OnInboxClosed()` becomes:
```csharp
    public static void OnInboxClosed()
    {
        FreeGiftManager.Instance?.TryShowIntro(); // all players, not just new ones
        if (IsNewPlayer && Done(MailboxIntroId)) TryFieldIntro();
    }
```

`Assets/Scripts/Narrative/NarrativeDirector.cs:9`, in the doc comment's event list, append ` · welcome_basket_done (ReputationManager)`.

- [ ] **Step 6: Compile, seed copy, test**

Run **Compile**, then:
```bash
cd "/c/Users/rjcla/IdleFarm - Silo"
printf '%s' "Farm Game/Narrative/Seed Missing Copy" > Temp/menu.request; sleep 6
grep -n "town_gift\|tip_town_gift" Assets/Resources/LetterCatalog.asset | head
```
Expected: both ids now exist in `LetterCatalog.asset`, and `docs/narrative/cast-and-copy.md` is regenerated. Then run **EditMode tests**: green.

- [ ] **Step 7: Checkpoint.** Don't commit.

---

### Task 7: FreeGiftButton (HUD) + bake menu

**Files:**
- Modify (replace placeholder): `Assets/Scripts/Monetization/UI/FreeGiftButton.cs`
- Modify: `Assets/Editor/MonetizationTools.cs` (add `Bake Gift Button`)
- Scene (via menu): `FreeGiftButton` under `Canvas` at anchored (70, 550), size 100×100. `LocationModeController.hideAtMarketByName += "FreeGiftButton"`.

**Interfaces:**
- Consumes: `FreeGiftManager.Instance.Status/SecondsUntilReady/NextClaimNeedsAd/RequestClaim/TryShowIntro/OnStateChanged` (Task 6); `MonetizationArt.chestClosed` (Task 3); `EggClaimButton` (existing scene object).
- Produces: `FreeGiftButton { static Instance; RectTransform Rect; bool IsShown; }`. Serialized fields: `button`, `icon`, `notificationDot`, `timerLabel`, `tagBadge`, `tagLabel`.

- [ ] **Step 1: Write the button**

```csharp
// Assets/Scripts/Monetization/UI/FreeGiftButton.cs
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// HUD Free Gift chest, in a fixed slot above the egg/gem claim button (baked by Farm Game >
/// Monetization > Bake Gift Button). Ready: pulse + dot, corner tag FREE / AD (none with the pass).
/// Cooldown: dim + mm:ss. Capped: dim + "Tomorrow". Locked: invisible (CanvasGroup, never deactivated,
/// because LocationModeController toggles this GameObject at the Market).
/// </summary>
[RequireComponent(typeof(CanvasGroup))]
public class FreeGiftButton : MonoBehaviour
{
    public static FreeGiftButton Instance { get; private set; }

    [SerializeField] private Button button;
    [SerializeField] private Image icon;
    [SerializeField] private GameObject notificationDot;
    [SerializeField] private TextMeshProUGUI timerLabel;
    [SerializeField] private GameObject tagBadge;
    [SerializeField] private TextMeshProUGUI tagLabel;

    private static readonly Color DimColor = new Color(0.6f, 0.6f, 0.6f, 0.85f);

    private CanvasGroup group;
    private float nextRefresh;
    private bool pulsing, introDone;

    public RectTransform Rect => (RectTransform)transform;
    public bool IsShown => group != null && group.alpha > 0.5f && gameObject.activeInHierarchy;

    private void Awake()
    {
        Instance = this;
        group = GetComponent<CanvasGroup>();
    }

    private void OnDestroy() { if (Instance == this) Instance = null; }

    private void Start()
    {
        if (button != null) button.onClick.AddListener(() => FreeGiftManager.Instance?.RequestClaim());
        if (FreeGiftManager.Instance != null) FreeGiftManager.Instance.OnStateChanged += Refresh;
        Refresh();
    }

    private void OnEnable() { nextRefresh = 0f; pulsing = false; }

    private void Update()
    {
        if (Time.unscaledTime < nextRefresh) return;
        nextRefresh = Time.unscaledTime + 0.5f;
        Refresh();
    }

    private void Refresh()
    {
        FreeGiftManager m = FreeGiftManager.Instance;
        GiftStatus status = m != null ? m.Status : GiftStatus.Locked;
        bool shown = status != GiftStatus.Locked;
        group.alpha = shown ? 1f : 0f;
        group.interactable = shown;
        group.blocksRaycasts = shown;
        if (!shown) { SetPulse(false); return; }

        bool ready = status == GiftStatus.Ready;
        if (icon != null) icon.color = ready ? Color.white : DimColor;
        if (notificationDot != null) notificationDot.SetActive(ready);
        if (timerLabel != null)
        {
            timerLabel.gameObject.SetActive(!ready);
            timerLabel.text = status == GiftStatus.Capped ? "Tomorrow" : MonetizationUI.Mmss(m.SecondsUntilReady);
        }
        bool pass = FreeGiftManager.PassOwned();
        bool showTag = ready && !pass;
        if (tagBadge != null) tagBadge.SetActive(showTag);
        if (tagLabel != null && showTag) tagLabel.text = m.NextClaimNeedsAd ? "AD" : "FREE";
        SetPulse(ready);

        // Retried every refresh (0.5s) until shown or completed: an onboarding step or the open
        // mailbox can block it, and the intro must not be lost.
        if (!introDone) introDone = m.TryShowIntro();
    }

    private void SetPulse(bool on)
    {
        if (on == pulsing) return;
        pulsing = on;
        LeanTween.cancel(gameObject);
        transform.localScale = Vector3.one;
        if (on) LeanTween.scale(gameObject, Vector3.one * 1.08f, 0.6f).setEaseInOutSine().setLoopPingPong();
    }
}
```

- [ ] **Step 2: Add the bake menu**

Add to `MonetizationTools` (also add `using TMPro;` and `using UnityEngine.UI;` at the top):

```csharp
    private const string FontPath = "Assets/Fonts/NotoSans-Regular SDF.asset";

    /// <summary>Clones EggClaimButton into a FreeGiftButton directly above it (70,550), swaps in the chest,
    /// adds timer + FREE/AD tag labels, and registers it with LocationModeController's Market hide list.</summary>
    [MenuItem("Farm Game/Monetization/Bake Gift Button")]
    public static void BakeGiftButton()
    {
        var egg = Object.FindFirstObjectByType<EggClaimButton>(FindObjectsInactive.Include);
        if (egg == null) { Debug.LogError("[Monetization] No EggClaimButton in the open scene to copy."); return; }
        Transform parent = egg.transform.parent;

        Transform existing = parent.Find("FreeGiftButton");
        if (existing != null) Object.DestroyImmediate(existing.gameObject); // rebake from scratch
        GameObject go = Object.Instantiate(egg.gameObject, parent);
        go.name = "FreeGiftButton";
        go.SetActive(true);
        Undo.RegisterCreatedObjectUndo(go, "Bake Gift Button");

        // Read the clone's EggClaimButton references (they point at the clone's own children), then drop it.
        var eggSo = new SerializedObject(go.GetComponent<EggClaimButton>());
        var btn = eggSo.FindProperty("button").objectReferenceValue as Button;
        var img = eggSo.FindProperty("buttonImage").objectReferenceValue as Image;
        var dot = eggSo.FindProperty("notificationDot").objectReferenceValue as GameObject;
        var emoji = eggSo.FindProperty("emojiText").objectReferenceValue as TextMeshProUGUI;
        Object.DestroyImmediate(go.GetComponent<EggClaimButton>());
        if (emoji != null) emoji.gameObject.SetActive(false);

        var rt = (RectTransform)go.transform;
        rt.anchoredPosition = new Vector2(70, 550);
        rt.sizeDelta = new Vector2(100, 100);
        rt.localScale = Vector3.one;

        MonetizationArt art = AssetDatabase.LoadAssetAtPath<MonetizationArt>(ArtPath);
        if (img != null)
        {
            if (art != null && art.chestClosed != null) img.sprite = art.chestClosed;
            img.preserveAspect = true;
            img.color = Color.white;
        }

        var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
        TextMeshProUGUI timer = NewLabel(go.transform, "TimerLabel", font, 24, new Vector2(0.5f, 0f), new Vector2(0, -16), new Vector2(140, 34));

        var badgeGo = new GameObject("TagBadge", typeof(RectTransform), typeof(Image));
        badgeGo.transform.SetParent(go.transform, false);
        var badgeRt = (RectTransform)badgeGo.transform;
        badgeRt.anchorMin = badgeRt.anchorMax = new Vector2(1f, 1f);
        badgeRt.anchoredPosition = new Vector2(-6, -6);
        badgeRt.sizeDelta = new Vector2(64, 30);
        badgeGo.GetComponent<Image>().color = new Color(0.35f, 0.22f, 0.10f, 0.95f);
        badgeGo.GetComponent<Image>().raycastTarget = false;
        TextMeshProUGUI tag = NewLabel(badgeGo.transform, "TagLabel", font, 20, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(64, 30));

        if (go.GetComponent<CanvasGroup>() == null) go.AddComponent<CanvasGroup>();
        var gift = go.AddComponent<FreeGiftButton>();
        var so = new SerializedObject(gift);
        so.FindProperty("button").objectReferenceValue = btn != null ? btn : go.GetComponent<Button>();
        so.FindProperty("icon").objectReferenceValue = img;
        so.FindProperty("notificationDot").objectReferenceValue = dot;
        so.FindProperty("timerLabel").objectReferenceValue = timer;
        so.FindProperty("tagBadge").objectReferenceValue = badgeGo;
        so.FindProperty("tagLabel").objectReferenceValue = tag;
        so.ApplyModifiedPropertiesWithoutUndo();

        var lmc = Object.FindFirstObjectByType<LocationModeController>(FindObjectsInactive.Include);
        if (lmc != null)
        {
            var lso = new SerializedObject(lmc);
            SerializedProperty list = lso.FindProperty("hideAtMarketByName");
            bool present = false;
            for (int i = 0; i < list.arraySize; i++) if (list.GetArrayElementAtIndex(i).stringValue == "FreeGiftButton") present = true;
            if (!present)
            {
                list.InsertArrayElementAtIndex(list.arraySize);
                list.GetArrayElementAtIndex(list.arraySize - 1).stringValue = "FreeGiftButton";
                lso.ApplyModifiedPropertiesWithoutUndo();
            }
        }
        else Debug.LogWarning("[Monetization] No LocationModeController; the gift button won't hide at the Market.");

        go.transform.SetSiblingIndex(egg.transform.GetSiblingIndex() + 1);
        EditorUtility.SetDirty(go);
        EditorSceneManager.MarkSceneDirty(go.scene);
        EditorSceneManager.SaveScene(go.scene);
        Debug.Log("[Monetization] Baked FreeGiftButton into " + go.scene.name + ".");
    }

    private static TextMeshProUGUI NewLabel(Transform parent, string name, TMP_FontAsset font, float size,
                                            Vector2 anchor, Vector2 pos, Vector2 sizeDelta)
    {
        var lgo = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        lgo.transform.SetParent(parent, false);
        var lrt = (RectTransform)lgo.transform;
        lrt.anchorMin = lrt.anchorMax = anchor;
        lrt.anchoredPosition = pos;
        lrt.sizeDelta = sizeDelta;
        var t = lgo.GetComponent<TextMeshProUGUI>();
        if (font != null) t.font = font;
        t.fontSize = size;
        t.fontStyle = FontStyles.Bold;
        t.alignment = TextAlignmentOptions.Center;
        t.color = new Color(0.98f, 0.93f, 0.82f);
        t.outlineWidth = 0.2f;
        t.outlineColor = new Color32(40, 25, 10, 255);
        t.raycastTarget = false;
        return t;
    }
```

- [ ] **Step 3: Compile and bake**

Run **Compile**, then:
```bash
cd "/c/Users/rjcla/IdleFarm - Silo"
printf '%s' "Farm Game/Monetization/Bake Gift Button" > Temp/menu.request; sleep 8
grep -n "m_Name: FreeGiftButton" Assets/Scenes/FarmMain.unity
grep -n "hideAtMarketByName" -A12 Assets/Scenes/FarmMain.unity | grep FreeGiftButton
```
Expected: both greps hit, and the console shows `[Monetization] Baked FreeGiftButton into FarmMain.`

- [ ] **Step 4: Checkpoint.** **EditMode tests** green. Don't commit.

---

### Task 8: StoreManager + save fields

**Files:**
- Create: `Assets/Scripts/Monetization/StoreManager.cs`
- Modify: `Assets/Scripts/GameData.cs` (store fields)
- Modify: `Assets/Scripts/SaveManager.cs` (capture/load next to FreeGiftManager)

**Interfaces:**
- Consumes: `PurchaseLedgerCore`, `StoreProductDef`, `ProductKind`, `StoreDefaults.PassId` (Task 2); `StoreCatalogSO` (Task 3); `MonetizationServices.Store` (Task 4); `ChestRevealUITK`, `RewardLine`, `RewardCurrency` (Task 5); `FreeGiftManager.PassOwned`, `FreeGiftManager.Instance.MarkPitchShown()` (Task 6).
- Produces:
  - `StoreManager`:
    - `static Instance`
    - `event Action OnEntitlementsChanged`
    - `bool HasFarmersPass`, `bool Busy`
    - `string PriceFor(string productId)`, `void Purchase(string productId)`, `void Restore()`
    - Save: `void CaptureTo(GameData)`, `void LoadFrom(GameData)`
    - Dev: `void DevResetPass()`
  - GameData: `farmersPassOwned`, `deliveredTransactionIds`

- [ ] **Step 1: Add the save fields**

In `GameData.cs`, below the Free Gift fields from Task 6:
```csharp
    public bool farmersPassOwned;
    public string[] deliveredTransactionIds;
```

- [ ] **Step 2: Write StoreManager**

```csharp
// Assets/Scripts/Monetization/StoreManager.cs
using System;
using UnityEngine;

/// <summary>
/// Real-money purchases (spec §6, §8, §9). Delivery: ledger check -> grant -> save -> confirm to the store,
/// then the chest reveal. The Farmer's Pass is cached locally for offline play and re-checked against the
/// store account at launch (store says not owned -> cleared; store unreachable -> kept).
/// Self-bootstrapping per scene, like FreeGiftManager.
/// </summary>
public sealed class StoreManager : MonoBehaviour
{
    private const string PassBanner = "Farmer's Pass unlocked!";

    public static StoreManager Instance { get; private set; }
    public event Action OnEntitlementsChanged;

    private readonly PurchaseLedgerCore ledger = new PurchaseLedgerCore();
    private bool passOwned;
    private bool busy;
    private bool initStarted;

    public bool HasFarmersPass => passOwned;
    public bool Busy => busy;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        UnityEngine.SceneManagement.SceneManager.sceneLoaded -= OnSceneLoaded;
        UnityEngine.SceneManagement.SceneManager.sceneLoaded += OnSceneLoaded;
        EnsureInstance();
    }

    private static void OnSceneLoaded(UnityEngine.SceneManagement.Scene s, UnityEngine.SceneManagement.LoadSceneMode m) => EnsureInstance();

    private static void EnsureInstance()
    {
        if (Instance != null || SaveManager.Instance == null) return;
        new GameObject("StoreManager").AddComponent<StoreManager>();
    }

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        FreeGiftManager.PassOwned = () => Instance != null && Instance.passOwned;
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            MonetizationServices.Store.OnPendingPurchase -= Deliver;
            Instance = null;
        }
    }

    // After LoadGame (CurrencyManager.Start, order 2000) so a launch-time reconcile wins over the save.
    private void Start() => Invoke(nameof(InitStore), 0.5f);

    private void InitStore()
    {
        if (initStarted) return;
        initStarted = true;
        IStoreService store = MonetizationServices.Store;
        store.OnPendingPurchase += Deliver;
        store.Initialize(ok =>
        {
            if (!ok) return;
            bool? owned = store.IsOwned(StoreDefaults.PassId);
            if (owned.HasValue && owned.Value != passOwned)
            {
                passOwned = owned.Value;
                Debug.Log($"[Store] Pass ownership reconciled with the store: {passOwned}");
                SaveManager.Instance?.SaveGame();
                OnEntitlementsChanged?.Invoke();
            }
        });
    }

    public string PriceFor(string productId)
    {
        string live = MonetizationServices.Store.GetPriceString(productId);
        if (!string.IsNullOrEmpty(live)) return live;
        StoreProductDef def = StoreCatalogSO.Instance.Get(productId);
        return def != null ? def.fallbackPrice : "";
    }

    public void Purchase(string productId)
    {
        StoreProductDef def = StoreCatalogSO.Instance.Get(productId);
        if (def == null || def.comingSoon || busy) return;
        if (def.kind == ProductKind.NonConsumable && productId == StoreDefaults.PassId && passOwned) return;
        busy = true;
        OnEntitlementsChanged?.Invoke();
        MonetizationServices.Store.Purchase(productId, result =>
        {
            busy = false;
            switch (result.outcome)
            {
                case PurchaseOutcome.Success: Deliver(result); break;
                case PurchaseOutcome.Failed: ToastManager.Show("Purchase failed. You weren't charged."); break;
                case PurchaseOutcome.Unavailable: ToastManager.Show("Store unavailable right now."); break;
                case PurchaseOutcome.Cancelled: break;
            }
            OnEntitlementsChanged?.Invoke();
        });
    }

    public void Restore()
    {
        MonetizationServices.Store.Restore(ok =>
            ToastManager.Show(ok ? "Purchases restored." : "Couldn't restore. Try again later."));
    }

    /// <summary>Grant + save, THEN confirm. Runs for fresh purchases and store re-deliveries alike.</summary>
    private void Deliver(PurchaseResult result)
    {
        StoreProductDef def = StoreCatalogSO.Instance.Get(result.productId);
        if (def == null)
        {
            Debug.LogError($"[Store] Delivered unknown product '{result.productId}'. Confirming so it doesn't loop.");
            MonetizationServices.Store.ConfirmDelivered(result.transactionId);
            return;
        }

        bool isPass = def.kind == ProductKind.NonConsumable && def.id == StoreDefaults.PassId;
        bool ledgerNew = ledger.TryMarkDelivered(result.transactionId);
        bool grant = PurchaseLedgerCore.ShouldGrant(ledgerNew, def.kind == ProductKind.NonConsumable, isPass && passOwned);

        if (isPass)
        {
            passOwned = true;
            FreeGiftManager.Instance?.MarkPitchShown();
        }
        if (grant && def.gems > 0) CurrencyManager.Instance?.AddGems(def.gems);
        Debug.Log($"[Store] Delivered {def.id} (tx {result.transactionId}) grant={grant}");

        SaveManager.Instance?.SaveGame();
        MonetizationServices.Store.ConfirmDelivered(result.transactionId);
        OnEntitlementsChanged?.Invoke();
        FreeGiftManager.Instance?.NotifyChanged(); // HUD tag drops "AD" once the pass is owned

        if (!grant) return;
        var request = new ChestRevealRequest { banner = isPass ? PassBanner : null };
        if (def.gems > 0) request.lines.Add(new RewardLine(RewardCurrency.Gems, def.gems));
        ChestRevealUITK.Show(request); // gems already granted; the chest is the celebration
    }

    public void CaptureTo(GameData d)
    {
        d.farmersPassOwned = passOwned;
        d.deliveredTransactionIds = ledger.Export();
    }

    public void LoadFrom(GameData d)
    {
        passOwned = d.farmersPassOwned;
        ledger.Import(d.deliveredTransactionIds);
        OnEntitlementsChanged?.Invoke();
    }

    /// <summary>Settings > Dev: forget the pass locally AND in the fake store account.</summary>
    public void DevResetPass()
    {
        passOwned = false;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        FakeStoreService.DevClearAccount();
#endif
        SaveManager.Instance?.SaveGame();
        OnEntitlementsChanged?.Invoke();
    }
}
```

- [ ] **Step 3: Save hooks**

`SaveManager.SaveGame`, right after the FreeGiftManager capture line from Task 6:
```csharp
        if (StoreManager.Instance != null) StoreManager.Instance.CaptureTo(data);
```
`SaveManager.LoadGame`, right after the FreeGiftManager load lines:
```csharp
                if (StoreManager.Instance != null)
                    StoreManager.Instance.LoadFrom(data);
```

- [ ] **Step 4: Compile and checkpoint**

Run **Compile** and **EditMode tests**: green. Don't commit.

---

### Task 9: StorePopupUITK + entry points + Settings rows

**Files:**
- Create: `Assets/Scripts/Monetization/UI/StorePopupUITK.cs`
- Modify: `Assets/Scripts/UI/TopBarUITK.cs` (gem row opens the Store)
- Modify: `Assets/Scripts/UI/SettingsPopupUITK.cs` (Account: "Store". Dev: gift and pass dev rows)

**Interfaces:**
- Consumes:
  - `StoreManager.Instance.Purchase/Restore/PriceFor/HasFarmersPass/Busy/OnEntitlementsChanged` (Task 8)
  - `FreeGiftManager.Instance.Status/SecondsUntilReady/NextClaimNeedsAd/RequestClaim/GemsPerClaim/CoinReward/OnStateChanged/DevResetGift/DevMakeReady` (Task 6)
  - `StoreCatalogSO` (Task 3), `MonetizationUI` (Task 4), `MonetizationServices.IsTestStore` (Task 4), `FakeAdService.SimulateNoFill` (Task 4)
- Produces: `StorePopupUITK { static void Open(); static void Close(); static bool IsOpen; }`

- [ ] **Step 1: Write the Store popup**

```csharp
// Assets/Scripts/Monetization/UI/StorePopupUITK.cs
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Scrollable Store (spec §6): Free Gift card, Farmer's Pass hero card, gem bundles, coming-soon teasers,
/// Restore footer. Built once; while open only labels/buttons update (never a rebuild on a tick).
/// Self-creating, sort 1150. Opened from the top-bar gem counter and Settings.
/// </summary>
public sealed class StorePopupUITK : MonoBehaviour
{
    private const int SortOrder = 1150;

    private static StorePopupUITK instance;
    public static bool IsOpen => instance != null && instance.isOpen;

    private PanelSettings runtimeSettings;
    private VisualElement root, popupRoot;
    private Label giftStatus, giftReward, passOwnedLabel;
    private Button giftButton, passBuy;
    private readonly List<(string id, Button button)> priceButtons = new List<(string, Button)>();
    private IVisualElementScheduledItem ticker;
    private bool isOpen, subscribed;

    public static void Open()
    {
        if (instance == null) instance = new GameObject("StorePopupUITK").AddComponent<StorePopupUITK>();
        instance.DoOpen();
    }

    public static void Close() { if (instance != null) instance.DoClose(); }

    private void OnDestroy()
    {
        Unsubscribe();
        if (instance == this) instance = null;
        if (runtimeSettings != null) Destroy(runtimeSettings);
    }

    private void DoOpen()
    {
        if (!EnsureBuilt()) return;
        Subscribe();
        isOpen = true;
        root.pickingMode = PickingMode.Position;
        popupRoot.style.display = DisplayStyle.Flex;
        RefreshAll();
        ticker?.Resume();
    }

    private void DoClose()
    {
        if (!isOpen) return;
        isOpen = false;
        ticker?.Pause();
        popupRoot.style.display = DisplayStyle.None;
        root.pickingMode = PickingMode.Ignore;
    }

    private void Subscribe()
    {
        if (subscribed) return;
        if (StoreManager.Instance != null) StoreManager.Instance.OnEntitlementsChanged += RefreshAll;
        if (FreeGiftManager.Instance != null) FreeGiftManager.Instance.OnStateChanged += RefreshAll;
        subscribed = true;
    }

    private void Unsubscribe()
    {
        if (!subscribed) return;
        if (StoreManager.Instance != null) StoreManager.Instance.OnEntitlementsChanged -= RefreshAll;
        if (FreeGiftManager.Instance != null) FreeGiftManager.Instance.OnStateChanged -= RefreshAll;
        subscribed = false;
    }

    // ── Build (once) ─────────────────────────────────────────────

    private bool EnsureBuilt()
    {
        if (root != null) return true;
        root = MonetizationUI.CreateRoot(this, SortOrder, "Store", out runtimeSettings);
        if (root == null) return false;

        popupRoot = new VisualElement();
        MonetizationUI.Fill(popupRoot);
        popupRoot.style.alignItems = Align.Center;
        popupRoot.style.justifyContent = Justify.Center;
        popupRoot.style.display = DisplayStyle.None;
        root.Add(popupRoot);

        var backdrop = new VisualElement();
        MonetizationUI.Fill(backdrop);
        backdrop.style.backgroundColor = new Color(0f, 0f, 0f, 0.8f);
        backdrop.RegisterCallback<ClickEvent>(_ => DoClose());
        popupRoot.Add(backdrop);

        VisualElement card = MonetizationUI.Card();
        card.style.width = Length.Percent(94);
        card.style.maxWidth = 900;
        card.style.height = Length.Percent(86);
        popupRoot.Add(card);

        var header = new VisualElement();
        header.style.flexDirection = FlexDirection.Row;
        header.style.alignItems = Align.Center;
        header.style.marginBottom = 12;
        Label title = MonetizationUI.Text("Store", 52, MonetizationUI.Ink, bold: true);
        MonetizationUI.Font(title, MonetizationUI.TitleFont);
        title.style.flexGrow = 1;
        header.Add(title);
        header.Add(MonetizationUI.BrownButton("X", DoClose, 30));
        card.Add(header);

        var scroll = new ScrollView(ScrollViewMode.Vertical);
        scroll.style.flexGrow = 1;
        scroll.verticalScrollerVisibility = ScrollerVisibility.Auto;
        card.Add(scroll);

        StoreCatalogSO catalog = StoreCatalogSO.Instance;
        BuildGiftCard(scroll);
        foreach (StoreProductDef p in catalog.products) if (p != null && p.section == StoreSection.Pass) BuildPassCard(scroll, p);
        SectionTitle(scroll, "Gems");
        foreach (StoreProductDef p in catalog.products) if (p != null && p.section == StoreSection.Gems) BuildBundleRow(scroll, p);
        SectionTitle(scroll, "Coming soon");
        foreach (StoreProductDef p in catalog.products) if (p != null && p.section == StoreSection.ComingSoon) BuildSoonRow(scroll, p);
        BuildFooter(scroll);

        ticker = root.schedule.Execute(RefreshGift).Every(500);
        ticker.Pause();
        return true;
    }

    private static VisualElement Panel()
    {
        var e = new VisualElement();
        e.style.backgroundColor = new Color(0.98f, 0.93f, 0.82f, 0.85f);
        MonetizationUI.Radius(e, 14);
        e.style.paddingLeft = 20; e.style.paddingRight = 20; e.style.paddingTop = 16; e.style.paddingBottom = 16;
        e.style.marginBottom = 14;
        e.style.flexDirection = FlexDirection.Row;
        e.style.alignItems = Align.Center;
        return e;
    }

    private static void SectionTitle(VisualElement parent, string text)
    {
        Label l = MonetizationUI.Text(text, 36, MonetizationUI.Ink, bold: true);
        MonetizationUI.Font(l, MonetizationUI.TitleFont);
        l.style.marginTop = 10; l.style.marginBottom = 8;
        parent.Add(l);
    }

    private void BuildGiftCard(VisualElement parent)
    {
        VisualElement panel = Panel();
        MonetizationArt art = MonetizationArt.Instance;
        panel.Add(MonetizationUI.Icon(art != null ? art.chestClosed : null, 110));
        var col = new VisualElement();
        col.style.flexGrow = 1; col.style.marginLeft = 16;
        col.Add(MonetizationUI.Text("Free Gift", 34, MonetizationUI.Ink, bold: true));
        giftReward = MonetizationUI.Text("", 26, MonetizationUI.Ink);
        col.Add(giftReward);
        giftStatus = MonetizationUI.Text("", 26, MonetizationUI.Muted);
        col.Add(giftStatus);
        panel.Add(col);
        giftButton = MonetizationUI.BrownButton("Open", () => FreeGiftManager.Instance?.RequestClaim(), 28);
        giftButton.style.minWidth = 170;
        panel.Add(giftButton);
        parent.Add(panel);
    }

    private void BuildPassCard(VisualElement parent, StoreProductDef p)
    {
        VisualElement panel = Panel();
        panel.style.flexDirection = FlexDirection.Column;
        panel.style.alignItems = Align.Stretch;
        panel.style.backgroundColor = new Color(0.99f, 0.88f, 0.62f, 0.95f);

        var top = new VisualElement();
        top.style.flexDirection = FlexDirection.Row;
        top.style.alignItems = Align.Center;
        MonetizationArt art = MonetizationArt.Instance;
        Sprite hero = art != null && art.passArt != null ? art.passArt : p.icon;
        top.Add(MonetizationUI.Icon(hero, 140));
        var col = new VisualElement();
        col.style.flexGrow = 1; col.style.marginLeft = 16;
        Label name = MonetizationUI.Text(p.displayName, 40, MonetizationUI.Ink, bold: true);
        MonetizationUI.Font(name, MonetizationUI.TitleFont);
        col.Add(name);
        col.Add(MonetizationUI.Text($"+{p.gems:N0} gems", 28, MonetizationUI.Walnut, bold: true));
        top.Add(col);
        panel.Add(top);

        Label desc = MonetizationUI.Text(p.description, 26, MonetizationUI.Ink);
        desc.style.marginTop = 10; desc.style.marginBottom = 12;
        panel.Add(desc);

        passBuy = MonetizationUI.BrownButton("", () => StoreManager.Instance?.Purchase(p.id), 32);
        priceButtons.Add((p.id, passBuy));
        panel.Add(passBuy);
        passOwnedLabel = MonetizationUI.Text("Owned", 32, MonetizationUI.Walnut, bold: true);
        passOwnedLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
        panel.Add(passOwnedLabel);
        parent.Add(panel);
    }

    private void BuildBundleRow(VisualElement parent, StoreProductDef p)
    {
        VisualElement panel = Panel();
        panel.Add(MonetizationUI.Icon(p.icon, 84));
        var col = new VisualElement();
        col.style.flexGrow = 1; col.style.marginLeft = 16;
        col.Add(MonetizationUI.Text(p.displayName, 30, MonetizationUI.Ink, bold: true));
        col.Add(MonetizationUI.Text($"{p.gems:N0} gems", 26, MonetizationUI.Ink));
        panel.Add(col);
        if (!string.IsNullOrEmpty(p.ribbon))
        {
            Label ribbon = MonetizationUI.Text(p.ribbon, 22, MonetizationUI.Cream, bold: true);
            ribbon.style.backgroundColor = MonetizationUI.Walnut;
            MonetizationUI.Radius(ribbon, 8);
            ribbon.style.paddingLeft = 10; ribbon.style.paddingRight = 10; ribbon.style.paddingTop = 4; ribbon.style.paddingBottom = 4;
            ribbon.style.marginRight = 12;
            panel.Add(ribbon);
        }
        Button buy = MonetizationUI.BrownButton("", () => StoreManager.Instance?.Purchase(p.id), 28);
        buy.style.minWidth = 170;
        priceButtons.Add((p.id, buy));
        panel.Add(buy);
        parent.Add(panel);
    }

    private static void BuildSoonRow(VisualElement parent, StoreProductDef p)
    {
        VisualElement panel = Panel();
        panel.style.opacity = 0.6f;
        var col = new VisualElement();
        col.style.flexGrow = 1;
        col.Add(MonetizationUI.Text(p.displayName, 30, MonetizationUI.Ink, bold: true));
        col.Add(MonetizationUI.Text(p.description, 24, MonetizationUI.Muted));
        panel.Add(col);
        panel.Add(MonetizationUI.Text("Soon", 26, MonetizationUI.Muted, bold: true));
        parent.Add(panel);
    }

    private void BuildFooter(VisualElement parent)
    {
        var footer = new VisualElement();
        footer.style.alignItems = Align.Center;
        footer.style.marginTop = 10; footer.style.marginBottom = 20;
        Button restore = new Button(() => StoreManager.Instance?.Restore()) { text = "Restore Purchases" };
        restore.style.backgroundColor = Color.clear;
        MonetizationUI.NoBorder(restore);
        restore.style.color = MonetizationUI.Walnut;
        restore.style.fontSize = 26;
        restore.style.unityFontStyleAndWeight = FontStyle.Bold;
        footer.Add(restore);
        string fine = MonetizationServices.IsTestStore ? "Test store. No real charges." : "Prices in your local currency.";
        footer.Add(MonetizationUI.Text(fine, 22, MonetizationUI.Muted));
        parent.Add(footer);
    }

    // ── Refresh (labels only) ────────────────────────────────────

    private void RefreshAll()
    {
        if (root == null) return;
        StoreManager sm = StoreManager.Instance;
        bool owned = sm != null && sm.HasFarmersPass;
        bool busy = sm != null && sm.Busy;
        if (passBuy != null) passBuy.style.display = owned ? DisplayStyle.None : DisplayStyle.Flex;
        if (passOwnedLabel != null) passOwnedLabel.style.display = owned ? DisplayStyle.Flex : DisplayStyle.None;
        foreach ((string id, Button button) in priceButtons)
        {
            button.text = sm != null ? sm.PriceFor(id) : "";
            button.SetEnabled(!busy);
        }
        RefreshGift();
    }

    private void RefreshGift()
    {
        FreeGiftManager m = FreeGiftManager.Instance;
        if (giftStatus == null) return;
        GiftStatus s = m != null ? m.Status : GiftStatus.Locked;
        if (m != null) giftReward.text = $"{m.GemsPerClaim} gems + {m.CoinReward:N0} coins";
        switch (s)
        {
            case GiftStatus.Locked:
                giftStatus.text = "Help the town to unlock it.";
                giftButton.SetEnabled(false); giftButton.text = "Locked"; break;
            case GiftStatus.Ready:
                giftStatus.text = "Ready!";
                giftButton.SetEnabled(true); giftButton.text = m.NextClaimNeedsAd ? "Watch" : "Open"; break;
            case GiftStatus.Cooldown:
                giftStatus.text = "Next gift in " + MonetizationUI.Mmss(m.SecondsUntilReady);
                giftButton.SetEnabled(false); giftButton.text = "Wait"; break;
            case GiftStatus.Capped:
                giftStatus.text = "That's all for today.";
                giftButton.SetEnabled(false); giftButton.text = "Tomorrow"; break;
        }
    }
}
```

- [ ] **Step 2: Top-bar gem counter opens the Store**

In `TopBarUITK.Cache()`, after `compostLabel = ...;` add:
```csharp
        VisualElement gemsRow = root.Q<VisualElement>("row-gems");
        if (gemsRow != null && !gemsRowWired)
        {
            gemsRow.RegisterCallback<ClickEvent>(_ => StorePopupUITK.Open());
            gemsRowWired = true;
        }
```
and add the field `private bool gemsRowWired;` next to `private bool subscribed;`. The guard keeps `OnEnable` re-runs from stacking handlers.

- [ ] **Step 3: Settings rows**

In `SettingsPopupUITK.BuildAccountSection()`, after the "Farm Name" row add:
```csharp
        SpawnButtonRow(rows, "Store", "Gems and the Farmer's Pass", "Open", () => StorePopupUITK.Open());
```

In `BuildDevSection()`, after the "Grant Gems" row add:
```csharp
        SpawnButtonRow(rows, "Free Gift: Ready Now", "Skip the 30-min cooldown", "Ready",
            () => FreeGiftManager.Instance?.DevMakeReady());
        SpawnButtonRow(rows, "Free Gift: Reset", "Clear claims, daily count and pitch", "Reset",
            () => FreeGiftManager.Instance?.DevResetGift());
        SpawnButtonRow(rows, "Reset Farmer's Pass", "Forget the pass here and in the test store", "Reset",
            () => StoreManager.Instance?.DevResetPass());
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        SpawnToggleRow(rows, "Test Ads: No Fill", "Pretend no ad is available",
            FakeAdService.SimulateNoFill, v => FakeAdService.SimulateNoFill = v);
#endif
```
Check `SpawnToggleRow`'s signature in `SettingsPopupUITK.cs` before pasting. It's used as `SpawnToggleRow(rows, "Mute All", "Silence everything", <bool>, <Action<bool>>)` at line ~66. Match the existing argument order exactly.

- [ ] **Step 4: Compile and checkpoint**

Run **Compile** and **EditMode tests**: green. Don't commit.

---

### Task 10: PassPitchUITK (one-time pitch)

**Files:**
- Modify (replace placeholder): `Assets/Scripts/Monetization/UI/PassPitchUITK.cs`

**Interfaces:**
- Consumes: `StoreCatalogSO.Get(StoreDefaults.PassId)`, `StoreManager.Instance.Purchase/PriceFor/HasFarmersPass`, `FreeGiftManager.Instance.MarkPitchShown()`, `MonetizationUI`, `MonetizationArt`.
- Produces: `PassPitchUITK.Show()` (static). It marks the pitch shown and is a no-op if the pass is already owned.

- [ ] **Step 1: Write the pitch**

```csharp
// Assets/Scripts/Monetization/UI/PassPitchUITK.cs
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>One-time Farmer's Pass pitch after the 3rd ad claim (spec §7): art, title, description,
/// price button, "Maybe later". Self-creating, sort 1450.</summary>
public sealed class PassPitchUITK : MonoBehaviour
{
    private const int SortOrder = 1450;

    private static PassPitchUITK instance;
    private PanelSettings runtimeSettings;
    private VisualElement root, layer;
    private Button buy;

    public static void Show()
    {
        FreeGiftManager.Instance?.MarkPitchShown();
        if (StoreManager.Instance != null && StoreManager.Instance.HasFarmersPass) return;
        if (instance == null) instance = new GameObject("PassPitchUITK").AddComponent<PassPitchUITK>();
        instance.Open();
    }

    private void OnDestroy()
    {
        if (instance == this) instance = null;
        if (runtimeSettings != null) Destroy(runtimeSettings);
    }

    private void Open()
    {
        if (root == null)
        {
            root = MonetizationUI.CreateRoot(this, SortOrder, "PassPitch", out runtimeSettings);
            if (root == null) return;
            Build();
        }
        buy.text = StoreManager.Instance != null ? StoreManager.Instance.PriceFor(StoreDefaults.PassId) : "";
        layer.style.display = DisplayStyle.Flex;
        root.pickingMode = PickingMode.Position;
    }

    private void Close()
    {
        layer.style.display = DisplayStyle.None;
        root.pickingMode = PickingMode.Ignore;
    }

    private void Build()
    {
        layer = new VisualElement();
        MonetizationUI.Fill(layer);
        layer.style.backgroundColor = new Color(0f, 0f, 0f, 0.78f);
        layer.style.alignItems = Align.Center;
        layer.style.justifyContent = Justify.Center;
        layer.RegisterCallback<ClickEvent>(e => { if (e.target == layer) Close(); });
        root.Add(layer);

        VisualElement card = MonetizationUI.Card();
        card.style.width = Length.Percent(86);
        card.style.maxWidth = 780;
        card.style.alignItems = Align.Center;
        layer.Add(card);

        StoreProductDef pass = StoreCatalogSO.Instance.Get(StoreDefaults.PassId);
        MonetizationArt art = MonetizationArt.Instance;
        Sprite hero = art != null && art.passArt != null ? art.passArt : (art != null ? art.chestClosed : null);
        card.Add(MonetizationUI.Icon(hero, 200));

        Label title = MonetizationUI.Text("Farmer's Pass", 50, MonetizationUI.Ink, bold: true);
        MonetizationUI.Font(title, MonetizationUI.TitleFont);
        title.style.marginTop = 8;
        card.Add(title);

        Label desc = MonetizationUI.Text(pass != null ? pass.description : StoreDefaults.PassDescription, 28, MonetizationUI.Ink);
        desc.style.unityTextAlign = TextAnchor.MiddleCenter;
        desc.style.marginTop = 10; desc.style.marginBottom = 20;
        card.Add(desc);

        buy = MonetizationUI.BrownButton("", () => { Close(); StoreManager.Instance?.Purchase(StoreDefaults.PassId); }, 34);
        buy.style.minWidth = 360;
        card.Add(buy);

        Button later = new Button(Close) { text = "Maybe later" };
        later.style.backgroundColor = Color.clear;
        MonetizationUI.NoBorder(later);
        later.style.color = MonetizationUI.Walnut;
        later.style.fontSize = 26;
        later.style.marginTop = 12;
        card.Add(later);
    }
}
```

- [ ] **Step 2: Compile and checkpoint**

Run **Compile** and **EditMode tests**: green. Don't commit.

---

### Task 11: Barn milestone tooltips show the Free Gift coins

**Files:**
- Modify: `Assets/Scripts/UI/BarnPopupUITK.cs:377-399`

**Interfaces:**
- Consumes: `FreeGiftManager.CoinsAtLevel(int level)` (static, Task 6).

- [ ] **Step 1: Replace the placeholder milestone copy**

Replace the `renownDesc` array and the `desc` line in the loop (`BarnPopupUITK.cs:378-389`) with:

```csharp
        for (int i = 0; i < renownMilestones.Length; i++)
        {
            int threshold = renownMilestones[i];
            string gift = $"Free Gift: {FreeGiftManager.CoinsAtLevel(threshold):N0} coins.";
            string desc = threshold == 25 ? "Silo: increases inventory space. (Not yet implemented.)\n" + gift : gift;
```
Delete the old `string[] renownDesc = { ... };` block and the old `string desc = renownDesc[i];` line. Keep the rest of the loop body (notch button, `notchTitle`, `ShowTooltip`) unchanged.

- [ ] **Step 2: Compile and checkpoint**

Run **Compile** and **EditMode tests**: green. Don't commit.

---

### Task 12: Play-mode verification (every Review Focus item)

**Files:** none. This task drives the game and screenshots it.

Back up the save first (memory rule):
```bash
S="$USERPROFILE/AppData/LocalLow/DefaultCompany/IdleFarm - Silo"
cp "$S/gamedata.json" "$S/gamedata.pre-store-test.json"; cp "$S/gamedata.json.bak" "$S/gamedata.pre-store-test.json.bak" 2>/dev/null
```

Drive play mode with `Temp/enter_play_mode.request` / `Temp/exit_play_mode.request`. Click with `Temp/ui_click.request` (UITK element by name) or `Temp/ui_tap.request` ("x,y", bottom-left px). Set the Game view with `Temp/gameview_size.request` "1080x2400". Screenshot the overlay UI via `ScreenCapture.CaptureScreenshot` (GladeKit `look_at_game_view` shows the world only). Check `get_unity_console_logs` after each step for exceptions.

- [ ] **Step 1: Unlocked + intro.** The dev save has Barn points, so the gift is unlocked. Enter play.
  - Expect the HUD chest at (70, 550) above the egg slot.
  - Expect the spotlight tooltip *"A gift every 30 minutes. This one's on us!"* on it.
  - Expect the tag **FREE**.
- [ ] **Step 2: Free chest.** Press the button. Expect:
  - no ad, and the chest drops in;
  - three taps: shake, bigger shake + glow, burst with rays, spray and `+10` gems / `+N` coins counting up;
  - **Collect**: numbers fly to the top bar, which updates;
  - the button dims to `29:5x`;
  - console `[FreeGift] Claimed +10 gems ... (free)`.
- [ ] **Step 3: Ad chest + mash (Review Focus 3).** Settings → Dev → "Free Gift: Ready Now". Expect:
  - the tag reads **AD**;
  - pressing the button 3× fast opens **one** Test Ad;
  - letting it run gives one chest; **Claim** then **Collect** closes it in 2 presses;
  - the console shows exactly one claim line.
- [ ] **Step 4: Close early + no fill.** Ready Now → press → "Close early". Expect no reward and the button still Ready. Then toggle "Test Ads: No Fill" → press. Expect the toast *"No gift right now. Try again soon."* and the button still Ready. Toggle No Fill off.
- [ ] **Step 5: Pitch.** Ready Now + watch the ad twice more (3 ad claims total). Expect the **Farmer's Pass** pitch after the 3rd chest closes, and never again after "Maybe later" (verify with a 4th ad claim).
- [ ] **Step 6: Store.** Tap the top-bar gem counter. Expect, in browns:
  - the Store scrolls;
  - the Free Gift card shows the status and timer;
  - the Pass card shows `$9.99`;
  - 5 bundles show prices and ribbons;
  - 4 "Soon" rows;
  - footer "Restore Purchases" + "Test store. No real charges.".

  Buy **Pouch** → Confirm → expect a chest with `+550` gems and the gems added. Buy **Handful** → **Fail** → expect the toast "Purchase failed. You weren't charged." and no gems. Buy → **Cancel** → nothing.
- [ ] **Step 7: Second reveal queues (Review Focus 2).** Ready Now → open the gift chest. **Don't open it.** From Settings → Dev, call "Reset Farmer's Pass", then use the Store behind it. If the Store isn't reachable, run `StoreManager.Instance.Purchase("farmers_pass")` via GladeKit execute_code and Confirm. Expect:
  - the pass chest waits until the gift chest closes, then shows with the banner "Farmer's Pass unlocked!" and `+500`;
  - each grant happens once (console).
- [ ] **Step 8: Pass behaviour + restore (Review Focus 5).** With the pass owned, expect:
  - the HUD tag hidden;
  - Ready Now → press → chest immediately (no ad);
  - the Store pass card shows "Owned".

  Tap **Restore Purchases**. Expect the toast "Purchases restored." and **no** extra 500 gems (console `grant=False`).
- [ ] **Step 9: Background mid-chest (Review Focus 1).** Ready Now → open a chest, don't tap it. Run `ChestRevealUITK` pause via GladeKit execute_code: `Object.FindFirstObjectByType<ChestRevealUITK>().SendMessage("OnApplicationPause", true)`. Expect the claim line in the console and the gems added immediately. Then open the chest and expect **no** second grant.
- [ ] **Step 10: Save round-trip.** Exit play, enter play. Expect the cooldown timer, today's count and pass ownership to persist, with no pitch replay.
- [ ] **Step 11: Market hide + Barn.** Pan to the Market and expect the HUD chest hidden. Back on the farm, expect it visible again. Open the Barn and tap the Farm Level 10 notch. Expect *"Free Gift: 100 coins."*
- [ ] **Step 12: Restore the save.**
```bash
S="$USERPROFILE/AppData/LocalLow/DefaultCompany/IdleFarm - Silo"
cp "$S/gamedata.pre-store-test.json" "$S/gamedata.json"; cp "$S/gamedata.pre-store-test.json.bak" "$S/gamedata.json.bak" 2>/dev/null
```
Also run Settings → Dev "Reset Farmer's Pass" if the test store account should be cleared.
- [ ] **Step 13: Final checkpoint.** EditMode suite green, and no exceptions logged during steps 1-11. Report the results with screenshots to the user. **Don't commit** unless the user asks.
