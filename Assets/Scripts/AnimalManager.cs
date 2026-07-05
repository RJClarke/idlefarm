using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class AnimalManager : MonoBehaviour
{
    public static AnimalManager Instance { get; private set; }

    [SerializeField] private List<AnimalData> allAnimals = new List<AnimalData>();

    private HashSet<string> unlockedAnimalIDs = new HashSet<string>();
    private string equippedAnimalID = null;
    private DateTime lastEggClaimTime = DateTime.MinValue;
    private GameObject activeVisualInstance;

    // Events
    public event Action<AnimalData> OnAnimalEquipped;
    public event Action OnAnimalUnequipped;
    public event Action OnEggReady;    // Fired for coin-reward PassiveTimer animals (chicken)
    public event Action OnEggClaimed;
    public event Action OnGemReady;    // Fired for gem-reward PassiveTimer animals (rooster)
    public event Action OnGemClaimed;
    public event Action<string> OnAnimalUnlocked;

    private bool eggReady = false;
    private bool eggNotified = false;

    private float eggCheckTimer = 0f;
    private const float EGG_CHECK_INTERVAL = 1f;

    // Cow passive compost accumulator (UtcNow-based so it works while app is closed)
    private DateTime lastCompostTickUtc = DateTime.MinValue;
    private float compostTickAccumulator;
    private const float COMPOST_TICK_INTERVAL_SECS = 5f;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    private void Start()
    {
        if (RunManager.Instance != null)
        {
            RunManager.Instance.OnRunStarted += OnRunStarted;
            RunManager.Instance.OnRunEnded += OnRunEnded;
        }
    }

    private void OnDestroy()
    {
        if (RunManager.Instance != null)
        {
            RunManager.Instance.OnRunStarted -= OnRunStarted;
            RunManager.Instance.OnRunEnded -= OnRunEnded;
        }
    }

    private void Update()
    {
        eggCheckTimer += Time.deltaTime;
        if (eggCheckTimer >= EGG_CHECK_INTERVAL)
        {
            eggCheckTimer = 0f;
            UpdatePassiveTimer();
        }

        compostTickAccumulator += Time.unscaledDeltaTime;
        if (compostTickAccumulator >= COMPOST_TICK_INTERVAL_SECS)
        {
            compostTickAccumulator = 0f;
            TickCompost();
        }
    }

    /// <summary>Amount of compost granted by the most recent "long gap" cow tick (offline catch-up). 0 if the most recent tick was a normal short interval.</summary>
    public int LastOfflineCompostGain { get; private set; }

    private void TickCompost()
    {
        AnimalData equipped = GetEquippedAnimal();
        if (equipped == null || equipped.compostPerMinute <= 0f) return;
        if (CurrencyManager.Instance == null) return;
        if (lastCompostTickUtc == DateTime.MinValue) lastCompostTickUtc = DateTime.UtcNow;

        double elapsedMin = (DateTime.UtcNow - lastCompostTickUtc).TotalMinutes;
        // Forward-only: a backward clock (tick timestamp now in the future) re-anchors to now so
        // the trickle resumes instead of freezing until real time catches up — never grants negatively.
        if (elapsedMin <= 0) { lastCompostTickUtc = DateTime.UtcNow; return; }

        float ratePerMin = equipped.compostPerMinute;
        if (ResearchManager.Instance != null)
            ratePerMin *= 1f + ResearchManager.Instance.GetBonus(Research.StatKey.CowPassiveCompost);

        int amount = Mathf.FloorToInt((float)(elapsedMin * ratePerMin));
        if (amount <= 0) return;

        CurrencyManager.Instance.AddCompost(amount);
        // Advance the timestamp by the minutes we just credited (avoids drift).
        double minutesAwarded = amount / ratePerMin;
        lastCompostTickUtc = lastCompostTickUtc.AddMinutes(minutesAwarded);

        // Flag any tick that delivered "a meaningful chunk" so the welcome-back modal can show it.
        // Normal in-app ticks are bounded by COMPOST_TICK_INTERVAL_SECS; anything past ~5 min was offline.
        if (elapsedMin >= 5.0) LastOfflineCompostGain = amount;
        else if (activeVisualInstance != null)
            // Online trickle: float the gain off the cow so the player can see it accruing.
            FloatingTextManager.ShowCompost(amount, activeVisualInstance.transform.position);
    }

    /// <summary>Force an immediate compost catch-up. Called by OfflineProgressManager so the welcome-back modal has fresh numbers without waiting for the periodic Update tick.</summary>
    public int RunOfflineCompostCatchUp()
    {
        LastOfflineCompostGain = 0;
        TickCompost();
        return LastOfflineCompostGain;
    }

    /// <summary>
    /// If a run was active (continued offline), estimate + award the compost the equipped cow
    /// would have earned by eating crops over the offline window, credited at max game speed
    /// (matching the run timer's offline model). Returns the awarded amount for the welcome-back modal.
    /// </summary>
    public int RunOfflineCowEatingCatchUp(double offlineSeconds)
    {
        if (offlineSeconds <= 0) return 0;
        if (RunManager.Instance == null || !RunManager.Instance.IsRunActive) return 0; // only a continued offline run
        AnimalData equipped = GetEquippedAnimal();
        if (equipped == null || equipped.animalID != "cow") return 0;

        Cow cow = activeVisualInstance != null ? activeVisualInstance.GetComponent<Cow>() : null;
        if (cow == null) return 0;

        float maxSpeed = 1f + (ResearchManager.Instance != null
            ? ResearchManager.Instance.GetBonus(Research.StatKey.GameSpeed)
            : 0f);
        double inGameSeconds = offlineSeconds * Mathf.Max(1f, maxSpeed);

        int amount = cow.EstimateOfflineEatingCompost(inGameSeconds);
        if (amount > 0 && CurrencyManager.Instance != null)
        {
            CurrencyManager.Instance.AddCompost(amount);
            Debug.Log($"Offline cow grazing: +{amount} compost (~{inGameSeconds:F0}s in-game)");
        }
        return amount;
    }

    public string GetLastCompostTimeISO() =>
        lastCompostTickUtc == DateTime.MinValue ? "" : lastCompostTickUtc.ToString("o");

    public void LoadCompostTime(string iso)
    {
        if (string.IsNullOrEmpty(iso)) { lastCompostTickUtc = DateTime.MinValue; return; }
        if (DateTime.TryParse(iso, null, System.Globalization.DateTimeStyles.RoundtripKind, out var t))
            lastCompostTickUtc = t.ToUniversalTime();
    }

    // ── Data Access ──────────────────────────────

    public List<AnimalData> GetAllAnimals()
    {
        return allAnimals.OrderBy(a => a.sortOrder).ToList();
    }

    public AnimalData GetAnimalData(string animalID)
    {
        return allAnimals.Find(a => a.animalID == animalID);
    }

    public AnimalData GetEquippedAnimal()
    {
        if (string.IsNullOrEmpty(equippedAnimalID)) return null;
        return GetAnimalData(equippedAnimalID);
    }

    public string GetEquippedAnimalID()
    {
        return equippedAnimalID ?? "";
    }

    // ── Unlock ──────────────────────────────

    public bool IsUnlocked(string animalID)
    {
        return unlockedAnimalIDs.Contains(animalID);
    }

    public bool TryUnlockAnimal(string animalID)
    {
        AnimalData data = GetAnimalData(animalID);
        if (data == null)
        {
            Debug.LogWarning($"AnimalManager: Unknown animal ID: {animalID}");
            return false;
        }

        if (IsUnlocked(animalID))
        {
            Debug.LogWarning($"AnimalManager: {animalID} already unlocked");
            return false;
        }

        if (!CurrencyManager.Instance.CanAffordGems(data.gemCost))
        {
            Debug.LogWarning($"AnimalManager: Not enough gems for {animalID}. Need {data.gemCost}");
            return false;
        }

        CurrencyManager.Instance.SpendGems(data.gemCost);
        unlockedAnimalIDs.Add(animalID);
        Debug.Log($"Unlocked animal: {data.displayName} for {data.gemCost} gems");
        OnAnimalUnlocked?.Invoke(animalID);
        return true;
    }

    // ── Equip / Unequip ──────────────────────────────

    public void EquipAnimal(string animalID)
    {
        if (!IsUnlocked(animalID))
        {
            Debug.LogWarning($"AnimalManager: Cannot equip locked animal: {animalID}");
            return;
        }

        // Unequip current first
        if (!string.IsNullOrEmpty(equippedAnimalID))
        {
            DestroyActiveVisual();
        }

        equippedAnimalID = animalID;
        AnimalData data = GetAnimalData(animalID);
        Debug.Log($"Equipped animal: {data.displayName}");

        SpawnAnimalVisual(data);
        OnAnimalEquipped?.Invoke(data);
    }

    public void UnequipAnimal()
    {
        if (string.IsNullOrEmpty(equippedAnimalID)) return;

        DestroyActiveVisual();
        equippedAnimalID = null;
        eggReady = false;
        eggNotified = false;
        Debug.Log("Unequipped animal");
        OnAnimalUnequipped?.Invoke();
    }

    // ── Passive Timer (PassiveTimer) ──────────────────────────────

    public bool IsPassiveReady => eggReady;
    public bool IsEggReady => eggReady; // kept for legacy references

    public void ForcePassiveReady()
    {
        lastEggClaimTime = DateTime.MinValue;
        UpdatePassiveTimer();
    }

    public float GetCooldownProgress()
    {
        AnimalData equipped = GetEquippedAnimal();
        if (equipped == null || equipped.abilityType != AnimalAbilityType.PassiveTimer)
            return 0f;

        double elapsedMinutes = (DateTime.UtcNow - lastEggClaimTime).TotalMinutes;
        float effectiveCooldown = EffectiveCooldownMinutes(equipped);
        return Mathf.Clamp01((float)(elapsedMinutes / effectiveCooldown));
    }

    private static float EffectiveCooldownMinutes(AnimalData a)
    {
        if (a == null) return 1f;
        if (ResearchManager.Instance == null) return a.cooldownMinutes;
        string key = a.animalID switch
        {
            "chicken" => Research.StatKey.ChickenCooldown,
            "rooster" => Research.StatKey.RoosterCooldown,
            _ => null
        };
        if (string.IsNullOrEmpty(key)) return a.cooldownMinutes;
        float bonus = ResearchManager.Instance.GetBonus(key);
        return a.cooldownMinutes / Mathf.Max(0.01f, 1f + bonus);
    }

    private static int EffectiveReward(AnimalData a, int baseReward)
    {
        if (a == null || ResearchManager.Instance == null) return baseReward;
        string key = a.animalID switch
        {
            "chicken" => Research.StatKey.ChickenEfficiency,
            "rooster" => Research.StatKey.RoosterEfficiency,
            _ => null
        };
        if (string.IsNullOrEmpty(key)) return baseReward;
        float bonus = ResearchManager.Instance.GetBonus(key);
        return Mathf.RoundToInt(baseReward * (1f + bonus));
    }

    public void ClaimEgg() => ClaimPassiveReward(); // legacy alias

    public void ClaimPassiveReward()
    {
        AnimalData equipped = GetEquippedAnimal();
        if (equipped == null || equipped.abilityType != AnimalAbilityType.PassiveTimer) return;
        if (!eggReady) return;

        bool isGemAnimal = equipped.rewardGems > 0;
        Vector3 rewardWorldPos = activeVisualInstance != null
            ? activeVisualInstance.transform.position + Vector3.down * 0.2f
            : Vector3.zero;

        AnimalVisual visual = activeVisualInstance?.GetComponent<AnimalVisual>();

        if (isGemAnimal)
        {
            int reward = EffectiveReward(equipped, equipped.rewardGems);
            CurrencyManager.Instance.AddGems(reward);
            Debug.Log($"Claimed gems! +{reward} gems");
            if (visual != null) visual.RemoveGem();
            FloatingTextManager.ShowGems(reward, rewardWorldPos);
        }
        else
        {
            int reward = EffectiveReward(equipped, equipped.rewardCoins);
            CurrencyManager.Instance.AddCoins(reward);
            Debug.Log($"Claimed egg! +{reward} coins");
            if (visual != null) visual.RemoveEgg();
            FloatingTextManager.ShowCoins(reward, rewardWorldPos);
        }

        // Defensive: zap any leftover egg/gem GameObjects in the scene. The active visual's
        // RemoveEgg/RemoveGem call above only cleans up its own tracked instance; this catches
        // orphans from earlier animal swaps or scene reloads.
        AnimalVisual.CleanupAllOrphanDrops();

        lastEggClaimTime = DateTime.UtcNow;
        eggReady = false;
        eggNotified = false;

        if (isGemAnimal) OnGemClaimed?.Invoke();
        else OnEggClaimed?.Invoke();
    }

    private void UpdatePassiveTimer()
    {
        AnimalData equipped = GetEquippedAnimal();
        if (equipped == null || equipped.abilityType != AnimalAbilityType.PassiveTimer) return;

        bool isGemAnimal = equipped.rewardGems > 0;
        double elapsedMinutes = (DateTime.UtcNow - lastEggClaimTime).TotalMinutes;
        // Forward-only: a backward clock (claim time now in the future) re-anchors to now so the
        // egg/gem timer resumes counting instead of freezing until real time catches up.
        if (elapsedMinutes < 0) { lastEggClaimTime = DateTime.UtcNow; return; }
        float effectiveCooldown = EffectiveCooldownMinutes(equipped);

        if (!eggReady && elapsedMinutes >= effectiveCooldown)
        {
            eggReady = true;

            if (!eggNotified)
            {
                eggNotified = true;

                AnimalVisual visual = activeVisualInstance?.GetComponent<AnimalVisual>();
                if (visual != null)
                {
                    if (isGemAnimal) visual.DropGem();
                    else visual.DropEgg();
                }

                if (isGemAnimal) OnGemReady?.Invoke();
                else OnEggReady?.Invoke();
            }
        }
    }

    // ── Visual Spawning ──────────────────────────────

    private void SpawnAnimalVisual(AnimalData data)
    {
        if (data.visualPrefab == null)
        {
            Debug.LogWarning($"AnimalManager: No visual prefab for {data.animalID}");
            return;
        }

        Vector3 spawnPos = GetHomeScreenSpawnPosition();
        activeVisualInstance = Instantiate(data.visualPrefab, spawnPos, Quaternion.identity);

        AnimalVisual visual = activeVisualInstance.GetComponent<AnimalVisual>();
        if (visual == null)
        {
            visual = activeVisualInstance.AddComponent<AnimalVisual>();
        }
        visual.Initialize(data);
    }

    private void DestroyActiveVisual()
    {
        if (activeVisualInstance != null)
        {
            Destroy(activeVisualInstance);
            activeVisualInstance = null;
        }
    }

    private Vector3 GetHomeScreenSpawnPosition()
    {
        Camera cam = Camera.main;
        if (cam == null) return Vector3.zero;

        // Spawn near bottom-center of visible area
        Vector3 bottomCenter = cam.ViewportToWorldPoint(new Vector3(0.5f, 0.3f, cam.nearClipPlane));
        bottomCenter.z = 0;
        return bottomCenter;
    }

    // ── Run Integration ──────────────────────────────

    private void OnRunStarted()
    {
        AnimalData equipped = GetEquippedAnimal();
        if (equipped == null) return;

        if (equipped.abilityType == AnimalAbilityType.RunDefender)
        {
            ActivateRunDefender(equipped);
        }
    }

    private void OnRunEnded()
    {
        AnimalData equipped = GetEquippedAnimal();
        if (equipped == null) return;

        if (equipped.abilityType == AnimalAbilityType.RunDefender)
        {
            DeactivateRunDefender();
        }
    }

    private void ActivateRunDefender(AnimalData data)
    {
        if (data.animalID == "farm_dog" && activeVisualInstance != null)
        {
            AnimalVisual visual = activeVisualInstance.GetComponent<AnimalVisual>();
            if (visual != null) visual.PauseWander = true;

            FarmDog dog = activeVisualInstance.GetComponent<FarmDog>();
            if (dog != null)
            {
                dog.ActivateChaseMode();
            }
        }
    }

    private void DeactivateRunDefender()
    {
        if (activeVisualInstance != null)
        {
            FarmDog dog = activeVisualInstance.GetComponent<FarmDog>();
            if (dog != null)
            {
                dog.DeactivateChaseMode();
            }

            AnimalVisual visual = activeVisualInstance.GetComponent<AnimalVisual>();
            if (visual != null) visual.PauseWander = false;
        }
    }

    // ── Save / Load ──────────────────────────────

    public string[] GetUnlockedAnimalIDs()
    {
        return unlockedAnimalIDs.ToArray();
    }

    public string GetLastEggClaimTimeISO()
    {
        if (lastEggClaimTime == DateTime.MinValue) return "";
        return lastEggClaimTime.ToString("o");
    }

    public void LoadState(string[] unlockedIDs, string equippedID, string eggTimeISO)
    {
        unlockedAnimalIDs.Clear();

        if (unlockedIDs != null)
        {
            foreach (string id in unlockedIDs)
            {
                if (!string.IsNullOrEmpty(id))
                    unlockedAnimalIDs.Add(id);
            }
        }

        // Restore egg timer
        if (!string.IsNullOrEmpty(eggTimeISO))
        {
            if (DateTime.TryParse(eggTimeISO, null, System.Globalization.DateTimeStyles.RoundtripKind, out DateTime parsed))
            {
                lastEggClaimTime = parsed;
            }
        }

        // Re-equip animal (spawns visual)
        if (!string.IsNullOrEmpty(equippedID) && IsUnlocked(equippedID))
        {
            EquipAnimal(equippedID);
        }
    }

    [ContextMenu("Add 100 Gems (Test)")]
    private void TestAdd100Gems()
    {
        CurrencyManager.Instance.AddGems(100);
    }

    [ContextMenu("Add 1000 Gems (Test)")]
    private void TestAdd1000Gems()
    {
        CurrencyManager.Instance.AddGems(1000);
    }
}
