# Chapter 5: Surviving Console Certification

Two patterns that keep a console build out of trouble: spreading pool warm-up across frames, and a save system that survives a crash mid-write.

## What is here

- `Runtime/FrameBudgetedObjectPooler.cs` instantiates as many objects per frame as fit a millisecond budget, and always at least one, so a budget of 0 still finishes.
- `Runtime/AdvancedSafeSaveSystem.cs` writes to a temp file, verifies a SHA-256 hash, then swaps it in with `File.Replace`, keeping a backup. A `FaultInjection` hook lets tests simulate the process dying between steps.

## Tests

The pooler tests step the warm-up coroutine by hand, one `MoveNext` per frame. The save tests use the real files in `Application.persistentDataPath`, inject failures at each stage, and corrupt the main file to check the backup is used.

Run its tests from **Window > General > Test Runner > EditMode**, under `Pearls.Ch05.Tests`.

## Try changing

- Inject a failure at `AfterVerify` and predict what `LoadDataAsync` returns before running it.
- Time the warm-up with a heavier prefab and watch how many instances fit in each frame.
