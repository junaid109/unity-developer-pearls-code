# Chapter 15: Prototyping the Future

One owner for which broadcast feed is primary and which are picture-in-picture, including what happens when a feed dies.

## What is here

- `Runtime/PlaybackStateController.cs` and `Runtime/PlaybackStateController.Health.cs` are the two partial classes from the book.

## Tests

The tests cover promotion, capacity, duplicates, a failed PIP feed, a failed primary with and without a PIP to fall back to, and buffering not counting as failure.

Run its tests from **Window > General > Test Runner > EditMode**, under `Pearls.Ch15.Tests`.

## Try changing

- Decide whether `SetPrimary` should refuse a feed already marked failed, write the test, then change the code.
- Add a UI that only listens to `OnStateChanged`.
