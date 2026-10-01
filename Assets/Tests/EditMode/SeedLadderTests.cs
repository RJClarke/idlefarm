using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public class SeedLadderTests
{
    private static readonly string[] Expected =
        { "Radish", "Carrot", "Green Beans", "Tomato", "Corn", "Green Pepper", "Red Pepper", "Strawberry", "Blueberry" };

    [Test]
    public void Order_IsTheFixedLadder()
    {
        CollectionAssert.AreEqual(Expected, SeedLadder.Order.ToArray());
    }

    [Test]
    public void Ladder_HasExactlyOneStarter_Radish()
    {
        var starters = SeedLadder.Entries.Where(e => e.starter).Select(e => e.cropName).ToArray();
        CollectionAssert.AreEqual(new[] { "Radish" }, starters);
    }

    [Test]
    public void Ladder_PricesAndFlags_MatchSpec()
    {
        var byName = SeedLadder.Entries.ToDictionary(e => e.cropName);
        Assert.AreEqual(10, byName["Carrot"].cost);
        Assert.AreEqual(250, byName["Green Beans"].cost);
        Assert.AreEqual(500, byName["Tomato"].cost);
        Assert.AreEqual(1000, byName["Corn"].cost);
        Assert.AreEqual(1500, byName["Green Pepper"].cost);
        Assert.AreEqual(2500, byName["Strawberry"].cost);
        Assert.AreEqual(5000, byName["Blueberry"].cost);
        Assert.AreEqual(1500, byName["Red Pepper"].cost);
        Assert.AreEqual("composting_basics", byName["Corn"].featureFlag);
        Assert.AreEqual("cannery_unlocked", byName["Strawberry"].featureFlag);
        Assert.AreEqual(2f, byName["Corn"].compostMultiplier);
    }

    [Test]
    public void Ladder_IsCheapestToMostExpensive()
    {
        var costs = SeedLadder.Entries.Select(e => e.cost).ToArray();
        CollectionAssert.IsOrdered(costs);
    }

    // 1st packet = the crop's price; 2nd = ten times that; then it doubles. The free starter's 2nd costs 10.
    [TestCase(10,    1, 10)]
    [TestCase(10,    2, 100)]
    [TestCase(10,    3, 200)]
    [TestCase(10,    4, 400)]
    [TestCase(1000,  2, 10000)]
    [TestCase(1000,  4, 40000)]
    [TestCase(0,     1, 0)]
    [TestCase(0,     2, 10)]
    [TestCase(0,     4, 40)]
    public void PacketPrice_FollowsTheRule(int firstPrice, int packetNumber, int expected)
    {
        Assert.AreEqual(expected, SeedShopRules.PacketPrice(firstPrice, packetNumber));
    }

    [TestCase(1, 1, false)] // one packet, one field: nothing to add
    [TestCase(1, 2, true)]
    [TestCase(2, 2, false)] // never more packets than fields
    [TestCase(3, 4, true)]
    [TestCase(4, 4, false)]
    [TestCase(0, 2, false)] // extra packets are for crops you own
    public void CanAddPacket_CappedByFields(int packets, int fields, bool expected)
    {
        Assert.AreEqual(expected, SeedShopRules.CanAddPacket(packets, fields));
    }

    [TestCase(true,  false, false, false, SeedState.Owned)]   // starter
    [TestCase(false, true,  true,  false, SeedState.Owned)]   // bought beats an unmet condition
    [TestCase(false, false, false, false, SeedState.Priced)]  // price-only
    [TestCase(false, false, true,  true,  SeedState.Priced)]  // condition met -> priced
    [TestCase(false, false, true,  false, SeedState.Masked)]  // condition unmet -> masked
    public void State_CoversEveryCase(bool starter, bool owned, bool hasCond, bool condMet, SeedState expected)
    {
        Assert.AreEqual(expected, SeedShopRules.State(starter, owned, hasCond, condMet));
    }

    [TestCase("Radish", "seed_radish")]
    [TestCase("Green Beans", "seed_green_beans")]
    [TestCase("Red Pepper", "seed_red_pepper")]
    public void OwnershipKey_Slugs(string name, string key)
    {
        Assert.AreEqual(key, SeedShopRules.OwnershipKey(name));
    }

    [Test]
    public void FilterInOrder_KeepsLadderOrder_WhateverThePurchaseOrder()
    {
        // Bought Tomato first, then Carrot: the result must still follow the ladder.
        var owned = new[] { "Tomato", "Radish", "Carrot" };
        var result = SeedShopRules.FilterInOrder(SeedLadder.Order, n => owned.Contains(n));
        CollectionAssert.AreEqual(new[] { "Radish", "Carrot", "Tomato" }, result);
    }

    // Guards the one fixed crop order: a stray drag in the CropDatabase Inspector must fail the suite
    // instead of silently reshuffling the stall, seed menu and Almanac. Reads via SerializedObject so
    // this EconomyCore-only test assembly needn't reference CropDatabase/CropData types.
    [Test]
    public void CropDatabaseAsset_AllCrops_FollowTheLadder()
    {
        var db = AssetDatabase.LoadMainAssetAtPath("Assets/Data/Crops/CropDatabase.asset");
        Assert.IsNotNull(db, "CropDatabase.asset missing");
        var list = new SerializedObject(db).FindProperty("allCrops");
        var names = new System.Collections.Generic.List<string>();
        for (int i = 0; i < list.arraySize; i++)
        {
            Object crop = list.GetArrayElementAtIndex(i).objectReferenceValue;
            Assert.IsNotNull(crop, $"allCrops[{i}] is empty");
            names.Add(new SerializedObject(crop).FindProperty("cropName").stringValue);
        }
        CollectionAssert.AreEqual(SeedLadder.Order.ToArray(), names.ToArray());
    }

    [TestCase(0, 0, 100)]   // fresh bar: exactly the first point (85 + 15*1)
    [TestCase(40, 0, 60)]   // partially filled bar: tops it up to the point
    [TestCase(0, 1, 115)]   // (edge) a point already earned: fills the next one
    public void WelcomeBasketReward_FillsExactlyOnePoint(int bar, int points, int expected)
    {
        Assert.AreEqual(expected, ReputationMath.WelcomeBasketReward(bar, points));
    }
}
