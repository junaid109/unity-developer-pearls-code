using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using System;
using System.IO;

namespace Pearls.DevOps
{
    /// <summary>
    /// Build entry point for CI/CD. Place under an Editor folder.
    ///
    /// Called via (what GameCI does for you when you set buildMethod):
    ///   Unity -batchmode -nographics -quit -projectPath . -buildTarget Android
    ///         -executeMethod Pearls.DevOps.AutomatedBuildSystem.PerformBuild
    ///         -customBuildPath build/Android -buildVersion 1.0.42 -environment production
    ///
    /// -buildTarget is a real Unity flag: it switches the active build target before
    /// the method runs, so we read EditorUserBuildSettings.activeBuildTarget rather than
    /// inventing our own per-platform flags. Our own parameters (-buildVersion,
    /// -environment, -androidVersionCode) are plain "-name value" pairs, which is how they
    /// arrive when passed through GameCI's customParameters input.
    /// </summary>
    public static class AutomatedBuildSystem
    {
        private static readonly string[] BuildScenes = {
            "Assets/Scenes/Boot.unity",
            "Assets/Scenes/Main.unity"
        };

        public static void PerformBuild()
        {
            Debug.Log("[BuildSystem] Starting automated build...");

            BuildTarget target = EditorUserBuildSettings.activeBuildTarget;
            string environment = GetArg("-environment", "development");
            string version = GetArg("-buildVersion", "0.0.1");
            string outputPath = ResolveOutputPath(target);

            // 1. Validate BEFORE the expensive build, and stop here on failure.
            if (!ValidateAndConfigureEnvironment(environment, target, out string error))
            {
                Debug.LogError($"[BuildSystem] Validation failed: {error}");
                EditorApplication.Exit(1);
                return; // Exit is not guaranteed to stop execution at this exact line.
            }

            // 2. Player settings
            PlayerSettings.bundleVersion = version;
            if (target == BuildTarget.Android &&
                int.TryParse(GetArg("-androidVersionCode", ""), out int versionCode))
            {
                // Supply the CI run number: monotonic and deterministic. A timestamp such as
                // yyMMddHH collides for two builds in the same hour and differs between reruns.
                PlayerSettings.Android.bundleVersionCode = versionCode;
            }

            // 3. Build options
            var options = new BuildPlayerOptions
            {
                scenes = BuildScenes,
                locationPathName = outputPath,
                target = target,
                // StrictMode makes the build fail if ERRORS are reported during it.
                // It does not turn warnings into failures. Add
                // BuildOptions.CompressWithLz4HC for Android/standalone-style players only if
                // that suits your platform; WebGL compression is a Player Setting.
                options = BuildOptions.StrictMode
            };
            if (environment == "development") options.options |= BuildOptions.Development;

            // 4. Build
            BuildReport report = BuildPipeline.BuildPlayer(options);
            BuildSummary summary = report.summary;

            // 5. Report
            if (summary.result == BuildResult.Succeeded)
            {
                Debug.Log($"[BuildSystem] SUCCESS: {target} build completed.");
                Debug.Log($"[BuildSystem] Output: {summary.outputPath} ({summary.totalSize / 1024 / 1024} MB)");
                EditorApplication.Exit(0);
            }
            else
            {
                Debug.LogError($"[BuildSystem] FAILED: {summary.totalErrors} errors encountered.");
                foreach (var step in report.steps)
                {
                    foreach (var msg in step.messages)
                    {
                        if (msg.type == LogType.Error) Debug.LogError($"[Step:{step.name}] {msg.content}");
                    }
                }
                EditorApplication.Exit(1);
            }
        }

        /// <summary>
        /// GameCI passes -customBuildPath (check the current unity-builder docs); fall back
        /// to a local folder so the same method works when you run it by hand.
        /// </summary>
        private static string ResolveOutputPath(BuildTarget target)
        {
            string basePath = GetArg("-customBuildPath", $"Builds/{target}");
            // Android needs a file name; WebGL and others take a folder.
            return target == BuildTarget.Android ? Path.Combine(basePath, "Quest.apk") : basePath;
        }

        private static string GetArg(string name, string fallback)
        {
            string[] args = Environment.GetCommandLineArgs();
            int index = Array.IndexOf(args, name);
            return index != -1 && index + 1 < args.Length ? args[index + 1] : fallback;
        }

        /// <summary>
        /// Pre-build validation and environment-specific configuration, run before
        /// BuildPipeline.BuildPlayer so a bad config fails in seconds, not after a
        /// full build. Returns false (with a reason) instead of exiting, so the caller
        /// stops at one well-defined place.
        /// </summary>
        private static bool ValidateAndConfigureEnvironment(string environment, BuildTarget target, out string error)
        {
            error = null;

            foreach (var scenePath in BuildScenes)
            {
                if (!File.Exists(scenePath))
                {
                    error = $"Required scene missing: {scenePath}";
                    return false;
                }
            }

            if (environment != "production" && environment != "development")
            {
                error = $"Unknown -environment '{environment}' (expected production or development).";
                return false;
            }

            // Toggle scripting define symbols per environment so debug-only code
            // (diagnostics overlays, verbose logging, a QA cheat menu) never ships in a
            // production build by accident.
            string defines = environment == "production" ? "PROD_BUILD" : "DEV_BUILD;ENABLE_DIAGNOSTICS";

            // Unity 6 (and 2023.x): NamedBuildTarget overload is the supported API.
            var namedTarget = NamedBuildTarget.FromBuildTargetGroup(BuildPipeline.GetBuildTargetGroup(target));
            PlayerSettings.SetScriptingDefineSymbols(namedTarget, defines);

            // Older editors (2021/2022 LTS) use the string-based API, which is obsolete in Unity 6:
            //   PlayerSettings.SetScriptingDefineSymbolsForGroup(BuildPipeline.GetBuildTargetGroup(target), defines);
            //
            // Either way this REPLACES every define on the target. Append your project's own
            // defines here, or read the existing list first, modify it and write it back.

            Debug.Log($"[BuildSystem] Environment '{environment}' validated. Defines: {defines}");
            return true;
        }
    }
}
