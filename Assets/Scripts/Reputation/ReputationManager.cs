using System;
using UnityEngine;

/// <summary>
/// Town Requests board owner: rolls/expires the 3 fixed-difficulty slots, tracks rep/points via
/// ReputationCore, and exposes Fulfill/Skip. Spec: 2026-07-19-reputation-design.md §3.
/// </summary>
public class ReputationManager : MonoBehaviour
{
    public static ReputationManager Instance { get; private set; }

    [SerializeField] private RequestCatalog catalog;
    [SerializeField] private CropDatabase cropDatabase;
    [SerializeField] private double cooldownHours = 8.0;

    private readonly ReputationCore core = new ReputationCore();
    private long CooldownTicks => (long)(cooldownHours * TimeSpan.TicksPerHour);

    public event Action OnChanged;
    public event Action<int> OnPointsAwarded;
    public event Action OnRequestFulfilled; // one Town Request delivered

    public int BarProgress => core.BarProgress;
    public int PointsEarned => core.PointsEarned;
    public int UnspentPoints => core.UnspentPoints;
    public int NextPointCost => ReputationMath.PointCost(core.PointsEarned + 1);
    public float BarProgress01 => NextPointCost <= 0 ? 0f : Mathf.Clamp01((float)core.BarProgress / NextPointCost);
    public int NextSkipCost => core.NextSkipCost();

    public DeliveryRequest GetSlotRequest(int slot) => core.GetSlotRequest(slot);
    public bool IsSlotOnCooldown(int slot) => core.IsSlotOnCooldown(slot, DateTime.UtcNow.Ticks);
    public double GetSlotCooldownRemainingSeconds(int slot)
    {
        long remainingTicks = core.GetSlotCooldownEndUtcTicks(slot) - DateTime.UtcNow.Ticks;
        return remainingTicks <= 0 ? 0 : remainingTicks / (double)TimeSpan.TicksPerSecond;
    }

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    public const string BoardIntroLetterFlag = "letter:town_board_intro";
    public const string WelcomeBasketFlag = "first_request_done";

    // A new player's first request after the board letter: 8 Radish (the one crop every farm owns),
    // from the Mayor, worth exactly one Barn point. Pinned to slot 0 until delivered; can't be skipped.
    private void EnsureWelcomeBasket()
    {
        var nm = NarrativeManager.Instance;
        if (nm == null || !nm.HasFired(BoardIntroLetterFlag) || nm.HasFired(WelcomeBasketFlag)) return;
        DeliveryRequest current = core.GetSlotRequest(0);
        if (current != null && current.isWelcomeBasket) return;
        core.SetSlotRequest(0, new DeliveryRequest
        {
            items = new[] { new DeliveryLineItem { itemId = "Radish", count = 8 } },
            repReward = ReputationMath.WelcomeBasketReward(core.BarProgress, core.PointsEarned),
            requesterName = "Mayor Bramble",
            flavorText = "A welcome basket for the new families in town. Radishes, if you can spare them!",
            isWelcomeBasket = true,
        });
        OnChanged?.Invoke();
    }

    private void Update()
    {
        EnsureWelcomeBasket();
        long now = DateTime.UtcNow.Ticks;
        for (int slot = 0; slot < 3; slot++)
        {
            if (core.GetSlotRequest(slot) == null && !core.IsSlotOnCooldown(slot, now))
                RollSlot(slot);
        }
    }

    public bool TryFulfill(int slot)
    {
        DeliveryRequest request = core.GetSlotRequest(slot);
        if (request == null || !DeliveryService.CanFulfill(request)) return false;
        if (!DeliveryService.TryFulfill(request)) return false;

        core.OnFulfilled(slot, DateTime.UtcNow.Ticks, CooldownTicks);
        if (request.isWelcomeBasket) NarrativeManager.Instance?.MarkFired(WelcomeBasketFlag);
        int awarded = core.AddRep(request.repReward);
        Debug.Log($"[Reputation] Fulfilled slot {slot} (+{request.repReward} rep)");
        OnChanged?.Invoke();
        OnRequestFulfilled?.Invoke();
        if (awarded > 0) OnPointsAwarded?.Invoke(awarded);
        return true;
    }

    public bool TrySpendPoint()
    {
        bool ok = core.TrySpendPoint();
        if (ok) OnChanged?.Invoke();
        return ok;
    }

