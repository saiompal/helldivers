# Analytics Dashboard

Charts for the gameplay events recorded in `Assets/Scripts/GameManager.cs`, built in the Unity Cloud dashboard with **Analytics > SQL Data Explorer**.

## How to add a chart

1. Open **Analytics > SQL Data Explorer** and choose the **production** environment.
2. Paste a query below and click **Run**.
3. Pick the listed chart type. Put **every** measure on the left Y axis. A second Y axis makes two different scales look comparable when they aren't.
4. **Save** the query as a report, then **Add to dashboard**. Use one dashboard named "Hat Hop Playtests" and lay it out as shown at the end of this file.

## Read this first

- **Level names are scene names, not the in-game numbers.** The game shows scene `Level0` as "Level 1", so the five levels appear here as `Level0` to `Level4`, plus `Tutorial`.
- **Every build sends to the same environment under the same version.** The `alpha`, `beta`, `beta-build` and `betawmob` builds on GitHub Pages all report to **production** as version `1.0`, so their data is mixed together. The alpha build had only `Level0` to `Level3`, different maps, and a fixed light allowance. Before each new build, raise **Project Settings > Player > Version** (for example `1.1`). Then filter on it with `AND CLIENT_VERSION = '1.1'`.
- Levels unlock in order, so a later level only gets data once players clear the level before it. On touch devices there's no on-screen restart, menu or next-level control, so mobile players can't get past a cleared or stuck level without reloading the page. Expect few mobile rows for `Level1` and later.
- Each query reads only the **last 30 days**. SQL Data Explorer bills by query time, so a date filter keeps queries cheap and fast. Change `-30` to widen the window.
- Editor playtests are logged too. To see which platforms are in your data, run `SELECT PLATFORM, COUNT(*) FROM EVENTS GROUP BY 1`.
- `attempt` resets when the page reloads. A **run** is one `sessionID` + `levelName` + `attempt`. The session ID lives inside `EVENT_JSON`, not as its own column.
- Charts marked **(new build)** read parameters added in the stuck/light update. Before that build ships, they come back empty. The queries skip older rows by checking that `startingCharges` is present.

## Event Manager changes for the stuck/light update

UGS **rejects any event that has a parameter it doesn't know about**. Make these changes in **Analytics > Event Manager** *before* you deploy the new build, or `levelStart`, `levelCompleted` and the other events will stop arriving.

New parameters:

| Parameter | Type | Meaning |
|---|---|---|
| `startingCharges` | integer | Light charges the run started with (from cleared milestone levels) |
| `revealsDenied` | integer | F presses with no charges left during the run |
| `secondsSinceStar` | float | Seconds since the last star, or since the lights went out if no star yet |

Events (**bold** marks what to add; existing parameter `chargesRemaining` is reused):

| Event | Parameters |
|---|---|
| `levelStart` | levelName, attempt, **startingCharges** |
| `starCollected` | levelName, **attempt**, runSeconds, starsCollected, starsTotal, **chargesRemaining**, **secondsSinceStar** |
| `revealUsed` | levelName, **attempt**, runSeconds, starsCollected, chargesRemaining |
| **`revealDenied`** (new event) | levelName, attempt, runSeconds, starsCollected, secondsSinceStar |
| `levelCompleted` | levelName, attempt, runSeconds, starsCollected, starsTotal, revealsUsed, flipCount, **startingCharges**, **chargesRemaining**, **revealsDenied** |
| `levelFailed` | levelName, attempt, failReason, runSeconds, starsCollected, revealsUsed, flipCount, **startingCharges**, **chargesRemaining**, **revealsDenied**, **secondsSinceStar** |
| `levelRestarted` | levelName, attempt, phase, runSeconds, starsCollected, revealsUsed, flipCount, **startingCharges**, **chargesRemaining**, **revealsDenied**, **secondsSinceStar** |
| `levelQuit` | same as `levelRestarted` |

`revealDenied` fires once per dry spell (the first press with no charges, then not again until the next star), so button-mashing doesn't flood the data. `revealsDenied` on the ending event counts every press.

---

## 1. Daily players (line)

Is anyone playing? Trend over time.

