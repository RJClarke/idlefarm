# Daily Quests & Weekly Milestone Track — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add a rolling quest pool (2 quests every 6 hours, max 10 held) with a weekly milestone track that rewards gems, gated by player unlocks.

**Architecture:** `QuestData` ScriptableObjects define quest types; `QuestManager` singleton handles scheduling, progress tracking via events, and milestone claiming; `QuestPopup` displays the quest list and weekly strip; save data extends `GameData` JSON.

**Tech Stack:** Unity C#, LeanTween, TextMesh Pro, JsonUtility, `TimeZoneInfo` for CT scheduling.

---

## File Map

**New files:**
- `Assets/Scripts/ActiveQuest.cs` — serializable runtime class for a quest in the player's pool
- `Assets/Scripts/QuestData.cs` — ScriptableObject defining a quest type
- `Assets/Scripts/QuestManager.cs` — singleton: scheduling, progress, claiming, weekly reset
- `Assets/Scripts/QuestPopup.cs` — popup UI controller
- `Assets/Scripts/QuestRow.cs` — component on each quest row prefab
- `Assets/Scripts/MilestoneChip.cs` — component on each of the 8 milestone chips
- `Assets/Prefabs/Quests/QuestRow.prefab` — single quest row UI prefab
- `Assets/Data/Quests/Quest_HarvestCrops.asset` — and 6 more QuestData assets

**Modified files:**
- `Assets/Scripts/GameData.cs` — add quest save fields
- `Assets/Scripts/SaveManager.cs` — read/write quest state
- `Assets/Scripts/RunStats.cs` — add Action events fired alongside existing increment methods

---

## Task 1: ActiveQuest class + GameData extensions

**Files:**
- Create: `Assets/Scripts/ActiveQuest.cs`
- Modify: `Assets/Scripts/GameData.cs`

- [ ] **Step 1: Create `ActiveQuest.cs`**

```csharp
using System;

[Serializable]
public class ActiveQuest
{
    public string questID;
    public int progress;
    public bool isCompleted;
    public bool isClaimed;
    public string droppedAt; // UTC ISO 8601

    public ActiveQuest() { }

    public ActiveQuest(string questID, string droppedAt)
    {
        this.questID = questID;
        this.droppedAt = droppedAt;
        progress = 0;
        isCompleted = false;
        isClaimed = false;
    }
}
```

- [ ] **Step 2: Extend `GameData.cs`**

Add these fields to the `GameData` class (after the existing `lastEggClaimTime` field):

```csharp
public ActiveQuest[] activeQuests;
public int questsCompletedThisWeek;
public bool[] weeklyMilestonesClaimed;
public string questWeekStart;
public string lastQuestDropTime;
```

Update the default constructor to initialize them:

```csharp
public GameData()
{
    coins = 0;
    gems = 0;
    unlockedAnimalIDs = new string[0];
    equippedAnimalID = "";
    lastEggClaimTime = "";
    activeQuests = new ActiveQuest[0];
    questsCompletedThisWeek = 0;
    weeklyMilestonesClaimed = new bool[8];
    questWeekStart = "";
    lastQuestDropTime = "";
}
```

Update the parameterized constructor signature and body to match:

```csharp
public GameData(int currentCoins, int currentGems, string[] animalIDs, string equippedID, string eggTime,
    ActiveQuest[] quests, int questsCompleted, bool[] milestones, string weekStart, string lastDrop)
{
    coins = currentCoins;
    gems = currentGems;
    unlockedAnimalIDs = animalIDs ?? new string[0];
    equippedAnimalID = equippedID ?? "";
    lastEggClaimTime = eggTime ?? "";
    activeQuests = quests ?? new ActiveQuest[0];
    questsCompletedThisWeek = questsCompleted;
    weeklyMilestonesClaimed = milestones ?? new bool[8];
    questWeekStart = weekStart ?? "";
    lastQuestDropTime = lastDrop ?? "";
}
```

- [ ] **Step 3: Verify compile**

Open Unity. Check Console — no errors. If `SaveManager.cs` shows errors because the `GameData` constructor signature changed, proceed to Task 6 immediately to fix it, then return here.

- [ ] **Step 4: Commit**

```
git add Assets/Scripts/ActiveQuest.cs Assets/Scripts/GameData.cs
git commit -m "feat: add ActiveQuest class and quest fields to GameData"
```

---

## Task 2: Add Action events to RunStats

`RunStats` currently uses plain increment methods. `QuestManager` needs to subscribe to stat events without coupling into RunStats internals. We add `Action` events fired inside each increment method.

**Files:**
- Modify: `Assets/Scripts/RunStats.cs`

- [ ] **Step 1: Add event declarations to `RunStats`**

Add these public event declarations after the existing `public static RunStats Instance` line:

```csharp
public event Action OnCropHarvested;
public event Action OnSeedPlanted;
public event Action OnPlantWatered;
public event Action OnDeerRepelled;
public event Action OnCrowRepelled;
```

- [ ] **Step 2: Fire events inside increment methods**

Update each of these five methods to fire the corresponding event:

```csharp
public void AddCropHarvested() { CropsHarvested++; OnCropHarvested?.Invoke(); }
public void AddSeedPlanted()   { SeedsPlanted++;   OnSeedPlanted?.Invoke(); }
public void AddPlantWatered()  { PlantsWatered++;  OnPlantWatered?.Invoke(); }
public void AddDeerRepelledByFence()      { DeerRepelledByFence++;      OnDeerRepelled?.Invoke(); }
public void AddCrowRepelledByScarecrow()  { CrowsRepelledByScarecrow++; OnCrowRepelled?.Invoke(); }
```

Leave all other increment methods (`AddTileTilled`, `AddPlantDehydrated`, etc.) unchanged.

- [ ] **Step 3: Verify compile**

Open Unity. Check Console — no errors.

- [ ] **Step 4: Commit**

