using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;

/// <summary>
/// Individual seed packet button in the 2×6 grid
/// Shows crop sprite and name, greys out when already assigned
/// </summary>
public class SeedPacketButton : MonoBehaviour
{
    [Header("Crop Data")]
    [SerializeField] private CropData cropData;

    [Header("UI References")]
    [SerializeField] private Button button;
    [SerializeField] private Image seedPacketImage;
    [SerializeField] private TextMeshProUGUI cropNameText;
    [SerializeField] private CanvasGroup canvasGroup;

    [Header("Visual States")]
    [SerializeField] private Color availableColor = Color.white;
    [SerializeField] private Color usedColor = new Color(0.5f, 0.5f, 0.5f);
    [SerializeField] private float usedAlpha = 0.5f;

    // State
    private bool isAvailable = true;

    // Events
    public event Action<CropData> OnSeedPacketClicked;

    // Properties
    public CropData CropData => cropData;

    private void Start()
    {
        if (button != null)
        {
            button.onClick.AddListener(OnButtonClicked);
        }

        if (canvasGroup == null)
        {
            canvasGroup = gameObject.AddComponent<CanvasGroup>();
        }

        UpdateVisuals();
    }

    private void OnDestroy()
    {
        if (button != null)
        {
            button.onClick.RemoveListener(OnButtonClicked);
        }
    }

    /// <summary>
    /// Initialize with crop data
    /// </summary>
    public void Initialize(CropData crop)
    {
        cropData = crop;

        if (crop != null)
        {
            // Set seed packet sprite
            if (seedPacketImage != null && crop.seedPacketSprite != null)
            {
                seedPacketImage.sprite = crop.seedPacketSprite;
            }

            // Set crop name
            if (cropNameText != null)
            {
                cropNameText.text = crop.cropName;
            }
        }

        UpdateVisuals();
    }

    /// <summary>
    /// Set availability (whether crop is already assigned to a zone)
    /// </summary>
    public void SetAvailable(bool available)
    {
        isAvailable = available;
        UpdateVisuals();
    }

    /// <summary>
    /// Update visual appearance based on availability
    /// </summary>
    private void UpdateVisuals()
    {
        if (cropData == null) return;

        if (isAvailable)
        {
            // Available - full opacity, enabled
            if (canvasGroup != null)
            {
                canvasGroup.alpha = 1f;
            }

            if (seedPacketImage != null)
            {
                seedPacketImage.color = availableColor;
            }

            if (button != null)
            {
                button.interactable = true;
            }
        }
        else
        {
            // Used - greyed out, reduced opacity, disabled
            if (canvasGroup != null)
            {
                canvasGroup.alpha = usedAlpha;
            }

            if (seedPacketImage != null)
            {
                seedPacketImage.color = usedColor;
            }

            if (button != null)
            {
                button.interactable = false;
            }
        }
    }

    /// <summary>
    /// Button clicked - notify popup
    /// </summary>
    private void OnButtonClicked()
    {
        if (!isAvailable) return;

        OnSeedPacketClicked?.Invoke(cropData);
    }
}