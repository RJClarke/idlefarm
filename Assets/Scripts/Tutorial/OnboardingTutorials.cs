using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// The authored onboarding. Story lives in letters (LetterCatalog); these spotlight steps and
/// one-time tips carry the "what do I tap / how does this work" instructions.
///
/// First session:
///   name farm → [mailbox] → read letter, close inbox → [Field] → seed picker [pick + Save]
///   → [Start Run] → run starts → [money vs coins note]
/// Then one-time how-to tips on first visit/open of each building or concept (tip_* ids).
///
/// Each step is its own small sequence so a detour (cancelling the seed picker, quitting) only
/// replays the step the player was on. First-session steps chain two ways: game code calls the
/// On* hooks, and OnSequenceCompleted picks up the next step when the hook fired while the
/// previous step's overlay was still up (a spotlighted press and its click land in one frame).
///
/// All text comes from LetterCatalog.tips (edit copy there — see docs/narrative/cast-and-copy.md),
/// falling back to NarrativeDefaults. Only for players who named their farm after this shipped
/// (NewPlayerFlag), so existing saves never get a surprise tutorial; Dev Tools "Replay Onboarding"
/// opts a save back in. TutorialManager.TryStart is a no-op for completed ids.
/// </summary>
public static class OnboardingTutorials
{
    /// <summary>Narrative flag set at first-run naming; gates every onboarding step and tip.</summary>
    public const string NewPlayerFlag = "onboarding_tutorial_v1";

    public const string MailboxIntroId = "onboarding_mailbox";
    public const string FieldIntroId = "onboarding_field";
    public const string PickSeedsId = "onboarding_pick_seeds";
    public const string StartRunId = "onboarding_start_run";
    public const string MoneyNoteId = "onboarding_money_note";

    private const string SeedsSavedEvent = "onboarding_seeds_saved";

    /// <summary>How-to tips over an open menu dim less, so the menu stays readable behind the card.</summary>
    private const float MenuTipDim = 0.45f;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Init()
    {
        // -= first: survives Enter Play Mode without a domain reload.
        TutorialManager.OnSequenceCompleted -= OnSequenceCompleted;
        TutorialManager.OnSequenceCompleted += OnSequenceCompleted;
    }

    public static bool IsNewPlayer =>
        NarrativeManager.Instance != null && NarrativeManager.Instance.HasFired(NewPlayerFlag);

    private static bool Done(string id) => TutorialManager.IsCompleted(id);

    /// <summary>Live copy for a tip/step id: the catalog asset first, then the code default.</summary>
    public static string Text(string id)
    {
        TipDef tip = InboxManager.Instance?.Catalog?.GetTip(id);
        if (tip != null && !string.IsNullOrWhiteSpace(tip.text)) return tip.text;
        foreach (TipDef d in NarrativeDefaults.Tips) if (d.id == id) return d.text;
        return "";
    }

    /// <summary>Dev tools: opt this save into onboarding and replay every step, tip and onboarding letter.</summary>
    public static void DevReplayAll()
    {
        TutorialManager.DevResetAll();
        var nm = NarrativeManager.Instance;
        if (nm == null) return;
        nm.MarkFired(NewPlayerFlag);
        foreach (LetterDef l in NarrativeDefaults.Letters)
            if (l.newPlayersOnly) nm.ClearFired("letter:" + l.id);
        PlayerPrefs.SetInt("runs_completed", 0);
        PlayerPrefs.Save();
        Debug.Log("[Onboarding] Replay armed: tutorials, tips and onboarding letters will show again.");
    }

    // ── First-session hooks (called from game code) ──────────────

    /// <summary>FarmNamePopupUITK, first-run save: enrol the player and point at the letter.</summary>
    public static void OnFirstRunNamed()
    {
        NarrativeManager.Instance?.MarkFired(NewPlayerFlag);
        TryMailboxIntro();
    }

    /// <summary>InboxPopupUITK.Close: after reading the Mayor's letter, show where to plant.</summary>
    public static void OnInboxClosed()
    {
        if (IsNewPlayer && Done(MailboxIntroId)) TryFieldIntro();
    }

    /// <summary>SeedSelectionPopup.Show.</summary>
    public static void OnSeedPickerOpened()
    {
        if (IsNewPlayer && Done(FieldIntroId)) TryPickSeeds();
    }

