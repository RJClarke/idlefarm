using NUnit.Framework;

public class ProcessingMathTests
{
    // ── Tier math ──────────────────────────────────────────────────────

    [Test]
    public void CookHours_And_Units_ScaleWithTier_Clamped()
    {
        Assert.AreEqual(4,  ProcessingMath.CookHoursForTier(1));
        Assert.AreEqual(8,  ProcessingMath.CookHoursForTier(2));
        Assert.AreEqual(12, ProcessingMath.CookHoursForTier(3));
        Assert.AreEqual(4,  ProcessingMath.CookHoursForTier(0));   // clamped up
        Assert.AreEqual(12, ProcessingMath.CookHoursForTier(99));  // clamped down
        // 1 unit per cook-hour (spec §4): jam=4, compote=8, sauce=12
        Assert.AreEqual(4,  ProcessingMath.UnitsRequiredForTier(1));
        Assert.AreEqual(12, ProcessingMath.UnitsRequiredForTier(3));
    }

    [Test]
    public void JarValue_IsUnitsTimesBaseTimesMultiplier_MinOne()
    {
        // strawberry-ish: base 10, tier 1, x2.5 → 10 * 4 * 2.5 = 100
        Assert.AreEqual(100, ProcessingMath.JarValue(10, 1, 2.5f));
        // tier 3: 10 * 12 * 2.8 = 336
        Assert.AreEqual(336, ProcessingMath.JarValue(10, 3, 2.8f));
        // never below 1, never negative-driven
        Assert.AreEqual(1, ProcessingMath.JarValue(0, 1, 2.5f));
        Assert.AreEqual(1, ProcessingMath.JarValue(-5, 1, 2.5f));
    }

    // ── Intake routing ─────────────────────────────────────────────────

    private static CanneryState MakeState(int slots)
    {
        var st = new CanneryState { slots = new CannerySlot[slots] };
        for (int i = 0; i < slots; i++) st.slots[i] = new CannerySlot();
        return st;
    }

    private static void LoadSlot(CannerySlot s, string cropId, int loaded, int required, double cookRemaining = 0)
    {
        s.cropId = cropId; s.cropName = cropId; s.tier = 1;
        s.unitsLoaded = loaded; s.unitsRequired = required;
        s.cookSecondsRemaining = cookRemaining; s.jarValue = 100;
    }

    [Test]
    public void FindIntakeSlot_PrefersPartialSameCrop_ThenEmpty_ElseMinusOne()
    {
        var st = MakeState(3);
        LoadSlot(st.slots[1], "strawberry", 2, 4);            // partial strawberry jar
        Assert.AreEqual(1, ProcessingMath.FindIntakeSlot(st, "strawberry"));
        Assert.AreEqual(0, ProcessingMath.FindIntakeSlot(st, "tomato")); // no partial → first empty

        // full up: slot0 cooking, slot1 full, slot2 partial other crop
        LoadSlot(st.slots[0], "tomato", 4, 4, 3600);
        LoadSlot(st.slots[1], "strawberry", 4, 4, 3600);
        LoadSlot(st.slots[2], "carrot", 1, 4);
        Assert.AreEqual(2, ProcessingMath.FindIntakeSlot(st, "carrot"));      // partial match
        Assert.AreEqual(-1, ProcessingMath.FindIntakeSlot(st, "strawberry")); // its jar is cooking, no empties
    }

    [Test]
    public void SlotIsCooking_RequiresFullLoad_AndRemainingCookTime()
    {
        var s = new CannerySlot();
        Assert.IsFalse(ProcessingMath.SlotIsCooking(s));               // empty
        LoadSlot(s, "tomato", 2, 4);
        Assert.IsFalse(ProcessingMath.SlotIsCooking(s));               // partial → not cooking
        LoadSlot(s, "tomato", 4, 4, 100);
        Assert.IsTrue(ProcessingMath.SlotIsCooking(s));
        s.cookSecondsRemaining = 0;
        Assert.IsFalse(ProcessingMath.SlotIsCooking(s));               // done
    }

