# Farm Animals System — Design Spec

## Overview

A new animal companion system where players unlock, equip, and benefit from farm animals. One animal can be equipped at a time. Animals roam visibly on the home screen and during runs, each providing a unique passive or active ability. Introduces a new "Gems" premium currency.

## Currency: Gems

### Integration

Extend `CurrencyManager` with a third currency:
- `int Gems` property
- `AddGems(int)`, `SpendGems(int)`, `CanAffordGems(int)`, `SetGems(int)`
- `event Action<int> OnGemsChanged`
- `CurrencyUI` adds a purple gem display in the top currency bar (alongside coins and money)

### Gem Sources (This Build)

- **Daily rewards:** 1-2 gems sprinkled into some daily reward slots (alongside existing coin rewards)
- **Weekly completion bonus:** Include a gem bonus for completing all 7 days
- **Mayor's gift:** One-time ~50 gem narrative reward. Thematically: the mayor thanks the player for rebuilding the farm. Delivered via a future mail system (mail icon button with developer + in-game story messages). Exact trigger timing TBD — likely after a few runs or a coin milestone. Not built in this phase; gems are manually grantable for testing.

### Gem Sources (Future, Not Built Now)

- Interstitial ad claim (manual trigger)
- IAP gem bundles (with "remove ads" option converting ad gems to timer-based)
- Achievements
- Server-side validation required before real-money gem purchases go live

### Persistence

Stored in `GameData` JSON alongside coins. Local save is sufficient pre-monetization. Server-authoritative gem balance is a future requirement when IAP is added — at that point, all persistent data (coins, gems, unlocks) migrates to server-side.

---

## AnimalData ScriptableObject

`[CreateAssetMenu(menuName = "Farm Game/Animal Data", order = 7)]`

| Field | Type | Description |
|---|---|---|
| animalID | string | Unique ID, e.g. "chicken", "farm_dog" |
| displayName | string | Display name, e.g. "Chicken" |
| description | string | Ability description for popup |
| animalEmoji | string | Emoji fallback for UI, e.g. "🐔" |
| gemCost | int | Purchase price in gems |
| sortOrder | int | Display order in animal popup |
| abilityType | enum | PassiveTimer, RunDefender, or None |
| cooldownMinutes | float | For PassiveTimer: real-time cooldown (e.g. 20) |
| rewardCoins | int | For PassiveTimer: coins per claim |
| visualPrefab | GameObject | The roaming animal prefab |
| roamSpeed | float | Wander speed |
| iconSprite | Sprite | For equip button and popup (fallback to emoji) |

### Ability Types

- **PassiveTimer** — Produces a reward on a real-time cooldown (Chicken: egg every 20 min → coins)
- **RunDefender** — Active behavior during farm runs (Farm Dog: chases deer)
- **None** — Placeholder for future animals. Visible in popup as locked/coming soon.

### Initial Animals

| Animal | ID | Cost | Ability | Cooldown | Reward |
|---|---|---|---|---|---|
| Chicken | chicken | 100 | PassiveTimer | 20 min | 30 coins |
| Farm Dog | farm_dog | 500 | RunDefender | — | — |
| Rooster | rooster | 1,500 | None | — | — |
| Cow | cow | 3,000 | None | — | — |
| Pig | pig | 5,000 | None | — | — |
| Horse | horse | 8,000 | None | — | — |

---

## AnimalManager Singleton

### State

- `List<AnimalData> allAnimals` — all animal SOs (serialized in inspector)
- `HashSet<string> unlockedAnimalIDs` — purchased animals
- `string equippedAnimalID` — currently equipped (null = none)
- `GameObject activeVisualInstance` — the roaming animal on screen

### Public API

```
// Unlock & Equip
bool IsUnlocked(string animalID)
bool TryUnlockAnimal(string animalID)   → spends gems, returns success
void EquipAnimal(string animalID)        → sets equipped, spawns visual
void UnequipAnimal()                     → clears equipped, destroys visual
AnimalData GetEquippedAnimal()
AnimalData GetAnimalData(string animalID)

// Egg Timer (PassiveTimer ability)
bool IsEggReady                          → true if cooldown elapsed
DateTime LastClaimTime                   → persisted timestamp
void ClaimEgg()                          → gives coins, resets timer, removes egg visual
float GetCooldownProgress()              → 0-1 for optional UI timer

// Events
event Action<AnimalData> OnAnimalEquipped
event Action OnAnimalUnequipped
event Action OnEggReady
event Action OnEggClaimed
```

### Egg Timer Logic

- Stores `DateTime.UtcNow` of last claim as ISO 8601 string in save data
- On load: compares current time to `LastClaimTime + cooldownMinutes`
- Works whether game was open or closed — real-world time, not game time
- When timer completes: fires `OnEggReady`, tells the AnimalVisual to drop an egg sprite at its current position
- Claiming via egg button: gives coins, destroys egg sprite, resets timer

### Run Integration

- Subscribes to `RunManager.OnRunStarted` / `OnRunEnded`
- Animal visual persists through home screen and runs (no destroy/recreate)
- FarmDog: when equipped + run active → activates chase behavior
- Equip/unequip is allowed during runs (may be restricted later for balance)

