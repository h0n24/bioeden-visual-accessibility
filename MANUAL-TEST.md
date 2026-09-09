# Manual test plan – version 1.3.0 beta 2

The current build is installed locally. Start BioEden normally. New options are under **Settings → Accessibility**; the original Depth of Field setting remains under **Video**.

## 1. Antenna outline

- Enable **Antenna Range Outline**, press **Confirm**, and start placing an antenna on the blue biome shown in the reference screenshot.
- Check the white outer border with the blue center around both the placement range and the already covered-area border. Move the preview over water, land and elevated terrain.
- Most important: move the preview across the **blue unexplored area**. The outline should remain equally visible over blue fog and revealed terrain, and should not become thinner when zoomed out.
- Open the menu while placing. The line must not cover settings text or block input. After returning to the game and moving the camera, it must follow the correct border.
- Try valid and invalid placement locations at different camera distances. The outline must not change building rules or actual range.
- Cancel placement. The preview outline must disappear with the original preview; the already covered-area border may remain.
- Set the option to **Off**, press **Confirm**, and verify that the original display returns.

## 2. Extended zoom

- In the normal world view, compare the maximum zoom with **Extended Zoom → Off** and **On**. On should allow about one third more distance.
- While zooming out, the camera should smoothly pitch farther downward. Return to the location from the reference screenshot and check whether the **flat blue strip at the top** is gone. Rotate and move the camera near the map edge as well.
- If the game switches to the topographic map at greater distances, verify that the switch happens only at the end of the expanded range.
- At maximum zoom, switch the option to **Off**. The camera must return to the original range without a stuck zoom input.
- Check entering the dome, returning to the world, camera rotation and camera movement.

## 3. Persistence and original Depth of Field

- Change one of the new options and leave without saving. The game should offer to discard changes; reopening the settings should show the previously saved value.
- Press **Confirm**, restart the game and verify that the selected values remain.
- In **Video**, try **Depth of Field → On / Off**. Both the original setting and the new features must work independently.

## 4. Map filter and performance

- In **Accessibility**, set **Map Desaturation Filter → On**, choose **Map Filter Hotkey → F1** (or F2/F3), and press **Confirm**.
- On a clean installation, verify that the filter is **Off** and the game starts normally. The filter icon must not appear during the loading screen.
- In the game, toggle the filter. Player structures and mineral objects should remain colored; grass, trees, rocks and clean water should remain desaturated. Building, selecting objects and normal markers must remain usable. The UI may remain colored.
- Check map cloud effects such as the colored orange/green patches in the reference screenshot. They should disappear with F1 enabled, while buildings, minerals and polluted water remain colored. Toggle F1 off and confirm the original clouds return.
- Compare camera movement and scrolling with the filter Off and On. There must be no severe FPS drop or continuous high load during normal movement.
- Toggle the filter with both the key and the icon labelled **F1** immediately before **?** in the top-right corner. After changing the binding, the icon must show F2 or F3. Blue means enabled and gray means disabled. The icon must be absent from the main menu and Settings and must not overlap Help.
- Find a clean lake or river with **0% pollution** and verify that it remains desaturated. Then find polluted water and verify that it remains colored.
- Check a clean lake and a polluted lake visible together: they may share one generated mesh but must have different filter states. Check the small pool and falling water at the spring of both a clean and polluted river, including the rocks around each spring (rocks must stay gray).
- Leave F1 enabled while a cleaner finishes. Within two seconds of the displayed pollution reaching **0%**, the lake or river and its spring should become gray without toggling F1 again.
- Toggle F1 off/on several times, then leave for the main menu and load the map again. Water geometry must not disappear or duplicate, original colors must return with F1 off, and the next load must start with the filter off.
- Open the game's planet-information mode and then start building. The new filter must remain independent and must not prevent normal building.
- With **Extended Zoom** active, set **Depth of Field → On**. The distant view should remain sharp. After disabling Extended Zoom, Depth of Field should follow the Video setting again.

