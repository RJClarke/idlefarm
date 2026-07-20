using UnityEngine;

/// <summary>
/// The single facade that knows where every requestable item type lives (Reputation Phase 2,
/// spec §3.5/§6). Board slots and future NPC fulfillment quests both go through this — neither
/// needs to know whether an item is a crop stack, a Pantry count, or a CurrencyManager resource.
/// v1 item id convention: crop names as-is, "egg", "wood", "compost", "fish_raw_&lt;tier&gt;",
/// "fish_smoked_&lt;tier&gt;". Jars are out of scope for v1 (spec §9 — no per-crop jar-count API).
/// </summary>
public static class DeliveryService
{
    public static bool CanFulfill(DeliveryRequest request)
    {
        if (request?.items == null || request.items.Length == 0) return false;
        foreach (DeliveryLineItem item in request.items)
            if (!CanFulfillLine(item)) return false;
        return true;
    }

    public static bool TryFulfill(DeliveryRequest request)
    {
        if (!CanFulfill(request)) return false;
        foreach (DeliveryLineItem item in request.items)
            SpendLine(item);
        return true;
    }

    private static bool CanFulfillLine(DeliveryLineItem item)
    {
        if (item == null || string.IsNullOrEmpty(item.itemId) || item.count <= 0) return false;
        string id = item.itemId;

        if (id == "egg") return ItemInventoryManager.Instance != null && ItemInventoryManager.Instance.Eggs >= item.count;
        if (id == "wood") return CurrencyManager.Instance != null && CurrencyManager.Instance.Wood >= item.count;
        if (id == "compost") return CurrencyManager.Instance != null && CurrencyManager.Instance.Compost >= item.count;
        if (id.StartsWith("fish_raw_"))
            return TryParseTier(id, "fish_raw_", out int rawTier) && PantryManager.Instance != null && PantryManager.Instance.GetRaw(rawTier) >= item.count;
        if (id.StartsWith("fish_smoked_"))
            return TryParseTier(id, "fish_smoked_", out int smokedTier) && PantryManager.Instance != null && PantryManager.Instance.GetSmoked(smokedTier) >= item.count;

        return ItemInventoryManager.Instance != null && ItemInventoryManager.Instance.GetCrop(id) >= item.count;
    }

    private static void SpendLine(DeliveryLineItem item)
    {
        string id = item.itemId;

        if (id == "egg") { ItemInventoryManager.Instance.TrySpendEggs(item.count); return; }
        if (id == "wood") { CurrencyManager.Instance.SpendWood(item.count); return; }
        if (id == "compost") { CurrencyManager.Instance.SpendCompost(item.count); return; }
        if (id.StartsWith("fish_raw_") && TryParseTier(id, "fish_raw_", out int rawTier))
        {
            for (int i = 0; i < item.count; i++) PantryManager.Instance.SpendRaw(rawTier);
            return;
        }
        if (id.StartsWith("fish_smoked_") && TryParseTier(id, "fish_smoked_", out int smokedTier))
        {
            for (int i = 0; i < item.count; i++) PantryManager.Instance.SpendSmoked(smokedTier);
            return;
        }
        ItemInventoryManager.Instance.TrySpendCrop(id, item.count);
    }

    private static bool TryParseTier(string id, string prefix, out int tier)
        => int.TryParse(id.Substring(prefix.Length), out tier);
}
