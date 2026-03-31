# Farm Animals System — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add a Farm Animals system with Gems currency, equippable animals (Chicken with egg timer, Farm Dog with run defense), animal picker popup, and home screen roaming visuals.

**Architecture:** Standalone AnimalManager singleton + AnimalData ScriptableObject, following existing patterns (CurrencyManager, EquipmentData, DailyRewardPopup). Gems added as third currency to CurrencyManager. FarmDog migrated from equipment/upgrade system to animal system.

**Tech Stack:** Unity 2D, C#, LeanTween (animations), TextMesh Pro (UI text), ScriptableObjects (data), JsonUtility (save/load)

**Spec:** `docs/superpowers/specs/2026-03-30-farm-animals-design.md`

---

## File Structure

### New Files
| File | Responsibility |
|---|---|
| `Assets/Scripts/AnimalData.cs` | ScriptableObject defining animal properties |
| `Assets/Scripts/AnimalManager.cs` | Singleton: unlock, equip, egg timer, save/load integration |
| `Assets/Scripts/AnimalVisual.cs` | Roaming behavior + egg drop visual for spawned animal |
| `Assets/Scripts/AnimalPopup.cs` | Vertical list popup for browsing/unlocking/equipping animals |
| `Assets/Scripts/AnimalEquipButton.cs` | Home screen button (bottom-left) showing equipped animal |
| `Assets/Scripts/EggClaimButton.cs` | Small button above animal button for claiming egg rewards |

### Modified Files
| File | Changes |
|---|---|
| `Assets/Scripts/CurrencyManager.cs` | Add Gems currency (property, methods, event) |
| `Assets/Scripts/CurrencyUI.cs` | Add gem count display element |
| `Assets/Scripts/GameData.cs` | Add gems, animal unlock/equip, egg timer fields |
| `Assets/Scripts/SaveManager.cs` | Save/load new GameData fields |
| `Assets/Scripts/FarmDog.cs` | Remove self-spawning, accept external control from AnimalManager |
| `Assets/Scripts/DailyRewardManager.cs` | Add gem rewards to some daily slots + weekly bonus |
| `Assets/Scripts/DailyRewardPopup.cs` | Display gem rewards in day cells |

---

## Task 1: Add Gems Currency to CurrencyManager

**Files:**
- Modify: `Assets/Scripts/CurrencyManager.cs`

- [ ] **Step 1: Add gems field, property, and event**

In `CurrencyManager.cs`, add alongside the existing coin fields (around line 16):

```csharp
private int currentGems = 0;
```

Add alongside existing events (around line 24):

```csharp
public event Action<int> OnGemsChanged;
```

Add alongside existing properties (around line 28):

```csharp
public int Gems => currentGems;
```

- [ ] **Step 2: Add gem methods**

Add after the coins methods section (after line ~176), mirroring the coin pattern:

```csharp
// ── Gems (Premium Currency) ──────────────────────────────

public void AddGems(int amount)
{
    if (amount <= 0)
    {
        Debug.LogWarning($"CurrencyManager: Tried to add invalid gem amount: {amount}");
        return;
    }
    currentGems += amount;
    Debug.Log($"Added {amount} gems. Total: {currentGems}");
    OnGemsChanged?.Invoke(currentGems);
}

public bool SpendGems(int amount)
{
    if (amount <= 0)
    {
        Debug.LogWarning($"CurrencyManager: Tried to spend invalid gem amount: {amount}");
        return false;
    }
    if (currentGems < amount)
    {
        Debug.LogWarning($"CurrencyManager: Not enough gems. Have: {currentGems}, Need: {amount}");
        return false;
    }
    currentGems -= amount;
    Debug.Log($"Spent {amount} gems. Remaining: {currentGems}");
    OnGemsChanged?.Invoke(currentGems);
    return true;
}

public bool CanAffordGems(int amount)
{
    return currentGems >= amount;
}

public void SetGems(int amount)
{
    currentGems = Mathf.Max(0, amount);
    OnGemsChanged?.Invoke(currentGems);
}
```

- [ ] **Step 3: Verify in Unity Editor**

Enter Play mode. In the Console, confirm no errors. Optionally test via a temporary script or the debug inspector by calling `CurrencyManager.Instance.AddGems(50)`.

- [ ] **Step 4: Commit**

```bash
git add Assets/Scripts/CurrencyManager.cs
git commit -m "feat: add Gems as third currency to CurrencyManager"
```

---

## Task 2: Add Gems to GameData and SaveManager

**Files:**
- Modify: `Assets/Scripts/GameData.cs`
- Modify: `Assets/Scripts/SaveManager.cs`

- [ ] **Step 1: Extend GameData with new fields**

In `GameData.cs`, add new fields after `public int coins;` (line 11):

```csharp
public int gems;
public string[] unlockedAnimalIDs;
public string equippedAnimalID;
public string lastEggClaimTime;
```

Update the default constructor to initialize the new fields:

```csharp
public GameData()
{
    coins = 0;
    gems = 0;
    unlockedAnimalIDs = new string[0];
    equippedAnimalID = "";
    lastEggClaimTime = "";
}
```

Update the parameterized constructor (if present) to accept gems:

```csharp
public GameData(int currentCoins, int currentGems, string[] animalIDs, string equippedID, string eggTime)
{
    coins = currentCoins;
    gems = currentGems;
    unlockedAnimalIDs = animalIDs ?? new string[0];
    equippedAnimalID = equippedID ?? "";
    lastEggClaimTime = eggTime ?? "";
}
```