    /// <summary>Dev-tools only: grants Barn skill points outside the normal rep-bar earn path.</summary>
    public void DevAddPoints(int amount)
    {
        core.DevAddUnspentPoints(amount);
        OnChanged?.Invoke();
    }

    public bool TrySkip(int slot)
    {
        DeliveryRequest request = core.GetSlotRequest(slot);
        if (request == null) return false;
        if (request.isWelcomeBasket) return false; // the tutorial request can't be skipped
        int cost = core.NextSkipCost();
        if (CurrencyManager.Instance == null || !CurrencyManager.Instance.SpendGems(cost)) return false;

        core.OnSkipped();
        core.SetSlotRequest(slot, null); // Update() rolls a fresh one immediately (no cooldown)
        Debug.Log($"[Reputation] Skipped slot {slot} for {cost} gems");
        OnChanged?.Invoke();
        return true;
    }

    private void RollSlot(int slot)
    {
        if (catalog == null) return;
        // Hand-authored townsfolk requests are the primary source; the old template roller stays
        // as a fallback for when nothing in the authored pool is currently obtainable.
        if (TryRollAuthored(slot)) return;
        RollFromTemplates(slot);
    }

    /// <summary>
    /// Weighted pick from the authored pool, filtered to requests whose every item is currently
    /// obtainable. Premium outliers carry a low weight and a reward multiplier.
    /// </summary>
    private bool TryRollAuthored(int slot)
    {
        AuthoredRequest[] pool = TownRequestContent.ForSlot(slot);
        if (pool == null || pool.Length == 0) return false;

        int totalWeight = 0;
        foreach (AuthoredRequest candidate in pool)
            if (IsRequestAvailable(candidate)) totalWeight += Mathf.Max(1, candidate.weight);
        if (totalWeight <= 0) return false;

        int roll = UnityEngine.Random.Range(0, totalWeight);
        AuthoredRequest chosen = null;
        foreach (AuthoredRequest candidate in pool)
        {
            if (!IsRequestAvailable(candidate)) continue;
            roll -= Mathf.Max(1, candidate.weight);
            if (roll < 0) { chosen = candidate; break; }
        }
        if (chosen == null) return false;

        int repBase = slot == 0 ? catalog.easyRepBase : slot == 1 ? catalog.mediumRepBase : catalog.hardRepBase;
        float varianceRoll = 1f + UnityEngine.Random.Range(-catalog.rewardVariance, catalog.rewardVariance);
        float multiplier = chosen.rewardMultiplier <= 0f ? 1f : chosen.rewardMultiplier;
        int reward = Mathf.Max(1, Mathf.RoundToInt(repBase * multiplier * varianceRoll));

        var items = new DeliveryLineItem[chosen.lines.Length];
        for (int i = 0; i < chosen.lines.Length; i++)
            items[i] = new DeliveryLineItem { itemId = chosen.lines[i].itemId, count = chosen.lines[i].count };

        core.SetSlotRequest(slot, new DeliveryRequest
        {
            items = items,
            repReward = reward,
            requesterName = chosen.requester,
            flavorText = chosen.blurb,
        });
        OnChanged?.Invoke();
        return true;
    }

    /// <summary>A request is only offered once every item in it is actually obtainable.</summary>
    private bool IsRequestAvailable(AuthoredRequest request)
    {
        if (request?.lines == null || request.lines.Length == 0) return false;
        foreach (AuthoredLine line in request.lines)
            if (line.count <= 0 || !IsItemAvailable(line.itemId)) return false;
        return true;
    }

    private bool IsItemAvailable(string id)
    {
        if (string.IsNullOrEmpty(id)) return false;
        if (id == "wood") return WoodcuttingManager.Instance != null && WoodcuttingManager.Instance.HasAxe;
        if (id == "egg" || id == "compost") return true; // always available in v1 (spec §9 deviation)
        if (id.StartsWith("fish_raw_")) return FishingManager.Instance != null && FishingManager.Instance.HasPole;
        if (id.StartsWith("fish_smoked_")) return SmokehouseManager.Instance != null && SmokehouseManager.Instance.IsBuilt;
        return IsCropUnlocked(id);
    }

    // Requests only ask for crops the farm can actually grow.
    private bool IsCropUnlocked(string cropName)
    {
        CropData crop = cropDatabase != null ? cropDatabase.GetCropByName(cropName) : null;
        return crop != null && CropOwnership.IsOwned(crop);
    }

