using UnityEngine;
using System.IO;

/// <summary>
/// Handles saving and loading game data
/// Uses JSON format stored in persistent data path (works on mobile)
/// </summary>
public class SaveManager : MonoBehaviour
{
    public static SaveManager Instance { get; private set; }

    [Header("Settings")]
    [SerializeField] private bool enableAutoSave = true;

    private string saveFilePath;
    private const string SAVE_FILE_NAME = "gamedata.json";

    private void Awake()
    {
        // Singleton pattern
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        // Set save file path (works on mobile)
        saveFilePath = Path.Combine(Application.persistentDataPath, SAVE_FILE_NAME);
        // Save file path set
    }

    /// <summary>
    /// Save current game data to disk
    /// </summary>
    public void SaveGame()
    {
        if (CurrencyManager.Instance == null)
        {
            Debug.LogError("Cannot save: CurrencyManager not found!");
            return;
        }

        // Create data object from current game state
        string[] animalIDs = new string[0];
        string equippedID = "";
        string eggTime = "";
        string compostTime = "";

        if (AnimalManager.Instance != null)
        {
            animalIDs = AnimalManager.Instance.GetUnlockedAnimalIDs();
            equippedID = AnimalManager.Instance.GetEquippedAnimalID();
            eggTime = AnimalManager.Instance.GetLastEggClaimTimeISO();
            compostTime = AnimalManager.Instance.GetLastCompostTimeISO();
        }

        ActiveQuest[] quests = new ActiveQuest[0];
        int questsCompleted = 0;
        bool[] milestones = new bool[8];
        string weekStart = "";
        string lastDrop = "";

        if (QuestManager.Instance != null)
        {
            quests = QuestManager.Instance.GetActiveQuestsForSave();
            questsCompleted = QuestManager.Instance.QuestsCompletedThisWeek;
            milestones = QuestManager.Instance.WeeklyMilestonesClaimed;
            weekStart = QuestManager.Instance.GetQuestWeekStartISO();
            lastDrop = QuestManager.Instance.GetLastQuestDropTimeISO();
        }

        bool[] researchUnlocked = new bool[ResearchManager.SlotCount];
        Research.ResearchSlotState[] researchSlots = null;
        string[] featureFlags = new string[0];
        ResearchLevelEntry[] researchLevels = new ResearchLevelEntry[0];
        if (ResearchManager.Instance != null)
        {
            researchUnlocked = ResearchManager.Instance.GetSlotsUnlockedForSave();
            researchSlots    = ResearchManager.Instance.GetSlotsForSave();
            featureFlags     = ResearchManager.Instance.GetFeatureFlagsForSave();
            researchLevels   = ResearchManager.Instance.GetLevelsForSave();
        }

        GameData data = new GameData(
            CurrencyManager.Instance.Coins,
            CurrencyManager.Instance.Gems,
            CurrencyManager.Instance.Compost,
            animalIDs,
            equippedID,
            eggTime,
            compostTime,
            quests,
            questsCompleted,
            milestones,
            weekStart,
            lastDrop,
            researchUnlocked,
            researchSlots,
            featureFlags,
            researchLevels
        );
        data.lastSeenUtcTicks = System.DateTime.UtcNow.Ticks;
        data.permanentUpgradeLevels = UpgradeManager.Instance != null
            ? UpgradeManager.Instance.GetPermanentLevelsForSave()
            : new UpgradeLevelEntry[0];
        data.purchasedHelperUpgradeIDs = HelperUpgradeManager.Instance != null
            ? HelperUpgradeManager.Instance.GetPurchasedIDsForSave()
            : new string[0];
        data.seenContentIds = NewContentTracker.Instance != null
            ? NewContentTracker.Instance.GetSeenForSave()
            : new string[0];

        data.farmName = NarrativeManager.Instance != null
            ? NarrativeManager.Instance.GetFarmNameForSave()
            : "";
        data.firedNarrativeFlags = NarrativeManager.Instance != null
            ? NarrativeManager.Instance.GetFiredFlagsForSave()
            : new string[0];
        data.inboxLetters = InboxManager.Instance != null
            ? InboxManager.Instance.GetForSave()
            : new InboxEntry[0];

        // Active-run snapshot — Money, temp upgrades, and the wall-clock start time so the
        // run can resume at the same point on the difficulty curve.
        bool runActive = RunManager.Instance != null && RunManager.Instance.IsRunActive;
        data.runActive = runActive;
        data.runStartUtcTicks = runActive ? RunManager.Instance.RunStartUtcTicks : 0L;
        data.runTotalSeconds = runActive ? RunManager.Instance.CurrentRunDuration : 0f;
        data.money = runActive && CurrencyManager.Instance != null ? CurrencyManager.Instance.Money : 0;
        data.temporaryUpgradeLevels = runActive && UpgradeManager.Instance != null
            ? UpgradeManager.Instance.GetTemporaryLevelsForSave()
            : new UpgradeLevelEntry[0];

        data.wood = CurrencyManager.Instance != null ? CurrencyManager.Instance.Wood : 0;
        data.axeLevel = WoodcuttingManager.Instance != null ? WoodcuttingManager.Instance.AxeLevel : 0;
        data.hasAxe = WoodcuttingManager.Instance != null && WoodcuttingManager.Instance.HasAxe;
        data.trees = WoodcuttingManager.Instance != null ? WoodcuttingManager.Instance.GetTreeSaveStates() : new TreeSaveState[0];

        if (CanneryManager.Instance != null) CanneryManager.Instance.CaptureTo(data);

        // Convert to JSON
        string json = JsonUtility.ToJson(data, true); // true = pretty print for debugging

        // Write to file atomically (tmp -> swap, prior copy kept at .bak) so a crash mid-write
        // can never corrupt the live save or its backup.
        try
        {
            SaveFileIO.WriteAtomic(saveFilePath, json);
            Debug.Log($"Game saved! Coins: {data.coins}");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Failed to save game: {e.Message}");
        }
    }

