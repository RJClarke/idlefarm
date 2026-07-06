using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Bottom navigation bar with 4 buttons: Farm, Helpers, Equipment, Settings.
/// Market is now accessed by panning the camera to the Market location and tapping a shop building.
/// </summary>
public class BottomNav : MonoBehaviour
{
    [Header("Button References")]
    [SerializeField] private Button farmButton;
    [SerializeField] private Button helpersButton;
    [SerializeField] private Button equipmentButton;
    [SerializeField] private Button settingsButton;

    [Header("Button Icons/Text")]
    [SerializeField] private TextMeshProUGUI farmText;
    [SerializeField] private TextMeshProUGUI helpersText;
    [SerializeField] private TextMeshProUGUI equipmentText;
    [SerializeField] private TextMeshProUGUI settingsText;

    [Header("Visual Settings")]
    [SerializeField] private Color normalColor = new Color(0.8f, 0.7f, 0.6f); // Light brown
    [SerializeField] private Color selectedColor = new Color(1f, 0.9f, 0.7f); // Bright parchment

    [Header("Mode Icons (emoji fallback)")]
    [SerializeField] private string farmIcon = "🌾";
    [SerializeField] private string helpersIcon = "🤖";
    [SerializeField] private string equipmentIcon = "🪓"; // Axe (closest to rake/shovel)
    [SerializeField] private string settingsIcon = "⚙️";

    [Header("Mode Icon Sprites (optional — override emoji with a pixel icon above the label)")]
    [SerializeField] private Sprite farmSprite;
    [SerializeField] private Sprite helpersSprite;
    [SerializeField] private Sprite equipmentSprite;
    [SerializeField] private Sprite settingsSprite;

    private DrawerUI.MenuType selectedMenu = DrawerUI.MenuType.None;

    private void Start()
    {
        if (DrawerUI.Instance != null)
        {
            DrawerUI.Instance.OnMenuOpened += OnMenuOpened;
            DrawerUI.Instance.OnMenuClosed += OnMenuClosed;
        }

        if (farmButton != null)
            farmButton.onClick.AddListener(() => OnButtonClicked(DrawerUI.MenuType.Farm));

        if (helpersButton != null)
            helpersButton.onClick.AddListener(() => OnButtonClicked(DrawerUI.MenuType.Helpers));

        if (equipmentButton != null)
            equipmentButton.onClick.AddListener(() => OnButtonClicked(DrawerUI.MenuType.Equipment));

        if (settingsButton != null)
            settingsButton.onClick.AddListener(() => OnButtonClicked(DrawerUI.MenuType.Settings));

        SetupButtonIcons();
        UpdateButtonVisuals();
    }

    private void OnDestroy()
    {
        if (DrawerUI.Instance != null)
        {
            DrawerUI.Instance.OnMenuOpened -= OnMenuOpened;
            DrawerUI.Instance.OnMenuClosed -= OnMenuClosed;
        }

        if (farmButton != null)
            farmButton.onClick.RemoveAllListeners();
        if (helpersButton != null)
            helpersButton.onClick.RemoveAllListeners();
        if (equipmentButton != null)
            equipmentButton.onClick.RemoveAllListeners();
        if (settingsButton != null)
            settingsButton.onClick.RemoveAllListeners();
    }

    private void SetupButtonIcons()
    {
        ApplyButtonIcon(farmText,      farmSprite,      farmIcon,      "Farm");
        ApplyButtonIcon(helpersText,   helpersSprite,   helpersIcon,   "Helpers");
        ApplyButtonIcon(equipmentText, equipmentSprite, equipmentIcon, "Equipment");
        ApplyButtonIcon(settingsText,  settingsSprite,  settingsIcon,  "Settings");
    }