- [ ] **Step 2: Update SaveManager.SaveGame()**

In `SaveManager.cs`, find the line that creates the GameData object (around line 46). Update it to pass all new fields. Since AnimalManager won't exist yet, use safe fallbacks:

```csharp
// In SaveGame(), replace the GameData construction:
string[] animalIDs = new string[0];
string equippedID = "";
string eggTime = "";

if (AnimalManager.Instance != null)
{
    animalIDs = AnimalManager.Instance.GetUnlockedAnimalIDs();
    equippedID = AnimalManager.Instance.GetEquippedAnimalID();
    eggTime = AnimalManager.Instance.GetLastEggClaimTimeISO();
}

GameData data = new GameData(
    CurrencyManager.Instance.Coins,
    CurrencyManager.Instance.Gems,
    animalIDs,
    equippedID,
    eggTime
);
```

- [ ] **Step 3: Update SaveManager.LoadGame()**

In `SaveManager.cs`, after the line that sets coins (around line 87), add:

```csharp
CurrencyManager.Instance.SetGems(data.gems);

if (AnimalManager.Instance != null)
{
    AnimalManager.Instance.LoadState(data.unlockedAnimalIDs, data.equippedAnimalID, data.lastEggClaimTime);
}
```

- [ ] **Step 4: Verify save/load cycle**

Enter Play mode. Add some gems via console or test script. Quit play mode (triggers auto-save). Re-enter play mode. Verify gems are restored. Check that existing coin save still works.

- [ ] **Step 5: Commit**

```bash
git add Assets/Scripts/GameData.cs Assets/Scripts/SaveManager.cs
git commit -m "feat: extend save system with gems, animal unlocks, and egg timer"
```

---

## Task 3: Add Gems Display to CurrencyUI

**Files:**
- Modify: `Assets/Scripts/CurrencyUI.cs`

- [ ] **Step 1: Add gem text reference**

Add a new serialized field alongside the existing text fields (around line 13):

```csharp
[SerializeField] private TextMeshProUGUI gemsText;
```

- [ ] **Step 2: Subscribe to gem events in Initialize()**

In the `Initialize()` method (around line 37), add after the coins subscription:

```csharp
CurrencyManager.Instance.OnGemsChanged += UpdateGemsDisplay;
```

Also update initial display call alongside existing ones:

```csharp
UpdateGemsDisplay(CurrencyManager.Instance.Gems);
```

- [ ] **Step 3: Unsubscribe in OnDestroy()**

In `OnDestroy()` (around line 55), add:

```csharp
if (CurrencyManager.Instance != null)
    CurrencyManager.Instance.OnGemsChanged -= UpdateGemsDisplay;
```

- [ ] **Step 4: Add UpdateGemsDisplay method**

Add after the existing `UpdateCoinsDisplay` method, following the same pattern:

```csharp
private void UpdateGemsDisplay(int newAmount)
{
    if (gemsText == null) return;

    string formatted = useThousandsSeparator
        ? string.Format("{0:N0}", newAmount)
        : newAmount.ToString();

    gemsText.text = formatted;
    AnimateText(gemsText);
}
```

- [ ] **Step 5: Wire up in Unity Editor**

In the scene, add a new TextMeshPro element to the currency bar area for gems display (purple gem icon + text). Assign it to the `gemsText` field on the CurrencyUI component.

- [ ] **Step 6: Verify display**

Enter Play mode. Confirm gem count shows "0". Use console to call `CurrencyManager.Instance.AddGems(25)`. Verify the display updates with animation.

- [ ] **Step 7: Commit**

```bash
git add Assets/Scripts/CurrencyUI.cs
git commit -m "feat: add gem count display to CurrencyUI"
```

---

## Task 4: Create AnimalData ScriptableObject

**Files:**
- Create: `Assets/Scripts/AnimalData.cs`

- [ ] **Step 1: Create the AnimalData class**

```csharp
using UnityEngine;

public enum AnimalAbilityType
{
    None,
    PassiveTimer,
    RunDefender
}

[CreateAssetMenu(fileName = "New Animal", menuName = "Farm Game/Animal Data", order = 7)]
public class AnimalData : ScriptableObject
{
    [Header("Identity")]
    public string animalID;
    public string displayName;
    [TextArea(2, 4)]
    public string description;
    public string animalEmoji;
    public int sortOrder;

    [Header("Cost")]
    public int gemCost;

    [Header("Ability")]
    public AnimalAbilityType abilityType;

    [Tooltip("For PassiveTimer: real-time cooldown in minutes")]
    public float cooldownMinutes = 20f;

    [Tooltip("For PassiveTimer: coins rewarded per claim")]
    public int rewardCoins = 30;

    [Header("Visuals")]
    public GameObject visualPrefab;
    public float roamSpeed = 0.6f;
    public Sprite iconSprite;
}
```

- [ ] **Step 2: Create AnimalData assets in Unity Editor**

Right-click in `Assets/Data/` → Create → Farm Game → Animal Data. Create 6 assets:

| Asset Name | animalID | displayName | gemCost | abilityType | cooldownMinutes | rewardCoins | sortOrder |
|---|---|---|---|---|---|---|---|
| Animal_Chicken | chicken | Chicken | 100 | PassiveTimer | 20 | 30 | 0 |
| Animal_FarmDog | farm_dog | Farm Dog | 500 | RunDefender | 0 | 0 | 1 |
| Animal_Rooster | rooster | Rooster | 1500 | None | 0 | 0 | 2 |
| Animal_Cow | cow | Cow | 3000 | None | 0 | 0 | 3 |
| Animal_Pig | pig | Pig | 5000 | None | 0 | 0 | 4 |
| Animal_Horse | horse | Horse | 8000 | None | 0 | 0 | 5 |

