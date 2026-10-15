using System;
using System.IO;
using System.Reflection;
using MysteryGame.Core;
using NUnit.Framework;
using UnityEngine;

public class AtomicSaveStoreTests
{
    string directory, path;
    static bool Valid(string value) => value.StartsWith("valid-");
    [SetUp] public void Setup()
    {
        directory = Path.GetFullPath(Path.Combine(Application.dataPath, "../Logs/AtomicSaveTests_" + Guid.NewGuid().ToString("N")));
        Directory.CreateDirectory(directory); path = Path.Combine(directory, "save.json");
    }
    [TearDown] public void Cleanup()
    {
        AtomicSaveStore.Delete(path); Directory.Delete(directory);
    }
    string Read(out bool recovered)
    { string value; Assert.That(AtomicSaveStore.TryRead(path, Valid, out value, out recovered), Is.True); return value; }
    [Test] public void FirstAndRepeatedCommitsKeepThePreviousValidSave()
    {
        AtomicSaveStore.Write(path, "valid-first", Valid);
        Assert.That(File.Exists(path + ".tmp"), Is.False); Assert.That(File.Exists(path + ".bak"), Is.False);
        AtomicSaveStore.Write(path, "valid-second", Valid);
        Assert.That(File.ReadAllText(path + ".bak"), Is.EqualTo("valid-first"));
        AtomicSaveStore.Write(path, "valid-third", Valid);
        bool recovered; Assert.That(Read(out recovered), Is.EqualTo("valid-third")); Assert.That(recovered, Is.False);
        Assert.That(File.ReadAllText(path + ".bak"), Is.EqualTo("valid-second"));
    }
    [Test] public void CorruptOrMissingPrimaryRecoversFromBackupNotFromAnUncommittedTemp()
    {
        AtomicSaveStore.Write(path, "valid-first", Valid); AtomicSaveStore.Write(path, "valid-second", Valid);
        File.WriteAllText(path, "broken"); File.WriteAllText(path + ".tmp", "valid-uncommitted");
        bool recovered; Assert.That(Read(out recovered), Is.EqualTo("valid-first")); Assert.That(recovered, Is.True);
        File.Delete(path); Assert.That(Read(out recovered), Is.EqualTo("valid-first")); Assert.That(recovered, Is.True);
    }
    [Test] public void SavingAfterRecoveryNeverReplacesAGoodBackupWithCorruptPrimary()
    {
        AtomicSaveStore.Write(path, "valid-first", Valid); AtomicSaveStore.Write(path, "valid-second", Valid);
        File.WriteAllText(path, "broken"); AtomicSaveStore.Write(path, "valid-recovered", Valid);
        Assert.That(File.ReadAllText(path), Is.EqualTo("valid-recovered"));
        Assert.That(File.ReadAllText(path + ".bak"), Is.EqualTo("valid-first"));
    }
    [Test] public void InvalidNewDataCannotModifyAnyCommittedSave()
    {
        AtomicSaveStore.Write(path, "valid-first", Valid);
        Assert.Throws<InvalidDataException>(() => AtomicSaveStore.Write(path, "broken", Valid));
        Assert.That(File.ReadAllText(path), Is.EqualTo("valid-first")); Assert.That(File.Exists(path + ".tmp"), Is.False);
    }
    [Test] public void FailedAtomicReplacementPreservesPrimaryBackupAndCleansTemporary()
    {
        AtomicSaveStore.Write(path, "valid-first", Valid); AtomicSaveStore.Write(path, "valid-second", Valid);
        using (var locked = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.None))
            Assert.Throws<IOException>(() => AtomicSaveStore.Write(path, "valid-third", Valid));
        Assert.That(File.ReadAllText(path), Is.EqualTo("valid-second"));
        Assert.That(File.ReadAllText(path + ".bak"), Is.EqualTo("valid-first"));
        Assert.That(File.Exists(path + ".tmp"), Is.False);
    }
    [Test] public void DeleteRemovesBackupAndTemporarySoNewGameCannotResurrectOldProgress()
    {
        AtomicSaveStore.Write(path, "valid-first", Valid); AtomicSaveStore.Write(path, "valid-second", Valid);
        File.WriteAllText(path + ".tmp", "valid-stale"); AtomicSaveStore.Delete(path);
        string value; bool recovered;
        Assert.That(AtomicSaveStore.TryRead(path, Valid, out value, out recovered), Is.False);
        Assert.That(File.Exists(path + ".tmp"), Is.False);
    }
    [Serializable] class Envelope { public int version = 1; public string savedAt = "test"; public StateSnapshot state = new StateSnapshot { CurrentSceneId = "Room01" }; }
    [Test] public void SaveSystemUsesTheSameValidatedBackupReadPathForRealJson()
    {
        var validate = typeof(SaveSystem).GetMethod("IsValidSaveJson", BindingFlags.Static | BindingFlags.NonPublic);
        Func<string, bool> valid = json => (bool)validate.Invoke(null, new object[] { json });
        string good = JsonUtility.ToJson(new Envelope()); AtomicSaveStore.Write(path, good, valid);
        AtomicSaveStore.Write(path, good, valid); File.WriteAllText(path, "{truncated");
        var read = typeof(SaveSystem).GetMethod("ReadFromPath", BindingFlags.Static | BindingFlags.NonPublic);
        Assert.That(read.Invoke(null, new object[] { path }), Is.Not.Null);
        Assert.That(SaveSystem.RecoveredFromBackup, Is.True); Assert.That(SaveSystem.LastError, Is.Null);
        File.WriteAllText(path + ".bak", JsonUtility.ToJson(new Envelope { version = 999 }));
        Assert.That(read.Invoke(null, new object[] { path }), Is.Null);
        Assert.That(SaveSystem.RecoveredFromBackup, Is.False); Assert.That(SaveSystem.LastError, Is.Not.Empty);
    }
}
