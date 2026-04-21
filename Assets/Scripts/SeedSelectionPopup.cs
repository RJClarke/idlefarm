using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Seed selection popup shown before starting a run
/// Allows player to choose which crops to grow in each zone
/// </summary>
public class SeedSelectionPopup : MonoBehaviour
{
    public static SeedSelectionPopup Instance { get; private set; }

    [Header("UI References")]
    [SerializeField] private CanvasGroup canvasGroup;
    [SerializeField] private RectTransform popupContainer;
    [SerializeField] private Button cancelButton;
    [SerializeField] private Button beginButton;
    [SerializeField] private TextMeshProUGUI errorText;

    [Header("Zone Slots (2×2 Grid)")]
    [SerializeField] private ZoneSlot zone1Slot;
    [SerializeField] private ZoneSlot zone2Slot;
    [SerializeField] private ZoneSlot zone3Slot;
    [SerializeField] private ZoneSlot zone4Slot;

    [Header("Seed Packet Grid (2×6)")]
    [SerializeField] private Transform seedPacketContainer;
    [SerializeField] private GameObject seedPacketButtonPrefab;

    [Header("Crop Database")]
    [SerializeField] private CropDatabase cropDatabase;

    [Header("Equipment")]
    [Tooltip("All equipment the player has unlocked. Drag EquipmentData assets here.")]
    [SerializeField] private EquipmentData[] availableEquipment;

    [Header("Equipment UI Per Zone")]
    [Tooltip("Optional buttons on each zone slot — click to cycle equipment. Wire in editor.")]
    [SerializeField] private Button zone1EquipButton;
    [SerializeField] private Button zone2EquipButton;
    [SerializeField] private Button zone3EquipButton;
    [SerializeField] private Button zone4EquipButton;
    [SerializeField] private TextMeshProUGUI zone1EquipLabel;
    [SerializeField] private TextMeshProUGUI zone2EquipLabel;
    [SerializeField] private TextMeshProUGUI zone3EquipLabel;
    [SerializeField] private TextMeshProUGUI zone4EquipLabel;

    [Header("Animation")]
    [SerializeField] private float fadeInDuration = 0.3f;
    [SerializeField] private LeanTweenType easeType = LeanTweenType.easeOutQuad;
    [SerializeField] private float backdropAlpha = 0.6f;

    // State
    private Image overlayImage;
    private SeedSelectionData selectionData;
    private List<SeedPacketButton> seedPacketButtons = new List<SeedPacketButton>();
    private int selectedZoneID = -1; // Which zone is currently highlighted
    private bool isOpen = false;

    // Equipment state — zone ID → index into availableEquipment (-1 = none)
    private Dictionary<int, int> zoneEquipmentIndex = new Dictionary<int, int>();
    private const string EQUIP_PREFS_KEY = "EquipmentSelectionData";

    // Events
    public event Action OnSelectionSaved;
    public event Action OnCancelled;

    private ZoneSlot[] allZoneSlots;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        // Get or add CanvasGroup
        if (canvasGroup == null)
        {
            canvasGroup = GetComponent<CanvasGroup>();
            if (canvasGroup == null)
            {
                canvasGroup = gameObject.AddComponent<CanvasGroup>();
            }
        }

        // Store zone slot references
        allZoneSlots = new ZoneSlot[] { zone1Slot, zone2Slot, zone3Slot, zone4Slot };

