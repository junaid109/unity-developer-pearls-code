using System;
using System.Collections;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using NUnit.Framework;
using Pearls.ConsoleUtils;
using UnityEngine;
using UnityEngine.TestTools;

namespace Pearls.ConsoleUtils.Tests
{
    /// <summary>
    /// Exercises the real save files in Application.persistentDataPath for this
    /// companion project. Each test starts and ends with no save files.
    /// </summary>
    public class AdvancedSafeSaveSystemTests
    {
        private static string Dir => Application.persistentDataPath;
        private static string Main => Path.Combine(Dir, "userdata.dat");
        private static string Temp => Path.Combine(Dir, "userdata.tmp");
        private static string Backup => Path.Combine(Dir, "userdata.bak");

        private static byte[] Bytes(string s) => Encoding.UTF8.GetBytes(s);
        private static string Text(byte[] b) => b == null ? null : Encoding.UTF8.GetString(b);

        private static void DeleteAll()
        {
            foreach (var p in new[] { Main, Temp, Backup })
                if (File.Exists(p)) File.Delete(p);
        }

        [SetUp] public void SetUp() { AdvancedSafeSaveSystem.FaultInjection = null; DeleteAll(); }
        [TearDown] public void TearDown() { AdvancedSafeSaveSystem.FaultInjection = null; DeleteAll(); }

        /// <summary>Waits for a Task across editor frames so Unity's synchronization context can run continuations.</summary>
        private static IEnumerator Await<T>(Task<T> task, Action<T> onResult)
        {
            float deadline = Time.realtimeSinceStartup + 10f;
            while (!task.IsCompleted)
            {
                if (Time.realtimeSinceStartup > deadline) Assert.Fail("Task did not finish within 10 s.");
                yield return null;
            }
            if (task.IsFaulted) throw task.Exception;
            onResult(task.Result);
        }

        [UnityTest]
        public IEnumerator NoFiles_LoadReturnsNull()
        {
            byte[] loaded = Bytes("sentinel");
            yield return Await(AdvancedSafeSaveSystem.LoadDataAsync(), r => loaded = r);
            Assert.IsNull(loaded);
        }

        [UnityTest]
        public IEnumerator SaveThenLoad_RoundTrips_AndLeavesNoTempFile()
        {
            bool ok = false;
            yield return Await(AdvancedSafeSaveSystem.SaveDataAsync(Bytes("v1")), r => ok = r);
            Assert.IsTrue(ok);
            Assert.IsFalse(File.Exists(Temp));

            byte[] loaded = null;
            yield return Await(AdvancedSafeSaveSystem.LoadDataAsync(), r => loaded = r);
            Assert.AreEqual("v1", Text(loaded));
        }

        [UnityTest]
        public IEnumerator SecondSave_KeepsPreviousVersionAsBackup()
        {
            yield return Await(AdvancedSafeSaveSystem.SaveDataAsync(Bytes("v1")), _ => { });
            yield return Await(AdvancedSafeSaveSystem.SaveDataAsync(Bytes("v2")), _ => { });

            Assert.IsTrue(File.Exists(Backup), "File.Replace should have moved v1 to the backup.");

            byte[] loaded = null;
            yield return Await(AdvancedSafeSaveSystem.LoadDataAsync(), r => loaded = r);
            Assert.AreEqual("v2", Text(loaded));
        }

        [UnityTest]
        public IEnumerator CrashBeforeReplace_KeepsTheOldSave()
        {
            yield return Await(AdvancedSafeSaveSystem.SaveDataAsync(Bytes("v1")), _ => { });

            AdvancedSafeSaveSystem.FaultInjection = stage =>
            {
                if (stage == "BeforeReplace") throw new IOException("simulated power cut");
            };
            LogAssert.Expect(LogType.Error, new Regex("Save failed: simulated power cut"));

            bool ok = true;
            yield return Await(AdvancedSafeSaveSystem.SaveDataAsync(Bytes("v2")), r => ok = r);
            Assert.IsFalse(ok);

            byte[] loaded = null;
            yield return Await(AdvancedSafeSaveSystem.LoadDataAsync(), r => loaded = r);
            Assert.AreEqual("v1", Text(loaded), "Main was never touched, so it still wins.");
        }

        [UnityTest]
        public IEnumerator CrashAfterFirstTempWrite_RecoversFromTempFile()
        {
            AdvancedSafeSaveSystem.FaultInjection = stage =>
            {
                if (stage == "AfterTempWrite") throw new IOException("simulated crash");
            };
            LogAssert.Expect(LogType.Error, new Regex("Save failed"));
            yield return Await(AdvancedSafeSaveSystem.SaveDataAsync(Bytes("first")), _ => { });
            AdvancedSafeSaveSystem.FaultInjection = null;

            Assert.IsFalse(File.Exists(Main));
            byte[] loaded = null;
            yield return Await(AdvancedSafeSaveSystem.LoadDataAsync(), r => loaded = r);
            Assert.AreEqual("first", Text(loaded), "A fully written, hash-valid temp file is recovered.");
        }

        [UnityTest]
        public IEnumerator CorruptMain_FallsBackToBackup()
        {
            yield return Await(AdvancedSafeSaveSystem.SaveDataAsync(Bytes("v1")), _ => { });
            yield return Await(AdvancedSafeSaveSystem.SaveDataAsync(Bytes("v2")), _ => { });

            byte[] raw = File.ReadAllBytes(Main);
            raw[0] ^= 0xFF; // one flipped bit-pattern in the payload
            File.WriteAllBytes(Main, raw);

            byte[] loaded = null;
            yield return Await(AdvancedSafeSaveSystem.LoadDataAsync(), r => loaded = r);
            Assert.AreEqual("v1", Text(loaded));
        }
    }
}
