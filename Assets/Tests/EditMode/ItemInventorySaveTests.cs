using NUnit.Framework;

public class ItemInventorySaveTests
{
    private static ItemInventoryCore NewCore() => new ItemInventoryCore(500, 200);

    [Test]
    public void ExportImport_RoundTripsStacksAndToggle()
    {
        var inv = NewCore();
        inv.AddCrop("Blueberry", 40, out _);
        inv.AddCrop("Tomato", 3, out _);
        inv.AddEggs(7, out _);
        inv.CollectMode = true;

        var entries = inv.ExportCrops();

        var inv2 = NewCore();
        inv2.Import(entries, inv.Eggs, inv.CollectMode);

        Assert.AreEqual(40, inv2.GetCrop("Blueberry"));
        Assert.AreEqual(3,  inv2.GetCrop("Tomato"));
        Assert.AreEqual(0,  inv2.GetCrop("Strawberry"));
        Assert.AreEqual(7,  inv2.Eggs);
        Assert.IsTrue(inv2.CollectMode);
    }

    [Test]
    public void Import_LegacyNullStacks_IsEmpty()
    {
        var inv = NewCore();
        inv.AddCrop("Blueberry", 5, out _);
        inv.AddEggs(3, out _);
        inv.CollectMode = true;

        inv.Import(null, 0, false);

        Assert.AreEqual(0, inv.GetCrop("Blueberry"));
        Assert.AreEqual(0, inv.Eggs);
        Assert.IsFalse(inv.CollectMode);
    }

    [Test]
    public void Import_ClampsToCapsAndSkipsJunkEntries()
    {
        var inv = NewCore();
        inv.Import(new[]
        {
            new ItemStackEntry { itemId = "Blueberry", count = 9999 },
            new ItemStackEntry { itemId = "", count = 5 },
            null,
            new ItemStackEntry { itemId = "Tomato", count = -2 },
        }, 9999, true);

        Assert.AreEqual(500, inv.GetCrop("Blueberry")); // clamped to crop cap
        Assert.AreEqual(0,   inv.GetCrop("Tomato"));    // negative count skipped
        Assert.AreEqual(200, inv.Eggs);                 // clamped to egg cap
        Assert.IsTrue(inv.CollectMode);
    }

    [Test]
    public void AddCrop_RespectsCapAndReportsOverflow()
    {
        var inv = NewCore();
        inv.AddCrop("Blueberry", 499, out _);
        int stored = inv.AddCrop("Blueberry", 5, out int overflow);
        Assert.AreEqual(1, stored);
        Assert.AreEqual(4, overflow);
        Assert.AreEqual(500, inv.GetCrop("Blueberry"));
    }

    [Test]
    public void TrySpend_FailsWhenShort_SucceedsWhenStocked()
    {
        var inv = NewCore();
        inv.AddCrop("Tomato", 10, out _);
        Assert.IsFalse(inv.TrySpendCrop("Tomato", 11));
        Assert.AreEqual(10, inv.GetCrop("Tomato"));
        Assert.IsTrue(inv.TrySpendCrop("Tomato", 10));
        Assert.AreEqual(0, inv.GetCrop("Tomato"));

        inv.AddEggs(2, out _);
        Assert.IsFalse(inv.TrySpendEggs(3));
        Assert.IsTrue(inv.TrySpendEggs(2));
        Assert.AreEqual(0, inv.Eggs);
    }

    [Test]
    public void ExportCrops_OmitsEmptyStacks()
    {
        var inv = NewCore();
        inv.AddCrop("Blueberry", 4, out _);
        inv.AddCrop("Tomato", 2, out _);
        inv.TrySpendCrop("Tomato", 2);

        var entries = inv.ExportCrops();

        Assert.AreEqual(1, entries.Length);
        Assert.AreEqual("Blueberry", entries[0].itemId);
        Assert.AreEqual(4, entries[0].count);
    }
}
