# Unity Developer Pearls: Companion Project

This Unity project holds the C# listings from the book *Unity Developer Pearls* by Junaid Malik, laid out so they compile and run, with EditMode tests that check what each listing claims to do.

The code is original illustration written for the book. It is not taken from any client or employer project.

## What you need

- Unity **6000.4.2f1** (Unity 6.4). Other Unity 6 versions will probably work, but this is the version the tests were run on.
- No extra packages beyond what is in `Packages/manifest.json`. The tests use the Unity Test Framework (`com.unity.test-framework`), which is already listed.

## Open the project

1. In Unity Hub, choose **Add > Add project from disk** and pick this folder.
2. Open it with Unity 6000.4.2f1. The first import takes a few minutes.
3. Everything from the book is under `Assets/Pearls`.

## Run the tests

In the Editor:

1. Open **Window > General > Test Runner**.
2. Pick the **EditMode** tab.
3. Click **Run All**, or expand a `Pearls.ChNN.Tests` assembly and run just one chapter.

From the command line, against a copy of the project that is not open in an Editor (batch mode cannot open a project that an Editor already has open):

```text
Unity.exe -batchmode -nographics -projectPath "<path to project>" -runTests -testPlatform EditMode -testResults results.xml -logFile test.log
```

or, with the Unity CLI:

```text
unity test "<path to project>" --mode EditMode --filter "^Pearls\." --output results.xml
```

A few tests write to `Application.persistentDataPath` (the save system and the kiosk queue) and clean up after themselves. The network tests (Chapters 10 and 14) start a tiny HTTP server on `127.0.0.1` inside the test, so they need no internet access.

Some tests log errors and warnings on purpose (a simulated power cut, a 503 from a fake server). Those lines in the Console are expected. The test result is what counts.

## Folder map

Each chapter folder follows the same split the book argues for: logic you can test, and a thin Unity layer around it.

```text
Assets/Pearls/
  ChNN_Name/
    README.md        what the folder is for and what to try
    Runtime/         Pearls.ChNN.Runtime.asmdef   the listing code
    Editor/          Pearls.ChNN.Editor.asmdef    Editor-only listings (importers, build scripts)
    Tests/EditMode/  Pearls.ChNN.Tests.asmdef     NUnit tests, Editor platform only
    Optional/<Sdk>/  listings that need a third-party SDK, compiled only when it is present
```

| Folder | Book chapter | Tested |
|---|---|---|
| `Ch01_AIWorkflow` | 1. AI-Assisted Workflow | yes |
| `Ch02_GenerativeAssets` | 2. Generative Asset Pipelines | rules yes, importer compile only |
| `Ch03_MixedReality` | 3. Mixed Reality Interaction | `MagnetismModel` yes, MRTK2 solver optional |
| `Ch04_RacingVR` | 4. VR Streaming and Telemetry | interpolator yes, Meta XR panel optional |
| `Ch05_ConsoleCert` | 5. Surviving Console Certification | yes |
| `Ch06_CardboardToQuest` | 6. From Cardboard to Quest 3 | yes |
| `Ch07_CADPipeline` | 7. Enterprise Bridge: CAD to Unity | `SiteClip` and `BIMDataContainer` yes, importer compile only |
| `Ch08_DigitalTwins` | 8. Digital Twins and Cloud | optional only (SignalR) |
| `Ch09_WebGLReact` | 9. Unity in the Browser | bridge queue yes, asset pool compile only |
| `Ch10_GenerativeAI` | 10. Generative AI Content Pipelines | yes, against a local fake broker |
| `Ch11_DevOps` | 11. DevOps for Unity Teams | compile only |
| `Ch12_Refactoring` | 12. Refactoring Legacy Codebases | yes |
| `Ch13_UnitTesting` | 13. Advanced Unit Testing | yes (NSubstitute test optional) |
| `Ch14_FullStack` | 14. Full-Stack Unity Architect | yes, against a local fake server |
| `Ch15_Prototyping` | 15. Prototyping the Future | yes |
| `Ch16_KioskMode` | 16. Bulletproof Kiosk Mode | idle policy and offline queue yes |
| `Ch17_ProblemSolving` | 17. When You're Stuck | yes |
| `Ch18_TeamGit` | 18. Working Together | rules yes, Editor menu compile only |

## What is and is not covered

- **Compiled:** every listing that stands on its own and needs only Unity and its built-in packages. Where the book shows one class across two code blocks, the blocks are merged into one file here.
- **Tested:** the pure logic and the parts of the Unity components that can be driven from an EditMode test. Nothing here was tested on a headset, console, WebGL build or kiosk.
- **Not compiled:** listings that need a third-party SDK (MRTK2, Meta XR SDK, the SignalR client, NSubstitute). They sit in `Optional/` folders whose assembly definitions only compile when that SDK is installed. See `NOT_COMPILED.md` for what to install.
- **Compile only:** Editor importers, the CI build script and the streaming asset pool. They compile, but there is no EditMode test for them.

`FINDINGS.md` lists every problem the compile and test runs found in the book's listings, and how each was fixed.
