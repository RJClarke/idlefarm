using UnityEngine;

/// <summary>Map nav button for the Market. Hidden while a run is active (the Market trip is disruptive mid-run).</summary>
public class MarketNavButton : MapNavButton
{
    protected override CameraPanController.Location Target => CameraPanController.Location.Market;

    protected override bool ShouldHide(CameraPanController.Location current)
        => RunManager.Instance != null && RunManager.Instance.IsRunActive;

    protected override void SubscribeExtra()
    {
        if (RunManager.Instance != null)
        {
            RunManager.Instance.OnRunStarted += OnRunChanged;
            RunManager.Instance.OnRunEnded   += OnRunChanged;
        }
    }

    protected override void UnsubscribeExtra()
    {
        if (RunManager.Instance != null)
        {
            RunManager.Instance.OnRunStarted -= OnRunChanged;
            RunManager.Instance.OnRunEnded   -= OnRunChanged;
        }
    }

    private void OnRunChanged() => RefreshVisibility();
}
