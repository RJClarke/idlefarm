#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;

/// <summary>Editor/Dev stand-in for rewarded ads: a 3-second "Test Ad" overlay with Close early.</summary>
public sealed class FakeAdService : IAdService
{
    /// <summary>Settings > Dev toggle: pretend the ad network has nothing to show.</summary>
    public static bool SimulateNoFill;

    public bool IsReady => !SimulateNoFill;

    public void ShowRewarded(Action onRewarded, Action<AdFailReason> onFailed)
    {
        if (SimulateNoFill) { onFailed?.Invoke(AdFailReason.NotReady); return; }
        MonetizationDevOverlay.ShowAd(completed =>
        {
            if (completed) onRewarded?.Invoke();
            else onFailed?.Invoke(AdFailReason.Closed);
        });
    }
}
#endif
