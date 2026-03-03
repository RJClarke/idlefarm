using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Displays run information and handles end run button
/// </summary>
public class RunUI : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private TextMeshProUGUI runTimerText;
    [SerializeField] private Button startRunButton;
    [SerializeField] private Button endRunButton;

    [Header("Display Settings")]
    [SerializeField] private bool showTimer = true;

    private void Start()
    {
        // Hook up buttons if assigned
        if (startRunButton != null)
        {
            startRunButton.onClick.AddListener(OnStartRunButtonClicked);
        }

        if (endRunButton != null)
        {
            endRunButton.onClick.AddListener(OnEndRunButtonClicked);
        }

        // Update button visibility
        UpdateButtonStates();
    }

    private void Update()
    {
        // Update timer display every frame
        if (showTimer && runTimerText != null && RunManager.Instance != null)
        {
            if (RunManager.Instance.IsRunActive)
            {
                runTimerText.text = "Run Time: " + RunManager.Instance.GetFormattedRunDuration();
            }
            else
            {
                runTimerText.text = "No Active Run";
            }
        }

        // Update button states
        UpdateButtonStates();
    }

    private void UpdateButtonStates()
    {
        if (RunManager.Instance == null) return;

        // Show/hide buttons based on run state
        if (startRunButton != null)
        {
            startRunButton.gameObject.SetActive(!RunManager.Instance.IsRunActive);
        }

        if (endRunButton != null)
        {
            endRunButton.gameObject.SetActive(RunManager.Instance.IsRunActive);
        }
    }

    private void OnStartRunButtonClicked()
    {
        if (RunManager.Instance != null)
        {
            RunManager.Instance.StartNewRun();
        }
    }

    private void OnEndRunButtonClicked()
    {
        if (RunManager.Instance != null)
        {
            RunManager.Instance.EndRun();
        }
    }

    private void OnDestroy()
    {
        // Clean up button listeners
        if (startRunButton != null)
        {
            startRunButton.onClick.RemoveListener(OnStartRunButtonClicked);
        }

        if (endRunButton != null)
        {
            endRunButton.onClick.RemoveListener(OnEndRunButtonClicked);
        }
    }
}