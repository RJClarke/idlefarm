using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// REUSABLE unlock button for any one-time purchase (crops, equipment, etc.)
/// Works with UnlockData ScriptableObject
/// ONE BUTTON SCRIPT FOR ALL UNLOCKS!
/// Just assign different UnlockData assets in Inspector
/// </summary>
public class UnlockButton : MonoBehaviour
{
    [Header("Unlock Data")]
    [Tooltip("Drag the UnlockData asset here (Scarecrow, Corn, Fence, etc.)")]
    [SerializeField] private UnlockData unlockData;
    
    [Header("UI References")]
    [SerializeField] private Button button;
    [SerializeField] private Image iconImage; // Use this for sprites (preferred)
    [SerializeField] private TextMeshProUGUI iconText; // Fallback for emoji (hidden if sprite available)
    [SerializeField] private TextMeshProUGUI titleText;
    [SerializeField] private TextMeshProUGUI descriptionText;
    [SerializeField] private TextMeshProUGUI statusText;
    [SerializeField] private TextMeshProUGUI costText;
    
    [Header("Visual Settings")]
    [SerializeField] private string coinIcon = "🪙";
    [SerializeField] private Color lockedColor = new Color(0.7f, 0.7f, 0.7f);
    [SerializeField] private Color unlockedColor = new Color(0.4f, 0.8f, 0.4f);
    [SerializeField] private Color affordableColor = Color.white;
    [SerializeField] private Color unaffordableColor = new Color(0.6f, 0.6f, 0.6f);
    [SerializeField] private Color prerequisiteColor = new Color(0.8f, 0.4f, 0.4f);
    
    private CanvasGroup canvasGroup;
    private bool isUnlocked = false;
    private float updateTimer = 0f;
    private const float UPDATE_INTERVAL = 0.5f;

