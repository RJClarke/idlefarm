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
