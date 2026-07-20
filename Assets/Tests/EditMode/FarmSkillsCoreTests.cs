using NUnit.Framework;

public class FarmSkillsCoreTests
{
    [Test]
    public void NewCore_AllTracksStartAtZero()
    {
        var core = new FarmSkillsCore();
        foreach (FarmSkillTrack t in System.Enum.GetValues(typeof(FarmSkillTrack)))
            Assert.AreEqual(0, core.GetLevel(t));
        Assert.AreEqual(0, core.TotalLevels);
    }

    [Test]
    public void TryLevelUp_IncrementsAndReturnsTrue()
    {
        var core = new FarmSkillsCore();
        bool ok = core.TryLevelUp(FarmSkillTrack.Harvesting);
        Assert.IsTrue(ok);
        Assert.AreEqual(1, core.GetLevel(FarmSkillTrack.Harvesting));
        Assert.AreEqual(1, core.TotalLevels);
    }

    [Test]
    public void TryLevelUp_CapsAt25()
    {
        var core = new FarmSkillsCore();
        for (int i = 0; i < 25; i++) Assert.IsTrue(core.TryLevelUp(FarmSkillTrack.Fishing));
        Assert.AreEqual(25, core.GetLevel(FarmSkillTrack.Fishing));
        Assert.IsFalse(core.TryLevelUp(FarmSkillTrack.Fishing));
        Assert.AreEqual(25, core.GetLevel(FarmSkillTrack.Fishing));
    }

    [Test]
    public void TotalLevels_SumsAllSevenTracks()
    {
        var core = new FarmSkillsCore();
        core.TryLevelUp(FarmSkillTrack.Harvesting);
        core.TryLevelUp(FarmSkillTrack.Harvesting);
        core.TryLevelUp(FarmSkillTrack.Ranching);
        Assert.AreEqual(3, core.TotalLevels);
    }

    [Test]
    public void ExportImport_RoundTrips()
    {
        var core = new FarmSkillsCore();
        core.TryLevelUp(FarmSkillTrack.Processing);
        core.TryLevelUp(FarmSkillTrack.Processing);
        core.TryLevelUp(FarmSkillTrack.Watering);
        var exported = core.Export();

        var core2 = new FarmSkillsCore();
        core2.Import(exported);
        Assert.AreEqual(2, core2.GetLevel(FarmSkillTrack.Processing));
        Assert.AreEqual(1, core2.GetLevel(FarmSkillTrack.Watering));
        Assert.AreEqual(3, core2.TotalLevels);
    }

    [Test]
    public void Import_NullOrShortArray_IsSafe()
    {
        var core = new FarmSkillsCore();
        core.Import(null);
        Assert.AreEqual(0, core.TotalLevels);
        core.Import(new[] { 5, 3 }); // shorter than 7
        Assert.AreEqual(5, core.GetLevel(FarmSkillTrack.Harvesting));
        Assert.AreEqual(3, core.GetLevel(FarmSkillTrack.Planting));
        Assert.AreEqual(0, core.GetLevel(FarmSkillTrack.Processing));
    }
}
