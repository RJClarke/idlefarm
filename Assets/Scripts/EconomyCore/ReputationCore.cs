using System;
using UnityEngine;

/// <summary>One (itemId, count) line of a delivery request.</summary>
[Serializable]
public class DeliveryLineItem
{
    public string itemId;
    public int count;
}

/// <summary>
/// A reusable "deliver these item stacks -> reward" requirement (Reputation Phase 2, spec §3.5).
/// Board slots are the only producer today; future NPC fulfillment quests construct the same
/// struct and fulfill it through the same DeliveryService, without touching board code.
/// </summary>
[Serializable]
public class DeliveryRequest
{
    public DeliveryLineItem[] items;
    public int repReward;
    public string requesterName;
    public string flavorText;
}

/// <summary>
/// Pure reputation + board-slot state: rep bar progress, lifetime points, unspent points, the
/// consecutive-skip counter, and the 3 fixed-difficulty slots (0=Easy,1=Medium,2=Hard). Rolling
/// new requests and spending items are NOT this class's job (they need live Unity singletons for
/// unlock state and item stores) — this only stores/transitions state given the outcome.
/// Spec: 2026-07-19-reputation-design.md §3.2/§3.4.
/// </summary>
public class ReputationCore
{
    public int BarProgress { get; private set; }
    public int PointsEarned { get; private set; }
    public int UnspentPoints { get; private set; }
    public int ConsecutiveSkips { get; private set; }

    private readonly DeliveryRequest[] slotRequests = new DeliveryRequest[3];
    private readonly long[] slotCooldownEndUtcTicks = new long[3];

    public DeliveryRequest GetSlotRequest(int slot) => slotRequests[slot];
    public long GetSlotCooldownEndUtcTicks(int slot) => slotCooldownEndUtcTicks[slot];
    public bool IsSlotOnCooldown(int slot, long nowUtcTicks) => slotCooldownEndUtcTicks[slot] > nowUtcTicks;

    /// <summary>Sets an active request on a slot and clears any cooldown (used for initial rolls, re-rolls, and skips).</summary>
    public void SetSlotRequest(int slot, DeliveryRequest request)
    {
        slotRequests[slot] = request;
        slotCooldownEndUtcTicks[slot] = 0;
    }

    public int AddRep(int gain)
    {
        ReputationMath.ApplyGain(BarProgress, PointsEarned, gain, out int barAfter, out int awarded);
        BarProgress = barAfter;
        PointsEarned += awarded;
        UnspentPoints += awarded;
        return awarded;
    }

    /// <summary>Call after the caller has already spent the request's items successfully.</summary>
    public void OnFulfilled(int slot, long nowUtcTicks, long cooldownTicks)
    {
        slotRequests[slot] = null;
        slotCooldownEndUtcTicks[slot] = nowUtcTicks + cooldownTicks;
        ConsecutiveSkips = 0;
    }

    public void OnSkipped() => ConsecutiveSkips++;

    public int NextSkipCost() => ReputationMath.SkipCost(ConsecutiveSkips);

    public void Import(int barProgress, int pointsEarned, int unspentPoints, int consecutiveSkips,
        DeliveryRequest[] requests, long[] cooldowns)
    {
        BarProgress = Mathf.Max(0, barProgress);
        PointsEarned = Mathf.Max(0, pointsEarned);
        UnspentPoints = Mathf.Max(0, unspentPoints);
        ConsecutiveSkips = Mathf.Max(0, consecutiveSkips);
        for (int i = 0; i < 3; i++)
        {
            DeliveryRequest r = (requests != null && i < requests.Length) ? requests[i] : null;
            slotRequests[i] = (r != null && r.items != null && r.items.Length > 0) ? r : null;
            slotCooldownEndUtcTicks[i] = (cooldowns != null && i < cooldowns.Length) ? cooldowns[i] : 0;
        }
    }

    /// <summary>Never null (JsonUtility-safe): an inactive slot exports as an empty-items sentinel.</summary>
    public DeliveryRequest[] ExportRequests()
    {
        var result = new DeliveryRequest[3];
        for (int i = 0; i < 3; i++)
            result[i] = slotRequests[i] ?? new DeliveryRequest { items = new DeliveryLineItem[0], repReward = 0, requesterName = "", flavorText = "" };
        return result;
    }

    public long[] ExportCooldowns() => (long[])slotCooldownEndUtcTicks.Clone();
}