```
git add Assets/Scripts/RunStats.cs
git commit -m "feat: add Action events to RunStats for quest progress tracking"
```

---

## Task 3: QuestData ScriptableObject

**Files:**
- Create: `Assets/Scripts/QuestData.cs`

- [ ] **Step 1: Create `QuestData.cs`**

```csharp
using UnityEngine;

public enum QuestObjectiveType
{
    HarvestCrops,
    PlantSeeds,
    WaterPlants,
    RepelDeer,
    RepelCrows,
    GatherEggs,
    GatherGems
}

[CreateAssetMenu(menuName = "Farm Game/Quest Data", order = 8)]
public class QuestData : ScriptableObject
{
    public string questID;
    public string displayName;
    public string description;
    public QuestObjectiveType objectiveType;
    public int targetCount;
    public int coinReward;
    [Tooltip("UpgradeManager permanent upgrade ID required. Empty = always eligible.")]
    public string requiredUnlockID;
    [Tooltip("AnimalManager animal ID required. Empty = no animal required.")]
    public string requiredAnimalID;
}
```

- [ ] **Step 2: Verify compile**

Open Unity. Check Console — no errors. Confirm `Farm Game/Quest Data` appears in the Create Asset menu (right-click in Project window → Create → Farm Game → Quest Data).

- [ ] **Step 3: Commit**

```
git add Assets/Scripts/QuestData.cs
git commit -m "feat: add QuestData ScriptableObject"
```

---

## Task 4: QuestManager — scheduling and weekly reset

**Files:**
- Create: `Assets/Scripts/QuestManager.cs`

- [ ] **Step 1: Create `QuestManager.cs` with singleton, state, and scheduling**

