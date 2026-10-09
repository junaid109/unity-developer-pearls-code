# Chapter 14: Full-Stack Unity Architect

A network client that retries only what is worth retrying, with capped backoff and full jitter, behind a circuit breaker.

## What is here

- `Runtime/CircuitBreaker.cs` is pure: closed, open, and half-open with exactly one trial.
- `Runtime/ResilientNetworkClient.cs` classifies failures (network errors, 5xx, 408 and 429 are transient; other 4xx and bad JSON are not) and honours `Retry-After`.
- `Tests/EditMode/TinyHttpServer.cs` is a small local HTTP responder used by the tests.

## Tests

The breaker tests use a 100 ms cooldown. The client tests run against the local server and check parsing, that a 404 and malformed JSON are not retried, and that a 429 with `Retry-After` is.

Run its tests from **Window > General > Test Runner > EditMode**, under `Pearls.Ch14.Tests`.

## Try changing

- Add a test where the server returns 503 until the breaker opens, and check the client then fails fast without sending a request.
- Inject a time source into `CircuitBreaker` so the tests need no `Thread.Sleep`.
