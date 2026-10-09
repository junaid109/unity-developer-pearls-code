# Chapter 12: Refactoring Legacy Codebases

Two ways out of a tangled manager class: a type-safe event bus, and narrow interfaces in front of the old code.

## What is here

- `Runtime/EventBus.cs` lets publishers and subscribers meet only through an event type.
- `Runtime/GameServices.cs` has the interfaces (`IGameService`, `IPlayerService`), the legacy manager that implements them, and a consumer that only sees the interface.

## Tests

The tests check that events reach the right subscribers only, that unsubscribing removes one handler, and that the legacy manager works through its interfaces.

Run its tests from **Window > General > Test Runner > EditMode**, under `Pearls.Ch12.Tests`.

## Try changing

- Add a test that subscribes during a publish and decide what should happen.
- Give `ModernScoreDisplay` a fake `IPlayerService` in a test.
