# Simulation performance comparison

User measurements on the same save (September 9, 2026):

| State | Mod installed | Original game |
| --- | --- | --- |
| Paused | approximately 130–150 FPS | approximately 140 FPS |
| 1× simulation | approximately 120–130 FPS | 60–112 FPS |
| Maximum simulation | approximately 50–60 FPS | 50–56 FPS |

These are manual observations, not a controlled benchmark. The low maximum-speed FPS reproduces without the mod, so rendering changes alone cannot explain it. The current log contains repeated failures to create NavMesh agents; an earlier log contains missing-agent exceptions in OrbsManager and TimeManager. Causality is not established. No navigation or simulation patch has been applied.

# Minimap inspection

The original UiContentMinimap.DragCameraStart first calls TryGetPointerOnCompass and returns immediately for a hit. That test uses the compass RectTransform's screen rectangle. An overlapping compass can therefore block map clicks even on its transparent area. MinimapSetCamera also clamps the destination to camera boundaries. Neither finding establishes an Extended Zoom regression without an in-game reproduction. No minimap patch has been applied.
