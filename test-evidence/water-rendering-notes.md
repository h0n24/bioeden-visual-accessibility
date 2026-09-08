# Lake and spring filter investigation

Inspected the local BioEden 1.2.0.0 assets and managed code on 2026-09-08. Unity version in the shipped assets: `6000.0.56f2`. Game assets are not included in this repository.

## Findings

- `terrain_lakes_water` uses `Mat_Terrain_Water_Lake` on layer 15. It is a generated mesh containing multiple lakes. Scanning only `LakePolygon`, `RamSpline`, or the Water layer misses it. `ValleysGenerator.GenerateWaterMesh` generates the individual water hexes used by `LakesGenerator`.
- `RiversGenerator.InitRiverGeneration` destroys its temporary `RamSpline` component after generating each river. It instantiates biome spring prefabs under the generated river object.
- Spring models in `defaultlocalgroup_assets_all.bundle` include `sanctuary_water` with `Mat_Terrain_Water_source_clean/polluted` and `forest_water_source` with **two material slots**: biome stones first, `Mat_Terrain_Water_Source_fall_clean/polluted` second. Updating only `sharedMaterial` misses that falling water. The stone, vegetation and building materials must not match the terrain-water rule.
- `WaterfallCircle` is an additional water renderer on layer 0, parented to the river by `GenerateWaterfallMesh`.
- Water inspection obtains `WorldGrid.Features[slot.featureIndex]`, checks `IsWaterOrRiver`, and averages `Slot.pollution.x` over `feature.shape.Coords`. It formats the result as a whole percentage. The filter retains the matching `< 0.005` clean threshold.

## Implementation and validation scope

The filter discovers terrain-water materials across layers and includes inactive spring variants. It updates each water material slot separately and associates springs with their parent river. Polluted spring materials retain their normal rendering without redrawing their surrounding stone submeshes in color.

Lake triangles are mapped to water features once when F1 is enabled. A temporary mesh groups their original indices into original-color and grayscale material slots. Vertices, UVs, vertex colors, triangle winding and bounds are retained. Feature pollution is refreshed every two seconds, once per feature, and indices are rebuilt only when classification changes. Original meshes and materials are restored when the filter is disabled or the playable world is left.

The automated geometry tests cover mixed clean/polluted lakes in one mesh, unknown edge triangles, cleanup transitions and unchanged original indices. Material-name tests use the actual lake/spring names and reject vegetation, stone and building names. These tests do **not** execute Unity rendering. The visual lake/spring result remains subject to the manual checks in `MANUAL-TEST.md`.

## Cloud VFX

The colored atmospheric patches are Unity Visual Effect objects rather than ordinary mesh renderers. The shipped cloud and ground-fog graphs expose one or more vector color inputs (`Color`, `Color 1`, and `Color 3` depending on quality and biome). The runtime now identifies environmental effects by their object or graph name (`cloud`, `fog`, `smoke`, or `dust`), stores every exposed color input, and sets each RGB triplet to luminance while preserving alpha. Disabling the filter restores the stored values. Gameplay effects are not selected by this rule. The previous beta2 log showed `Cloud color controls: 0`, which confirmed that matching only the literal `Cloud` object name did not reach the visible fog layer.
