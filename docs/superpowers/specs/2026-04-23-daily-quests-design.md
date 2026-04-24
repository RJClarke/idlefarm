# Daily Quests & Weekly Milestone Track — Design Spec

## Overview

A daily quest system that drops 2 new quests every 6 hours into a rolling pool (max 10 held). Completing quests earns coins and advances a weekly milestone track that rewards gems. Quests are eligibility-gated based on what the player has unlocked. The weekly track resets every Sunday at midnight CT.

---

## Quest Drop Schedule

- **Drop cadence:** Every 6 hours at fixed CT times — 6am, 12pm, 6pm, 12am
- **Quests per drop:** 2
- **Pool cap:** 10 active quests maximum. If the pool is full at drop time, new quests are skipped until slots open.
- **Missed drops:** If the app was closed across multiple drop windows, catch-up drops are applied on next open (still capped at 10 total).
- **Cross-week carry:** Active quests are never cleared on weekly reset. A quest in progress during a week boundary completes and counts toward the new week's milestone track.

---

## Quest Types & Pool

Each quest type is a `QuestData` ScriptableObject. The full initial pool:

| Quest Name | Objective Type | Target | Coin Reward | Eligibility |
|---|---|---|---|---|
| Bountiful Harvest | HarvestCrops | 50 | 150 | Always |
| Green Thumb | PlantSeeds | 500 | 200 | Always |
| Hydration Station | WaterPlants | 500 | 175 | Always |
| Deer Patrol | RepelDeer | 25 | 200 | Fence unlocked |
| Crow Watch | RepelCrows | 25 | 200 | Scarecrow unlocked |
| Egg Collector | GatherEggs | 5 | 100 | Chicken owned |
| Gem Seeker | GatherGems | 5 | 150 | Rooster owned |

**Eligibility gating:** Before each drop, the `QuestManager` filters the pool to only eligible quest types:
- `requiredUnlockID` — checked against `UpgradeManager` (e.g. `"fence"`, `"scarecrow"`)
- `requiredAnimalID` — checked against `AnimalManager.IsUnlocked()` (e.g. `"chicken"`, `"rooster"`)
- Empty string = always eligible

Quests are selected randomly from eligible types, excluding any already active in the pool.

Progress accumulates **across runs** — a quest is not scoped to a single run. All progress is tracked cumulatively within the 6-hour window and beyond until claimed.

---

## QuestData ScriptableObject

```
[CreateAssetMenu(menuName = "Farm Game/Quest Data", order = 8)]

string questID              — unique ID, e.g. "harvest_crops"
string displayName          — e.g. "Bountiful Harvest"
string description          — e.g. "Harvest 50 crops"
QuestObjectiveType objectiveType  — enum value
int targetCount             — e.g. 50
int coinReward              — e.g. 150
string requiredUnlockID     — empty = no unlock required
string requiredAnimalID     — empty = no animal required
```

**QuestObjectiveType enum:**
```
HarvestCrops, PlantSeeds, WaterPlants, RepelDeer, RepelCrows, GatherEggs, GatherGems
```

---

## ActiveQuest (Runtime Class)

Plain C# class — not a ScriptableObject. Represents a quest currently in the player's pool.

```
string questID          — links back to QuestData
int progress            — current count toward target
bool isCompleted        — true when progress >= target
bool isClaimed          — true after coin reward collected
string droppedAt        — UTC ISO 8601 — when this quest was added (display use only, e.g. "Added 3h ago")
```

---

## QuestManager Singleton

`QuestManager : MonoBehaviour` — follows existing singleton pattern.

### Scheduling

On `Start()` and on app resume, compare `lastQuestDropTime` (UTC) to now:
- Convert 6am/12pm/6pm/12am CT to UTC using `TimeZoneInfo.FindSystemTimeZoneById("Central Standard Time")` — handles DST automatically on Windows/Unity
- For each elapsed drop window since `lastQuestDropTime`, attempt to add 2 quests
- Stop adding if pool reaches 10
- Update `lastQuestDropTime` to the most recent elapsed drop time
- Fire `OnQuestsDropped` if any quests were added

### Progress Tracking

Subscribe to events on `Start()`. The following events need to be added to `RunStats` as `Action` delegates (currently RunStats uses increment methods only):

| Event to add to RunStats | Drives quest type |
|---|---|
| `Action OnCropHarvested` | HarvestCrops |
| `Action OnSeedPlanted` | PlantSeeds |
| `Action OnPlantWatered` | WaterPlants |
| `Action OnDeerRepelled` | RepelDeer |
| `Action OnCrowRepelled` | RepelCrows |

Animal-sourced events from `AnimalManager`:

| Event | Drives quest type |
|---|---|
| `OnEggClaimed` | GatherEggs |
| `OnGemDropClaimed` | GatherGems |

On each event, increment `progress` on all matching active, uncompleted quests. When progress reaches `targetCount`, mark `isCompleted = true` and fire `OnQuestCompleted`.

### Quest Claiming

`TryClaimQuest(string questID)`:
- Validates quest is completed and unclaimed
- Grants `coinReward` via `CurrencyManager.AddCoins()`
- Sets `isClaimed = true`
- Increments `questsCompletedThisWeek`
- Triggers save
- Returns true on success

### Weekly Reset

