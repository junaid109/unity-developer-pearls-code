# Chapter 7: Enterprise Bridge: CAD to Unity

Bringing BIM and CAD models into Unity with sensible import settings, metadata and colliders, and clipping streamed map tiles to the site.

## What is here

- `Editor/EnterpriseBIMImporter.cs` is an `AssetPostprocessor` for models under any `_Enterprise/` folder. It sets import options, attaches metadata and adds colliders by size.
- `Runtime/BIMDataContainer.cs` holds the metadata on each object. It is in Runtime because a component in an Editor assembly cannot exist in a player build.
- `Runtime/SiteClip.cs` decides whether a map tile is near enough to the site to keep, measured on the horizontal plane only.

## Tests

The tests check `SiteClip` at the radius boundary and with a tall tile far above the site centre, and that `BIMDataContainer` returns values and null for missing keys. The importer is compile only.

Run its tests from **Window > General > Test Runner > EditMode**, under `Pearls.Ch07.Tests`.

## Try changing

- Write `ParseMetadataJson` for your own sidecar format, test-first.
- Add a test for a tile that straddles the radius on one axis only.
