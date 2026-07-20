using NUnit.Framework;

public class InventoryMathTests
{
    [Test]
    public void RawCropCoinValue_ScalesAndRounds()
    {
        Assert.AreEqual(15, InventoryMath.RawCropCoinValue(10, 1.5f));
        Assert.AreEqual(8,  InventoryMath.RawCropCoinValue(5, 1.5f));   // 7.5 rounds to 8
    }

    [Test]
    public void RawCropCoinValue_NeverBelowOne()
    {
        Assert.AreEqual(1, InventoryMath.RawCropCoinValue(1, 0.1f));
        Assert.AreEqual(1, InventoryMath.RawCropCoinValue(0, 1.5f));
    }

    [Test]
    public void AddToCap_StoresWhenUnderCap()
    {
        int stored = InventoryMath.AddToCap(10, 5, 500, out int overflow);
        Assert.AreEqual(5, stored);
        Assert.AreEqual(0, overflow);
    }

    [Test]
    public void AddToCap_SplitsAtCap()
    {
        int stored = InventoryMath.AddToCap(498, 5, 500, out int overflow);
        Assert.AreEqual(2, stored);
        Assert.AreEqual(3, overflow);
    }

    [Test]
    public void AddToCap_FullStackStoresNothing()
    {
        int stored = InventoryMath.AddToCap(500, 3, 500, out int overflow);
        Assert.AreEqual(0, stored);
        Assert.AreEqual(3, overflow);
    }

    [Test]
    public void AddToCap_GuardsNegativeAdd()
    {
        int stored = InventoryMath.AddToCap(10, -5, 500, out int overflow);
        Assert.AreEqual(0, stored);
        Assert.AreEqual(0, overflow);
    }
}
