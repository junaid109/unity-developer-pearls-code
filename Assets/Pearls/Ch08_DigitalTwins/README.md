# Chapter 8: Digital Twins and Cloud

A SignalR client that buffers twin updates from a background thread and releases them to the main thread at a fixed rate.

## What is here

- `Optional/SignalR/` holds `SignalRTwinService` and `ServerRackVisuals`. They need the Microsoft .NET SignalR client DLLs, so they are not compiled until you import those and add the `PEARLS_SIGNALR` scripting define. See `NOT_COMPILED.md` at the project root.

## Tests

There are no tests for this chapter yet.

## Try changing

- Pull the latest-value-per-key buffer out into a plain class and test it without SignalR. That is the same split Chapters 13 and 16 use.
