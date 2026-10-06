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

    // Rooster (gem) reward carry: the effective gem reward is fractional (e.g. 2.4 at mid
    // efficiency), so we accumulate the remainder and roll it into the next claim instead of
    // rounding each drop away — 2.4 → grant 2 (carry .4); next 2.8 → 2 (carry .8); next 3.2 → 3…
    private double gemRewardCarry = 0.0;

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

            // Resume race: SaveManager.ResumeRun fires OnRunStarted during load, possibly before
            // this Start() subscribed - without catch-up the RunDefender never activates.
            if (RunManager.Instance.IsRunActive) OnRunStarted();
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


    public string GetLastCompostTimeISO() =>
        lastCompostTickUtc == DateTime.MinValue ? "" : lastCompostTickUtc.ToString("o");

    public void LoadCompostTime(string iso, long fallbackLastSeenUtcTicks = 0)
    {
        if (!string.IsNullOrEmpty(iso)
            && DateTime.TryParse(iso, null, System.Globalization.DateTimeStyles.RoundtripKind, out var t))
        {
            lastCompostTickUtc = t.ToUniversalTime();
            return;
        }

        // No saved anchor (e.g. a compost animal was equipped but never ticked before the app closed).
        // If we know when the app was last online AND a compost animal was the equipped one at save
        // time, credit the away window from there rather than losing it. Gated on the equipped animal
        // so we never credit compost for a window when no cow was equipped. Requires the caller to have
        // already restored the equipped animal (SaveManager loads AnimalManager state before this).
        AnimalData equipped = GetEquippedAnimal();
        lastCompostTickUtc = (fallbackLastSeenUtcTicks > 0 && equipped != null && equipped.compostPerMinute > 0f)
            ? new DateTime(fallbackLastSeenUtcTicks, DateTimeKind.Utc)
            : DateTime.MinValue;
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

        // Start the passive-compost clock immediately on equip. Otherwise the anchor is only set by
        // the periodic (every 5s) TickCompost, so equipping a cow and closing the app within a few
        // seconds saves an empty anchor — and the next launch's offline catch-up would credit +0 for
        // the whole away window instead of the compost the cow earned.
        if (data != null && data.compostPerMinute > 0f && lastCompostTickUtc == DateTime.MinValue)
            lastCompostTickUtc = DateTime.UtcNow;

        SpawnAnimalVisual(data);
        OnAnimalEquipped?.Invoke(data);

        // If equipped mid-run, start its run-defender behavior now — otherwise chase mode would
        // only kick in on the next run (the dog would just wander for the rest of this one).
        if (RunManager.Instance != null && RunManager.Instance.IsRunActive
            && data != null && data.abilityType.HasFlag(AnimalAbilityType.RunDefender))
        {
            ActivateRunDefender(data);
        }
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
        if (equipped == null || !equipped.abilityType.HasFlag(AnimalAbilityType.PassiveTimer))
            return 0f;

        double elapsedMinutes = (DateTime.UtcNow - lastEggClaimTime).TotalMinutes;
        float effectiveCooldown = EffectiveCooldownMinutes(equipped);
        return Mathf.Clamp01((float)(elapsedMinutes / effectiveCooldown));
    }

    /// <summary>Research stat that shortens this animal's gift cooldown (null if none). Shared with the Almanac.</summary>
    public static string CooldownResearchKey(AnimalData a) => a == null ? null : a.animalID switch
    {
        "chicken" => Research.StatKey.ChickenCooldown,
        "rooster" => Research.StatKey.RoosterCooldown,
        _ => null
    };

    /// <summary>Research stat that raises this animal's gift reward (null if none). Shared with the Almanac.</summary>
    public static string RewardResearchKey(AnimalData a) => a == null ? null : a.animalID switch
    {
        "chicken" => Research.StatKey.ChickenEfficiency,
        "rooster" => Research.StatKey.RoosterEfficiency,
        _ => null
    };

    private static float EffectiveCooldownMinutes(AnimalData a)
    {
        if (a == null) return 1f;
        if (ResearchManager.Instance == null) return a.cooldownMinutes;
        string key = CooldownResearchKey(a);
        if (string.IsNullOrEmpty(key)) return a.cooldownMinutes;
        float bonus = ResearchManager.Instance.GetBonus(key);
        return a.cooldownMinutes / Mathf.Max(0.01f, 1f + bonus);
    }

    private static int EffectiveReward(AnimalData a, int baseReward)
    {
        if (a == null || ResearchManager.Instance == null) return baseReward;
        string key = RewardResearchKey(a);
        if (string.IsNullOrEmpty(key)) return baseReward;
        float bonus = ResearchManager.Instance.GetBonus(key);
        return Mathf.RoundToInt(baseReward * (1f + bonus));
    }

    /// <summary>Exact (unrounded) efficiency-scaled reward. Used by the gem carry-over path so the
    /// fractional part isn't lost to rounding — see <see cref="gemRewardCarry"/>.</summary>
    private static double EffectiveRewardExact(AnimalData a, int baseReward)
    {
        if (a == null || ResearchManager.Instance == null) return baseReward;
        string key = RewardResearchKey(a);
        if (string.IsNullOrEmpty(key)) return baseReward;
        return baseReward * (1.0 + ResearchManager.Instance.GetBonus(key));
    }

    public void ClaimEgg() => ClaimPassiveReward(); // legacy alias

    /// <summary>Count a tapped egg/gem toward this run's animal stats (only during a run).</summary>
    private static void RecordGiftForRun(AnimalData a, int eggs, int gems, int coins)
    {
        if (a == null || RunStats.Instance == null || RunManager.Instance == null || !RunManager.Instance.IsRunActive) return;
        RunStats.Instance.AddAnimalGift(a.animalID, eggs, gems, coins);
    }

    public void ClaimPassiveReward()
    {
        AnimalData equipped = GetEquippedAnimal();
        if (equipped == null || !equipped.abilityType.HasFlag(AnimalAbilityType.PassiveTimer)) return;
        if (!eggReady) return;

        bool isGemAnimal = equipped.rewardGems > 0;
        Vector3 rewardWorldPos = activeVisualInstance != null
            ? activeVisualInstance.transform.position + Vector3.down * 0.2f
            : Vector3.zero;

        AnimalVisual visual = activeVisualInstance?.GetComponent<AnimalVisual>();

        if (isGemAnimal)
        {
            // Fractional carry: accumulate the exact (unrounded) reward and grant the whole part,
            // keeping the remainder for next time so no partial gems are lost to rounding.
            double total = gemRewardCarry + EffectiveRewardExact(equipped, equipped.rewardGems);
            int reward = (int)System.Math.Floor(total);
            gemRewardCarry = total - reward;
            CurrencyManager.Instance.AddGems(reward);
            RecordGiftForRun(equipped, 0, reward, 0);
            Debug.Log($"Claimed gems! +{reward} gems (carry {gemRewardCarry:F2})");
            if (visual != null) visual.RemoveGem();
            FloatingTextManager.ShowGems(reward, rewardWorldPos);
        }
        else
        {
            // Collect mode (Reputation Phase 1): bank the egg as an inventory item instead of
            // coins. Full egg stack falls back to the normal coin payout.
            // Ranching skill milestones (Lv 5-20): chance the egg counts double.
            bool doubleEgg = FarmSkillsManager.RollMilestone(FarmSkillTrack.Ranching);
            int eggsBanked = 0;
            if (ItemInventoryManager.Instance != null && ItemInventoryManager.Instance.CollectMode
                && (eggsBanked = ItemInventoryManager.Instance.AddEggs(doubleEgg ? 2 : 1, out _)) > 0)
            {
                RecordGiftForRun(equipped, eggsBanked, 0, 0);
                Debug.Log($"Claimed egg into inventory (+{eggsBanked} egg)");
                if (visual != null) visual.RemoveEgg();
                FloatingTextManager.ShowText(eggsBanked > 1 ? $"+{eggsBanked} Eggs" : "+1 Egg", new Color(0.55f, 0.8f, 0.35f), rewardWorldPos);
            }
            else
            {
                int reward = EffectiveReward(equipped, equipped.rewardCoins);
                if (FarmSkillsManager.Instance != null)
                    reward = Mathf.RoundToInt(reward * (1f + FarmSkillsManager.Instance.GetBonus(FarmSkillTrack.Ranching)));
                if (doubleEgg) reward *= 2;
                CurrencyManager.Instance.AddCoins(reward);
                RecordGiftForRun(equipped, doubleEgg ? 2 : 1, 0, reward);
                Debug.Log($"Claimed egg! +{reward} coins");
                if (visual != null) visual.RemoveEgg();
                FloatingTextManager.ShowCoins(reward, rewardWorldPos);
            }
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
        if (equipped == null || !equipped.abilityType.HasFlag(AnimalAbilityType.PassiveTimer)) return;

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
        SkinSwapper.Attach(activeVisualInstance, data.animalID); // equipped colour variant (Store skins)

        // Rustle crops this animal brushes past (cow grazing, dog running through, etc.).
        if (activeVisualInstance.GetComponent<CropAgitator>() == null)
            activeVisualInstance.AddComponent<CropAgitator>();
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

        // Animals must never appear at the Market. Spawning at the visible area would drop a newly
        // equipped animal straight into town if the player happens to be parked there — AnimalVisual's
        // penning would then walk it home, but only after it had already been standing in the market.
        // Place it in the Farm framing instead.
        CameraPanController pan = cam.GetComponent<CameraPanController>();
        if (pan != null && pan.CurrentLocation == CameraPanController.Location.Market)
        {
            Rect farm = pan.GetViewRect(CameraPanController.Location.Farm, 0.08f);
            return new Vector3(farm.center.x, Mathf.Lerp(farm.yMin, farm.yMax, 0.3f), 0f);
        }

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

        if (equipped.abilityType.HasFlag(AnimalAbilityType.RunDefender))
        {
            ActivateRunDefender(equipped);
        }
    }

    private void OnRunEnded()
    {
        AnimalData equipped = GetEquippedAnimal();
        if (equipped == null) return;

        if (equipped.abilityType.HasFlag(AnimalAbilityType.RunDefender))
        {
            DeactivateRunDefender();
        }
    }

    // Any defender animal is found by its AnimalDefender base type — no per-animal ID checks, so a
    // new defender only has to add the component to its visual prefab and set the RunDefender flag.
    private void ActivateRunDefender(AnimalData data)
    {
        if (activeVisualInstance == null) return;

        AnimalDefender defender = activeVisualInstance.GetComponent<AnimalDefender>();
        if (defender == null)
        {
            Debug.LogWarning($"AnimalManager: {data.displayName} is flagged RunDefender but its visual " +
                             "prefab has no AnimalDefender component — it will just wander.");
            return;
        }

        AnimalVisual visual = activeVisualInstance.GetComponent<AnimalVisual>();
        if (visual != null) visual.PauseWander = true;
        defender.ActivateChaseMode();
    }

    private void DeactivateRunDefender()
    {
        if (activeVisualInstance == null) return;

        AnimalDefender defender = activeVisualInstance.GetComponent<AnimalDefender>();
        if (defender != null) defender.DeactivateChaseMode();

        AnimalVisual visual = activeVisualInstance.GetComponent<AnimalVisual>();
        if (visual != null) visual.PauseWander = false;
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
