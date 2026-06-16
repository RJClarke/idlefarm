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

    // ---- helpers to build contexts ----
    private static SimCrop FastCheapCrop() => new SimCrop {
        id = "Carrot", growSeconds = 30f, harvestWindowSeconds = 60f,
        harvestValue = 20, coinValue = 1, bagBaseCost = 10, bagSize = 20, tier = 1
    };

    private static OfflineSimContext Ctx(float away, int money, OfflineSimTuning t = null)
    {
        t = t ?? new OfflineSimTuning();
        return new OfflineSimContext {
            awaySeconds = away, startFarmSeconds = 0f, startMoney = money, maxGameSpeed = 1f,
            zones = new List<SimZone> { new SimZone { crop = FastCheapCrop(), tileCount = 4 } },
            tuning = t
        };
    }

    [Test]
    public void Simulate_ZeroAway_ProducesEmptyResult()
    {
        var r = OfflineRunSimulator.Simulate(Ctx(0f, 1000));
        Assert.AreEqual(0, r.TotalHarvested);
        Assert.IsFalse(r.bankrupt);
    }

    [Test]
    public void Simulate_SolventLongRun_HarvestsAndStaysSolvent()
    {
        // cheap bags + good value + plenty of start money -> never bankrupt, harvests accrue.
        // startFarm=0, maxSpeed=1, away=3600 -> finalFarmSeconds should land on 3600.
        var r = OfflineRunSimulator.Simulate(Ctx(3600f, 100000));
        Assert.Greater(r.TotalHarvested, 0);
        Assert.IsFalse(r.bankrupt);
        Assert.AreEqual(3600f, r.finalFarmSeconds, 0.5f);
    }

    [Test]
    public void Simulate_NoMoney_GoesBankrupt()
    {
        // can't afford the first bag -> nothing ever plants -> bankrupt after first tick
        var r = OfflineRunSimulator.Simulate(Ctx(3600f, 0));
        Assert.IsTrue(r.bankrupt);
        Assert.Less(r.finalFarmSeconds, 3600f);
    }

    [Test]
    public void Simulate_DeerCauseAttributed_WhenNoMitigation()
    {
        var r = OfflineRunSimulator.Simulate(Ctx(3600f, 100000));
        Assert.Greater(r.eatenByDeer, 0);            // deer active from wave 1
        // a 3600s window crosses the wave-25 storm (1500s), so lightning losses are expected too
        Assert.Greater(r.struckByLightning, 0);
    }

    [Test]
    public void Simulate_FenceReducesDeerLosses()
    {
        var baseR = OfflineRunSimulator.Simulate(Ctx(3600f, 100000));
        var ctx = Ctx(3600f, 100000); ctx.deerLossReduction = 0.8f;
        var mitR = OfflineRunSimulator.Simulate(ctx);
        Assert.Less(mitR.eatenByDeer, baseR.eatenByDeer);
    }

    [Test]
    public void Simulate_IsDeterministic()
    {
        var a = OfflineRunSimulator.Simulate(Ctx(1800f, 5000));
        var b = OfflineRunSimulator.Simulate(Ctx(1800f, 5000));
        Assert.AreEqual(a.TotalHarvested, b.TotalHarvested);
        Assert.AreEqual(a.eatenByDeer, b.eatenByDeer);
        Assert.AreEqual(a.bankrupt, b.bankrupt);
        Assert.AreEqual(a.finalMoney, b.finalMoney);
    }
}
