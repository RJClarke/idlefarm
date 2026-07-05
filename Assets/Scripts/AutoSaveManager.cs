using UnityEngine;

/// <summary>
/// Debounced auto-save. Subscribes to "something durable changed" events across managers
/// and triggers SaveManager.SaveGame() after a quiet window. This is the crash-safety net
/// for mid-session: without it, only AppLifecycleSaver (pause/quit) writes to disk.
///
/// Notes:
/// - OnMoneyChanged IS subscribed because Money now persists across closes (active-run
///   resume needs it). Debounce keeps harvest cascades from spamming disk writes.
/// - The debounce coalesces bursts (research catch-up that levels up 30 times, harvest
///   cascades on run end, etc.) into a single disk write.
/// </summary>
[DefaultExecutionOrder(1500)]
public class AutoSaveManager : MonoBehaviour
{
    public static AutoSaveManager Instance { get; private set; }

    [Tooltip("Quiet-period required after the last triggering event before SaveGame fires.")]
    [SerializeField] private float debounceSecs = 2f;

    [Tooltip("Hard ceiling on how long a pending save may be deferred by the trailing debounce. " +
             "If a save has been pending this long since the last actual save, it fires immediately " +
             "so a busy run (constant events pushing the deadline out) can't starve saves forever.")]
    [SerializeField] private float maxIntervalSecs = 30f;

    private float pendingDeadline = -1f;
    private float lastSaveTime = -1f; // realtimeSinceStartup of the last actual save (or subscription time for the first)
    private bool subscribed;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    private void Start()    => TrySubscribe();
    private void OnEnable() => TrySubscribe();
    private void OnDisable() => Unsubscribe();

    private void Update()
    {
        if (pendingDeadline < 0f) return;
        float now = Time.realtimeSinceStartup;
        bool debounceReady = now >= pendingDeadline;
        // Max-interval backstop: a save that has been pending past maxIntervalSecs since the last
        // actual save fires now, even if events keep arriving (which would otherwise keep pushing
        // the debounce deadline out and starve the save during a busy run).
        bool maxIntervalReached = lastSaveTime >= 0f && (now - lastSaveTime) >= maxIntervalSecs;
        if (!debounceReady && !maxIntervalReached) return;
        pendingDeadline = -1f;
        if (SaveManager.Instance == null) return;
        try { SaveManager.Instance.SaveGame(); lastSaveTime = now; }
        catch (System.Exception e) { Debug.LogError($"[AutoSaveManager] SaveGame failed: {e.Message}"); }
    }

    private void Request() => pendingDeadline = Time.realtimeSinceStartup + debounceSecs;

    private void TrySubscribe()
    {
        if (subscribed) return;

        // CurrencyManager is the anchor subscription (Money/Coins drive most saves). If it
        // isn't up yet, don't latch `subscribed` — leave the flag clear so a later
        // Start/OnEnable retry can wire everything instead of silently never auto-saving.
        if (CurrencyManager.Instance == null) return;

        CurrencyManager.Instance.OnMoneyChanged   += OnInt;
        CurrencyManager.Instance.OnCoinsChanged   += OnInt;
        CurrencyManager.Instance.OnGemsChanged    += OnInt;
        CurrencyManager.Instance.OnCompostChanged += OnInt;
        if (UpgradeManager.Instance != null)
            UpgradeManager.Instance.OnUpgradePurchased += OnString;

        if (ResearchManager.Instance != null)
        {
            ResearchManager.Instance.OnSlotUnlocked       += OnInt;
            ResearchManager.Instance.OnSlotStateChanged   += OnInt;
            ResearchManager.Instance.OnResearchLeveledUp  += OnLeveledUp;
            ResearchManager.Instance.OnFeatureFlagUnlocked += OnString;
        }
        if (RunManager.Instance != null)
        {
            RunManager.Instance.OnRunStarted += OnVoid; // capture runActive flag in save
            RunManager.Instance.OnRunEnded   += OnVoid;
        }

        if (AnimalManager.Instance != null)
        {
            AnimalManager.Instance.OnAnimalEquipped   += OnAnimal;
            AnimalManager.Instance.OnAnimalUnequipped += OnVoid;
            AnimalManager.Instance.OnEggClaimed       += OnVoid;
            AnimalManager.Instance.OnGemClaimed       += OnVoid;
            AnimalManager.Instance.OnAnimalUnlocked   += OnString;
        }
        if (QuestManager.Instance != null)
        {
            QuestManager.Instance.OnQuestCompleted  += OnVoid;
            QuestManager.Instance.OnQuestsDropped   += OnVoid;
            QuestManager.Instance.OnMilestoneClaimed += OnVoid;
        }

        // First save of the session counts its max-interval from subscription time.
        lastSaveTime = Time.realtimeSinceStartup;
        subscribed = true;
    }

    private void Unsubscribe()
    {
        if (!subscribed) return;

        if (CurrencyManager.Instance != null)
        {
            CurrencyManager.Instance.OnMoneyChanged   -= OnInt;
            CurrencyManager.Instance.OnCoinsChanged   -= OnInt;
            CurrencyManager.Instance.OnGemsChanged    -= OnInt;
            CurrencyManager.Instance.OnCompostChanged -= OnInt;
        }
        if (UpgradeManager.Instance != null)
            UpgradeManager.Instance.OnUpgradePurchased -= OnString;

        if (ResearchManager.Instance != null)
        {
            ResearchManager.Instance.OnSlotUnlocked       -= OnInt;
            ResearchManager.Instance.OnSlotStateChanged   -= OnInt;
            ResearchManager.Instance.OnResearchLeveledUp  -= OnLeveledUp;
            ResearchManager.Instance.OnFeatureFlagUnlocked -= OnString;
        }
        if (RunManager.Instance != null)
        {
            RunManager.Instance.OnRunStarted -= OnVoid;
            RunManager.Instance.OnRunEnded   -= OnVoid;
        }

        if (AnimalManager.Instance != null)
        {
            AnimalManager.Instance.OnAnimalEquipped   -= OnAnimal;
            AnimalManager.Instance.OnAnimalUnequipped -= OnVoid;
            AnimalManager.Instance.OnEggClaimed       -= OnVoid;
            AnimalManager.Instance.OnGemClaimed       -= OnVoid;
            AnimalManager.Instance.OnAnimalUnlocked   -= OnString;
        }
        if (QuestManager.Instance != null)
        {
            QuestManager.Instance.OnQuestCompleted  -= OnVoid;
            QuestManager.Instance.OnQuestsDropped   -= OnVoid;
            QuestManager.Instance.OnMilestoneClaimed -= OnVoid;
        }

        subscribed = false;
    }

    // Event adapters — each just bumps the debounce deadline.
    private void OnInt(int _) => Request();
    private void OnString(string _) => Request();
    private void OnLeveledUp(string _, int __) => Request();
    private void OnVoid() => Request();
    private void OnAnimal(AnimalData _) => Request();
}
