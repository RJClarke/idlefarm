using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Builds the shared ledger (Economy / Fields / Animals) from a RunLedgerData into a container.
/// Used by RunStatsPopupUITK and both welcome-back modal variants — ONE layout for all four
/// run-stat windows. `compact` = the lighter welcome-back "Continue" summary (keeps Economy +
/// zone cards; drops equipment lines, Animals, and spent-on-bags).
/// NOTE: no emoji anywhere — currency/crop/animal icons are sprite-backed VisualElements
/// (the UITK panel has no emoji fallback font on Android).
/// </summary>
public static class RunStatsLedgerView
{
    public static void Build(VisualElement container, RunLedgerData d, bool compact)
    {
        container.Clear();

        // Economy — currency values with real icon sprites on the right.
        var econ = Section(container, "Economy");
        if (d.hasResumeMoney) Row(econ, "Money now", d.resumeMoney.ToString("N0"), null, "money");
        else if (!compact)
        {
            Row(econ, "Money earned", d.moneyEarned.ToString("N0"), null, "money");
            if (d.moneySpentOnBags > 0) Row(econ, "Spent on seed bags", "−" + d.moneySpentOnBags.ToString("N0"), "neg", "money");
        }
        Row(econ, "Coins banked", "+" + d.coinsBanked.ToString("N0"), "coin", "coins");
        if (d.compostGained > 0) Row(econ, "Compost gained", "+" + d.compostGained.ToString("N0"), "pos", "compost");
        Row(econ, "Total harvested", d.totalHarvested.ToString("N0"), "total");
        if (d.offlineTaxApplied) Row(econ, "after 30% offline tax", "applied", "dim");

        // Fields — 2x2 zone cards mirroring the farm.
        if (d.zoneCards.Count > 0)
        {
            Section(container, "Fields");
            var grid = new VisualElement(); grid.AddToClassList("zone-grid");
            container.Add(grid);
            foreach (var card in d.zoneCards) ZoneCard(grid, card, compact);
        }
        else
        {
            var none = Section(container, "Fields");
            Row(none, "Nothing planted", "0", "dim");
        }

        // Animals — small sprite icon rows; live full view only.
        if (!compact && (d.hasDog || d.hasCow))
        {
            var animals = Section(container, "Animals");
            if (d.hasDog) IconRow(animals, d.dogSprite, "Dog — deer chased off", d.deerChasedByDog.ToString());
            if (d.hasCow)
            {
                IconRow(animals, d.cowSprite, "Cow — plants eaten", d.plantsEatenByCow.ToString());
                IconRow(animals, d.cowSprite, "Cow — compost gained", "+" + d.compostFromCow.ToString("N0"), "compost");
            }
        }
    }

    // ── Zone card ────────────────────────────────────────────────────────

    private static void ZoneCard(VisualElement grid, LedgerZoneCard c, bool compact)
    {
        var card = new VisualElement(); card.AddToClassList("zone-card");

        var header = new VisualElement(); header.AddToClassList("zone-card__header");
        var icon = new VisualElement(); icon.AddToClassList("zone-card__icon");
        if (c.cropSprite != null) icon.style.backgroundImage = new StyleBackground(c.cropSprite);
        var name = new Label(c.cropName); name.AddToClassList("zone-card__name");
        var zone = new Label($"Z{c.zoneId}"); zone.AddToClassList("zone-card__zone");
        header.Add(icon); header.Add(name); header.Add(zone);
        card.Add(header);

        ZoneRow(card, "Harvested", c.harvested.ToString("N0"), c.harvested == 0, null, null);
        ZoneRow(card, "Cash", "+" + c.moneyEarned.ToString("N0"), c.moneyEarned == 0, "money", "pos");
        ZoneRow(card, "Coins", "+" + c.coinsBanked.ToString("N0"), c.coinsBanked == 0, "coins", "pos");

        ZoneRow(card, "Deer ate", c.eatenByDeer.ToString(), c.eatenByDeer == 0, null, "neg");
        ZoneRow(card, "Crows ate", c.eatenByCrows.ToString(), c.eatenByCrows == 0, null, "neg");
        ZoneRow(card, "Lightning", c.struckByLightning.ToString(), c.struckByLightning == 0, null, "neg");
        ZoneRow(card, "Dried up", c.driedUp.ToString(), c.driedUp == 0, null, "neg");
        ZoneRow(card, "Rotted", c.rotted.ToString(), c.rotted == 0, null, "neg");

        if (!compact)
        {
            if (c.deerRepelled.HasValue)       ZoneRow(card, "Fence blocked", c.deerRepelled.Value.ToString(), false, null, "def");
            if (c.crowsRepelled.HasValue)      ZoneRow(card, "Scarecrow scared", c.crowsRepelled.Value.ToString(), false, null, "def");
            if (c.wateredBySprinkler.HasValue) ZoneRow(card, "Sprinkler watered", c.wateredBySprinkler.Value.ToString(), false, null, "def");
        }

        grid.Add(card);
    }

