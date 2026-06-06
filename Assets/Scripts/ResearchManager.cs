using System;
using UnityEngine;

/// <summary>
/// Tracks the 4 research slots' unlock state + (later) which research is active in each.
/// Slot unlocks: 1=100 coins, 2=100 gems, 3=requires research (id "slot_3_unlock"), 4=requires research (id "slot_4_unlock").
/// Slot 1 is free-purchase, all 4 default to locked. Persisted via PlayerPrefs.
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
        public int costAmount;          // for Coins/Gems
        public string requiredResearchID; // for Research-gated slots
    }

    [Header("Slot Unlock Costs")]
    [SerializeField] private SlotDefinition[] slotDefs = new SlotDefinition[SlotCount]
    {
        new SlotDefinition { unlockType = SlotUnlockType.Coins,    costAmount = 100 },
        new SlotDefinition { unlockType = SlotUnlockType.Gems,     costAmount = 100 },
        new SlotDefinition { unlockType = SlotUnlockType.Research, requiredResearchID = "slot_3_unlock" },
        new SlotDefinition { unlockType = SlotUnlockType.Research, requiredResearchID = "slot_4_unlock" },
    };

    private bool[] slotUnlocked = new bool[SlotCount];

    public event Action<int> OnSlotUnlocked;   // slotIndex

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        Load();
    }

    public SlotDefinition GetSlotDef(int index) => (index >= 0 && index < SlotCount) ? slotDefs[index] : null;

    public bool IsSlotUnlocked(int index) => index >= 0 && index < SlotCount && slotUnlocked[index];

    /// <summary>
    /// True if the requirements for unlocking this slot are currently met (player can act on it).
    /// Coin/Gem slots: can afford the cost. Research slots: required research is complete.
    /// </summary>
    public bool CanUnlockSlot(int index)
    {
        if (!IsValidSlot(index) || slotUnlocked[index]) return false;
        SlotDefinition def = slotDefs[index];
        switch (def.unlockType)
        {
            case SlotUnlockType.Coins:
                return CurrencyManager.Instance != null && CurrencyManager.Instance.CanAffordCoins(def.costAmount);
            case SlotUnlockType.Gems:
                return CurrencyManager.Instance != null && CurrencyManager.Instance.CanAffordGems(def.costAmount);
            case SlotUnlockType.Research:
                // TODO: query ResearchData level once research-data ScriptableObjects exist.
                return false;
        }
        return false;
    }

    public bool TryUnlockSlot(int index)
    {
        if (!CanUnlockSlot(index)) return false;
        SlotDefinition def = slotDefs[index];
        switch (def.unlockType)
        {
            case SlotUnlockType.Coins:
                if (!CurrencyManager.Instance.SpendCoins(def.costAmount)) return false;
                break;
            case SlotUnlockType.Gems:
                if (!CurrencyManager.Instance.SpendGems(def.costAmount)) return false;
                break;
            case SlotUnlockType.Research:
                return false; // no payment path yet
        }
        slotUnlocked[index] = true;
        Save();
        OnSlotUnlocked?.Invoke(index);
        return true;
    }

    private static bool IsValidSlot(int index) => index >= 0 && index < SlotCount;

    // ─── Persistence (PlayerPrefs for now — research timestamps will need JSON later) ───

    private const string PrefKey = "research_slot_unlocked_";

    private void Load()
    {
        for (int i = 0; i < SlotCount; i++)
            slotUnlocked[i] = PlayerPrefs.GetInt(PrefKey + i, 0) == 1;
    }

    private void Save()
    {
        for (int i = 0; i < SlotCount; i++)
            PlayerPrefs.SetInt(PrefKey + i, slotUnlocked[i] ? 1 : 0);
        PlayerPrefs.Save();
    }

#if UNITY_EDITOR
    [ContextMenu("Reset All Slots")]
    private void EditorResetAllSlots()
    {
        for (int i = 0; i < SlotCount; i++) slotUnlocked[i] = false;
        Save();
        Debug.Log("[ResearchManager] All slots reset to locked.");
    }
#endif
}
