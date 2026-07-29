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

    [Test]
    public void JarStackValue_SumsLeadingJars()
    {
        var values = new[] { 100, 250, 40, 900 };
        Assert.AreEqual(0,   InventoryMath.JarStackValue(values, 0));
        Assert.AreEqual(100, InventoryMath.JarStackValue(values, 1));
        Assert.AreEqual(350, InventoryMath.JarStackValue(values, 2));
        Assert.AreEqual(390, InventoryMath.JarStackValue(values, 3));
    }

    [Test]
    public void JarStackValue_ClampsAboveCount()
    {
        var values = new[] { 100, 250, 40 };
        Assert.AreEqual(390, InventoryMath.JarStackValue(values, 99));
    }

    [Test]
    public void JarStackValue_NonPositiveOrEmptyPaysNothing()
    {
        var values = new[] { 100, 250 };
        Assert.AreEqual(0, InventoryMath.JarStackValue(values, -5));
        Assert.AreEqual(0, InventoryMath.JarStackValue(new int[0], 3));
        Assert.AreEqual(0, InventoryMath.JarStackValue(null, 3));
    }

    [Test]
    public void JarStackValue_IgnoresNegativeJarValues()
    {
        var values = new[] { 100, -50, 25 };
        Assert.AreEqual(125, InventoryMath.JarStackValue(values, 3));
    }
}
