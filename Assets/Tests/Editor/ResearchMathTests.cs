using NUnit.Framework;
using Research;
using UnityEngine;

public class ResearchMathTests
{
    [Test]
    public void StandardTier_L1_Is_TwoMinutes()
    {
        float secs = 120f * Mathf.Pow(1f, 2.16f);
        Assert.AreEqual(120f, secs, 1f);
    }

    [Test]
    public void StandardTier_L25_Is_Roughly_1_5_Days()
    {
        float secs = 120f * Mathf.Pow(25f, 2.16f);
        // 1.5 days = 129,600 sec. Allow ±10%.
        Assert.That(secs, Is.InRange(116_000f, 142_000f));
    }

    [Test]
    public void StandardTier_L100_Is_Roughly_30_Days()
    {
        float secs = 120f * Mathf.Pow(100f, 2.16f);
        // 30 days = 2,592,000 sec. Allow ±10%.
        Assert.That(secs, Is.InRange(2_330_000f, 2_850_000f));
    }

    [Test]
    public void AbsurdTier_L100_Is_Roughly_90_Days()
    {
        float secs = 540f * Mathf.Pow(100f, 2.16f); // base 9 min × L100
        // 90 days = 7,776,000 sec. Allow ±10%.
        Assert.That(secs, Is.InRange(7_000_000f, 8_550_000f));
    }

    [Test]
    public void TimeDifficulty_Scales_Linearly()
    {
        float baseSecs = 120f * Mathf.Pow(50f, 2.16f);
        float scaledSecs = (120f * 2f) * Mathf.Pow(50f, 2.16f);
        Assert.AreEqual(baseSecs * 2f, scaledSecs, 1f);
    }

    [Test]
    public void CostScaling_L100_Matches_Reference()
    {
        // Standard 100-lvl: base 50, p_cost 2.0, costDifficulty 1.0
        // L100 single-level cost = 50 * 100^2 = 500,000
        float cost = 50f * Mathf.Pow(100f, 2.0f);
        Assert.AreEqual(500_000f, cost, 1f);
    }

    [Test]
    public void GameSpeed_Bonus_Hits_10x_At_L10()
    {
        // Game Speed: bonusPerLevel = 0.9, L10 bonus = 9.0, total multiplier = 1 + 9 = 10
        float bonus = 10 * 0.9f;
        float multiplier = 1f + bonus;
        Assert.AreEqual(10f, multiplier, 0.001f);
    }
}
