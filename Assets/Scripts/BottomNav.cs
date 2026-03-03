using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Bottom navigation bar with 5 buttons always visible
/// Town Mode: All 5 buttons enabled (Market, Farm, Helpers, Equipment, Settings)
/// Farm Mode: Market button disabled with low opacity (can't access during runs)
/// </summary>
public class BottomNav : MonoBehaviour
{
    [Header("Button References")]
    [SerializeField] private Button marketButton;
    [SerializeField] private Button farmButton;
    [SerializeField] private Button helpersButton;
    [SerializeField] private Button equipmentButton;
    [SerializeField] private Button settingsButton;

    [Header("Button Icons/Text")]
    [SerializeField] private TextMeshProUGUI marketText;
    [SerializeField] private TextMeshProUGUI farmText;
    [SerializeField] private TextMeshProUGUI helpersText;
    [SerializeField] private TextMeshProUGUI equipmentText;
    [SerializeField] private TextMeshProUGUI settingsText;

    [Header("Visual Settings")]
    [SerializeField] private Color normalColor = new Color(0.8f, 0.7f, 0.6f); // Light brown
    [SerializeField] private Color selectedColor = new Color(1f, 0.9f, 0.7f); // Bright parchment
    [SerializeField] private Color disabledColor = new Color(0.4f, 0.4f, 0.4f); // Gray
    [SerializeField] private float disabledOpacity = 0.3f; // Opacity for disabled Market button

    [Header("Mode Icons")]
    [SerializeField] private string marketIcon = "🏪";
    [SerializeField] private string farmIcon = "🌾";
    [SerializeField] private string helpersIcon = "🤖";
    [SerializeField] private string equipmentIcon = "🪓"; // Axe (closest to rake/shovel)
    [SerializeField] private string settingsIcon = "⚙️";

    private DrawerUI.MenuType selectedMenu = DrawerUI.MenuType.None;
    private bool isTownMode = true;

    private void Start()
    {
        // Subscribe to run events
        if (RunManager.Instance != null)
        {
            RunManager.Instance.OnRunStarted += OnRunStarted;
            RunManager.Instance.OnRunEnded += OnRunEnded;
        }

        // Subscribe to drawer events
        if (DrawerUI.Instance != null)
        {
            DrawerUI.Instance.OnMenuOpened += OnMenuOpened;
            DrawerUI.Instance.OnMenuClosed += OnMenuClosed;
        }

        // Setup button listeners
        if (marketButton != null)
            marketButton.onClick.AddListener(() => OnButtonClicked(DrawerUI.MenuType.Market));
        
        if (farmButton != null)
            farmButton.onClick.AddListener(() => OnButtonClicked(DrawerUI.MenuType.Farm));
        
        if (helpersButton != null)
            helpersButton.onClick.AddListener(() => OnButtonClicked(DrawerUI.MenuType.Helpers));
        
        if (equipmentButton != null)
            equipmentButton.onClick.AddListener(() => OnButtonClicked(DrawerUI.MenuType.Equipment));
        
        if (settingsButton != null)
            settingsButton.onClick.AddListener(() => OnButtonClicked(DrawerUI.MenuType.Settings));

        // Setup initial icons
        SetupButtonIcons();

        // Start in appropriate mode
        UpdateMode(RunManager.Instance != null && RunManager.Instance.IsRunActive);

        // Initial visual state
        UpdateButtonVisuals();
    }

    private void OnDestroy()
    {
        if (RunManager.Instance != null)
        {
            RunManager.Instance.OnRunStarted -= OnRunStarted;
            RunManager.Instance.OnRunEnded -= OnRunEnded;
        }

        if (DrawerUI.Instance != null)
        {
            DrawerUI.Instance.OnMenuOpened -= OnMenuOpened;
            DrawerUI.Instance.OnMenuClosed -= OnMenuClosed;
        }

        if (marketButton != null)
            marketButton.onClick.RemoveAllListeners();
        if (farmButton != null)
            farmButton.onClick.RemoveAllListeners();
        if (helpersButton != null)
            helpersButton.onClick.RemoveAllListeners();
        if (equipmentButton != null)
            equipmentButton.onClick.RemoveAllListeners();
        if (settingsButton != null)
            settingsButton.onClick.RemoveAllListeners();
    }