```sql
SELECT
    EVENT_DATE                                    AS "Date",
    COUNT(DISTINCT USER_ID)                       AS "Players",
    COUNT_IF(EVENT_NAME = 'levelCompleted')       AS "Levels completed"
FROM EVENTS
WHERE EVENT_NAME IN ('levelStart', 'levelCompleted')
  AND EVENT_DATE >= DATEADD(day, -30, CURRENT_DATE)
GROUP BY EVENT_DATE
ORDER BY EVENT_DATE;
```

## 2. Level funnel (bar)

How many players reach and clear each level? A drop between levels shows where people give up.

```sql
SELECT
    EVENT_JSON:levelName::STRING                                         AS "Level",
    COUNT(DISTINCT IFF(EVENT_NAME = 'levelStart',     USER_ID, NULL))   AS "Players started",
    COUNT(DISTINCT IFF(EVENT_NAME = 'levelCompleted', USER_ID, NULL))   AS "Players completed"
FROM EVENTS
WHERE EVENT_NAME IN ('levelStart', 'levelCompleted')
  AND EVENT_DATE >= DATEADD(day, -30, CURRENT_DATE)
GROUP BY 1
ORDER BY 1;
```

## 3. Completion rate (bar)

The share of players who start a level and eventually clear it. This is a percentage, so it gets its own chart instead of sharing an axis with chart 2.

```sql
SELECT
    EVENT_JSON:levelName::STRING AS "Level",
    ROUND(100 * COUNT(DISTINCT IFF(EVENT_NAME = 'levelCompleted', USER_ID, NULL))
              / NULLIF(COUNT(DISTINCT IFF(EVENT_NAME = 'levelStart', USER_ID, NULL)), 0), 1)
                                 AS "Completion rate (%)"
FROM EVENTS
WHERE EVENT_NAME IN ('levelStart', 'levelCompleted')
  AND EVENT_DATE >= DATEADD(day, -30, CURRENT_DATE)
GROUP BY 1
ORDER BY 1;
```

## 4. Attempts to first clear (bar)

Difficulty: how many tries a typical player needs before their first win. This counts every `levelStart` up to the first clear, across page reloads. (The `attempt` parameter resets on reload, so it undercounts players who refresh between tries.) It uses the median, so one very stubborn player doesn't skew it.

```sql
WITH first_clear AS (
    SELECT
        USER_ID,
        EVENT_JSON:levelName::STRING  AS level,
        MIN(EVENT_TIMESTAMP)          AS cleared_at
    FROM EVENTS
    WHERE EVENT_NAME = 'levelCompleted'
      AND EVENT_DATE >= DATEADD(day, -30, CURRENT_DATE)
    GROUP BY 1, 2
),
tries AS (
    SELECT f.level, f.USER_ID, COUNT(*) AS attempts
    FROM first_clear f
    JOIN EVENTS e
      ON  e.USER_ID = f.USER_ID
      AND e.EVENT_NAME = 'levelStart'
      AND e.EVENT_JSON:levelName::STRING = f.level
      AND e.EVENT_TIMESTAMP <= f.cleared_at
      AND e.EVENT_DATE >= DATEADD(day, -30, CURRENT_DATE)
    GROUP BY 1, 2
)
SELECT
    level              AS "Level",
    MEDIAN(attempts)   AS "Median attempts to first clear"
FROM tries
GROUP BY level
ORDER BY level;
```

## 5. Clear time (bar)

The median seconds from the end of the preview to the exit, counting completed runs only.

```sql
SELECT
    EVENT_JSON:levelName::STRING                     AS "Level",
    ROUND(MEDIAN(EVENT_JSON:runSeconds::FLOAT), 1)   AS "Median clear time (s)"
FROM EVENTS
WHERE EVENT_NAME = 'levelCompleted'
  AND EVENT_DATE >= DATEADD(day, -30, CURRENT_DATE)
GROUP BY 1
ORDER BY 1;
```

## 6. Light budget on clears (bar) (new build)

Replaces "Reveals per clear". The number of charges is no longer fixed: a run starts with 0 to 3 charges (one per cleared milestone level), and each star adds one. So a raw reveal count can't tell you whether light was scarce. This chart shows what winners had, what they spent, and what they left unused.

