# Planet face load performance

Notes from a 2026-04-30 investigation into why `DetectorPagePlanet.LoadFace` takes ~750 ms per cube face in-game on a vanilla EarthLike planet (2048×2048 `_mat.png`).

> **Outcome: closed, no further work planned.** The one remaining idea with real headroom — persisting parsed face
> data — was dropped; see [decisions.md](decisions.md#abandoned-caching-parsed-planet-data-into-save-files). The
> ~3-4× sandbox tax measured here is the durable finding and is summarised in
> [engine-notes.md](engine-notes.md#modding-sandbox).

## In-game baseline

Measured via `DetectorServer.PlanetLoadTicks` with a temporary `Stopwatch` split inside `LoadFace`:

| Phase | In-game time |
|---|---|
| `PlanetMatHelper.LoadPlanetFacePng` (`Png.Open`) | ~453 ms |
| Classify + dedup + pyramid build | ~303 ms |
| **Total `LoadFace`** | **~756 ms** |

## Standalone reproduction

A standalone .NET 4.6.2 console exe under `Tests/PngBlueSum/` opens the same `right_mat.png` and runs three passes:

1. `System.Drawing.Bitmap` + `LockBits` — raw blue-channel sum.
2. `BigGustave.Png.Open` + `GetPixel` blue sum (the same vendored decoder the mod uses).
3. Mimic of `LoadFace`: BigGustave decode + classify into 32×32 cells + dedup + quadtree pyramid build, with a synthesized `blueToOreIndex` table sampled from the image (we can't load the real `OreMappings` outside the game).

| Pass | Standalone | In-game | Sandbox tax |
|---|---|---|---|
| `System.Drawing` raw blue sum | ~30 ms | — | — |
| `BigGustave` decode + sum | ~155 ms | — | — |
| Mimic decode (`Png.Open` only) | ~144 ms | ~453 ms | ~3.1× |
| Mimic post-decode (classify + dedup + pyramid) | ~73 ms | ~303 ms | ~4.1× |
| **Mimic total** | **~262 ms** | **~756 ms** | **~2.9×** |

Standalone numbers are with the in-game `MyCompressionStreamLoad` path active (`NOVRAGE` undefined). Toggling to `DeflateStream` (`NOVRAGE` defined) is ~30 ms faster standalone but irrelevant to the in-game baseline.

## Conclusions

- The in-game cost is roughly **3-4× a uniform tax** across both the decode and the post-decode work. There is no single hot spot; the cost scales with the number of IL instructions executed in tight loops (`Decoder.Decode` unfilter, `GetPixel` + per-pixel bucket-add).
- This is consistent with Space Engineers' modding sandbox running mod IL through additional verification / under restricted JIT, not with an algorithmic problem. **A pure code optimization is unlikely to recover meaningful time** — the 4× factor sits below user code.
- The standalone exe at ~262 ms is the lower bound any mod-side optimization could approach. Even halving the algorithmic work would only save ~100-150 ms in-game.

## Things tried, marginal or no effect

- **Replaced `new byte[100 << 20]` in `BigGustavePng/PngOpener.Inflate` with an exact `(1 + width × bpp) × height` allocation** (`PngOpener.cs:22`, `PngOpener.cs:165`). For 2048×2048 RGB this drops the per-call allocation from 100 MiB to ~12 MiB, 6× the LOH garbage per planet load (6 faces × 100 MiB → 6 × 12 MiB = 72 MiB). Single-shot wall time barely moved (748 → 740 ms in-game) but the GC-pressure reduction is worth keeping.
- **Confirmed `MyCompressionStreamLoad` (GZipStream + BufferedStream) vs `DeflateStream`**: the VRage path is ~25% slower standalone but that gap is dwarfed by the sandbox tax in-game.

## Things not tried (would require larger changes)

- **Persistent on-disk cache of the parsed face data** (per-ore `pixels[] / gridStarts[] / gridCounts[] / pyramid`). Would skip PNG decode + classify on every load after the first. Cache key for vanilla planets is `(folderName, face)`; modded generators need a content hash of the PNG bytes. Discarded for now: there's no convenient mod lifecycle hook for a shared cross-session cache, and normal worlds typically only have one of each planet so first-load latency is the only thing that matters.
- **Sandbox-aware decoder rewrite**: e.g. unrolling `Decoder.Decode`'s unfilter loop, or skipping the `RawPngData` indirection on `GetPixel`. Possible but invasive given the 3-4× sandbox tax suggests diminishing returns.

## How the standalone numbers were obtained

A throwaway .NET 4.6.2 x64 console exe was built that links the same BigGustave sources from `Data/Scripts/OreDetectorReforged/BigGustavePng/` (via MSBuild `<Compile Include="...\*.cs">` with `Link` metadata) and provides a small `VRage.MyCompressionStreamLoad` shim so `PngOpener`'s non-`NOVRAGE` branch compiles outside SE. It opens the same `EarthLike/right_mat.png`, runs `Png.Open` + a per-pixel sweep that mirrors `LoadFace`'s classify/dedup/pyramid steps, and prints per-phase times. The exe is gone — re-create it if these numbers ever need re-validating.
