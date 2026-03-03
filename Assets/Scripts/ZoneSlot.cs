using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;

/// <summary>
/// Individual zone slot in the seed selection popup
/// Shows: Locked state, Empty state, or Filled state with seed packet
/// </summary>
public class ZoneSlot : MonoBehaviour
{
    [Header("Zone Info")]
    [SerializeField] private int zoneID;

    [Header("UI References")]
    [SerializeField] private Image backgroundImage;
    [SerializeField] private Image seedPacketImage;
    [SerializeField] private TextMeshProUGUI cropNameText;
    [SerializeField] private TextMeshProUGUI statusText; // "Select a Crop" or "Locked"
    [SerializeField] private Button slotButton;
    [SerializeField] private Button clearButton; // X button

    [Header("Visual States")]
    [SerializeField] private Color normalColor = new Color(0.9f, 0.85f, 0.7f); // Parchment
    [SerializeField] private Color selectedColor = new Color(1f, 0.95f, 0.5f); // Bright yellow
    [SerializeField] private Color lockedColor = new Color(0.5f, 0.5f, 0.5f); // Grey

    // State
    private bool isUnlocked = true;
    private bool isSelected = false;
    private CropData assignedCrop = null;

    // Events
    public event Action<int> OnZoneClicked;
    public event Action<int> OnZoneClearedRequest;

    // Properties
    public int ZoneID => zoneID;
    public bool IsUnlocked => isUnlocked;
    public CropData AssignedCrop => assignedCrop;

    private void Start()
    {
        if (slotButton != null)
        {
            slotButton.onClick.AddListener(OnSlotClicked);
        }

        if (clearButton != null)
        {
            clearButton.onClick.AddListener(OnClearClicked);
        }

        UpdateVisuals();
    }

    private void OnDestroy()
    {
        if (slotButton != null)
        {
            slotButton.onClick.RemoveListener(OnSlotClicked);
        }

        if (clearButton != null)
        {
            clearButton.onClick.RemoveListener(OnClearClicked);
        }
    }

    /// <summary>
    /// Initialize zone with ID and unlock status
    /// </summary>
    public void Initialize(int id, bool unlocked)
    {
        zoneID = id;
        isUnlocked = unlocked;
        UpdateVisuals();
    }

    /// <summary>
    /// Assign a crop to this zone
    /// </summary>
    public void AssignCrop(CropData crop)
    {
        assignedCrop = crop;
        UpdateVisuals();
    }

    /// <summary>
    /// Clear crop assignment
    /// </summary>
    public void ClearCrop()
    {
        assignedCrop = null;
        UpdateVisuals();
    }

    /// <summary>
    /// Set selected state (highlighted)
    /// </summary>
    public void SetSelected(bool selected)
    {
        isSelected = selected;
        UpdateVisuals();
    }

    /// <summary>
    /// Update visual appearance based on state
    /// </summary>
    private void UpdateVisuals()
    {
        // LOCKED STATE
        if (!isUnlocked)
        {
            if (backgroundImage != null)
                backgroundImage.color = lockedColor;

            if (seedPacketImage != null)
                seedPacketImage.gameObject.SetActive(false);

            if (cropNameText != null)
                cropNameText.gameObject.SetActive(false);

            if (statusText != null)
            {
                statusText.gameObject.SetActive(true);
                statusText.text = "🔒 Locked";
            }

            if (slotButton != null)
                slotButton.interactable = false;

            if (clearButton != null)
                clearButton.gameObject.SetActive(false);

            return;
        }

        // UNLOCKED - Update background color
        if (backgroundImage != null)
        {
            backgroundImage.color = isSelected ? selectedColor : normalColor;
        }

        // FILLED STATE
        if (assignedCrop != null)
        {
            // Show seed packet sprite
            if (seedPacketImage != null)
            {
                seedPacketImage.gameObject.SetActive(true);
                seedPacketImage.sprite = assignedCrop.seedPacketSprite;
            }

            // Show crop name
            if (cropNameText != null)
            {
                cropNameText.gameObject.SetActive(true);
                cropNameText.text = assignedCrop.cropName;
            }

            // Hide status text
            if (statusText != null)
            {
                statusText.gameObject.SetActive(false);
            }

            // Show clear button (X)
            if (clearButton != null)
            {
                clearButton.gameObject.SetActive(true);
            }

            if (slotButton != null)
            {
                slotButton.interactable = true;
            }
        }
        // EMPTY STATE
        else
        {
            // Hide seed packet
            if (seedPacketImage != null)
            {
                seedPacketImage.gameObject.SetActive(false);
            }

            // Hide crop name
            if (cropNameText != null)
            {
                cropNameText.gameObject.SetActive(false);
            }

            // Show "Select a Crop" message
            if (statusText != null)
            {
                statusText.gameObject.SetActive(true);
                statusText.text = "Select a Crop";
            }

            // Hide clear button
            if (clearButton != null)
            {
                clearButton.gameObject.SetActive(false);
            }

            if (slotButton != null)
            {
                slotButton.interactable = true;
            }
        }
    }

    /// <summary>
    /// Slot clicked - notify popup
    /// </summary>
    private void OnSlotClicked()
    {
        if (!isUnlocked) return;

        OnZoneClicked?.Invoke(zoneID);
    }

    /// <summary>
    /// Clear button clicked
    /// </summary>
    private void OnClearClicked()
    {
        OnZoneClearedRequest?.Invoke(zoneID);
    }
}