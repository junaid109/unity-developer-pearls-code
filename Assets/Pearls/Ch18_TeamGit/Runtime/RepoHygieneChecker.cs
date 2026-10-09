using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace Pearls.Team
{
    public enum Severity { Warning, Error }

    public sealed class Finding
    {
        public Severity Severity { get; }
        public string Rule { get; }
        public string Message { get; }

        public Finding(Severity severity, string rule, string message)
        {
            Severity = severity;
            Rule = rule;
            Message = message;
        }
    }

    /// <summary>
    /// A read-only picture of the things the rules need. Built by the Editor layer
    /// in the real project and by hand in unit tests.
    /// </summary>
    public sealed class RepoSnapshot
    {
        public string SerializationMode { get; set; } = "";
        public string VersionControlMode { get; set; } = "";
        public IReadOnlyList<string> GitIgnoreLines { get; set; } = Array.Empty<string>();
        public IReadOnlyList<string> GitAttributesLines { get; set; } = Array.Empty<string>();

        // Every path under Assets/, with forward slashes, including .meta files.
        public IReadOnlyList<string> AssetPaths { get; set; } = Array.Empty<string>();
    }

    public interface IHygieneRule
    {
        string Name { get; }
        IEnumerable<Finding> Evaluate(RepoSnapshot snapshot);
    }

    public sealed class ForceTextRule : IHygieneRule
    {
        public string Name => "force-text";

        public IEnumerable<Finding> Evaluate(RepoSnapshot s)
        {
            if (!string.Equals(s.SerializationMode, "ForceText", StringComparison.OrdinalIgnoreCase))
            {
                yield return new Finding(Severity.Error, Name,
                    $"Asset serialization is '{s.SerializationMode}'. Set it to Force Text so scenes and prefabs can merge.");
            }
        }
    }

    public sealed class VisibleMetaRule : IHygieneRule
    {
        public string Name => "visible-meta";

        public IEnumerable<Finding> Evaluate(RepoSnapshot s)
        {
            if (!s.VersionControlMode.Contains("Visible Meta", StringComparison.OrdinalIgnoreCase))
            {
                yield return new Finding(Severity.Error, Name,
                    $"Version control mode is '{s.VersionControlMode}'. Use Visible Meta Files so GUIDs travel with assets.");
            }
        }
    }

    public sealed class GitIgnoreRule : IHygieneRule
    {
        public string Name => "gitignore";

        // Folders Unity regenerates on every machine.
        private static readonly string[] RequiredIgnores = { "library", "temp", "logs", "obj" };

        public IEnumerable<Finding> Evaluate(RepoSnapshot s)
        {
            var lines = s.GitIgnoreLines
                .Select(Normalise)
                .Where(l => l.Length > 0 && !l.StartsWith("#") && !l.StartsWith("!"))
                .ToList();

            foreach (var folder in RequiredIgnores)
            {
                bool covered = lines.Any(l => IgnoresRootFolder(l, folder));
                if (!covered)
                {
                    yield return new Finding(Severity.Error, Name,
                        $"'{folder}/' is not ignored. Generated folders should never be committed.");
                }
            }

            // Ignoring .meta files is the single most damaging .gitignore mistake in Unity.
            if (lines.Any(l => l == "*.meta" || l.EndsWith("/*.meta")))
            {
                yield return new Finding(Severity.Error, Name,
                    "'*.meta' is ignored. Every GUID reference will break on the next clone.");
            }
        }

        // GitHub's Unity template writes "/[Ll]ibrary/". Collapse one-letter case classes
        // such as [Ll] to a single letter, then lower-case: "/[Ll]ibrary/" -> "/library/".
        private static string Normalise(string line) =>
            Regex.Replace(line.Trim(), @"\[([A-Za-z])([A-Za-z])\]",
                m => char.ToLowerInvariant(m.Groups[1].Value[0]) == char.ToLowerInvariant(m.Groups[2].Value[0])
                    ? m.Groups[1].Value
                    : m.Value)
            .ToLowerInvariant();

        // Matches whole path segments, not substrings: "library", "library/", "/library/" and
        // "**/library/" all ignore the root Library folder; "assets/library/", "assets/templates/"
        // (which contains "temp") and "*.objx" (which contains "obj") do not.
        private static bool IgnoresRootFolder(string pattern, string folder)
        {
            string p = pattern.TrimEnd('/');
            if (p.StartsWith("**/")) p = p.Substring(3);
            return p.TrimStart('/') == folder;
        }
    }

    public sealed class LargeBinaryLfsRule : IHygieneRule
    {
        public string Name => "lfs-binaries";

        private static readonly string[] BinaryExtensions = { ".fbx", ".psd", ".wav", ".mp4", ".exr" };

        public IEnumerable<Finding> Evaluate(RepoSnapshot s)
        {
            foreach (var extension in BinaryExtensions)
            {
                bool present = s.AssetPaths.Any(p => p.EndsWith(extension, StringComparison.OrdinalIgnoreCase));
                if (!present) continue;

                bool tracked = s.GitAttributesLines.Any(l =>
                    l.Contains("*" + extension, StringComparison.OrdinalIgnoreCase) &&
                    l.Contains("filter=lfs", StringComparison.OrdinalIgnoreCase));

                if (!tracked)
                {
                    yield return new Finding(Severity.Warning, Name,
                        $"Project contains {extension} files but .gitattributes does not route them through LFS.");
                }
            }
        }
    }

    public sealed class OrphanMetaRule : IHygieneRule
    {
        public string Name => "orphan-meta";

        public IEnumerable<Finding> Evaluate(RepoSnapshot s)
        {
            var all = new HashSet<string>(s.AssetPaths, StringComparer.OrdinalIgnoreCase);

            foreach (var path in s.AssetPaths)
            {
                if (path.EndsWith(".meta", StringComparison.OrdinalIgnoreCase))
                {
                    var asset = path.Substring(0, path.Length - ".meta".Length);
                    if (!all.Contains(asset))
                    {
                        yield return new Finding(Severity.Warning, Name,
                            $"'{path}' has no matching asset. A deleted asset left its .meta behind.");
                    }
                }
                else if (!all.Contains(path + ".meta") && !IsIgnoredBySuffix(path))
                {
                    yield return new Finding(Severity.Error, Name,
                        $"'{path}' has no .meta file. Anything referencing it will break on other machines.");
                }
            }
        }

        // Folders ending in ~ and hidden files are skipped by Unity's importer, so no .meta is expected.
        private static bool IsIgnoredBySuffix(string path)
        {
            var parts = path.Split('/');
            return parts.Any(p => p.EndsWith("~") || p.StartsWith("."));
        }
    }

    public sealed class RepoHygieneChecker
    {
        private readonly IReadOnlyList<IHygieneRule> _rules;

        public RepoHygieneChecker(IEnumerable<IHygieneRule> rules)
        {
            _rules = rules.ToList();
        }

        public static RepoHygieneChecker CreateDefault() => new RepoHygieneChecker(new IHygieneRule[]
        {
            new ForceTextRule(),
            new VisibleMetaRule(),
            new GitIgnoreRule(),
            new LargeBinaryLfsRule(),
            new OrphanMetaRule()
        });

        public IReadOnlyList<Finding> Run(RepoSnapshot snapshot)
        {
            return _rules.SelectMany(r => r.Evaluate(snapshot)).ToList();
        }
    }
}