using NUnit.Framework;
using System.Collections.Generic;

public class OfflineZoneStatsTests
{
    private static SimCrop Crop(string id) => new SimCrop {
        id = id, growSeconds = 30f, harvestWindowSeconds = 60f,
        harvestValue = 20, coinValue = 2, bagBaseCost = 10, bagSize = 20, tier = 1
    };

    private static OfflineSimContext TwoZoneCtx()
    {
        return new OfflineSimContext {
            awaySeconds = 3600f, startFarmSeconds = 0f, startMoney = 100000, maxGameSpeed = 1f,
            zones = new List<SimZone> {
                new SimZone { crop = Crop("Strawberry"), tileCount = 5, zoneId = 1 },
                new SimZone { crop = Crop("Blueberry"),  tileCount = 0, zoneId = 3 },
            },
            tuning = new OfflineSimTuning()
        };
    }

    [Test]
    public void Zones_CarryIdAndCrop()
    {
        var r = OfflineRunSimulator.Simulate(TwoZoneCtx());
        Assert.AreEqual(2, r.zones.Count);
        Assert.AreEqual(1, r.zones[0].zoneId);
        Assert.AreEqual("Strawberry", r.zones[0].cropId);
        Assert.AreEqual(3, r.zones[1].zoneId);
        Assert.AreEqual("Blueberry", r.zones[1].cropId);
    }

    [Test]
    public void EmptyZone_GetsNothing_ActiveZoneGetsEverything()
    {
        var r = OfflineRunSimulator.Simulate(TwoZoneCtx());
        Assert.Greater(r.zones[0].harvested, 0);
        Assert.Greater(r.zones[0].eatenByDeer, 0);
        Assert.AreEqual(0, r.zones[1].harvested);
        Assert.AreEqual(0, r.zones[1].eatenByDeer + r.zones[1].eatenByCrows
            + r.zones[1].struckByLightning + r.zones[1].driedUp + r.zones[1].rotted);
    }

    [Test]
    public void Totals_EqualZoneSums()
    {
        var r = OfflineRunSimulator.Simulate(TwoZoneCtx());
        int h = 0, money = 0, coins = 0, deer = 0, crow = 0, light = 0, dry = 0, rot = 0;
        foreach (var z in r.zones)
        {
            h += z.harvested; money += z.moneyEarned; coins += z.coinsBanked;
            deer += z.eatenByDeer; crow += z.eatenByCrows; light += z.struckByLightning;
            dry += z.driedUp; rot += z.rotted;
        }
        Assert.AreEqual(r.TotalHarvested, h);
        Assert.AreEqual(r.moneyEarned, money);
        Assert.AreEqual(r.coinsBanked, coins);
        Assert.AreEqual(r.eatenByDeer, deer);
        Assert.AreEqual(r.eatenByCrows, crow);
        Assert.AreEqual(r.struckByLightning, light);
        Assert.AreEqual(r.driedUp, dry);
        Assert.AreEqual(r.rotted, rot);
    }
}
