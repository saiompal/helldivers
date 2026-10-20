# Hat Hop Repository Reference

Use this file as the first-stop project map. It summarizes the repository and durable design decisions so future work can jump directly to the relevant files. Re-check source files when behavior may have changed; this reference is not a substitute for tests or Unity playtesting.

## Project identity

- Game/design name: **Hat Hop** (the GDD title is **In the dark**; the GitHub repo is `saiompal/helldivers`).
- Genre: 2D maze puzzle platformer built around memorization, limited visibility, collection, and automatic 180-degree world flips.
- Unity version: **6000.3.23f1**. URP 17.3.0, Input System 1.20.0, TextMesh Pro/uGUI, 2D physics and 2D lighting.
- Primary target in the repo: WebGL. The checked-in GitHub Pages build is under `docs/`.
- Design source: `Section 3 - Alpha Prototype GDD (Fall 2026 Friday).docx`.
- Detailed Ring Exchange notes: `LEVEL3_README.md`. `READ_ME_FIRST.txt` documents the four-level main menu.

## Current game loop and controls

Each level begins with a 5-second fully lit overview. The global light then turns off, the camera follows the player at the challenging original orthographic size 4, and only the player's proximity light remains. Every collected star adds 10% of the light's initial inner and outer radius and awards one additional F-light charge in every level. Radius growth is 110%, 120%, 130%, and so on, never compounding the previous radius. The player collects every star, then reaches the exit. Pressing F spends a charge to reveal the full map for 2 seconds and temporarily expands the camera to orthographic size 8; it returns to size 4 when the reveal ends. The map counts for 8 seconds, warns for 2 seconds with a red light pulse, then rotates 180 degrees over 0.6 seconds while gravity remains world-down.

- Move: A/D or Left/Right
- Jump: Space, W, or Up
- Reveal: F (3 starting charges)
- Restart: R
- Menu: Esc
- Next level after a win: N, when another build-index scene exists

Level 3 intentionally differs by keeping its exit visible during the preview. The +1 reveal charge per collected star is now a shared rule for Levels 0–3 and future levels.

## Scene and build order

`ProjectSettings/EditorBuildSettings.asset` enables exactly:

1. `Assets/Scenes/MainMenu.unity`
2. `Assets/Scenes/Level0.unity` - original base level
3. `Assets/Scenes/Level1.unity` - first Lisa design level
4. `Assets/Scenes/Level2.unity` - second Lisa design level
5. `Assets/Scenes/Level3.unity` - Ring Exchange

`LevelNEW.unity` and `LevelNEW2.unity` were promoted to Levels 1 and 2. `SampleScene.unity` remains unused and is not in the active build.

The main menu has matching Level 0 through Level 3 buttons plus Quit. `MainMenuBuilder` preserves this order and filters missing/blank scenes.

## Runtime architecture

- `Assets/Scripts/GameManager.cs`: singleton level state machine; preview/reveal/play/win/fail phases, input for F/R/Esc/N, star counts, additive per-star proximity-light growth, universal per-star charge rewards, exit unlock, and HUD text.
- `Assets/Scripts/MapFlipController.cs`: timed warning/turn cycle; rotates the map root and player around the map center, suspends player physics during the turn, exposes phase/timing state and `Flipped`.
- `Assets/Scripts/CameraFollow.cs`: overview-to-follow zoom, normal size-4 framing, temporary size-8 F-reveal framing, bounds clamping, HUD offset, and exact following during flips.
- `Assets/Scripts/PlayerMovement.cs`: shared accelerated run/jump controller for every current and future level (7.5 maximum horizontal speed, 38 ground acceleration, 12 air acceleration, 13 flat-ground braking, 13.2 jump speed, coyote time, and jump buffer). It also handles slopes, conveyors, moving support velocity, and lift-aware jumps.
- `Assets/Scripts/StarCollectible.cs`, `ExitTrigger.cs`, `PulsingGlow.cs`: pickup, locked/unlocked exit behavior, and visual pulse.
- `Assets/Scripts/Hazard.cs`, `PortalController.cs`: optional hazard failure and paired teleport support.
- `Assets/Scripts/MainMenu.cs`: serialized level buttons, Quit behavior, and initial UI selection.

## Level 0

Level 0's authoritative layout is the ASCII grid in `Assets/Editor/Level0Builder.cs` (`#` wall, `.` space, `S` spawn, `E` exit, `*` star). It has two stars and requires a flip to reach the exit. `Tools/check_level.py` simulates movement/jumping/flips against that grid.

Editor commands:

- `Tools > Level 0 > Build Level 0`
- `Tools > Build Alpha (Menu + Level 0)`
- `Tools > Build HUD`

## Levels 1 and 2

`Assets/Scenes/Level1.unity` and `Assets/Scenes/Level2.unity` are the two scenes originally committed as `LevelNEW` and `LevelNEW2` on the `lisa-design-level` branch. They use the shared standard gameplay scripts and are now active build scenes.

## Level 3 Ring Exchange

