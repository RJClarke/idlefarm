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

    // ---- regrowing crops ----
    private static OfflineSimContext NoLossCtx(float regrowSeconds)
    {
        var t = new OfflineSimTuning { deerPlantsPerHungerSecond = 0f, crowPlantsPerHungerSecond = 0f,
                                       dryFractionPerSecond = 0f, lightningPlantsPerStrike = 0f };
        var crop = FastCheapCrop(); crop.regrowSeconds = regrowSeconds;
        return new OfflineSimContext {
            awaySeconds = 600f, startFarmSeconds = 0f, startMoney = 1000, maxGameSpeed = 1f,
            zones = new List<SimZone> { new SimZone { crop = crop, tileCount = 4 } }, tuning = t
        };
    }

    [Test]
    public void Simulate_Regrower_PlantsOnceAndKeepsHarvesting()
    {
        var r = OfflineRunSimulator.Simulate(NoLossCtx(10f));
        Assert.AreEqual(4, r.seedsPlanted, "a regrowing plant stays in the ground after a harvest");
        Assert.Greater(r.TotalHarvested, 4 * 10, "grows back in 10s instead of the full 30s");
    }

    [Test]
    public void Simulate_Regrower_OutHarvestsReplantedCrop()
    {
        var regrow = OfflineRunSimulator.Simulate(NoLossCtx(10f));
        var replant = OfflineRunSimulator.Simulate(NoLossCtx(0f));
        Assert.Greater(replant.seedsPlanted, 4);
        Assert.Greater(regrow.TotalHarvested, replant.TotalHarvested);
    }

    // ---- mechanics added since v1: per-crop pests/thirst, per-field compost, Cannery + Collect ----
    private static OfflineSimContext Quiet(float away = 600f)
    {
        var ctx = NoLossCtx(0f);
        ctx.awaySeconds = away;
        return ctx;
    }

    [Test]
    public void Simulate_SellsEveryHarvest()
    {
        var r = OfflineRunSimulator.Simulate(Quiet());
        Assert.Greater(r.TotalHarvested, 0);
        Assert.AreEqual(r.TotalHarvested * 20, r.moneyEarned, "away-runs never collect or can - it all sells");
    }

    // ---- the equipped animal while away ----
    [Test]
    public void Simulate_NoAnimal_DoesNothing()
    {
        var r = OfflineRunSimulator.Simulate(Ctx(3600f, 100000));
        Assert.AreEqual(0, r.animalDeerChased + r.animalCrowsChased + r.animalPlantsEaten + r.animalCompost);
    }

    [Test]
    public void Simulate_DeerChaser_ChasesOncePerRest()
    {
        var ctx = Ctx(600f, 100000); // deer from wave 1 (farm second 0); no crows before wave 10
        ctx.animal = new SimAnimal { chaseCooldownSeconds = 60f, chasesDeer = true };
        var r = OfflineRunSimulator.Simulate(ctx);
        Assert.AreEqual(10, r.animalDeerChased, 1, "one chase per 60s rest over 600s");
        Assert.AreEqual(0, r.animalCrowsChased);
    }

    [Test]
    public void Simulate_CrowChaser_WaitsForCrows()
    {
        var ctx = Ctx(600f, 100000); // crows only arrive at wave 10 (farm second 540)
        ctx.animal = new SimAnimal { chaseCooldownSeconds = 30f, chasesCrows = true };
        var r = OfflineRunSimulator.Simulate(ctx);
        Assert.AreEqual(0, r.animalDeerChased);
        Assert.LessOrEqual(r.animalCrowsChased, 2, "only the last minute has crows");
    }

    [Test]
    public void Simulate_BothPestsChaser_SplitsChasesBetweenThem()
    {
        var ctx = Ctx(3600f, 100000); // crows join the deer from wave 10
        ctx.animal = new SimAnimal { chaseCooldownSeconds = 18f, chasesDeer = true, chasesCrows = true };
        var r = OfflineRunSimulator.Simulate(ctx);
        Assert.Greater(r.animalDeerChased, 0);
        Assert.Greater(r.animalCrowsChased, 0, "crows get chased too once they arrive");
    }

    [Test]
    public void Simulate_Grazer_EatsRipeCropsForCompost()
    {
        var ctx = Quiet(1200f);
        ctx.animal = new SimAnimal { eatIntervalSeconds = 60f, compostPerEat = 15 };
        var withCow = OfflineRunSimulator.Simulate(ctx);
        var without = OfflineRunSimulator.Simulate(Quiet(1200f));
        Assert.Greater(withCow.animalPlantsEaten, 0);
        Assert.AreEqual(withCow.animalPlantsEaten * 15, withCow.animalCompost);
        Assert.AreEqual(withCow.animalCompost, withCow.compostGained, "cow compost is granted with the run's");
        Assert.Less(withCow.TotalHarvested, without.TotalHarvested, "eaten crops aren't harvested");
    }

    // SimZone/SimCrop are structs held in a list: change a copy, then write it back.
    private static OfflineSimContext Tweak(OfflineSimContext ctx, System.Func<SimZone, SimZone> f)
    {
        ctx.zones[0] = f(ctx.zones[0]);
        return ctx;
    }

    private static OfflineSimContext Losses(float away, float? deer = null, float? crow = null, float? dry = null) =>
        Tweak(Ctx(away, 100000), z => { var c = z.crop; c.deerLoss = deer; c.crowLoss = crow; c.dryLoss = dry; z.crop = c; return z; });

    [Test]
    public void Simulate_DeerLoveACrop_LosesMoreOfIt()
    {
        Assert.Greater(OfflineRunSimulator.Simulate(Losses(3600f, deer: 2f)).eatenByDeer,
                       OfflineRunSimulator.Simulate(Losses(3600f, deer: 0.25f)).eatenByDeer);
    }

    [Test]
    public void Simulate_CrowsLoveACrop_LosesMoreOfIt()
    {
        Assert.Greater(OfflineRunSimulator.Simulate(Losses(7200f, crow: 2f)).eatenByCrows,
                       OfflineRunSimulator.Simulate(Losses(7200f, crow: 0.25f)).eatenByCrows);
    }

    [Test]
    public void Simulate_ThirstyCrop_DriesUpMore()
    {
        Assert.Greater(OfflineRunSimulator.Simulate(Losses(3600f, dry: 1.5f)).driedUp,
                       OfflineRunSimulator.Simulate(Losses(3600f, dry: 0.5f)).driedUp);
    }

    [Test]
    public void Simulate_CompostOnlyFromFieldsWithABay()
    {
        var noBay = Tweak(Ctx(3600f, 100000), z => { z.compostPerLoss = 0; return z; });
        var bay = Tweak(Ctx(3600f, 100000), z => { z.compostPerLoss = 4; return z; });
        var rNo = OfflineRunSimulator.Simulate(noBay);
        var rBay = OfflineRunSimulator.Simulate(bay);
        Assert.Greater(rNo.eatenByDeer, 0, "plants were still lost");
        Assert.AreEqual(0, rNo.compostGained);
        int lost = rBay.eatenByDeer + rBay.eatenByCrows + rBay.struckByLightning + rBay.driedUp + rBay.rotted;
        Assert.AreEqual(lost * 4, rBay.compostGained);
    }
}