```csharp
using UnityEngine;
using System;
using System.Collections.Generic;
using System.Linq;

public class QuestManager : MonoBehaviour
{
    public static QuestManager Instance { get; private set; }

    [Header("Quest Pool")]
    [SerializeField] private List<QuestData> allQuests = new List<QuestData>();

    [Header("Settings")]
    [SerializeField] private int questsPerDrop = 2;
    [SerializeField] private int maxActiveQuests = 10;

    // Drop times in CT (hours): 6am, 12pm, 6pm, 12am
    private static readonly int[] DropHoursCT = { 0, 6, 12, 18 };

    // Milestone thresholds and rewards (index 0-7 → 5,10,15,20,25,30,35,40 quests)
    private static readonly int[] MilestoneThresholds = { 5, 10, 15, 20, 25, 30, 35, 40 };
    private static readonly int[] MilestoneGems = { 1, 1, 2, 2, 2, 2, 2, 10 };
    private static readonly int[] MilestoneCoins = { 50, 100, 150, 200, 200, 250, 250, 500 };

    // Runtime state
    private List<ActiveQuest> activeQuests = new List<ActiveQuest>();
    private int questsCompletedThisWeek;
    private bool[] weeklyMilestonesClaimed = new bool[8];
    private DateTime questWeekStart = DateTime.MinValue;
    private DateTime lastQuestDropTime = DateTime.MinValue;

    // Events
    public event Action OnQuestCompleted;
    public event Action OnQuestsDropped;
    public event Action OnMilestoneClaimed;

    // Public read-only access
    public int QuestsCompletedThisWeek => questsCompletedThisWeek;
    public bool[] WeeklyMilestonesClaimed => weeklyMilestonesClaimed;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void Start()
    {
        SubscribeToEvents();
        CheckWeeklyReset();
        ProcessDrops();
    }

    private void OnDestroy()
    {
        UnsubscribeFromEvents();
    }

    private void OnApplicationPause(bool pause)
    {
        if (!pause)
        {
            CheckWeeklyReset();
            ProcessDrops();
        }
    }

    // ── Scheduling ───────────────────────────────────────────────

    private void ProcessDrops()
    {
        DateTime nowUtc = DateTime.UtcNow;
        TimeZoneInfo ct = TimeZoneInfo.FindSystemTimeZoneById("Central Standard Time");
        DateTime nowCt = TimeZoneInfo.ConvertTimeFromUtc(nowUtc, ct);

        // Build list of all drop times (UTC) for today and yesterday
        List<DateTime> recentDropsUtc = new List<DateTime>();
        for (int dayOffset = -1; dayOffset <= 0; dayOffset++)
        {
            DateTime day = nowCt.Date.AddDays(dayOffset);
            foreach (int hour in DropHoursCT)
            {
                DateTime dropCt = day.AddHours(hour);
                DateTime dropUtc = TimeZoneInfo.ConvertTimeToUtc(dropCt, ct);
                if (dropUtc <= nowUtc)
                    recentDropsUtc.Add(dropUtc);
            }
        }

        recentDropsUtc.Sort();

        // Count how many drop windows have passed since lastQuestDropTime
        int missedDrops = 0;
        DateTime latestDropUtc = lastQuestDropTime;
        foreach (DateTime drop in recentDropsUtc)
        {
            if (drop > lastQuestDropTime)
            {
                missedDrops++;
                latestDropUtc = drop;
            }
        }

        if (missedDrops == 0) return;

        int added = 0;
        for (int i = 0; i < missedDrops * questsPerDrop; i++)
        {
            if (activeQuests.Count >= maxActiveQuests) break;
            QuestData picked = PickEligibleQuest();
            if (picked == null) break;
            activeQuests.Add(new ActiveQuest(picked.questID, latestDropUtc.ToString("o")));
            added++;
        }

        lastQuestDropTime = latestDropUtc;

        if (added > 0)
        {
            Debug.Log($"[Quests] Dropped {added} new quest(s).");
            OnQuestsDropped?.Invoke();
        }
    }

    private QuestData PickEligibleQuest()
    {
        HashSet<string> activeIDs = new HashSet<string>(activeQuests.Select(q => q.questID));
        List<QuestData> eligible = allQuests.Where(q =>
            !activeIDs.Contains(q.questID) && IsEligible(q)).ToList();

        if (eligible.Count == 0) return null;
        return eligible[UnityEngine.Random.Range(0, eligible.Count)];
    }

    private bool IsEligible(QuestData quest)
    {
        if (!string.IsNullOrEmpty(quest.requiredUnlockID))
        {
            if (UpgradeManager.Instance == null ||
                UpgradeManager.Instance.GetPermanentLevel(quest.requiredUnlockID) <= 0)
                return false;
        }
        if (!string.IsNullOrEmpty(quest.requiredAnimalID))
        {
            if (AnimalManager.Instance == null ||
                !AnimalManager.Instance.IsUnlocked(quest.requiredAnimalID))
                return false;
        }
        return true;
    }

    // ── Weekly Reset ─────────────────────────────────────────────

    private void CheckWeeklyReset()
    {
        DateTime nowUtc = DateTime.UtcNow;
        TimeZoneInfo ct = TimeZoneInfo.FindSystemTimeZoneById("Central Standard Time");
        DateTime nowCt = TimeZoneInfo.ConvertTimeFromUtc(nowUtc, ct);

        // Sunday midnight CT = start of this week
        int daysSinceSunday = (int)nowCt.DayOfWeek;
        DateTime thisWeekStartCt = nowCt.Date.AddDays(-daysSinceSunday);
        DateTime thisWeekStartUtc = TimeZoneInfo.ConvertTimeToUtc(thisWeekStartCt, ct);

        if (questWeekStart < thisWeekStartUtc)
        {
            questsCompletedThisWeek = 0;
            weeklyMilestonesClaimed = new bool[8];
            questWeekStart = thisWeekStartUtc;
            Debug.Log("[Quests] Weekly milestone track reset.");
        }
    }

    // ── Public API ───────────────────────────────────────────────

    public List<ActiveQuest> GetActiveQuests()
    {
        return activeQuests.Where(q => !q.isClaimed).ToList();
    }

    public bool TryClaimQuest(string questID)
    {
        ActiveQuest quest = activeQuests.Find(q => q.questID == questID);
        if (quest == null || !quest.isCompleted || quest.isClaimed) return false;

        QuestData data = allQuests.Find(q => q.questID == questID);
        if (data == null) return false;

        CurrencyManager.Instance?.AddCoins(data.coinReward);
        quest.isClaimed = true;
        questsCompletedThisWeek++;

        Debug.Log($"[Quests] Claimed quest '{data.displayName}' for {data.coinReward} coins. Week total: {questsCompletedThisWeek}");
        SaveManager.Instance?.SaveGame();
        OnQuestCompleted?.Invoke();
        return true;
    }

    public bool TryClaimMilestone(int tierIndex)
    {
        if (tierIndex < 0 || tierIndex >= 8) return false;
        if (weeklyMilestonesClaimed[tierIndex]) return false;
        if (questsCompletedThisWeek < MilestoneThresholds[tierIndex]) return false;

        CurrencyManager.Instance?.AddGems(MilestoneGems[tierIndex]);
        CurrencyManager.Instance?.AddCoins(MilestoneCoins[tierIndex]);
        weeklyMilestonesClaimed[tierIndex] = true;

        Debug.Log($"[Quests] Milestone {MilestoneThresholds[tierIndex]} claimed: {MilestoneGems[tierIndex]} gems, {MilestoneCoins[tierIndex]} coins.");
        SaveManager.Instance?.SaveGame();
        OnMilestoneClaimed?.Invoke();
        return true;
    }

    public DateTime GetNextDropTimeUtc()
    {
        TimeZoneInfo ct = TimeZoneInfo.FindSystemTimeZoneById("Central Standard Time");
        DateTime nowCt = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, ct);

        foreach (int hour in DropHoursCT)
        {
            DateTime candidate = nowCt.Date.AddHours(hour);
            if (candidate > nowCt)
                return TimeZoneInfo.ConvertTimeToUtc(candidate, ct);
        }
        // Next drop is 6am tomorrow
        return TimeZoneInfo.ConvertTimeToUtc(nowCt.Date.AddDays(1).AddHours(6), ct);
    }

    public int ActiveQuestCount => activeQuests.Count(q => !q.isClaimed);
    public bool HasUnclaimedCompleted => activeQuests.Any(q => q.isCompleted && !q.isClaimed);
    public bool HasNewDrops => activeQuests.Any(q => q.progress == 0 && !q.isClaimed);

    // ── Save / Load ──────────────────────────────────────────────

    public ActiveQuest[] GetActiveQuestsForSave() => activeQuests.ToArray();
    public string GetQuestWeekStartISO() => questWeekStart == DateTime.MinValue ? "" : questWeekStart.ToString("o");
    public string GetLastQuestDropTimeISO() => lastQuestDropTime == DateTime.MinValue ? "" : lastQuestDropTime.ToString("o");

    public void LoadState(ActiveQuest[] quests, int completedCount, bool[] milestones,
        string weekStart, string lastDrop)
    {
        activeQuests = (quests ?? Array.Empty<ActiveQuest>()).ToList();
        questsCompletedThisWeek = completedCount;
        weeklyMilestonesClaimed = milestones ?? new bool[8];

        if (!string.IsNullOrEmpty(weekStart) && DateTime.TryParse(weekStart, null,
            System.Globalization.DateTimeStyles.RoundtripKind, out DateTime ws))
            questWeekStart = ws;

        if (!string.IsNullOrEmpty(lastDrop) && DateTime.TryParse(lastDrop, null,
            System.Globalization.DateTimeStyles.RoundtripKind, out DateTime ld))
            lastQuestDropTime = ld;
    }

    // ── Event Wiring ─────────────────────────────────────────────

    private void SubscribeToEvents()
    {
        if (RunStats.Instance != null)
        {
            RunStats.Instance.OnCropHarvested += () => IncrementProgress(QuestObjectiveType.HarvestCrops);
            RunStats.Instance.OnSeedPlanted   += () => IncrementProgress(QuestObjectiveType.PlantSeeds);
            RunStats.Instance.OnPlantWatered  += () => IncrementProgress(QuestObjectiveType.WaterPlants);
            RunStats.Instance.OnDeerRepelled  += () => IncrementProgress(QuestObjectiveType.RepelDeer);
            RunStats.Instance.OnCrowRepelled  += () => IncrementProgress(QuestObjectiveType.RepelCrows);
        }
        if (AnimalManager.Instance != null)
        {
            AnimalManager.Instance.OnEggClaimed  += () => IncrementProgress(QuestObjectiveType.GatherEggs);
            AnimalManager.Instance.OnGemClaimed  += () => IncrementProgress(QuestObjectiveType.GatherGems);
        }
    }

    private void UnsubscribeFromEvents()
    {
        // Lambda subscriptions can't be unsubscribed by reference — acceptable for a singleton
        // that lives for the full session.
    }

    private void IncrementProgress(QuestObjectiveType type)
    {
        bool anyCompleted = false;
        foreach (ActiveQuest quest in activeQuests)
        {
            if (quest.isClaimed || quest.isCompleted) continue;
            QuestData data = allQuests.Find(q => q.questID == quest.questID);
            if (data == null || data.objectiveType != type) continue;
            quest.progress++;
            if (quest.progress >= data.targetCount)
            {
                quest.isCompleted = true;
                anyCompleted = true;
                Debug.Log($"[Quests] Quest complete: {data.displayName}");
            }
        }
        if (anyCompleted) OnQuestCompleted?.Invoke();
    }
}
```

