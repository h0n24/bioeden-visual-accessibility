# Manual test plan – version 1.3.0 beta 1

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
- Compare camera movement and scrolling with the filter Off and On. There must be no severe FPS drop or continuous high load during normal movement.
- Toggle the filter with both the key and the icon labelled **F1** immediately before **?** in the top-right corner. After changing the binding, the icon must show F2 or F3. Blue means enabled and gray means disabled. The icon must be absent from the main menu and Settings and must not overlap Help.
- Find a clean lake or river with **0% pollution** and verify that it remains desaturated. Then find polluted water and verify that it remains colored.
- Check a clean lake and a polluted lake visible together: they may share one generated mesh but must have different filter states. Check the small pool and falling water at the spring of both a clean and polluted river, including the rocks around each spring (rocks must stay gray).
- Leave F1 enabled while a cleaner finishes. Within two seconds of the displayed pollution reaching **0%**, the lake or river and its spring should become gray without toggling F1 again.
- Toggle F1 off/on several times, then leave for the main menu and load the map again. Water geometry must not disappear or duplicate, original colors must return with F1 off, and the next load must start with the filter off.
- Open the game's planet-information mode and then start building. The new filter must remain independent and must not prevent normal building.
- With **Extended Zoom** active, set **Depth of Field → On**. The distant view should remain sharp. After disabling Extended Zoom, Depth of Field should follow the Video setting again.

If anything differs, record the active settings and send a screenshot. The most useful checks are outline visibility over blue fog, the top edge at maximum zoom, filter FPS, clean versus polluted water and building usability while the filter is active.