On `Start()`, compute the most recent Sunday midnight CT (UTC). If `questWeekStart` is before that:
- `questsCompletedThisWeek` → 0
- `weeklyMilestonesClaimed` → all false
- `questWeekStart` → new Sunday midnight UTC
- `activeQuests` → **unchanged** (carry over — do not clear)
- Save state

### Milestone Claiming

`TryClaimMilestone(int tierIndex)` — tiers 0–7 corresponding to 5/10/15/20/25/30/35/40:
- Validates `questsCompletedThisWeek >= tierThreshold[tierIndex]`
- Validates `weeklyMilestonesClaimed[tierIndex] == false`
- Grants gems + coins via `CurrencyManager`
- Sets `weeklyMilestonesClaimed[tierIndex] = true`
- Triggers save
- Fires `OnMilestoneClaimed`

### Milestone Rewards

| Tier | Quests Required | Gem Reward | Coin Reward |
|---|---|---|---|
| 0 | 5 | 1 | 50 |
| 1 | 10 | 1 | 100 |
| 2 | 15 | 2 | 150 |
| 3 | 20 | 2 | 200 |
| 4 | 25 | 2 | 200 |
| 5 | 30 | 2 | 250 |
| 6 | 35 | 2 | 250 |
| 7 | 40 | 10 | 500 |

*Coin rewards are provisional tunable defaults — adjust in QuestManager inspector or constants.*

### Public API

```csharp
List<ActiveQuest> GetActiveQuests()
bool TryClaimQuest(string questID)
bool TryClaimMilestone(int tierIndex)
int QuestsCompletedThisWeek { get; }
bool[] WeeklyMilestonesClaimed { get; }
event Action OnQuestCompleted
event Action OnQuestsDropped
event Action OnMilestoneClaimed
```

---

## Save Data

### GameData Extensions

```
GameData (JSON)
├── coins                       (existing)
├── gems                        (existing)
├── ...                         (existing)
├── activeQuests                (new — ActiveQuest[])
├── questsCompletedThisWeek     (new — int)
├── weeklyMilestonesClaimed     (new — bool[8])
├── questWeekStart              (new — string, UTC ISO 8601)
└── lastQuestDropTime           (new — string, UTC ISO 8601)
```

`JsonUtility.FromJson` handles missing fields gracefully — existing saves get an empty quest pool on first load, no migration needed.

`ActiveQuest` must be marked `[Serializable]` for `JsonUtility` to serialize it. Since `JsonUtility` doesn't serialize `List<T>` at the top level, use a wrapper or `ActiveQuest[]`.

---

## UI

### Quest Button (Floating)

- **Position:** Top-left, directly below the basket/daily rewards button
- **Size:** Same style as existing floating buttons (basket, egg button)
- **Content:** 📋 icon (or quest scroll sprite)
- **Notification dot:** Red dot appears when: (a) new quests dropped, or (b) a quest is completed and unclaimed
- **Tap:** Opens `QuestPopup`

### QuestPopup

Popup overlay — fade + scale 0.8→1.0 animation (matching `DailyRewardPopup`, 0.3s easeOutQuad). Closed by X button or backdrop tap.

**Visual design is locked to the approved mockup.** Key specs:

**Header:**
- Dark warm background (`#2a1f0a`)
- "📋 Daily Quests" in gold (`#FFD700`), bold, 17px
- ✕ close button top-right

**Weekly Track Strip** (always visible, above quest list):
- Slightly darker background (`#1f1608`)
- Row 1: "WEEKLY TRACK" label (left) + "X / 40 quests · resets Sun" (right), both 11px muted
- Row 2: Gradient progress bar (gold → orange), 8px tall, with 7 tick marks at each milestone boundary
- Row 3: 8 milestone chips side-by-side, each showing:
  - Tier number (5, 10, 15... 40)
  - Gem reward (💎1, 💎1, 💎2... 💎10)
  - State: ✓ claimed (green), → next claimable (gold highlight border), ○ locked (muted)
  - Tier 7 (40) uses ★ icon

**Quest List** (scrollable):

Quest row states:
- **Completed, unclaimed:** Green border (`#3a6020`), green background tint, green title, "✓ Complete!" label, **Claim** button (green, shows coin amount)
- **In progress:** Warm dark background, white title, progress bar, count and coin reward top-right
- **New (just dropped):** Purple border (`#4a2060`), "NEW" label in purple above title, purple progress bar. The "NEW" badge shows until the quest has any progress (progress > 0).
- **Claimed:** Not shown (removed from list after claiming)

Progress bar colors by quest state: green (harvest), blue (water), default green (plant), purple (new). 8px height, full-width, rounded.

Footer (below list): "Next drop in Xh Xm · Y / 10 slots used" in muted small text.

---

## Future Considerations (Not Built Now)

- **Gem quest rewards:** Individual quests reward gems in addition to coins. Gated behind an upgrade, unlock, or building (TBD). The `QuestData` SO already has room for a `gemReward` field.
- **More quest types:** EarnMoney (earn $X in a run), CompleteRuns (finish X runs), etc.
- **Quest reroll:** Spend gems to swap out an active quest for a new one.
- **Difficulty scaling:** Quest targets scale with player progression (more tiles unlocked = higher harvest targets).