        CreateOverlay();
    }

    private void CreateOverlay()
    {
        GameObject overlayGO = new GameObject("DarkOverlay");
        overlayGO.transform.SetParent(transform, false);
        overlayGO.transform.SetSiblingIndex(0); // behind PopupContainer

        RectTransform rt = overlayGO.AddComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.sizeDelta = Vector2.zero;
        rt.anchoredPosition = Vector2.zero;

        overlayImage = overlayGO.AddComponent<Image>();
        overlayImage.color = new Color(0f, 0f, 0f, 0f);
        overlayImage.raycastTarget = true;
    }

    private void Start()
    {
        // Hook up button listeners
        if (cancelButton != null)
            cancelButton.onClick.AddListener(OnCancelClicked);

        if (beginButton != null)
            beginButton.onClick.AddListener(OnBeginClicked);

        // Subscribe to zone slot events
        if (zone1Slot != null)
        {
            zone1Slot.OnZoneClicked += OnZoneSlotClicked;
            zone1Slot.OnZoneClearedRequest += OnZoneClearRequested;
        }
        if (zone2Slot != null)
        {
            zone2Slot.OnZoneClicked += OnZoneSlotClicked;
            zone2Slot.OnZoneClearedRequest += OnZoneClearRequested;
        }
        if (zone3Slot != null)
        {
            zone3Slot.OnZoneClicked += OnZoneSlotClicked;
            zone3Slot.OnZoneClearedRequest += OnZoneClearRequested;
        }
        if (zone4Slot != null)
        {
            zone4Slot.OnZoneClicked += OnZoneSlotClicked;
            zone4Slot.OnZoneClearedRequest += OnZoneClearRequested;
        }

        // Equipment buttons — cycle through available equipment on click
        if (zone1EquipButton != null) zone1EquipButton.onClick.AddListener(() => CycleEquipment(1));
        if (zone2EquipButton != null) zone2EquipButton.onClick.AddListener(() => CycleEquipment(2));
        if (zone3EquipButton != null) zone3EquipButton.onClick.AddListener(() => CycleEquipment(3));
        if (zone4EquipButton != null) zone4EquipButton.onClick.AddListener(() => CycleEquipment(4));

        // Start hidden
        HideImmediate();
    }

    private void OnDestroy()
    {
        if (cancelButton != null)
            cancelButton.onClick.RemoveListener(OnCancelClicked);

        if (beginButton != null)
            beginButton.onClick.RemoveListener(OnBeginClicked);

        // Unsubscribe from zone events
        if (zone1Slot != null)
        {
            zone1Slot.OnZoneClicked -= OnZoneSlotClicked;
            zone1Slot.OnZoneClearedRequest -= OnZoneClearRequested;
        }
        if (zone2Slot != null)
        {
            zone2Slot.OnZoneClicked -= OnZoneSlotClicked;
            zone2Slot.OnZoneClearedRequest -= OnZoneClearRequested;
        }
        if (zone3Slot != null)
        {
            zone3Slot.OnZoneClicked -= OnZoneSlotClicked;
            zone3Slot.OnZoneClearedRequest -= OnZoneClearRequested;
        }
        if (zone4Slot != null)
        {
            zone4Slot.OnZoneClicked -= OnZoneSlotClicked;
            zone4Slot.OnZoneClearedRequest -= OnZoneClearRequested;
        }

        if (zone1EquipButton != null) zone1EquipButton.onClick.RemoveAllListeners();
        if (zone2EquipButton != null) zone2EquipButton.onClick.RemoveAllListeners();
        if (zone3EquipButton != null) zone3EquipButton.onClick.RemoveAllListeners();
        if (zone4EquipButton != null) zone4EquipButton.onClick.RemoveAllListeners();
    }

    /// <summary>
    /// Show the popup
    /// </summary>
    public void Show()
    {
        if (isOpen) return;

        // Load last selection
        selectionData = SeedSelectionData.Load();

        // Initialize zone slots
        InitializeZoneSlots();

        // Create seed packet buttons
        CreateSeedPacketButtons();

        // Apply saved selections
        ApplySavedSelections();

        // Load equipment assignments and disable buttons for locked zones
        LoadEquipmentAssignments();
        UpdateAllEquipmentLabels();
        UpdateEquipmentButtonStates();

        // Update UI state
        UpdateAllSeedPacketAvailability();
        ValidateAndUpdateBeginButton();

        // Animate in
        AnimateIn();

        isOpen = true;


    }

    /// <summary>
    /// Hide the popup
    /// </summary>
    public void Hide()
    {
        if (!isOpen) return;

        AnimateOut();

        isOpen = false;


    }

    /// <summary>
    /// Initialize zone slots with unlock status
    /// </summary>
    private void InitializeZoneSlots()
    {
        if (zone1Slot != null)
            zone1Slot.Initialize(1, true); // Zone 1 always unlocked

        if (zone2Slot != null)
            zone2Slot.Initialize(2, selectionData.IsZoneUnlocked(2));

        if (zone3Slot != null)
            zone3Slot.Initialize(3, selectionData.IsZoneUnlocked(3));

        if (zone4Slot != null)
            zone4Slot.Initialize(4, selectionData.IsZoneUnlocked(4));
    }

    /// <summary>
    /// Create seed packet buttons from crop database
    /// </summary>
    private void CreateSeedPacketButtons()
    {
        if (cropDatabase == null)
        {
            Debug.LogError("CropDatabase not assigned to SeedSelectionPopup!");
            return;
        }

        if (seedPacketButtonPrefab == null)
        {
            Debug.LogError("SeedPacketButton prefab not assigned!");
            return;
        }

        // Clear existing buttons
        foreach (SeedPacketButton button in seedPacketButtons)
        {
            if (button != null)
                Destroy(button.gameObject);
        }
        seedPacketButtons.Clear();

        // Create buttons for all crops in database
        // NOTE: For MVP, all crops available. Later will filter by unlocks.
        foreach (CropData crop in cropDatabase.allCrops)
        {
            if (crop == null) continue;

            GameObject buttonObj = Instantiate(seedPacketButtonPrefab, seedPacketContainer);
            SeedPacketButton button = buttonObj.GetComponent<SeedPacketButton>();

            if (button != null)
            {
                button.Initialize(crop);
                button.OnSeedPacketClicked += OnSeedPacketClicked;
                seedPacketButtons.Add(button);
            }
        }


    }

    /// <summary>
    /// Apply saved selections to zone slots
    /// </summary>
    private void ApplySavedSelections()
    {
        for (int zoneID = 1; zoneID <= 4; zoneID++)
        {
            string cropName = selectionData.GetCropName(zoneID);
            if (cropName != null && cropDatabase != null)
            {
                CropData crop = cropDatabase.GetCropByName(cropName);
                if (crop != null)
                {
                    ZoneSlot slot = GetZoneSlot(zoneID);
                    if (slot != null && slot.IsUnlocked)
                    {
                        slot.AssignCrop(crop);
                    }
                }
            }
        }
    }

    /// <summary>
    /// Zone slot clicked - select it
    /// </summary>
    private void OnZoneSlotClicked(int zoneID)
    {
        // Deselect previously selected zone
        if (selectedZoneID > 0)
        {
            ZoneSlot prevSlot = GetZoneSlot(selectedZoneID);
            if (prevSlot != null)
                prevSlot.SetSelected(false);
        }

        // Select new zone
        selectedZoneID = zoneID;
        ZoneSlot newSlot = GetZoneSlot(zoneID);
        if (newSlot != null)
            newSlot.SetSelected(true);


    }

    /// <summary>
    /// Clear button clicked on zone slot
    /// </summary>
    private void OnZoneClearRequested(int zoneID)
    {
        // Clear from data
        selectionData.ClearZone(zoneID);

        // Clear from UI
        ZoneSlot slot = GetZoneSlot(zoneID);
        if (slot != null)
        {
            slot.ClearCrop();
        }

        // Update seed packet availability
        UpdateAllSeedPacketAvailability();
        ValidateAndUpdateBeginButton();


    }

    /// <summary>
    /// Seed packet clicked - assign to selected zone or auto-fill
    /// </summary>
    private void OnSeedPacketClicked(CropData crop)
    {
        if (crop == null) return;

        // Check if already assigned
        if (selectionData.IsCropAssigned(crop.cropName))
        {
            return;
        }

        int targetZone = selectedZoneID;

        // If no zone selected, auto-fill first empty zone
        if (targetZone <= 0)
        {
            targetZone = selectionData.GetFirstEmptyZone();
        }

        if (targetZone <= 0)
        {
            Debug.LogWarning("No empty zone available!");
            return;
        }

        // Assign crop to zone
        selectionData.AssignCrop(targetZone, crop);

        // Update UI
        ZoneSlot slot = GetZoneSlot(targetZone);
        if (slot != null)
        {
            slot.AssignCrop(crop);
        }

        // Deselect zone after assignment
        if (selectedZoneID > 0)
        {
            ZoneSlot prevSlot = GetZoneSlot(selectedZoneID);
            if (prevSlot != null)
                prevSlot.SetSelected(false);
        }
        selectedZoneID = -1;

        // Update seed packet availability
        UpdateAllSeedPacketAvailability();
        ValidateAndUpdateBeginButton();


    }

    /// <summary>
    /// Update all seed packet buttons' availability
    /// </summary>
    private void UpdateAllSeedPacketAvailability()
    {
        foreach (SeedPacketButton button in seedPacketButtons)
        {
            if (button == null || button.CropData == null) continue;

            bool isAvailable = !selectionData.IsCropAssigned(button.CropData.cropName);
            button.SetAvailable(isAvailable);
        }
    }

    /// <summary>
    /// Update Save button state — always enabled (partial config is fine)
    /// </summary>
    private void ValidateAndUpdateBeginButton()
    {
        if (beginButton != null)
            beginButton.interactable = true;

        if (errorText != null)
            errorText.gameObject.SetActive(false);
    }

    /// <summary>
    /// Check if saved config is ready to start a run (all unlocked zones filled).
    /// Called by RunManager before starting.
    /// </summary>
    public bool IsReadyToRun()
    {
        if (selectionData == null)
            selectionData = SeedSelectionData.Load();
        return selectionData.AreAllUnlockedZonesFilled();
    }

    /// <summary>
    /// Get zone slot by ID
    /// </summary>
    private ZoneSlot GetZoneSlot(int zoneID)
    {
        switch (zoneID)
        {
            case 1: return zone1Slot;
            case 2: return zone2Slot;
            case 3: return zone3Slot;
            case 4: return zone4Slot;
            default: return null;
        }
    }

    /// <summary>
    /// Cancel button clicked
    /// </summary>
    private void OnCancelClicked()
    {
        Hide();
        OnCancelled?.Invoke();
    }

    /// <summary>
    /// Save button clicked - save selections and close
    /// </summary>
    private void OnBeginClicked()
    {
        // Save selection
        selectionData.Save();
        SaveEquipmentAssignments();

        // Push equipment assignments to EquipmentManager (for home screen visuals)
        ApplyEquipmentToManager();

        // Notify listeners
        OnSelectionSaved?.Invoke();

        // Hide popup
        Hide();

        Debug.Log("Field configuration saved");
    }

    /// <summary>
    /// Load saved seed/equipment selections and apply equipment to manager.
    /// Called by RunManager when starting a run without opening the popup.
    /// Returns the zone seed dictionary.
    /// </summary>
    public Dictionary<int, CropData> LoadAndApplySavedSelections()
    {
        selectionData = SeedSelectionData.Load();
        LoadEquipmentAssignments();
        ApplyEquipmentToManager();
        return selectionData.ToZoneSeedDictionary(cropDatabase);
    }

    /// <summary>
    /// Animate popup in
    /// </summary>
    private void AnimateIn()
    {
        canvasGroup.alpha = 0f;
        canvasGroup.blocksRaycasts = true;
        canvasGroup.interactable = true;

        if (overlayImage != null)
        {
            overlayImage.color = new Color(0f, 0f, 0f, 0f);
            LeanTween.value(overlayImage.gameObject, 0f, backdropAlpha, fadeInDuration)
                .setEase(easeType)
                .setOnUpdate(a => { if (overlayImage != null) overlayImage.color = new Color(0f, 0f, 0f, a); });
        }

        if (popupContainer != null)
        {
            popupContainer.localScale = Vector3.one * 0.8f;
            LeanTween.scale(popupContainer.gameObject, Vector3.one, fadeInDuration)
                .setEase(easeType);
        }

        LeanTween.alphaCanvas(canvasGroup, 1f, fadeInDuration)
            .setEase(easeType);
    }

    /// <summary>
    /// Animate popup out
    /// </summary>
    private void AnimateOut()
    {
        canvasGroup.blocksRaycasts = false;
        canvasGroup.interactable = false;

        if (overlayImage != null)
        {
            float startAlpha = overlayImage.color.a;
            LeanTween.value(overlayImage.gameObject, startAlpha, 0f, fadeInDuration)
                .setEase(easeType)
                .setOnUpdate(a => { if (overlayImage != null) overlayImage.color = new Color(0f, 0f, 0f, a); });
        }

        if (popupContainer != null)
        {
            LeanTween.scale(popupContainer.gameObject, Vector3.one * 0.8f, fadeInDuration)
                .setEase(easeType);
        }

        LeanTween.alphaCanvas(canvasGroup, 0f, fadeInDuration)
            .setEase(easeType);
    }

    /// <summary>
    /// Hide immediately without animation
    /// </summary>
    private void HideImmediate()
    {
        canvasGroup.alpha = 0f;
        canvasGroup.blocksRaycasts = false;
        canvasGroup.interactable = false;

        if (overlayImage != null)
            overlayImage.color = new Color(0f, 0f, 0f, 0f);
    }

    // ─────────────────────────────────────────────────────────────────────
    // Equipment Selection
    // ─────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Cycle equipment for a zone: None → next unlocked → ... → None
    /// Skips equipment that hasn't been purchased at the Market.
    /// </summary>
    private void CycleEquipment(int zoneId)
    {
        if (availableEquipment == null || availableEquipment.Length == 0) return;

        ZoneSlot slot = GetZoneSlot(zoneId);
        if (slot == null || !slot.IsUnlocked) return;

        int currentIdx;
        if (!zoneEquipmentIndex.TryGetValue(zoneId, out currentIdx))
            currentIdx = -1;

        // Find next unlocked equipment (or wrap to -1 = none)
        int startIdx = currentIdx;
        while (true)
        {
            currentIdx++;
            if (currentIdx >= availableEquipment.Length)
                currentIdx = -1;

            // Wrapped back to none — always valid
            if (currentIdx == -1)
                break;

            // Check if this equipment is unlocked
            if (availableEquipment[currentIdx] != null && availableEquipment[currentIdx].IsUnlocked())
                break;

            // Safety: if we've looped all the way around, settle on none
            if (currentIdx == startIdx)
            {
                currentIdx = -1;
                break;
            }
        }

        zoneEquipmentIndex[zoneId] = currentIdx;
        UpdateEquipmentLabel(zoneId);
    }

    private void UpdateEquipmentLabel(int zoneId)
    {
        TextMeshProUGUI label = GetEquipmentLabel(zoneId);
        if (label == null) return;

        int idx;
        if (!zoneEquipmentIndex.TryGetValue(zoneId, out idx))
            idx = -1;

        if (idx < 0 || availableEquipment == null || idx >= availableEquipment.Length)
        {
            label.text = "No Equipment";
        }
        else
        {
            EquipmentData eq = availableEquipment[idx];
            label.text = eq != null ? eq.displayName : "No Equipment";
        }
    }

    private void UpdateAllEquipmentLabels()
    {
        for (int z = 1; z <= 4; z++)
            UpdateEquipmentLabel(z);
    }

    private TextMeshProUGUI GetEquipmentLabel(int zoneId)
    {
        switch (zoneId)
        {
            case 1: return zone1EquipLabel;
            case 2: return zone2EquipLabel;
            case 3: return zone3EquipLabel;
            case 4: return zone4EquipLabel;
            default: return null;
        }
    }

    private Button GetEquipmentButton(int zoneId)
    {
        switch (zoneId)
        {
            case 1: return zone1EquipButton;
            case 2: return zone2EquipButton;
            case 3: return zone3EquipButton;
            case 4: return zone4EquipButton;
            default: return null;
        }
    }

    /// <summary>
    /// Disable equipment buttons and clear selections for locked zones.
    /// </summary>
    private void UpdateEquipmentButtonStates()
    {
        for (int z = 1; z <= 4; z++)
        {
            ZoneSlot slot = GetZoneSlot(z);
            bool unlocked = slot != null && slot.IsUnlocked;

            Button btn = GetEquipmentButton(z);
            if (btn != null)
            {
                btn.interactable = unlocked;
                btn.gameObject.SetActive(unlocked);
            }

            TextMeshProUGUI label = GetEquipmentLabel(z);
            if (label != null)
                label.gameObject.SetActive(unlocked);

            // Clear any saved equipment for locked zones
            if (!unlocked && zoneEquipmentIndex.ContainsKey(z))
                zoneEquipmentIndex.Remove(z);
        }
    }

    /// <summary>
    /// Push current equipment selections to EquipmentManager.
    /// </summary>
    private void ApplyEquipmentToManager()
    {
        if (EquipmentManager.Instance == null) return;

        EquipmentManager.Instance.ClearAllAssignments();

        foreach (var kvp in zoneEquipmentIndex)
        {
            int idx = kvp.Value;
            if (idx >= 0 && availableEquipment != null && idx < availableEquipment.Length)
            {
                EquipmentManager.Instance.AssignEquipment(kvp.Key, availableEquipment[idx]);
            }
        }
    }

    /// <summary>
    /// Save equipment assignments to PlayerPrefs (parallel to SeedSelectionData).
    /// Format: "zoneId:equipmentID;zoneId:equipmentID;..."
    /// </summary>
    private void SaveEquipmentAssignments()
    {
        List<string> entries = new List<string>();
        foreach (var kvp in zoneEquipmentIndex)
        {
            int idx = kvp.Value;
            if (idx >= 0 && availableEquipment != null && idx < availableEquipment.Length)
            {
                EquipmentData eq = availableEquipment[idx];
                if (eq != null)
                    entries.Add($"{kvp.Key}:{eq.equipmentID}");
            }
        }
        PlayerPrefs.SetString(EQUIP_PREFS_KEY, string.Join(";", entries));
        PlayerPrefs.Save();
    }

    /// <summary>
    /// Load equipment assignments from PlayerPrefs.
    /// </summary>
    private void LoadEquipmentAssignments()
    {
        zoneEquipmentIndex.Clear();

        if (!PlayerPrefs.HasKey(EQUIP_PREFS_KEY)) return;
        if (availableEquipment == null || availableEquipment.Length == 0) return;

        string saved = PlayerPrefs.GetString(EQUIP_PREFS_KEY);
        if (string.IsNullOrEmpty(saved)) return;

        string[] entries = saved.Split(';');
        foreach (string entry in entries)
        {
            string[] parts = entry.Split(':');
            if (parts.Length != 2) continue;

            int zoneId;
            if (!int.TryParse(parts[0], out zoneId)) continue;

            string equipId = parts[1];
            int idx = Array.FindIndex(availableEquipment, e => e != null && e.equipmentID == equipId);
            if (idx >= 0 && availableEquipment[idx].IsUnlocked())
                zoneEquipmentIndex[zoneId] = idx;
        }
    }
}