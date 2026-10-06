using System.Collections.Generic;

/// <summary>One plowable tile as the planner sees it: world centre + whether it still needs tilling.</summary>
public struct PlowTile
{
    public float x, y;
    public bool untilled;
    public PlowTile(float x, float y, bool untilled) { this.x = x; this.y = y; this.untilled = untilled; }
}

/// <summary>A horizontal line of tiles (same world Y) spanning every plowable zone it crosses.</summary>
public struct PlowRow
{
    public float y, minX, maxX;
    public int untilledCount;
}

/// <summary>One straight plow run along a row, from <see cref="startX"/> to <see cref="endX"/>.</summary>
public struct PlowPass
{
    public float rowY, startX, endX;
    public int Direction => endX >= startX ? 1 : -1;
}

/// <summary>
/// Route planning for the Horse's plough. The farm is treated as horizontal rows (Zone 1 and
/// Zone 2 share rows in the 2×2 layout, so a row runs straight across both). Each pass picks the
/// nearest row that still has untilled soil, enters from whichever end is closer, and plows all the
/// way to the far end — never "hunt and peck". Because a pass ends at one side, the next row's
/// nearest end is that same side, so consecutive passes snake back and forth (boustrophedon) and
/// uneven farms (Zones 1+2+3 = an L) are covered row by row. Ties between equally-near rows keep
/// the current vertical sweep direction.
/// </summary>
public static class PlowPlanner
{
    /// <summary>Tiles whose Y differs by less than this share a row.</summary>
    public const float RowTolerance = 0.05f;

    public static List<PlowRow> BuildRows(IEnumerable<PlowTile> tiles)
    {
        var rows = new List<PlowRow>();
        foreach (var t in tiles)
        {
            int i = rows.FindIndex(r => System.Math.Abs(r.y - t.y) < RowTolerance);
            if (i < 0)
            {
                rows.Add(new PlowRow { y = t.y, minX = t.x, maxX = t.x, untilledCount = t.untilled ? 1 : 0 });
                continue;
            }
            var row = rows[i];
            if (t.x < row.minX) row.minX = t.x;
            if (t.x > row.maxX) row.maxX = t.x;
            if (t.untilled) row.untilledCount++;
            rows[i] = row;
        }
        rows.Sort((a, b) => b.y.CompareTo(a.y)); // top row first
        return rows;
    }

    /// <summary>
    /// Plan the next pass for a horse at (<paramref name="hx"/>, <paramref name="hy"/>).
    /// <paramref name="sweepDir"/> is the vertical direction of the last row change (+1 up, −1 down,
    /// 0 none) and only breaks ties. <paramref name="topFirst"/> ignores distance and takes the
    /// highest row that needs work (the run's opening pass, so the horse works top-down while the
    /// helpers start from the bottom). Returns false when no row has untilled soil.
    /// </summary>
    public static bool TryPlanPass(IList<PlowRow> rows, float hx, float hy, int sweepDir, out PlowPass pass,
                                   bool topFirst = false)
    {
        pass = default;
        int best = -1;
        float bestDist = float.MaxValue;
        for (int i = 0; i < rows.Count; i++)
        {
            if (rows[i].untilledCount <= 0) continue;
            if (topFirst)
            {
                if (best < 0 || rows[i].y > rows[best].y) best = i;
                continue;
            }
            float dy = rows[i].y - hy;
            float dist = System.Math.Abs(dy);
            if (best < 0 || dist < bestDist - RowTolerance)
            {
                best = i;
                bestDist = dist;
            }
            else if (System.Math.Abs(dist - bestDist) < RowTolerance && sweepDir != 0
                     && System.Math.Sign(dy) == sweepDir
                     && System.Math.Sign(rows[best].y - hy) != sweepDir)
            {
                best = i; // tie → keep sweeping the same way
            }
        }
        if (best < 0) return false;

        var row = rows[best];
        bool fromLeft = System.Math.Abs(hx - row.minX) <= System.Math.Abs(hx - row.maxX);
        pass = new PlowPass
        {
            rowY = row.y,
            startX = fromLeft ? row.minX : row.maxX,
            endX = fromLeft ? row.maxX : row.minX,
        };
        return true;
    }
}