- [ ] **Step 2: Verify compile**

Open Unity. Check Console — no compile errors. Fix any namespace/missing reference issues.

- [ ] **Step 3: Add `QuestManager` GameObject to scene**

In the Unity Hierarchy, create an empty GameObject named `QuestManager` under the `[MANAGERS]` group (following HierarchyColorizer convention). Add the `QuestManager` component to it.

- [ ] **Step 4: Commit**

```
git add Assets/Scripts/QuestManager.cs
git commit -m "feat: add QuestManager with scheduling, progress tracking, and milestone claiming"
```

---

## Task 5: Check AnimalManager for IsUnlocked method

`QuestManager` calls `AnimalManager.Instance.IsUnlocked(animalID)`. Verify this method exists.

**Files:**
- Possibly modify: `Assets/Scripts/AnimalManager.cs`

- [ ] **Step 1: Search for IsUnlocked in AnimalManager**

In `AnimalManager.cs`, look for a method named `IsUnlocked`. If it exists — skip to the commit. If it does not exist, add it:

```csharp
public bool IsUnlocked(string animalID)
{
    return unlockedAnimalIDs.Contains(animalID);
}
```

- [ ] **Step 2: Verify compile**

Check Console — no errors.

- [ ] **Step 3: Commit (only if file changed)**

```
git add Assets/Scripts/AnimalManager.cs
git commit -m "feat: expose IsUnlocked on AnimalManager for quest eligibility checks"
```

---

## Task 6: Extend SaveManager for quest state

**Files:**
- Modify: `Assets/Scripts/SaveManager.cs`

- [ ] **Step 1: Update `SaveGame()` to include quest data**

In `SaveManager.SaveGame()`, replace the `GameData data = new GameData(...)` call with the updated constructor that includes quest fields:

```csharp
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

GameData data = new GameData(
    CurrencyManager.Instance.Coins,
    CurrencyManager.Instance.Gems,
    animalIDs,
    equippedID,
    eggTime,
    quests,
    questsCompleted,
    milestones,
    weekStart,
    lastDrop
);
```

- [ ] **Step 2: Update `LoadGame()` to restore quest state**

In `SaveManager.LoadGame()`, after the `AnimalManager.Instance.LoadState(...)` call, add:

```csharp
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
```

- [ ] **Step 3: Verify compile + smoke test save/load**

Enter Play mode. Open the Console. You should see `[Quests] Dropped X new quest(s).` on first run (since `lastQuestDropTime` is empty, all recent drop windows fire). Exit Play mode. Re-enter Play mode — quests should NOT re-drop (they were saved). If no quests dropped at all, check that the `QuestManager` GO is in the scene and `allQuests` list is populated (it won't be yet until Task 7 — that's fine for now; the log will say "Dropped 0").

- [ ] **Step 4: Commit**

```
git add Assets/Scripts/SaveManager.cs
git commit -m "feat: save and load quest state via SaveManager"
```

---

## Task 7: Create QuestData ScriptableObject assets

**Files:**
- Create: `Assets/Data/Quests/` (7 assets)

- [ ] **Step 1: Create the Quests folder**

In the Unity Project window, navigate to `Assets/Data/` and create a folder named `Quests`.

- [ ] **Step 2: Create 7 QuestData assets**

Right-click in `Assets/Data/Quests/` → Create → Farm Game → Quest Data. Create and configure each asset:

| Asset name | questID | displayName | description | objectiveType | targetCount | coinReward | requiredUnlockID | requiredAnimalID |
|---|---|---|---|---|---|---|---|---|
| `Quest_HarvestCrops` | `harvest_crops` | Bountiful Harvest | Harvest 50 crops | HarvestCrops | 50 | 150 | _(empty)_ | _(empty)_ |
| `Quest_PlantSeeds` | `plant_seeds` | Green Thumb | Plant 500 seeds | PlantSeeds | 500 | 200 | _(empty)_ | _(empty)_ |
| `Quest_WaterPlants` | `water_plants` | Hydration Station | Water 500 plants | WaterPlants | 500 | 175 | _(empty)_ | _(empty)_ |
| `Quest_RepelDeer` | `repel_deer` | Deer Patrol | Repel 25 deer | RepelDeer | 25 | 200 | `fence` | _(empty)_ |
| `Quest_RepelCrows` | `repel_crows` | Crow Watch | Repel 25 crows | RepelCrows | 25 | 200 | `scarecrow` | _(empty)_ |
| `Quest_GatherEggs` | `gather_eggs` | Egg Collector | Gather 5 eggs from your Chicken | GatherEggs | 5 | 100 | _(empty)_ | `chicken` |
| `Quest_GatherGems` | `gather_gems` | Gem Seeker | Gather 5 gems from your Rooster | GatherGems | 5 | 150 | _(empty)_ | `rooster` |