    private static void ZoneRow(VisualElement card, string label, string value, bool zero, string iconClass, string mod)
    {
        var row = new VisualElement(); row.AddToClassList("zone-row");
        if (zero) row.AddToClassList("zone-row--zero");
        var l = new Label(label); l.AddToClassList("zone-row__label");
        var group = new VisualElement(); group.AddToClassList("stat-row__value-group");
        if (iconClass != null) group.Add(CurrencyIcon(iconClass, small: true));
        var v = new Label(value); v.AddToClassList("zone-row__value");
        if (mod == "neg" && !zero) v.AddToClassList("stat-row__value--negative");
        if (mod == "pos" && !zero) v.AddToClassList("stat-row__value--positive");
        if (mod == "def") v.AddToClassList("zone-row__value--defense");
        group.Add(v);
        row.Add(l); row.Add(group);
        card.Add(row);
    }

    // ── Shared rows ──────────────────────────────────────────────────────

    private static VisualElement Section(VisualElement parent, string title)
    {
        var header = new Label(title); header.AddToClassList("section-title");
        parent.Add(header);
        var body = new VisualElement(); body.AddToClassList("section");
        parent.Add(body);
        return body;
    }

    /// <summary>Sprite icon for a currency, sized by context. `kind` = money/coins/gems/compost
    /// (matches the shared .currency-icon--* USS classes, same art as the top bar).</summary>
    private static VisualElement CurrencyIcon(string kind, bool small)
    {
        var icon = new VisualElement();
        icon.AddToClassList(small ? "ledger-currency-icon--small" : "ledger-currency-icon");
        icon.AddToClassList("currency-icon--" + kind);
        return icon;
    }

    private static void Row(VisualElement parent, string label, string value, string valueMod, string currency = null)
    {
        var row = new VisualElement(); row.AddToClassList("stat-row");
        if (valueMod == "total") row.AddToClassList("stat-row--total");
        if (valueMod == "coin")  row.AddToClassList("stat-row--coin");
        if (valueMod == "dim")   row.AddToClassList("stat-row--dim");
        var l = new Label(label); l.AddToClassList("stat-row__label");

        var valueGroup = new VisualElement(); valueGroup.AddToClassList("stat-row__value-group");
        if (!string.IsNullOrEmpty(currency)) valueGroup.Add(CurrencyIcon(currency, small: false));
        var v = new Label(value); v.AddToClassList("stat-row__value");
        if (valueMod == "neg") v.AddToClassList("stat-row__value--negative");
        if (valueMod == "pos" || valueMod == "coin") v.AddToClassList("stat-row__value--positive");
        valueGroup.Add(v);

        row.Add(l); row.Add(valueGroup);
        parent.Add(row);
    }

    private static void IconRow(VisualElement parent, Sprite sprite, string label, string value, string currency = null)
    {
        var row = new VisualElement(); row.AddToClassList("stat-row");
        var left = new VisualElement(); left.AddToClassList("crop-row__left");
        var icon = new VisualElement(); icon.AddToClassList("crop-row__icon");
        if (sprite != null) icon.style.backgroundImage = new StyleBackground(sprite);
        var name = new Label(label); name.AddToClassList("stat-row__label");
        left.Add(icon); left.Add(name);
        var group = new VisualElement(); group.AddToClassList("stat-row__value-group");
        if (currency != null) group.Add(CurrencyIcon(currency, small: false));
        var v = new Label(value); v.AddToClassList("stat-row__value");
        v.AddToClassList("stat-row__value--positive");
        group.Add(v);
        row.Add(left); row.Add(group);
        parent.Add(row);
    }
}
