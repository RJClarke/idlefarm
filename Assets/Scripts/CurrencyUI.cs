using UnityEngine;
using TMPro;

/// <summary>
/// Handles displaying currency values on screen
/// Updates automatically when currency changes
/// Optimized for mobile with TextMeshPro
/// </summary>
public class CurrencyUI : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private TextMeshProUGUI moneyText;
    [SerializeField] private TextMeshProUGUI coinsText;

    [Header("Display Settings")]
    [SerializeField] private string moneyPrefix = "$";
    [SerializeField] private string coinsPrefix = "";
    [SerializeField] private bool useThousandsSeparator = true;

    [Header("Animation (Optional)")]
    [SerializeField] private bool animateOnChange = true;
    [SerializeField] private float animationDuration = 0.3f;
    [SerializeField] private float punchScale = 1.2f;

    private void Start()
    {
        // Wait a frame to ensure CurrencyManager is initialized
        Invoke(nameof(Initialize), 0.1f);
    }

    private void Initialize()
    {
        if (CurrencyManager.Instance != null)
        {
            // Subscribe to events
            CurrencyManager.Instance.OnMoneyChanged += UpdateMoneyDisplay;
            CurrencyManager.Instance.OnCoinsChanged += UpdateCoinsDisplay;

            // Initial display
            UpdateMoneyDisplay(CurrencyManager.Instance.Money);
            UpdateCoinsDisplay(CurrencyManager.Instance.Coins);
            
        }
        else
        {
            Debug.LogError("CurrencyManager not found! Make sure CurrencyManager exists in the scene.");
        }
    }

    private void OnDestroy()
    {
        // Unsubscribe to prevent memory leaks
        if (CurrencyManager.Instance != null)
        {
            CurrencyManager.Instance.OnMoneyChanged -= UpdateMoneyDisplay;
            CurrencyManager.Instance.OnCoinsChanged -= UpdateCoinsDisplay;
        }
    }

    private void UpdateMoneyDisplay(int newAmount)
    {
        if (moneyText != null)
        {
            string formattedAmount = FormatCurrency(newAmount);
            moneyText.text = moneyPrefix + formattedAmount;

            if (animateOnChange)
            {
                AnimateText(moneyText);
            }
        }
    }

    private void UpdateCoinsDisplay(int newAmount)
    {
        if (coinsText != null)
        {
            string formattedAmount = FormatCurrency(newAmount);
            coinsText.text = coinsPrefix + formattedAmount;

            if (animateOnChange)
            {
                AnimateText(coinsText);
            }
        }
    }

    private string FormatCurrency(int amount)
    {
        if (useThousandsSeparator)
        {
            return amount.ToString("N0");
        }
        else
        {
            return amount.ToString();
        }
    }

    private void AnimateText(TextMeshProUGUI textComponent)
    {
        if (textComponent == null) return;

        // Cancel any existing animation
        LeanTween.cancel(textComponent.gameObject);

        // Scale up then back down
        textComponent.transform.localScale = Vector3.one;
        LeanTween.scale(textComponent.gameObject, Vector3.one * punchScale, animationDuration * 0.5f)
            .setEaseOutQuad()
            .setOnComplete(() =>
            {
                LeanTween.scale(textComponent.gameObject, Vector3.one, animationDuration * 0.5f)
                    .setEaseInQuad();
            });
    }
}