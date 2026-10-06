using NUnit.Framework;

public class PurchaseLedgerCoreTests
{
    [Test]
    public void FirstDelivery_IsNew_DuplicateIsNot()
    {
        var l = new PurchaseLedgerCore();
        Assert.IsTrue(l.TryMarkDelivered("tx-1"));
        Assert.IsFalse(l.TryMarkDelivered("tx-1"));
        Assert.IsTrue(l.IsDelivered("tx-1"));
    }

    [Test]
    public void EmptyTransactionId_AlwaysDeliverable_NeverRecorded()
    {
        var l = new PurchaseLedgerCore();
        Assert.IsTrue(l.TryMarkDelivered(""));
        Assert.IsTrue(l.TryMarkDelivered(null));
        Assert.AreEqual(0, l.Export().Length);
    }

    [Test]
    public void ExportImport_RoundTrips_AndSkipsBlanks()
    {
        var l = new PurchaseLedgerCore();
        l.TryMarkDelivered("b"); l.TryMarkDelivered("a");
        var copy = new PurchaseLedgerCore();
        copy.Import(new[] { "b", "a", "", null });
        CollectionAssert.AreEqual(new[] { "a", "b" }, copy.Export());
        Assert.IsFalse(copy.TryMarkDelivered("a"));
    }

    [Test]
    public void Import_Null_IsEmpty()
    {
        var l = new PurchaseLedgerCore();
        l.Import(null);
        Assert.AreEqual(0, l.Export().Length);
    }

    [Test]
    public void ShouldGrant_NewConsumable_Grants() =>
        Assert.IsTrue(PurchaseLedgerCore.ShouldGrant(true, isNonConsumable: false, alreadyOwned: false));

    [Test]
    public void ShouldGrant_DuplicateTransaction_NoGrant() =>
        Assert.IsFalse(PurchaseLedgerCore.ShouldGrant(false, isNonConsumable: false, alreadyOwned: false));

    [Test]
    public void ShouldGrant_PassAlreadyOwned_NoGems() =>
        Assert.IsFalse(PurchaseLedgerCore.ShouldGrant(true, isNonConsumable: true, alreadyOwned: true));

    [Test]
    public void ShouldGrant_FirstPass_Grants() =>
        Assert.IsTrue(PurchaseLedgerCore.ShouldGrant(true, isNonConsumable: true, alreadyOwned: false));
}
