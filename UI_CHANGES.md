# Reference UI

The menu, level-selection map, and gameplay HUD share runtime layout code.
Editor rebuilds bake that same hierarchy into the scenes. Legacy scenes repair
their UI when they start, rather than showing a different fallback design.

## Appearance and behavior

- Home: framed **IN THE DARK** title and centered Start/How to Play buttons. The
  controls box starts hidden and opens in the center; X or Escape dismisses it.
  The tutorial scene remains available for development, with no menu button.
- Map: one sequential curved route (1–2–3–4–5), five round level nodes,
  play/finish markers and Back. Locked nodes remain opaque and cannot be selected.
- Completion: one outlined star per level fills yellow when that level is completed,
  independent of run time. Existing saved completions carry over. Time ratings,
  best-time recording and the time legend have been removed. Completion stars are
  separate from the collectible stars required to open the exit.
- HUD: a crisp red animated hourglass at left, yellow stars/count at center, and
  a new light-blue handheld flashlight with charge cells at right. The icons use
  vector geometry with narrow antialiasing fringes, without the old stacked glow
  or lantern artwork. There is no numeric flip timer. Sand drains with
  the flip cycle, pulses during the warning, rotates/enlarges during the actual
  turn, and resets afterward. Tutorial flips are triggered by the guide.
- Extra live light charges expand the rail to five pips; additional charges use
  a `+N` count. The tutorial supplies one starting charge for its F-light step.
- World elements retain their original sprites and colors. The added silver
  shading, rim graphics, and player/star halo sprites were removed. Their generated
  textures are backed up outside Assets in ignored `UIReview/RevertedArt/`.
- The preview frames the whole maze below the HUD. Gameplay retains the original
  player-follow zoom, including the wider view while using an F light charge.

## Completed-run trajectory

`RunTrajectory` records maze-local positions at 20 Hz, skips world turns and
deduplicates stationary samples. Successful completion captures the endpoint and
uses `RunPathSimplifier` to summarize the pathway through visible passages rather
than reproduce jump/projectile arcs. Small loops and repeated airborne movement
collapse; star pickups split the route into mandatory legs so their detours remain.
Shortcuts use a small circle cast against permanent maze walls; triggers, the player
and moving bodies do not distort the summary. The recorder then waits for the
overview and reveals a thin ball-colored line over 1.2 seconds.
The line uses an included unlit shader and stays about two
screen pixels wide. Failed attempts never show it; replay/menu/next-level discard it.
Only the current attempt is held in memory; no route save or analytics upload is added.

The success message and existing rewards/shortcuts move into a bottom footer.
The completion camera fits the maze between that footer and the top HUD, including
narrow screens. Memorization, active player-follow zoom and world art are unchanged.
The smoke checks include coordinate stability across flips, removal of jump arcs
and local loops, wall-safe segments, preserved pickup stops, endpoint capture,
hidden recording, win/replay/failure behavior,
and overview fitting at 16:9, 4:3 and 9:16. Completion screenshots use a clearly
scripted render fixture, not a recorded human playthrough.

## Rebuild

In Unity, use **Tools > UI > Rebuild Menu and All HUDs** to save the current
layout in MainMenu, Tutorial, and Level0–Level4. Use
**Tools > Build > Rebuild UI and WebGL** to also rebuild `docs/`.
The custom `Reference` WebGL template fills the browser with the game and a dark
loading screen instead of the default small canvas, white margins, and footer.

`GameUi`, `UiShapeGraphic`, and `MainMenu` own the menu artwork and layout.
`GameplayHudRuntime` owns the gameplay layout; `HourglassIndicator` binds it to
`MapFlipController`. Decorative icons are geometry, avoiding unsupported-font boxes.

## Verification

`ReferenceUiReview.RebuildAndCapture` produces screenshots in the ignored
`UIReview/` directory. `ReferenceUiReview.BuildAndCaptureWebGl` also builds the web game.

Run `ReferenceUiSmoke.Run` in batch mode with `-uiReviewSmoke` for scene lifecycle
checks. This uses isolated save keys and suppresses synthetic gameplay analytics.
It exercises the hidden/open/closed help box, Start/Back, a light reveal, star
rewards, a real flip and animated sand, completion stars, unlocking, replay,
original world art, and all five HUDs.

The EditMode tests cover progression, saved rewards, completion persistence and
legacy time-record independence. They use isolated save keys, leaving player saves intact.
