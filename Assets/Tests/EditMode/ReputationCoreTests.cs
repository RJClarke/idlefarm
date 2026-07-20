using NUnit.Framework;

public class ReputationCoreTests
{
    private static DeliveryRequest MakeRequest(string itemId = "Blueberry", int count = 10, int reward = 30)
        => new DeliveryRequest
        {
            items = new[] { new DeliveryLineItem { itemId = itemId, count = count } },
            repReward = reward,
            requesterName = "Marta",
            flavorText = "",
        };

    [Test]
    public void AddRep_AwardsPointsViaReputationMath()
    {
        var core = new ReputationCore();
        int awarded = core.AddRep(100); // exactly point 1's cost
        Assert.AreEqual(1, awarded);
        Assert.AreEqual(1, core.PointsEarned);
        Assert.AreEqual(1, core.UnspentPoints);
        Assert.AreEqual(0, core.BarProgress);
    }

    [Test]
    public void SetSlotRequest_ThenGetSlotRequest_RoundTrips()
    {
        var core = new ReputationCore();
        var req = MakeRequest();
        core.SetSlotRequest(0, req);
        Assert.AreEqual(req, core.GetSlotRequest(0));
        Assert.IsFalse(core.IsSlotOnCooldown(0, System.DateTime.UtcNow.Ticks));
    }

    [Test]
    public void OnFulfilled_ClearsRequestStartsCooldownResetsSkips()
    {
        var core = new ReputationCore();
        core.SetSlotRequest(1, MakeRequest());
        core.OnSkipped();
        core.OnSkipped();
        Assert.AreEqual(2, core.ConsecutiveSkips);

        long now = 1_000_000L;
        long cooldownTicks = System.TimeSpan.FromHours(8).Ticks;
        core.OnFulfilled(1, now, cooldownTicks);

        Assert.IsNull(core.GetSlotRequest(1));
        Assert.AreEqual(0, core.ConsecutiveSkips);
        Assert.IsTrue(core.IsSlotOnCooldown(1, now));
        Assert.IsFalse(core.IsSlotOnCooldown(1, now + cooldownTicks + 1));
    }

    [Test]
    public void OnSkipped_IncrementsAndDrivesNextSkipCost()
    {
        var core = new ReputationCore();
        Assert.AreEqual(25, core.NextSkipCost());
        core.OnSkipped();
        Assert.AreEqual(50, core.NextSkipCost());
        core.OnSkipped();
        Assert.AreEqual(100, core.NextSkipCost());
    }

    [Test]
    public void ExportRequests_NeverNull_EmptySlotBecomesSentinel()
    {
        var core = new ReputationCore();
        var exported = core.ExportRequests();
        Assert.AreEqual(3, exported.Length);
        for (int i = 0; i < 3; i++)
        {
            Assert.IsNotNull(exported[i]);
            Assert.IsNotNull(exported[i].items);
            Assert.AreEqual(0, exported[i].items.Length);
        }
    }

    [Test]
    public void Import_SentinelEmptyRequest_BecomesNullInMemory()
    {
        var core = new ReputationCore();
        var sentinel = new DeliveryRequest { items = new DeliveryLineItem[0], repReward = 0, requesterName = "", flavorText = "" };
        core.Import(0, 0, 0, 0, new[] { sentinel, sentinel, sentinel }, new long[] { 0, 0, 0 });
        Assert.IsNull(core.GetSlotRequest(0));
    }

    [Test]
    public void ExportImport_RoundTripsActiveRequestAndCooldown()
    {
        var core = new ReputationCore();
        var req = MakeRequest("Tomato", 5, 60);
        core.SetSlotRequest(2, req);
        core.AddRep(250);

        var requests = core.ExportRequests();
        var cooldowns = core.ExportCooldowns();

        var core2 = new ReputationCore();
        core2.Import(core.BarProgress, core.PointsEarned, core.UnspentPoints, core.ConsecutiveSkips, requests, cooldowns);

        Assert.AreEqual("Tomato", core2.GetSlotRequest(2).items[0].itemId);
        Assert.AreEqual(5, core2.GetSlotRequest(2).items[0].count);
        Assert.AreEqual(core.PointsEarned, core2.PointsEarned);
        Assert.AreEqual(core.UnspentPoints, core2.UnspentPoints);
    }

    [Test]
    public void Import_NullArrays_IsSafeAndEmpty()
    {
        var core = new ReputationCore();
        core.Import(0, 0, 0, 0, null, null);
        for (int i = 0; i < 3; i++)
        {
            Assert.IsNull(core.GetSlotRequest(i));
            Assert.IsFalse(core.IsSlotOnCooldown(i, System.DateTime.UtcNow.Ticks));
        }
    }
}
