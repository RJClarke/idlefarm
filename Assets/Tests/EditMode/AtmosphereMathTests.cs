using NUnit.Framework;

public class AtmosphereMathTests
{
    [Test]
    public void EaseIntensity_MovesTowardTargetByLerpSpeedTimesDt()
    {
        // 0 -> 1 target, dt 0.5, speed 1  => +0.5
        Assert.AreEqual(0.5f, AtmosphereMath.EaseIntensity(0f, 1f, 0.5f, 1f), 1e-4f);
    }

    [Test]
    public void EaseIntensity_DoesNotOvershootTarget()
    {
        Assert.AreEqual(1f, AtmosphereMath.EaseIntensity(0.9f, 1f, 1f, 5f), 1e-4f);
    }

    [Test]
    public void DriftSpeed_ScalesWithWindAndStormIntensity()
    {
        // base 2, wind 1, intensity 0 => 2
        Assert.AreEqual(2f, AtmosphereMath.DriftSpeed(2f, 1f, 0f, 1.5f), 1e-4f);
        // intensity 1, stormMul 1.5 => 2 * (1 + 1.5) = 5
        Assert.AreEqual(5f, AtmosphereMath.DriftSpeed(2f, 1f, 1f, 1.5f), 1e-4f);
    }

    [Test]
    public void EmissionRate_ScalesWithWindAndStorm()
    {
        Assert.AreEqual(10f, AtmosphereMath.EmissionRate(10f, 1f, 0f, 2f), 1e-4f);
        Assert.AreEqual(30f, AtmosphereMath.EmissionRate(10f, 1f, 1f, 2f), 1e-4f); // 10*(1+2)
    }

    [Test]
    public void SpawnEdgeX_PlacesPatchJustOffUpwindEdge()
    {
        // wind blows left (-1): patches enter from the RIGHT edge.
        // cam 0, halfWidth 10, patchHalf 2 => right edge 10, plus patchHalf => 12
        Assert.AreEqual(12f, AtmosphereMath.SpawnEdgeX(0f, 10f, 2f, -1f), 1e-4f);
        // wind blows right (+1): enter from LEFT => -12
        Assert.AreEqual(-12f, AtmosphereMath.SpawnEdgeX(0f, 10f, 2f, 1f), 1e-4f);
    }

    [Test]
    public void IsPatchOffscreen_TrueOnlyAfterFullyPastDownwindEdge()
    {
        // wind left (-1): downwind edge is the LEFT (-10). Patch fully off when right side < -10,
        // i.e. patchX + patchHalf < -10  => patchX < -12.
        Assert.IsFalse(AtmosphereMath.IsPatchOffscreen(-11f, 2f, 0f, 10f, -1f)); // right side -9, still visible
        Assert.IsTrue (AtmosphereMath.IsPatchOffscreen(-13f, 2f, 0f, 10f, -1f)); // right side -11, gone
    }
}
