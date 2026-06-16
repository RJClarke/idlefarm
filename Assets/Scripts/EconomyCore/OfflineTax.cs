using UnityEngine;

/// <summary>
/// Offline-inefficiency tax. Applied to PAYOUT ONLY (final coins + resume-money), never inside the
/// simulator, so survival/bankruptcy timing matches the live game. Compost is never taxed.
///
/// The base rate (30%) is reduced by the caller's `offline_efficiency` research bonus, so upgrading
/// that research gives a direct offline benefit. The effective rate is clamped to [0, BaseRate].
/// </summary>
public static class OfflineTax
{
    public const float BaseRate = 0.30f;

    /// <summary>Effective tax rate after applying the offline-efficiency research bonus (clamped 0..BaseRate).</summary>
    public static float EffectiveRate(float offlineEfficiencyBonus)
        => Mathf.Clamp(BaseRate - Mathf.Max(0f, offlineEfficiencyBonus), 0f, BaseRate);

    /// <summary>Returns the kept amount after the offline tax (floored).</summary>
    public static int Payout(int gross, float offlineEfficiencyBonus = 0f)
        => Mathf.FloorToInt(Mathf.Max(0, gross) * (1f - EffectiveRate(offlineEfficiencyBonus)));
}
