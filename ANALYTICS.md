# Analytics

Hat Hop sends gameplay events to **Unity Gaming Services (UGS) Analytics**
(`com.unity.services.analytics` 6.3.0).

- `Assets/Scripts/GameAnalytics.cs`: the only file that talks to UGS. It starts the service, queues events recorded before the service is ready, counts attempts per level, and handles opt-out.
- `Assets/Scripts/GameManager.cs`: records the events below. Every level uses `GameManager`, so current and future levels are tracked automatically.

## One-time setup

1. **Install the package.** Focus the Unity editor so it resolves `Packages/manifest.json`. It installs Analytics and its dependency, `com.unity.services.core`, then recompiles. The Console should show no errors.
2. **Link the project.** Open `Edit > Project Settings > Services` and confirm the project is linked to the `saiompal` organization's cloud project (`82a8509c-4483-4f48-87f5-03a74016f324`).
3. **Create the event schemas.** Open [cloud.unity.com](https://cloud.unity.com), then go to `Analytics > Event Manager`.
   - Create each parameter below with the listed type.
   - Create each custom event below and attach its parameters.
   - UGS **rejects events it doesn't know about**, so do this before you expect data.
4. **Test in the editor.** Press Play and finish or fail a level. Within a few minutes, events appear in `Analytics > Event Browser`. Dashboards and Data Explorer can take several hours to update.
5. **Ship it.** Rebuild WebGL into `docs/` so the GitHub Pages build sends events too.

## Parameters

| Parameter | Type |
|---|---|
| `levelName` | string |
| `attempt` | integer |
| `runSeconds` | float |
| `starsCollected` | integer |
| `starsTotal` | integer |
| `chargesRemaining` | integer |
| `revealsUsed` | integer |
| `flipCount` | integer |
| `failReason` | string |
| `phase` | string |

## Events

| Event | When | Parameters |
|---|---|---|
| `levelStart` | Level scene starts (including restarts) | levelName, attempt |
| `starCollected` | Star picked up | levelName, runSeconds, starsCollected, starsTotal |
| `revealUsed` | F reveal spent | levelName, runSeconds, starsCollected, chargesRemaining |
| `levelCompleted` | Exit reached with all stars | levelName, attempt, runSeconds, starsCollected, starsTotal, revealsUsed, flipCount |
| `levelFailed` | Hazard kills the player | levelName, attempt, failReason, runSeconds, starsCollected, revealsUsed, flipCount |
| `levelRestarted` | R pressed | levelName, attempt, phase, runSeconds, starsCollected, revealsUsed, flipCount |
| `levelQuit` | Esc to menu | levelName, attempt, phase, runSeconds, starsCollected, revealsUsed, flipCount |

- `attempt` counts level starts per level within one browser session.
- `runSeconds` measures from the end of the 5-second preview. It's 0 during the preview and frozen once the level is won or failed.
- `phase` is `Preview`, `Playing`, `Revealing`, `Won`, or `Failed`.

UGS also sends its own standard events (`newPlayer`, `gameStarted`, `gameRunning`, `gameEnded`).

## Dashboard

`ANALYTICS_DASHBOARD.md` contains ready-to-paste SQL Data Explorer queries, chart types, and a dashboard layout.

## Consent

Data collection starts automatically unless the player has opted out with `GameAnalytics.HasConsent = false`, which persists in PlayerPrefs. That's fine for class playtests. Before a public release, show a consent prompt and only set `HasConsent = true` after the player agrees. UGS requires consent in GDPR and similar regions.

## Adding an event

Call `GameAnalytics.Record("eventName", ("param", value), ...)`. Values must be string, int, float, or bool. Then add the event, and any new parameters, in the Event Manager.