- [ ] **Step 3: Wire assets into QuestManager**

Select the `QuestManager` GO in the Hierarchy. In the Inspector, expand `All Quests` and drag all 7 assets from `Assets/Data/Quests/` into the list.

- [ ] **Step 4: Verify drops in Play mode**

Enter Play mode. Console should show `[Quests] Dropped X new quest(s).` with at least the 3 always-eligible quests in the pool. Exit play mode.

- [ ] **Step 5: Commit**

```
git add Assets/Data/Quests/
git commit -m "feat: add 7 QuestData ScriptableObject assets"
```

---

## Task 8: QuestRow and MilestoneChip scripts

**Files:**
- Create: `Assets/Scripts/QuestRow.cs`
- Create: `Assets/Scripts/MilestoneChip.cs`

- [ ] **Step 1: Create `QuestRow.cs`**

```csharp
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class QuestRow : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private TextMeshProUGUI questNameText;
    [SerializeField] private TextMeshProUGUI descriptionText;
    [SerializeField] private TextMeshProUGUI progressText;
    [SerializeField] private TextMeshProUGUI rewardText;
    [SerializeField] private TextMeshProUGUI newBadgeText;
    [SerializeField] private TextMeshProUGUI completeLabel;
    [SerializeField] private Slider progressBar;
    [SerializeField] private Button claimButton;
    [SerializeField] private TextMeshProUGUI claimButtonText;
    [SerializeField] private Image backgroundImage;
    [SerializeField] private Image borderImage;

    [Header("State Colors")]
    [SerializeField] private Color inProgressBg = new Color(0.122f, 0.102f, 0.039f, 1f);      // #1f1a0a
    [SerializeField] private Color completedBg  = new Color(0.102f, 0.180f, 0.063f, 1f);      // #1a2e10
    [SerializeField] private Color newBg        = new Color(0.102f, 0.063f, 0.125f, 1f);      // #1a1020
    [SerializeField] private Color inProgressBorder = new Color(0.227f, 0.180f, 0.063f, 1f);  // #3a2e10
    [SerializeField] private Color completedBorder  = new Color(0.227f, 0.376f, 0.125f, 1f);  // #3a6020
    [SerializeField] private Color newBorder        = new Color(0.290f, 0.125f, 0.376f, 1f);  // #4a2060

    [Header("Bar Colors")]
    [SerializeField] private Color harvestColor = new Color(0.545f, 0.765f, 0.290f, 1f); // #8BC34A green
    [SerializeField] private Color waterColor   = new Color(0.290f, 0.624f, 0.875f, 1f); // #4a9fdf blue
    [SerializeField] private Color newColor     = new Color(0.690f, 0.502f, 1.000f, 1f); // #b080ff purple
    [SerializeField] private Color defaultColor = new Color(0.545f, 0.765f, 0.290f, 1f); // green

    private string currentQuestID;

    public void Bind(ActiveQuest quest, QuestData data)
    {
        currentQuestID = quest.questID;
        questNameText.text = data.displayName;
        descriptionText.text = data.description;
        rewardText.text = "🪙" + data.coinReward;

        float fill = data.targetCount > 0 ? (float)quest.progress / data.targetCount : 0f;
        progressBar.value = fill;

        bool isNew = quest.progress == 0;

        // State: completed
        if (quest.isCompleted)
        {
            backgroundImage.color = completedBg;
            borderImage.color = completedBorder;
            progressText.text = "";
            completeLabel.gameObject.SetActive(true);
            newBadgeText.gameObject.SetActive(false);
            claimButton.gameObject.SetActive(true);
            claimButtonText.text = "Claim\n🪙" + data.coinReward;
            questNameText.color = new Color(0.545f, 0.765f, 0.290f, 1f);
            progressBar.fillRect.GetComponent<Image>().color = harvestColor;
        }
        // State: new (no progress)
        else if (isNew)
        {
            backgroundImage.color = newBg;
            borderImage.color = newBorder;
            progressText.text = "0 / " + data.targetCount;
            completeLabel.gameObject.SetActive(false);
            newBadgeText.gameObject.SetActive(true);
            claimButton.gameObject.SetActive(false);
            questNameText.color = Color.white;
            progressBar.fillRect.GetComponent<Image>().color = newColor;
        }
        // State: in progress
        else
        {
            backgroundImage.color = inProgressBg;
            borderImage.color = inProgressBorder;
            progressText.text = quest.progress + " / " + data.targetCount;
            completeLabel.gameObject.SetActive(false);
            newBadgeText.gameObject.SetActive(false);
            claimButton.gameObject.SetActive(false);
            questNameText.color = Color.white;

            progressBar.fillRect.GetComponent<Image>().color = data.objectiveType switch
            {
                QuestObjectiveType.WaterPlants => waterColor,
                _ => defaultColor
            };
        }

        claimButton.onClick.RemoveAllListeners();
        claimButton.onClick.AddListener(OnClaimClicked);
    }

    private void OnClaimClicked()
    {
        if (QuestManager.Instance != null && QuestManager.Instance.TryClaimQuest(currentQuestID))
        {
            QuestPopup.Instance?.RefreshList();
        }
    }
}
```

- [ ] **Step 2: Create `MilestoneChip.cs`**