Set descriptions:
- Chicken: "Lays an egg every 20 minutes. Sell it for bonus coins!"
- Farm Dog: "Chases away deer during farm runs. Good boy!"
- Rooster: "Coming soon..."
- Cow: "Coming soon..."
- Pig: "Coming soon..."
- Horse: "Coming soon..."

Set emojis: 🐔, 🐕, 🐓, 🐄, 🐖, 🐴

- [ ] **Step 3: Commit**

```bash
git add Assets/Scripts/AnimalData.cs
git commit -m "feat: create AnimalData ScriptableObject"
```

Note: SO assets (.asset files) should also be committed after creation in the editor.

---

## Task 5: Create AnimalManager Singleton

**Files:**
- Create: `Assets/Scripts/AnimalManager.cs`

- [ ] **Step 1: Create the AnimalManager class with core state**

```csharp
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
    public event Action OnEggReady;
    public event Action OnEggClaimed;
    public event Action<string> OnAnimalUnlocked;

    private bool eggReady = false;
    private bool eggNotified = false;

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
        UpdateEggTimer();
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

    // ── Egg Timer (PassiveTimer) ──────────────────────────────

    public bool IsEggReady => eggReady;

    public float GetCooldownProgress()
    {
        AnimalData equipped = GetEquippedAnimal();
        if (equipped == null || equipped.abilityType != AnimalAbilityType.PassiveTimer)
            return 0f;

        double elapsedMinutes = (DateTime.UtcNow - lastEggClaimTime).TotalMinutes;
        return Mathf.Clamp01((float)(elapsedMinutes / equipped.cooldownMinutes));
    }

    public void ClaimEgg()
    {
        AnimalData equipped = GetEquippedAnimal();
        if (equipped == null || equipped.abilityType != AnimalAbilityType.PassiveTimer) return;
        if (!eggReady) return;

        CurrencyManager.Instance.AddCoins(equipped.rewardCoins);
        Debug.Log($"Claimed egg! +{equipped.rewardCoins} coins");

        lastEggClaimTime = DateTime.UtcNow;
        eggReady = false;
        eggNotified = false;

        // Tell visual to remove egg sprite
        AnimalVisual visual = activeVisualInstance?.GetComponent<AnimalVisual>();
        if (visual != null)
        {
            visual.RemoveEgg();
        }

        OnEggClaimed?.Invoke();
    }

    private void UpdateEggTimer()
    {
        AnimalData equipped = GetEquippedAnimal();
        if (equipped == null || equipped.abilityType != AnimalAbilityType.PassiveTimer) return;

        double elapsedMinutes = (DateTime.UtcNow - lastEggClaimTime).TotalMinutes;

        if (!eggReady && elapsedMinutes >= equipped.cooldownMinutes)
        {
            eggReady = true;

            if (!eggNotified)
            {
                eggNotified = true;

                // Tell visual to drop egg
                AnimalVisual visual = activeVisualInstance?.GetComponent<AnimalVisual>();
                if (visual != null)
                {
                    visual.DropEgg();
                }

                OnEggReady?.Invoke();
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
}
```

- [ ] **Step 2: Add AnimalManager to scene**

In Unity Editor, create an empty GameObject named `AnimalManager` in the scene hierarchy. Add the `AnimalManager` component. Drag all 6 AnimalData assets into the `allAnimals` list.

- [ ] **Step 3: Verify basic flow**

Enter Play mode. In Console, run:
```
AnimalManager.Instance.TryUnlockAnimal("chicken")  // should fail — no gems
CurrencyManager.Instance.AddGems(100)
AnimalManager.Instance.TryUnlockAnimal("chicken")  // should succeed
AnimalManager.Instance.EquipAnimal("chicken")       // should equip
```

- [ ] **Step 4: Commit**

```bash
git add Assets/Scripts/AnimalManager.cs
git commit -m "feat: create AnimalManager singleton with unlock, equip, and egg timer"
```

---

## Task 6: Create AnimalVisual Roaming Behavior

**Files:**
- Create: `Assets/Scripts/AnimalVisual.cs`

- [ ] **Step 1: Create AnimalVisual class**

Based on the HomeHelperWander pattern but with screen-bounds clamping and egg drop support:

