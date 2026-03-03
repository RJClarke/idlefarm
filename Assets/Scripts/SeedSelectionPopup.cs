using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;
using System.Collections.Generic;

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
    [SerializeField] private Button backdropButton; // Click outside to close
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

    [Header("Animation")]
    [SerializeField] private float fadeInDuration = 0.3f;
    [SerializeField] private LeanTweenType easeType = LeanTweenType.easeOutQuad;

    // State
    private SeedSelectionData selectionData;
    private List<SeedPacketButton> seedPacketButtons = new List<SeedPacketButton>();
    private int selectedZoneID = -1; // Which zone is currently highlighted
    private bool isOpen = false;

    // Events
    public event Action<Dictionary<int, CropData>> OnSeedsConfirmed;
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
    }

    private void Start()
    {
        // Hook up button listeners
        if (cancelButton != null)
            cancelButton.onClick.AddListener(OnCancelClicked);

        if (beginButton != null)
            beginButton.onClick.AddListener(OnBeginClicked);

        if (backdropButton != null)
            backdropButton.onClick.AddListener(OnCancelClicked);

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

        // Start hidden
        HideImmediate();
    }

    private void OnDestroy()
    {
        if (cancelButton != null)
            cancelButton.onClick.RemoveListener(OnCancelClicked);

        if (beginButton != null)
            beginButton.onClick.RemoveListener(OnBeginClicked);

        if (backdropButton != null)
            backdropButton.onClick.RemoveListener(OnCancelClicked);

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
    /// Check if all unlocked zones are filled and update Begin button
    /// </summary>
    private void ValidateAndUpdateBeginButton()
    {
        bool allFilled = selectionData.AreAllUnlockedZonesFilled();

        if (beginButton != null)
        {
            beginButton.interactable = allFilled;
        }

        if (errorText != null)
        {
            if (allFilled)
            {
                errorText.gameObject.SetActive(false);
            }
            else
            {
                errorText.gameObject.SetActive(true);
                errorText.text = "⚠️ Please select crops for all unlocked zones";
            }
        }
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
    /// Begin button clicked - validate and confirm
    /// </summary>
    private void OnBeginClicked()
    {
        if (!selectionData.AreAllUnlockedZonesFilled())
        {
            Debug.LogWarning("Cannot begin - not all zones filled!");
            return;
        }

        // Save selection for next time
        selectionData.Save();

        // Convert to Dictionary for HelperManager
        Dictionary<int, CropData> zoneSeeds = selectionData.ToZoneSeedDictionary(cropDatabase);

        // Notify listeners
        OnSeedsConfirmed?.Invoke(zoneSeeds);

        // Hide popup
        Hide();

        Debug.Log($"✅ Seed selection confirmed: {zoneSeeds.Count} zones");
    }

    /// <summary>
    /// Animate popup in
    /// </summary>
    private void AnimateIn()
    {
        canvasGroup.alpha = 0f;
        canvasGroup.blocksRaycasts = true;
        canvasGroup.interactable = true;

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
    }
}