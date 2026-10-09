# Chapter 16: Bulletproof Kiosk Mode

Keeping an unattended kiosk alive: idle and crash resets, offline-first data capture and a background uploader.

## What is here

- `Runtime/IdleResetPolicy.cs` is the pure decision logic: is the kiosk idle, and may a reset run now. It has no Unity types.
- `Runtime/KioskArmor.cs` uses the policy, writes fatal errors to disk from any thread, and resets on the main thread. It reads input through the Input System or the legacy Input Manager, whichever is switched on. This project uses the Input System only, and there the legacy `Input` class throws.
- `Runtime/KioskArmor.cs` also holds `OfflinePersistence`, which writes each record to a temp file and renames it, so the uploader never sees half a file.
- `Runtime/OfflineQueueUploader.cs` drains the queue slowly and quarantines records the server keeps rejecting. It is compile only.

## Tests

The policy tests replay kiosk time with made-up numbers. The persistence tests check one complete `.json` file per record and no leftover temp file.

Run its tests from **Window > General > Test Runner > EditMode**, under `Pearls.Ch16.Tests`.

## Try changing

- Change `MinSecondsBetweenResets` and add a test for a scene that throws on every load.
- Run a soak test in a build: leave it idle overnight and check the error log.