If anything differs, record the active settings and send a screenshot. The most useful checks are outline visibility over blue fog, the top edge at maximum zoom, filter FPS, clean versus polluted water and building usability while the filter is active.

## Performance comparison

Compare the same save, camera position and zoom with F1 off/on, then Antenna Range Outline off/on during placement. Record FPS and whether the issue is horizontal screen tearing or intermittent stutters. Also compare Extended Zoom off at the normal camera distance. Keep resolution, V Sync and framerate limit identical. No FPS improvement has been measured for beta 4; its scene-search reduction is verified from code only.

## Sanctuary and relic depletion

With F1 enabled, compare a sanctuary with remaining research points against one with zero points. Only the former should retain its colors. Currency-free relics should lose color after exploration finishes. Verify depletion while F1 stays enabled (refresh within two seconds), then toggle F1 off to restore normal rendering.

## Clean lake palette (1.3.0 beta 8)

- Enable the filter near both a 0% lake and a polluted lake. The clean lake should be lighter and neutral gray; the polluted lake should retain its original color.
- Toggle Accessibility > Simplify Clean Lakes and Confirm: On should suppress contrasting stripes and caustics; Off should restore subtle gray waves without leaving the filter.
- Switch the filter off: both lakes must return to their original appearance.
- Clean a polluted lake while the filter is active and check that its style changes when the inspection panel reaches 0%.
- Reload and verify the setting persists; inspect the additional row for clipping at your screen resolution.

## Neutral water rendering (1.3.0 beta 9)

- Sample a clean lake and river away from edges/UI: RGB channels should match.
- Verify river/lake junctions and source water use matching gray; inspect shorelines for exposed mesh edges.
- Simplify Clean Lakes: On gives flat gray, Off gives a subtle static gray pattern.
- Polluted water must keep the original appearance. Turning F1 off must restore all water materials.
- Compare FPS with beta 8 at the same camera position. This version adds cached clean-water draw calls, with no per-frame scene scan.

## Original clean-lake waves (1.3.0 beta 10)

- With F1 on, set Simplify Clean Lakes Off and Confirm: original moving wave bands should return, in neutral gray.
- Set On and Confirm: flat gray should return. Repeat both directions without restarting.
- Sample lake interiors with a color picker; verify RGB equality and no displaced/upside-down image artifacts.
- Check structures overlapping a clean lake and polluted lakes for unintended desaturation.
- Check FPS in both modes; Off now requires a screen-color copy plus a clean-triangle pass.
- Turn F1 off and verify original water colors and materials return.

## Structure layer isolation (1.3.0 beta 12)

Beta 13 performance follow-up: compare F1 On/Off at the same camera position and simulation speed after restarting. Verify selected colors, polluted water and transparent buildings, then repeat power-plant and antenna placement. Native drawing now uses rendering masks rather than individual commands; confirm both FPS and gameplay stability.

- Restart the game before testing; existing sessions retain the old runtime.
- With F1 active, build a power plant, connect power and place an antenna on the unexplored border.
- Verify units keep moving, tiles are revealed and resource notifications expire normally.
- Check building, mineral and water colors, including transparent surfaces, after toggling F1.
- Compare FPS at the same camera position. Selected objects now use cached explicit draw calls without changing GameObject layers or camera masks.
- If the freeze returns, preserve Player.log before restarting. The previous log contains missing NavMeshAgent errors in OrbsManager and TimeManager; their cause is not yet confirmed.

## Antenna reveal stability (1.3.0 beta 11)

- Enable F1 and place an antenna in an area that still contains unexplored blue tiles.
- Confirm that the antenna reveals tiles and that resource plus/minus notifications continue updating.
- Move the antenna preview repeatedly before confirming placement; the range outline should follow it without freezing the UI.
- Repeat with Antenna Range Outline Off. The reveal behavior should be identical; only the extra outline should disappear.
- Toggle F1 off and on around a placement. The antenna should keep its normal placement and reveal behavior in both states.
