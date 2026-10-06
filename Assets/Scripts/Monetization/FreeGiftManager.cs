using System;
using UnityEngine;

/// <summary>
/// The town's Free Gift chest (spec §3). Unlocks when the Welcome Basket is delivered; then one chest
/// every 30 min, max 10 a day. The day's first chest is free, others need a rewarded ad unless the player
/// owns the Farmer's Pass. Rewards: 10 gems + Coins by Overall Farm Level. Grants at chest open (or on
/// backgrounding), then saves. Self-bootstrapping per scene, like the Almanac.
/// </summary>
public sealed class FreeGiftManager : MonoBehaviour
{
    public const string IntroTipId = "tip_town_gift";
    public const string LetterId = "town_gift";
    public const string UnlockEvent = "welcome_basket_done";
    private const string NoFillToast = "No gift right now. Try again soon.";

    public static FreeGiftManager Instance { get; private set; }

    /// <summary>Whether the Farmer's Pass is owned. StoreManager sets this; false until then.</summary>
    public static Func<bool> PassOwned = () => false;

    public event Action OnStateChanged;

    private FreeGiftCore core;
    private bool busy;          // an ad or chest for this claim is in flight
    private bool pendingPitch;  // show the pass pitch when the chest closes
    private bool subscribed;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        UnityEngine.SceneManagement.SceneManager.sceneLoaded -= OnSceneLoaded;
        UnityEngine.SceneManagement.SceneManager.sceneLoaded += OnSceneLoaded;
        EnsureInstance();
    }

    private static void OnSceneLoaded(UnityEngine.SceneManagement.Scene s, UnityEngine.SceneManagement.LoadSceneMode m) => EnsureInstance();

    // Only scenes with a save (FarmMain): sceneLoaded runs after Awake, before Start, so this exists
    // before CurrencyManager.Start triggers SaveManager.LoadGame.
    private static void EnsureInstance()
    {
        if (Instance != null || SaveManager.Instance == null) return;
        new GameObject("FreeGiftManager").AddComponent<FreeGiftManager>();
    }

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        core = new FreeGiftCore(FreeGiftTuning.Instance.ToRules());
    }

    private void Start() => TrySubscribe();

    private void OnDestroy()
    {
        if (subscribed && ReputationManager.Instance != null) ReputationManager.Instance.OnRequestFulfilled -= NotifyChanged;
        if (Instance == this) Instance = null;
    }

    private void TrySubscribe()
    {
        if (subscribed || ReputationManager.Instance == null) return;
        ReputationManager.Instance.OnRequestFulfilled += NotifyChanged;
        subscribed = true;
    }

    /// <summary>Re-evaluate listeners (unlock, pass bought, load).</summary>
    public void NotifyChanged() => OnStateChanged?.Invoke();

    // ── State ────────────────────────────────────────────────────

    private static long NowUtc => DateTime.UtcNow.Ticks;
    private static string Today => FreeGiftCore.LocalDateKey(DateTime.Now);

    public bool IsUnlocked
    {
        get
        {
            NarrativeManager nm = NarrativeManager.Instance;
            ReputationManager rm = ReputationManager.Instance;
            return (nm != null && nm.HasFired(ReputationManager.WelcomeBasketFlag)) || (rm != null && rm.PointsEarned >= 1);
        }
    }

    public GiftStatus Status
    {
        get
        {
            long now = NowUtc;
            core.HealClock(now);
            return core.Status(IsUnlocked, now, Today);
        }
    }

    public double SecondsUntilReady => core.SecondsUntilReady(NowUtc);
    public bool NextClaimNeedsAd => !PassOwned() && !core.IsFreeChest(Today);
    public int GemsPerClaim => core.Rules.gemsPerClaim;
    public int CoinReward => CoinsAtLevel(ReputationManager.Instance != null ? ReputationManager.Instance.PointsEarned : 0);
    public static int CoinsAtLevel(int level) => FreeGiftCore.CoinsForLevel(FreeGiftTuning.Instance.coinAnchors, level);

    // ── Claim flow ───────────────────────────────────────────────

    /// <summary>HUD button / Store card. Free chest or pass: straight to the chest. Otherwise an ad first.</summary>
    public void RequestClaim()
    {
        TrySubscribe();
        if (busy || ChestRevealUITK.IsShowing || Status != GiftStatus.Ready) return;
        TutorialManager.CompleteIfActive(IntroTipId); // its spotlight would otherwise sit over the chest
        int gems = GemsPerClaim, coins = CoinReward;

        if (!NextClaimNeedsAd) { OpenChest(viaAd: false, gems, coins); return; }

        busy = true;
        MonetizationServices.Ads.ShowRewarded(
            onRewarded: () => OpenChest(viaAd: true, gems, coins),
            onFailed: reason =>
            {
                busy = false;
                if (reason == AdFailReason.NotReady || reason == AdFailReason.Error)
                    ToastManager.Show(NoFillToast, null, ToastManager.ToastKind.Success, MonetizationUI.ChestIcon);
                OnStateChanged?.Invoke();
            });
    }

    private void OpenChest(bool viaAd, int gems, int coins)
    {
        busy = true;
        var request = new ChestRevealRequest();
        request.lines.Add(new RewardLine(RewardCurrency.Gems, gems));
        if (coins > 0) request.lines.Add(new RewardLine(RewardCurrency.Coins, coins));
        request.onOpened = () => Grant(viaAd, gems, coins);
        request.onClosed = () =>
        {
            busy = false;
            OnStateChanged?.Invoke();
            if (pendingPitch) { pendingPitch = false; PassPitchUITK.Show(); }
        };
        ChestRevealUITK.Show(request);
    }

    private void Grant(bool viaAd, int gems, int coins)
    {
        long now = NowUtc;
        core.HealClock(now);
        CurrencyManager cm = CurrencyManager.Instance;
        if (cm != null) { cm.AddGems(gems); if (coins > 0) cm.AddCoins(coins); }
        if (core.RecordClaim(now, Today, viaAd)) pendingPitch = true;
        string via = viaAd ? "ad" : (PassOwned() ? "pass" : "free");
        Debug.Log($"[FreeGift] Claimed +{gems} gems, +{coins} coins ({via}); today {core.ClaimsToday(Today)}/{core.Rules.dailyCap}");
        SaveManager.Instance?.SaveGame();
        OnStateChanged?.Invoke();
    }

    // ── Intro (letter -> tooltip on the button) ─────────────────

    /// <summary>Spotlights the HUD Gift button once: after the Mayor's thank-you letter is read, or right
    /// away for saves that unlocked before this shipped (they never get the letter).</summary>
    /// <returns>True once the intro is done or just started (callers stop retrying).</returns>
    public bool TryShowIntro()
    {
        if (TutorialManager.IsCompleted(IntroTipId)) return true;
        if (!IsUnlocked || TutorialManager.IsActive || LetterUnread()) return false;
        if (InboxPopupUITK.Instance != null && InboxPopupUITK.Instance.IsOpen) return false; // wait for the inbox to close
        if (OfflineProgressModalUITK.Instance != null && OfflineProgressModalUITK.Instance.IsOpen) return false; // and "Welcome back"
        FreeGiftButton button = FreeGiftButton.Instance;
        if (button == null || !button.IsShown) return false;
        var seq = new TutorialSequence { id = IntroTipId };
        seq.steps.Add(new TutorialStep
        {
            text = OnboardingTutorials.Text(IntroTipId),
            advance = TutorialAdvance.TargetPressed,
            getTargetScreenRect = TutorialTargets.FromUGUI(button.Rect),
        });
        return TutorialManager.TryStart(seq);
    }

    private static bool LetterUnread()
    {
        InboxManager inbox = InboxManager.Instance;
        if (inbox == null) return false;
        foreach (InboxEntry e in inbox.Entries)
            if (e != null && e.letterId == LetterId && !e.read) return true;
        return false;
    }

    // ── Save ─────────────────────────────────────────────────────

    public void CaptureTo(GameData d)
    {
        d.giftLastClaimUtcTicks = core.LastClaimUtcTicks;
        d.giftClaimsTodayDate = core.ClaimsDate;
        d.giftClaimsToday = core.ClaimsOnSavedDate;
        d.giftLifetimeAdClaims = core.LifetimeAdClaims;
        d.passPitchShown = core.PitchShown;
    }

    public void LoadFrom(GameData d)
    {
        core.Import(d.giftLastClaimUtcTicks, d.giftClaimsTodayDate, d.giftClaimsToday, d.giftLifetimeAdClaims, d.passPitchShown);
        core.HealClock(NowUtc);
        OnStateChanged?.Invoke();
    }

    // ── Dev ──────────────────────────────────────────────────────

    public void DevResetGift()
    {
        core.Import(0, "", 0, 0, false);
        SaveManager.Instance?.SaveGame();
        OnStateChanged?.Invoke();
    }

    public void DevMakeReady()
    {
        core.Import(0, core.ClaimsDate, core.ClaimsOnSavedDate, core.LifetimeAdClaims, core.PitchShown);
        OnStateChanged?.Invoke();
    }

    /// <summary>The pitch was shown (or the pass bought): never pitch again.</summary>
    public void MarkPitchShown()
    {
        core.MarkPitchShown();
        pendingPitch = false;
    }
}