    /// <summary>
    /// Load game data from disk
    /// Returns true if load was successful
    /// </summary>
    public bool LoadGame()
    {
        // Read the live save, falling back to the .bak copy if the primary is missing/empty/corrupt.
        // isValid does the JSON sanity check here so EconomyCore never needs to know about JsonUtility.
        bool found = SaveFileIO.ReadWithFallback(saveFilePath, IsValidSaveJson, out string json, out bool usedBackup);
        if (!found)
        {
            Debug.Log("No save file found. Starting new game.");
            return false;
        }

        if (usedBackup)
            Debug.LogWarning("Primary save was missing or corrupt; recovered from backup (.bak).");

        try
        {
            // Convert from JSON to GameData object (already validated non-null by ReadWithFallback).
            GameData data = JsonUtility.FromJson<GameData>(json);

            // CurrencyManager is the anchor for all applied state; without it we can't load.
            if (CurrencyManager.Instance == null)
            {
                Debug.LogError("Cannot load: CurrencyManager not found!");
                return false;
            }

            {
                CurrencyManager.Instance.SetCoins(data.coins);
                CurrencyManager.Instance.SetGems(data.gems);
                CurrencyManager.Instance.SetCompost(data.compost);
                CurrencyManager.Instance.SetWood(data.wood);
                if (WoodcuttingManager.Instance != null)
                {
                    WoodcuttingManager.Instance.SetAxeLevel(data.axeLevel);
                    WoodcuttingManager.Instance.SetHasAxe(data.hasAxe);
                    WoodcuttingManager.Instance.LoadTreeStates(data.trees);
                }

                if (CanneryManager.Instance != null)
                    CanneryManager.Instance.LoadFrom(data);

                if (AnimalManager.Instance != null)
                {
                    AnimalManager.Instance.LoadState(data.unlockedAnimalIDs, data.equippedAnimalID, data.lastEggClaimTime);
                    AnimalManager.Instance.LoadCompostTime(data.lastCompostClaimTime);
                }

                if (QuestManager.Instance != null)
                {
                    QuestManager.Instance.LoadState(
                        data.activeQuests,
                        data.questsCompletedThisWeek,
                        data.weeklyMilestonesClaimed,
                        data.questWeekStart,
                        data.lastQuestDropTime
                    );
                }

                if (ResearchManager.Instance != null)
                {
                    ResearchManager.Instance.LoadState(
                        data.researchSlotsUnlocked,
                        data.researchSlots,
                        data.binaryFeatureFlagsSet,
                        data.researchLevels
                    );
                }

                if (UpgradeManager.Instance != null)
                    UpgradeManager.Instance.LoadPermanentLevels(data.permanentUpgradeLevels);

                if (HelperUpgradeManager.Instance != null)
                    HelperUpgradeManager.Instance.LoadState(data.purchasedHelperUpgradeIDs);

                // Active-run resume. Order matters:
                //   1. Set Money first so listeners (UI) see the real value, not the post-reset default.
                //   2. RunManager.ResumeRun fires OnRunStarted; UpgradeManager's handler clears temp levels.
                //   3. Then restore temp upgrade levels on top of the cleared state.
                if (data.runActive && data.runStartUtcTicks > 0)
                {
                    if (CurrencyManager.Instance != null)
                        CurrencyManager.Instance.SetMoney(data.money);

                    // Pass lastSeenUtcTicks so ResumeRun credits the offline window at max
                    // game speed for difficulty advance (per offline-runs design 2026-06-10).
                    if (RunManager.Instance != null)
                        RunManager.Instance.ResumeRun(data.runStartUtcTicks, data.runTotalSeconds, data.lastSeenUtcTicks);

                    if (UpgradeManager.Instance != null)
                        UpgradeManager.Instance.LoadTemporaryLevels(data.temporaryUpgradeLevels);
                }

                // Hand the offline anchor + active-run snapshot to the welcome-back system. The run was
                // already resumed above (so temp-upgrade-level ordering is preserved); the gate simulates
                // the away-period and ADJUSTS the resumed run (override farm time / force-end on bankruptcy).
                OfflineProgressManager.SeedRunSnapshot(data.runActive, data.runTotalSeconds, data.money);
                OfflineProgressManager.SeedLastSeen(data.lastSeenUtcTicks);

                // After research/upgrade/animal state is restored so availability is accurate.
                if (NewContentTracker.Instance != null)
                    NewContentTracker.Instance.LoadState(data.seenContentIds);

                if (NarrativeManager.Instance != null)
                    NarrativeManager.Instance.LoadState(data.farmName, data.firedNarrativeFlags);

                if (InboxManager.Instance != null)
                    InboxManager.Instance.LoadState(data.inboxLetters);

                Debug.Log($"Game loaded! Coins: {data.coins}");
            }

            return true;
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Failed to load game: {e.Message}");
            return false;
        }
    }