    private void RollFromTemplates(int slot)
    {
        RequestItemTemplate[] templates = slot == 0 ? catalog.easyPool : slot == 1 ? catalog.mediumPool : catalog.hardPool;
        int repBase = slot == 0 ? catalog.easyRepBase : slot == 1 ? catalog.mediumRepBase : catalog.hardRepBase;

        RequestItemOption[] pool = BuildPool(templates);
        var rng = new System.Random();
        if (!RequestGenerationMath.TryRoll(pool, rng, out string itemId, out int count)) return;

        float varianceRoll = 1f + UnityEngine.Random.Range(-catalog.rewardVariance, catalog.rewardVariance);
        int reward = Mathf.Max(1, Mathf.RoundToInt(repBase * varianceRoll));
        string requester = catalog.requesterNames != null && catalog.requesterNames.Length > 0
            ? catalog.requesterNames[UnityEngine.Random.Range(0, catalog.requesterNames.Length)]
            : "A neighbor";

        var request = new DeliveryRequest
        {
            items = new[] { new DeliveryLineItem { itemId = itemId, count = count } },
            repReward = reward,
            requesterName = requester,
            flavorText = "",
        };
        core.SetSlotRequest(slot, request);
        OnChanged?.Invoke();
    }

    private RequestItemOption[] BuildPool(RequestItemTemplate[] templates)
    {
        var options = new System.Collections.Generic.List<RequestItemOption>();
        if (templates == null) return options.ToArray();

        foreach (RequestItemTemplate t in templates)
        {
            switch (t.kind)
            {
                case RequestItemKind.SpecificId:
                    if (IsSpecificIdAvailable(t.specificId))
                        options.Add(new RequestItemOption { itemId = t.specificId, minCount = t.minCount, maxCount = t.maxCount, weight = t.weight });
                    break;
                case RequestItemKind.AnyUnlockedCrop:
                    if (cropDatabase != null)
                        foreach (CropData crop in CropOwnership.Owned(cropDatabase))
                            if (crop != null)
                                options.Add(new RequestItemOption { itemId = crop.cropName, minCount = t.minCount, maxCount = t.maxCount, weight = t.weight });
                    break;
                case RequestItemKind.AnyRawFish:
                    if (FishingManager.Instance != null && FishingManager.Instance.HasPole)
                        for (int tier = 1; tier <= FishTiers.Count; tier++)
                            options.Add(new RequestItemOption { itemId = "fish_raw_" + tier, minCount = t.minCount, maxCount = t.maxCount, weight = t.weight });
                    break;
                case RequestItemKind.AnySmokedFish:
                    if (SmokehouseManager.Instance != null && SmokehouseManager.Instance.IsBuilt)
                        for (int tier = 1; tier <= FishTiers.Count; tier++)
                            options.Add(new RequestItemOption { itemId = "fish_smoked_" + tier, minCount = t.minCount, maxCount = t.maxCount, weight = t.weight });
                    break;
            }
        }
        return options.ToArray();
    }

    private static bool IsSpecificIdAvailable(string id)
    {
        if (id == "wood") return WoodcuttingManager.Instance != null && WoodcuttingManager.Instance.HasAxe;
        return true; // "egg", "compost" — always available in v1 (spec §9 deviation)
    }

    public void CaptureTo(GameData d)
    {
        d.repBarProgress = core.BarProgress;
        d.repPointsEarned = core.PointsEarned;
        d.repUnspentPoints = core.UnspentPoints;
        d.repConsecutiveSkips = core.ConsecutiveSkips;

        DeliveryRequest[] requests = core.ExportRequests();
        long[] cooldowns = core.ExportCooldowns();
        d.repSlots = new RequestSlotSave[3];
        for (int i = 0; i < 3; i++)
            d.repSlots[i] = new RequestSlotSave { request = requests[i], cooldownEndUtcTicks = cooldowns[i] };
    }

    public void LoadFrom(GameData d)
    {
        var requests = new DeliveryRequest[3];
        var cooldowns = new long[3];
        if (d.repSlots != null)
            for (int i = 0; i < Mathf.Min(3, d.repSlots.Length); i++)
            {
                requests[i] = d.repSlots[i]?.request;
                cooldowns[i] = d.repSlots[i]?.cooldownEndUtcTicks ?? 0;
            }
        core.Import(d.repBarProgress, d.repPointsEarned, d.repUnspentPoints, d.repConsecutiveSkips, requests, cooldowns);
        OnChanged?.Invoke();
    }
}