    // ── Firebox simulation ─────────────────────────────────────────────

    [Test]
    public void Simulate_CooksAndConsumesFuel_FinishedJarMovesToShelfAndFreesSlot()
    {
        var st = MakeState(2);
        LoadSlot(st.slots[0], "tomato", 4, 4, cookRemaining: 10);
        st.fuelWood = 100;
        int finished = ProcessingMath.Simulate(st, 10, baseWoodPerHour: 0f, perSlotWoodPerHour: 3600f);
        Assert.AreEqual(1, finished);
        Assert.AreEqual(1, st.readyJars.Count);
        Assert.AreEqual(100, st.readyJars[0].value);
        Assert.IsTrue(ProcessingMath.SlotIsEmpty(st.slots[0]));      // slot freed
        Assert.AreEqual(90.0, st.fuelWood, 1e-6);                    // 1 wood/sec × 10s
    }

    [Test]
    public void Simulate_FuelOut_PausesCooking_NothingRuined()
    {
        var st = MakeState(1);
        LoadSlot(st.slots[0], "tomato", 4, 4, cookRemaining: 100);
        st.fuelWood = 30; // only 30s of fire at 1 wood/sec
        int finished = ProcessingMath.Simulate(st, 1000, 0f, 3600f);
        Assert.AreEqual(0, finished);
        Assert.AreEqual(0.0, st.fuelWood, 1e-6);
        Assert.AreEqual(70.0, st.slots[0].cookSecondsRemaining, 1e-6); // paused at 70s left

        // re-stoke and resume: part-fills are fine (spec §2)
        st.fuelWood = 70;
        finished = ProcessingMath.Simulate(st, 70, 0f, 3600f);
        Assert.AreEqual(1, finished);
    }

    [Test]
    public void Simulate_EmptyFire_BurnsBaseRate_AsWaste()
    {
        var st = MakeState(2); // nothing loaded
        st.fuelWood = 10;
        ProcessingMath.Simulate(st, 3600, baseWoodPerHour: 5f, perSlotWoodPerHour: 3600f);
        Assert.AreEqual(5.0, st.fuelWood, 1e-6);  // base 5/h burned for an hour, no progress made
        // and with base 0 + nothing cooking, nothing burns (no infinite loop either)
        var st2 = MakeState(1);
        st2.fuelWood = 10;
        ProcessingMath.Simulate(st2, 3600, 0f, 3600f);
        Assert.AreEqual(10.0, st2.fuelWood, 1e-6);
    }

    [Test]
    public void Simulate_MultiSlot_RateDropsWhenFirstJarFinishes()
    {
        var st = MakeState(2);
        LoadSlot(st.slots[0], "a", 4, 4, cookRemaining: 10);
        LoadSlot(st.slots[1], "b", 4, 4, cookRemaining: 30);
        st.fuelWood = 1000;
        int finished = ProcessingMath.Simulate(st, 30, 0f, 3600f);
        Assert.AreEqual(2, finished);
        // 10s at 2 wood/sec + 20s at 1 wood/sec = 40 wood
        Assert.AreEqual(960.0, st.fuelWood, 1e-6);
    }

    // ── Stoke-to-finish ────────────────────────────────────────────────

    [Test]
    public void WoodToFinishLoaded_ExactPiecewiseNeed_MinusCurrentFuel()
    {
        var st = MakeState(2);
        LoadSlot(st.slots[0], "a", 4, 4, cookRemaining: 10);
        LoadSlot(st.slots[1], "b", 4, 4, cookRemaining: 30);
        st.fuelWood = 15;
        // need 40 (see multi-slot test) minus 15 on hand = 25
        Assert.AreEqual(25.0, ProcessingMath.WoodToFinishLoaded(st, 0f, 3600f), 1e-6);
        // nothing cooking → zero
        var idle = MakeState(2);
        idle.fuelWood = 5;
        Assert.AreEqual(0.0, ProcessingMath.WoodToFinishLoaded(idle, 5f, 3600f), 1e-6);
        // ample fuel → zero (never negative)
        st.fuelWood = 500;
        Assert.AreEqual(0.0, ProcessingMath.WoodToFinishLoaded(st, 0f, 3600f), 1e-6);
    }

