# Chapter 2: Generative Asset Pipelines

Import-time checks for AI-generated 3D models: provenance, naming, triangle budget, scale, UVs and material count.

## What is here

- `Runtime/GeneratedAssetRules.cs` is the pure rule layer. Its assembly has no Unity references at all (`noEngineReferences`), so the rules cannot quietly start depending on the engine.
- `Editor/GeneratedAssetValidator.cs` is the thin `AssetPostprocessor` that gathers facts from an imported model and logs the findings. It also moves a model back to quarantine if it was promoted without an approver.
- In the book this is one file. It is split here so the rules can live outside an Editor-only assembly. The code is the same.

## Tests

The tests build `AssetFacts` by hand and check each rule, including that the triangle budget is inclusive.

Run its tests from **Window > General > Test Runner > EditMode**, under `Pearls.Ch02.Tests`.

## Try changing

- Drop an `.fbx` into `Assets/Generated/` and read the Console.
- Add a rule, for example a maximum texture size, and write its test first.
