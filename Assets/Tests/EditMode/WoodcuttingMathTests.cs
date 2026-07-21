using NUnit.Framework;
using StackMode = WoodcuttingMath.StackMode;

public class WoodcuttingMathTests
{
    [Test]
    public void EffectiveHitsToFell_ReducesPerAxeLevel_ClampedToMin()
    {
        // base 5 hits, no axe → 5
        Assert.AreEqual(5, WoodcuttingMath.EffectiveHitsToFell(5, 0, 1));
        // axe level 2, reduction 1/level → 3
        Assert.AreEqual(3, WoodcuttingMath.EffectiveHitsToFell(5, 2, 1));
        // never drops below minHits (default 1)
        Assert.AreEqual(1, WoodcuttingMath.EffectiveHitsToFell(5, 99, 1));
        // explicit minHits respected
        Assert.AreEqual(2, WoodcuttingMath.EffectiveHitsToFell(5, 99, 1, 2));
    }

    [Test]
    public void CanFell_RequiresAxeLevelAtOrAboveTreeRequirement()
    {
        Assert.IsTrue(WoodcuttingMath.CanFell(0, 0));   // softwood, bare hands
        Assert.IsFalse(WoodcuttingMath.CanFell(1, 0));  // hardwood, no axe
        Assert.IsTrue(WoodcuttingMath.CanFell(1, 1));   // hardwood, axe lvl 1
    }

    [Test]
    public void RegrowFraction_ZeroToOne_Clamped()
    {
        Assert.AreEqual(0f, WoodcuttingMath.RegrowFraction(0, 10f), 1e-4f);
        Assert.AreEqual(0.5f, WoodcuttingMath.RegrowFraction(5, 10f), 1e-4f);
        Assert.AreEqual(1f, WoodcuttingMath.RegrowFraction(20, 10f), 1e-4f);
    }

    [Test]
    public void RegrowFraction_ZeroDuration_IsImmediatelyFull()
    {
        Assert.AreEqual(1f, WoodcuttingMath.RegrowFraction(0, 0f), 1e-4f);
    }

    [Test]
    public void IsRegrown_TrueAtOrPastDuration()
    {
        Assert.IsFalse(WoodcuttingMath.IsRegrown(9.9, 10f));
        Assert.IsTrue(WoodcuttingMath.IsRegrown(10, 10f));
        Assert.IsTrue(WoodcuttingMath.IsRegrown(50, 10f));
    }

    [Test]
    public void ResolveStackAmount_ClampsToAvailable()
    {
        Assert.AreEqual(1, WoodcuttingMath.ResolveStackAmount(StackMode.One, 50));
        Assert.AreEqual(10, WoodcuttingMath.ResolveStackAmount(StackMode.Ten, 50));
        Assert.AreEqual(5, WoodcuttingMath.ResolveStackAmount(StackMode.Ten, 5)); // fewer than 10
        Assert.AreEqual(50, WoodcuttingMath.ResolveStackAmount(StackMode.All, 50));
        Assert.AreEqual(0, WoodcuttingMath.ResolveStackAmount(StackMode.All, 0));
    }

    [Test]
    public void SellValue_MultipliesAmountByPrice_NeverNegative()
    {
        Assert.AreEqual(0, WoodcuttingMath.SellValue(0, 5));
        Assert.AreEqual(50, WoodcuttingMath.SellValue(10, 5));
        Assert.AreEqual(0, WoodcuttingMath.SellValue(-3, 5));
    }

    [Test]
    public void CanBuyAxe_RequiresNotOwnedAndEnoughCoins()
    {
        // don't own one, can afford → true
        Assert.IsTrue(WoodcuttingMath.CanBuyAxe(hasAxe: false, coins: 75, coinCost: 75));
        // can't afford → false
        Assert.IsFalse(WoodcuttingMath.CanBuyAxe(hasAxe: false, coins: 74, coinCost: 75));
        // already own one → false (buy is a one-time thing; upgrades are separate)
        Assert.IsFalse(WoodcuttingMath.CanBuyAxe(hasAxe: true, coins: 1000, coinCost: 75));
    }

