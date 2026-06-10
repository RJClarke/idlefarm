using UnityEngine;

/// <summary>
/// Pure, Unity-light economic math for the seed-bag system. No singletons, no state —
/// so it's unit-testable. Tunable constants live here.
///
/// Lives in its own asmdef (IdleFarm.EconomyCore) so the EditMode test assembly can
/// reference it. Assembly-CSharp auto-references it, so the MonoBehaviour managers see it too.
/// </summary>
public static class SeedEconomy
{
    /// <summary>Fractional bag-cost increase per minute of run time (0.15 = +15%/min).</summary>
    public const float EscalationPerMinute = 0.15f;

    /// <summary>
    /// Cost (money) of one seed bag. Escalates with run time, then discount is applied.
    /// </summary>
    public static int BagCost(int baseCost, float runMinutes, float discountBonus)
    {
        float escalated = baseCost * (1f + EscalationPerMinute * Mathf.Max(0f, runMinutes));
        float discounted = escalated * (1f - Mathf.Clamp01(discountBonus));
        return Mathf.Max(1, Mathf.RoundToInt(discounted));
    }

    /// <summary>Seeds delivered by one bag, scaled by a size bonus and floored.</summary>
    public static int BagSize(int baseSize, float sizeBonus)
    {
        return Mathf.Max(1, Mathf.FloorToInt(baseSize * (1f + sizeBonus)));
    }
}
