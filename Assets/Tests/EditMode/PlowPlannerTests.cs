using System.Collections.Generic;
using NUnit.Framework;

public class PlowPlannerTests
{
    // Zones in the 2×2 layout: Z1 top-left, Z2 top-right, Z3 bottom-left. Each zone is n×n tiles of
    // width 1 with a 0.5 gap between zones. Rows are listed top (y = n-1 + offset) to bottom.
    private static List<PlowTile> Zone(int zone, int n, bool untilled = true)
    {
        float zx = (zone == 2 || zone == 4) ? n + 0.5f : 0f;
        float zy = (zone == 3 || zone == 4) ? -(n + 0.5f) : 0f;
        var list = new List<PlowTile>();
        for (int x = 0; x < n; x++)
            for (int y = 0; y < n; y++)
                list.Add(new PlowTile(zx + x, zy - y, untilled));
        return list;
    }

    // Run the planner to completion, tilling every row it passes; returns the passes in order.
    private static List<PlowPass> Plow(List<PlowTile> tiles, float hx, float hy)
    {
        var passes = new List<PlowPass>();
        int sweep = 0;
        float lastY = hy;
        for (int guard = 0; guard < 100; guard++)
        {
            var rows = PlowPlanner.BuildRows(tiles);
            if (!PlowPlanner.TryPlanPass(rows, hx, hy, sweep, out PlowPass p)) break;
            passes.Add(p);
            for (int i = 0; i < tiles.Count; i++)
                if (System.Math.Abs(tiles[i].y - p.rowY) < PlowPlanner.RowTolerance)
                    tiles[i] = new PlowTile(tiles[i].x, tiles[i].y, false);
            if (p.rowY != lastY) sweep = p.rowY > lastY ? 1 : -1;
            lastY = p.rowY;
            hx = p.endX;
            hy = p.rowY;
        }
        return passes;
    }

    [Test]
    public void Rows_SpanAdjacentZones()
    {
        var tiles = Zone(1, 2);
        tiles.AddRange(Zone(2, 2));
        var rows = PlowPlanner.BuildRows(tiles);
        Assert.AreEqual(2, rows.Count);
        Assert.AreEqual(0f, rows[0].minX);
        Assert.AreEqual(3.5f, rows[0].maxX); // Z2's far tile: 2.5 + 1
        Assert.AreEqual(4, rows[0].untilledCount);
    }

    [Test]
    public void SingleZone_SnakesBackAndForth()
    {
        var passes = Plow(Zone(1, 3), -1f, 0f); // horse just left of the top row
        Assert.AreEqual(3, passes.Count);
        Assert.AreEqual(1, passes[0].Direction);   // L → R
        Assert.AreEqual(-1, passes[1].Direction);  // R → L
        Assert.AreEqual(1, passes[2].Direction);   // L → R
        Assert.AreEqual(0f, passes[0].rowY);
        Assert.AreEqual(-1f, passes[1].rowY);
        Assert.AreEqual(-2f, passes[2].rowY);
    }

    [Test]
    public void TwoZones_EachPassCrossesBothZones()
    {
        var tiles = Zone(1, 2);
        tiles.AddRange(Zone(2, 2));
        var passes = Plow(tiles, -1f, 0f);
        Assert.AreEqual(2, passes.Count);
        Assert.AreEqual(0f, passes[0].startX);
        Assert.AreEqual(3.5f, passes[0].endX);
        Assert.AreEqual(3.5f, passes[1].startX); // turned around at Zone 2's edge
        Assert.AreEqual(0f, passes[1].endX);
    }

    [Test]
    public void LShape_CoversEveryRow_WideThenNarrow()
    {
        var tiles = Zone(1, 2);
        tiles.AddRange(Zone(2, 2));
        tiles.AddRange(Zone(3, 2));
        var passes = Plow(tiles, -1f, 0f);
        Assert.AreEqual(4, passes.Count);
        Assert.AreEqual(3.5f, System.Math.Max(passes[0].startX, passes[0].endX)); // wide rows
        Assert.AreEqual(3.5f, System.Math.Max(passes[1].startX, passes[1].endX));
        Assert.AreEqual(1f, System.Math.Max(passes[2].startX, passes[2].endX));   // Zone 3 rows
        Assert.AreEqual(1f, System.Math.Max(passes[3].startX, passes[3].endX));
        Assert.AreEqual(-passes[2].Direction, passes[3].Direction); // still snaking
    }

    [Test]
    public void LoneUntilledTile_PlowsTheWholeRow_FromTheNearEnd()
    {
        var tiles = Zone(1, 3, untilled: false);
        tiles.AddRange(Zone(2, 3, untilled: false));
        int i = tiles.FindIndex(t => t.x == 1f && t.y == -1f);
        tiles[i] = new PlowTile(1f, -1f, true);

        var rows = PlowPlanner.BuildRows(tiles);
        Assert.IsTrue(PlowPlanner.TryPlanPass(rows, 7f, 3f, 0, out PlowPass p)); // horse off to the right
        Assert.AreEqual(-1f, p.rowY);
        Assert.AreEqual(5.5f, p.startX); // near (right) end of the row
        Assert.AreEqual(0f, p.endX);     // keeps going to the far end
    }

    [Test]
    public void TopFirst_TakesTheHighestRowNeedingWork_EvenWhenFarAway()
    {
        var tiles = Zone(1, 3);
        tiles.AddRange(Zone(3, 3));
        var rows = PlowPlanner.BuildRows(tiles);
        // Horse below the farm: nearest would be the bottom row, but the opening pass goes to the top.
        Assert.IsTrue(PlowPlanner.TryPlanPass(rows, 0f, -10f, 0, out PlowPass p, topFirst: true));
        Assert.AreEqual(0f, p.rowY);
        Assert.IsTrue(PlowPlanner.TryPlanPass(rows, 0f, -10f, 0, out PlowPass near));
        Assert.AreEqual(-5.5f, near.rowY); // Zone 3's bottom row
    }

    [Test]
    public void TopFirst_SkipsTopRowsThatAreAlreadyTilled()
    {
        var tiles = Zone(1, 3);
        for (int i = 0; i < tiles.Count; i++)
            if (tiles[i].y == 0f) tiles[i] = new PlowTile(tiles[i].x, 0f, false); // pre-tilled top row
        var rows = PlowPlanner.BuildRows(tiles);
        Assert.IsTrue(PlowPlanner.TryPlanPass(rows, 0f, -10f, 0, out PlowPass p, topFirst: true));
        Assert.AreEqual(-1f, p.rowY);
    }

    [Test]
    public void NothingUntilled_NoPass()
    {
        var rows = PlowPlanner.BuildRows(Zone(1, 2, untilled: false));
        Assert.IsFalse(PlowPlanner.TryPlanPass(rows, 0f, 0f, 0, out _));
    }

    [Test]
    public void EquallyNearRows_KeepTheSweepDirection()
    {
        var tiles = Zone(1, 3);
        var rows = PlowPlanner.BuildRows(tiles);
        // Horse on the middle row (y=-1); rows above (0) and below (-2) are equally near.
        Assert.IsTrue(PlowPlanner.TryPlanPass(rows, 0f, -1.5f, -1, out PlowPass down));
        Assert.AreEqual(-2f, down.rowY);
        Assert.IsTrue(PlowPlanner.TryPlanPass(rows, 0f, -0.5f, 1, out PlowPass up));
        Assert.AreEqual(0f, up.rowY);
    }
}
