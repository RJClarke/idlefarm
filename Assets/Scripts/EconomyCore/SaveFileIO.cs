using System;
using System.IO;

/// <summary>
/// Atomic, crash-safe save file I/O. Lives in EconomyCore (System.IO only, no UnityEngine)
/// so it is unit-testable without a Unity scene. The caller supplies its own validity check
/// (e.g. a JsonUtility parse) via a delegate so this class never references Unity types.
///
/// Layout: the live save is at <c>path</c>; the previous good copy is kept at <c>path + ".bak"</c>;
/// a transient <c>path + ".tmp"</c> holds an in-flight write. A crash mid-write can only ever
/// corrupt the .tmp, never the live file or the backup.
/// </summary>
public static class SaveFileIO
{
    public const string TmpSuffix = ".tmp";
    public const string BakSuffix = ".bak";

    /// <summary>
    /// Write <paramref name="contents"/> to <paramref name="path"/> atomically. The bytes are
    /// first written to <c>path + ".tmp"</c>, then swapped in: when <paramref name="path"/>
    /// already exists we use <see cref="File.Replace(string,string,string)"/> so the previous
    /// contents are preserved at <c>path + ".bak"</c>; on the first-ever write we just move the
    /// temp file into place. Any stray temp file from a prior interrupted write is removed first.
    /// </summary>
    public static void WriteAtomic(string path, string contents)
    {
        if (string.IsNullOrEmpty(path)) throw new ArgumentException("path is null or empty", nameof(path));

        string tmp = path + TmpSuffix;
        string bak = path + BakSuffix;

        // Clean up a stray temp file left over from a previous interrupted write.
        if (File.Exists(tmp)) File.Delete(tmp);

        File.WriteAllText(tmp, contents);

        if (File.Exists(path))
        {
            // Atomically swap tmp -> path, moving the old path to bak.
            File.Replace(tmp, path, bak);
        }
        else
        {
            // First-ever write: no existing file to back up.
            File.Move(tmp, path);
        }
    }

    /// <summary>
    /// Read <paramref name="path"/>, falling back to <c>path + ".bak"</c> when the primary file
    /// is missing, empty, or fails <paramref name="isValid"/>. Returns true and sets
    /// <paramref name="contents"/> to whichever copy succeeded; <paramref name="usedBackup"/> is
    /// true when the backup was the one that succeeded. Returns false (contents null) only when
    /// both copies are missing/unreadable/invalid.
    /// </summary>
    public static bool ReadWithFallback(string path, Func<string, bool> isValid, out string contents, out bool usedBackup)
    {
        contents = null;
        usedBackup = false;
        if (string.IsNullOrEmpty(path)) return false;

        if (TryReadValid(path, isValid, out string primary))
        {
            contents = primary;
            usedBackup = false;
            return true;
        }

        if (TryReadValid(path + BakSuffix, isValid, out string backup))
        {
            contents = backup;
            usedBackup = true;
            return true;
        }

        return false;
    }

    private static bool TryReadValid(string path, Func<string, bool> isValid, out string contents)
    {
        contents = null;
        try
        {
            if (!File.Exists(path)) return false;
            string text = File.ReadAllText(path);
            if (string.IsNullOrEmpty(text)) return false;
            if (isValid != null && !isValid(text)) return false;
            contents = text;
            return true;
        }
        catch (IOException)
        {
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
    }
}