Level 3 is derived from Level 0's rig and appearance. Preserve Level 0's player sprite, pale-cyan tint, 0.65 scale, material, background, proximity-light world radius (0.5 inner / 2 outer), shared HUD, flip timing, and shared scripts unless the user explicitly changes the design.

The data source is `Assets/Resources/Level3RingLayout.json` (currently revision 3, 48x24 map). It defines fixed blocks, four lifts, five star positions, spawn, and central exit. Five enclosed sectors surround the exit; the exit has west/east approaches. Conveyors are slanted, and lifts pause during preview and map turning.

Level 3 files:

- `Assets/Scripts/Level3SceneSetup.cs`: loads the JSON and rebuilds geometry at early scene startup; converts the Level 0 player to the Level 3 rolling setup.
- `Assets/Scripts/Level3BallMotor.cs`: compatibility subclass retained for existing Level 3 scene serialization; all actual movement behavior is defined by the shared `PlayerMovement` standard.
- `Assets/Scripts/Level3MotionSurface.cs`: map-local sinusoidal lift motion that pauses during preview/turns.
- `Assets/Scripts/Level3BeltMark.cs`: conveyor tread animation only.
- `Assets/Editor/Level3Builder.cs`: copies the Level 0 scene to Level 3, attaches/reuses setup, rebuilds geometry/HUD, and saves only Level 3.
- `Assets/Resources/Level3Rolling.physicsMaterial2D`, `Level3Star.png`: Level 3 runtime resources.

To change Level 3 geometry, edit `Level3RingLayout.json`, then in Unity run `Tools > Level 3 > Build or Refresh Ring Exchange`. Do not hand-edit the large baked `Level3.unity` for layout changes unless there is a strong reason.

## Editor/build helpers

- `Assets/Editor/Level0Builder.cs`: Level 0 geometry and serialized gameplay constants.
- `Assets/Editor/Level3Builder.cs`: Level 3 rebuild workflow.
- `Assets/Editor/HudBuilder.cs`: common top/bottom HUD and scene bindings.
- `Assets/Editor/MainMenuBuilder.cs`: menu UI and build-scene order.

Because scene YAML contains serialized values and object references, changing a C# field default alone does not necessarily change existing scenes. Inspect/update the builder and serialized scene value together when retuning gameplay.

## Validation and known caveats

Run from the repository root on Windows:

```powershell
python -X utf8 Tools/check_level.py Assets/Editor/Level0Builder.cs
python -X utf8 Tools/validate_level3.py
```

Current observed results on 2026-10-16:

- `check_level.py` passes its important outcomes: exit reachable with flipping, not reachable without flipping, both stars reachable in both orientations, and zero soft-lock states.
- `validate_level3.py` checks Ring Exchange geometry, references, GUIDs, preserved Level 0 appearance, and protected base hashes.
- A headless Unity 6000.3.23f1 compile/regeneration attempt on 2026-10-16 was blocked by the local licensing client (`com.unity.editor.headless` unavailable). Open the project normally in Unity to perform the first editor compile and playtest.
- Static validation is not Unity compilation or a playthrough. Always report separately whether Unity import, Console compilation, physics traversal, flip behavior, URP lighting, and a full completion run were actually tested.

## Web build

`docs/index.html` loads `docs/Build/Build.{data,framework.js,loader.js,wasm}` and is the active GitHub Pages entry. The root-level `docs/Darkmaze.*` bundle appears to be an older/stale build and is not referenced by the current index. Project metadata still says `DefaultCompany` / `My project`; the browser title also reflects that.

Do not edit generated WebGL `.data`, `.wasm`, or framework files by hand. Rebuild from Unity when gameplay changes must reach GitHub Pages.

## Durable design decisions from the Hat Hop ChatGPT project

The latest Ring Exchange conversation established these constraints, all of which match the current repository:

- Ring Exchange is the selected Level 3 concept: five sectors around a central exit, two exit approaches, outer-loop traversal, slanted conveyors, and four lifts.
- Level 0 is the base. Do not apply a separate blue-ball look or new visual theme to Level 3.
- Show the Level 3 exit during the preview.
- Each star in every level awards one light charge.
- Conveyor/escalator movement restriction was a bug; belts should carry the ball while still allowing steering and jumping.
- Main menu exposes buttons for Levels 0 through 3 in the existing style.
- Unity runtime/full-playthrough verification was still pending in that project history; never infer it from static checks.

## Change discipline

- Preserve Unity `.meta` files and GUIDs; never regenerate or delete them casually.
- Avoid committing Unity-generated `Library/`, `Temp/`, `Logs/`, `obj/`, `Build/`, or `Builds/` directories (already ignored).
- Prefer source-of-truth edits (Level 0 grid, Level 3 JSON, builders, scripts) over manual scene-YAML surgery.
- When changing shared scripts, test all four levels. Movement, normal/reveal camera sizes, and per-star proximity-light growth are shared standards and must also be used by future levels.
- Keep the build order `MainMenu`, `Level0`, `Level1`, `Level2`, `Level3`, because next-level loading depends on build index.
