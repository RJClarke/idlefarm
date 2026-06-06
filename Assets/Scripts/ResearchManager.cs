using System;
using System.Collections.Generic;
using System.Linq;
using Research;
using UnityEngine;

/// <summary>
/// Owns the player's research state: 4 slots (locked/unlocked), per-slot active research, real-time level-ups,
/// and stat-key bonus queries. Catalog loaded from Resources/Research/*. Persists via GameData.json.
/// </summary>
public class ResearchManager : MonoBehaviour
{
    public static ResearchManager Instance { get; private set; }

    public const int SlotCount = 4;

    public enum SlotUnlockType { Coins, Gems, Research }

    [Serializable]
    public class SlotDefinition
    {
        public SlotUnlockType unlockType;
        public int costAmount;
        public string requiredResearchID;
    }

    [Header("Slot Unlock Costs")]
    [SerializeField] private SlotDefinition[] slotDefs = new SlotDefinition[SlotCount]
    {
        new SlotDefinition { unlockType = SlotUnlockType.Coins, costAmount = 100 },
        new SlotDefinition { unlockType = SlotUnlockType.Gems,  costAmount = 100 },
        new SlotDefinition { unlockType = SlotUnlockType.Research, requiredResearchID = "slot_3_unlock" },
        new SlotDefinition { unlockType = SlotUnlockType.Research, requiredResearchID = "slot_4_unlock" },
    };

    [Header("Tuning")]
    [SerializeField] private ResearchTuning tuning; // assign in Inspector → ResearchTuning.asset

    private readonly bool[] slotUnlocked = new bool[SlotCount];
    private readonly ResearchSlotState[] slots = new ResearchSlotState[SlotCount];
    private readonly HashSet<string> featureFlags = new HashSet<string>();

    // researchID -> ResearchData
    private readonly Dictionary<string, ResearchData> catalog = new Dictionary<string, ResearchData>();
    // researchID -> highest level reached
    private readonly Dictionary<string, int> levelsByResearchID = new Dictionary<string, int>();

    public event Action<int> OnSlotUnlocked;              // slotIndex
    public event Action<int> OnSlotStateChanged;          // slotIndex — assigned/cancelled
    public event Action<string, int> OnResearchLeveledUp; // researchID, newLevel
    public event Action<string> OnFeatureFlagUnlocked;    // featureID

