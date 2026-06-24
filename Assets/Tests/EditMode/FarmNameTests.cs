using NUnit.Framework;
using System;

public class FarmNameTests
{
    [Test]
    public void IsValid_RejectsTooShort()
    {
        Assert.IsFalse(FarmName.IsValid("ab"));   // 2 chars
        Assert.IsFalse(FarmName.IsValid("  a "));  // trims to 1
        Assert.IsFalse(FarmName.IsValid(null));
    }

    [Test]
    public void IsValid_AcceptsBoundaries()
    {
        Assert.IsTrue(FarmName.IsValid("abc"));            // 3
        Assert.IsTrue(FarmName.IsValid(new string('x', 30))); // 30
    }

    [Test]
    public void IsValid_RejectsTooLong()
    {
        Assert.IsFalse(FarmName.IsValid(new string('x', 31)));
    }

    [Test]
    public void Sanitize_TrimsAndClamps()
    {
        Assert.AreEqual("Sunny Acres", FarmName.Sanitize("  Sunny Acres  "));
        Assert.AreEqual(30, FarmName.Sanitize(new string('y', 50)).Length);
    }

    [Test]
    public void Suggestions_AreAllValidNames()
    {
        Assert.Greater(FarmNameSuggestions.Count, 0);
        for (int i = 0; i < FarmNameSuggestions.Count; i++)
            Assert.IsTrue(FarmName.IsValid(FarmNameSuggestions.At(i)),
                $"Suggestion '{FarmNameSuggestions.At(i)}' must satisfy 3..30 chars");
    }

    [Test]
    public void Suggestions_At_WrapsModulo()
    {
        Assert.AreEqual(FarmNameSuggestions.At(0), FarmNameSuggestions.At(FarmNameSuggestions.Count));
    }

    [Test]
    public void Suggestions_Random_IsDeterministicForSeed()
    {
        var a = FarmNameSuggestions.Random(new Random(42));
        var b = FarmNameSuggestions.Random(new Random(42));
        Assert.AreEqual(a, b);
    }
}
