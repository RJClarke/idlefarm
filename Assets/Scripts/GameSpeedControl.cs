using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Player-facing game-speed multiplier, applied by RunManager.ApplyGameSpeedScale as
/// <c>Time.timeScale = baseGameSpeed × Multiplier</c>.
///
/// Game Speed research does NOT multiply the running game — it raises <see cref="UnlockedMax"/>,
/// the highest rung the stepper can reach. So finishing a level never silently speeds the game up;
/// it just lets the player choose a faster setting. (This used to multiply on top of the stepper,
/// which made "1×" secretly run at 1.9× once a level was done.)
///
/// Driven by the speed stepper under the run timer (RunUI). Clamped to the ladder range —
/// stepping past either end is a no-op (no wrap-around).
/// </summary>
public static class GameSpeedControl
{
    /// <summary>
    /// Selectable rungs. Everything at or below <see cref="UnlockedMax"/> is earned; the rest stay
    /// reachable for dev testing and are flagged yellow on the stepper. The current cap is spliced
    /// in as its own rung, so an awkward unlock like 2.75× is always exactly selectable.
    /// </summary>
    private static readonly float[] Ladder =
        { 1f, 1.25f, 1.5f, 1.75f, 2f, 2.5f, 3f, 5f, 10f, 20f, 30f };

    private const float Epsilon = 0.001f;

    private static readonly List<float> steps = new List<float>();
    private static float cachedMax = -1f;

    public static int Index { get; private set; }

    /// <summary>Highest speed the player has actually unlocked: 1 + the Game Speed research bonus.</summary>
    public static float UnlockedMax
    {
        get
        {
            float bonus = ResearchManager.Instance != null
                ? ResearchManager.Instance.GetBonus(Research.StatKey.GameSpeed)
                : 0f;
            return Mathf.Max(1f, 1f + bonus);
        }
    }

    /// <summary>True when the current rung is above what research has unlocked — i.e. dev-only.</summary>
    public static bool IsDevSpeed => Multiplier > UnlockedMax + Epsilon;

    public static float Multiplier
    {
        get { EnsureSteps(); return steps[Mathf.Clamp(Index, 0, steps.Count - 1)]; }
    }

    public static string Label => FormatMultiplier(Multiplier);

    public static bool AtMin { get { EnsureSteps(); return Index <= 0; } }
    public static bool AtMax { get { EnsureSteps(); return Index >= steps.Count - 1; } }

    /// <summary>Step the speed up (dir &gt; 0) or down (dir &lt; 0), clamped. True if the value changed.</summary>
    public static bool Step(int dir)
    {
        EnsureSteps();
        int next = Mathf.Clamp(Index + (dir > 0 ? 1 : -1), 0, steps.Count - 1);
        if (next == Index) return false;
        Index = next;
        return true;
    }

    /// <summary>Jump straight to the fastest (dir &gt; 0) or slowest rung. True if the value changed.</summary>
    public static bool JumpToEnd(int dir)
    {
        EnsureSteps();
        int next = dir > 0 ? steps.Count - 1 : 0;
        if (next == Index) return false;
        Index = next;
        return true;
    }

    /// <summary>"1×", "1.25×", "2.75×" — trailing zeros trimmed so whole numbers stay clean.</summary>
    public static string FormatMultiplier(float m)
    {
        string n = Mathf.Approximately(m, Mathf.Round(m))
            ? Mathf.RoundToInt(m).ToString()
            : m.ToString("0.##");
        return n + "×";
    }

    /// <summary>
    /// Rebuild the rung list when the unlocked cap changes, keeping the player on the same speed
    /// where possible (research finishing mid-run must not jolt the game to a different rate).
    /// </summary>
    private static void EnsureSteps()
    {
        float max = UnlockedMax;
        if (steps.Count > 0 && Mathf.Approximately(max, cachedMax)) return;

        float current = steps.Count > 0 ? steps[Mathf.Clamp(Index, 0, steps.Count - 1)] : 1f;

        steps.Clear();
        foreach (float rung in Ladder)
        {
            // Splice the cap in ahead of the first rung that overshoots it.
            if (max > 1f + Epsilon && rung > max + Epsilon && !ContainsApprox(steps, max))
                steps.Add(max);
            steps.Add(rung);
        }
        if (max > 1f + Epsilon && !ContainsApprox(steps, max)) steps.Add(max);

        cachedMax = max;
        Index = NearestIndex(current);
    }

    private static bool ContainsApprox(List<float> list, float v)
    {
        for (int i = 0; i < list.Count; i++) if (Mathf.Abs(list[i] - v) < Epsilon) return true;
        return false;
    }

    private static int NearestIndex(float target)
    {
        int best = 0;
        float bestDiff = float.MaxValue;
        for (int i = 0; i < steps.Count; i++)
        {
            float d = Mathf.Abs(steps[i] - target);
            if (d < bestDiff) { bestDiff = d; best = i; }
        }
        return best;
    }

#if UNITY_EDITOR
    /// <summary>Test hook — drops the cached ladder so the next read rebuilds from current research.</summary>
    public static void EditorInvalidate() { steps.Clear(); cachedMax = -1f; Index = 0; }
#endif
}
