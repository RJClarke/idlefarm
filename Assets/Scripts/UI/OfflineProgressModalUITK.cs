using System;
using UnityEngine;
using UnityEngine.UIElements;

[RequireComponent(typeof(UIDocument))]
[DefaultExecutionOrder(1200)]
public class OfflineProgressModalUITK : MonoBehaviour
{
    public static OfflineProgressModalUITK Instance { get; private set; }

    private const float LoadDurationSecs = 1.5f;

    private UIDocument document;
    private VisualElement root;
    private VisualElement modalRoot;
    private Label timeAwayLabel;
    private Label cowCompostLabel;
    private Label boostSpendLabel;
    private Label netCompostLabel;
    private VisualElement researchSection;
    private Label boostSummaryLabel;
    private Button continueButton;
    private VisualElement loadingBarFill;
    private Label loadingLabel;
    private IVisualElementScheduledItem loadTicker;
    private double loadStartTimeSecs;

    // Snapshot of final values; counted up from 0 during the load animation.
    private int targetCowCompost;
    private int targetBoostSpend;
    private int targetNetCompost;
    private System.Collections.Generic.List<(Label label, int finalDelta, int finalAfter)> researchTargets
        = new System.Collections.Generic.List<(Label, int, int)>();
    private string boostSummaryFinalText;

