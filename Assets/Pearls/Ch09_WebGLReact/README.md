# Chapter 9: Unity in the Browser: WebGL and React

The Unity half of a typed bridge to a React page, and a pool that streams assets over HTTP inside a byte budget.

## What is here

- `Runtime/WebGLInteropService.cs` is the book's two listings merged: the bridge, plus a queue that holds outgoing messages until React says it is ready. In the Editor it logs `[Mock] Sent to React: <type>` instead of calling JavaScript.
- `Runtime/StreamedAssetPool.cs` downloads one asset at a time and evicts the least recently used ones when over budget. It is compile only: its eviction is tied to `Destroy` and coroutines, so it needs a Play Mode test, or a pure LRU class pulled out of it.

## Tests

The bridge tests read the mock log lines to check that early messages are held and then sent in order.

Run its tests from **Window > General > Test Runner > EditMode**, under `Pearls.Ch09.Tests`.

## Try changing

- Pull the LRU bookkeeping out of `StreamedAssetPool` into a plain class and test eviction order.
- Build for WebGL with a `.jslib` and check the same messages arrive in the page.
