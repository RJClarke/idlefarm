using NUnit.Framework;

public class CropTraitsTests
{
    // Pill tests live in AlmanacPillsTests (the pills replaced CropTraits.Tags).

    [TestCase(0.25f, "won't touch it")]
    [TestCase(0.5f, "avoids it")]
    [TestCase(0.75f, "nibbles")]
    [TestCase(1f, "normal")]
    [TestCase(1.25f, "likes it")]
    [TestCase(1.5f, "loves it")]
    public void AppetiteWord(float appetite, string word) => Assert.AreEqual(word, CropTraits.AppetiteWord(appetite));

    [Test]
    public void AppetiteFill_IsHalfTheMultiplier_Clamped()
    {
        Assert.AreEqual(0.5f, CropTraits.AppetiteFill(1f), 1e-5f);
        Assert.AreEqual(1f, CropTraits.AppetiteFill(3f), 1e-5f);
        Assert.AreEqual(0f, CropTraits.AppetiteFill(-1f), 1e-5f);
    }

    // Regrow timing: a harvested regrower restarts at the sapling stage; regrowSeconds (when set)
    // is the whole wait until the next harvest, otherwise the sapling stage's own length.
    [Test] public void RegrowSeconds_UsesAuthoredTime() => Assert.AreEqual(90f, CropTraits.RegrowSeconds(true, 90f, 140f));
    [Test] public void RegrowSeconds_FallsBackToSaplingStage() => Assert.AreEqual(140f, CropTraits.RegrowSeconds(true, 0f, 140f));
    [Test] public void RegrowSeconds_ZeroForNonRegrower() => Assert.AreEqual(0f, CropTraits.RegrowSeconds(false, 90f, 140f));
}
