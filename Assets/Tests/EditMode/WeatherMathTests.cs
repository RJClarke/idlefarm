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
    public void RollCasual_PartitionsByWeight()
    {
        Assert.AreEqual(0, WeatherMath.RollCasual(0.0f, 2f, 1f, 1f));
        Assert.AreEqual(0, WeatherMath.RollCasual(0.49f, 2f, 1f, 1f));
        Assert.AreEqual(1, WeatherMath.RollCasual(0.60f, 2f, 1f, 1f));
        Assert.AreEqual(2, WeatherMath.RollCasual(0.90f, 2f, 1f, 1f));
    }
}