    /// <summary>
    /// Validity check handed to <see cref="SaveFileIO.ReadWithFallback"/>: a candidate save is
    /// valid only if it parses to a non-null <see cref="GameData"/>. Keeps the JsonUtility
    /// dependency on the Unity side so EconomyCore stays engine-free.
    /// </summary>
    private static bool IsValidSaveJson(string json)
    {
        try
        {
            return JsonUtility.FromJson<GameData>(json) != null;
        }
        catch (System.Exception)
        {
            return false;
        }
    }

    /// <summary>
    /// Delete save file (useful for testing or "reset game" feature)
    /// </summary>
    public void DeleteSave()
    {
        if (File.Exists(saveFilePath))
        {
            File.Delete(saveFilePath);
            Debug.Log("Save file deleted.");
        }
        else
        {
            Debug.Log("No save file to delete.");
        }
    }

    /// <summary>
    /// Check if a save file exists
    /// </summary>
    public bool SaveFileExists()
    {
        return File.Exists(saveFilePath);
    }

    #region Auto-Save on Application Events

    private void OnApplicationQuit()
    {
        if (!enableAutoSave)
        {
            return;
        }

        // Auto-save when game closes
        SaveGame();
    }

    private void OnApplicationPause(bool pause)
    {
        if (!enableAutoSave) return;

        // Auto-save when app is paused (important for mobile!). Skip during editor play-stop
        // teardown when the singleton is already gone (avoids a spurious "not found" error).
        if (pause && CurrencyManager.Instance != null)
        {
            SaveGame();
        }
    }

    #endregion

    #region Testing/Debug Methods

    [ContextMenu("Save Game Now")]
    private void TestSave()
    {
        SaveGame();
    }

    [ContextMenu("Load Game Now")]
    private void TestLoad()
    {
        LoadGame();
    }

    [ContextMenu("Delete Save File")]
    private void TestDelete()
    {
        DeleteSave();
    }

    [ContextMenu("Check Save File Exists")]
    private void TestExists()
    {
        Debug.Log($"Save file exists: {SaveFileExists()}");
    }

    #endregion
}