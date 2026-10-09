using System.Linq;
using NUnit.Framework;
using Pearls.Generative;

namespace Pearls.Generative.Tests
{
    public class GeneratedAssetRulesTests
    {
        private static AssetFacts CleanFacts() => new AssetFacts
        {
            FileName = "GEN_Crate.fbx",
            TriangleCount = 5000,
            MaxExtentMeters = 1.2f,
            AllMeshesHaveUVs = true,
            MaterialCount = 1,
            HasProvenanceSidecar = true
        };

        [Test]
        public void CleanAsset_HasNoFindings()
        {
            var findings = GeneratedAssetRules.Evaluate(CleanFacts(), new AssetBudget());
            Assert.IsEmpty(findings);
            Assert.IsFalse(GeneratedAssetRules.HasErrors(findings));
        }

        [Test]
        public void MissingSidecar_IsAnError()
        {
            var f = CleanFacts();
            f.HasProvenanceSidecar = false;
            var findings = GeneratedAssetRules.Evaluate(f, new AssetBudget());
            Assert.AreEqual(1, findings.Count);
            Assert.AreEqual(Severity.Error, findings[0].Severity);
            StringAssert.Contains("provenance", findings[0].Message);
        }

        [Test]
        public void WrongPrefix_IsOnlyAWarning()
        {
            var f = CleanFacts();
            f.FileName = "Crate.fbx";
            var findings = GeneratedAssetRules.Evaluate(f, new AssetBudget());
            Assert.AreEqual(Severity.Warning, findings.Single().Severity);
            Assert.IsFalse(GeneratedAssetRules.HasErrors(findings));
        }

        [TestCase(20000, false)]
        [TestCase(20001, true)]
        public void TriangleBudget_IsInclusive(long triangles, bool expectError)
        {
            var f = CleanFacts();
            f.TriangleCount = triangles;
            Assert.AreEqual(expectError, GeneratedAssetRules.HasErrors(GeneratedAssetRules.Evaluate(f, new AssetBudget())));
        }

        [TestCase(0.01f)]   // centimetre export read as metres the other way round
        [TestCase(120f)]    // a 1.2 m crate imported in centimetres
        public void OutOfRangeSize_WarnsAboutUnits(float extent)
        {
            var f = CleanFacts();
            f.MaxExtentMeters = extent;
            var finding = GeneratedAssetRules.Evaluate(f, new AssetBudget()).Single();
            Assert.AreEqual(Severity.Warning, finding.Severity);
            StringAssert.Contains("units", finding.Message);
        }

        [Test]
        public void MissingUVs_AndTooManyMaterials_AreReportedTogether()
        {
            var f = CleanFacts();
            f.AllMeshesHaveUVs = false;
            f.MaterialCount = 5;
            var findings = GeneratedAssetRules.Evaluate(f, new AssetBudget());
            Assert.AreEqual(2, findings.Count);
            Assert.AreEqual(1, findings.Count(x => x.Severity == Severity.Error));
            Assert.AreEqual(1, findings.Count(x => x.Severity == Severity.Warning));
        }
    }
}
