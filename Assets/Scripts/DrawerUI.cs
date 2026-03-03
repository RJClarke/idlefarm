using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;

/// <summary>
/// Controls the sliding drawer UI that displays different menu panels
/// Handles smooth animations, backdrop overlay, and menu switching
/// </summary>
public class DrawerUI : MonoBehaviour
{
    public static DrawerUI Instance { get; private set; }

    [Header("UI References")]
    [SerializeField] private RectTransform drawerContainer;
    [SerializeField] private CanvasGroup backdrop; // 50% opacity overlay
    [SerializeField] private Button closeButton; // X button in drawer
    [SerializeField] private Button backdropButton; // Invisible button covering backdrop
    [SerializeField] private TextMeshProUGUI titleText; // Header title text

    [Header("Menu Panels")]
    [SerializeField] private GameObject marketPanel;
    [SerializeField] private GameObject farmPanel;
    [SerializeField] private GameObject helpersPanel;
    [SerializeField] private GameObject equipmentPanel;
    [SerializeField] private GameObject settingsPanel;

    [Header("Animation Settings")]
    [SerializeField] private float slideDuration = 0.3f; // Quick but smooth
    [SerializeField] private LeanTweenType easeType = LeanTweenType.easeOutQuad;

    [Header("Layout Settings")]
    [SerializeField] private float drawerWidthPercent = 0.95f; // 95% of screen width

    // State tracking
    private MenuType currentMenu = MenuType.None;
    private bool isOpen = false;
    private Vector2 closedPosition;
    private Vector2 openPosition;

    public enum MenuType
    {
        None,
        Market,
        Farm,
        Helpers,
        Equipment,
        Settings
    }

    // Events
    public event Action<MenuType> OnMenuOpened;
    public event Action OnMenuClosed;

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
        // Setup button listeners
        if (closeButton != null)
            closeButton.onClick.AddListener(CloseDrawer);
        
        if (backdropButton != null)
            backdropButton.onClick.AddListener(CloseDrawer);

        // FORCE drawer off-screen immediately (before layout calculations)
        drawerContainer.anchoredPosition = new Vector2(0f, -2000f);
        
        // Calculate positions after forcing off-screen
        CalculatePositions();

