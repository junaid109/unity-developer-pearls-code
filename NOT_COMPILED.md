# Listings That Are Not Compiled Here

These listings need software that does not ship with Unity. They are in the project, inside `Optional/` folders, but their assembly definitions carry a define constraint, so Unity skips them until the SDK is installed. That keeps the rest of the project compiling.

None of them has been compiled or tested for this companion project. Treat them as syntax-checked only.

| Listing | Chapter | Folder | Needs | How it switches on |
|---|---|---|---|---|
| `MagneticButtonSolver` | 3 | `Ch03_MixedReality/Optional/MRTK2` | MRTK2 (`com.microsoft.mixedreality.toolkit.foundation`), for the `Solver` base class | `versionDefines` sets `PEARLS_MRTK2` when the package is installed |
| `MagneticButtonFeedback` | 3 | same | MRTK2, because it depends on `MagneticButtonSolver` | same |
| `GrabbableVideoPanel` | 4 | `Ch04_RacingVR/Optional/MetaXR` | Meta XR Core SDK (`com.meta.xr.sdk.core`), for `OVRInput` | `versionDefines` sets `PEARLS_METAXR` when the package is installed |
| `TwinState`, `SignalRTwinService` | 8 | `Ch08_DigitalTwins/Optional/SignalR` | The Microsoft .NET SignalR client (`Microsoft.AspNetCore.SignalR.Client` and its dependencies), imported as DLLs | Add `PEARLS_SIGNALR` under **Project Settings > Player > Scripting Define Symbols** after importing the DLLs |
| `ServerRackVisuals` | 8 | same | The SignalR listing above | same |
| `LoadTimerTests` (the NSubstitute test) | 13 | `Ch13_UnitTesting/Optional/NSubstitute` | `NSubstitute.dll` and `Castle.Core.dll`, imported as precompiled plugins | Add `PEARLS_NSUBSTITUTE` as a Scripting Define Symbol after importing the DLLs |

Notes:

- **MRTK2 assembly names.** The MRTK2 asmdef references `Microsoft.MixedReality.Toolkit` and `Microsoft.MixedReality.Toolkit.SDK`. Check them against the MRTK version you install.
- **Meta XR assembly name.** The Meta XR asmdef references `Oculus.VR`. Check it against the SDK version you install.
- **SignalR.** The SignalR client comes from NuGet as DLLs, not as a Unity package, so Unity cannot detect it on its own. That is why it uses a manual define.
- **NSubstitute.** The same test also exists in `Ch13_UnitTesting/Tests/EditMode/LoadTimerTests.cs` with a hand-written fake, which needs nothing extra. That version is compiled and passes.

One more listing is left out on purpose:

| Listing | Chapter | Why |
|---|---|---|
| `IGenerativeProvider` | 10 | It shows the shape of an interface on the server-side broker, not Unity code. It uses types (`ProviderRequest`, `ProviderResponse`, `ModelCapabilities`) that the book never defines. |
