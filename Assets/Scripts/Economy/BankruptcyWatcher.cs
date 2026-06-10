using UnityEngine;

/// <summary>
/// Ends the run when the player is out of seed money AND nothing is growing — i.e. no future
/// income can arrive. Checked on an interval (not every frame) while a run is active.
/// </summary>
public class BankruptcyWatcher : MonoBehaviour
{
    [Tooltip("Seconds between bankruptcy checks.")]
    [SerializeField] private float checkInterval = 2f;

    [Tooltip("Grace period after run start before bankruptcy can trigger.")]
    [SerializeField] private float startGrace = 8f;

    private float _timer;

    private void Update()
    {
        if (RunManager.Instance == null || !RunManager.Instance.IsRunActive) return;
        if (RunManager.Instance.CurrentRunDuration < startGrace) return;

        _timer += Time.unscaledDeltaTime;
        if (_timer < checkInterval) return;
        _timer = 0f;

        if (IsBankrupt())
            RunManager.Instance.EndRun(bankrupt: true);
    }

    /// <summary>
    /// Bankrupt = (a) no equipped crop can be planted (no seeds AND no affordable bag) AND
    /// (b) zero crops currently growing anywhere (no harvest income incoming).
    /// </summary>
    private bool IsBankrupt()
    {
        if (FarmGrid.Instance == null || HelperManager.Instance == null || SeedInventory.Instance == null)
            return false;

        // (b) anything still growing?
        foreach (int zoneId in FarmGrid.Instance.GetActiveZoneIds())
        {
            if (FarmGrid.Instance.GetOccupiedTilesInZone(zoneId).Count > 0)
                return false;
        }

        // (a) can we plant anything anywhere?
        foreach (int zoneId in FarmGrid.Instance.GetActiveZoneIds())
        {
            CropData crop = HelperManager.Instance.GetSeedForZone(zoneId);
            if (crop != null && SeedInventory.Instance.CanPlant(crop))
                return false;
        }

        return true; // nothing growing and nothing plantable -> done
    }
}
