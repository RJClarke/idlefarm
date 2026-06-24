using NUnit.Framework;

public class InboxModelTests
{
    [Test]
    public void Deliver_AppendsEntry_NewestFirst()
    {
        var m = new InboxModel();
        m.Deliver("welcome", 100);
        m.Deliver("scarecrow", 200);
        Assert.AreEqual(2, m.Entries.Count);
        Assert.AreEqual("scarecrow", m.Entries[0].letterId); // newest first
        Assert.AreEqual("welcome", m.Entries[1].letterId);
    }

    [Test]
    public void UnreadCount_CountsOnlyUnread()
    {
        var m = new InboxModel();
        m.Deliver("a", 1);
        m.Deliver("b", 2);
        Assert.AreEqual(2, m.UnreadCount());
        m.MarkRead("a");
        Assert.AreEqual(1, m.UnreadCount());
    }

    [Test]
    public void MarkRead_OnlyFlipsOnce()
    {
        var m = new InboxModel();
        m.Deliver("a", 1);
        Assert.IsTrue(m.MarkRead("a"));
        Assert.IsFalse(m.MarkRead("a"));   // already read
        Assert.IsFalse(m.MarkRead("missing"));
    }

    [Test]
    public void Claim_ReturnsTrueOnlyOnFirstClaim()
    {
        var m = new InboxModel();
        m.Deliver("a", 1);
        Assert.IsTrue(m.Claim("a"));
        Assert.IsFalse(m.Claim("a"));      // already claimed
        Assert.IsFalse(m.Claim("missing"));
    }

    [Test]
    public void LoadAndToArray_RoundTrips()
    {
        var m = new InboxModel();
        m.Deliver("a", 10);
        m.MarkRead("a");
        var saved = m.ToArray();

        var m2 = new InboxModel();
        m2.Load(saved);
        Assert.AreEqual(1, m2.Entries.Count);
        Assert.AreEqual(0, m2.UnreadCount());
        Assert.AreEqual("a", m2.Entries[0].letterId);
    }
}
