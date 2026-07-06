using UnityEngine;

/// <summary>
/// Player-facing game-speed multiplier, applied on top of the run's normal speed
/// (1 + Game Speed research bonus) by RunManager.ApplyGameSpeedScale.
///
/// Driven by the speed stepper under the run timer (RunUI). Clamped to the Steps
/// range — stepping past either end is a no-op (no wrap-around).
/// </summary>
public static class GameSpeedControl
{
    // Stepper multipliers and labels (index-aligned). 1–4× are the intended player range;
    // 10/20/30× are dev-only fast-forward tiers (flagged yellow on the stepper for now).
    public static readonly float[] Steps = { 1f, 2f, 3f, 4f, 10f, 20f, 30f };
    private static readonly string[] Labels = { "1×", "2×", "3×", "4×", "10×", "20×", "30×" };

    public static int Index { get; private set; } = 0;

    /// <summary>True for the dev-only fast-forward tiers (10×+); used to flag the stepper visually.</summary>
    public static bool IsDevSpeed => Multiplier >= 10f;

    public static float Multiplier => Steps[Mathf.Clamp(Index, 0, Steps.Length - 1)];
    public static string Label      => Labels[Mathf.Clamp(Index, 0, Labels.Length - 1)];

    public static bool AtMin => Index <= 0;
    public static bool AtMax => Index >= Steps.Length - 1;

    /// <summary>Step the speed up (dir &gt; 0) or down (dir &lt; 0), clamped. Returns true if the value changed.</summary>
    public static bool Step(int dir)
    {
        int next = Mathf.Clamp(Index + (dir > 0 ? 1 : -1), 0, Steps.Length - 1);
        if (next == Index) return false;
        Index = next;
        return true;
    }
}
