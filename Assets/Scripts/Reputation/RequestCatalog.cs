using UnityEngine;

public enum RequestItemKind { SpecificId, AnyUnlockedCrop, AnyRawFish, AnySmokedFish }

/// <summary>One line of an authored request: a concrete itemId plus a fixed count.</summary>
[System.Serializable]
public struct AuthoredLine
{
    public string itemId;
    public int count;
}

/// <summary>
/// A hand-authored townsfolk request: who is asking, why, and exactly what they want. Authored
/// requests replace template rolling when the difficulty's array is non-empty (templates remain
/// as a fallback). Counts are value-balanced per difficulty band — see the design spec.
/// </summary>
[System.Serializable]
public class AuthoredRequest
{
    public string requester;
    [Tooltip("Short in-world reason the goods are needed. Shown on the request note.")]
    public string blurb;
    public AuthoredLine[] lines;
    [Tooltip("Relative pick weight. Premium outliers use a low weight so they roll rarely.")]
    public int weight;
    [Tooltip("Reward multiplier vs the difficulty base. >1 for premium outliers whose item value " +
             "far exceeds the band (a single Pike/Smoked Bass cannot be scaled down below qty 1).")]
    public float rewardMultiplier;
}

/// <summary>One catalog line: a category of requestable item plus its count range and pick weight.</summary>
[System.Serializable]
public struct RequestItemTemplate
{
    public RequestItemKind kind;
    [Tooltip("Used only when kind == SpecificId: \"egg\", \"wood\", or \"compost\".")]
    public string specificId;
    public int minCount;
    public int maxCount;
    public int weight;
}

/// <summary>
/// Town Requests generation data (Reputation Phase 2, spec §3.3): per-difficulty item template
/// pools, reward bases, and requester names. ReputationManager expands templates into concrete
/// RequestItemOptions at roll time, filtered by live unlock state.
/// </summary>
[CreateAssetMenu(menuName = "Farm Game/Reputation/Request Catalog", order = 20)]
public class RequestCatalog : ScriptableObject
{
    [Header("Reward base (spec §3.3) + variance")]
    public int easyRepBase = 30;
    public int mediumRepBase = 60;
    public int hardRepBase = 90;
    [Range(0f, 1f)] public float rewardVariance = 0.2f;

    [Header("Item template pools per difficulty")]
    public RequestItemTemplate[] easyPool = new[]
    {
        new RequestItemTemplate { kind = RequestItemKind.AnyUnlockedCrop, minCount = 10, maxCount = 25, weight = 6 },
        new RequestItemTemplate { kind = RequestItemKind.SpecificId, specificId = "egg", minCount = 2, maxCount = 5, weight = 2 },
        new RequestItemTemplate { kind = RequestItemKind.SpecificId, specificId = "wood", minCount = 15, maxCount = 30, weight = 1 },
        new RequestItemTemplate { kind = RequestItemKind.SpecificId, specificId = "compost", minCount = 10, maxCount = 20, weight = 1 },
    };
    public RequestItemTemplate[] mediumPool = new[]
    {
        new RequestItemTemplate { kind = RequestItemKind.AnyUnlockedCrop, minCount = 30, maxCount = 60, weight = 5 },
        new RequestItemTemplate { kind = RequestItemKind.AnyRawFish, minCount = 3, maxCount = 6, weight = 3 },
        new RequestItemTemplate { kind = RequestItemKind.SpecificId, specificId = "wood", minCount = 40, maxCount = 70, weight = 1 },
        new RequestItemTemplate { kind = RequestItemKind.SpecificId, specificId = "compost", minCount = 25, maxCount = 45, weight = 1 },
    };
    public RequestItemTemplate[] hardPool = new[]
    {
        new RequestItemTemplate { kind = RequestItemKind.AnyUnlockedCrop, minCount = 70, maxCount = 140, weight = 4 },
        new RequestItemTemplate { kind = RequestItemKind.AnyRawFish, minCount = 6, maxCount = 12, weight = 3 },
        new RequestItemTemplate { kind = RequestItemKind.AnySmokedFish, minCount = 2, maxCount = 5, weight = 2 },
        new RequestItemTemplate { kind = RequestItemKind.SpecificId, specificId = "wood", minCount = 90, maxCount = 150, weight = 1 },
    };

    public string[] requesterNames = { "Marta", "Old Finch", "Sal", "Widow Bree", "Pip", "Cormac" };
}