```csharp
using UnityEngine;

public class AnimalVisual : MonoBehaviour
{
    private AnimalData data;
    private SpriteRenderer spriteRenderer;

    // Wander state
    private Vector3 wanderTarget;
    private float pauseTimer = 0f;
    private bool isPaused = true;

    // Wander config
    private const float WANDER_RADIUS = 3f;
    private const float MIN_PAUSE = 1.5f;
    private const float MAX_PAUSE = 4f;
    private const float SCREEN_PADDING = 0.5f;

    // Egg visual
    private GameObject eggInstance;

    public void Initialize(AnimalData animalData)
    {
        data = animalData;
        spriteRenderer = GetComponentInChildren<SpriteRenderer>();

        if (spriteRenderer != null)
        {
            spriteRenderer.sortingOrder = 10;
        }

        wanderTarget = transform.position;
        pauseTimer = Random.Range(MIN_PAUSE, MAX_PAUSE);
    }

    private void Update()
    {
        if (data == null) return;

        if (isPaused)
        {
            pauseTimer -= Time.deltaTime;
            if (pauseTimer <= 0f)
            {
                isPaused = false;
                PickNewTarget();
            }
        }
        else
        {
            Vector3 direction = wanderTarget - transform.position;
            float distance = direction.magnitude;

            if (distance < 0.1f)
            {
                // Arrived at target
                isPaused = true;
                pauseTimer = Random.Range(MIN_PAUSE, MAX_PAUSE);
            }
            else
            {
                // Move toward target
                float speed = data.roamSpeed > 0 ? data.roamSpeed : 0.6f;
                transform.position = Vector3.MoveTowards(transform.position, wanderTarget, speed * Time.deltaTime);

                // Flip sprite based on direction
                if (spriteRenderer != null && Mathf.Abs(direction.x) > 0.01f)
                {
                    spriteRenderer.flipX = direction.x < 0;
                }
            }
        }
    }

    private void PickNewTarget()
    {
        Vector2 randomOffset = Random.insideUnitCircle * WANDER_RADIUS;
        Vector3 candidate = transform.position + new Vector3(randomOffset.x, randomOffset.y, 0);

        // Clamp to visible screen bounds
        wanderTarget = ClampToScreenBounds(candidate);
    }

    private Vector3 ClampToScreenBounds(Vector3 position)
    {
        Camera cam = Camera.main;
        if (cam == null) return position;

        Vector3 minScreen = cam.ViewportToWorldPoint(new Vector3(SCREEN_PADDING / cam.pixelWidth * 100f, SCREEN_PADDING / cam.pixelHeight * 100f, cam.nearClipPlane));
        Vector3 maxScreen = cam.ViewportToWorldPoint(new Vector3(1f - SCREEN_PADDING / cam.pixelWidth * 100f, 1f - SCREEN_PADDING / cam.pixelHeight * 100f, cam.nearClipPlane));

        // Use viewport directly with padding as a fraction
        float pad = 0.08f; // 8% inset from edges
        Vector3 minWorld = cam.ViewportToWorldPoint(new Vector3(pad, pad, cam.nearClipPlane));
        Vector3 maxWorld = cam.ViewportToWorldPoint(new Vector3(1f - pad, 1f - pad, cam.nearClipPlane));

        position.x = Mathf.Clamp(position.x, minWorld.x, maxWorld.x);
        position.y = Mathf.Clamp(position.y, minWorld.y, maxWorld.y);
        position.z = 0;

        return position;
    }

    // ── Egg Visual ──────────────────────────────

    public void DropEgg()
    {
        if (eggInstance != null) return; // Already has an egg

        eggInstance = new GameObject("EggDrop");
        eggInstance.transform.position = transform.position + Vector3.down * 0.2f;

        SpriteRenderer eggRenderer = eggInstance.AddComponent<SpriteRenderer>();
        eggRenderer.sortingOrder = 9;

        // Create a simple egg texture (placeholder — replace with sprite asset later)
        Texture2D tex = new Texture2D(12, 16, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Point;
        Color eggWhite = new Color(1f, 0.98f, 0.9f);
        Color eggShadow = new Color(0.9f, 0.88f, 0.8f);

        Color[] pixels = new Color[12 * 16];
        for (int i = 0; i < pixels.Length; i++) pixels[i] = Color.clear;

        // Simple egg shape
        for (int y = 0; y < 16; y++)
        {
            for (int x = 0; x < 12; x++)
            {
                float cx = (x - 5.5f) / 5.5f;
                float cy = (y - 8f) / 8f;
                // Egg-ish ellipse (narrower at top)
                float topFactor = 1f - cy * 0.3f;
                float dist = (cx * cx) / (topFactor * topFactor) + cy * cy;
                if (dist < 0.85f)
                {
                    pixels[y * 12 + x] = y < 6 ? eggShadow : eggWhite;
                }
            }
        }

        tex.SetPixels(pixels);
        tex.Apply();

        eggRenderer.sprite = Sprite.Create(tex, new Rect(0, 0, 12, 16), new Vector2(0.5f, 0f), 32f);

        // Subtle drop animation
        eggInstance.transform.localScale = Vector3.zero;
        LeanTween.scale(eggInstance, Vector3.one * 0.8f, 0.3f).setEaseOutBack();
    }

    public void RemoveEgg()
    {
        if (eggInstance == null) return;

        GameObject egg = eggInstance;
        eggInstance = null;

        // Pop-out animation then destroy
        LeanTween.scale(egg, Vector3.zero, 0.2f).setEaseInBack().setOnComplete(() =>
        {
            Destroy(egg);
        });
    }
}
```

- [ ] **Step 2: Verify wandering behavior**

Temporarily give the chicken a visual prefab (can be a simple sprite GameObject). Equip it and verify:
- Animal wanders within screen bounds
- Sprite flips when changing direction
- Animal pauses between movements

- [ ] **Step 3: Commit**

```bash
git add Assets/Scripts/AnimalVisual.cs
git commit -m "feat: create AnimalVisual with screen-clamped wandering and egg drop"
```

---

## Task 7: Create AnimalPopup UI

**Files:**
- Create: `Assets/Scripts/AnimalPopup.cs`

- [ ] **Step 1: Create the AnimalPopup class**

