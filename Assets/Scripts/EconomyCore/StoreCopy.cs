using System.Globalization;

/// <summary>Shared player-facing Store copy (short and plain — see the copy rule).</summary>
public static class StoreCopy
{
    /// <summary>Every 2-tap gem spend (skin cards, research Finish-now): "Confirm 1,000 gems?".</summary>
    public static string ConfirmGems(int cost) =>
        "Confirm " + cost.ToString("N0", CultureInfo.InvariantCulture) + (cost == 1 ? " gem?" : " gems?");
}
