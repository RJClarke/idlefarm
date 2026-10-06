using System.Linq;
using NUnit.Framework;

public class NarrativeDefaultsTests
{
    [Test]
    public void Letters_HaveUniqueIds_Text_AndKnownSenders()
    {
        var letters = NarrativeDefaults.Letters;
        var castNames = NarrativeDefaults.Cast.Select(c => c.displayName).ToList();
        Assert.AreEqual(letters.Length, letters.Select(l => l.id).Distinct().Count(), "duplicate letter id");
        foreach (var l in letters)
        {
            Assert.IsFalse(string.IsNullOrWhiteSpace(l.subject), l.id);
            Assert.IsFalse(string.IsNullOrWhiteSpace(l.body), l.id);
            Assert.Contains(l.senderName, castNames, $"{l.id}: sender not in cast");
            Assert.IsTrue(!string.IsNullOrEmpty(l.triggerEvent) || !string.IsNullOrEmpty(l.triggerFeatureFlag),
                $"{l.id}: no trigger");
        }
    }

    [Test]
    public void Tips_HaveUniqueIds_TextAndWhen()
    {
        var tips = NarrativeDefaults.Tips;
        Assert.AreEqual(tips.Length, tips.Select(t => t.id).Distinct().Count(), "duplicate tip id");
        foreach (var t in tips)
        {
            Assert.IsFalse(string.IsNullOrWhiteSpace(t.text), t.id);
            Assert.IsFalse(string.IsNullOrWhiteSpace(t.when), t.id);
        }
    }

    [Test]
    public void Copy_HasNoEmoji_WhichRenderInvisibleOnAndroid()
    {
        var all = NarrativeDefaults.Letters.SelectMany(l => new[] { l.subject, l.body })
            .Concat(NarrativeDefaults.Tips.Select(t => t.text));
        foreach (string s in all)
            foreach (char c in s)
                Assert.IsFalse(char.IsSurrogate(c), $"emoji/surrogate in: {s}");
    }

    [TestCase("seed_stall_intro", "run_ended:1")]
    [TestCase("regrow_bought", "seed_bought:regrow")]
    [TestCase("town_board_intro", "run_ended:4")]
    [TestCase("extra_packets", "upgrade:zone_unlock_2")]
    public void SeedProgressionLetters_Exist_WithTriggers(string id, string trigger)
    {
        var letter = NarrativeDefaults.Letters.FirstOrDefault(l => l.id == id);
        Assert.IsNotNull(letter, id);
        Assert.AreEqual(trigger, letter.triggerEvent);
        Assert.IsTrue(letter.newPlayersOnly);
        Assert.IsTrue(NarrativeDefaults.Cast.Any(c => c.displayName == letter.senderName), letter.senderName);
    }

    [TestCase("tip_seed_stall")]
    [TestCase("tip_regrow")]
    [TestCase("tip_barn_spend")]
    [TestCase("tip_extra_packet")]
    [TestCase("tip_idle_field")]
    public void SeedProgressionTips_Exist(string id)
    {
        Assert.IsTrue(NarrativeDefaults.Tips.Any(t => t.id == id && !string.IsNullOrWhiteSpace(t.text)), id);
    }

    [Test]
    public void NoCopy_UsesTheWordGold()
    {
        foreach (var l in NarrativeDefaults.Letters) StringAssert.DoesNotContain(" Gold", l.body, l.id);
        foreach (var t in NarrativeDefaults.Tips) StringAssert.DoesNotContain(" Gold", t.text, t.id);
    }

    [Test]
    public void TownGift_LetterAndTip_Exist()
    {
        var letter = NarrativeDefaults.Letters.FirstOrDefault(l => l.id == "town_gift");
        Assert.IsNotNull(letter);
        Assert.AreEqual("welcome_basket_done", letter.triggerEvent);
        Assert.AreEqual("A little thank-you", letter.subject);
        Assert.IsFalse(letter.newPlayersOnly);
        var tip = NarrativeDefaults.Tips.FirstOrDefault(t => t.id == "tip_town_gift");
        Assert.IsNotNull(tip);
        Assert.AreEqual("A gift every 30 minutes. This one's on us!", tip.text);
    }
}
