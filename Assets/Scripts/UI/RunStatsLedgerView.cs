using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Builds the shared "ledger" sections (Economy / Harvested / Losses / Defense) from a RunLedgerData into
/// a container VisualElement. Used by RunStatsPopupUITK and both welcome-back modal variants.
/// `compact` trims the full recap to the lighter "Continue" summary (no Defense, no spent-on-bags line).
/// </summary>
public static class RunStatsLedgerView
{
    public static void Build(VisualElement container, RunLedgerData d, bool compact)
    {
        container.Clear();

        // Economy
        var econ = Section(container, "💰 Economy");
        if (d.hasResumeMoney) Row(econ, "Money now", "$" + d.resumeMoney.ToString("N0"), null);
        else if (!compact)
        {
            Row(econ, "Money earned", "$" + d.moneyEarned.ToString("N0"), null);
            if (d.moneySpentOnBags > 0) Row(econ, "Spent on seed bags", "−$" + d.moneySpentOnBags.ToString("N0"), "neg");
        }
        Row(econ, "🪙 Coins banked", "+" + d.coinsBanked.ToString("N0"), "coin");
        if (d.compostGained > 0) Row(econ, "🌱 Compost gained", "+" + d.compostGained.ToString("N0"), "pos");
        if (d.offlineTaxApplied) Row(econ, "after 30% offline tax", "applied", "dim");

        // Harvested (itemized)
        var harv = Section(container, "🌾 Harvested");
        if (d.harvested.Count == 0) Row(harv, "Nothing harvested", "0", "dim");
        foreach (var c in d.harvested) CropRow(harv, c);
        Row(harv, "Total harvested", d.totalHarvested.ToString("N0"), "total");

        // Losses (itemized by cause)
        var loss = Section(container, "☠️ Losses");
        Row(loss, "🦌 Eaten by deer", d.eatenByDeer.ToString(), "neg");
        Row(loss, "🐦 Eaten by crows", d.eatenByCrows.ToString(), "neg");
        Row(loss, "⚡ Struck by lightning", d.struckByLightning.ToString(), "neg");
        Row(loss, "🏜️ Dried up", d.driedUp.ToString(), "neg");
        Row(loss, "🍂 Rotted", d.rotted.ToString(), "neg");

        // Defense (live only)
        if (!compact && d.hasDefense)
        {
            var def = Section(container, "🛡️ Defense");
            Row(def, "Deer repelled (fence)", d.deerRepelled.ToString(), null);
            Row(def, "Crows repelled (scarecrow)", d.crowsRepelled.ToString(), null);
        }
    }

    private static VisualElement Section(VisualElement parent, string title)
    {
        var header = new Label(title); header.AddToClassList("section-title");
        parent.Add(header);
        var body = new VisualElement(); body.AddToClassList("section");
        parent.Add(body);
        return body;
    }

    private static void Row(VisualElement parent, string label, string value, string valueMod)
    {
        var row = new VisualElement(); row.AddToClassList("stat-row");
        if (valueMod == "total") row.AddToClassList("stat-row--total");
        if (valueMod == "coin")  row.AddToClassList("stat-row--coin");
        if (valueMod == "dim")   row.AddToClassList("stat-row--dim");
        var l = new Label(label); l.AddToClassList("stat-row__label");
        var v = new Label(value); v.AddToClassList("stat-row__value");
        if (valueMod == "neg") v.AddToClassList("stat-row__value--negative");
        if (valueMod == "pos" || valueMod == "coin") v.AddToClassList("stat-row__value--positive");
        row.Add(l); row.Add(v);
        parent.Add(row);
    }

    private static void CropRow(VisualElement parent, LedgerCropRow c)
    {
        var row = new VisualElement(); row.AddToClassList("stat-row");
        var left = new VisualElement(); left.AddToClassList("crop-row__left");
        var icon = new VisualElement(); icon.AddToClassList("crop-row__icon");
        if (c.sprite != null) icon.style.backgroundImage = new StyleBackground(c.sprite);
        var name = new Label(c.name); name.AddToClassList("stat-row__label");
        left.Add(icon); left.Add(name);
        var v = new Label(c.count.ToString("N0")); v.AddToClassList("stat-row__value");
        row.Add(left); row.Add(v);
        parent.Add(row);
    }
}