```csharp
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class MilestoneChip : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI tierLabel;
    [SerializeField] private TextMeshProUGUI gemLabel;
    [SerializeField] private TextMeshProUGUI stateIcon;
    [SerializeField] private Image backgroundImage;
    [SerializeField] private Image borderImage;

    [Header("State Colors")]
    [SerializeField] private Color claimedBg     = new Color(0.227f, 0.180f, 0.063f, 1f); // #3a2e10
    [SerializeField] private Color claimedBorder  = new Color(0.416f, 0.353f, 0.125f, 1f); // #6a5a20
    [SerializeField] private Color nextBg         = new Color(0.165f, 0.122f, 0.039f, 1f); // #2a1f0a
    [SerializeField] private Color nextBorder      = new Color(1.000f, 0.843f, 0.000f, 1f); // #FFD700
    [SerializeField] private Color lockedBg        = new Color(0.102f, 0.078f, 0.035f, 1f); // #1a1409
    [SerializeField] private Color lockedBorder    = new Color(0.267f, 0.267f, 0.267f, 1f); // #444

    private int tierIndex;
    private int questsRequired;

    public void Bind(int index, int required, int gemReward, bool isClaimed, bool isNext)
    {
        tierIndex = index;
        questsRequired = required;
        tierLabel.text = required.ToString();
        gemLabel.text = "💎" + gemReward;

        bool isFinalTier = index == 7;

        if (isClaimed)
        {
            backgroundImage.color = claimedBg;
            borderImage.color = claimedBorder;
            stateIcon.text = "✓";
            stateIcon.color = new Color(0.545f, 0.765f, 0.290f, 1f); // green
            gemLabel.color = new Color(0.494f, 0.812f, 1.000f, 1f);
        }
        else if (isNext)
        {
            backgroundImage.color = nextBg;
            borderImage.color = nextBorder;
            stateIcon.text = "→";
            stateIcon.color = new Color(1f, 0.843f, 0f, 1f); // gold
            gemLabel.color = new Color(0.494f, 0.812f, 1.000f, 1f);
        }
        else
        {
            backgroundImage.color = lockedBg;
            borderImage.color = lockedBorder;
            stateIcon.text = isFinalTier ? "★" : "○";
            stateIcon.color = new Color(0.333f, 0.333f, 0.333f, 1f);
            gemLabel.color = new Color(0.267f, 0.400f, 0.533f, 1f);
        }

        GetComponent<Button>()?.onClick.RemoveAllListeners();
        GetComponent<Button>()?.onClick.AddListener(OnChipClicked);
    }

    private void OnChipClicked()
    {
        if (QuestManager.Instance != null && QuestManager.Instance.TryClaimMilestone(tierIndex))
        {
            QuestPopup.Instance?.RefreshMilestoneStrip();
        }
    }
}
```

- [ ] **Step 3: Verify compile**

Check Console — no errors.

- [ ] **Step 4: Commit**

```
git add Assets/Scripts/QuestRow.cs Assets/Scripts/MilestoneChip.cs
git commit -m "feat: add QuestRow and MilestoneChip UI components"
```

---

## Task 9: QuestPopup script

**Files:**
- Create: `Assets/Scripts/QuestPopup.cs`

- [ ] **Step 1: Create `QuestPopup.cs`**

