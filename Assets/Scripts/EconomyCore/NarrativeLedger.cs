using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Pure fire-once flag set. Records "this narrative beat has happened" so it never
/// fires again. Persisted as a flat string[] in GameData; wrapped by NarrativeManager.
/// </summary>
public class NarrativeLedger
{
    private readonly HashSet<string> fired = new HashSet<string>();

    public bool HasFired(string id) => !string.IsNullOrEmpty(id) && fired.Contains(id);

    /// <summary>Returns true iff this id was not already present (i.e. fires now).</summary>
    public bool MarkFired(string id)
    {
        if (string.IsNullOrEmpty(id)) return false;
        return fired.Add(id);
    }

    public void Load(IEnumerable<string> ids)
    {
        fired.Clear();
        if (ids == null) return;
        foreach (var id in ids)
            if (!string.IsNullOrEmpty(id)) fired.Add(id);
    }

    public string[] ToArray() => fired.ToArray();
}
