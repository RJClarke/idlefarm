using UnityEngine;

/// <summary>
/// Farm menu panel - shows grid size, zone unlocks, and tilling options
/// Inherits from MenuPanel for common functionality
/// </summary>
public class FarmMenuPanel : MenuPanel
{
    [Header("Farm Menu Specific")]
    [SerializeField] private GameObject upgradeButtonPrefab;
    [SerializeField] private Transform upgradeContainer;

    [Header("Upgrade Data")]
    [SerializeField] private UpgradeData gridSizeUpgrade;
    [SerializeField] private UpgradeData[] zoneUnlockUpgrades;

    protected override void OnEnable()
    {
        base.OnEnable();
        
        // Refresh upgrade displays when panel opens
        RefreshUpgrades();
    }

    private void Start()
    {
        CreateUpgradeButtons();
    }

    /// <summary>
    /// Create upgrade buttons from data
    /// </summary>
    private void CreateUpgradeButtons()
    {
        if (upgradeButtonPrefab == null || upgradeContainer == null)
        {
            Debug.LogError("Farm Menu: Missing prefab or container reference!");
            return;
        }

        // Clear existing buttons
        foreach (Transform child in upgradeContainer)
        {
            Destroy(child.gameObject);
        }

        // Create grid size button
        if (gridSizeUpgrade != null)
        {
            CreateUpgradeButton(gridSizeUpgrade);
        }

        // Create zone unlock buttons
        if (zoneUnlockUpgrades != null)
        {
            foreach (UpgradeData zoneUpgrade in zoneUnlockUpgrades)
            {
                if (zoneUpgrade != null)
                {
                    CreateUpgradeButton(zoneUpgrade);
                }
            }
        }
    }

    /// <summary>
    /// Instantiate a single upgrade button
    /// </summary>
    private void CreateUpgradeButton(UpgradeData data)
    {
        GameObject buttonObj = Instantiate(upgradeButtonPrefab, upgradeContainer);
        UpgradeButton button = buttonObj.GetComponent<UpgradeButton>();
        
        if (button != null)
        {
            button.SetUpgradeData(data);
        }
        else
        {
            Debug.LogError("Upgrade button prefab doesn't have UpgradeButton component!");
        }
    }

    /// <summary>
    /// Refresh all upgrade displays (called when panel opens or currency changes)
    /// </summary>
    private void RefreshUpgrades()
    {
        // Buttons auto-update themselves, but we can trigger manual refresh if needed
        UpgradeButton[] buttons = upgradeContainer.GetComponentsInChildren<UpgradeButton>();
        foreach (UpgradeButton button in buttons)
        {
            // Buttons have their own update logic
        }
    }
}