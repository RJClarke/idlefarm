using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// How a tutorial step is dismissed/advanced:
/// - TapAnywhere: any tap advances (informational step; whole screen is input-blocked).
/// - TargetPressed: only pressing the spotlighted control advances (everything else blocked;
///   the press also reaches the real control underneath).
/// - GameEvent: advances only when game code reports a named event (e.g. an upgrade purchase),
///   letting a step wait for the player to actually DO the thing, not just click a button.
/// </summary>
public enum TutorialAdvance
{
    TapAnywhere = 0,
    TargetPressed = 1,
    GameEvent = 2,
}

/// <summary>
/// Pure step-sequencer for one running tutorial: which step is current and which input kind
/// advances it. Engine-free (EconomyCore) so the advance rules are unit-testable; all
/// rendering/input capture lives in TutorialManager.
/// </summary>
public class TutorialFlow
{
    private readonly TutorialAdvance[] modes;
    private readonly string[] eventIds; // aligned with modes; only read for GameEvent steps

    public int StepIndex { get; private set; }
    public int StepCount => modes.Length;
    public bool IsComplete { get; private set; }
    public TutorialAdvance CurrentMode => modes[StepIndex];

    public TutorialFlow(TutorialAdvance[] stepModes, string[] stepEventIds)
    {
        modes = stepModes ?? new TutorialAdvance[0];
        eventIds = stepEventIds ?? new string[modes.Length];
        if (modes.Length == 0) IsComplete = true;
    }

    /// <summary>Any-tap input. Returns true if the current step advanced.</summary>
    public bool HandleTapAnywhere()
    {
        if (IsComplete || CurrentMode != TutorialAdvance.TapAnywhere) return false;
        return Advance();
    }

    /// <summary>A press landed inside the spotlight. Returns true if the step advanced.</summary>
    public bool HandleTargetPressed()
    {
        if (IsComplete || CurrentMode != TutorialAdvance.TargetPressed) return false;
        return Advance();
    }

    /// <summary>Game code reported a named event. Returns true if the step advanced.</summary>
    public bool HandleGameEvent(string eventId)
    {
        if (IsComplete || CurrentMode != TutorialAdvance.GameEvent) return false;
        string expected = StepIndex < eventIds.Length ? eventIds[StepIndex] : null;
        if (string.IsNullOrEmpty(expected) || expected != eventId) return false;
        return Advance();
    }

    private bool Advance()
    {
        StepIndex++;
        if (StepIndex >= StepCount)
        {
            StepIndex = StepCount - 1; // stay on a valid index for CurrentMode readers
            IsComplete = true;
        }
        return true;
    }
}

/// <summary>
/// Which tutorials the player has already completed — the "only teach this once" memory.
/// Persisted via GetForSave/LoadState (string ids in GameData, same pattern as
/// NewContentTracker's seenContentIds). Tutorials for features unlocked months in are just
/// sequences whose TryStart happens at unlock time; the ledger makes any replay a no-op.
/// </summary>
public class TutorialLedger
{
    private readonly HashSet<string> completed = new HashSet<string>();

    public bool IsCompleted(string id) => !string.IsNullOrEmpty(id) && completed.Contains(id);

    public void Complete(string id)
    {
        if (string.IsNullOrEmpty(id)) return;
        completed.Add(id);
    }

    /// <summary>Dev-tools replay: forget completions so the tutorials show again.</summary>
    public void Clear() => completed.Clear();

    public string[] GetForSave() => completed.OrderBy(s => s).ToArray();

    public void LoadState(string[] ids)
    {
        completed.Clear();
        if (ids == null) return;
        foreach (string id in ids)
            if (!string.IsNullOrEmpty(id)) completed.Add(id);
    }
}
