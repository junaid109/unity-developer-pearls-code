# Chapter 4: High-Fidelity VR Streaming and Telemetry

A jitter buffer that renders live car telemetry a little behind real time, so bursty network delivery still looks smooth.

## What is here

- `Runtime/TelemetryInterpolator.cs` takes packets from any thread, sorts them by timestamp and each frame interpolates, extrapolates briefly, or holds. `Update` calls `Tick(nowMs)`, which is what the tests call with a fake clock.
- `Optional/MetaXR/GrabbableVideoPanel.cs` needs the Meta XR Core SDK for `OVRInput`, so it only compiles when that package is installed.

## Tests

The tests replay packets on a made-up clock (each packet arrives 1000 ms after it was sent) and check exact positions for interpolation, extrapolation, holding, out-of-order arrival and the buffer cap.

Run its tests from **Window > General > Test Runner > EditMode**, under `Pearls.Ch04.Tests`.

## Try changing

- Change `BufferDelayMs` and work out the new expected positions before running the tests.
- Push packets from a background `Task` in a Play Mode test, which is what `PushPacket` is built for.