```sql
SELECT
    EVENT_JSON:levelName::STRING                                                         AS "Level",
    ROUND(AVG(EVENT_JSON:startingCharges::INT + EVENT_JSON:starsCollected::INT), 2)      AS "Charges available",
    ROUND(AVG(EVENT_JSON:revealsUsed::INT), 2)                                           AS "Charges used",
    ROUND(AVG(EVENT_JSON:chargesRemaining::INT), 2)                                      AS "Charges left at exit"
FROM EVENTS
WHERE EVENT_NAME = 'levelCompleted'
  AND EVENT_JSON:startingCharges IS NOT NULL
  AND EVENT_DATE >= DATEADD(day, -30, CURRENT_DATE)
GROUP BY 1
ORDER BY 1;
```

## 7. How runs end (stacked bar)

Every run per level, by how it ended: a win, a death, a mid-run restart, a quit to the menu, or **abandoned**. An abandoned run has a `levelStart` but no ending event: the tab was closed, the page was reloaded, or a mobile player had no way out. The old version of this chart counted ending events, so it missed abandoned runs entirely. A run that was won or failed and then restarted counts once, as the win or death.

```sql
WITH runs AS (
    SELECT
        EVENT_JSON:sessionID::STRING  AS session_id,
        EVENT_JSON:levelName::STRING  AS level,
        EVENT_JSON:attempt::INT       AS attempt,
        COUNT_IF(EVENT_NAME = 'levelStart')      AS started,
        COUNT_IF(EVENT_NAME = 'levelCompleted')  AS won,
        COUNT_IF(EVENT_NAME = 'levelFailed')     AS died,
        COUNT_IF(EVENT_NAME = 'levelRestarted')  AS restarted,
        COUNT_IF(EVENT_NAME = 'levelQuit')       AS quit
    FROM EVENTS
    WHERE EVENT_NAME IN ('levelStart', 'levelCompleted', 'levelFailed', 'levelRestarted', 'levelQuit')
      AND EVENT_DATE >= DATEADD(day, -30, CURRENT_DATE)
    GROUP BY 1, 2, 3
    HAVING started > 0
)
SELECT
    level                                                             AS "Level",
    COUNT_IF(won > 0)                                                 AS "Completed",
    COUNT_IF(won = 0 AND died > 0)                                    AS "Failed",
    COUNT_IF(won = 0 AND died = 0 AND restarted > 0)                  AS "Restarted mid-run",
    COUNT_IF(won = 0 AND died = 0 AND restarted = 0 AND quit > 0)     AS "Quit mid-run",
    COUNT_IF(won + died + restarted + quit = 0)                       AS "Abandoned"
FROM runs
GROUP BY level
ORDER BY level;
```

UGS starts a new session after the app has been in the background for a while, so a few long, paused runs can show up as abandoned.

## 8. Fail reasons (bar)

What kills players. `Level2` has spike traps, but both are set to slow the player instead of killing them, so no level fires `levelFailed` yet. Skip this chart until a hazard is set to kill.

```sql
SELECT
    EVENT_JSON:levelName::STRING || ': ' || EVENT_JSON:failReason::STRING   AS "Level: reason",
    COUNT(*)                                                                 AS "Deaths"
FROM EVENTS
WHERE EVENT_NAME = 'levelFailed'
  AND EVENT_DATE >= DATEADD(day, -30, CURRENT_DATE)
GROUP BY 1
ORDER BY 2 DESC;
```

---

## Where players get stuck

## 9. Stars collected when players give up (stacked bar)

For runs that didn't end in a win: how many stars the player had when they restarted, quit, died or abandoned the run. A tall "1 star" block means players find the first star, then can't find the second. Restarts during the 5-second preview are left out, because those players are re-memorizing, not stuck. Restarted, quit and failed runs work with old data. Abandoned runs need the new build, because `starCollected` didn't record `attempt` before.

