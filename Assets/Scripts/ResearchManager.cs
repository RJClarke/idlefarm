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
    // offline_progress is unlocked by default (the 30% offline tax already makes being away
    // suboptimal, so we don't also gate the feature behind research). The flag is kept so it
    // can still be designed around later. Seeded here for new games; re-added after load-clear.
    private readonly HashSet<string> featureFlags = new HashSet<string> { Research.FeatureFlag.OfflineProgress };

    // researchID -> ResearchData
    private readonly Dictionary<string, ResearchData> catalog = new Dictionary<string, ResearchData>();
    // researchID -> highest level reached
    private readonly Dictionary<string, int> levelsByResearchID = new Dictionary<string, int>();
    // researchID -> seconds accumulated toward the next level (cost already paid). 0 = fresh.
    private readonly Dictionary<string, float> partialSecsByResearchID = new Dictionary<string, float>();

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

        float partial = GetPartialSecs(researchID);

        if (partial <= 0f)
        {
            // Fresh start — pay L+1 cost
            int nextLevel = curLevel + 1;
            int cost = GetCostForLevel(rd, nextLevel);
            if (CurrencyManager.Instance == null || !CurrencyManager.Instance.SpendCoins(cost)) return false;
        }
        // else: resume paid-but-paused — no charge. partial seconds count as elapsed.

        slots[slotIndex].activeResearchID = researchID;
        slots[slotIndex].currentLevel     = curLevel;
        // Backdate startUtc so elapsed = partial immediately
        slots[slotIndex].startUtcTicks    = DateTime.UtcNow.Ticks - (long)(partial * TimeSpan.TicksPerSecond);
        OnSlotStateChanged?.Invoke(slotIndex);
        return true;
    }

    public float GetPartialSecs(string researchID)
    {
        if (string.IsNullOrEmpty(researchID)) return 0f;
        return partialSecsByResearchID.TryGetValue(researchID, out var s) ? s : 0f;
    }

    public void CancelResearch(int slotIndex)
    {
        if (!IsValidSlot(slotIndex)) return;
        var s = slots[slotIndex];

        // Preserve progress toward the in-progress level so re-assigning later doesn't charge again.
        if (!s.IsIdle)
        {
            var rd = GetResearch(s.activeResearchID);
            if (rd != null && !rd.IsBinary && s.currentLevel < rd.MaxLevel)
            {
                int nextLevel = s.currentLevel + 1;
                float secsForLevel = GetSecondsForLevel(rd, nextLevel);
                double elapsed = ComputeElapsedSeconds(s, DateTime.UtcNow.Ticks);
                float partial = (float)Math.Min(Math.Max(0d, elapsed), secsForLevel);
                if (partial > 0f) partialSecsByResearchID[s.activeResearchID] = partial;
            }
        }

        s.activeResearchID = "";
        s.currentLevel = 0;
        s.startUtcTicks = 0;
        s.boostExpiresUtcTicks = 0;
        s.boostMultiplier = 1.0f;
        OnSlotStateChanged?.Invoke(slotIndex);
    }

    /// <summary>
    /// Buy a Compost boost token for an active research slot. Refuses to purchase
    /// while a boost is still active — buying again would discard the unspent time
    /// and waste compost. durationSecs is the boost window starting NOW.
    /// </summary>
    public bool TryApplyBoost(int slotIndex, float multiplier, float durationSecs, int compostCost)
    {
        if (!IsValidSlot(slotIndex)) return false;
        var s = slots[slotIndex];
        if (s.IsIdle) return false;
        if (multiplier <= 1.0f || durationSecs <= 0f) return false;
        if (s.HasActiveBoost(DateTime.UtcNow)) return false; // already boosted — don't stack/overwrite
        if (CurrencyManager.Instance == null) return false;
        if (!CurrencyManager.Instance.SpendCompost(compostCost)) return false;

        s.boostMultiplier = multiplier;
        s.boostExpiresUtcTicks = DateTime.UtcNow.Ticks + (long)(durationSecs * TimeSpan.TicksPerSecond);
        OnSlotStateChanged?.Invoke(slotIndex);
        return true;
    }

    /// <summary>
    /// Configure a slot to automatically re-purchase the given boost token whenever the
    /// current boost expires (provided the player has enough compost at that moment).
    /// Pass multiplier=0 / cost=0 to clear.
    /// </summary>
    public void SetAutoBuyBoost(int slotIndex, float multiplier, float durationSecs, int compostCost)
    {
        if (!IsValidSlot(slotIndex)) return;
        var s = slots[slotIndex];
        s.autoBuyMultiplier   = multiplier;
        s.autoBuyDurationSecs = durationSecs;
        s.autoBuyCost         = compostCost;
        OnSlotStateChanged?.Invoke(slotIndex);
    }

    /// <summary>
    /// Best-effort auto-buy. Called by Tick when an active boost expires and the slot
    /// has an auto-buy token configured. Silently no-ops if compost is insufficient.
    /// </summary>
    private void TryAutoBuyOnExpiry(int slotIndex)
    {
        var s = slots[slotIndex];
        if (s == null || s.IsIdle || !s.HasAutoBuy) return;
        if (s.HasActiveBoost(DateTime.UtcNow)) return; // shouldn't happen — caller guards
        if (CurrencyManager.Instance == null) return;
        if (CurrencyManager.Instance.Compost < s.autoBuyCost) return;
        if (!CurrencyManager.Instance.SpendCompost(s.autoBuyCost)) return;

        s.boostMultiplier = s.autoBuyMultiplier;
        s.boostExpiresUtcTicks = DateTime.UtcNow.Ticks + (long)(s.autoBuyDurationSecs * TimeSpan.TicksPerSecond);
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
        DateTime nowUtc = DateTime.UtcNow;
        for (int i = 0; i < SlotCount; i++)
        {
            var s = slots[i];
            if (s.IsIdle) continue;
            var rd = GetResearch(s.activeResearchID);
            if (rd == null) { CancelResearch(i); continue; }

            // Auto-buy boost when expired (retries each tick until compost is available)
            if (s.HasAutoBuy && !s.HasActiveBoost(nowUtc))
                TryAutoBuyOnExpiry(i);

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
                partialSecsByResearchID.Remove(rd.researchID); // partial only applies to the level we just finished
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
        // Union of researchIDs across levels + partials so partial-only entries persist too.
        var ids = new HashSet<string>(levelsByResearchID.Keys);
        foreach (var k in partialSecsByResearchID.Keys) ids.Add(k);
        var result = new ResearchLevelEntry[ids.Count];
        int i = 0;
        foreach (var id in ids)
        {
            result[i++] = new ResearchLevelEntry
            {
                researchID = id,
                level = levelsByResearchID.TryGetValue(id, out var lvl) ? lvl : 0,
                partialSecs = partialSecsByResearchID.TryGetValue(id, out var p) ? p : 0f,
            };
        }
        return result;
    }

    /// <summary>
    /// DevTools-only: finish ONE level of whatever research is active in each slot. Skips real-time wait.
    /// Binary completions still fire OnFeatureFlagUnlocked / OnSlotUnlocked so downstream UI refreshes.
    /// After leveling up, the slot becomes idle (matches the natural "you paid for L+1, level done" flow);
    /// the player chooses what to research next.
    /// </summary>
    public void DebugFinishCurrentResearches()
    {
        for (int i = 0; i < SlotCount; i++)
        {
            var s = slots[i];
            if (s.IsIdle) continue;
            var rd = GetResearch(s.activeResearchID);
            if (rd == null) { CancelResearch(i); continue; }

            int newLevel = Math.Min(rd.MaxLevel, s.currentLevel + 1);
            s.currentLevel = newLevel;
            levelsByResearchID[rd.researchID] = newLevel;
            partialSecsByResearchID.Remove(rd.researchID);
            OnResearchLeveledUp?.Invoke(rd.researchID, newLevel);

            if (newLevel >= rd.MaxLevel)
                OnResearchCompleted(rd, i); // handles binary unlocks + cancels slot
            else
                CancelResearch(i); // slot becomes idle; player chooses next research/level
        }
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
        featureFlags.Add(Research.FeatureFlag.OfflineProgress); // unlocked by default (see field init)

        levelsByResearchID.Clear();
        partialSecsByResearchID.Clear();
        if (levels != null)
            foreach (var e in levels)
            {
                if (string.IsNullOrEmpty(e.researchID)) continue;
                if (e.level > 0) levelsByResearchID[e.researchID] = e.level;
                if (e.partialSecs > 0f) partialSecsByResearchID[e.researchID] = e.partialSecs;
            }

        // Snapshot levels before catch-up so the welcome-back modal can show deltas.
        int[] levelsBefore = new int[SlotCount];
        string[] researchBefore = new string[SlotCount];
        for (int i = 0; i < SlotCount; i++)
        {
            researchBefore[i] = slots[i] != null ? slots[i].activeResearchID : "";
            levelsBefore[i]   = (slots[i] != null && !string.IsNullOrEmpty(slots[i].activeResearchID))
                ? GetCurrentLevel(slots[i].activeResearchID)
                : 0;
        }

        // Offline auto-buy catch-up: simulate the boost renewals that would have fired during
        // the offline window. Pretty close, not exact — we assume Cow ran the whole time so the
        // current compost balance (already topped up elsewhere) is the budget, and credit each
        // affordable renewal as a single boosted window.
        ApplyOfflineAutoBuyRenewals(levelsBefore, researchBefore);
    }

    /// <summary>Result of the most recent offline catch-up. Populated by LoadState.</summary>
    public OfflineCatchUpReport LastOfflineReport { get; private set; }

    public class OfflineCatchUpReport
    {
        public int compostSpentOnAutoBuy;
        public int totalAutoBuyRenewals;
        public SlotProgress[] slots = new SlotProgress[SlotCount];
    }

    public class SlotProgress
    {
        public int slotIndex;
        public string researchID = "";
        public string displayName = "";
        public int levelBefore;
        public int levelAfter;
        public int autoBuyRenewals;
    }

    private void ApplyOfflineAutoBuyRenewals(int[] levelsBefore, string[] researchBefore)
    {
        var report = new OfflineCatchUpReport();
        long nowTicks = DateTime.UtcNow.Ticks;
        DateTime nowUtc = DateTime.UtcNow;

        for (int i = 0; i < SlotCount; i++)
        {
            var slotReport = new SlotProgress { slotIndex = i };
            report.slots[i] = slotReport;

            var s = slots[i];
            if (s == null || s.IsIdle || !s.HasAutoBuy) continue;
            if (s.HasActiveBoost(nowUtc)) continue;

            long gapTicks = nowTicks - s.boostExpiresUtcTicks;
            if (gapTicks <= 0) continue;

            double gapSecs = gapTicks / (double)TimeSpan.TicksPerSecond;
            int maxByTime    = (int)(gapSecs / s.autoBuyDurationSecs); // floor — partial last window not credited
            int maxByCompost = (CurrencyManager.Instance != null && s.autoBuyCost > 0)
                ? CurrencyManager.Instance.Compost / s.autoBuyCost
                : 0;
            int renewals = Mathf.Min(maxByTime, maxByCompost);
            if (renewals <= 0) continue;

            int totalCost = renewals * s.autoBuyCost;
            if (CurrencyManager.Instance == null || !CurrencyManager.Instance.SpendCompost(totalCost)) continue;

            s.boostMultiplier = s.autoBuyMultiplier;
            s.boostExpiresUtcTicks += (long)(renewals * s.autoBuyDurationSecs * TimeSpan.TicksPerSecond);
            slotReport.autoBuyRenewals = renewals;
            report.compostSpentOnAutoBuy += totalCost;
            report.totalAutoBuyRenewals  += renewals;
            OnSlotStateChanged?.Invoke(i);
        }

        Tick(); // apply the extended boost windows to research progress

        // Fill per-slot level deltas (after Tick, so level-ups are reflected)
        for (int i = 0; i < SlotCount; i++)
        {
            var slotReport = report.slots[i];
            string rid = researchBefore[i];
            slotReport.researchID = rid;
            slotReport.levelBefore = levelsBefore[i];
            slotReport.levelAfter  = string.IsNullOrEmpty(rid) ? 0 : GetCurrentLevel(rid);
            var rd = string.IsNullOrEmpty(rid) ? null : GetResearch(rid);
            slotReport.displayName = rd != null ? rd.displayName : "";
        }

        LastOfflineReport = report;
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
            boostMultiplier = src.boostMultiplier <= 0 ? 1f : src.boostMultiplier,
            autoBuyMultiplier = src.autoBuyMultiplier,
            autoBuyDurationSecs = src.autoBuyDurationSecs,
            autoBuyCost = src.autoBuyCost
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