    /// <summary>SeedSelectionPopup save. An empty save doesn't count — the step replays.</summary>
    public static void OnSeedsSaved(bool anyCropEquipped)
    {
        if (TutorialManager.ActiveId != PickSeedsId) return;
        if (anyCropEquipped) TutorialManager.Notify(SeedsSavedEvent);
        else TutorialManager.Abort();
    }

    /// <summary>SeedSelectionPopup cancel/close: drop the step so it replays on the next open.</summary>
    public static void OnSeedPickerCancelled()
    {
        if (TutorialManager.ActiveId == PickSeedsId) TutorialManager.Abort();
    }

    /// <summary>RunUI, after the player starts a run.</summary>
    public static void OnRunStarted()
    {
        if (!IsNewPlayer) return;
        if (Done(StartRunId)) TryMoneyNote();
        // After the Mayor's town-board letter, point out the Collect / Sell switch (requests need items).
        if (NarrativeManager.Instance != null && NarrativeManager.Instance.HasFired(ReputationManager.BoardIntroLetterFlag))
            TryCollectSellTip();
    }

    private static void OnSequenceCompleted(string id)
    {
        if (!IsNewPlayer) return;
        switch (id)
        {
            case FieldIntroId:
                if (SeedSelectionPopup.Instance != null && SeedSelectionPopup.Instance.IsOpen) TryPickSeeds();
                break;
            case PickSeedsId:
                TryStartRun();
                break;
            case StartRunId:
                if (RunManager.Instance != null && RunManager.Instance.IsRunActive) TryMoneyNote();
                break;
            case "tip_barn":
                if (BarnPopupUITK.Instance != null && BarnPopupUITK.Instance.IsOpen) TryBarnSpendTip();
                break;
        }
    }

    // ── How-to tip hooks (called from game code) ─────────────────

    /// <summary>A menu opened for the first time: tipId is the matching tip_* id.</summary>
    public static void OnMenuOpened(string tipId)
    {
        if (!IsNewPlayer) return;
        Tip(tipId, dim: MenuTipDim, atBottom: true);
    }

    /// <summary>Plant.StartRegrowth: a regrowing crop was just picked and is growing back.</summary>
    public static void OnFirstRegrow()
    {
        if (!IsNewPlayer) return;
        Tip("tip_regrow");
    }

    /// <summary>BarnPopupUITK.Open: with a point to spend, spotlight a "+" (after the Barn how-to).</summary>
    public static void OnBarnOpened()
    {
        if (!IsNewPlayer || !Done("tip_barn")) return; // the how-to card shows first; see OnSequenceCompleted
        TryBarnSpendTip();
    }

    private static void TryBarnSpendTip()
    {
        if (Done("tip_barn_spend")) return;
        if (ReputationManager.Instance == null || ReputationManager.Instance.UnspentPoints <= 0) return;
        VisualElement plus = BarnPopupUITK.Instance != null ? BarnPopupUITK.Instance.FirstSpendableButton : null;
        if (plus == null) return;
        Start("tip_barn_spend", new TutorialStep
        {
            text = Text("tip_barn_spend"),
            advance = TutorialAdvance.TapAnywhere,
            getTargetScreenRect = TutorialTargets.FromUITK(plus),
            dimOpacity = MenuTipDim,
        });
    }

    /// <summary>NarrativeDirector: the camera finished panning to a location.</summary>
    public static void OnArrived(CameraPanController.Location location)
    {
        if (!IsNewPlayer) return;
        switch (location)
        {
            case CameraPanController.Location.Market:
                Tip("tip_market");
                break;
            case CameraPanController.Location.Woods:
                bool hasAxe = WoodcuttingManager.Instance != null && WoodcuttingManager.Instance.HasAxe;
                Tip(hasAxe ? "tip_woods" : "tip_woods_no_axe");
                break;
            case CameraPanController.Location.Lake:
                if (FishingManager.Instance != null && FishingManager.Instance.HasPole) Tip("tip_lake");
                break;
        }
    }

    /// <summary>NarrativeDirector: a fish was banked.</summary>
    public static void OnFirstCatch()
    {
        if (IsNewPlayer) Tip("tip_first_catch");
    }

    /// <summary>NarrativeDirector: a field with a crop chosen has stayed bare for a while this run
    /// (one helper can't keep up). A hint only — helper priorities are unchanged, so tending the
    /// crops already growing still comes first.</summary>
    public static void OnFieldIdle()
    {
        if (IsNewPlayer) Tip("tip_idle_field");
    }

