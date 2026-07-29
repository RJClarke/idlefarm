using NUnit.Framework;

public class WeatherMathTests
{
    [Test]
    public void EaseChannel_MovesTowardTarget_NoOvershoot()
    {
        Assert.AreEqual(0.5f, WeatherMath.EaseChannel(0f, 1f, 0.5f, 1f), 1e-4f);
        Assert.AreEqual(1f,   WeatherMath.EaseChannel(0.9f, 1f, 1f, 5f), 1e-4f);
    }

    [Test]
    public void StormSeverity_RisesWithStormNumber_ClampedTo1()
    {
        Assert.AreEqual(0.2f, WeatherMath.StormSeverity(1, 5f), 1e-4f);
        Assert.AreEqual(1.0f, WeatherMath.StormSeverity(5, 5f), 1e-4f);
        Assert.AreEqual(1.0f, WeatherMath.StormSeverity(9, 5f), 1e-4f);
    }

    [Test]
    public void RainAngle_ZeroWhenCalm_RisesWithSeverity_Capped()
    {
        Assert.AreEqual(0f,  WeatherMath.RainAngleDegrees(0f, 0f, 80f), 1e-4f);
        Assert.AreEqual(40f, WeatherMath.RainAngleDegrees(0f, 0.5f, 80f), 1e-4f);
        Assert.AreEqual(80f, WeatherMath.RainAngleDegrees(1f, 1f, 80f), 1e-4f);
    }

    [Test]
    public void DebrisAngle_LiesDownWithWind_WhenDry()
    {
        // calm 35 -> windy 68 as the wind rises, with no rain falling.
        Assert.AreEqual(35f,   WeatherMath.DebrisAngleDegrees(0f,   0f, 0f, 35f, 68f, 10f), 1e-3f);
        Assert.AreEqual(51.5f, WeatherMath.DebrisAngleDegrees(0.5f, 0f, 0f, 35f, 68f, 10f), 1e-3f);
        Assert.AreEqual(68f,   WeatherMath.DebrisAngleDegrees(1f,   0f, 0f, 35f, 68f, 10f), 1e-3f);
    }

    [Test]
    public void DebrisAngle_SwingsToRainAngle_WhilePrecipitating()
    {
        // Full rain at a 20-deg rain angle pulls the leaves to 20 + 10 offset, NOT the windy 68.
        Assert.AreEqual(30f, WeatherMath.DebrisAngleDegrees(1f, 1f, 20f, 35f, 68f, 10f), 1e-3f);
        // Half rain sits halfway between the dry angle and the rain-matched one.
        Assert.AreEqual(49f, WeatherMath.DebrisAngleDegrees(1f, 0.5f, 20f, 35f, 68f, 10f), 1e-3f);
    }

    [Test]
    public void DebrisAngle_NeverGoesFullyHorizontal()
    {
        Assert.LessOrEqual(WeatherMath.DebrisAngleDegrees(1f, 1f, 89f, 35f, 68f, 30f), 89f);
    }

    [Test]
    public void DebrisSpeed_RisesWithWind_ButIsDampedByRain()
    {
        Assert.AreEqual(5f,  WeatherMath.DebrisSpeed(0f, 0f, 5f, 18f, 0.5f), 1e-3f);
        Assert.AreEqual(18f, WeatherMath.DebrisSpeed(1f, 0f, 5f, 18f, 0.5f), 1e-3f);
        Assert.AreEqual(9f,  WeatherMath.DebrisSpeed(1f, 1f, 5f, 18f, 0.5f), 1e-3f); // full rain halves it
    }

    [Test]
    public void RollWindDirection_SplitsOnLeftChance()
    {
        Assert.AreEqual(-1f, WeatherMath.RollWindDirection(0.10f, 0.5f), 1e-4f);
        Assert.AreEqual( 1f, WeatherMath.RollWindDirection(0.90f, 0.5f), 1e-4f);
        Assert.AreEqual( 1f, WeatherMath.RollWindDirection(0.10f, 0f),   1e-4f); // never left
        Assert.AreEqual(-1f, WeatherMath.RollWindDirection(0.99f, 1f),   1e-4f); // always left
    }

    [Test]
    public void StormDarkness_ZeroWhenDry_DeepensWithSeverity()
    {
        Assert.AreEqual(0f,    WeatherMath.StormDarkness(0f, 1f, 0.5f, 0.22f), 1e-4f);
        Assert.AreEqual(0.5f,  WeatherMath.StormDarkness(1f, 0f, 0.5f, 0.22f), 1e-4f);
        Assert.AreEqual(0.72f, WeatherMath.StormDarkness(1f, 1f, 0.5f, 0.22f), 1e-4f);
        Assert.AreEqual(1f,    WeatherMath.StormDarkness(1f, 1f, 0.9f, 0.5f),  1e-4f); // clamped
    }

    [Test]
    public void RollCasual_PartitionsByWeight()
    {
        Assert.AreEqual(0, WeatherMath.RollCasual(0.0f, 2f, 1f, 1f));
        Assert.AreEqual(0, WeatherMath.RollCasual(0.49f, 2f, 1f, 1f));
        Assert.AreEqual(1, WeatherMath.RollCasual(0.60f, 2f, 1f, 1f));
        Assert.AreEqual(2, WeatherMath.RollCasual(0.90f, 2f, 1f, 1f));
    }
}
