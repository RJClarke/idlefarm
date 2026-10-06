using System;
using System.Collections.Generic;

/// <summary>
/// Which tiles the player has bought as Pre-Tilled Soil (Farm tab, upgrade "pre_till"), in purchase
/// order. Static so it outlives FarmGrid regenerations and SaveManager can read/write it whether or
/// not the grid exists yet. The upgrade LEVEL (UpgradeManager) is how many marks the player owns;
/// FarmGrid keeps this list in step with it via <see cref="PreTillPlanner.Reconcile"/>.
/// </summary>
public static class PreTilledSoil
{
    public const string UpgradeID = "pre_till";

    private static readonly List<TileKey> marks = new List<TileKey>();
    private static readonly HashSet<TileKey> lookup = new HashSet<TileKey>();

    /// <summary>Fired when the mark list changes (purchase, load, reconcile).</summary>
    public static event Action OnChanged;

    public static int Count => marks.Count;
    public static bool Contains(TileKey key) => lookup.Contains(key);
    public static ICollection<TileKey> Lookup => lookup;

    /// <summary>Run <paramref name="edit"/> on the ordered list; the lookup is rebuilt and listeners
    /// notified only if it reports a change.</summary>
    public static bool Edit(Func<List<TileKey>, bool> edit)
    {
        if (!edit(marks)) return false;
        Rebuild();
        OnChanged?.Invoke();
        return true;
    }

    public static string[] ToSave()
    {
        var arr = new string[marks.Count];
        for (int i = 0; i < marks.Count; i++) arr[i] = marks[i].ToString();
        return arr;
    }

    public static void Load(string[] saved)
    {
        marks.Clear();
        var seen = new HashSet<TileKey>();
        if (saved != null)
            foreach (string s in saved)
                if (TileKey.TryParse(s, out TileKey k) && seen.Add(k)) marks.Add(k);
        Rebuild();
        OnChanged?.Invoke();
    }

    // Statics survive play sessions when domain reload is off; start each session clean.
    [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        marks.Clear();
        lookup.Clear();
        OnChanged = null;
    }

    private static void Rebuild()
    {
        lookup.Clear();
        foreach (var k in marks) lookup.Add(k);
    }
}
