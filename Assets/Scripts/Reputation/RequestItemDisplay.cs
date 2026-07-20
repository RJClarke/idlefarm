/// <summary>Maps a DeliveryLineItem.itemId (spec §6 convention) to a human-readable name for UI.</summary>
public static class RequestItemDisplay
{
    public static string Name(string itemId)
    {
        if (string.IsNullOrEmpty(itemId)) return "?";
        if (itemId == "egg") return "Eggs";
        if (itemId == "wood") return "Wood";
        if (itemId == "compost") return "Compost";
        if (itemId.StartsWith("fish_raw_") && int.TryParse(itemId.Substring("fish_raw_".Length), out int rawTier))
            return FishTiers.Name(rawTier);
        if (itemId.StartsWith("fish_smoked_") && int.TryParse(itemId.Substring("fish_smoked_".Length), out int smokedTier))
            return FishTiers.SmokedName(smokedTier);
        return itemId; // crop name as-is
    }
}
