using UnityEngine;

/// <summary>Code-side wiring of the hybrid model: listens to existing game events and,
/// for any catalog letter whose trigger matches, delivers it exactly once (guarded by
/// the NarrativeManager ledger). Letter *content* is data; this is the *condition* logic.
///
/// Event ids (LetterDef.triggerEvent) raised here:
///   run_ended:N · run_survived:1h / 3h · tree_felled · axe_bought · pole_bought · fish_caught ·
///   animal_unlocked · upgrade:&lt;upgradeId&gt; · built:&lt;BuildingState key&gt; · town_request_done ·
///   welcome_basket_done (raised by ReputationManager: the Free Gift chest unlocks)
/// Research unlocks still use LetterDef.triggerFeatureFlag, animals triggerAnimalId.
/// Also forwards camera arrivals and first catches to OnboardingTutorials for how-to tips.</summary>
[DefaultExecutionOrder(1200)] // after NarrativeManager/InboxManager (1100)
public class NarrativeDirector : MonoBehaviour
{
    public static NarrativeDirector Instance { get; private set; }

    private bool subscribed;
    private CameraPanController pan;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(this); return; }
        Instance = this;
    }

    // Subscribe in Start (every Awake has run by then) — several of these managers use late
    // execution orders, and subscribing in OnEnable silently missed them before.
    private void Start() => Subscribe();

    // ── Idle-field hint ──────────────────────────────────────────
    // A field the player chose a crop for but that's still completely bare after this much farm
    // time means the helpers can't reach it (e.g. one helper, a second field of fast crops).
    private const float IdleFieldAfterSeconds = 300f; // 5 min: long enough that a helper merely running late never triggers it
    private float nextIdleCheck;

    private void Update()
    {
        if (Time.unscaledTime < nextIdleCheck) return;
        nextIdleCheck = Time.unscaledTime + 5f;
        if (!OnboardingTutorials.IsNewPlayer || TutorialManager.IsCompleted("tip_idle_field")) return;
        var rm = RunManager.Instance;
        if (rm == null || !rm.IsRunActive || rm.CurrentRunDuration < IdleFieldAfterSeconds) return;
        if (HelperManager.Instance == null || FarmGrid.Instance == null) return;

        for (int zone = 2; zone <= 4; zone++)
        {
            if (HelperManager.Instance.GetSeedForZone(zone) == null) continue; // left empty on purpose
            bool bare = true;
            foreach (SoilTile t in FarmGrid.Instance.GetZoneTiles(zone))
                if (t != null && (t.State == TileState.Tilled || t.CurrentPlant != null)) { bare = false; break; }
            if (bare) { OnboardingTutorials.OnFieldIdle(); return; }
        }
    }

    private void OnDestroy()
    {
        Unsubscribe();
        if (Instance == this) Instance = null;
    }

    /// <summary>Deliver every catalog letter triggered by <paramref name="eventId"/>.</summary>
    public static void Raise(string eventId)
    {
        var catalog = InboxManager.Instance?.Catalog;
        if (catalog == null || string.IsNullOrEmpty(eventId)) return;
        foreach (var def in catalog.ByEvent(eventId)) TryFire(def);
    }

    private void Subscribe()
    {
        if (subscribed) return;
        subscribed = true;
        if (ResearchManager.Instance != null) ResearchManager.Instance.OnFeatureFlagUnlocked += OnFeatureFlagUnlocked;
        if (AnimalManager.Instance != null) AnimalManager.Instance.OnAnimalUnlocked += OnAnimalUnlocked;
        if (RunManager.Instance != null) RunManager.Instance.OnRunEnded += OnRunEnded;
        if (WoodcuttingManager.Instance != null)
        {
            WoodcuttingManager.Instance.OnTreeFelled += OnTreeFelled;
            WoodcuttingManager.Instance.OnAxeLevelChanged += OnAxeLevelChanged;
        }
        if (FishingManager.Instance != null)
        {
            FishingManager.Instance.OnCatch += OnCatch;
            FishingManager.Instance.OnPoleLevelChanged += OnPoleLevelChanged;
        }
        if (UpgradeManager.Instance != null) UpgradeManager.Instance.OnUpgradePurchased += OnUpgradePurchased;
        if (ReputationManager.Instance != null) ReputationManager.Instance.OnRequestFulfilled += OnRequestFulfilled;
        BuildingState.OnBuildingBuilt += OnBuildingBuilt;

        pan = FindFirstObjectByType<CameraPanController>();
        if (pan != null) pan.OnPanCompleted += OnPanCompleted;
    }

    private void Unsubscribe()
    {
        if (!subscribed) return;
        subscribed = false;
        if (ResearchManager.Instance != null) ResearchManager.Instance.OnFeatureFlagUnlocked -= OnFeatureFlagUnlocked;
        if (AnimalManager.Instance != null) AnimalManager.Instance.OnAnimalUnlocked -= OnAnimalUnlocked;
        if (RunManager.Instance != null) RunManager.Instance.OnRunEnded -= OnRunEnded;
        if (WoodcuttingManager.Instance != null)
        {
            WoodcuttingManager.Instance.OnTreeFelled -= OnTreeFelled;
            WoodcuttingManager.Instance.OnAxeLevelChanged -= OnAxeLevelChanged;
        }
        if (FishingManager.Instance != null)
        {
            FishingManager.Instance.OnCatch -= OnCatch;
            FishingManager.Instance.OnPoleLevelChanged -= OnPoleLevelChanged;
        }
        if (UpgradeManager.Instance != null) UpgradeManager.Instance.OnUpgradePurchased -= OnUpgradePurchased;
        if (ReputationManager.Instance != null) ReputationManager.Instance.OnRequestFulfilled -= OnRequestFulfilled;
        BuildingState.OnBuildingBuilt -= OnBuildingBuilt;
        if (pan != null) pan.OnPanCompleted -= OnPanCompleted;
    }

    // ── Event → letter ───────────────────────────────────────────

    private void OnFeatureFlagUnlocked(string featureId)
    {
        var catalog = InboxManager.Instance?.Catalog;
        if (catalog == null) return;
        foreach (var def in catalog.ByFeatureFlag(featureId)) TryFire(def);
    }

    private void OnAnimalUnlocked(string animalId)
    {
        var catalog = InboxManager.Instance?.Catalog;
        if (catalog != null)
            foreach (var def in catalog.ByAnimalId(animalId)) TryFire(def);
        Raise("animal_unlocked");
    }

    private void OnRunEnded()
    {
        var rm = RunManager.Instance;
        if (rm == null) return;
        Raise("run_ended:" + rm.RunsCompleted);
        if (rm.LastRunSurvivedSeconds >= 3600) Raise("run_survived:1h");
        if (rm.LastRunSurvivedSeconds >= 3 * 3600) Raise("run_survived:3h");
    }

    private void OnTreeFelled() => Raise("tree_felled");
    // The first purchase reports level 0 (it flips HasAxe/HasPole), so gate on ownership, not level.
    // These also fire on save load — harmless, TryFire's ledger makes each letter one-shot.
    private void OnAxeLevelChanged(int level)
    {
        if (WoodcuttingManager.Instance != null && WoodcuttingManager.Instance.HasAxe) Raise("axe_bought");
    }

    private void OnPoleLevelChanged(int level)
    {
        if (FishingManager.Instance != null && FishingManager.Instance.HasPole) Raise("pole_bought");
    }
    private void OnUpgradePurchased(string upgradeId) => Raise("upgrade:" + upgradeId);
    private void OnRequestFulfilled() => Raise("town_request_done");
    private void OnBuildingBuilt(string key) => Raise("built:" + key);

    private void OnCatch(int tier)
    {
        Raise("fish_caught");
        OnboardingTutorials.OnFirstCatch();
    }

    private void OnPanCompleted(CameraPanController.Location location) => OnboardingTutorials.OnArrived(location);

    private static void TryFire(LetterDef def)
    {
        if (def == null || string.IsNullOrEmpty(def.id)) return;
        if (NarrativeManager.Instance == null || InboxManager.Instance == null) return;
        if (def.newPlayersOnly && !OnboardingTutorials.IsNewPlayer) return;

        string flag = "letter:" + def.id;
        if (NarrativeManager.Instance.HasFired(flag)) return;

        InboxManager.Instance.Deliver(def.id);
        NarrativeManager.Instance.MarkFired(flag);
    }
}
