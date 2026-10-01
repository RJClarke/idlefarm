using System.Collections.Generic;
using UnityEngine;

/// <summary>How a stat's numbers are written in the Almanac.</summary>
public enum StatFormat { Money, Coins, Number, Seconds, Multiplier, Percent, Tiles, Speed }

/// <summary>One contribution to a stat: which source changed it and by how much.</summary>
public struct StatBonus
{
    public string source;
    public float delta;
}

/// <summary>
/// A stat shown as "base, then each bonus source, then total". Built by applying the same steps
/// gameplay applies (Add / Multiply / Divide / AtLeast, optionally rounding like gameplay does), so
/// the Almanac and the game compute a value with one piece of code. Each step that changes the
/// value records a StatBonus; repeated steps from one source merge into a single line.
/// </summary>
public class StatLine
{
    public readonly string label;
    public readonly float baseValue;
    public readonly StatFormat format;
    public float total { get; private set; }

    private readonly List<StatBonus> bonuses = new List<StatBonus>();
    public IReadOnlyList<StatBonus> Bonuses => bonuses;
    public bool HasBonus => bonuses.Count > 0;

    public StatLine(string label, float baseValue, StatFormat format)
    {
        this.label = label;
        this.baseValue = baseValue;
        this.format = format;
        total = baseValue;
    }

    public StatLine Add(string source, float amount) => Apply(source, total + amount, false);

    public StatLine Multiply(string source, float factor, bool roundToInt = false) =>
        Apply(source, total * factor, roundToInt);

    public StatLine Divide(string source, float divisor, bool roundToInt = false) =>
        Apply(source, total / Mathf.Max(0.0001f, divisor), roundToInt);

    /// <summary>Floors the total (e.g. an equipment cooldown's minimum); recorded as "Minimum".</summary>
    public StatLine AtLeast(float min) => total < min ? Apply("Minimum", min, false) : this;

    private StatLine Apply(string source, float next, bool roundToInt)
    {
        if (roundToInt) next = Mathf.RoundToInt(next);
        float delta = next - total;
        total = next;
        if (Mathf.Abs(delta) < 0.0001f) return this;

        for (int i = 0; i < bonuses.Count; i++)
        {
            if (bonuses[i].source != source) continue;
            bonuses[i] = new StatBonus { source = source, delta = bonuses[i].delta + delta };
            return this;
        }
        bonuses.Add(new StatBonus { source = source, delta = delta });
        return this;
    }
}

/// <summary>Formats StatLine numbers for display: "$37", "+$12", "5m 25s", "x1.25", "40%".</summary>
public static class StatText
{
    public static string Value(float v, StatFormat f) => f switch
    {
        StatFormat.Money => "$" + Num(v),
        StatFormat.Coins => Num(v) + (Mathf.Approximately(v, 1f) ? " Coin" : " Coins"),
        StatFormat.Seconds => Duration(v),
        StatFormat.Multiplier => "x" + Num(v, 2),
        StatFormat.Percent => Num(v * 100f) + "%",
        StatFormat.Tiles => Num(v, 1) + (Mathf.Approximately(v, 1f) ? " tile" : " tiles"),
        StatFormat.Speed => Num(v, 1) + " tiles/s",
        _ => Num(v),
    };

    public static string Delta(float d, StatFormat f)
    {
        string sign = d < 0f ? "-" : "+";
        float a = Mathf.Abs(d);
        return f switch
        {
            StatFormat.Money => sign + "$" + Num(a),
            StatFormat.Seconds => sign + Duration(a),
            StatFormat.Multiplier => sign + Num(a, 2),
            StatFormat.Percent => sign + Num(a * 100f) + "%",
            StatFormat.Tiles => sign + Num(a, 1),
            StatFormat.Speed => sign + Num(a, 1),
            _ => sign + Num(a),
        };
    }

    private static string Num(float v, int decimals = 1)
    {
        float rounded = (float)System.Math.Round(v, decimals);
        return rounded.ToString(decimals >= 2 ? "0.##" : "0.#", System.Globalization.CultureInfo.InvariantCulture);
    }

    private static string Duration(float seconds)
    {
        int s = Mathf.RoundToInt(seconds);
        if (s < 60) return s + "s";
        int m = s / 60, r = s % 60;
        if (m < 60) return r == 0 ? m + "m" : $"{m}m {r}s";
        int h = m / 60, rm = m % 60;
        return rm == 0 ? h + "h" : $"{h}h {rm}m";
    }
}
