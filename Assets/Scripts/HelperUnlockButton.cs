using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Special button for unlocking helper slots (Town Mode) or renting helpers (Farm Mode)
/// Full-width button that switches behavior based on game mode
/// </summary>
public class HelperUnlockButton : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private Button button;
    [SerializeField] private TextMeshProUGUI iconText;
    [SerializeField] private TextMeshProUGUI titleText;
    [SerializeField] private TextMeshProUGUI descText;
    [SerializeField] private TextMeshProUGUI costText;

    [Header("Upgrade Settings")]
    [SerializeField] private string upgradeID = "helper_slot_unlock";
    [SerializeField] private int maxSlots = 5; // Maximum helper slots (levels 0-4)
    [SerializeField] private int rentCost = 50000; // Cost to rent temporary helper (Money)
    
    [Header("Visuals")]
    [SerializeField] private string unlockIcon = "🤖";
    [SerializeField] private string rentIcon = "🤖";
    [SerializeField] private string coinIcon = "🪙";
    [SerializeField] private string moneyIcon = "💵";
    
    private CanvasGroup canvasGroup;
    private float updateTimer = 0f;
    private const float UPDATE_INTERVAL = 0.5f;

    private void Awake()
    {
        if (button == null)
            button = GetComponent<Button>();

        // Add CanvasGroup for visibility control
        canvasGroup = GetComponent<CanvasGroup>();
        if (canvasGroup == null)
            canvasGroup = gameObject.AddComponent<CanvasGroup>();
    }

    private void Start()
    {
        if (button != null)
            button.onClick.AddListener(OnButtonClicked);
        
        UpdateDisplay();
    }

    private void Update()
    {
        // Update periodically
        updateTimer += Time.deltaTime;
        if (updateTimer >= UPDATE_INTERVAL)
        {
            updateTimer = 0f;
            UpdateDisplay();
        }
    }

    /// <summary>
    /// Update button display based on current mode and upgrade level
    /// </summary>
    private void UpdateDisplay()
    {
        if (UpgradeManager.Instance == null || RunManager.Instance == null)
        {
            HideButton();
            return;
        }

        // Check if we're in Town Mode or Farm Mode
        bool isTownMode = !RunManager.Instance.IsRunActive;

        if (isTownMode)
        {
            DisplayUnlockMode();
        }
        else
        {
            DisplayRentMode();
        }
    }

    /// <summary>
    /// Display as "Unlock Helper Slot" (Town Mode, Coins)
    /// </summary>
    private void DisplayUnlockMode()
    {
        ShowButton();

        int currentLevel = UpgradeManager.Instance.GetPermanentLevel(upgradeID);
        int currentSlots = currentLevel + 1; // Level 0 = 1 slot, Level 4 = 5 slots

        if (iconText != null)
            iconText.text = unlockIcon;

        // Check if max slots reached
        if (currentLevel >= maxSlots - 1)
        {
            // Max helpers unlocked
            if (titleText != null)
                titleText.text = "Max Helpers Unlocked";
            
            if (descText != null)
                descText.text = $"{currentSlots} helper slots active";
            
            if (costText != null)
                costText.text = "MAX";
            
            if (button != null)
                button.interactable = false;
        }
        else
        {
            // Can unlock more slots
            int nextLevel = currentLevel + 1;
            int nextSlots = nextLevel + 1;
            int cost = CalculateSlotCost(nextLevel);
            
            if (titleText != null)
                titleText.text = $"Unlock Helper Slot {nextSlots}";
            
            if (descText != null)
                descText.text = $"Add helper #{nextSlots} permanently";
            
            if (costText != null)
                costText.text = $"{cost} {CurrencyIcons.Coin}";
            
            // Check if can afford
            bool canAfford = CurrencyManager.Instance != null && 
                            CurrencyManager.Instance.CanAffordCoins(cost);
            
            if (button != null)
                button.interactable = canAfford;
        }
    }

    /// <summary>
    /// Display as "Rent a Helper" (Farm Mode, Money)
    /// </summary>
    private void DisplayRentMode()
    {
        ShowButton();

        if (iconText != null)
            iconText.text = rentIcon;

        if (titleText != null)
            titleText.text = "Rent a Helper";
        
        if (descText != null)
            descText.text = "Hire temporary helper for this run";
        
        if (costText != null)
            costText.text = $"{rentCost / 1000}k {CurrencyIcons.Cash}";
        
        // Check if can afford
        bool canAfford = CurrencyManager.Instance != null && 
                        CurrencyManager.Instance.CanAffordMoney(rentCost);
        
        if (button != null)
            button.interactable = canAfford;
    }

    /// <summary>
    /// Handle button click
    /// </summary>
    private void OnButtonClicked()
    {
        if (UpgradeManager.Instance == null || RunManager.Instance == null)
            return;

        bool isTownMode = !RunManager.Instance.IsRunActive;

        if (isTownMode)
        {
            PurchaseSlotUnlock();
        }
        else
        {
            RentHelper();
        }
    }

    /// <summary>
    /// Purchase permanent helper slot (Town Mode)
    /// </summary>
    private void PurchaseSlotUnlock()
    {
        int currentLevel = UpgradeManager.Instance.GetPermanentLevel(upgradeID);
        int cost = CalculateSlotCost(currentLevel + 1);
        
        if (UpgradeManager.Instance.PurchasePermanentUpgrade(upgradeID, cost))
        {
            int newSlots = currentLevel + 2;
            Debug.Log($"✅ Unlocked helper slot {newSlots}!");
            UpdateDisplay();
        }
    }

    /// <summary>
    /// Rent temporary helper (Farm Mode)
    /// </summary>
    private void RentHelper()
    {
        if (CurrencyManager.Instance == null || !CurrencyManager.Instance.SpendMoney(rentCost))
        {
            return;
        }

        if (HelperManager.Instance != null)
        {
            Helper newHelper = HelperManager.Instance.SpawnUniversalHelper();
            if (newHelper != null)
            {
                Debug.Log("✅ Rented temporary helper!");
            }
        }
    }

    /// <summary>
    /// Calculate cost for unlocking a specific slot level
    /// 5x multiplier: 100, 500, 2500, 12500
    /// </summary>
    private int CalculateSlotCost(int level)
    {
        if (level <= 0) return 0;

        int baseCost = 100;
        float cost = baseCost;
        
        for (int i = 1; i < level; i++)
        {
            cost *= 5f;
        }
        
        return Mathf.RoundToInt(cost);
    }

    /// <summary>
    /// Show button (CanvasGroup)
    /// </summary>
    private void ShowButton()
    {
        if (canvasGroup != null)
        {
            canvasGroup.alpha = 1f;
            canvasGroup.interactable = true;
            canvasGroup.blocksRaycasts = true;
        }
    }

    /// <summary>
    /// Hide button (CanvasGroup)
    /// </summary>
    private void HideButton()
    {
        if (canvasGroup != null)
        {
            canvasGroup.alpha = 0f;
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;
        }
    }

    private void OnDestroy()
    {
        if (button != null)
            button.onClick.RemoveListener(OnButtonClicked);
    }

#if UNITY_EDITOR
    [ContextMenu("Test Unlock Mode")]
    private void TestUnlockMode()
    {
        DisplayUnlockMode();
    }

    [ContextMenu("Test Rent Mode")]
    private void TestRentMode()
    {
        DisplayRentMode();
    }
#endif
}