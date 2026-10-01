using System;
using System.Collections.Generic;

/// <summary>What a seed packet looks like in Hazel's stall.</summary>
public enum SeedState { Owned, Priced, Masked }

/// <summary>Pure rules for crop ownership and the Plants stall. No Unity types.</summary>
public static class SeedShopRules
{
    public const string MaskedHint = "Discovered through research";

    /// <summary>Owned if it's the starter or bought; otherwise Masked while an unlock condition is
    /// unmet, else Priced (visible, locked, shows its price).</summary>
    public static SeedState State(bool isStarter, bool owned, bool hasCondition, bool conditionMet)
    {
        if (isStarter || owned) return SeedState.Owned;
        if (hasCondition && !conditionMet) return SeedState.Masked;
        return SeedState.Priced;
    }

    /// <summary>UpgradeManager permanent-level id that records ownership, e.g. "seed_green_beans".
    /// New ids on purpose: the old "*_unlock" crop ids are never read, so old saves start fresh.</summary>
    public static string OwnershipKey(string cropName) =>
        "seed_" + (cropName ?? "").Trim().ToLowerInvariant().Replace(' ', '_');

    /// <summary>Most fields a farm can have, so the most packets of one crop.</summary>
    public const int MaxPackets = 4;

    /// <summary>The 2nd packet costs this many times the 1st (then each further packet doubles).
    /// The one knob for "how expensive is doubling down" — tune it here.</summary>
    public const int SecondPacketMultiplier = 10;

    /// <summary>Price of the Nth packet of a crop (1-based). 1st = the crop's price; 2nd = ten times
    /// that (a free starter's 2nd costs 10); 3rd and 4th double. Extra packets let one crop grow in
    /// more fields at once — a deliberate choice, not a bargain.</summary>
    public static int PacketPrice(int firstPrice, int packetNumber)
    {
        if (packetNumber <= 1) return Math.Max(0, firstPrice);
        int second = firstPrice > 0 ? firstPrice * SecondPacketMultiplier : SecondPacketMultiplier;
        return second << (packetNumber - 2);
    }

    /// <summary>A crop you own can gain a packet while you have more fields than packets of it.</summary>
    public static bool CanAddPacket(int packetsOwned, int fieldsOwned) =>
        packetsOwned >= 1 && packetsOwned < Math.Min(fieldsOwned, MaxPackets);

    /// <summary>Filters an already-ordered list and keeps its order. Every crop list goes through
    /// this (or an equivalent Where) instead of sorting, so the order is the same for every player.</summary>
    public static List<T> FilterInOrder<T>(IEnumerable<T> ordered, Func<T, bool> keep)
    {
        var result = new List<T>();
        if (ordered == null) return result;
        foreach (T item in ordered) if (keep(item)) result.Add(item);
        return result;
    }
}