```csharp
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class AnimalPopup : MonoBehaviour
{
    public static AnimalPopup Instance { get; private set; }

    [Header("Popup")]
    [SerializeField] private GameObject popupRoot;
    [SerializeField] private CanvasGroup popupCanvasGroup;
    [SerializeField] private RectTransform popupPanel;
    [SerializeField] private GameObject backdrop;

    [Header("Header")]
    [SerializeField] private TextMeshProUGUI titleText;
    [SerializeField] private TextMeshProUGUI gemCountText;

    [Header("Animal List")]
    [SerializeField] private Transform animalListContainer;
    [SerializeField] private GameObject animalRowPrefab;

    private List<GameObject> rowInstances = new List<GameObject>();

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        if (popupRoot != null)
            popupRoot.SetActive(false);
    }

    private void Start()
    {
        if (CurrencyManager.Instance != null)
            CurrencyManager.Instance.OnGemsChanged += UpdateGemCount;

        if (AnimalManager.Instance != null)
        {
            AnimalManager.Instance.OnAnimalEquipped += (_) => RefreshList();
            AnimalManager.Instance.OnAnimalUnequipped += RefreshList;
            AnimalManager.Instance.OnAnimalUnlocked += (_) => RefreshList();
        }
    }

    private void OnDestroy()
    {
        if (CurrencyManager.Instance != null)
            CurrencyManager.Instance.OnGemsChanged -= UpdateGemCount;
    }

    // ── Show / Hide ──────────────────────────────

    public void Show()
    {
        popupRoot.SetActive(true);

        UpdateGemCount(CurrencyManager.Instance.Gems);
        RefreshList();

        // Animate in (scale + fade)
        popupPanel.localScale = Vector3.one * 0.8f;
        popupCanvasGroup.alpha = 0f;

        LeanTween.scale(popupPanel, Vector3.one, 0.3f).setEaseOutQuad();
        LeanTween.alphaCanvas(popupCanvasGroup, 1f, 0.3f).setEaseOutQuad();
    }

    public void Hide()
    {
        LeanTween.scale(popupPanel, Vector3.one * 0.8f, 0.2f).setEaseInQuad();
        LeanTween.alphaCanvas(popupCanvasGroup, 0f, 0.2f).setEaseInQuad().setOnComplete(() =>
        {
            popupRoot.SetActive(false);
        });
    }

    public void OnBackdropClick()
    {
        Hide();
    }

    public void OnCloseClick()
    {
        Hide();
    }

    // ── List Population ──────────────────────────────

    private void RefreshList()
    {
        // Clear existing rows
        foreach (GameObject row in rowInstances)
        {
            Destroy(row);
        }
        rowInstances.Clear();

        List<AnimalData> animals = AnimalManager.Instance.GetAllAnimals();

        foreach (AnimalData animal in animals)
        {
            GameObject row = Instantiate(animalRowPrefab, animalListContainer);
            rowInstances.Add(row);
            SetupRow(row, animal);
        }
    }

    private void SetupRow(GameObject row, AnimalData animal)
    {
        // Find row child elements by name
        TextMeshProUGUI nameText = row.transform.Find("NameText")?.GetComponent<TextMeshProUGUI>();
        TextMeshProUGUI descText = row.transform.Find("DescText")?.GetComponent<TextMeshProUGUI>();
        TextMeshProUGUI emojiText = row.transform.Find("EmojiText")?.GetComponent<TextMeshProUGUI>();
        Button actionButton = row.transform.Find("ActionButton")?.GetComponent<Button>();
        TextMeshProUGUI actionText = actionButton?.GetComponentInChildren<TextMeshProUGUI>();
        Image rowBackground = row.GetComponent<Image>();

        bool isUnlocked = AnimalManager.Instance.IsUnlocked(animal.animalID);
        bool isEquipped = AnimalManager.Instance.GetEquippedAnimalID() == animal.animalID;

        // Set text
        if (nameText != null) nameText.text = animal.displayName;
        if (descText != null) descText.text = animal.description;
        if (emojiText != null) emojiText.text = animal.animalEmoji;

        // Set state
        if (isEquipped)
        {
            // Green highlight — equipped
            if (rowBackground != null) rowBackground.color = new Color(0.29f, 0.49f, 0.18f, 0.3f);
            if (actionText != null) actionText.text = "EQUIPPED";
            if (actionButton != null)
            {
                actionButton.onClick.RemoveAllListeners();
                actionButton.onClick.AddListener(() =>
                {
                    AnimalManager.Instance.UnequipAnimal();
                });
            }
        }
        else if (isUnlocked)
        {
            // Neutral — unlocked, tap to equip
            if (rowBackground != null) rowBackground.color = new Color(0.3f, 0.3f, 0.3f, 0.2f);
            if (actionText != null) actionText.text = "EQUIP";
            if (actionButton != null)
            {
                actionButton.onClick.RemoveAllListeners();
                string id = animal.animalID;
                actionButton.onClick.AddListener(() =>
                {
                    AnimalManager.Instance.EquipAnimal(id);
                });
            }
        }
        else
        {
            // Locked
            bool canAfford = CurrencyManager.Instance.CanAffordGems(animal.gemCost);

            if (rowBackground != null) rowBackground.color = new Color(0.2f, 0.2f, 0.2f, 0.15f);
            if (emojiText != null) emojiText.alpha = 0.4f;
            if (nameText != null) nameText.color = new Color(0.6f, 0.6f, 0.6f);
            if (descText != null) descText.color = new Color(0.5f, 0.5f, 0.5f);

            string costDisplay = $"💎 {animal.gemCost:N0}";
            if (actionText != null) actionText.text = costDisplay;

            if (actionButton != null)
            {
                actionButton.interactable = canAfford;
                actionButton.onClick.RemoveAllListeners();

                if (canAfford)
                {
                    string id = animal.animalID;
                    int cost = animal.gemCost;
                    string name = animal.displayName;
                    actionButton.onClick.AddListener(() =>
                    {
                        // TODO: Show confirmation dialog in the future
                        // For now, unlock directly
                        if (AnimalManager.Instance.TryUnlockAnimal(id))
                        {
                            Debug.Log($"Unlocked {name}!");
                        }
                    });
                }
            }
        }
    }

    private void UpdateGemCount(int gems)
    {
        if (gemCountText != null)
            gemCountText.text = $"💎 {gems:N0}";
    }
}
```