```csharp
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;
using System.Collections.Generic;

public class QuestPopup : MonoBehaviour
{
    public static QuestPopup Instance { get; private set; }

    [Header("Popup Structure")]
    [SerializeField] private CanvasGroup canvasGroup;
    [SerializeField] private RectTransform popupContainer;
    [SerializeField] private Button backdropButton;
    [SerializeField] private Button closeButton;

    [Header("Weekly Track Strip")]
    [SerializeField] private TextMeshProUGUI weeklyCountLabel;   // "18 / 40 quests · resets Sun"
    [SerializeField] private Slider weeklyProgressBar;
    [SerializeField] private List<MilestoneChip> milestoneChips; // 8 chips, wired in inspector

    [Header("Quest List")]
    [SerializeField] private Transform questListContent;         // Scroll view content root
    [SerializeField] private GameObject questRowPrefab;
    [SerializeField] private TextMeshProUGUI footerText;         // "Next drop in Xh Xm · Y / 10 slots used"

    [Header("Quest Button")]
    [SerializeField] private Button questButton;
    [SerializeField] private GameObject notificationDot;

    [Header("Animation")]
    [SerializeField] private float fadeInDuration = 0.3f;
    [SerializeField] private LeanTweenType easeType = LeanTweenType.easeOutQuad;

    private bool isOpen = false;
    private List<GameObject> spawnedRows = new List<GameObject>();

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        canvasGroup.alpha = 0f;
        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = false;
        popupContainer.localScale = Vector3.one * 0.8f;
    }

    private void Start()
    {
        backdropButton.onClick.AddListener(Close);
        closeButton.onClick.AddListener(Close);
        questButton.onClick.AddListener(Open);

        if (QuestManager.Instance != null)
        {
            QuestManager.Instance.OnQuestCompleted += UpdateNotificationDot;
            QuestManager.Instance.OnQuestsDropped  += UpdateNotificationDot;
            QuestManager.Instance.OnQuestCompleted += () => { if (isOpen) RefreshList(); };
        }

        UpdateNotificationDot();
    }

    private void OnDestroy()
    {
        if (QuestManager.Instance != null)
        {
            QuestManager.Instance.OnQuestCompleted -= UpdateNotificationDot;
            QuestManager.Instance.OnQuestsDropped  -= UpdateNotificationDot;
        }
    }

    public void Open()
    {
        if (isOpen) return;
        isOpen = true;
        RefreshAll();

        LeanTween.cancel(popupContainer.gameObject);
        LeanTween.cancel(canvasGroup.gameObject);

        canvasGroup.interactable = true;
        canvasGroup.blocksRaycasts = true;
        LeanTween.alphaCanvas(canvasGroup, 1f, fadeInDuration).setEase(easeType);
        LeanTween.scale(popupContainer, Vector3.one, fadeInDuration).setEase(easeType);
    }

    public void Close()
    {
        if (!isOpen) return;
        isOpen = false;

        LeanTween.cancel(popupContainer.gameObject);
        LeanTween.cancel(canvasGroup.gameObject);

        LeanTween.alphaCanvas(canvasGroup, 0f, fadeInDuration).setEase(easeType)
            .setOnComplete(() =>
            {
                canvasGroup.interactable = false;
                canvasGroup.blocksRaycasts = false;
            });
        LeanTween.scale(popupContainer, Vector3.one * 0.8f, fadeInDuration).setEase(easeType);
    }

    public void RefreshAll()
    {
        RefreshMilestoneStrip();
        RefreshList();
        RefreshFooter();
    }

    public void RefreshMilestoneStrip()
    {
        if (QuestManager.Instance == null) return;

        int completed = QuestManager.Instance.QuestsCompletedThisWeek;
        bool[] claimed = QuestManager.Instance.WeeklyMilestonesClaimed;
        int[] thresholds = { 5, 10, 15, 20, 25, 30, 35, 40 };
        int[] gems       = { 1,  1,  2,  2,  2,  2,  2, 10 };

        weeklyCountLabel.text = $"{completed} / 40 quests · resets Sun";
        weeklyProgressBar.value = Mathf.Clamp01(completed / 40f);

        // Find the first unclaimed, reachable tier (the "next" chip)
        int nextTier = -1;
        for (int i = 0; i < 8; i++)
        {
            if (!claimed[i])
            {
                nextTier = i;
                break;
            }
        }

        for (int i = 0; i < milestoneChips.Count && i < 8; i++)
        {
            milestoneChips[i].Bind(i, thresholds[i], gems[i], claimed[i], i == nextTier);
        }
    }

    public void RefreshList()
    {
        if (QuestManager.Instance == null) return;

        // Clear existing rows
        foreach (GameObject row in spawnedRows)
            Destroy(row);
        spawnedRows.Clear();

        List<ActiveQuest> quests = QuestManager.Instance.GetActiveQuests();

        // Sort: completed first, then new (progress==0), then in-progress
        quests.Sort((a, b) =>
        {
            int scoreA = a.isCompleted ? 0 : (a.progress == 0 ? 2 : 1);
            int scoreB = b.isCompleted ? 0 : (b.progress == 0 ? 2 : 1);
            return scoreA.CompareTo(scoreB);
        });

        foreach (ActiveQuest quest in quests)
        {
            QuestData data = FindQuestData(quest.questID);
            if (data == null) continue;

            GameObject rowGO = Instantiate(questRowPrefab, questListContent);
            QuestRow row = rowGO.GetComponent<QuestRow>();
            row.Bind(quest, data);
            spawnedRows.Add(rowGO);
        }

        RefreshFooter();
    }

    private void RefreshFooter()
    {
        if (QuestManager.Instance == null) return;

        DateTime nextDrop = QuestManager.Instance.GetNextDropTimeUtc();
        TimeSpan remaining = nextDrop - DateTime.UtcNow;
        if (remaining < TimeSpan.Zero) remaining = TimeSpan.Zero;
        int hours = (int)remaining.TotalHours;
        int minutes = remaining.Minutes;

        string timeStr = hours > 0 ? $"{hours}h {minutes}m" : $"{minutes}m";
        footerText.text = $"Next drop in {timeStr} · {QuestManager.Instance.ActiveQuestCount} / 10 slots used";
    }

    private void UpdateNotificationDot()
    {
        if (QuestManager.Instance == null) { notificationDot.SetActive(false); return; }
        notificationDot.SetActive(QuestManager.Instance.HasUnclaimedCompleted || QuestManager.Instance.HasNewDrops);
    }

    private QuestData FindQuestData(string questID)
    {
        // QuestData lookup — QuestManager holds the list
        // Access via reflection of allQuests is messy; expose a getter instead
        return QuestManager.Instance?.GetQuestData(questID);
    }
}
```

- [ ] **Step 2: Add `GetQuestData` to `QuestManager`**

In `QuestManager.cs`, add this public method to the Public API section:

```csharp
public QuestData GetQuestData(string questID)
{
    return allQuests.Find(q => q.questID == questID);
}
```

- [ ] **Step 3: Verify compile**

Check Console — no errors.

- [ ] **Step 4: Commit**

```
git add Assets/Scripts/QuestPopup.cs Assets/Scripts/QuestManager.cs
git commit -m "feat: add QuestPopup controller with milestone strip and quest list"
```

---

## Task 10: Build QuestPopup UI hierarchy in scene

Build the popup to match the approved mockup exactly. Colors and sizes reference the spec.

**Files:**
- Modify: Scene (`Assets/Scenes/SampleScene.unity`)
- Create: `Assets/Prefabs/Quests/QuestRow.prefab`

- [ ] **Step 1: Create QuestRow prefab**