    /// <summary>CompostBay: compost was credited.</summary>
    public static void OnCompostEarned()
    {
        if (IsNewPlayer) Tip("tip_compost");
    }

    /// <summary>SeedInventory: a helper couldn't afford a seed bag during a run.</summary>
    public static void OnOutOfSeedMoney()
    {
        if (!IsNewPlayer || Done("tip_out_of_money") || TutorialManager.IsActive) return;
        SeedCounterHUD hud = Object.FindFirstObjectByType<SeedCounterHUD>();
        RectTransform bag = hud != null ? hud.FirstBagRect : null;
        Start("tip_out_of_money", new TutorialStep
        {
            text = Text("tip_out_of_money"),
            advance = TutorialAdvance.TapAnywhere,
            getTargetScreenRect = bag != null ? TutorialTargets.FromUGUI(bag) : null,
        });
    }

    private static void TryCollectSellTip()
    {
        if (Done("tip_collect_sell")) return;
        CollectModeToggle toggle = Object.FindFirstObjectByType<CollectModeToggle>();
        RectTransform rect = toggle != null ? toggle.TargetRect : null;
        Start("tip_collect_sell", new TutorialStep
        {
            text = Text("tip_collect_sell"),
            advance = TutorialAdvance.TapAnywhere,
            getTargetScreenRect = rect != null ? TutorialTargets.FromUGUI(rect) : null,
        });
    }

    /// <summary>A plain how-to card, dismissed with a tap: centered, or at the bottom of the screen
    /// when it explains an open menu (so it doesn't cover that menu).</summary>
    private static bool Tip(string id, float? dim = null, bool atBottom = false)
    {
        if (Done(id)) return false;
        return Start(id, new TutorialStep
        {
            text = Text(id),
            advance = TutorialAdvance.TapAnywhere,
            dimOpacity = dim,
            cardAtBottom = atBottom,
        });
    }

    // ── First-session steps ──────────────────────────────────────

    public static bool TryMailboxIntro()
    {
        InboxButton inbox = Object.FindFirstObjectByType<InboxButton>();
        if (inbox == null) return false;
        return Start(MailboxIntroId, new TutorialStep
        {
            text = Text(MailboxIntroId),
            advance = TutorialAdvance.TargetPressed,
            getTargetScreenRect = TutorialTargets.FromUGUI((RectTransform)inbox.transform),
        });
    }

    private static bool TryFieldIntro()
    {
        RunUI runUI = Object.FindFirstObjectByType<RunUI>();
        if (runUI == null || runUI.EquipFieldsButtonRect == null) return false;
        return Start(FieldIntroId, new TutorialStep
        {
            text = Text(FieldIntroId),
            advance = TutorialAdvance.TargetPressed,
            getTargetScreenRect = TutorialTargets.FromUGUI(runUI.EquipFieldsButtonRect),
        });
    }

    private static bool TryPickSeeds()
    {
        SeedSelectionPopup picker = SeedSelectionPopup.Instance;
        if (picker == null || picker.Frame == null) return false;
        var frameRect = TutorialTargets.FromUITK(picker.Frame);
        return Start(PickSeedsId, new TutorialStep
        {
            text = Text(PickSeedsId),
            advance = TutorialAdvance.GameEvent,
            eventId = SeedsSavedEvent,
            // Only while the picker is actually showing (its frame keeps a stale rect when hidden).
            getTargetScreenRect = () => picker.IsOpen && picker.Frame.resolvedStyle.display != DisplayStyle.None
                ? frameRect() : null,
            spotlightPadding = 4f,
        });
    }

    private static bool TryStartRun()
    {
        RunUI runUI = Object.FindFirstObjectByType<RunUI>();
        if (runUI == null || runUI.StartRunButtonRect == null) return false;
        return Start(StartRunId, new TutorialStep
        {
            text = Text(StartRunId),
            advance = TutorialAdvance.TargetPressed,
            getTargetScreenRect = TutorialTargets.FromUGUI(runUI.StartRunButtonRect),
        });
    }

    private static bool TryMoneyNote()
    {
        return Start(MoneyNoteId, new TutorialStep
        {
            text = Text(MoneyNoteId),
            advance = TutorialAdvance.TapAnywhere,
        });
    }

    private static bool Start(string id, TutorialStep step)
    {
        var seq = new TutorialSequence { id = id };
        seq.steps.Add(step);
        return TutorialManager.TryStart(seq);
    }
}
