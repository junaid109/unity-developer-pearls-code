# Chapter 18: Working Together

A repository hygiene check that turns Unity's version-control rules into errors and warnings a CI job can fail on.

## What is here

- `Runtime/RepoHygieneChecker.cs` is pure: a `RepoSnapshot` of settings and paths, five rules, and a checker. The asmdef sets `noEngineReferences`.
- `Editor/RepoHygiene.cs` fills the snapshot from the real project and adds a **Pearls > Check Repository Hygiene** menu item and a `-executeMethod` entry point for CI. It is compile only.
- The `.gitignore` rule collapses case classes such as `[Ll]` and matches whole folder names, so GitHub's standard Unity template passes, and look-alikes such as `Assets/Templates/` do not count as ignoring `Temp`.

## Tests

The tests run every rule against the standard Unity `.gitignore` and against broken setups.

Run its tests from **Window > General > Test Runner > EditMode**, under `Pearls.Ch18.Tests`.

## Try changing

- Run **Pearls > Check Repository Hygiene** on this project and read the Console.
- The rules use `string.Contains(string, StringComparison)`, which needs the .NET Standard 2.1 API level (Unity's default). Switch the API Compatibility Level and see what breaks; this was not tested.
