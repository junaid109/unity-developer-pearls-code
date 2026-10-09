# Chapter 3: Mixed Reality Interaction

Magnetic buttons: a target that drifts toward a fingertip so it is easier to hit with gloves or noisy hand tracking.

## What is here

- `Runtime/MagnetismModel.cs` is the platform-neutral logic: attraction strength, falloff curve, and a release margin so the effect does not flicker at the edge. It also picks one target in a dense group.
- `Optional/MRTK2/` holds the MRTK2 `Solver` version and its audio feedback. They only compile when MRTK2 is installed. See `NOT_COMPILED.md` at the project root.

## Tests

The tests check the pull at a known distance, the hysteresis band, that events fire once, and that lost tracking relaxes the target home.

Run its tests from **Window > General > Test Runner > EditMode**, under `Pearls.Ch03.Tests`.

## Try changing

- Set `ReleaseMargin` to 0 and add a test that shows the flicker the margin prevents.
- Try a different `Falloff` curve and update the expected pull.