    private void Awake()
    {
        if (button == null)
            button = GetComponent<Button>();
        
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
        updateTimer += Time.deltaTime;
        if (updateTimer >= UPDATE_INTERVAL)
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
    /// Set unlock data (alternative to Inspector assignment)
    /// </summary>
    public void SetUnlockData(UnlockData data)
    {
        unlockData = data;
        UpdateDisplay();
    }

    /// <summary>
    /// Update button display based on unlock status and game mode
    /// </summary>
    private void UpdateDisplay()
    {
        if (unlockData == null)
        {
            Debug.LogWarning("UnlockButton has no UnlockData assigned!");
            HideButton();
            return;
        }

        if (UpgradeManager.Instance == null || RunManager.Instance == null)
        {
            HideButton();
            return;
        }

        // Market unlocks only available in Town Mode
        bool isTownMode = !RunManager.Instance.IsRunActive;
        if (!isTownMode)
        {
            HideButton();
            return;
        }

        ShowButton();

        // Check if already unlocked (level > 0 = unlocked)
        isUnlocked = UpgradeManager.Instance.GetPermanentLevel(unlockData.unlockID) > 0;

        // Update icon
        UpdateIcon();

        // Update title
        if (titleText != null)
            titleText.text = unlockData.displayName;

        // Update based on unlock state
        if (isUnlocked)
        {
            DisplayUnlockedState();
        }
        else if (!unlockData.MeetsPrerequisites())
        {
            DisplayPrerequisiteState();
        }
        else
        {
            DisplayLockedState();
        }
    }

    /// <summary>
    /// Update icon (dynamically pulls sprite from CropData or equipment)
    /// </summary>
    private void UpdateIcon()
    {
        Sprite spriteToUse = GetIconSprite();

        // If we have a sprite, use the Image component
        if (spriteToUse != null && iconImage != null)
        {
            iconImage.sprite = spriteToUse;
            iconImage.gameObject.SetActive(true);
            
            // Hide text icon
            if (iconText != null)
                iconText.gameObject.SetActive(false);
        }
        // Otherwise fall back to emoji text
        else if (iconText != null)
        {
            iconText.text = unlockData.icon;
            iconText.gameObject.SetActive(true);
            
            // Hide image
            if (iconImage != null)
                iconImage.gameObject.SetActive(false);
        }
    }

    /// <summary>
    /// Get the sprite to display based on unlock type
    /// For Crops: Uses seedPacketSprite from CropData
    /// For Equipment: Uses equipmentSprite
    /// </summary>
    private Sprite GetIconSprite()
    {
        if (unlockData == null) return null;

        // CROPS: Pull sprite from CropData
        if (unlockData.category == UnlockCategory.Crop)
        {
            if (unlockData.cropData != null)
            {
                // Use seed packet sprite (shows the seed package for the store)
                return unlockData.cropData.seedPacketSprite;
            }
            else
            {
                Debug.LogWarning($"Unlock '{unlockData.displayName}' is a Crop but has no CropData assigned!");
            }
        }

        // EQUIPMENT: Use equipment sprite (icon for store, not the in-game sprite)
        if (unlockData.category == UnlockCategory.Equipment)
        {
            if (unlockData.equipmentSprite != null)
            {
                return unlockData.equipmentSprite;
            }
            else
            {
                Debug.LogWarning($"Unlock '{unlockData.displayName}' is Equipment but has no sprite assigned!");
            }
        }

        return null; // Fall back to emoji text
    }

    /// <summary>
    /// Display when locked and prerequisites met
    /// </summary>
    private void DisplayLockedState()
    {
        if (descriptionText != null)
        {
            descriptionText.text = unlockData.lockedDescription;
            descriptionText.color = lockedColor;
        }

        if (statusText != null)
        {
            statusText.text = "🔒 LOCKED";
            statusText.color = lockedColor;
        }

        if (costText != null)
        {
            costText.text = $"UNLOCK: {unlockData.coinCost} {coinIcon}";
            
            bool canAfford = CurrencyManager.Instance != null && 
                           CurrencyManager.Instance.CanAffordCoins(unlockData.coinCost);
            
            costText.color = canAfford ? affordableColor : unaffordableColor;
            
            if (button != null)
                button.interactable = canAfford;
        }
    }

    /// <summary>
    /// Display when prerequisites not met
    /// </summary>
    private void DisplayPrerequisiteState()
    {
        if (descriptionText != null)
        {
            string missing = unlockData.GetMissingPrerequisites();
            descriptionText.text = $"Requires: {missing}";
            descriptionText.color = prerequisiteColor;
        }

        if (statusText != null)
        {
            statusText.text = "⚠️ LOCKED";
            statusText.color = prerequisiteColor;
        }

        if (costText != null)
        {
            costText.text = "PREREQUISITES NEEDED";
            costText.color = prerequisiteColor;
        }

        if (button != null)
            button.interactable = false;
    }

    /// <summary>
    /// Display when unlocked
    /// </summary>
    private void DisplayUnlockedState()
    {
        if (descriptionText != null)
        {
            descriptionText.text = unlockData.unlockedMessage;
            descriptionText.color = unlockedColor;
        }

        if (statusText != null)
        {
            statusText.text = "✅ UNLOCKED";
            statusText.color = unlockedColor;
        }

        if (costText != null)
        {
            costText.text = "OWNED";
            costText.color = unlockedColor;
        }

        if (button != null)
            button.interactable = false;
    }

    /// <summary>
    /// Handle button click - attempt purchase
    /// </summary>
    private void OnButtonClicked()
    {
        if (unlockData == null) return;

        if (isUnlocked)
        {
            return;
        }

        if (!unlockData.MeetsPrerequisites())
        {
            return;
        }

        if (UpgradeManager.Instance == null || CurrencyManager.Instance == null)
        {
            Debug.LogError("Missing required managers!");
            return;
        }

        // Check if can afford
        if (!CurrencyManager.Instance.CanAffordCoins(unlockData.coinCost))
        {
            return;
        }

        // Purchase unlock (set to level 1 = unlocked)
        if (UpgradeManager.Instance.PurchasePermanentUpgrade(unlockData.unlockID, unlockData.coinCost))
        {
            Debug.Log($"✅ Unlocked {unlockData.displayName} for {unlockData.coinCost} coins!");
            
            // Optional: Play unlock sound/animation
            PlayUnlockEffect();
            
            // Refresh display
            isUnlocked = true;
            UpdateDisplay();
        }
        else
        {
            Debug.LogError($"Failed to unlock {unlockData.displayName}!");
        }
    }

    /// <summary>
    /// Optional: Play unlock effect
    /// </summary>
    private void PlayUnlockEffect()
    {
        // Add unlock animation here if desired
        // Example: scale bounce
        LeanTween.cancel(gameObject);
        LeanTween.scale(gameObject, Vector3.one * 1.1f, 0.15f)
            .setEaseOutQuad()
            .setOnComplete(() =>
            {
                LeanTween.scale(gameObject, Vector3.one, 0.15f).setEaseInQuad();
            });
    }

    private void HideButton()
    {
        if (canvasGroup != null)
        {
            canvasGroup.alpha = 0f;
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;
        }
    }

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
    [ContextMenu("Test Unlock")]
    private void TestUnlock()
    {
        if (unlockData != null && UpgradeManager.Instance != null)
        {
            UpgradeManager.Instance.PurchasePermanentUpgrade(unlockData.unlockID, 0);
            UpdateDisplay();
        }
    }

    [ContextMenu("Force Update Display")]
    private void ForceUpdate()
    {
        UpdateDisplay();
    }

    [ContextMenu("Show Unlock Info")]
    private void ShowUnlockInfo()
    {
        if (unlockData != null)
        {
            Debug.Log($"=== {gameObject.name} ===");
            Debug.Log($"Unlock Data: {unlockData.name}");
            Debug.Log($"Display Name: {unlockData.displayName}");
            Debug.Log($"ID: {unlockData.unlockID}");
            Debug.Log($"Cost: {unlockData.coinCost} Coins");
            Debug.Log($"Category: {unlockData.category}");
            
            if (UpgradeManager.Instance != null)
            {
                bool unlocked = UpgradeManager.Instance.GetPermanentLevel(unlockData.unlockID) > 0;
                Debug.Log($"Status: {(unlocked ? "UNLOCKED" : "LOCKED")}");
            }
        }
        else
        {
            Debug.LogWarning("No UnlockData assigned!");
        }
    }
#endif
}