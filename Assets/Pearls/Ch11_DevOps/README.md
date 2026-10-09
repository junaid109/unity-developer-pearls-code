# Chapter 11: DevOps for Unity Teams

A CI build entry point that validates first, sets the version and defines, builds, and exits with a code the pipeline can read.

## What is here

- `Editor/AutomatedBuildSystem.cs` merges the book's two listings. It is compile only: running it starts a real player build, and it expects `Assets/Scenes/Boot.unity` and `Assets/Scenes/Main.unity`, which this project does not have.

## Tests

There are no tests for this chapter. It compiles against the Unity 6 `NamedBuildTarget` API.

## Try changing

- Point `BuildScenes` at `Assets/Scenes/SampleScene.unity` and run it with `-executeMethod Pearls.DevOps.AutomatedBuildSystem.PerformBuild` on a copy of the project.
- Pull `GetArg` and the validation into a pure class and test them.
