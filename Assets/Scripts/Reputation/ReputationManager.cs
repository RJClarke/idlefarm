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

    private void Update()
    {
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
        int awarded = core.AddRep(request.repReward);
        Debug.Log($"[Reputation] Fulfilled slot {slot} (+{request.repReward} rep)");
        OnChanged?.Invoke();
        if (awarded > 0) OnPointsAwarded?.Invoke(awarded);
        return true;
    }

    public bool TrySpendPoint()
    {
        bool ok = core.TrySpendPoint();
        if (ok) OnChanged?.Invoke();
        return ok;
    }

    public bool TrySkip(int slot)
    {
        DeliveryRequest request = core.GetSlotRequest(slot);
        if (request == null) return false;
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
                        foreach (CropData crop in cropDatabase.startingCrops)
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
