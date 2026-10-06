using System;
using System.Collections.Generic;

/// <summary>
/// Remembers which store transactions were already delivered, so a purchase the store re-sends
/// (app killed between grant and confirm, or a restore) never grants twice. Saved in GameData.
/// </summary>
public sealed class PurchaseLedgerCore
{
    private readonly HashSet<string> delivered = new HashSet<string>(StringComparer.Ordinal);

    public bool IsDelivered(string transactionId) =>
        !string.IsNullOrEmpty(transactionId) && delivered.Contains(transactionId);

    /// <summary>True the first time (grant), false for a re-delivery. An empty id can't be
    /// de-duplicated, so it is always deliverable and never recorded.</summary>
    public bool TryMarkDelivered(string transactionId)
    {
        if (string.IsNullOrEmpty(transactionId)) return true;
        return delivered.Add(transactionId);
    }

    public string[] Export()
    {
        var ids = new string[delivered.Count];
        delivered.CopyTo(ids);
        Array.Sort(ids, StringComparer.Ordinal);
        return ids;
    }

    public void Import(string[] ids)
    {
        delivered.Clear();
        if (ids == null) return;
        foreach (string id in ids) if (!string.IsNullOrEmpty(id)) delivered.Add(id);
    }

    /// <summary>Whether a delivery grants its gems. A permanent unlock the save already owns
    /// grants nothing again, even under a new transaction id (iOS restores mint new ids).</summary>
    public static bool ShouldGrant(bool ledgerIsNew, bool isNonConsumable, bool alreadyOwned) =>
        ledgerIsNew && !(isNonConsumable && alreadyOwned);
}
