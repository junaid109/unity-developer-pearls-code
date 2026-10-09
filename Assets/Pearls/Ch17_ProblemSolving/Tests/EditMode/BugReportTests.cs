using System;
using System.Collections.Generic;
using NUnit.Framework;
using Pearls.Stuck;
using UnityEngine;

namespace Pearls.Stuck.Tests
{
    public class LogRingBufferTests
    {
        [Test]
        public void KeepsTheNewestLines_OldestFirst()
        {
            var buffer = new LogRingBuffer(3);
            foreach (var line in new[] { "1", "2", "3", "4", "5" }) buffer.Add(line);
            CollectionAssert.AreEqual(new[] { "3", "4", "5" }, buffer.Snapshot());
        }

        [Test]
        public void PartlyFilled_ReturnsOnlyWhatWasAdded()
        {
            var buffer = new LogRingBuffer(5);
            buffer.Add("a");
            buffer.Add(null);
            CollectionAssert.AreEqual(new[] { "a", "" }, buffer.Snapshot());
        }

        [Test]
        public void SnapshotIsACopy()
        {
            var buffer = new LogRingBuffer(2);
            buffer.Add("a");
            var snap = buffer.Snapshot();
            buffer.Add("b");
            Assert.AreEqual(1, snap.Count);
        }

        [Test]
        public void ZeroCapacity_Throws()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new LogRingBuffer(0));
        }
    }

    public class BugReportFormatterTests
    {
        [Test]
        public void EmptyDraft_MarksEveryHumanSectionAsTodo()
        {
            string md = BugReportFormatter.ToMarkdown(new BugReportDraft(), new EnvironmentFacts(), null);

            StringAssert.StartsWith("## _TODO: fill in_", md);
            Assert.AreEqual(7, CountOf(md, "_TODO: fill in_"), "Summary, 4 sections and 2 lists.");
            StringAssert.Contains("### Last 0 log lines", md);
        }

        [Test]
        public void FilledDraft_NumbersStepsAndBulletsTriedItems_AndFencesLogs()
        {
            var draft = new BugReportDraft
            {
                Summary = "  Crash on resume  ",
                Steps = new List<string> { "Open app", "Background it" },
                Tried = new List<string> { "Clean build" }
            };
            var env = new EnvironmentFacts { UnityVersion = "6000.4.2f1", GitCommit = "abc123" };

            string md = BugReportFormatter.ToMarkdown(draft, env, new[] { "[Error] boom" });

            StringAssert.StartsWith("## Crash on resume" + Environment.NewLine, md);
            StringAssert.Contains("1. Open app", md);
            StringAssert.Contains("2. Background it", md);
            StringAssert.Contains("- Clean build", md);
            StringAssert.Contains("- Unity 6000.4.2f1, app unknown, commit abc123", md);
            StringAssert.Contains("```" + Environment.NewLine + "[Error] boom" + Environment.NewLine + "```", md);
        }

        [Test]
        public void ListsHaveABlankLineBeforeThem()
        {
            var draft = new BugReportDraft { Steps = new List<string> { "x" } };
            string md = BugReportFormatter.ToMarkdown(draft, new EnvironmentFacts(), null);
            string nl = Environment.NewLine;
            StringAssert.Contains("### Steps to reproduce" + nl + nl + "1. x", md);
        }

        [Test]
        public void NullDraftOrFacts_Throw()
        {
            Assert.Throws<ArgumentNullException>(() => BugReportFormatter.ToMarkdown(null, new EnvironmentFacts(), null));
            Assert.Throws<ArgumentNullException>(() => BugReportFormatter.ToMarkdown(new BugReportDraft(), null, null));
        }

        private static int CountOf(string text, string token)
        {
            int count = 0, i = 0;
            while ((i = text.IndexOf(token, i, StringComparison.Ordinal)) >= 0) { count++; i += token.Length; }
            return count;
        }
    }

    public class BugReportCollectorTests
    {
        [Test]
        public void CopyReport_WorksInEditMode_WhereAwakeHasNotRun()
        {
            // [ContextMenu] runs in Edit Mode. Awake does not run there for a plain
            // MonoBehaviour, so anything Awake sets up is still null.
            var go = new GameObject("Collector");
            try
            {
                var collector = go.AddComponent<BugReportCollector>();
                string md = collector.CopyReport();
                StringAssert.Contains("### Environment", md);
                StringAssert.Contains("Unity " + Application.unityVersion, md);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
            }
        }
    }
}
