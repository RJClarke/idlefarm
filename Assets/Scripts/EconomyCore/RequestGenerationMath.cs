using UnityEngine;

/// <summary>One weighted, concrete item choice a request slot can roll (Reputation Phase 2, spec §3.3).</summary>
public struct RequestItemOption
{
    public string itemId;
    public int minCount;
    public int maxCount;
    public int weight;
}

/// <summary>
/// Pure weighted-roll math for Town Requests generation. The caller (ReputationManager) is
/// responsible for filtering the pool down to what the player can currently produce before
/// calling this — this class only picks among options it's handed.
/// </summary>
public static class RequestGenerationMath
{
    public static bool TryRoll(RequestItemOption[] pool, System.Random rng, out string itemId, out int count)
    {
        itemId = null;
        count = 0;
        if (pool == null || pool.Length == 0 || rng == null) return false;

        int totalWeight = 0;
        for (int i = 0; i < pool.Length; i++) totalWeight += Mathf.Max(0, pool[i].weight);
        if (totalWeight <= 0) return false;

        int roll = rng.Next(0, totalWeight);
        int acc = 0;
        for (int i = 0; i < pool.Length; i++)
        {
            acc += Mathf.Max(0, pool[i].weight);
            if (roll >= acc) continue;

            itemId = pool[i].itemId;
            int min = Mathf.Max(1, pool[i].minCount);
            int max = Mathf.Max(min, pool[i].maxCount);
            count = rng.Next(min, max + 1); // System.Random.Next max is exclusive
            return true;
        }
        return false;
    }
}
