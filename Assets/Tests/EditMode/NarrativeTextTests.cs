using NUnit.Framework;

public class NarrativeTextTests
{
    [Test]
    public void Resolve_ReplacesFarmNameToken()
    {
        Assert.AreEqual("Dear Sunny Acres,",
            NarrativeText.Resolve("Dear {farmName},", "Sunny Acres"));
    }

    [Test]
    public void Resolve_ReplacesAllOccurrences()
    {
        Assert.AreEqual("A A",
            NarrativeText.Resolve("{farmName} {farmName}", "A"));
    }

    [Test]
    public void Resolve_LeavesUnknownTokensUntouched()
    {
        Assert.AreEqual("Hi {playerName}",
            NarrativeText.Resolve("Hi {playerName}", "Acres"));
    }

    [Test]
    public void Resolve_HandlesNulls()
    {
        Assert.AreEqual("", NarrativeText.Resolve(null, "x"));
        Assert.AreEqual("Dear ,", NarrativeText.Resolve("Dear {farmName},", null));
    }
}
