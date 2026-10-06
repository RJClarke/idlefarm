using System.Collections.Generic;
using NUnit.Framework;

public class PreTillPlannerTests
{
    // All tiles of the given zones at an n×n grid size.
    private static List<TileKey> Grid(int size, params int[] zones)
    {
        var list = new List<TileKey>();
        foreach (int z in zones)
            for (int x = 0; x < size; x++)
                for (int y = 0; y < size; y++)
                    list.Add(new TileKey(z, x, y));
        return list;
    }

    private static TileKey Pick(List<TileKey> unlocked, HashSet<TileKey> marked, ICollection<TileKey> avoid = null)
    {
        Assert.IsTrue(PreTillPlanner.TryPickNext(unlocked, marked, out TileKey t, avoid));
        marked.Add(t);
        return t;
    }

    [Test]
    public void FillsZone1_InReadingOrder_First()
    {
        var unlocked = Grid(2, 1, 2);
        var marked = new HashSet<TileKey>();
        Assert.AreEqual(new TileKey(1, 0, 0), Pick(unlocked, marked));
        Assert.AreEqual(new TileKey(1, 1, 0), Pick(unlocked, marked));
        Assert.AreEqual(new TileKey(1, 0, 1), Pick(unlocked, marked));
        Assert.AreEqual(new TileKey(1, 1, 1), Pick(unlocked, marked));
        Assert.AreEqual(new TileKey(2, 0, 0), Pick(unlocked, marked)); // zone 1 full → zone 2
    }

    [Test]
    public void GridGrowth_SendsNextPickBackToZone1()
    {
        var marked = new HashSet<TileKey>();
        var small = Grid(2, 1, 2);
        for (int i = 0; i < 5; i++) Pick(small, marked); // all of Z1 + one of Z2

        var bigger = Grid(3, 1, 2);
        TileKey next = Pick(bigger, marked);
        Assert.AreEqual(1, next.Zone);
        Assert.AreEqual(2, next.Ring); // the new outer ring
        Assert.AreEqual(new TileKey(1, 2, 0), next);
    }

    [Test]
    public void InnerRingFillsBeforeOuterRing()
    {
        var unlocked = Grid(3, 1);
        var marked = new HashSet<TileKey>();
        for (int i = 0; i < 4; i++) Assert.LessOrEqual(Pick(unlocked, marked).Ring, 1); // the 2×2 corner
        for (int i = 0; i < 5; i++) Assert.AreEqual(2, Pick(unlocked, marked).Ring);
        Assert.IsFalse(PreTillPlanner.TryPickNext(unlocked, marked, out _));
    }

    [Test]
    public void AvoidedTile_GoesLastWithinItsZone_ButBeforeLaterZones()
    {
        var unlocked = Grid(2, 1, 2);
        var marked = new HashSet<TileKey>();
        var avoid = new HashSet<TileKey> { new TileKey(1, 0, 0) };
        Assert.AreEqual(new TileKey(1, 1, 0), Pick(unlocked, marked, avoid));
        Assert.AreEqual(new TileKey(1, 0, 1), Pick(unlocked, marked, avoid));
        Assert.AreEqual(new TileKey(1, 1, 1), Pick(unlocked, marked, avoid));
        Assert.AreEqual(new TileKey(1, 0, 0), Pick(unlocked, marked, avoid));
        Assert.AreEqual(2, Pick(unlocked, marked, avoid).Zone);
    }

    [Test]
    public void Remaining_CountsUnmarkedUnlockedTiles()
    {
        var unlocked = Grid(2, 1);
        var marked = new HashSet<TileKey> { new TileKey(1, 0, 0), new TileKey(3, 0, 0) };
        Assert.AreEqual(3, PreTillPlanner.Remaining(unlocked, marked));
    }

    [Test]
    public void Reconcile_TrimsMostRecentMarks_WhenLevelDrops()
    {
        var marks = new List<TileKey> { new TileKey(1, 0, 0), new TileKey(1, 1, 0), new TileKey(2, 0, 0) };
        Assert.IsTrue(PreTillPlanner.Reconcile(marks, 2, Grid(2, 1, 2)));
        CollectionAssert.AreEqual(new[] { new TileKey(1, 0, 0), new TileKey(1, 1, 0) }, marks);
    }

    [Test]
    public void Reconcile_BanksLevelsThatDontFitYet()
    {
        var marks = new List<TileKey>();
        Assert.IsTrue(PreTillPlanner.Reconcile(marks, 6, Grid(2, 1)));
        Assert.AreEqual(4, marks.Count); // only 4 tiles exist

        // Grid grows → the 2 banked levels land in the new ring.
        Assert.IsTrue(PreTillPlanner.Reconcile(marks, 6, Grid(3, 1)));
        Assert.AreEqual(6, marks.Count);
        Assert.AreEqual(2, marks[4].Ring);
        Assert.IsFalse(PreTillPlanner.Reconcile(marks, 6, Grid(3, 1)));
    }

    [Test]
    public void TileKey_RoundTripsThroughString()
    {
        var k = new TileKey(3, 4, 2);
        Assert.IsTrue(TileKey.TryParse(k.ToString(), out TileKey back));
        Assert.AreEqual(k, back);
        Assert.IsFalse(TileKey.TryParse("garbage", out _));
        Assert.IsFalse(TileKey.TryParse("", out _));
    }
}