---

## FarmDog Migration

### What Changes

- FarmDog no longer spawns based on `UpgradeManager.GetPermanentLevel("dog_unlock")`
- AnimalManager triggers FarmDog behavior: equipped + run active → chase mode
- Remove FarmDog from EquipmentData assets and equipment unlock flow
- Remove the dog_unlock UnlockData (replaced by gem purchase in AnimalPopup)

### What Stays the Same

- `FarmDog.cs` chase/repel logic (chase speed, 30s cooldown, `AnimalThreat.ForceRepel()`)
- `DogVisual.cs` visual component
- Interaction with `ThreatWaveManager` and `AnimalThreat` system

### Behavior by Phase

- **Home screen:** Dog wanders using AnimalVisual wander behavior
- **During runs:** Dog switches from wandering to active chase mode (existing chase logic)

---

## UI Components

### Animal Equip Button (Home Screen)

- **Position:** Bottom-left, anchored just above the nav bar
- **Size:** 44px square, rounded corners
- **Content:** Shows equipped animal's emoji/sprite. Silhouette placeholder when nothing equipped.
- **Visibility:** Always visible (even before any animals unlocked — teases the system)
- **Tap:** Opens AnimalPopup

### Egg Claim Button (Home Screen)

- **Position:** Stacked directly above the animal equip button (bottom-left)
- **Size:** 34px square (smaller than animal button)
- **Visibility:** Only visible when a PassiveTimer animal (Chicken) is equipped
- **States:**
  - Egg ready: Full color + red notification dot. Tap → claims coins, egg on ground disappears, cooldown restarts.
  - On cooldown: Greyed out / muted, no notification dot.
- **Visible during runs too** — egg can be claimed anytime.

### AnimalPopup (Vertical List)

- **Trigger:** Tap animal equip button
- **Animation:** Fade + scale 0.8→1.0 (same as DailyRewardPopup, 0.3s easeOutQuad)
- **Header:** "Farm Animals" title + gem count display
- **Layout:** Vertical scrollable list, full-width rows
- **Each row:** Emoji/icon (left), name + description (center), status/action (right)
- **Row states:**
  - **Unlocked + equipped:** Green highlight border, "EQUIPPED" badge. Tap to unequip.
  - **Unlocked + not equipped:** Neutral style, "EQUIP" button. Tap to equip.
  - **Locked + can afford:** Gem price button (active). Tap → confirmation: "Unlock Chicken for 💎 100?"
  - **Locked + can't afford:** Gem price (dimmed). Tap → "Need 💎 53 more" toast.
- **Close:** X button or backdrop tap

### Gem Counter in Currency Bar

- Added to `CurrencyUI` alongside coins display
- Purple gem icon + count with thousand separators
- Same animation-on-change behavior as coins

---

## AnimalVisual (Roaming Behavior)

### Wandering

- Attached to spawned animal prefab
- Random point → walk → pause → repeat (similar to HomeHelperWander)
- Flips sprite based on movement direction
- **Screen bounds clamping:** Wander targets are clamped to stay within visible camera bounds with a configurable padding inset. If the animal drifts outside (e.g. camera changes), it redirects toward center. Does not inherit the HomeHelperWander drift-off-screen issue.

### Egg Drop Visual (Chicken-specific)

- When egg timer completes, a small egg sprite is spawned at the chicken's current position on the ground
- Egg sprite persists until claimed via the egg button
- On claim: egg sprite is destroyed (fade out or pop animation)

### Persistence Across Phases

- Animal visual is NOT destroyed when a run starts or ends
- Same instance lives through home screen → run → home screen
- FarmDog: switches from wander mode to chase mode when run starts

---

## Save Data

### GameData Extensions

```
GameData (JSON)
├── coins               (existing)
├── gems                (new int)
├── unlockedAnimalIDs   (new string[])
├── equippedAnimalID    (new string, nullable)
├── lastEggClaimTime    (new string — DateTime UTC, ISO 8601)
```

### Migration Safety

- `JsonUtility.FromJson` handles missing fields gracefully — new fields default to null/0/empty
- Existing saves won't break; players will simply have no animals unlocked

### Daily Reward Gem Integration

- Add optional `gemReward` field to some daily reward slots
- Weekly completion bonus includes gems
- Gem amounts flow through `CurrencyManager.AddGems()`
- Daily reward state still uses PlayerPrefs (existing pattern)

---

## Future Considerations (Not Built Now)

- **Multi-equip:** Allow 2 animals equipped simultaneously (UI and balance changes)
- **Animal upgrades:** Spend gems for research/coop upgrades (reduce cooldown, increase payout)
- **Rooster/Cow/Pig/Horse abilities:** TBD utility for each
- **Server-side gem validation:** Required before IAP. Server becomes source of truth for all persistent data.
- **Ad-based gem earning:** Interstitial ad → gems, with IAP "remove ads" converting to timer-based claims
- **Achievements:** Additional gem earning sources
- **Settings button:** Small gear icon, top-right, rounded square — opens settings popup (volume, sound, account). Not part of this build.
