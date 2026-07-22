using NUnit.Framework;

public class TutorialFlowTests
{
    // ── TutorialLedger ─────────────────────────────────────────────

    [Test]
    public void Ledger_NewLedger_NothingCompleted()
    {
        var ledger = new TutorialLedger();

        Assert.IsFalse(ledger.IsCompleted("first_run"));
    }

    [Test]
    public void Ledger_Complete_MarksCompleted()
    {
        var ledger = new TutorialLedger();

        ledger.Complete("first_run");

        Assert.IsTrue(ledger.IsCompleted("first_run"));
        Assert.IsFalse(ledger.IsCompleted("other"));
    }

    [Test]
    public void Ledger_NullOrEmptyIds_AreNeverCompletedAndIgnored()
    {
        var ledger = new TutorialLedger();

        ledger.Complete(null);
        ledger.Complete("");

        Assert.IsFalse(ledger.IsCompleted(null));
        Assert.IsFalse(ledger.IsCompleted(""));
        Assert.AreEqual(0, ledger.GetForSave().Length);
    }

    [Test]
    public void Ledger_SaveLoad_RoundTrips()
    {
        var ledger = new TutorialLedger();
        ledger.Complete("a");
        ledger.Complete("b");

        var restored = new TutorialLedger();
        restored.LoadState(ledger.GetForSave());

        Assert.IsTrue(restored.IsCompleted("a"));
        Assert.IsTrue(restored.IsCompleted("b"));
        Assert.IsFalse(restored.IsCompleted("c"));
    }

    [Test]
    public void Ledger_LoadState_Null_ClearsToEmpty()
    {
        var ledger = new TutorialLedger();
        ledger.Complete("a");

        ledger.LoadState(null);

        Assert.IsFalse(ledger.IsCompleted("a"));
        Assert.AreEqual(0, ledger.GetForSave().Length);
    }

    [Test]
    public void Ledger_LoadState_ReplacesPriorContents()
    {
        var ledger = new TutorialLedger();
        ledger.Complete("old");

        ledger.LoadState(new[] { "new" });

        Assert.IsFalse(ledger.IsCompleted("old"));
        Assert.IsTrue(ledger.IsCompleted("new"));
    }

    // ── TutorialFlow ───────────────────────────────────────────────

    private static TutorialFlow Flow(params TutorialAdvance[] modes) => new TutorialFlow(modes, null);

    [Test]
    public void Flow_EmptySequence_IsImmediatelyComplete()
    {
        var flow = Flow();

        Assert.IsTrue(flow.IsComplete);
    }

    [Test]
    public void Flow_StartsAtStepZero_NotComplete()
    {
        var flow = Flow(TutorialAdvance.TapAnywhere);

        Assert.AreEqual(0, flow.StepIndex);
        Assert.IsFalse(flow.IsComplete);
    }

    [Test]
    public void Flow_Tap_AdvancesTapStep()
    {
        var flow = Flow(TutorialAdvance.TapAnywhere, TutorialAdvance.TapAnywhere);

        Assert.IsTrue(flow.HandleTapAnywhere());

        Assert.AreEqual(1, flow.StepIndex);
        Assert.IsFalse(flow.IsComplete);
    }

    [Test]
    public void Flow_Tap_DoesNotAdvanceTargetStep()
    {
        var flow = Flow(TutorialAdvance.TargetPressed);

        Assert.IsFalse(flow.HandleTapAnywhere());

        Assert.AreEqual(0, flow.StepIndex);
    }

    [Test]
    public void Flow_TargetPress_AdvancesTargetStep()
    {
        var flow = Flow(TutorialAdvance.TargetPressed);

        Assert.IsTrue(flow.HandleTargetPressed());

        Assert.IsTrue(flow.IsComplete);
    }

    [Test]
    public void Flow_TargetPress_DoesNotAdvanceTapStep()
    {
        var flow = Flow(TutorialAdvance.TapAnywhere);

        Assert.IsFalse(flow.HandleTargetPressed());

        Assert.AreEqual(0, flow.StepIndex);
    }

    [Test]
    public void Flow_GameEvent_AdvancesOnMatchingId()
    {
        var flow = new TutorialFlow(
            new[] { TutorialAdvance.GameEvent },
            new[] { "upgrade_purchased" });

        Assert.IsFalse(flow.HandleGameEvent("wrong_event"));
        Assert.AreEqual(0, flow.StepIndex);

        Assert.IsTrue(flow.HandleGameEvent("upgrade_purchased"));
        Assert.IsTrue(flow.IsComplete);
    }

    [Test]
    public void Flow_GameEvent_IgnoredOnNonEventSteps()
    {
        var flow = Flow(TutorialAdvance.TapAnywhere);

        Assert.IsFalse(flow.HandleGameEvent("anything"));

        Assert.AreEqual(0, flow.StepIndex);
    }

    [Test]
    public void Flow_CompletesAfterLastStep()
    {
        var flow = Flow(TutorialAdvance.TapAnywhere, TutorialAdvance.TapAnywhere);

        flow.HandleTapAnywhere();
        Assert.IsFalse(flow.IsComplete);
        flow.HandleTapAnywhere();

        Assert.IsTrue(flow.IsComplete);
        Assert.IsFalse(flow.HandleTapAnywhere(), "a completed flow must ignore further input");
    }

    [Test]
    public void Flow_FarmUpgradeExample_ThreeStepWalkthrough()
    {
        // Mirrors the designed sequence: info tap ("upgrades live down here") →
        // forced press of the Farm button → wait for the actual purchase event.
        var flow = new TutorialFlow(
            new[] { TutorialAdvance.TapAnywhere, TutorialAdvance.TargetPressed, TutorialAdvance.GameEvent },
            new[] { null, null, "farm_upgrade_purchased" });

        Assert.IsTrue(flow.HandleTapAnywhere());
        Assert.IsFalse(flow.HandleTapAnywhere(), "tap must not skip the forced-press step");
        Assert.IsTrue(flow.HandleTargetPressed());
        Assert.IsFalse(flow.HandleTargetPressed(), "extra presses must not skip the event step");
        Assert.IsFalse(flow.HandleGameEvent("other"));
        Assert.IsTrue(flow.HandleGameEvent("farm_upgrade_purchased"));
        Assert.IsTrue(flow.IsComplete);
    }
}
