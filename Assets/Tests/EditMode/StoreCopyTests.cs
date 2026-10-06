using NUnit.Framework;

public class StoreCopyTests
{
    [TestCase(1, "Confirm 1 gem?")]
    [TestCase(2, "Confirm 2 gems?")]
    [TestCase(1000, "Confirm 1,000 gems?")]
    [TestCase(12500, "Confirm 12,500 gems?")]
    public void ConfirmGems_IsTheShared2TapCopy(int cost, string expected) =>
        Assert.AreEqual(expected, StoreCopy.ConfirmGems(cost));

    [Test]
    public void PassDescription_IsShort() => Assert.AreEqual("No ads. Ever.", StoreDefaults.PassDescription);
}