    [Test]
    public void CanUpgradeAxe_RequiresUnderMaxAndAffordBoth()
    {
        // under max, can afford both → true
        Assert.IsTrue(WoodcuttingMath.CanUpgradeAxe(0, 3, coins: 100, coinCost: 100, wood: 50, woodCost: 50));
        // at max → false
        Assert.IsFalse(WoodcuttingMath.CanUpgradeAxe(3, 3, 1000, 100, 1000, 50));
        // not enough coins → false
        Assert.IsFalse(WoodcuttingMath.CanUpgradeAxe(0, 3, 99, 100, 1000, 50));
        // not enough wood → false
        Assert.IsFalse(WoodcuttingMath.CanUpgradeAxe(0, 3, 1000, 100, 49, 50));
    }

    [Test]
    public void StageIndex_PartitionsGrowthIntoStages_Clamped()
    {
        Assert.AreEqual(0, WoodcuttingMath.StageIndex(0f, 5));    // sapling
        Assert.AreEqual(0, WoodcuttingMath.StageIndex(0.19f, 5)); // still stage 0
        Assert.AreEqual(2, WoodcuttingMath.StageIndex(0.5f, 5));  // middle
        Assert.AreEqual(4, WoodcuttingMath.StageIndex(1f, 5));    // full (clamped, not 5)
        Assert.AreEqual(4, WoodcuttingMath.StageIndex(2f, 5));    // over-clamped
    }

    [Test]
    public void StageYield_ZeroForSapling_ScalesToFullAtLastStage()
    {
        // 5 stages, full yield 100 → sapling pays NOTHING (anti-exploit: instant
        // chop-replant-chop must never mint wood), then scales linearly to full.
        Assert.AreEqual(0, WoodcuttingMath.StageYield(100, 0, 5));
        Assert.AreEqual(25, WoodcuttingMath.StageYield(100, 1, 5));
        Assert.AreEqual(50, WoodcuttingMath.StageYield(100, 2, 5));
        Assert.AreEqual(100, WoodcuttingMath.StageYield(100, 4, 5));
    }

    [Test]
    public void StageHits_ScalesWithStage_MinimumOne()
    {
        // 10 hits full, 5 stages → stage 0 = 2, stage 4 = 10
        Assert.AreEqual(2, WoodcuttingMath.StageHits(10, 0, 5));
        Assert.AreEqual(10, WoodcuttingMath.StageHits(10, 4, 5));
        // never below 1 even for a tiny full count at an early stage
        Assert.AreEqual(1, WoodcuttingMath.StageHits(1, 0, 5));
    }

    [Test]
    public void SwingWood_SmallTeasersEarly_BulkOnTheFellingBlow()
    {
        // The user's example: 10 wood over 5 hits at the 0.6 default → 1,1,1,1,6 (10/10/10/10/60%).
        int[] expected = { 1, 1, 1, 1, 6 };
        for (int i = 0; i < 5; i++)
            Assert.AreEqual(expected[i], WoodcuttingMath.SwingWood(10, i + 1, 5), $"hit {i + 1}");
    }

    [Test]
    public void SwingWood_FinalBlowIsAlwaysTheLargestSwing()
    {
        // Whatever the hit count, the felling blow must be the biggest single reward (lopsided).
        foreach (var (yield, needed) in new[] { (50, 6), (110, 10), (25, 3), (30, 2), (7, 4) })
        {
            int final = WoodcuttingMath.SwingWood(yield, needed, needed);
            for (int i = 1; i < needed; i++)
                Assert.GreaterOrEqual(final, WoodcuttingMath.SwingWood(yield, i, needed),
                    $"final blow must be ≥ swing {i} (yield {yield}, {needed} hits)");
        }
    }

    [Test]
    public void SwingWood_SwingsAlwaysSumToTotal()
    {
        // Whatever the split, the per-swing amounts must total exactly the yield (no minting/loss).
        foreach (var (yield, needed) in new[] { (50, 6), (25, 3), (7, 10), (100, 1), (0, 4) })
        {
            int sum = 0;
            for (int i = 1; i <= needed; i++) sum += WoodcuttingMath.SwingWood(yield, i, needed);
            Assert.AreEqual(yield, sum, $"yield {yield} over {needed} hits");
        }
    }

    [Test]
    public void SwingWood_SingleHitFell_PaysWholeTreeOnThatBlow()
    {
        Assert.AreEqual(100, WoodcuttingMath.SwingWood(100, 1, 1));
    }

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
}