```sql
WITH runs AS (
    SELECT
        EVENT_JSON:sessionID::STRING  AS session_id,
        EVENT_JSON:levelName::STRING  AS level,
        EVENT_JSON:attempt::INT       AS attempt,
        COUNT_IF(EVENT_NAME = 'levelStart')                              AS started,
        COUNT_IF(EVENT_NAME = 'levelCompleted')                          AS won,
        COUNT_IF(EVENT_JSON:phase::STRING IN ('Preview', 'Won', 'Failed')) AS not_stuck,
        COALESCE(MAX(EVENT_JSON:starsCollected::INT), 0)                 AS stars
    FROM EVENTS
    WHERE EVENT_NAME IN ('levelStart', 'starCollected', 'levelCompleted',
                         'levelFailed', 'levelRestarted', 'levelQuit')
      AND EVENT_DATE >= DATEADD(day, -30, CURRENT_DATE)
    GROUP BY 1, 2, 3
    HAVING started > 0 AND won = 0 AND not_stuck = 0
)
SELECT
    level                  AS "Level",
    COUNT_IF(stars = 0)    AS "0 stars",
    COUNT_IF(stars = 1)    AS "1 star",
    COUNT_IF(stars = 2)    AS "2 stars",
    COUNT_IF(stars = 3)    AS "3 stars",
    COUNT_IF(stars = 4)    AS "4 stars",
    COUNT_IF(stars >= 5)   AS "5 stars"
FROM runs
GROUP BY level
ORDER BY level;
```

A run that gives up with every star collected (`Level0` = 2, `Level1` = 4, `Level2` = 5, `Level3` = 5, `Level4` = 5, `Tutorial` = 1) means the player couldn't find the open exit.

## 10. Time to reach each star (bar)

The median seconds between one star and the next, for all runs. The step that takes longest is the hardest part of the level. "Star 2" is the second star picked up, whichever one it was. This works with old data.

```sql
WITH stars AS (
    SELECT
        EVENT_JSON:levelName::STRING     AS level,
        EVENT_JSON:starsCollected::INT   AS star,
        EVENT_JSON:runSeconds::FLOAT     AS t,
        LAG(EVENT_JSON:starsCollected::INT) OVER (
            PARTITION BY EVENT_JSON:sessionID::STRING, EVENT_JSON:levelName::STRING ORDER BY EVENT_TIMESTAMP) AS prev_star,
        LAG(EVENT_JSON:runSeconds::FLOAT) OVER (
            PARTITION BY EVENT_JSON:sessionID::STRING, EVENT_JSON:levelName::STRING ORDER BY EVENT_TIMESTAMP) AS prev_t
    FROM EVENTS
    WHERE EVENT_NAME = 'starCollected'
      AND EVENT_DATE >= DATEADD(day, -30, CURRENT_DATE)
)
SELECT
    level || ' · star ' || star                         AS "Level · star",
    ROUND(MEDIAN(IFF(star = 1, t, t - prev_t)), 1)      AS "Median seconds to reach"
FROM stars
WHERE star = 1 OR (prev_star = star - 1 AND t > prev_t)
GROUP BY level, star
ORDER BY level, star;
```

## 11. Time stuck before giving up (bar) (new build)

How long players searched without finding another star before they restarted, quit or died. Read it next to chart 9. If many players give up at 1 star after a long search, they're lost looking for star 2. If they give up quickly, something else made them restart, such as a flip or a bad jump.

```sql
SELECT
    EVENT_JSON:levelName::STRING || ' · after ' || EVENT_JSON:starsCollected::INT || ' stars'   AS "Level · stars so far",
    COUNT(*)                                                                                     AS "Runs given up",
    ROUND(MEDIAN(EVENT_JSON:secondsSinceStar::FLOAT), 1)                                         AS "Median seconds since last star"
FROM EVENTS
WHERE EVENT_NAME IN ('levelRestarted', 'levelQuit', 'levelFailed')
  AND COALESCE(EVENT_JSON:phase::STRING, '') NOT IN ('Preview', 'Won', 'Failed')
  AND EVENT_JSON:secondsSinceStar IS NOT NULL
  AND EVENT_DATE >= DATEADD(day, -30, CURRENT_DATE)
GROUP BY 1
ORDER BY 1;
```

Put "Runs given up" and "Median seconds" in **separate** charts, or show this one as a table. They aren't on the same scale.

---

## Is the light too scarce or too plentiful?

## 12. Running out of light (bar) (new build)

The share of runs where the player pressed F with no charges left, and whether those runs still got won.

