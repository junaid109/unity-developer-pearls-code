using System.IO;
using System.Linq;
using NUnit.Framework;
using Pearls.Kiosk;

namespace Pearls.Kiosk.Tests
{
    public class OfflinePersistenceTests
    {
        private string[] _before;

        [SetUp]
        public void SetUp()
        {
            Directory.CreateDirectory(OfflinePersistence.QueueDir);
            _before = Directory.GetFiles(OfflinePersistence.QueueDir);
        }

        [TearDown]
        public void TearDown()
        {
            // Remove only what this test created.
            foreach (var f in Directory.GetFiles(OfflinePersistence.QueueDir).Except(_before))
                File.Delete(f);
        }

        [Test]
        public void Enqueue_WritesOneCompleteJsonFile_AndNoTempFile()
        {
            OfflinePersistence.Enqueue("{\"lead\":\"a@example.com\"}");

            var created = Directory.GetFiles(OfflinePersistence.QueueDir).Except(_before).ToArray();
            Assert.AreEqual(1, created.Length);
            StringAssert.EndsWith(".json", created[0]);
            Assert.AreEqual("{\"lead\":\"a@example.com\"}", File.ReadAllText(created[0]));
        }

        [Test]
        public void TwoEnqueues_NeverOverwriteEachOther()
        {
            OfflinePersistence.Enqueue("{\"n\":1}");
            OfflinePersistence.Enqueue("{\"n\":2}");

            var created = Directory.GetFiles(OfflinePersistence.QueueDir, "*.json").Except(_before).ToArray();
            Assert.AreEqual(2, created.Length);
        }
    }
}
