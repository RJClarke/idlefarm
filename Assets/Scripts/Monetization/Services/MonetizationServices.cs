using UnityEngine;

/// <summary>
/// Picks the ad and store services once per play session.
///   Device build with the real SDK define -> real adapter (phase 2).
///   Editor / Development build             -> fakes (dev overlay; no real money, ever).
///   Release build without the SDK          -> Unavailable* (never grants anything).
/// </summary>
public static class MonetizationServices
{
    private static IAdService ads;
    private static IStoreService store;

    public static IAdService Ads => ads ??= CreateAds();
    public static IStoreService Store => store ??= CreateStore();

    /// <summary>True when purchases go to the fake test store (shown as a footnote in the Store).</summary>
    public static bool IsTestStore
    {
        get
        {
#if UNITY_PURCHASING && !UNITY_EDITOR
            return false;
#elif UNITY_EDITOR || DEVELOPMENT_BUILD
            return true;
#else
            return false;
#endif
        }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() { ads = null; store = null; }

    private static IAdService CreateAds()
    {
#if LEVELPLAY && !UNITY_EDITOR
        return new LevelPlayAdService();
#elif UNITY_EDITOR || DEVELOPMENT_BUILD
        return new FakeAdService();
#else
        return new UnavailableAdService();
#endif
    }

    private static IStoreService CreateStore()
    {
#if UNITY_PURCHASING && !UNITY_EDITOR
        return new UnityIapStoreService();
#elif UNITY_EDITOR || DEVELOPMENT_BUILD
        return new FakeStoreService();
#else
        return new UnavailableStoreService();
#endif
    }
}