- [ ] **Step 2: Create animalRowPrefab in Unity Editor**

Create a UI prefab `Assets/Prefabs/AnimalRow.prefab` with this structure:
```
AnimalRow (Image — background, horizontal layout)
├── EmojiText (TextMeshProUGUI — large, left side)
├── TextGroup (VerticalLayoutGroup)
│   ├── NameText (TextMeshProUGUI — bold, 16pt)
│   └── DescText (TextMeshProUGUI — regular, 11pt, grey)
└── ActionButton (Button)
    └── ActionButtonText (TextMeshProUGUI)
```

Style the row: rounded corners (10px), 8px padding, dark background. The row should be full-width in a vertical layout group.

- [ ] **Step 3: Create popup GameObject in scene**

In the Canvas, create the popup structure:
```
AnimalPopup (AnimalPopup component)
├── Backdrop (Image — semi-transparent black, Button for click-to-close)
├── PopupPanel (RectTransform, CanvasGroup)
│   ├── Header
│   │   ├── TitleText ("Farm Animals")
│   │   ├── GemCountText
│   │   └── CloseButton (X)
│   ├── Subtitle ("Equip one animal to help on your farm")
│   └── ScrollView
│       └── Content (VerticalLayoutGroup — this is animalListContainer)
```

Wire all references in the inspector.

- [ ] **Step 4: Verify popup**

Enter Play mode. Call `AnimalPopup.Instance.Show()`. Verify:
- Popup animates in
- All 6 animals listed with correct names, emojis, descriptions
- All locked (no gems yet)
- Backdrop click closes popup

- [ ] **Step 5: Commit**

```bash
git add Assets/Scripts/AnimalPopup.cs
git commit -m "feat: create AnimalPopup with vertical list for browse/unlock/equip"
```

---

## Task 8: Create AnimalEquipButton and EggClaimButton

**Files:**
- Create: `Assets/Scripts/AnimalEquipButton.cs`
- Create: `Assets/Scripts/EggClaimButton.cs`

- [ ] **Step 1: Create AnimalEquipButton**

```csharp
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class AnimalEquipButton : MonoBehaviour
{
    [SerializeField] private Button button;
    [SerializeField] private Image animalIcon;
    [SerializeField] private TextMeshProUGUI emojiText;
    [SerializeField] private Sprite silhouetteSprite;

    private void Start()
    {
        button.onClick.AddListener(OnClick);

        if (AnimalManager.Instance != null)
        {
            AnimalManager.Instance.OnAnimalEquipped += (_) => UpdateDisplay();
            AnimalManager.Instance.OnAnimalUnequipped += UpdateDisplay;
        }

        UpdateDisplay();
    }

    private void OnClick()
    {
        if (AnimalPopup.Instance != null)
            AnimalPopup.Instance.Show();
    }

    private void UpdateDisplay()
    {
        AnimalData equipped = AnimalManager.Instance?.GetEquippedAnimal();

        if (equipped != null)
        {
            // Show equipped animal
            if (emojiText != null) emojiText.text = equipped.animalEmoji;
            if (animalIcon != null && equipped.iconSprite != null)
            {
                animalIcon.sprite = equipped.iconSprite;
                animalIcon.enabled = true;
                if (emojiText != null) emojiText.gameObject.SetActive(false);
            }
            else
            {
                if (animalIcon != null) animalIcon.enabled = false;
                if (emojiText != null) emojiText.gameObject.SetActive(true);
            }
        }
        else
        {
            // Show silhouette / empty state
            if (emojiText != null)
            {
                emojiText.gameObject.SetActive(true);
                emojiText.text = "❓";
            }
            if (animalIcon != null)
            {
                if (silhouetteSprite != null)
                {
                    animalIcon.sprite = silhouetteSprite;
                    animalIcon.enabled = true;
                }
                else
                {
                    animalIcon.enabled = false;
                }
            }
        }
    }
}
```

- [ ] **Step 2: Create EggClaimButton**