    /// <summary>
    /// If a sprite is assigned, render a pixel icon at the top of the button with the word
    /// below it; otherwise fall back to the "{emoji}\n{word}" text label.
    /// </summary>
    private void ApplyButtonIcon(TextMeshProUGUI text, Sprite sprite, string emoji, string word)
    {
        if (text == null) return;

        if (sprite == null)
        {
            text.text = $"{emoji}\n{word}";
            return;
        }

        text.text = word;
        text.alignment = TextAlignmentOptions.Bottom;

        Transform parent = text.transform.parent; // the Button
        Transform existing = parent.Find("NavIcon");
        Image img;
        if (existing != null)
        {
            img = existing.GetComponent<Image>();
        }
        else
        {
            var iconGO = new GameObject("NavIcon", typeof(RectTransform), typeof(Image));
            iconGO.transform.SetParent(parent, false);
            img = iconGO.GetComponent<Image>();
            img.raycastTarget = false;
            img.preserveAspect = true;
            var rt = iconGO.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 1f);
            rt.anchorMax = new Vector2(0.5f, 1f);
            rt.pivot     = new Vector2(0.5f, 1f);
            rt.sizeDelta = new Vector2(40f, 40f);
            rt.anchoredPosition = new Vector2(0f, -6f);
        }
        img.sprite = sprite;
        img.enabled = true;
    }

    /// <summary>
    /// Button clicked - tell drawer to open menu
    /// </summary>
    private void OnButtonClicked(DrawerUI.MenuType menuType)
    {
        // Close any standalone popup that isn't the one we're toggling.
        CloseOtherPopups(menuType);

        // Equipment is served by the standalone UI Toolkit popup, not the uGUI drawer.
        if (menuType == DrawerUI.MenuType.Equipment)
        {
            if (DrawerUI.Instance != null && DrawerUI.Instance.IsAnyMenuOpen())
                DrawerUI.Instance.CloseDrawer();

            if (EquipmentPopupUITK.Instance == null) return;
            if (EquipmentPopupUITK.Instance.IsOpen) EquipmentPopupUITK.Instance.Close();
            else EquipmentPopupUITK.Instance.Open();
            return;
        }

        // Helpers is also a standalone UI Toolkit popup.
        if (menuType == DrawerUI.MenuType.Helpers)
        {
            if (DrawerUI.Instance != null && DrawerUI.Instance.IsAnyMenuOpen())
                DrawerUI.Instance.CloseDrawer();

            if (HelpersPopupUITK.Instance == null) return;
            if (HelpersPopupUITK.Instance.IsOpen) HelpersPopupUITK.Instance.Close();
            else HelpersPopupUITK.Instance.Open();
            return;
        }

        // Farm is also a standalone UI Toolkit popup.
        if (menuType == DrawerUI.MenuType.Farm)
        {
            if (DrawerUI.Instance != null && DrawerUI.Instance.IsAnyMenuOpen())
                DrawerUI.Instance.CloseDrawer();

            if (FarmPopupUITK.Instance == null) return;
            if (FarmPopupUITK.Instance.IsOpen) FarmPopupUITK.Instance.Close();
            else FarmPopupUITK.Instance.Open();
            return;
        }

        // Settings — same fallback pattern.
        if (menuType == DrawerUI.MenuType.Settings && SettingsPopupUITK.Instance != null)
        {
            if (DrawerUI.Instance != null && DrawerUI.Instance.IsAnyMenuOpen())
                DrawerUI.Instance.CloseDrawer();

            if (SettingsPopupUITK.Instance.IsOpen) SettingsPopupUITK.Instance.Close();
            else SettingsPopupUITK.Instance.Open();
            return;
        }

        if (DrawerUI.Instance != null)
            DrawerUI.Instance.OpenMenu(menuType);
    }

    private void CloseOtherPopups(DrawerUI.MenuType current)
    {
        if (current != DrawerUI.MenuType.Equipment &&
            EquipmentPopupUITK.Instance != null && EquipmentPopupUITK.Instance.IsOpen)
            EquipmentPopupUITK.Instance.Close();

        if (current != DrawerUI.MenuType.Helpers &&
            HelpersPopupUITK.Instance != null && HelpersPopupUITK.Instance.IsOpen)
            HelpersPopupUITK.Instance.Close();

        if (current != DrawerUI.MenuType.Farm &&
            FarmPopupUITK.Instance != null && FarmPopupUITK.Instance.IsOpen)
            FarmPopupUITK.Instance.Close();

        if (current != DrawerUI.MenuType.Settings &&
            SettingsPopupUITK.Instance != null && SettingsPopupUITK.Instance.IsOpen)
            SettingsPopupUITK.Instance.Close();
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

    private void UpdateButtonVisuals()
    {
        UpdateButtonColor(farmButton, farmText, DrawerUI.MenuType.Farm);
        UpdateButtonColor(helpersButton, helpersText, DrawerUI.MenuType.Helpers);
        UpdateButtonColor(equipmentButton, equipmentText, DrawerUI.MenuType.Equipment);
        UpdateButtonColor(settingsButton, settingsText, DrawerUI.MenuType.Settings);
    }

    private void UpdateButtonColor(Button button, TextMeshProUGUI text, DrawerUI.MenuType menuType)
    {
        if (button == null || text == null) return;
        text.color = (selectedMenu == menuType) ? selectedColor : normalColor;
    }
}