In the Hierarchy, create a new UI GameObject named `QuestRow` (don't parent it anywhere yet). Build this structure:

```
QuestRow (RectTransform, Image [background], Outline/border component or use a Border Image child)
├── BorderImage (Image — outline frame, set as sibling behind content or use a separate panel)
├── NewBadge (TextMeshPro — "NEW", 11px, purple #b080ff, uppercase)
├── QuestNameText (TextMeshPro — 13px, bold, white/green depending on state)
├── DescriptionText (TextMeshPro — 11px, grey #aaaaaa)
├── CompleteLabel (TextMeshPro — "✓ Complete!", 11px, green, hidden by default)
├── ProgressText (TextMeshPro — "312 / 500", 11px, right-aligned)
├── RewardText (TextMeshPro — "🪙200", 11px, light blue #7ecfff, right-aligned)
├── ProgressBar (Slider — interactable OFF, fill rect colored by state, height 5px)
└── ClaimButton (Button + Image, green background #4a8020, hidden by default)
    └── ClaimButtonText (TextMeshPro — "Claim\n🪙150")
```

Set `QuestRow` height to ~90px, full width. Padding: 10px vertical, 12px horizontal.

Drag `QuestRow` into `Assets/Prefabs/Quests/QuestRow.prefab`. Wire all serialized fields on the `QuestRow` component in the prefab.

Delete the scene instance of `QuestRow` after saving the prefab.

- [ ] **Step 2: Build QuestPopup hierarchy in Canvas**

Under the main Canvas, create this hierarchy:

```
QuestPopupRoot (CanvasGroup)
├── Backdrop (Image — full screen, black 50% alpha, Button component for close)
└── PopupPanel (RectTransform — 360px wide, auto height, centered)
    ├── Header (Image — #2a1f0a, 50px tall)
    │   ├── TitleText (TMP — "📋 Daily Quests", #FFD700, bold, 17px)
    │   └── CloseButton (Button + TMP "✕", 20px, top-right)
    ├── WeeklyStrip (Image — #1f1608)
    │   ├── WeeklyCountLabel (TMP — 11px, grey)
    │   ├── WeeklyProgressBar (Slider — interactable OFF, gradient fill via Image)
    │   └── MilestoneRow (Horizontal Layout Group, 8 children)
    │       ├── MilestoneChip_0 … MilestoneChip_7
    │       │   (each: Image bg + border, TMP tier, TMP gem, TMP icon, Button)
    ├── ScrollView (Scroll Rect — vertical only)
    │   └── Content (Vertical Layout Group, auto-size)
    └── Footer (Image — dark bg)
        └── FooterText (TMP — 11px, muted)
```

- [ ] **Step 3: Add QuestPopup component and wire all serialized fields**

Select `QuestPopupRoot`. Add `QuestPopup` component. In the Inspector, wire:
- `canvasGroup` → QuestPopupRoot's CanvasGroup
- `popupContainer` → PopupPanel RectTransform
- `backdropButton` → Backdrop Button
- `closeButton` → Header CloseButton
- `weeklyCountLabel` → WeeklyCountLabel TMP
- `weeklyProgressBar` → WeeklyProgressBar Slider
- `milestoneChips` → drag in MilestoneChip_0 through MilestoneChip_7
- `questListContent` → ScrollView/Content Transform
- `questRowPrefab` → QuestRow.prefab
- `footerText` → FooterText TMP

Wire each `MilestoneChip` component's fields on each chip GO.

Set `QuestPopupRoot` initial state: CanvasGroup alpha=0, interactable=false, blocksRaycasts=false.

- [ ] **Step 4: Enter Play mode and open popup manually**

In Play mode, call `QuestPopup.Instance.Open()` via a temporary test button or the Unity `[ContextMenu]` attribute. Verify:
- Popup fades in with scale animation
- Weekly strip shows correct count and 8 milestone chips
- Quest rows render (at least the 3 always-eligible quests after first drop)
- Footer shows next drop time

- [ ] **Step 5: Commit**

```
git add Assets/Scenes/SampleScene.unity Assets/Prefabs/Quests/
git commit -m "feat: build QuestPopup UI hierarchy and QuestRow prefab"
```

---

## Task 11: Quest floating button

**Files:**
- Modify: Scene (`Assets/Scenes/SampleScene.unity`)

- [ ] **Step 1: Add Quest button to Canvas**

In the Canvas, find the basket button (DailyRewardManager's chest button). Directly below it in the hierarchy, create a new GO:

```
QuestButton (RectTransform, Image, Button)
├── QuestIcon (TextMeshPro — "📋", centered, ~24px)
└── NotificationDot (Image — red circle, ~14px, anchored top-right corner of QuestButton)
```

Match the size and anchoring of the basket button (typically ~80x80px, top-left anchor, offset below the basket). Set background color to match the warm brown style of existing buttons.

- [ ] **Step 2: Wire into QuestPopup**

Select `QuestPopupRoot`. In the `QuestPopup` Inspector:
- `questButton` → QuestButton's Button component
- `notificationDot` → NotificationDot GO

Set `NotificationDot` active=false by default.

- [ ] **Step 3: Verify end-to-end in Play mode**

Enter Play mode. Confirm:
- Quest button appears below the basket
- Tapping the quest button opens the popup with correct animation
- Notification dot appears if any quest is complete or newly dropped
- Tapping the backdrop or ✕ closes the popup
- Claiming a quest removes it from the list and updates the weekly counter
- Tapping a reachable milestone chip grants gems + coins (check Console log)

- [ ] **Step 4: Commit**

```
git add Assets/Scenes/SampleScene.unity
git commit -m "feat: add quest floating button with notification dot"
```

---

## Task 12: Final verification and cleanup

- [ ] **Step 1: Weekly reset smoke test**

In Play mode, open `QuestManager.cs` and temporarily change `CheckWeeklyReset()` to always reset (set `questWeekStart = DateTime.MinValue` at the top of the method). Enter Play mode — `questsCompletedThisWeek` should be 0 and all milestone chips should show locked. Revert the temporary change.

- [ ] **Step 2: Verify notification dot logic**

Use the Unity Inspector in Play mode to manually set `quest.isCompleted = true` on an active quest via `QuestManager.Instance.GetActiveQuests()[0].isCompleted = true`. The notification dot should appear on the quest button.

- [ ] **Step 3: Verify save/load round trip**

Enter Play mode → let quests drop → claim one → exit Play mode → re-enter Play mode. The quest list, weekly count, and milestone states should persist correctly. Check the `gamedata.json` file in `Application.persistentDataPath` to confirm `activeQuests` is populated.

- [ ] **Step 4: Check for missing quest data gracefully**

If `GetQuestData()` returns null for a quest in the pool (e.g. an asset was deleted), `QuestPopup.RefreshList()` skips that row silently. Verify no null reference exceptions by temporarily removing a quest from the `allQuests` list in the Inspector.

- [ ] **Step 5: Final commit**

```
git add -A
git commit -m "feat: daily quests and weekly milestone track complete"
```