```sql
WITH runs AS (
    SELECT
        EVENT_JSON:sessionID::STRING  AS session_id,
        EVENT_JSON:levelName::STRING  AS level,
        EVENT_JSON:attempt::INT       AS attempt,
        COUNT_IF(EVENT_NAME = 'levelStart' AND EVENT_JSON:startingCharges IS NOT NULL) AS started,
        COUNT_IF(EVENT_NAME = 'revealDenied')    AS dry,
        COUNT_IF(EVENT_NAME = 'levelCompleted')  AS won
    FROM EVENTS
    WHERE EVENT_NAME IN ('levelStart', 'revealDenied', 'levelCompleted')
      AND EVENT_DATE >= DATEADD(day, -30, CURRENT_DATE)
    GROUP BY 1, 2, 3
    HAVING started > 0
)
SELECT
    level                                                                           AS "Level",
    ROUND(100 * COUNT_IF(dry > 0) / COUNT(*), 1)                                    AS "Runs that ran out of light (%)",
    ROUND(100 * COUNT_IF(dry > 0 AND won > 0) / NULLIF(COUNT_IF(dry > 0), 0), 1)    AS "Win rate after running out (%)",
    ROUND(100 * COUNT_IF(dry = 0 AND won > 0) / NULLIF(COUNT_IF(dry = 0), 0), 1)    AS "Win rate, never ran out (%)"
FROM runs
GROUP BY level
ORDER BY level;
```

The touch LIGHT button is greyed out at 0 charges, so mobile taps at 0 charges aren't recorded. This chart mostly reflects keyboard players.

## 13. Light left: winners vs. players who gave up (bar) (new build)

Did players quit because they had no light left, or did they give up with charges unused?

```sql
SELECT
    EVENT_JSON:levelName::STRING                                                                     AS "Level",
    ROUND(AVG(IFF(EVENT_NAME =  'levelCompleted', EVENT_JSON:chargesRemaining::INT, NULL)), 2)       AS "Charges left: wins",
    ROUND(AVG(IFF(EVENT_NAME <> 'levelCompleted', EVENT_JSON:chargesRemaining::INT, NULL)), 2)       AS "Charges left: gave up",
    ROUND(AVG(IFF(EVENT_NAME <> 'levelCompleted', EVENT_JSON:revealsDenied::INT,    NULL)), 2)       AS "Presses with no light: gave up"
FROM EVENTS
WHERE (EVENT_NAME = 'levelCompleted'
       OR (EVENT_NAME IN ('levelRestarted', 'levelQuit', 'levelFailed')
           AND COALESCE(EVENT_JSON:phase::STRING, '') NOT IN ('Preview', 'Won', 'Failed')))
  AND EVENT_JSON:startingCharges IS NOT NULL
  AND EVENT_DATE >= DATEADD(day, -30, CURRENT_DATE)
GROUP BY 1
ORDER BY 1;
```

### Reading charts 6, 12 and 13 together

These cut-offs are starting points to tune once real data arrives, not fixed rules.

| Signal | Suggests |
|---|---|
| Over ~30% of runs run out (12), players who gave up have about 0 charges left and several no-light presses (13), and the win rate drops after running out | **Too scarce.** Give a starting charge earlier, or make the reveal last longer. |
| Winners finish with 2 or more charges unused (6, 13) and almost no runs run out (12) | **Too plentiful.** Light isn't a real choice. Award charges for fewer stars, or cap them per run. |
| Players who gave up still have charges left (13) but stall at one star (9, 11) | **Not the light.** The layout or flip timing is the problem on that stretch. |

---

## Dashboard layout

| Row | Charts | Question it answers |
|---|---|---|
| 1 | Daily players (full width) | Is anyone playing? |
| 2 | Level funnel · Completion rate | Where do players drop off? |
| 3 | Attempts to first clear · Clear time | Which level is hardest? |
| 4 | How runs end · Fail reasons | How do failed runs end? |
| 5 | Stars when giving up · Time to each star · Time stuck | Where inside a level are players stuck? |
| 6 | Light budget on clears · Running out of light · Light left | Is the light too scarce or too plentiful? |

The dashboard picks the colors. In the stacked bars, keep the column order above (Completed first in chart 7, 0 stars first in chart 9). That keeps each outcome in the same color across levels.
