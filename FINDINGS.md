# Findings: What Compiling and Testing the Book's Code Turned Up

Before this project existed, the book's C# listings had only been checked by a syntax parser. Parsing tells you the brackets match. It does not tell you that a type exists, that a `using` is there, or that the code does what the comment says. This file records what changed once the listings were compiled in Unity 6000.4.2f1 and tested.

Every row below is something a compile or test run actually hit, or something checked directly in the Editor. Where a finding turned out not to be a bug, it says so.

## Summary

| Type | Count |
|---|---|
| Compile errors in a listing | 2 |
| Logic bugs found by a test | 3 |
| Runtime failure checked in the Editor | 1 |
| Changes made so a class can be tested | 2 |
| Suspected bug that a test showed was not one (code clarified anyway) | 1 |
| Layout changes needed to compile (book text unchanged) | 3 |
| Mistakes in my own test, worth knowing about | 1 |
| **Total** | **13** |

Test runs on the listings as printed (after the two compile fixes): 107 tests, 94 passed, 13 failed. Of the 13, 12 came from the 3 logic bugs below and 1 from a mistake in my own test. After the fixes, and with the 5 new tests for `IdleResetPolicy`: **112 tests, 112 passed**, both in a scratch copy run in batch mode and in the live Editor.

## The findings

| # | Ch | Class | What was wrong | How it was fixed | Book text changed |
|---|---|---|---|---|---|
| 1 | 7 | `SiteClip` | **Compile error.** `CS0246: The type or namespace name 'Bounds' could not be found` (and `Vector3`). The listing had no `using` lines. | Added `using UnityEngine;`. The prose said the function had "no engine dependency". It uses Unity's math types, so the prose now says that. | Yes |
| 2 | 9 | `WebGLInteropService` (queued version) | **Compile error** once the second block is merged into the class: `CS0246: The type or namespace name 'Queue<>' could not be found`. The first block's file had no `using System.Collections.Generic;`. | Added the `using` to the merged file. The second block's header comment now says where its members go and which `using` the file needs. | Yes (comment in the listing) |
| 3 | 5 | `FrameBudgetedObjectPooler` | **Logic bug.** With `MillisecondBudgetPerFrame` set to 0 (easy to mistype in the Inspector), the inner loop never ran. The warm-up coroutine yielded forever and never finished. The test gave up after 100 frames. | The loop now always spawns at least one instance per frame. | Yes |
| 4 | 17 | `BugReportCollector` | **Logic bug.** `CopyReport` is a `[ContextMenu]`, which you normally use in Edit Mode. Awake does not run in Edit Mode for a plain `MonoBehaviour`, so the ring buffer created in `Awake` was null and the call threw `NullReferenceException`. | The buffer is also created on first use (`_buffer ??= ...`). Awake still creates it, so it exists on the main thread before any log arrives. | Yes |
| 5 | 18 | `GitIgnoreRule` | **Logic bug, both ways.** It lower-cased each line and checked `Contains("library")`. GitHub's Unity template, and the chapter's own example, write `/[Ll]ibrary/`, which becomes `/[ll]ibrary/` and does not contain `library`. So a correct `.gitignore` produced four errors. Substring matching also accepted lines that only look right: `Assets/Templates/` "covered" `temp`, and `*.objx` "covered" `obj`. 10 tests failed. | Collapse one-letter case classes such as `[Ll]` to a single letter, then match whole path segments: `library`, `library/`, `/library/` and `**/library/` count; `Assets/Library/` does not. Negated lines (`!`) are skipped. | Yes |
| 6 | 16 | `KioskArmor` | **Runtime failure.** This project, created from a Unity 6 3D template, has Active Input Handling set to the Input System package only. In that mode `Input.anyKey` throws `InvalidOperationException` ("You are trying to read Input using the UnityEngine.Input class, but you have switched active Input handling to Input System package"). Checked with `eval` in the Editor. `Update` would have thrown every frame. | A small `VisitorActivityThisFrame()` reads the Input System under `#if ENABLE_INPUT_SYSTEM` and the legacy `Input` class under `#if ENABLE_LEGACY_INPUT_MANAGER`. | Yes, with a new warning callout |
| 7 | 16 | `KioskArmor` | **Not testable.** The idle and reset-rate decisions were mixed into `Update` with `Time`, `Input` and `SceneManager`, so a test would have had to wait two real minutes. | Pulled the decisions into a pure `IdleResetPolicy` class (no Unity types) with 5 tests. `KioskArmor` calls it. | Yes, new listing |
| 8 | 4 | `TelemetryInterpolator` | **Not testable.** `Update` read `Time.unscaledTimeAsDouble` directly, so a test could not control time. | `Update()` now calls a public `Tick(double nowMs)`. Tests replay packet arrival on a fake clock and check interpolation, extrapolation, holding, reordering and the buffer cap exactly. The time-seam idea comes from Chapter 13. | Yes |
| 9 | 10 | `GenerativeRequestClient` | **Suspected, not a bug.** The timeout path threw without calling `www.Abort()`, so it looked as if a timed-out request would keep downloading. A test with a local server that never answers showed the connection does close, because leaving the `using` block disposes the request, and disposing aborts it. | Added an explicit `www.Abort()` anyway, with a comment, so the intent is visible. The test still checks that the connection closes. | Yes |
| 10 | 2 | `GeneratedAssetRules` / `GeneratedAssetValidator` | **Layout.** The listing is one file in an Editor folder. That compiles, but then the pure rules live in an Editor-only assembly. | Split into `Runtime/GeneratedAssetRules.cs` (no Unity references at all; the asmdef sets `noEngineReferences`) and `Editor/GeneratedAssetValidator.cs`. The code is unchanged. | No |
| 11 | 7 | `BIMDataContainer` | **Layout.** The listing puts this `MonoBehaviour` in the Editor importer's file. Its own note says that is wrong for a real project, because Editor assemblies are not in a player build. | Moved to `Runtime/BIMDataContainer.cs`. The code is unchanged. | No (the listing's note already says this) |
| 12 | 13 | `LoadTimerTests` | **Layout.** The first test uses NSubstitute, which Unity does not ship. With the test as printed, the assembly does not compile unless you import NSubstitute. | The hand-written-fake test runs as normal. The NSubstitute test sits in `Optional/NSubstitute` behind a define. The chapter already explains how to import NSubstitute. | No |
| 13 | 4 | `TelemetryInterpolator` (my test) | **Gotcha in my own test, not the listing.** I expected one interpolated frame after a single `Tick`, but got two. Every `Tick` both stores packets and renders, so the tick on which the third packet arrived had already interpolated. | Counted from a "before" value. If you write your own tests, remember each arrival tick also renders a frame. | No |

