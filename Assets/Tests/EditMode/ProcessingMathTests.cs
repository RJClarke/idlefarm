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

    // ── Slot purchase gating ───────────────────────────────────────────

    [Test]
    public void CanBuySlot_RequiresUnderCap_AndBothCurrencies()
    {
        Assert.IsTrue(ProcessingMath.CanBuySlot(4, 20, coins: 150, coinCost: 150, wood: 40, woodCost: 40));
        Assert.IsFalse(ProcessingMath.CanBuySlot(20, 20, 99999, 150, 99999, 40)); // at purchasable cap
        Assert.IsFalse(ProcessingMath.CanBuySlot(4, 20, 149, 150, 99999, 40));    // short coins
        Assert.IsFalse(ProcessingMath.CanBuySlot(4, 20, 99999, 150, 39, 40));     // short wood
    }
}
