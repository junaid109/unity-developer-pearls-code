# Chapter 6: From Cardboard to Quest 3

Combining many small static meshes into a few big ones to cut draw calls on a mobile headset.

## What is here

- `Runtime/AdvancedMeshCombiner.cs` groups child meshes by material and, optionally, by spatial cell. It splits groups that pass a vertex budget, owns the meshes it creates, and can undo the whole thing with `ClearCombined`.

## Tests

The tests build cubes with two materials and check the number of combined meshes, their vertex counts, that vertices stay in the root's local space, cell splitting, vertex-budget splitting, and that running twice does not combine its own output.

Run its tests from **Window > General > Test Runner > EditMode**, under `Pearls.Ch06.Tests`.

## Try changing

- Use **Create Optimized Mesh** from the component's context menu on a real scene and compare draw calls in the Frame Debugger.
- Set `CellSize` to match your scene and check that frustum culling still works.