    /// <summary>
    /// Setup button icons and labels
    /// </summary>
    private void SetupButtonIcons()
    {
        if (marketText != null)
            marketText.text = $"{marketIcon}\nMarket";
        
        if (farmText != null)
            farmText.text = $"{farmIcon}\nFarm";
        
        if (helpersText != null)
            helpersText.text = $"{helpersIcon}\nHelpers";
        
        if (equipmentText != null)
            equipmentText.text = $"{equipmentIcon}\nEquipment";
        
        if (settingsText != null)
            settingsText.text = $"{settingsIcon}\nSettings";
    }

    /// <summary>
    /// Button clicked - tell drawer to open menu
    /// </summary>
    private void OnButtonClicked(DrawerUI.MenuType menuType)
    {
        if (DrawerUI.Instance != null)
        {
            DrawerUI.Instance.OpenMenu(menuType);
        }
    }

    /// <summary>
    /// Called when drawer opens a menu
    /// </summary>
    private void OnMenuOpened(DrawerUI.MenuType menuType)
    {
        selectedMenu = menuType;
        UpdateButtonVisuals();
    }

    /// <summary>
    /// Called when drawer closes
    /// </summary>
    private void OnMenuClosed()
    {
        selectedMenu = DrawerUI.MenuType.None;
        UpdateButtonVisuals();
    }

    /// <summary>
    /// Called when run starts - switch to Farm Mode (hide Market)
    /// </summary>
    private void OnRunStarted()
    {
        UpdateMode(true); // Farm Mode
    }

    /// <summary>
    /// Called when run ends - switch to Town Mode (show Market)
    /// </summary>
    private void OnRunEnded()
    {
        UpdateMode(false); // Town Mode
    }

    /// <summary>
    /// Update UI based on mode
    /// </summary>
    private void UpdateMode(bool isFarmMode)
    {
        isTownMode = !isFarmMode;

        // Market button always visible, but disabled in Farm Mode
        if (marketButton != null)
        {
            // Keep button visible but disable it in Farm Mode
            marketButton.interactable = isTownMode;
        }

        // If Market was open when entering Farm Mode, close it
        if (isFarmMode && selectedMenu == DrawerUI.MenuType.Market)
        {
            if (DrawerUI.Instance != null)
            {
                DrawerUI.Instance.CloseDrawer();
            }
        }

        UpdateButtonVisuals();

        Debug.Log($"🔄 BottomNav: Switched to {(isTownMode ? "Town" : "Farm")} Mode");
    }

    /// <summary>
    /// Update button colors based on selection
    /// </summary>
    private void UpdateButtonVisuals()
    {
        UpdateButtonColor(marketButton, marketText, DrawerUI.MenuType.Market);
        UpdateButtonColor(farmButton, farmText, DrawerUI.MenuType.Farm);
        UpdateButtonColor(helpersButton, helpersText, DrawerUI.MenuType.Helpers);
        UpdateButtonColor(equipmentButton, equipmentText, DrawerUI.MenuType.Equipment);
        UpdateButtonColor(settingsButton, settingsText, DrawerUI.MenuType.Settings);
    }

    /// <summary>
    /// Update individual button color
    /// </summary>
    private void UpdateButtonColor(Button button, TextMeshProUGUI text, DrawerUI.MenuType menuType)
    {
        if (button == null || text == null) return;

        // Check if button is disabled (Market in Farm Mode)
        bool isDisabled = !button.interactable;

        // Selected state
        if (selectedMenu == menuType && !isDisabled)
        {
            text.color = selectedColor;
        }
        // Disabled state (Market in Farm Mode)
        else if (isDisabled)
        {
            Color dimmed = disabledColor;
            dimmed.a = disabledOpacity; // Apply low opacity
            text.color = dimmed;
        }
        // Normal state
        else
        {
            text.color = normalColor;
        }
    }

#if UNITY_EDITOR
    [ContextMenu("Force Town Mode")]
    private void TestTownMode()
    {
        UpdateMode(false);
    }

    [ContextMenu("Force Farm Mode")]
    private void TestFarmMode()
    {
        UpdateMode(true);
    }
#endif
}