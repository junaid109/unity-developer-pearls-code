# Chapter 13: Advanced Unit Testing

The chapter's own examples: a pure calculation, and a time-dependent timer behind an interface.

## What is here

- `Runtime/CranePhysicsCalculator.cs` and `Runtime/LoadTimer.cs` are pure C#. The asmdef sets `noEngineReferences`, so the compiler stops anyone adding a Unity dependency.
- `Tests/EditMode/CraneSafetyTests.cs` is the book's test file as printed.
- `Tests/EditMode/LoadTimerTests.cs` uses the hand-written fake. The NSubstitute version is in `Optional/NSubstitute` and only compiles once you import NSubstitute. See `NOT_COMPILED.md`.

## Tests

All the book's crane test cases pass as printed, plus tests for the grace window restarting and the exact boundary.

Run its tests from **Window > General > Test Runner > EditMode**, under `Pearls.Ch13.Tests`.

## Try changing

- Feed radians instead of degrees and watch `AngleIsInDegrees` catch it.
- Import NSubstitute, add `PEARLS_NSUBSTITUTE` and run the substitute version.
