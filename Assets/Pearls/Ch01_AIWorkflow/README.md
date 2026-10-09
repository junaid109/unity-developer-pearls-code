# Chapter 1: AI-Assisted Workflow

Two small tools that hand an AI assistant good context instead of a vague question.

## What is here

- `Runtime/AgenticContextGenerator.cs` builds a Markdown summary of the project: Unity version, every interface in a `Pearls.*` namespace (from every loaded assembly), and the top of the active scene.
- `Runtime/LogToContextPipeline.cs` keeps a rolling window of console lines and, when an error arrives, raises an event with the error, the lines before it and one stack trace.

## Tests

The tests add a probe interface in the test assembly and check the generator finds it. They also feed log lines straight into the pipeline's handler and check what the error context contains.

Run its tests from **Window > General > Test Runner > EditMode**, under `Pearls.Ch01.Tests`.

## Try changing

- Change `_linesOfContextOnError` and watch the context test fail, then fix the expected line numbers.
- Make `GetSummary` read an XML doc comment or a custom attribute instead of returning a fixed string.
