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
        return Mathf.Clamp01((float)(elapsedMinutes / equipped.cooldownMinutes));
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
            CurrencyManager.Instance.AddGems(equipped.rewardGems);
            Debug.Log($"Claimed gems! +{equipped.rewardGems} gems");
            if (visual != null) visual.RemoveGem();
            FloatingTextManager.ShowGems(equipped.rewardGems, rewardWorldPos);
        }
        else
        {
            CurrencyManager.Instance.AddCoins(equipped.rewardCoins);
            Debug.Log($"Claimed egg! +{equipped.rewardCoins} coins");
            if (visual != null) visual.RemoveEgg();
            FloatingTextManager.ShowCoins(equipped.rewardCoins, rewardWorldPos);
        }

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

        if (!eggReady && elapsedMinutes >= equipped.cooldownMinutes)
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
