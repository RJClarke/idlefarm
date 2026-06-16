using NUnit.Framework;
using System.Collections.Generic;

public class OfflineRunSimulatorTests
{
    private static OfflineSimTuning T() => new OfflineSimTuning();

    // ---- wave / count helpers ----
    [Test] public void Wave_AtZero_IsOne()        => Assert.AreEqual(1, OfflineRunSimulator.WaveAt(0f, T()));
    [Test] public void Wave_At90s_IsTwo()          => Assert.AreEqual(2, OfflineRunSimulator.WaveAt(90f, T()));
    [Test] public void Deer_StartsAtWaveOne()      => Assert.AreEqual(1, OfflineRunSimulator.DeerCount(1, T()));
    [Test] public void Deer_CapsAtMax()            => Assert.AreEqual(6, OfflineRunSimulator.DeerCount(1000, T()));
    [Test] public void Crows_NoneBeforeWave10()    => Assert.AreEqual(0, OfflineRunSimulator.CrowCount(9, T()));
    [Test] public void Crows_OneAtWave10()         => Assert.AreEqual(1, OfflineRunSimulator.CrowCount(10, T()));

    // ---- lightning window ----
    [Test]
    public void Lightning_ActiveInsideFirstStormWindow()
    {
        var t = T(); // storm at wave 25 -> farm time >= 25*60 = 1500s, window 30s
        Assert.IsTrue(OfflineRunSimulator.LightningActiveAt(1500f, t));
        Assert.IsFalse(OfflineRunSimulator.LightningActiveAt(1490f, t));
        Assert.IsFalse(OfflineRunSimulator.LightningActiveAt(1600f, t));
    }
}
