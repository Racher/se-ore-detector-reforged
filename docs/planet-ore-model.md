# Engine model: planet ore vein generation

This document captures how Space Engineers generates planet ore veins, independent of how the mod consumes that model. The mod's `DetectorPagePlanet.TrySubPixel` and `PlanetMatHelper.ShapeNormalZ` are direct implementations of the formulas below; if the engine ever diverges, fix the implementation, not this document.

## Inputs from the planet definition

For each planet generator, `MyPlanet.Generator.OreMappings` is a list of `OreMapping` entries. Each entry is a triple `(Value, Type, Start, Depth)`:

- `Value` — the blue channel byte (`0..255`) in the face `_mat.png` that selects this ore. The PNGs are 2048×2048 per cube face. A pixel with this exact blue value marks a vein of `Type` at that surface location.
- `Type` — voxel material subtype name; resolved via `MyDefinitionManager.GetVoxelMaterialDefinition(...).Index`.
- `Start` — top of the vein, in meters below the planet surface.
- `Depth` — vertical thickness of the vein, in meters. The mod ignores entries with `Depth < 1.5` (1m sheets aren't generated as continuous voxels — see Planets notes).

The vein's voxel sampling target sits at `Start + Depth - 1` meters below the surface (one voxel above the vein bottom, where ore content is most reliably present).

## Surface point lookup

`MyPlanet.GetClosestSurfacePointLocal(ref dir)` takes a direction vector in planet-local space and returns the nearest surface point along (approximately) that radial direction. Magnitude of the returned vector is the surface radius at that location (`= averageRadius ± terrain height`).

Cube-face/local conversions:

- A planet has 6 cube faces (indices 0–5: front/back/right/left/up/down — see `PlanetCubemapHelper.GetFaceMatInfix`).
- `LocalToFace(localDir, f)` projects a local-space direction onto face `f`'s 2D plane: result components in `[-1, 1]`.
- `FaceToCube(faceXY, f)` reverses it, returning an unnormalized direction on the cube. Normalize before use as a surface direction.
- `TexToCube(tx, width, f) = FaceToCube(tx*2/width - 1, f)` — convert texel-space coordinates to a cube-space direction (e.g., `width = subGrid * 2048` for sub-pixel granularity).

## Surface slope correction

Veins are extruded **perpendicular to the local surface**, not radially. To convert "depth below surface" into a radial offset usable with `GetClosestSurfacePointLocal`, divide by the cosine of the slope angle (the angle between the surface normal and the radial direction):

```
oreRad = surfRad - depth / cosSlope
```

`cosSlope` is computed by finite-differencing the surface height across the face plane (`PlanetMatHelper.ShapeNormalZ`):

1. Pick a face-tangent step `texStep = 1/(2048*64)` (small enough that surface curvature doesn't dominate; large enough to avoid quantization noise from `GetClosestSurfacePointLocal`).
2. Sample three surface radii: at the input direction `a`, at directions offset by `+2*texStep` in face X (`b`) and face Y (`c`).
3. Construct the unnormalized normal `(b-a, c-a, mMapStepScale)` where `mMapStepScale = faceSize * texStep` and `faceSize = averageRadius * π/2` is the meters-per-face-unit scale.
4. Normalize; the Z component is `cosSlope`.

Intuition: `(b-a, c-a)` are radial-height differences in meters across a face displacement of `mMapStepScale` meters. The third component anchors the normal in the tangent frame. After normalization, Z is the cosine between the surface normal and the radial up direction. Flat ground → Z ≈ 1; steep slope → Z < 1 and the radial offset to reach a perpendicular depth grows.

## Final ore voxel position

Given a (sub)pixel center on face `f` at texel `(x, y)` (resolution `subGrid * 2048`):

1. `up = normalize(TexToCube((x+0.5, y+0.5), subGrid*2048, f))`
2. `surf = planet.GetClosestSurfacePointLocal(up)`; `surfRad = |surf|`
3. `cosSlope = ShapeNormalZ(planet, surf)`
4. `depth = Start + Depth - 1` of the matching `OreMapping` (read from the PNG blue value)
5. `orePos = up * (surfRad - depth / cosSlope)` — local-space coordinate of the expected ore voxel center

The mod then reads a small voxel storage box around `orePos` (at LOD1) to find the actual ore voxel — the model gives the seed point; voxel storage is the ground truth.
