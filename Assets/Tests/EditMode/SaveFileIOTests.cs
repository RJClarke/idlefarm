using System.IO;
using NUnit.Framework;

public class SaveFileIOTests
{
    private string _dir;
    private string _path;

    [SetUp]
    public void SetUp()
    {
        _dir = Path.Combine(Path.GetTempPath(), "SaveFileIOTests_" + Path.GetRandomFileName());
        Directory.CreateDirectory(_dir);
        _path = Path.Combine(_dir, "gamedata.json");
    }

    [TearDown]
    public void TearDown()
    {
        if (Directory.Exists(_dir)) Directory.Delete(_dir, true);
    }

    // Accept anything non-null/non-empty; corruption is simulated by rejecting a sentinel string.
    private static bool AlwaysValid(string s) => true;
    private static bool RejectCorrupt(string s) => s != "CORRUPT";

    [Test]
    public void WriteAtomic_CreatesFile()
    {
        SaveFileIO.WriteAtomic(_path, "hello");

        Assert.IsTrue(File.Exists(_path), "primary file should exist after write");
        Assert.AreEqual("hello", File.ReadAllText(_path));
        Assert.IsFalse(File.Exists(_path + SaveFileIO.TmpSuffix), "temp file should be gone after write");
    }

    [Test]
    public void WriteAtomic_FirstWrite_NoBackupCreated()
    {
        SaveFileIO.WriteAtomic(_path, "first");

        Assert.IsFalse(File.Exists(_path + SaveFileIO.BakSuffix), "first-ever write should not create a .bak");
    }

    [Test]
    public void WriteAtomic_SecondWrite_BackupHasPriorContents()
    {
        SaveFileIO.WriteAtomic(_path, "v1");
        SaveFileIO.WriteAtomic(_path, "v2");

        Assert.AreEqual("v2", File.ReadAllText(_path), "primary should hold the newest contents");
        Assert.IsTrue(File.Exists(_path + SaveFileIO.BakSuffix), "second write should create a .bak");
        Assert.AreEqual("v1", File.ReadAllText(_path + SaveFileIO.BakSuffix), ".bak should hold the prior contents");
    }

    [Test]
    public void WriteAtomic_RemovesStrayTemp()
    {
        File.WriteAllText(_path + SaveFileIO.TmpSuffix, "leftover");

        SaveFileIO.WriteAtomic(_path, "clean");

        Assert.AreEqual("clean", File.ReadAllText(_path));
        Assert.IsFalse(File.Exists(_path + SaveFileIO.TmpSuffix), "stray temp file should be cleaned up");
    }

    [Test]
    public void ReadWithFallback_ReadsPrimary()
    {
        File.WriteAllText(_path, "good");

        bool ok = SaveFileIO.ReadWithFallback(_path, AlwaysValid, out string contents, out bool usedBackup);

        Assert.IsTrue(ok);
        Assert.AreEqual("good", contents);
        Assert.IsFalse(usedBackup);
    }

    [Test]
    public void ReadWithFallback_CorruptPrimary_ValidBackup_FallsBack()
    {
        File.WriteAllText(_path, "CORRUPT");
        File.WriteAllText(_path + SaveFileIO.BakSuffix, "good-backup");

        bool ok = SaveFileIO.ReadWithFallback(_path, RejectCorrupt, out string contents, out bool usedBackup);

        Assert.IsTrue(ok);
        Assert.AreEqual("good-backup", contents);
        Assert.IsTrue(usedBackup, "should report that the backup was used");
    }

    [Test]
    public void ReadWithFallback_MissingPrimary_ValidBackup_FallsBack()
    {
        File.WriteAllText(_path + SaveFileIO.BakSuffix, "good-backup");

        bool ok = SaveFileIO.ReadWithFallback(_path, AlwaysValid, out string contents, out bool usedBackup);

        Assert.IsTrue(ok);
        Assert.AreEqual("good-backup", contents);
        Assert.IsTrue(usedBackup);
    }

    [Test]
    public void ReadWithFallback_BothCorrupt_ReturnsFalse()
    {
        File.WriteAllText(_path, "CORRUPT");
        File.WriteAllText(_path + SaveFileIO.BakSuffix, "CORRUPT");

        bool ok = SaveFileIO.ReadWithFallback(_path, RejectCorrupt, out string contents, out bool usedBackup);

        Assert.IsFalse(ok);
        Assert.IsNull(contents);
        Assert.IsFalse(usedBackup);
    }

    [Test]
    public void ReadWithFallback_NeitherExists_ReturnsFalse()
    {
        bool ok = SaveFileIO.ReadWithFallback(_path, AlwaysValid, out string contents, out bool usedBackup);

        Assert.IsFalse(ok);
        Assert.IsNull(contents);
        Assert.IsFalse(usedBackup);
    }

    [Test]
    public void ReadWithFallback_EmptyPrimary_ValidBackup_FallsBack()
    {
        File.WriteAllText(_path, "");
        File.WriteAllText(_path + SaveFileIO.BakSuffix, "good-backup");

        bool ok = SaveFileIO.ReadWithFallback(_path, AlwaysValid, out string contents, out bool usedBackup);

        Assert.IsTrue(ok);
        Assert.AreEqual("good-backup", contents);
        Assert.IsTrue(usedBackup);
    }

    [Test]
    public void WriteThenReadRoundTrip_SurvivesCorruptedPrimary()
    {
        // Simulate: save v1, save v2 (v1 -> .bak), primary later corrupted on disk.
        SaveFileIO.WriteAtomic(_path, "v1");
        SaveFileIO.WriteAtomic(_path, "v2");
        File.WriteAllText(_path, "CORRUPT");

        bool ok = SaveFileIO.ReadWithFallback(_path, RejectCorrupt, out string contents, out bool usedBackup);

        Assert.IsTrue(ok);
        Assert.AreEqual("v1", contents, "should recover the prior good save from .bak");
        Assert.IsTrue(usedBackup);
    }
}