    // Outcome-variant elements (Run survived / ended) + the shared ledger breakdown.
    private VisualElement outcomeHero, breakdown, legacySections;
    private ScrollView breakdownScroll;
    private Label modalTitle, heroLabel, heroHeadline, heroSub;
    private Button secondaryButton;
    private System.Action onSecondary;
    private System.Action mainAction;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        document = GetComponent<UIDocument>();
    }

    private void OnEnable() => Cache();

    private void Start() { if (root == null) Cache(); }

    private void Cache()
    {
        if (document == null) document = GetComponent<UIDocument>();
        root = document != null ? document.rootVisualElement : null;
        if (root == null) return;
        root.pickingMode = PickingMode.Ignore;

        modalRoot         = root.Q<VisualElement>("modal-root");
        timeAwayLabel     = root.Q<Label>("time-away");
        cowCompostLabel   = root.Q<Label>("cow-compost");
        boostSpendLabel   = root.Q<Label>("boost-spend");
        netCompostLabel   = root.Q<Label>("net-compost");
        researchSection   = root.Q<VisualElement>("research-section");
        boostSummaryLabel = root.Q<Label>("boost-summary");
        continueButton    = root.Q<Button>("continue-button");
        loadingBarFill    = root.Q<VisualElement>("loading-bar-fill");
        loadingLabel      = root.Q<Label>("loading-label");

        modalTitle        = root.Q<Label>("modal-title");
        outcomeHero       = root.Q<VisualElement>("outcome-hero");
        heroLabel         = root.Q<Label>("hero-label");
        heroHeadline      = root.Q<Label>("hero-headline");
        heroSub           = root.Q<Label>("hero-sub");
        breakdownScroll   = root.Q<ScrollView>("breakdown-scroll");
        breakdown         = root.Q<VisualElement>("breakdown");
        legacySections    = root.Q<VisualElement>("legacy-sections");
        secondaryButton   = root.Q<Button>("secondary-button");

        if (continueButton != null)
            continueButton.RegisterCallback<ClickEvent>(_ =>
            {
                if (!continueButton.enabledSelf) return;
                (mainAction ?? (System.Action)Close).Invoke();
            });
        if (secondaryButton != null)
            secondaryButton.RegisterCallback<ClickEvent>(_ => onSecondary?.Invoke());
    }

    private void SetLegacySectionsVisible(bool visible)
    {
        if (legacySections != null) legacySections.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
    }

    /// <summary>Run survived offline — green hero, light summary, "Continue the Run" CTA.</summary>
    public void OpenContinue(TimeSpan gap, RunLedgerData d, string farmAdvancedHms, string nowHms, System.Action onContinue)
    {
        if (root == null) Cache();
        if (modalRoot == null) return;
        PrepCommon(gap, "Welcome back! 👋");
        ShowHero("green", "Your run is still going",
            "Farm Time +" + farmAdvancedHms, "now " + nowHms + " · ran at max speed while away");
        RunStatsLedgerView.Build(breakdown, d, compact: true);
        if (breakdownScroll != null) breakdownScroll.style.display = DisplayStyle.Flex;

        SetCtas("▶  Continue the Run", mainGold: false, "See full breakdown",
            onMain: onContinue,
            onSecondaryAction: () => { Close(); RunStatsPopupUITK.Instance?.Show(d); });
        Reveal();
    }

    /// <summary>Run ended offline — red hero, breakdown, "View Full Run Stats" CTA.</summary>
    public void OpenEnded(TimeSpan gap, RunLedgerData d, System.Action onNewRun)
    {
        if (root == null) Cache();
        if (modalRoot == null) return;
        PrepCommon(gap, "Welcome back 👋");
        ShowHero("red", "💸 Your run ended while away",
            "Bankrupt at " + d.farmTimeHms, "ran out of seed money · final score " + d.farmTimeHms);
        RunStatsLedgerView.Build(breakdown, d, compact: true);
        if (breakdownScroll != null) breakdownScroll.style.display = DisplayStyle.Flex;

        SetCtas("📊  View Full Run Stats", mainGold: true, "Start a new run",
            onMain: () => { Close(); RunStatsPopupUITK.Instance?.Show(d); },
            onSecondaryAction: () => { Close(); onNewRun?.Invoke(); });
        Reveal();
    }

    private void PrepCommon(TimeSpan gap, string title)
    {
        if (modalTitle != null) modalTitle.text = title;
        if (timeAwayLabel != null) timeAwayLabel.text = $"You were away for {FormatGap(gap)}";
        SetLegacySectionsVisible(false); // hide cow/research/loading for the outcome variants
    }

    private void ShowHero(string variant, string label, string headline, string sub)
    {
        if (outcomeHero == null) return;
        outcomeHero.style.display = DisplayStyle.Flex;
        outcomeHero.RemoveFromClassList("hero--green");
        outcomeHero.RemoveFromClassList("hero--red");
        outcomeHero.AddToClassList(variant == "red" ? "hero--red" : "hero--green");
        if (heroLabel != null) heroLabel.text = label;
        if (heroHeadline != null) heroHeadline.text = headline;
        if (heroSub != null) heroSub.text = sub;
    }

    private void SetCtas(string mainText, bool mainGold, string secondaryText,
                         System.Action onMain, System.Action onSecondaryAction)
    {
        if (continueButton != null)
        {
            continueButton.text = mainText;
            continueButton.RemoveFromClassList("cta--gold");
            continueButton.RemoveFromClassList("cta--primary");
            continueButton.AddToClassList(mainGold ? "cta--gold" : "cta--primary");
            continueButton.SetEnabled(true);
        }
        mainAction = onMain;
        if (secondaryButton != null)
        {
            secondaryButton.text = secondaryText;
            secondaryButton.style.display = DisplayStyle.Flex;
        }
        onSecondary = onSecondaryAction;
    }

    private void Reveal()
    {
        if (root != null) root.pickingMode = PickingMode.Position;
        if (modalRoot != null) modalRoot.style.display = DisplayStyle.Flex;
    }

    public void Open(TimeSpan gap, int cowCompostGain, ResearchManager.OfflineCatchUpReport report)
    {
        if (root == null) Cache();
        if (modalRoot == null) return;

        // Legacy (no active run) layout: cow/research sections + load animation, no outcome hero/breakdown.
        if (modalTitle != null) modalTitle.text = "Welcome Back!";
        SetLegacySectionsVisible(true);
        if (outcomeHero != null) outcomeHero.style.display = DisplayStyle.None;
        if (breakdownScroll != null) breakdownScroll.style.display = DisplayStyle.None;
        if (secondaryButton != null) secondaryButton.style.display = DisplayStyle.None;
        mainAction = null; // continue button defaults to Close
        if (continueButton != null) { continueButton.text = "Continue"; continueButton.RemoveFromClassList("cta--gold"); continueButton.AddToClassList("cta--primary"); }

        if (timeAwayLabel  != null) timeAwayLabel.text  = $"You were away for {FormatGap(gap)}";

        // Capture target values; UI starts at zero and counts up over LoadDurationSecs.
        targetCowCompost = cowCompostGain;
        targetBoostSpend = report != null ? report.compostSpentOnAutoBuy : 0;
        targetNetCompost = targetCowCompost - targetBoostSpend;

        if (cowCompostLabel != null) cowCompostLabel.text = "+0";
        if (boostSpendLabel != null) boostSpendLabel.text = "−0";
        if (netCompostLabel != null)
        {
            netCompostLabel.text = targetNetCompost >= 0 ? "+0" : "0";
            netCompostLabel.ClearClassList();
            netCompostLabel.AddToClassList("stat-row__value");
            netCompostLabel.AddToClassList(targetNetCompost >= 0 ? "stat-row__value--positive" : "stat-row__value--negative");
        }

        BuildResearchRows(report);
        BuildBoostSummary(report);

        if (root != null) root.pickingMode = PickingMode.Position;
        modalRoot.style.display = DisplayStyle.Flex;

        StartLoadAnimation();
    }

    private void StartLoadAnimation()
    {
        if (continueButton != null) continueButton.SetEnabled(false);
        if (loadingBarFill != null) loadingBarFill.style.width = new StyleLength(new Length(0f, LengthUnit.Percent));
        if (loadingLabel != null)   loadingLabel.text = "Loading offline progress…";

        loadStartTimeSecs = Time.realtimeSinceStartupAsDouble;
        loadTicker?.Pause();
        loadTicker = root.schedule.Execute(TickLoadAnimation).Every(16); // ~60Hz
    }

    private void TickLoadAnimation()
    {
        double t = (Time.realtimeSinceStartupAsDouble - loadStartTimeSecs) / LoadDurationSecs;
        if (t >= 1.0) { ApplyLoadProgress(1f); FinishLoadAnimation(); return; }
        ApplyLoadProgress((float)t);
    }

    private void ApplyLoadProgress(float t)
    {
        // EaseOutQuad: punchier at the start, gentle at the end so the final values "land".
        float e = 1f - (1f - t) * (1f - t);

        if (loadingBarFill != null)
            loadingBarFill.style.width = new StyleLength(new Length(e * 100f, LengthUnit.Percent));

        int cow = Mathf.RoundToInt(Mathf.Lerp(0, targetCowCompost, e));
        int spend = Mathf.RoundToInt(Mathf.Lerp(0, targetBoostSpend, e));
        int net = cow - spend;

        if (cowCompostLabel != null) cowCompostLabel.text = $"+{cow:N0}";
        if (boostSpendLabel != null) boostSpendLabel.text = $"−{spend:N0}";
        if (netCompostLabel != null) netCompostLabel.text = (net >= 0 ? "+" : "") + net.ToString("N0");

        for (int i = 0; i < researchTargets.Count; i++)
        {
            var rt = researchTargets[i];
            if (rt.label == null) continue;
            if (rt.finalDelta <= 0) continue; // "no change" rows stay static
            int curDelta = Mathf.RoundToInt(Mathf.Lerp(0, rt.finalDelta, e));
            int curAfter = rt.finalAfter - (rt.finalDelta - curDelta);
            int curBefore = rt.finalAfter - rt.finalDelta;
            rt.label.text = $"L{curBefore} → L{curAfter}  (+{curDelta})";
        }
    }

    private void FinishLoadAnimation()
    {
        loadTicker?.Pause();
        loadTicker = null;
        if (loadingLabel != null) loadingLabel.text = "Done!";
        if (continueButton != null) continueButton.SetEnabled(true);
        if (!string.IsNullOrEmpty(boostSummaryFinalText) && boostSummaryLabel != null)
        {
            boostSummaryLabel.text = boostSummaryFinalText;
            boostSummaryLabel.style.display = DisplayStyle.Flex;
        }
    }

    private void Close()
    {
        if (modalRoot != null) modalRoot.style.display = DisplayStyle.None;
        if (root != null) root.pickingMode = PickingMode.Ignore;
    }

    private void BuildResearchRows(ResearchManager.OfflineCatchUpReport report)
    {
        if (researchSection == null) return;
        researchSection.Clear();
        researchTargets.Clear();

        if (report == null || report.slots == null || report.slots.Length == 0)
        {
            var none = new Label("No active research."); none.AddToClassList("research-row__label");
            researchSection.Add(none);
            return;
        }

        bool anyActive = false;
        for (int i = 0; i < report.slots.Length; i++)
        {
            var sp = report.slots[i];
            if (sp == null || string.IsNullOrEmpty(sp.researchID)) continue;
            anyActive = true;

            int delta = sp.levelAfter - sp.levelBefore;
            var row = new VisualElement(); row.AddToClassList("research-row");
            var label = new Label($"Slot {i + 1}: {sp.displayName}"); label.AddToClassList("research-row__label");
            var value = new Label();
            value.AddToClassList("research-row__value");
            if (delta > 0)
            {
                // Start at zero-delta; the animation counts up.
                value.text = $"L{sp.levelBefore} → L{sp.levelBefore}  (+0)";
                researchTargets.Add((value, delta, sp.levelAfter));
            }
            else
            {
                value.text = $"L{sp.levelAfter}  (no change)";
                value.AddToClassList("research-row__value--none");
            }
            row.Add(label); row.Add(value);
            researchSection.Add(row);
        }

        if (!anyActive)
        {
            var none = new Label("No active research."); none.AddToClassList("research-row__label");
            researchSection.Add(none);
        }
    }

    private void BuildBoostSummary(ResearchManager.OfflineCatchUpReport report)
    {
        if (boostSummaryLabel == null) return;
        if (report == null || report.totalAutoBuyRenewals <= 0)
        {
            boostSummaryFinalText = "";
            boostSummaryLabel.text = "";
            boostSummaryLabel.style.display = DisplayStyle.None;
            return;
        }
        // Defer text until the load animation completes — it appears in FinishLoadAnimation.
        boostSummaryFinalText = $"Auto-bought {report.totalAutoBuyRenewals} boost{(report.totalAutoBuyRenewals == 1 ? "" : "s")} for {report.compostSpentOnAutoBuy:N0} 🌱.";
        boostSummaryLabel.text = "";
        boostSummaryLabel.style.display = DisplayStyle.None;
    }

    private static string FormatGap(TimeSpan ts)
    {
        if (ts.TotalDays >= 1)
            return $"{(int)ts.TotalDays}d {ts.Hours}h {ts.Minutes}m";
        if (ts.TotalHours >= 1)
            return $"{(int)ts.TotalHours}h {ts.Minutes}m";
        return $"{(int)ts.TotalMinutes}m {ts.Seconds}s";
    }
}