    // ── Slot purchase gating ───────────────────────────────────────────

    [Test]
    public void CanBuySlot_RequiresUnderCap_AndBothCurrencies()
    {
        Assert.IsTrue(ProcessingMath.CanBuySlot(4, 20, coins: 150, coinCost: 150, wood: 40, woodCost: 40));
        Assert.IsFalse(ProcessingMath.CanBuySlot(20, 20, 99999, 150, 99999, 40)); // at purchasable cap
        Assert.IsFalse(ProcessingMath.CanBuySlot(4, 20, 149, 150, 99999, 40));    // short coins
        Assert.IsFalse(ProcessingMath.CanBuySlot(4, 20, 99999, 150, 39, 40));     // short wood
    }

    // ── Research-gated cap + efficiency burn (Phase 3) ──────────────────

    [Test]
    public void EffectiveSlotCap_AddsPerExpansion_ClampedToTotal()
    {
        // Cannery: base 20, total 24, +2 per expansion.
        Assert.AreEqual(20, ProcessingMath.EffectiveSlotCap(20, 24, 0, 2));
        Assert.AreEqual(22, ProcessingMath.EffectiveSlotCap(20, 24, 1, 2));
        Assert.AreEqual(24, ProcessingMath.EffectiveSlotCap(20, 24, 2, 2));
        Assert.AreEqual(24, ProcessingMath.EffectiveSlotCap(20, 24, 3, 2)); // clamped to total
        // Smokehouse: base 6, total 8.
        Assert.AreEqual(6, ProcessingMath.EffectiveSlotCap(6, 8, 0, 2));
        Assert.AreEqual(8, ProcessingMath.EffectiveSlotCap(6, 8, 1, 2));
        // Negative expansions treated as zero.
        Assert.AreEqual(20, ProcessingMath.EffectiveSlotCap(20, 24, -5, 2));
    }

    [Test]
    public void EffectiveBurnPerSlot_ReducesByBonus_ClampedToMax()
    {
        Assert.AreEqual(20f, ProcessingMath.EffectiveBurnPerSlot(20f, 0f,   0.4f), 1e-4f); // no bonus
        Assert.AreEqual(18f, ProcessingMath.EffectiveBurnPerSlot(20f, 0.1f, 0.4f), 1e-4f); // 10% off
        Assert.AreEqual(12f, ProcessingMath.EffectiveBurnPerSlot(20f, 0.9f, 0.4f), 1e-4f); // capped at 40% off
        Assert.AreEqual(20f, ProcessingMath.EffectiveBurnPerSlot(20f, -1f,  0.4f), 1e-4f); // negative bonus ignored
    }

    // ── Finished good routing (Phase 2: fish tier) ──────────────────────

    [Test]
    public void Simulate_FinishedGood_CarriesTierAndSourceId()
    {
        var st = MakeState(1);
        LoadSlot(st.slots[0], "fish_perch", 1, 1, cookRemaining: 10);
        st.slots[0].tier = 2;                 // pretend a Bass
        st.slots[0].jarValue = 1400;
        st.fuelWood = 100;
        int finished = ProcessingMath.Simulate(st, 10, baseWoodPerHour: 0f, perSlotWoodPerHour: 3600f);
        Assert.AreEqual(1, finished);
        Assert.AreEqual(1, st.readyJars.Count);
        Assert.AreEqual(2, st.readyJars[0].tier);
        Assert.AreEqual("fish_perch", st.readyJars[0].sourceId);
        Assert.AreEqual(1400, st.readyJars[0].value);
    }
}
