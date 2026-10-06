using System.Collections.Generic;

/// <summary>A soil tile's stable address: zone + in-zone grid coords. Survives grid-size upgrades
/// (the grid grows right/down, so existing (x,y) indices never move).</summary>
public readonly struct TileKey : System.IEquatable<TileKey>
{
    public readonly int Zone, X, Y;
    public TileKey(int zone, int x, int y) { Zone = zone; X = x; Y = y; }

    /// <summary>The ring a tile belongs to: the 2×2 corner is rings 0–1, the cells a 3×3 adds are ring 2, …</summary>
    public int Ring => X > Y ? X : Y;

    public bool Equals(TileKey o) => Zone == o.Zone && X == o.X && Y == o.Y;
    public override bool Equals(object obj) => obj is TileKey k && Equals(k);
    public override int GetHashCode() => (Zone * 397 ^ X) * 397 ^ Y;
    public override string ToString() => $"{Zone}:{X}:{Y}";

    public static bool TryParse(string s, out TileKey key)
    {
        key = default;
        if (string.IsNullOrEmpty(s)) return false;
        string[] p = s.Split(':');
        if (p.Length != 3) return false;
        if (!int.TryParse(p[0], out int z) || !int.TryParse(p[1], out int x) || !int.TryParse(p[2], out int y)) return false;
        key = new TileKey(z, x, y);
        return true;
    }
}

/// <summary>
/// Decides WHICH tile the next "Pre-Tilled Soil" purchase marks. Rule: the lowest-numbered zone
/// with an unmarked tile wins, so Zone 1 always fills first; a new zone is used only once every
/// tile in the earlier zones is marked, and a grid-size upgrade sends the next purchase back to
/// Zone 1's new ring. Within a zone, inner rings fill first (2×2 before the 3×3 ring), then
/// top-to-bottom, left-to-right. "Avoid" tiles (e.g. the one a sprinkler sits on) go last in their zone.
///
/// Purchases are stored as an ordered mark list (purchase order) alongside the upgrade level;
/// <see cref="Reconcile"/> keeps the two in step if they ever drift (dev resets, level grants).
/// </summary>
public static class PreTillPlanner
{
    public static bool TryPickNext(IEnumerable<TileKey> unlocked, ICollection<TileKey> marked,
                                   out TileKey next, ICollection<TileKey> avoid = null)
    {
        bool found = false;
        next = default;
        foreach (var t in unlocked)
        {
            if (marked.Contains(t)) continue;
            if (!found || Before(t, next, avoid)) { next = t; found = true; }
        }
        return found;
    }

    /// <summary>How many unlocked tiles are still unmarked (0 = the buy button should disable).</summary>
    public static int Remaining(IEnumerable<TileKey> unlocked, ICollection<TileKey> marked)
    {
        int n = 0;
        foreach (var t in unlocked) if (!marked.Contains(t)) n++;
        return n;
    }

    /// <summary>
    /// Bring the ordered mark list in line with the owned level: drop the most recent marks if
    /// there are more marks than levels; mark more tiles (as space allows) if there are fewer.
    /// Levels that can't be placed yet stay "banked" and land when more tiles unlock.
    /// Returns true if the list changed.
    /// </summary>
    public static bool Reconcile(List<TileKey> marks, int level, IEnumerable<TileKey> unlocked,
                                 ICollection<TileKey> avoid = null)
    {
        bool changed = false;
        if (level < 0) level = 0;
        while (marks.Count > level) { marks.RemoveAt(marks.Count - 1); changed = true; }

        if (marks.Count < level)
        {
            var unlockedList = new List<TileKey>(unlocked);
            var set = new HashSet<TileKey>(marks);
            while (marks.Count < level && TryPickNext(unlockedList, set, out TileKey t, avoid))
            {
                marks.Add(t);
                set.Add(t);
                changed = true;
            }
        }
        return changed;
    }

    private static bool Before(TileKey a, TileKey b, ICollection<TileKey> avoid)
    {
        if (a.Zone != b.Zone) return a.Zone < b.Zone;
        bool aAvoid = avoid != null && avoid.Contains(a);
        bool bAvoid = avoid != null && avoid.Contains(b);
        if (aAvoid != bAvoid) return !aAvoid;
        if (a.Ring != b.Ring) return a.Ring < b.Ring;
        if (a.Y != b.Y) return a.Y < b.Y;
        return a.X < b.X;
    }
}
