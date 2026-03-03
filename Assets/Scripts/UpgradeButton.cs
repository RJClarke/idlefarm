using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// UI button for upgrades with dual-progression display
/// Shows different info based on Town Mode (Coins) vs Farm Mode (Money)
/// </summary>
public class UpgradeButton : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private UpgradeData upgradeData;
    [SerializeField] private Button button;

    [Header("UI Elements")]
    [SerializeField] private TextMeshProUGUI titleText;
    [SerializeField] private TextMeshProUGUI levelInfoText;
    [SerializeField] private TextMeshProUGUI buttonText;
    [SerializeField] private Image iconImage; // Optional

    [Header("Colors")]
    [SerializeField] private Color affordableColor = Color.white;
    [SerializeField] private Color unaffordableColor = new Color(0.5f, 0.5f, 0.5f);
    [SerializeField] private Color maxLevelColor = new Color(1f, 0.9f, 0.4f);

    [Header("Auto-Update")]
    [SerializeField] private float updateInterval = 0.5f; // Update UI every 0.5 seconds
    private float updateTimer = 0f;

    private CanvasGroup canvasGroup;

    private void Start()
    {
        if (button == null)
            button = GetComponent<Button>();

        // Get or add CanvasGroup for hiding
        canvasGroup = GetComponent<CanvasGroup>();
        if (canvasGroup == null)
            canvasGroup = gameObject.AddComponent<CanvasGroup>();

        button.onClick.AddListener(OnButtonClicked);
        
        UpdateDisplay();
    }

    private void Update()
    {
        // Periodically update display (currency changes, etc.)
        updateTimer += Time.deltaTime;
        if (updateTimer >= updateInterval)
        {
            updateTimer = 0f;
            UpdateDisplay();
        }
    }

    private void OnDestroy()
    {
        if (button != null)
            button.onClick.RemoveListener(OnButtonClicked);
    }

    /// <summary>
    /// Set which upgrade this button represents
    /// </summary>
    public void SetUpgradeData(UpgradeData data)
    {
        upgradeData = data;
        UpdateDisplay();
    }

    /// <summary>
    /// Update button display based on current mode and upgrade levels
    /// </summary>
    private void UpdateDisplay()
    {
        if (upgradeData == null || UpgradeManager.Instance == null)
            return;

        bool isInRun = RunManager.Instance != null && RunManager.Instance.IsRunActive;
        
        // If this is a permanent-only upgrade and we're in a run, hide it
        if (!upgradeData.allowTemporaryUpgrades && isInRun)
        {
            HideButton();
            return;
        }
        
        // Make sure it's visible if we're in the right mode
        ShowButton();

        int permanentLevel = UpgradeManager.Instance.GetPermanentLevel(upgradeData.upgradeID);
        int currentLevel = UpgradeManager.Instance.GetCurrentLevel(upgradeData.upgradeID);

        // Title (always show upgrade name and icon)
        if (titleText != null)
        {
            titleText.text = $"{upgradeData.icon} {upgradeData.displayName}";
        }

        // Check if at max level
        bool atMaxPermanent = permanentLevel >= upgradeData.maxLevel;
        bool atMaxCurrent = currentLevel >= upgradeData.maxLevel;

        if (isInRun && upgradeData.allowTemporaryUpgrades)
        {
            // FARM MODE (during run) - show dual-progression
            UpdateFarmModeDisplay(permanentLevel, currentLevel, atMaxCurrent);
        }
        else
        {
            // TOWN MODE (between runs) - show permanent only
            UpdateTownModeDisplay(permanentLevel, atMaxPermanent);
        }
    }

    /// <summary>
    /// Display for Town Mode (between runs, Coin purchases)
    /// </summary>
    private void UpdateTownModeDisplay(int permanentLevel, bool atMaxLevel)
    {
        // Level info: "Level 2 (+2%)"
        if (levelInfoText != null)
        {
            string bonusText = upgradeData.GetBonusText(permanentLevel);
            levelInfoText.text = $"Level {permanentLevel} ({bonusText})";
        }

        // Button text: "UPGRADE Lv 3: 500 🪙" or "MAX LEVEL"
        if (buttonText != null)
        {
            if (atMaxLevel)
            {
                buttonText.text = "MAX LEVEL";
                buttonText.color = maxLevelColor;
                button.interactable = false;
            }
            else
            {
                int nextLevel = permanentLevel + 1;
                int coinCost = upgradeData.GetCoinCost(nextLevel);
                buttonText.text = $"UPGRADE Lv {nextLevel}: {coinCost} 🪙";

                // Check affordability
                bool canAfford = CurrencyManager.Instance != null && 
                                CurrencyManager.Instance.CanAffordCoins(coinCost);
                
                buttonText.color = canAfford ? affordableColor : unaffordableColor;
                button.interactable = canAfford;
            }
        }
    }

    /// <summary>
    /// Display for Farm Mode (during run, Money purchases)
    /// </summary>
    private void UpdateFarmModeDisplay(int permanentLevel, int currentLevel, bool atMaxLevel)
    {
        // Level info: "Base: Lv 2 (+2%) | Now: Lv 4 (+4%) ⚡"
        if (levelInfoText != null)
        {
            string baseBonus = upgradeData.GetBonusText(permanentLevel);
            string currentBonus = upgradeData.GetBonusText(currentLevel);
            
            if (currentLevel > permanentLevel)
            {
                // Has temporary bonus
                levelInfoText.text = $"Base: Lv {permanentLevel} ({baseBonus})\nNow: Lv {currentLevel} ({currentBonus}) ⚡";
            }
            else
            {
                // No temporary bonus yet
                levelInfoText.text = $"Base: Lv {permanentLevel} ({baseBonus})";
            }
        }

        // Button text: "UPGRADE Lv 5: 48k 💵" or "MAX LEVEL"
        if (buttonText != null)
        {
            if (atMaxLevel)
            {
                buttonText.text = "MAX LEVEL";
                buttonText.color = maxLevelColor;
                button.interactable = false;
            }
            else
            {
                int nextLevel = currentLevel + 1;
                int moneyCost = UpgradeManager.Instance.CalculateMoneyCost(nextLevel);
                string costText = UpgradeManager.Instance.FormatMoneyCost(moneyCost);
                buttonText.text = $"UPGRADE Lv {nextLevel}: {costText} 💵";

                // Check affordability
                bool canAfford = CurrencyManager.Instance != null && 
                                CurrencyManager.Instance.CanAffordMoney(moneyCost);
                
                buttonText.color = canAfford ? affordableColor : unaffordableColor;
                button.interactable = canAfford;
            }
        }
    }

    /// <summary>
    /// Button clicked - attempt purchase
    /// </summary>
    private void OnButtonClicked()
    {
        if (upgradeData == null || UpgradeManager.Instance == null)
            return;

        bool isInRun = RunManager.Instance != null && RunManager.Instance.IsRunActive;
        
        if (isInRun)
        {
            // Farm Mode - purchase with Money
            PurchaseTemporary();
        }
        else
        {
            // Town Mode - purchase with Coins
            PurchasePermanent();
        }
    }

    /// <summary>
    /// Purchase permanent upgrade (Coins)
    /// </summary>
    private void PurchasePermanent()
    {
        int permanentLevel = UpgradeManager.Instance.GetPermanentLevel(upgradeData.upgradeID);
        
        if (permanentLevel >= upgradeData.maxLevel)
        {
            Debug.Log("Already at max level!");
            return;
        }

        int nextLevel = permanentLevel + 1;
        int coinCost = upgradeData.GetCoinCost(nextLevel);

        if (UpgradeManager.Instance.PurchasePermanentUpgrade(upgradeData.upgradeID, coinCost))
        {
            Debug.Log($"✅ Purchased {upgradeData.displayName} → Level {nextLevel}");
            UpdateDisplay();
            
            // TODO: Apply upgrade effect (increase actual helper speed, grid size, etc.)
        }
        else
        {
            Debug.Log($"❌ Cannot afford {upgradeData.displayName} upgrade (Need {coinCost} Coins)");
        }
    }

    /// <summary>
    /// Purchase temporary upgrade (Money)
    /// </summary>
    private void PurchaseTemporary()
    {
        int currentLevel = UpgradeManager.Instance.GetCurrentLevel(upgradeData.upgradeID);
        
        if (currentLevel >= upgradeData.maxLevel)
        {
            Debug.Log("Already at max level!");
            return;
        }

        int nextLevel = currentLevel + 1;
        int moneyCost = UpgradeManager.Instance.CalculateMoneyCost(nextLevel);

        if (UpgradeManager.Instance.PurchaseTemporaryUpgrade(upgradeData.upgradeID, moneyCost))
        {
            Debug.Log($"⚡ Purchased {upgradeData.displayName} → Level {nextLevel} (This Run)");
            UpdateDisplay();
            
            // TODO: Apply upgrade effect (increase actual helper speed, grid size, etc.)
        }
        else
        {
            Debug.Log($"❌ Cannot afford {upgradeData.displayName} upgrade (Need ${moneyCost} Money)");
        }
    }

    /// <summary>
    /// Hide this button (but keep Update running)
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

    /// <summary>
    /// Show this button
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

#if UNITY_EDITOR
    [ContextMenu("Force Update Display")]
    private void ForceUpdate()
    {
        UpdateDisplay();
    }
#endif
}