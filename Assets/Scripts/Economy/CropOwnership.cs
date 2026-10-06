using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Which crops a farm can plant, and in how many fields at once. The UpgradeManager permanent level under
/// SeedShopRules.OwnershipKey ("seed_carrot") is the PACKET COUNT: 0 = not owned, 1 = one field,
/// up to 4 (never more than the farm's fields). The starter always has at least 1. Lists always come
/// back in CropDatabase.allCrops order (the one fixed crop order) — never re-sorted.
/// </summary>
public static class CropOwnership
{
    public static event Action OnOwnershipChanged;

    public static bool IsOwned(CropData crop) => Packets(crop) > 0;

    /// <summary>How many fields this crop may grow in at once.</summary>
    public static int Packets(CropData crop)
    {
        if (crop == null) return 0;
        int level = UpgradeManager.Instance != null
            ? UpgradeManager.Instance.GetPermanentLevel(SeedShopRules.OwnershipKey(crop.cropName)) : 0;
        return crop.isStarter ? Mathf.Max(1, level) : level;
    }

    /// <summary>Fields the farm has unlocked (Field 1 is free; 2-4 are Farm upgrades).</summary>
    public static int FieldsOwned()
    {
        int fields = 1;
        if (UpgradeManager.Instance != null)
            for (int z = 2; z <= SeedShopRules.MaxPackets; z++)
                if (UpgradeManager.Instance.GetPermanentLevel($"zone_unlock_{z}") > 0) fields++;
        return fields;
    }

    public static bool CanAddPacket(CropData crop) => SeedShopRules.CanAddPacket(Packets(crop), FieldsOwned());

    /// <summary>Price of this crop's next packet (its 1st if not owned yet).</summary>
    public static int NextPacketPrice(CropData crop) =>
        crop == null ? 0 : SeedShopRules.PacketPrice(crop.unlockCost, Packets(crop) + 1);

    public static SeedState StateOf(CropData crop)
    {
        bool hasCondition = crop != null && !string.IsNullOrEmpty(crop.unlockFeatureFlag);
        bool conditionMet = hasCondition && ResearchManager.Instance != null
                            && ResearchManager.Instance.IsFeatureUnlocked(crop.unlockFeatureFlag);
        return SeedShopRules.State(crop != null && crop.isStarter, IsOwned(crop), hasCondition, conditionMet);
    }

    /// <summary>Owned crops, in allCrops order.</summary>
    public static List<CropData> Owned(CropDatabase db) =>
        db == null ? new List<CropData>() : SeedShopRules.FilterInOrder(db.allCrops, c => c != null && IsOwned(c));

    /// <summary>Buys a Priced crop's first packet, or an extra packet of an owned crop (while the farm
    /// has more fields than packets). Masked packets can't be bought.</summary>
    public static bool TryBuy(CropData crop)
    {
        if (crop == null) return false;
        SeedState state = StateOf(crop);
        bool first = state == SeedState.Priced;
        bool extra = state == SeedState.Owned && CanAddPacket(crop);
        if (!first && !extra) return false;
        if (UpgradeManager.Instance == null || CurrencyManager.Instance == null) return false;

        int price = NextPacketPrice(crop);
        if (!CurrencyManager.Instance.SpendCoins(price)) return false;

        int packets = Packets(crop) + 1;
        UpgradeManager.Instance.GrantPermanentLevel(SeedShopRules.OwnershipKey(crop.cropName), packets);
        Debug.Log($"[Seeds] Bought {crop.cropName} packet #{packets} for {price} Coins");
        OnOwnershipChanged?.Invoke();
        if (first)
        {
            NarrativeDirector.Raise("seed_bought");
            if (crop.canRegrow) NarrativeDirector.Raise("seed_bought:regrow");
        }
        else NarrativeDirector.Raise("packet_bought");
        return true;
    }

    /// <summary>A gifted packet (e.g. enclosed with a letter): owns the crop for free. A crop already
    /// owned (the starter always is) keeps its packets — gifts never stack extra fields.</summary>
    public static void GrantFirstPacket(CropData crop)
    {
        if (crop == null || IsOwned(crop) || UpgradeManager.Instance == null) return;
        UpgradeManager.Instance.GrantPermanentLevel(SeedShopRules.OwnershipKey(crop.cropName), 1);
        Debug.Log($"[Seeds] Gifted {crop.cropName} packet");
        OnOwnershipChanged?.Invoke();
    }

    /// <summary>The crop with this name from the scene's crop list, or null.</summary>
    public static CropData Find(string cropName)
    {
        var db = SeedSelectionPopup.Instance != null ? SeedSelectionPopup.Instance.Crops : null;
        return db != null ? db.GetCropByName(cropName) : null;
    }
}