        // Start closed
        backdrop.alpha = 0f;
        backdrop.blocksRaycasts = false;
        HideAllPanels();
    }

    private void OnDestroy()
    {
        if (closeButton != null)
            closeButton.onClick.RemoveListener(CloseDrawer);
        
        if (backdropButton != null)
            backdropButton.onClick.RemoveListener(CloseDrawer);
    }

    /// <summary>
    /// Calculate open and closed positions for drawer
    /// </summary>
    private void CalculatePositions()
    {
        // Force layout update to get accurate height
        Canvas.ForceUpdateCanvases();
        LayoutRebuilder.ForceRebuildLayoutImmediate(drawerContainer);
        
        // Closed position: fully off-screen below
        // Use a large negative value to ensure it's completely hidden
        float drawerHeight = drawerContainer.rect.height;
        closedPosition = new Vector2(0f, -Mathf.Max(drawerHeight + 100f, 2000f));

        // Open position: at bottom of screen (y=0 since anchor is at bottom)
        openPosition = new Vector2(0f, 0f);
        
        Debug.Log($"Drawer positions calculated - Closed: {closedPosition.y}, Open: {openPosition.y}, Height: {drawerHeight}");
    }

    /// <summary>
    /// Open a specific menu (closes current if different)
    /// </summary>
    public void OpenMenu(MenuType menuType)
    {
        // If same menu, just toggle closed
        if (currentMenu == menuType && isOpen)
        {
            CloseDrawer();
            return;
        }

        // If different menu is open, switch directly (no close animation)
        if (isOpen && currentMenu != menuType)
        {
            HideAllPanels();
            ShowPanel(menuType);
            UpdateTitle(menuType);
            currentMenu = menuType;
            OnMenuOpened?.Invoke(menuType);
            
            // Recalculate height for new content
            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate(drawerContainer);
            CalculatePositions();
            
            return;
        }

        // Open fresh (animate in)
        currentMenu = menuType;
        HideAllPanels();
        ShowPanel(menuType);
        UpdateTitle(menuType);

        // Force layout update to get correct height
        Canvas.ForceUpdateCanvases();
        LayoutRebuilder.ForceRebuildLayoutImmediate(drawerContainer);
        CalculatePositions();

        AnimateOpen();
        OnMenuOpened?.Invoke(menuType);
    }

    /// <summary>
    /// Close the drawer with animation
    /// </summary>
    public void CloseDrawer()
    {
        if (!isOpen) return;

        AnimateClose();
        OnMenuClosed?.Invoke();
    }

    /// <summary>
    /// Show the appropriate panel based on menu type
    /// </summary>
    private void ShowPanel(MenuType menuType)
    {
        switch (menuType)
        {
            case MenuType.Market:
                if (marketPanel != null) marketPanel.SetActive(true);
                break;
            case MenuType.Farm:
                if (farmPanel != null) farmPanel.SetActive(true);
                break;
            case MenuType.Helpers:
                if (helpersPanel != null) helpersPanel.SetActive(true);
                break;
            case MenuType.Equipment:
                if (equipmentPanel != null) equipmentPanel.SetActive(true);
                break;
            case MenuType.Settings:
                if (settingsPanel != null) settingsPanel.SetActive(true);
                break;
        }
    }

    /// <summary>
    /// Hide all menu panels
    /// </summary>
    private void HideAllPanels()
    {
        if (marketPanel != null) marketPanel.SetActive(false);
        if (farmPanel != null) farmPanel.SetActive(false);
        if (helpersPanel != null) helpersPanel.SetActive(false);
        if (equipmentPanel != null) equipmentPanel.SetActive(false);
        if (settingsPanel != null) settingsPanel.SetActive(false);
    }

    /// <summary>
    /// Update header title based on current menu
    /// </summary>
    private void UpdateTitle(MenuType menuType)
    {
        if (titleText == null) return;

        switch (menuType)
        {
            case MenuType.Market:
                titleText.text = "🏪 Market";
                break;
            case MenuType.Farm:
                titleText.text = "🌾 Farm";
                break;
            case MenuType.Helpers:
                titleText.text = "🤖 Helpers";
                break;
            case MenuType.Equipment:
                titleText.text = "🪓 Equipment";
                break;
            case MenuType.Settings:
                titleText.text = "⚙️ Settings";
                break;
            default:
                titleText.text = "Menu";
                break;
        }
    }

    /// <summary>
    /// Animate drawer sliding up
    /// </summary>
    private void AnimateOpen()
    {
        isOpen = true;

        // Recalculate positions to account for current content height
        CalculatePositions();

        // Enable backdrop
        backdrop.blocksRaycasts = true;

        // Cancel any existing animations
        LeanTween.cancel(drawerContainer.gameObject);
        LeanTween.cancel(backdrop.gameObject);

        // Slide drawer up
        LeanTween.moveY(drawerContainer, openPosition.y, slideDuration)
            .setEase(easeType);

        // Fade in backdrop
        LeanTween.alphaCanvas(backdrop, 0.5f, slideDuration)
            .setEase(LeanTweenType.easeOutQuad);
    }

    /// <summary>
    /// Animate drawer sliding down
    /// </summary>
    private void AnimateClose()
    {
        isOpen = false;
        currentMenu = MenuType.None;

        // Recalculate to ensure proper closed position
        CalculatePositions();

        // Cancel any existing animations
        LeanTween.cancel(drawerContainer.gameObject);
        LeanTween.cancel(backdrop.gameObject);

        // Slide drawer down
        LeanTween.moveY(drawerContainer, closedPosition.y, slideDuration)
            .setEase(easeType)
            .setOnComplete(() => HideAllPanels());

        // Fade out backdrop
        LeanTween.alphaCanvas(backdrop, 0f, slideDuration)
            .setEase(LeanTweenType.easeOutQuad)
            .setOnComplete(() => backdrop.blocksRaycasts = false);
    }

    /// <summary>
    /// Check if a specific menu is currently open
    /// </summary>
    public bool IsMenuOpen(MenuType menuType)
    {
        return isOpen && currentMenu == menuType;
    }

    /// <summary>
    /// Check if any menu is open
    /// </summary>
    public bool IsAnyMenuOpen()
    {
        return isOpen;
    }

#if UNITY_EDITOR
    [ContextMenu("Test Open Market")]
    private void TestOpenMarket() => OpenMenu(MenuType.Market);

    [ContextMenu("Test Open Farm")]
    private void TestOpenFarm() => OpenMenu(MenuType.Farm);

    [ContextMenu("Test Close")]
    private void TestClose() => CloseDrawer();
#endif
}