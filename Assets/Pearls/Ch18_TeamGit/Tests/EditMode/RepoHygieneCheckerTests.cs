using System.Linq;
using NUnit.Framework;
using Pearls.Team;

namespace Pearls.Team.Tests
{
    public class RepoHygieneCheckerTests
    {
        // The relevant lines of GitHub's standard Unity.gitignore template.
        private static readonly string[] StandardUnityGitIgnore =
        {
            "# Unity .gitignore Template",
            "/[Ll]ibrary/",
            "/[Tt]emp/",
            "/[Oo]bj/",
            "/[Bb]uild/",
            "/[Ll]ogs/",
            "/[Uu]ser[Ss]ettings/",
            "*.csproj",
            "*.tmp"
        };

        private static RepoSnapshot Healthy() => new RepoSnapshot
        {
            SerializationMode = "ForceText",
            VersionControlMode = "Visible Meta Files",
            GitIgnoreLines = StandardUnityGitIgnore,
            GitAttributesLines = new[] { "*.fbx filter=lfs diff=lfs merge=lfs -text" },
            AssetPaths = new[] { "Assets/Models", "Assets/Models.meta", "Assets/Models/Crane.fbx", "Assets/Models/Crane.fbx.meta" }
        };

        private static string[] Rules(RepoSnapshot s) =>
            RepoHygieneChecker.CreateDefault().Run(s).Select(f => f.Rule).ToArray();

        [Test]
        public void HealthyProject_WithTheStandardUnityGitIgnore_HasNoFindings()
        {
            var findings = RepoHygieneChecker.CreateDefault().Run(Healthy());
            Assert.IsEmpty(findings, string.Join("\n", findings.Select(f => f.Message)));
        }

        [Test]
        public void GitIgnore_SubstringLookalikes_DoNotCountAsIgnoringTheFolder()
        {
            var s = Healthy();
            s.GitIgnoreLines = new[] { "/[Ll]ibrary/", "/[Ll]ogs/", "Assets/Templates/", "*.objx" };

            var messages = new GitIgnoreRule().Evaluate(s).Select(f => f.Message).ToArray();

            Assert.AreEqual(2, messages.Length, string.Join("\n", messages));
            Assert.IsTrue(messages.Any(m => m.StartsWith("'temp/'")), "'Templates' is not 'Temp'.");
            Assert.IsTrue(messages.Any(m => m.StartsWith("'obj/'")), "'*.objx' is not the obj folder.");
        }

        [TestCase("Library/")]
        [TestCase("library")]
        [TestCase("**/Library/")]
        [TestCase("/[Ll]ibrary/")]
        public void GitIgnore_AcceptsCommonSpellingsOfTheSameRule(string libraryLine)
        {
            var s = Healthy();
            s.GitIgnoreLines = new[] { libraryLine, "/[Tt]emp/", "/[Oo]bj/", "/[Ll]ogs/" };
            Assert.IsEmpty(new GitIgnoreRule().Evaluate(s).ToArray());
        }

        [Test]
        public void GitIgnore_NestedFolderWithTheSameName_DoesNotCoverTheRootFolder()
        {
            var s = Healthy();
            s.GitIgnoreLines = new[] { "Assets/Library/", "/[Tt]emp/", "/[Oo]bj/", "/[Ll]ogs/" };
            Assert.AreEqual(1, new GitIgnoreRule().Evaluate(s).Count());
        }

        [Test]
        public void IgnoringMetaFiles_IsAnError()
        {
            var s = Healthy();
            s.GitIgnoreLines = StandardUnityGitIgnore.Concat(new[] { "*.meta" }).ToArray();
            CollectionAssert.AreEqual(new[] { "gitignore" }, Rules(s));
        }

        [Test]
        public void BinarySerialization_AndHiddenMeta_AreErrors()
        {
            var s = Healthy();
            s.SerializationMode = "ForceBinary";
            s.VersionControlMode = "Hidden Meta Files";
            CollectionAssert.AreEquivalent(new[] { "force-text", "visible-meta" }, Rules(s));
        }

        [Test]
        public void FbxWithoutLfs_IsAWarning()
        {
            var s = Healthy();
            s.GitAttributesLines = new string[0];
            var finding = RepoHygieneChecker.CreateDefault().Run(s).Single();
            Assert.AreEqual("lfs-binaries", finding.Rule);
            Assert.AreEqual(Severity.Warning, finding.Severity);
        }

        [Test]
        public void OrphanMeta_AndMissingMeta_AreBothCaught_TildeFoldersAreSkipped()
        {
            var s = Healthy();
            s.AssetPaths = new[]
            {
                "Assets/Gone.png.meta",          // asset deleted, meta left behind
                "Assets/New.png",                // asset added without its meta
                "Assets/Samples~", "Assets/Samples~/Demo.cs" // Unity never imports ~ folders
            };

            var findings = new OrphanMetaRule().Evaluate(s).ToArray();

            Assert.AreEqual(2, findings.Length);
            Assert.AreEqual(1, findings.Count(f => f.Severity == Severity.Warning && f.Message.Contains("Gone.png.meta")));
            Assert.AreEqual(1, findings.Count(f => f.Severity == Severity.Error && f.Message.Contains("New.png")));
        }
    }
}
