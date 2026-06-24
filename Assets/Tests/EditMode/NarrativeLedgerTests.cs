using NUnit.Framework;

public class NarrativeLedgerTests
{
    [Test]
    public void MarkFired_FirstTime_ReturnsTrue_ThenFalse()
    {
        var ledger = new NarrativeLedger();
        Assert.IsTrue(ledger.MarkFired("onboarding_named"));
        Assert.IsFalse(ledger.MarkFired("onboarding_named"));
    }

    [Test]
    public void HasFired_TracksMarkedFlags()
    {
        var ledger = new NarrativeLedger();
        Assert.IsFalse(ledger.HasFired("letter:welcome"));
        ledger.MarkFired("letter:welcome");
        Assert.IsTrue(ledger.HasFired("letter:welcome"));
    }

    [Test]
    public void NullOrEmpty_IsIgnored_NeverFires()
    {
        var ledger = new NarrativeLedger();
        Assert.IsFalse(ledger.MarkFired(null));
        Assert.IsFalse(ledger.MarkFired(""));
        Assert.IsFalse(ledger.HasFired(null));
    }

    [Test]
    public void LoadAndToArray_RoundTrips_Deduped()
    {
        var ledger = new NarrativeLedger();
        ledger.Load(new[] { "a", "b", "a", null, "" });
        var arr = ledger.ToArray();
        Assert.AreEqual(2, arr.Length);
        Assert.Contains("a", arr);
        Assert.Contains("b", arr);
    }
}
