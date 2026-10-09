#if UNITY_EDITOR
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.VersionControl;
using UnityEngine;

namespace Pearls.Team.Editor
{
    public static class RepoHygiene
    {
        [MenuItem("Pearls/Check Repository Hygiene")]
        public static void RunFromMenu()
        {
            var findings = Check();
            if (findings.Count == 0)
            {
                Debug.Log("Repository hygiene: no findings.");
                return;
            }

            foreach (var f in findings)
            {
                var text = $"[{f.Rule}] {f.Message}";
                if (f.Severity == Severity.Error) Debug.LogError(text);
                else Debug.LogWarning(text);
            }
        }

        /// <summary>
        /// For CI: Unity -batchmode -quit -executeMethod Pearls.Team.Editor.RepoHygiene.RunFromCommandLine
        /// Exits non-zero when any error-level finding exists, so the pipeline stops.
        /// </summary>
        public static void RunFromCommandLine()
        {
            var findings = Check();
            foreach (var f in findings)
            {
                Debug.Log($"{f.Severity}: [{f.Rule}] {f.Message}");
            }

            bool failed = findings.Any(f => f.Severity == Severity.Error);
            EditorApplication.Exit(failed ? 1 : 0);
        }

        private static System.Collections.Generic.IReadOnlyList<Finding> Check()
        {
            // Application.dataPath is <project>/Assets, so its parent is the project root.
            var projectRoot = Directory.GetParent(Application.dataPath).FullName;

            var snapshot = new RepoSnapshot
            {
                SerializationMode = EditorSettings.serializationMode.ToString(),
                VersionControlMode = VersionControlSettings.mode,
                GitIgnoreLines = ReadLines(Path.Combine(projectRoot, ".gitignore")),
                GitAttributesLines = ReadLines(Path.Combine(projectRoot, ".gitattributes")),
                AssetPaths = Directory
                    .EnumerateFileSystemEntries(Application.dataPath, "*", SearchOption.AllDirectories)
                    .Select(p => p.Substring(projectRoot.Length + 1).Replace('\\', '/'))
                    .ToList()
            };

            return RepoHygieneChecker.CreateDefault().Run(snapshot);
        }

        private static string[] ReadLines(string path)
        {
            return File.Exists(path) ? File.ReadAllLines(path) : new string[0];
        }
    }
}
#endif