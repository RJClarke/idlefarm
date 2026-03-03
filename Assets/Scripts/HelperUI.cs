using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Displays helper information in UI
/// Phase 5.1: Shows helper counts and states
/// </summary>
public class HelperUI : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private TextMeshProUGUI helperInfoText;
    [SerializeField] private Button spawnUniversalHelperButton;

    [Header("Update Settings")]
    [SerializeField] private float updateInterval = 0.5f;
    private float updateTimer = 0f;

    private void Start()
    {
        if (spawnUniversalHelperButton != null)
        {
            spawnUniversalHelperButton.onClick.AddListener(OnSpawnUniversalHelperClicked);
        }

        UpdateDisplay();
    }

    private void Update()
    {
        updateTimer += Time.deltaTime;
        if (updateTimer >= updateInterval)
        {
            updateTimer = 0f;
            UpdateDisplay();
        }
    }

    private void UpdateDisplay()
    {
        if (helperInfoText == null || HelperManager.Instance == null)
        {
            return;
        }

        int totalHelpers = HelperManager.Instance.GetHelperCount();
        int idleHelpers = HelperManager.Instance.GetIdleHelperCount();
        int workingHelpers = totalHelpers - idleHelpers;
        int pendingTasks = HelperManager.Instance.GetPendingTaskCount();

        string info = "<b>HELPERS</b>\n";
        info += $"🤖 Total: {totalHelpers}\n";
        
        if (totalHelpers > 0)
        {
            info += $"💤 Idle: {idleHelpers}\n";
            info += $"⚙️ Working: {workingHelpers}\n";
        }
        
        if (pendingTasks > 0)
        {
            info += $"📋 Tasks: {pendingTasks}\n";
        }

        helperInfoText.text = info;
    }

    private void OnSpawnUniversalHelperClicked()
    {
        if (HelperManager.Instance != null)
        {
            HelperManager.Instance.SpawnUniversalHelper();
        }
    }

    private void OnDestroy()
    {
        if (spawnUniversalHelperButton != null)
        {
            spawnUniversalHelperButton.onClick.RemoveListener(OnSpawnUniversalHelperClicked);
        }
    }
}