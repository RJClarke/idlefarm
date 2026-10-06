#if LEVELPLAY
using System;

/// <summary>
/// PHASE 2 STUB, compiled only when the LEVELPLAY define is set (after installing the Ads Mediation
/// package). Implement with the unity:levelplay-unity-integration skill:
///   - initialize LevelPlay with the app key, then load a rewarded ad unit;
///   - IsReady = the rewarded unit's loaded state;
///   - ShowRewarded: call onRewarded from the "ad rewarded" callback (NOT on close),
///     onFailed(Closed) when it closes without a reward, onFailed(NotReady) when nothing is loaded;
///   - reload the next ad after each show.
/// Until then it behaves like UnavailableAdService.
/// </summary>
public sealed class LevelPlayAdService : IAdService
{
    public bool IsReady => false;
    public void ShowRewarded(Action onRewarded, Action<AdFailReason> onFailed) => onFailed?.Invoke(AdFailReason.NotReady);
}
#endif
