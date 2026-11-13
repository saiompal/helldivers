# Mobile tilt controls

## Browser code

`Assets/Plugins/WebGL/MobileTilt.jslib` implements the sensor bridge.

1. `MobileControls.Start()` calls JavaScript `StartTiltSensor()` when a gameplay scene starts.
2. The browser check accepts Android/iPhone/iPad/iPod identifiers, User-Agent Client Hints reporting mobile, or iPads identifying as a Mac with multitouch. Screen width alone and touchscreen support alone do not enable tilt UI. Device identification is a browser heuristic, not a guarantee.
3. Mobile browsers get an **Enable Tilt Controls** button. Desktop browsers return immediately, without a button or sensor listener.
4. Pressing the button requests sensor permission if the browser requires it, then attaches a `deviceorientation` listener. Sensor access needs HTTPS in supported browsers. The button stays available as **Recenter Tilt** once readings arrive.
5. The first valid sensor reading is the neutral holding position. Rotating the screen, recentering, or switching browser tabs clears steering and captures a new neutral position on the next reading.

## Orientation mapping

`screen.orientation.angle` is preferred; `window.orientation` is the fallback. Angles are normalized to 0–359 degrees, so -90 becomes 270.

| Screen angle relative to the device's natural orientation | Sensor value used |
| --- | --- |
| 0 | `event.gamma` |
| 90 | `event.beta` |
| 180 | `-event.gamma` |
| 270 | `-event.beta` |

`gamma` describes rotation around the device Y axis; `beta` describes rotation around X. The screen's horizontal direction changes when the device rotates, so the selected axis and sign change too. This does not lock the screen to landscape. On most phones, 0 is portrait; some devices have a different natural orientation.

The current implementation uses one Euler angle at a time. It is intended for ordinary handheld postures; extreme postures can cross Euler angle discontinuities. Verify both landscape directions and portrait on actual phones before changing signs or sensitivity.

## Sensitivity

Edit the `settings` object near the top of `MobileTilt.jslib`:

```javascript
var settings = { deadZoneDegrees: 4, fullTiltDegrees: 25, invert: false };
```

- `deadZoneDegrees`: tilt within this distance from neutral gives zero movement. Increase it to reduce unwanted movement from small hand motions.
- `fullTiltDegrees`: deviation from neutral that gives maximum steering. Lower it for more sensitive steering. Keep it greater than `deadZoneDegrees`.
- `invert`: reverses left and right globally.

The difference from neutral is wrapped into [-180, 180). Outside the dead zone, steering strength is:

```text
min(1, (abs(difference) - deadZoneDegrees) / (fullTiltDegrees - deadZoneDegrees))
```

The difference's sign supplies direction. The final value ranges from -1 to +1. With the defaults, 4 degrees gives 0, 14.5 degrees gives 0.5, and 25 degrees gives 1.

## Unity code

- `Assets/Scripts/MobileControls.cs`: reads JavaScript `GetTiltValue()` every frame into `Horizontal`. A screen tap calls `PlayerMovement.RequestJump()` unless Unity UI is under the touch.
- `Assets/Scripts/PlayerMovement.cs`: uses `Horizontal` as the movement steering input in WebGL. Keyboard movement still works when tilt input is zero. Existing acceleration and speed limits apply.
- `Assets/Scripts/DeviceVisibility.cs`: exposes the shared `IsMobileDevice` check. WebGL calls the browser bridge's `IsMobileBrowser()`; the editor always uses desktop controls. A Unity touchscreen input device alone does not enable mobile controls.
- `Assets/Scripts/GameManager.cs`: creates the mobile torch button only when `DeviceVisibility.IsMobileDevice` is true. `RequestReveal()` is shared by that button and the F key. The button updates its size and safe-area position as the screen changes, and is usable only during play with charges remaining.

## Build and publish

Changes to `.jslib` or C# source require a new WebGL build; changing a tag does not compile code. Build into `docs`, commit the source and generated files on `main`, and point `beta-build` at that commit. The Pages workflow extracts `docs/` from the tag for `/beta-build/`.

## References

- Sensor axes: https://www.w3.org/TR/orientation-event/
- Screen angle: https://www.w3.org/TR/screen-orientation/
- Permission and user gesture requirements: https://developer.mozilla.org/en-US/docs/Web/API/DeviceOrientationEvent/requestPermission_static
