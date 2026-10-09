# Chapter 10: Generative AI Content Pipelines in Unity

A client that asks your own backend broker for generated text, with a hard time limit, a session cache, retries with backoff and a fallback line.

## What is here

- `Runtime/GenerativeRequestClient.cs` is the book's two listings merged. On timeout it aborts the request and returns the fallback.
- `Tests/EditMode/TinyHttpServer.cs` is a small HTTP responder on `127.0.0.1` used only by the tests. It can answer, fail or hang on demand.

## Tests

The tests run the client against the local server: success then a cache hit, a 500, a hang that must time out and close the connection, a retry that succeeds, retries that run out, and the cache cap.

Run its tests from **Window > General > Test Runner > EditMode**, under `Pearls.Ch10.Tests`.

## Try changing

- Make the backoff use jitter, as Chapter 14 does, and test that two clients do not retry at the same moment.
- Replace the arbitrary cache eviction with an LRU and test which entry goes.
