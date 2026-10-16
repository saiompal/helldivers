# Hat Hop — Ring Exchange (Unity Level 2)

## Open and test

Use Unity **6000.3.23f1**. Open this folder as a project in Unity Hub, then open **Assets/Scenes/Level2.unity** and press **Play**. Start directly in Level 2; main-menu wiring is unchanged.

The scene contains the Ring Exchange geometry for editor inspection. Its early setup refreshes that geometry on Play from **Assets/Resources/Level2RingLayout.json**, before GameManager counts the five stars. Edit that resource to tune positions. To regenerate and save through Unity itself, use **Tools → Level 2 → Build or Refresh Ring Exchange**. The builder starts from Level 1's rig each time and writes only Level2.unity.

## This revision

- Five enclosed sectors around a central exit, with one star in each. All five unlock the existing exit trigger.
- Openings on both sides of each sector connect the hub and outer ring; the player can choose sector order and return route.
- Central exit has west and east approaches. Its landing does not obstruct the hub lift shaft.
- Four useful lifts: hub exchange, west ring, east ring, and the short transfer lift in Sector 2. The east lift serves three different room entrances. All lift sweeps are clear of fixed geometry.
- Four slanted conveyor surfaces. No flat conveyor strips, rollers, enemies or stick/attachment power.
- Manual rolling and jumping, acceleration and friction on slopes. No automatic hopping. Ground braking uses the supporting platform's velocity; jumping inherits a lift's vertical velocity.
- Lifts stop for preview and the original world-turn animation. Their phases resume afterward.
- Removed the isolated top/bottom strips and right-side staircase from the earlier greybox preview.

## Level 1 remains the base

The player retains Level 1's original sprite, pale cyan tint, .65 scale, material and warm vision light. No blue-ball override or extra rolling marker is applied. The vision radius is .5 inner / 2 outer world units, with the light child scale compensating for player scale. Walls use Level 1's grey; the background, camera, menu and shared game/flip/light scripts remain the originals. Stars use the original star bitmap and glow.

Original timing is retained: five-second overview, then darkness; F has three two-second reveals; the serialized Level 1 flip cycle is eight seconds plus a two-second warning and .6-second turn. Level 2 does not add periodic lights-on cycles.

Controls: **A/D or arrows** move; **Space/W/Up** jump; **F** reveal; **R** restart; **Esc** original menu. No new developer-only player controls are added.

## Validation performed outside Unity

Run `python3 Tools/validate_level2.py`.

Passed: scene file-reference integrity and component types; five distinct star placements and spawn/exit clearance; all lifts' complete swept volumes versus all fixed blocks; ball-sized free-space connectivity to all stars and both exit approaches; GUID uniqueness; Level 1 player/light records preserved; no horizontal conveyors. The original Level 1 scene, main menu scene, GameManager, MapFlipController, CameraFollow, StarCollectible, ExitTrigger and Level1Builder were compared byte-for-byte and are unchanged.

**These are geometric/static checks.** Free-space connectivity does not establish jump reach, collection order, timing, difficulty, or absence of every soft lock. The Unity Editor is unavailable in the build environment. Unity import/compilation, URP appearance, full physics traversal and complete playthrough have not been verified. This is a Unity project for testing, not a compiled standalone player or WebGL export.

The two marked-up attachments from the latest request were not present at their supplied local paths. This implementation follows the visible Ring Exchange concept and the written correction to use Level 1's appearance.

## First Unity acceptance pass

1. Scene imports without errors; Level 2 shows five sectors and 0/5 stars. The original menu/Level 1 still open normally.
2. Player looks like Level 1; compare the two proximity circles at the same zoom. Radius remains fixed as the ball rolls.
3. The five-second preview transitions to the original darkness and following camera. Check the warned full-map rotation while on each lift.
4. Board the west lift from the start; choose a sector opening. Test early/late dismounts and jump from a rising and falling lift.
5. Visit every star in both orientations and try a different sector order. Missed jumps should allow recovery or a deliberate flip rather than trap the player.
6. With fewer than five stars, the center exit stays locked. At five, approach it from the west, then retry from the east. Restart resets pickups, lift phases, orientation and light charges.

The older standalone browser prototype is not included as a representation of this Unity revision.