    private float tickAccumulator;
    private bool stateLoadedFromSave;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        for (int i = 0; i < SlotCount; i++) slots[i] = new ResearchSlotState { slotIndex = i };
        LoadCatalog();
    }

    private void Start()
    {
        // If save didn't supply state, fall back to legacy PlayerPrefs slot bits (one-shot migration).
        if (!stateLoadedFromSave)
        {
            for (int i = 0; i < SlotCount; i++)
                slotUnlocked[i] = PlayerPrefs.GetInt("research_slot_unlocked_" + i, 0) == 1;
        }
    }

    private void Update()
    {
        float interval = tuning != null ? tuning.tickIntervalSecs : 1f;
        tickAccumulator += Time.unscaledDeltaTime;
        if (tickAccumulator < interval) return;
        tickAccumulator = 0f;
        Tick();
    }

    // ───────── Catalog ─────────

    private void LoadCatalog()
    {
        catalog.Clear();
        var all = Resources.LoadAll<ResearchData>("Research");
        foreach (var rd in all)
        {
            if (rd == null || string.IsNullOrEmpty(rd.researchID)) continue;
            if (catalog.ContainsKey(rd.researchID)) { Debug.LogError($"[Research] Duplicate ID: {rd.researchID}"); continue; }
            catalog[rd.researchID] = rd;
        }
        Debug.Log($"[Research] Catalog loaded: {catalog.Count} entries");
    }

    public ResearchData GetResearch(string id) => (id != null && catalog.TryGetValue(id, out var rd)) ? rd : null;
    public IEnumerable<ResearchData> AllResearches() => catalog.Values;

    // ───────── Slot Unlock (existing surface, kept) ─────────

    public SlotDefinition GetSlotDef(int index) => (index >= 0 && index < SlotCount) ? slotDefs[index] : null;
    public bool IsSlotUnlocked(int index) => index >= 0 && index < SlotCount && slotUnlocked[index];

    public bool CanUnlockSlot(int index)
    {
        if (!IsValidSlot(index) || slotUnlocked[index]) return false;
        SlotDefinition def = slotDefs[index];
        switch (def.unlockType)
        {
            case SlotUnlockType.Coins: return CurrencyManager.Instance != null && CurrencyManager.Instance.CanAffordCoins(def.costAmount);
            case SlotUnlockType.Gems:  return CurrencyManager.Instance != null && CurrencyManager.Instance.CanAffordGems(def.costAmount);
            case SlotUnlockType.Research: return GetCurrentLevel(def.requiredResearchID) >= 1;
        }
        return false;
    }

    public bool TryUnlockSlot(int index)
    {
        if (!CanUnlockSlot(index)) return false;
        var def = slotDefs[index];
        switch (def.unlockType)
        {
            case SlotUnlockType.Coins: if (!CurrencyManager.Instance.SpendCoins(def.costAmount)) return false; break;
            case SlotUnlockType.Gems:  if (!CurrencyManager.Instance.SpendGems(def.costAmount))  return false; break;
            case SlotUnlockType.Research: /* no payment */ break;
        }
        UnlockSlotInternal(index);
        return true;
    }

    private void UnlockSlotInternal(int index)
    {
        if (slotUnlocked[index]) return;
        slotUnlocked[index] = true;
        OnSlotUnlocked?.Invoke(index);
    }

    private static bool IsValidSlot(int index) => index >= 0 && index < SlotCount;

    // ───────── Assign / Cancel ─────────

    public ResearchSlotState GetSlot(int index) => IsValidSlot(index) ? slots[index] : null;

    public bool TryAssignResearch(int slotIndex, string researchID)
    {
        if (!IsValidSlot(slotIndex) || !slotUnlocked[slotIndex]) return false;
        var rd = GetResearch(researchID);
        if (rd == null) return false;
        if (!IsResearchVisible(researchID)) return false;
        int curLevel = GetCurrentLevel(researchID);
        if (curLevel >= rd.MaxLevel) return false;

        // Pay L+1 cost (level we're about to start)
        int nextLevel = curLevel + 1;
        int cost = GetCostForLevel(rd, nextLevel);
        if (CurrencyManager.Instance == null || !CurrencyManager.Instance.SpendCoins(cost)) return false;

        slots[slotIndex].activeResearchID = researchID;
        slots[slotIndex].currentLevel     = curLevel;
        slots[slotIndex].startUtcTicks    = DateTime.UtcNow.Ticks;
        OnSlotStateChanged?.Invoke(slotIndex);
        return true;
    }

    public void CancelResearch(int slotIndex)
    {
        if (!IsValidSlot(slotIndex)) return;
        slots[slotIndex].activeResearchID = "";
        slots[slotIndex].currentLevel = 0;
        slots[slotIndex].startUtcTicks = 0;
        slots[slotIndex].boostExpiresUtcTicks = 0;
        slots[slotIndex].boostMultiplier = 1.0f;
        OnSlotStateChanged?.Invoke(slotIndex);
    }

    public int GetCurrentLevel(string researchID)
    {
        if (string.IsNullOrEmpty(researchID)) return 0;
        return levelsByResearchID.TryGetValue(researchID, out var lvl) ? lvl : 0;
    }

    public bool IsBinaryComplete(string researchID)
    {
        var rd = GetResearch(researchID);
        return rd != null && rd.IsBinary && GetCurrentLevel(researchID) >= 1;
    }

    public bool IsFeatureUnlocked(string featureID) => !string.IsNullOrEmpty(featureID) && featureFlags.Contains(featureID);

    // ───────── Math ─────────

    public float GetSecondsForLevel(ResearchData rd, int levelOneIndexed)
    {
        if (rd == null || levelOneIndexed < 1) return 0f;
        if (rd.IsBinary) return rd.binaryFixedDurationSecs;
        float p = tuning != null ? tuning.pTime : 2.16f;
        float baseSecs = rd.baseDurationSecs * rd.timeDifficulty;
        // Apply Research Speed bonus globally (divide duration by 1 + bonus)
        float rsBonus = GetBonus(StatKey.ResearchSpeed);
        float scaled = baseSecs * Mathf.Pow(levelOneIndexed, p) / Mathf.Max(0.01f, 1f + rsBonus);
        return scaled;
    }

    public int GetCostForLevel(ResearchData rd, int levelOneIndexed)
    {
        if (rd == null || levelOneIndexed < 1) return 0;
        if (rd.IsBinary) return rd.binaryFixedCost;
        float p = tuning != null ? tuning.pCost : 2.0f;
        float baseCost = rd.baseCostCoins * rd.costDifficulty;
        return Mathf.CeilToInt(baseCost * Mathf.Pow(levelOneIndexed, p));
    }

    // ───────── Tick (real-time level-ups) ─────────

    public void Tick()
    {
        long nowTicks = DateTime.UtcNow.Ticks;
        for (int i = 0; i < SlotCount; i++)
        {
            var s = slots[i];
            if (s.IsIdle) continue;
            var rd = GetResearch(s.activeResearchID);
            if (rd == null) { CancelResearch(i); continue; }

            int safety = 0;
            while (safety++ < 100)
            {
                if (s.currentLevel >= rd.MaxLevel) { OnResearchCompleted(rd, i); break; }
                int nextLevel = s.currentLevel + 1;
                float secs = GetSecondsForLevel(rd, nextLevel);
                if (secs <= 0f) break;

                double elapsedSec = ComputeElapsedSeconds(s, nowTicks);
                if (elapsedSec < secs) break;

                // Level up
                s.currentLevel = nextLevel;
                levelsByResearchID[rd.researchID] = nextLevel;
                long consumedTicks = (long)(secs * TimeSpan.TicksPerSecond);
                s.startUtcTicks += consumedTicks;
                OnResearchLeveledUp?.Invoke(rd.researchID, nextLevel);

                if (nextLevel >= rd.MaxLevel) { OnResearchCompleted(rd, i); break; }

                // Auto-charge next level — if player can't afford, pause at current level.
                int nextCost = GetCostForLevel(rd, nextLevel + 1);
                if (CurrencyManager.Instance == null || !CurrencyManager.Instance.CanAffordCoins(nextCost))
                {
                    s.startUtcTicks = 0;
                    OnSlotStateChanged?.Invoke(i);
                    break;
                }
                CurrencyManager.Instance.SpendCoins(nextCost);
            }
        }
    }

    private static double ComputeElapsedSeconds(ResearchSlotState s, long nowTicks)
    {
        long deltaTicks = nowTicks - s.startUtcTicks;
        if (deltaTicks < 0) deltaTicks = 0;
        double secs = deltaTicks / (double)TimeSpan.TicksPerSecond;
        if (s.boostExpiresUtcTicks > s.startUtcTicks && s.boostMultiplier > 1.0f)
        {
            long boostEnd = Math.Min(nowTicks, s.boostExpiresUtcTicks);
            long boostTicks = Math.Max(0, boostEnd - s.startUtcTicks);
            double boostSecs = boostTicks / (double)TimeSpan.TicksPerSecond;
            secs += boostSecs * (s.boostMultiplier - 1.0);
        }
        return secs;
    }

    private void OnResearchCompleted(ResearchData rd, int slotIndex)
    {
        if (rd.IsBinary)
        {
            if (rd.unlocksSlotIndex >= 0 && rd.unlocksSlotIndex < SlotCount)
                UnlockSlotInternal(rd.unlocksSlotIndex);
            if (!string.IsNullOrEmpty(rd.unlocksFeatureID))
            {
                if (featureFlags.Add(rd.unlocksFeatureID))
                    OnFeatureFlagUnlocked?.Invoke(rd.unlocksFeatureID);
            }
        }
        CancelResearch(slotIndex);
    }

    // ───────── Bonus query ─────────

    public float GetBonus(string statKey)
    {
        if (string.IsNullOrEmpty(statKey)) return 0f;
        float total = 0f;
        foreach (var rd in catalog.Values)
        {
            if (rd.targetStatKey != statKey) continue;
            int lvl = GetCurrentLevel(rd.researchID);
            total += lvl * rd.bonusPerLevel;
        }
        return total;
    }

    // ───────── Visibility ─────────

    public bool IsResearchVisible(string researchID)
    {
        var rd = GetResearch(researchID);
        if (rd == null) return false;

        if (!string.IsNullOrEmpty(rd.prerequisiteResearchID))
            if (GetCurrentLevel(rd.prerequisiteResearchID) < 1) return false;

        if (!string.IsNullOrEmpty(rd.requiredUnlockID))
            if (UpgradeManager.Instance == null || UpgradeManager.Instance.GetPermanentLevel(rd.requiredUnlockID) < 1) return false;

        if (!string.IsNullOrEmpty(rd.requiredAnimalID))
            if (AnimalManager.Instance == null || !AnimalManager.Instance.IsUnlocked(rd.requiredAnimalID)) return false;

        return true;
    }

    // ───────── Save/Load surface ─────────

    public bool[] GetSlotsUnlockedForSave() => (bool[])slotUnlocked.Clone();
    public ResearchSlotState[] GetSlotsForSave() => slots.Select(s => DeepCopy(s)).ToArray();
    public string[] GetFeatureFlagsForSave() => featureFlags.ToArray();
    public ResearchLevelEntry[] GetLevelsForSave()
    {
        var result = new ResearchLevelEntry[levelsByResearchID.Count];
        int i = 0;
        foreach (var kv in levelsByResearchID)
        {
            result[i++] = new ResearchLevelEntry { researchID = kv.Key, level = kv.Value };
        }
        return result;
    }

    public void LoadState(bool[] unlocked, ResearchSlotState[] savedSlots, string[] flags, ResearchLevelEntry[] levels)
    {
        stateLoadedFromSave = true;

        // One-shot migration: if save was completely empty but PlayerPrefs has legacy slot bits, adopt them.
        bool anyFromSave = unlocked != null && unlocked.Any(b => b);
        if (!anyFromSave)
        {
            for (int i = 0; i < SlotCount; i++)
            {
                if (PlayerPrefs.GetInt("research_slot_unlocked_" + i, 0) == 1)
                    slotUnlocked[i] = true;
            }
            // Clear legacy keys so we don't re-migrate.
            for (int i = 0; i < SlotCount; i++) PlayerPrefs.DeleteKey("research_slot_unlocked_" + i);
            PlayerPrefs.Save();
        }

        if (unlocked != null) for (int i = 0; i < Math.Min(unlocked.Length, SlotCount); i++) slotUnlocked[i] = unlocked[i];
        if (savedSlots != null) for (int i = 0; i < Math.Min(savedSlots.Length, SlotCount); i++) slots[i] = DeepCopy(savedSlots[i]);
        featureFlags.Clear();
        if (flags != null) foreach (var f in flags) if (!string.IsNullOrEmpty(f)) featureFlags.Add(f);

        levelsByResearchID.Clear();
        if (levels != null)
            foreach (var e in levels)
                if (!string.IsNullOrEmpty(e.researchID)) levelsByResearchID[e.researchID] = e.level;
    }

    private static ResearchSlotState DeepCopy(ResearchSlotState src)
    {
        if (src == null) return new ResearchSlotState();
        return new ResearchSlotState
        {
            slotIndex = src.slotIndex,
            activeResearchID = src.activeResearchID ?? "",
            currentLevel = src.currentLevel,
            startUtcTicks = src.startUtcTicks,
            boostExpiresUtcTicks = src.boostExpiresUtcTicks,
            boostMultiplier = src.boostMultiplier <= 0 ? 1f : src.boostMultiplier
        };
    }

#if UNITY_EDITOR
    [ContextMenu("Reset All Research State")]
    private void EditorResetAll()
    {
        for (int i = 0; i < SlotCount; i++) { slotUnlocked[i] = false; CancelResearch(i); }
        levelsByResearchID.Clear();
        featureFlags.Clear();
        for (int i = 0; i < SlotCount; i++) PlayerPrefs.SetInt("research_slot_unlocked_" + i, 0);
        PlayerPrefs.Save();
        Debug.Log("[ResearchManager] All research state reset.");
    }
#endif
}
