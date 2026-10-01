using System.Linq;
using NUnit.Framework;

public class AlmanacPillsTests
{
    private static PillKind[] Kinds(System.Collections.Generic.List<Pill> p) => p.Select(x => x.kind).ToArray();

    [Test]
    public void CropPills_FromThresholds_RadishLike()
    {
        var pills = AlmanacPills.CropPills(140, 70, regrows: false, cannable: false, thirst: 0.8f, deerAppetite: 1f, crowAppetite: 0.5f);
        CollectionAssert.AreEqual(
            new[] { PillKind.QuickGrower, PillKind.Fragile, PillKind.DroughtHardy, PillKind.CrowDamage }, Kinds(pills));
    }

    [Test]
    public void CropPills_RegrowsCannableThirsty_TomatoLike()
    {
        var pills = AlmanacPills.CropPills(355, 95, regrows: true, cannable: true, thirst: 1.25f, deerAppetite: 1.25f, crowAppetite: 1f);
        CollectionAssert.AreEqual(
            new[] { PillKind.Regrows, PillKind.SlowGrower, PillKind.Cannable, PillKind.Sturdy, PillKind.Thirsty, PillKind.DeerDamage },
            Kinds(pills));
    }

    [Test]
    public void CropPills_AverageCrop_HasNone() =>
        CollectionAssert.IsEmpty(AlmanacPills.CropPills(275, 80, false, false, 1f, 1f, 1f));

    [Test]
    public void CropPills_GreatCompost_OnlyAboveOne()
    {
        CollectionAssert.Contains(Kinds(AlmanacPills.CropPills(275, 80, false, false, 1f, 1f, 1f, compostMultiplier: 2f)), PillKind.GreatCompost);
        CollectionAssert.DoesNotContain(Kinds(AlmanacPills.CropPills(275, 80, false, false, 1f, 1f, 1f)), PillKind.GreatCompost);
    }

    [Test]
    public void PestDamage_ReadsAsPlainPercent()
    {
        Pill deer = AlmanacPills.PestDamage(deer: true, appetite: 1.75f);
        Assert.AreEqual("Deer", deer.label);
        Assert.AreEqual("175%", deer.value);
        Assert.AreEqual(1, AlmanacPills.Heat(deer));
        Pill crow = AlmanacPills.PestDamage(deer: false, appetite: 0.25f);
        Assert.AreEqual("Crow", crow.label);
        Assert.AreEqual("25%", crow.value);
        Assert.AreEqual(-1, AlmanacPills.Heat(crow));
    }

    [Test]
    public void PestDamage_HelpExplainsTheNumber()
    {
        StringAssert.Contains("175%", AlmanacPills.Help(AlmanacPills.PestDamage(true, 1.75f)));
        StringAssert.Contains("25%", AlmanacPills.Help(AlmanacPills.PestDamage(false, 0.25f)));
    }

    [Test]
    public void EveryKind_HasALabelAndHelp()
    {
        foreach (PillKind k in System.Enum.GetValues(typeof(PillKind)))
        {
            Pill p = k == PillKind.DeerDamage || k == PillKind.CrowDamage
                ? AlmanacPills.PestDamage(k == PillKind.DeerDamage, 1.5f)
                : AlmanacPills.Simple(k);
            Assert.IsFalse(string.IsNullOrWhiteSpace(p.label), k.ToString());
            Assert.IsFalse(string.IsNullOrWhiteSpace(AlmanacPills.Help(p)), k.ToString());
        }
    }

    [Test]
    public void ThresholdHelp_QuotesTheRealThresholds()
    {
        StringAssert.Contains("2m 30s", AlmanacPills.Help(AlmanacPills.Simple(PillKind.QuickGrower)));
        StringAssert.Contains("90", AlmanacPills.Help(AlmanacPills.Simple(PillKind.Sturdy)));
    }
}