## Things that worked first time, and are worth knowing

- `File.Replace` in the save system worked in the Windows Editor (Mono). The crash tests (failure before the replace, failure after the first temp write, a corrupted main file) all recovered the data the chapter says they will.
- `UnityWebRequest` to `http://127.0.0.1` worked in the Editor without changing the "Allow downloads over HTTP" Player Setting.
- `string.Contains(string, StringComparison)` in Chapter 18 compiled because this project's API Compatibility Level is .NET Standard 2.1, Unity's default. I did not test it under the .NET Framework setting, where that overload does not exist.
- `GameObject.CreatePrimitive` meshes are readable, so the Chapter 6 mesh combiner could be tested with plain cubes.

## How this was tested

Everything was run on Windows 11 with Unity 6000.4.2f1.

**Scratch copy, batch mode.** The live project was open in the Editor, so batch runs used a copy of `Assets`, `Packages` and `ProjectSettings` in a temp folder. These commands worked:

```text
Unity.exe -batchmode -nographics -projectPath "<copy>" -quit -logFile compile.log
Unity.exe -batchmode -nographics -projectPath "<copy>" -runTests -testPlatform EditMode -assemblyNames "Pearls.Ch01.Tests;...;Pearls.Ch18.Tests" -testResults results.xml -logFile test.log
unity test "<copy>" --mode EditMode --filter "^Pearls\." --output results.xml --timeout 900
```

Compile errors show up in the log as `error CS####` lines. With a compile error, the test run stops before running any test, so fix compiles first.

**Live Editor, through the Unity CLI and the project's Pipeline package (`com.unity.pipeline`).** After the files were copied into this project, these commands drove the open Editor. Add `--project-path "<this project>"` when more than one Editor is open.

```text
unity status --json
unity command recompile
unity command recompile_status
unity command console_status
unity command console --level error
unity command run_tests --mode editor --filter Pearls --filter_type assembly --timeout 300 --result-only
unity command eval 'try { return UnityEngine.Input.anyKey.ToString(); } catch (System.Exception e) { return e.GetType().Name + ": " + e.Message; }'
```

Poll `recompile_status` until it says `completed`. While the domain reloads, one poll may fail with an HTTP 400; just poll again. `console_status` reports `compilationFailed` directly, which is quicker than reading the Editor log. The last command is how finding 6 was checked.