```csharp
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class EggClaimButton : MonoBehaviour
{
    [SerializeField] private Button button;
    [SerializeField] private Image buttonImage;
    [SerializeField] private GameObject notificationDot;
    [SerializeField] private TextMeshProUGUI emojiText;

    [Header("Colors")]
    [SerializeField] private Color readyColor = new Color(0.55f, 0.35f, 0.17f, 0.9f);
    [SerializeField] private Color cooldownColor = new Color(0.35f, 0.35f, 0.35f, 0.5f);

    private void Start()
    {
        button.onClick.AddListener(OnClick);

        if (AnimalManager.Instance != null)
        {
            AnimalManager.Instance.OnAnimalEquipped += (_) => UpdateVisibility();
            AnimalManager.Instance.OnAnimalUnequipped += UpdateVisibility;
            AnimalManager.Instance.OnEggReady += OnEggReady;
            AnimalManager.Instance.OnEggClaimed += UpdateState;
        }

        UpdateVisibility();
    }

    private void Update()
    {
        // Update state each frame for cooldown progress
        if (gameObject.activeSelf)
        {
            UpdateState();
        }
    }

    private void OnClick()
    {
        if (AnimalManager.Instance != null && AnimalManager.Instance.IsEggReady)
        {
            AnimalManager.Instance.ClaimEgg();
        }
    }

    private void UpdateVisibility()
    {
        AnimalData equipped = AnimalManager.Instance?.GetEquippedAnimal();
        bool showButton = equipped != null && equipped.abilityType == AnimalAbilityType.PassiveTimer;
        gameObject.SetActive(showButton);

        if (showButton)
        {
            UpdateState();
        }
    }

    private void UpdateState()
    {
        bool ready = AnimalManager.Instance != null && AnimalManager.Instance.IsEggReady;

        if (buttonImage != null)
            buttonImage.color = ready ? readyColor : cooldownColor;

        if (notificationDot != null)
            notificationDot.SetActive(ready);

        if (emojiText != null)
            emojiText.text = "🥚";
    }

    private void OnEggReady()
    {
        UpdateState();

        // Subtle bounce animation to draw attention
        LeanTween.cancel(gameObject);
        transform.localScale = Vector3.one;
        LeanTween.scale(gameObject, Vector3.one * 1.2f, 0.15f)
            .setEaseOutQuad()
            .setLoopPingPong(1);
    }
}
```

- [ ] **Step 3: Create UI GameObjects in scene**

Create in the Canvas, anchored to bottom-left above the BottomNav:

```
AnimalEquipButton (44x44, Button + Image + AnimalEquipButton component)
├── EmojiText (TextMeshProUGUI — centered, large)
└── AnimalIcon (Image — hidden by default)

EggClaimButton (34x34, positioned directly above AnimalEquipButton)
├── EmojiText (TextMeshProUGUI — "🥚")
└── NotificationDot (Image — small red circle, top-right corner)
```

Anchor both to bottom-left. EggClaimButton's bottom edge should be ~4px above AnimalEquipButton's top edge.

- [ ] **Step 4: Verify buttons**

Enter Play mode. Verify:
- Animal button shows "❓" silhouette (no animal equipped)
- Egg button is hidden (no PassiveTimer animal equipped)
- Tapping animal button opens popup
- After equipping chicken: animal button shows 🐔, egg button appears
- After unequipping: reverts to ❓, egg button hides

- [ ] **Step 5: Commit**

```bash
git add Assets/Scripts/AnimalEquipButton.cs Assets/Scripts/EggClaimButton.cs
git commit -m "feat: add animal equip button and egg claim button to home screen"
```

---

## Task 9: Migrate FarmDog to Animal System

**Files:**
- Modify: `Assets/Scripts/FarmDog.cs`

- [ ] **Step 1: Refactor FarmDog for external control**

The current FarmDog self-spawns based on UpgradeManager. We need to:
1. Remove the self-spawning logic (Awake/Start unlock check, OnRunStarted auto-spawn)
2. Add `ActivateChaseMode()` / `DeactivateChaseMode()` public methods
3. Keep all chase/repel logic intact

Key changes to `FarmDog.cs`:

Remove or disable the `OnRunStarted` handler that checks `IsUnlocked()` and calls `SpawnDog()`. Instead, the dog is spawned by AnimalManager when equipped, and FarmDog just handles chase behavior.

Add these public methods:

```csharp
public void ActivateChaseMode()
{
    isChasing = true;
    // Start the chase coroutine loop
    StartCoroutine(ChaseLoop());
}

public void DeactivateChaseMode()
{
    isChasing = false;
    StopAllCoroutines();
}
```

Remove the `IsUnlocked()` check that references `UpgradeManager.GetPermanentLevel(unlockID)`. The dog's unlock state is now managed by AnimalManager gem purchases.

Remove the self-contained spawn/despawn lifecycle. The FarmDog component now lives on the animal visual prefab and is activated/deactivated by AnimalManager.

Keep:
- `TryChaseNearestDeer()` logic
- `ChaseDeer()` coroutine
- Chase cooldown timer
- `ForceRepel()` calls on AnimalThreat
- Sprite flip during chase

- [ ] **Step 2: Create FarmDog visual prefab**

Create a prefab `Assets/Prefabs/Animals/FarmDog.prefab`:
```
FarmDog (GameObject)
├── SpriteRenderer (using existing dog sprite/DogVisual)
├── AnimalVisual (component — handles wandering)
└── FarmDog (component — handles chase mode)
```

Assign this prefab to the `Animal_FarmDog` AnimalData asset's `visualPrefab` field.

- [ ] **Step 3: Create Chicken visual prefab**

Create a prefab `Assets/Prefabs/Animals/Chicken.prefab`:
```
Chicken (GameObject)
├── SpriteRenderer (placeholder chicken sprite or pixel art)
└── AnimalVisual (component — handles wandering + egg drop)
```

Assign to `Animal_Chicken` AnimalData asset's `visualPrefab` field.

- [ ] **Step 4: Verify FarmDog migration**

