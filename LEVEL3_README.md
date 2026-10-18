# Hat Hop — Ring Exchange (Unity Level 3)

## Open and test

Use Unity **6000.3.23f1**. Open this folder as a project in Unity Hub, then open **Assets/Scenes/Level3.unity** and press **Play**. Start directly in Level 3; the main menu exposes Levels 0 through 3.

The scene contains the Ring Exchange geometry for editor inspection. Its early setup refreshes that geometry on Play from **Assets/Resources/Level3RingLayout.json**, before GameManager counts the five stars. Edit that resource to tune positions. To regenerate and save through Unity itself, use **Tools → Level 3 → Build or Refresh Ring Exchange**. The builder starts from Level 0's rig each time and writes only Level3.unity.

## This revision

- Five enclosed sectors around a central exit, with one star in each. All five unlock the existing exit trigger.
- Openings on both sides of each sector connect the hub and outer ring; the player can choose sector order and return route.
- Central exit has west and east approaches. Its landing does not obstruct the hub lift shaft.
- Four useful lifts: hub exchange, west ring, east ring, and the short transfer lift in Sector 2. The east lift serves three different room entrances. All lift sweeps are clear of fixed geometry.
- Four slanted conveyor surfaces. No flat conveyor strips, rollers, enemies or stick/attachment power.
- The shared movement standard provides manual rolling and jumping, acceleration and friction on slopes. No automatic hopping. Ground braking uses the supporting platform's velocity; jumping inherits a lift's vertical velocity. Levels 0 through 3 and future levels use the same controller.
- Lifts stop for preview and the original world-turn animation. Their phases resume afterward.
- Removed the isolated top/bottom strips and right-side staircase from the earlier greybox preview.

## Level 0 remains the base

The player retains Level 0's original sprite, pale cyan tint, .65 scale, material and warm vision light. No blue-ball override or extra rolling marker is applied. The vision starts at .5 inner / 2 outer world units, with the light child scale compensating for player scale. Each collected star adds 10% of those initial radii without compounding. Walls use Level 0's grey; the background, camera, menu and shared game/flip/light scripts remain the originals. Stars use the original star bitmap and glow. Normal dark-play framing uses orthographic size 4; spending an F light charge temporarily expands it to size 8 until the reveal ends.

Original timing is retained: five-second overview, then darkness; F starts with three two-second reveals, and every collected star awards another charge in all levels; the serialized Level 0 flip cycle is eight seconds plus a two-second warning and .6-second turn. Level 3 does not add periodic lights-on cycles.

Controls: **A/D or arrows** move; **Space/W/Up** jump; **F** reveal; **R** restart; **Esc** original menu. No new developer-only player controls are added.

## Validation performed outside Unity

Run `python -X utf8 Tools/validate_level3.py`.

Passed: scene file-reference integrity and component types; five distinct star placements and spawn/exit clearance; all lifts' complete swept volumes versus all fixed blocks; ball-sized free-space connectivity to all stars and both exit approaches; GUID uniqueness; Level 0 player/light records preserved; no horizontal conveyors. Protected base hashes cover Level 0, the menu, and shared gameplay code.

**These are geometric/static checks.** Free-space connectivity does not establish jump reach, collection order, timing, difficulty, or absence of every soft lock. The Unity Editor is unavailable in the build environment. Unity import/compilation, URP appearance, full physics traversal and complete playthrough have not been verified. This is a Unity project for testing, not a compiled standalone player or WebGL export.

The two marked-up attachments from the original request were not present at their supplied local paths. This implementation follows the visible Ring Exchange concept and the written correction to use Level 0's appearance.

## First Unity acceptance pass

1. Scene imports without errors; Level 3 shows five sectors and 0/5 stars. Levels 0, 1, and 2 still open normally.
2. Player looks like Level 0; compare the proximity circles at the same zoom. Confirm each star changes the outer radius from 2 to 2.2, 2.4, 2.6, 2.8, then 3 world units (with matching additive inner-radius growth).
3. The five-second preview transitions to the original darkness and following camera. Check the warned full-map rotation while on each lift.
4. Board the west lift from the start; choose a sector opening. Test early/late dismounts and jump from a rising and falling lift.
5. Visit every star in both orientations and try a different sector order. Missed jumps should allow recovery or a deliberate flip rather than trap the player.
6. With fewer than five stars, the center exit stays locked. At five, approach it from the west, then retry from the east. Restart resets pickups, lift phases, orientation and light charges.

The older standalone browser prototype is not included as a representation of this Unity revision.
