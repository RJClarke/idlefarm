using System;
using System.Collections.Generic;
using System.Linq;

public enum SkinBuyResult { Ok, AlreadyOwned, SetOnly, NotEnoughGems, Unknown }

/// <summary>Which skins are owned and which is equipped per target. Classic (target + "_classic") is always
/// owned and is what EquippedFor returns when nothing else is equipped. Unknown ids are dropped on import.</summary>
public sealed class SkinOwnershipCore
{
    private readonly Dictionary<string, SkinDef> defs = new Dictionary<string, SkinDef>(StringComparer.Ordinal);
    private readonly HashSet<string> owned = new HashSet<string>(StringComparer.Ordinal);
    private readonly Dictionary<string, string> equipped = new Dictionary<string, string>(StringComparer.Ordinal);

    public SkinOwnershipCore(IEnumerable<SkinDef> catalog)
    {
        if (catalog == null) return;
        foreach (SkinDef d in catalog) if (d != null && !string.IsNullOrEmpty(d.id)) defs[d.id] = d;
    }

    public bool IsOwned(string id) => SkinDefaults.IsClassic(id) || (id != null && owned.Contains(id));

    public string EquippedFor(string target) =>
        target != null && equipped.TryGetValue(target, out string id) ? id : SkinDefaults.ClassicId(target);

    public bool IsEquipped(string id)
    {
        if (string.IsNullOrEmpty(id)) return false;
        string target = SkinDefaults.IsClassic(id) ? SkinDefaults.TargetOfClassic(id) : (defs.TryGetValue(id, out SkinDef d) ? d.target : null);
        return target != null && EquippedFor(target) == id;
    }

    public int PriceOf(string id) => id != null && defs.TryGetValue(id, out SkinDef d) ? d.gemPrice : 0;

    public SkinBuyResult CheckBuy(string id, int gems)
    {
        if (IsOwned(id)) return SkinBuyResult.AlreadyOwned;
        if (id == null || !defs.TryGetValue(id, out SkinDef d)) return SkinBuyResult.Unknown;
        if (d.IsSetOnly) return SkinBuyResult.SetOnly;
        return gems >= d.gemPrice ? SkinBuyResult.Ok : SkinBuyResult.NotEnoughGems;
    }

    /// <summary>After the caller spent the gems: own it and wear it.</summary>
    public void MarkBought(string id)
    {
        if (id == null || !defs.ContainsKey(id)) return;
        owned.Add(id);
        Equip(id);
    }

    public void Grant(IEnumerable<string> ids)
    {
        if (ids == null) return;
        foreach (string id in ids) if (id != null && defs.ContainsKey(id)) owned.Add(id);
    }

    public bool Equip(string id)
    {
        if (SkinDefaults.IsClassic(id)) { equipped.Remove(SkinDefaults.TargetOfClassic(id)); return true; }
        if (id == null || !owned.Contains(id) || !defs.TryGetValue(id, out SkinDef d)) return false;
        equipped[d.target] = id;
        return true;
    }

    public string[] ExportOwned() => owned.OrderBy(s => s, StringComparer.Ordinal).ToArray();
    public string[] ExportEquipped() => equipped.OrderBy(kv => kv.Key, StringComparer.Ordinal).Select(kv => kv.Key + "=" + kv.Value).ToArray();

    public void Import(string[] ownedIds, string[] equippedPairs)
    {
        owned.Clear(); equipped.Clear();
        Grant(ownedIds);
        if (equippedPairs == null) return;
        foreach (string pair in equippedPairs)
        {
            if (string.IsNullOrEmpty(pair)) continue;
            int eq = pair.IndexOf('=');
            if (eq <= 0) continue;
            Equip(pair.Substring(eq + 1)); // validates ownership + derives the target from the skin
        }
    }
}