Enter Play mode:
1. Give gems: `CurrencyManager.Instance.AddGems(500)`
2. Unlock dog: `AnimalManager.Instance.TryUnlockAnimal("farm_dog")`
3. Equip dog: `AnimalManager.Instance.EquipAnimal("farm_dog")`
4. Verify dog wanders on home screen
5. Start a run — verify dog enters chase mode and chases deer
6. End run — verify dog returns to wandering

- [ ] **Step 5: Commit**

```bash
git add Assets/Scripts/FarmDog.cs
git commit -m "refactor: migrate FarmDog from equipment system to animal system"
```

---

## Task 10: Add Gems to Daily Rewards

**Files:**
- Modify: `Assets/Scripts/DailyRewardManager.cs`
- Modify: `Assets/Scripts/DailyRewardPopup.cs`

- [ ] **Step 1: Add gem rewards array to DailyRewardManager**

In `DailyRewardManager.cs`, add alongside the existing `dailyRewards` array (around line 15):

```csharp
[SerializeField] private int[] dailyGemRewards = new int[] { 0, 1, 0, 2, 0, 1, 0 };
[SerializeField] private int weeklyGemBonus = 10;
```

This gives 1-2 gems on some days (Tue, Thu, Sat) and 10 gems for completing the full week.

- [ ] **Step 2: Grant gems in ClaimToday()**

In the `ClaimToday()` method (around line 131), after the coins are added, add:

```csharp
int gemReward = dailyGemRewards[todayIndex];
if (gemReward > 0)
{
    CurrencyManager.Instance.AddGems(gemReward);
}
```

Also in the weekly bonus section (around line 140), add:

```csharp
if (weeklyGemBonus > 0)
{
    CurrencyManager.Instance.AddGems(weeklyGemBonus);
}
```

- [ ] **Step 3: Update DailyRewardPopup to show gem rewards**

In `DailyRewardPopup.cs`, in the `CreateDayCell()` method where reward text is displayed, update to show gems alongside coins:

Find where the coin reward text is set (something like `rewardText.text = $"{reward}"`) and extend it:

```csharp
int gemReward = DailyRewardManager.Instance.GetDailyGemReward(dayIndex);
if (gemReward > 0)
{
    rewardText.text = $"{reward} 🪙\n{gemReward} 💎";
}
else
{
    rewardText.text = $"{reward} 🪙";
}
```

Add a public accessor to `DailyRewardManager`:

```csharp
public int GetDailyGemReward(int dayIndex)
{
    if (dayIndex < 0 || dayIndex >= dailyGemRewards.Length) return 0;
    return dailyGemRewards[dayIndex];
}
```

- [ ] **Step 4: Verify daily gem rewards**

Enter Play mode. Open daily reward popup. Verify:
- Some days show gem amounts alongside coins
- Claiming a day with gems grants them (check console log)
- Weekly bonus grants gem bonus

- [ ] **Step 5: Commit**

```bash
git add Assets/Scripts/DailyRewardManager.cs Assets/Scripts/DailyRewardPopup.cs
git commit -m "feat: add gem rewards to daily reward system"
```

---

## Task 11: Integration Testing and Polish

**Files:**
- All previously created/modified files

- [ ] **Step 1: Full flow test — new player experience**

Enter Play mode with a fresh save (delete save file). Verify:
1. Gem count shows 0
2. Animal equip button shows ❓ silhouette
3. Egg button is hidden
4. Tapping animal button opens popup — all 6 animals visible, all locked
5. Locked animals show gem prices and descriptions

- [ ] **Step 2: Full flow test — unlock and equip chicken**

1. Grant gems: `CurrencyManager.Instance.AddGems(100)`
2. Open animal popup — Chicken should show affordable (active buy button)
3. Tap to unlock — gems deducted, Chicken now shows "EQUIP"
4. Tap EQUIP — Chicken shows "EQUIPPED", popup reflects state
5. Close popup — chicken roams on home screen, egg button appears (greyed, cooldown)
6. Wait 20 min OR temporarily set cooldown to 0.1 min in AnimalData
7. Egg button turns full color + notification dot
8. Chicken drops egg sprite on ground
9. Tap egg button — coins granted, egg disappears, cooldown restarts

- [ ] **Step 3: Full flow test — save/load persistence**

1. With chicken equipped, exit play mode (auto-save)
2. Re-enter play mode
3. Verify: gems restored, chicken still equipped, egg timer resumed correctly
4. If egg was ready before exit, it should still be ready

- [ ] **Step 4: Full flow test — run with equipped animal**

1. Equip chicken, start a run
2. Chicken should roam on the farm during the run
3. Egg button stays visible during run
4. If egg becomes ready during run, can claim it
5. End run — chicken still roaming on home screen

- [ ] **Step 5: Full flow test — Farm Dog**

1. Grant 500 gems, unlock Farm Dog
2. Equip Farm Dog (chicken unequips)
3. Dog wanders on home screen
4. Egg button disappears (not a PassiveTimer animal)
5. Start run — Dog enters chase mode, chases deer
6. End run — Dog returns to wandering

- [ ] **Step 6: Full flow test — equip/unequip**

1. Open popup with Dog equipped
2. Tap Dog row — unequips (reverts to ❓)
3. Tap Chicken row — equips chicken
4. Tap Chicken row again — unequips
5. Verify only one animal is ever equipped

- [ ] **Step 7: Commit final integration**

```bash
git add -A
git commit -m "feat: complete Farm Animals system with gems, chicken, farm dog, and UI"
```
