# Chapter 17: When You're Stuck

A bug report builder that fills in the facts a teammate needs, so you can spend your words on what you tried and what you think.

## What is here

- `Runtime/BugReport.cs` is pure: the draft, the environment facts, a thread-safe ring buffer of log lines, and the Markdown formatter.
- `Runtime/BugReportCollector.cs` is the Unity adapter. It collects facts and logs, and copies the report to the clipboard from its context menu. The buffer is created on first use, because the context menu runs in Edit Mode where `Awake` has not run.

## Tests

The tests check the ring buffer's order and wrap-around, every TODO placeholder, numbered steps, the blank line before lists, and that `CopyReport` works in Edit Mode.

Run its tests from **Window > General > Test Runner > EditMode**, under `Pearls.Ch17.Tests`.

## Try changing

- Add a field for the build's commit hash from CI and test that it appears.
- Paste a generated report into a real ticket and see what a teammate still has to ask.
