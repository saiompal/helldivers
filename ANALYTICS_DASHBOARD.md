# Analytics Dashboard

Charts for the events in `ANALYTICS.md`, built in the Unity Cloud dashboard with **Analytics > SQL Data Explorer**.

## How to add a chart

1. Open **Analytics > SQL Data Explorer** and choose the **production** environment.
2. Paste a query below and click **Run**.
3. Pick the listed chart type. Put **every** measure on the left Y axis. The second Y axis makes two different scales look comparable when they aren't.
4. **Save** the query as a report, then **Add to dashboard**. Use one dashboard named "Hat Hop Playtests" and lay it out as shown at the end of this file.

Notes:

- Each query reads only the **last 30 days**. SQL Data Explorer bills by query time, so a date filter keeps queries cheap and fast. Change `-30` to widen the window.
- Editor playtests are logged too. To see which platforms are in your data, run `SELECT PLATFORM, COUNT(*) FROM EVENTS GROUP BY 1`. If you want only the web build, add `AND PLATFORM = '<web value>'` to the `WHERE` clauses.
- `attempt` resets when the browser page reloads, so "attempts" means attempts within one sitting.
- Levels sort as `Level0` to `Level3`, which is also the play order.

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

Difficulty: how many tries a typical player needs before their first win. Uses the median, so one very stubborn player doesn't skew it.

```sql
WITH first_clear AS (
    SELECT
        USER_ID,
        EVENT_JSON:levelName::STRING     AS level,
        MIN(EVENT_JSON:attempt::INT)     AS attempts
    FROM EVENTS
    WHERE EVENT_NAME = 'levelCompleted'
      AND EVENT_DATE >= DATEADD(day, -30, CURRENT_DATE)
    GROUP BY 1, 2
)
SELECT
    level                AS "Level",
    MEDIAN(attempts)     AS "Median attempts to first clear"
FROM first_clear
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

## 6. Reveals per clear (bar)

How much winning players lean on the F light. Near 0 means the memorization phase is enough. Near the number of charges available means the dark is too punishing.

```sql
SELECT
    EVENT_JSON:levelName::STRING                    AS "Level",
    ROUND(AVG(EVENT_JSON:revealsUsed::INT), 2)      AS "Avg reveals per clear"
FROM EVENTS
WHERE EVENT_NAME = 'levelCompleted'
  AND EVENT_DATE >= DATEADD(day, -30, CURRENT_DATE)
GROUP BY 1
ORDER BY 1;
```

## 7. How runs end (stacked bar)

The share of runs per level that end in a win, a death, a mid-run restart, or a quit to the menu. A restart or quit after a level is already won or failed doesn't count, because that run already ended.

```sql
SELECT
    EVENT_JSON:levelName::STRING AS "Level",
    COUNT_IF(EVENT_NAME = 'levelCompleted')                                               AS "Completed",
    COUNT_IF(EVENT_NAME = 'levelFailed')                                                  AS "Failed",
    COUNT_IF(EVENT_NAME = 'levelRestarted' AND EVENT_JSON:phase::STRING NOT IN ('Won', 'Failed')) AS "Restarted mid-run",
    COUNT_IF(EVENT_NAME = 'levelQuit'      AND EVENT_JSON:phase::STRING NOT IN ('Won', 'Failed')) AS "Quit mid-run"
FROM EVENTS
WHERE EVENT_NAME IN ('levelCompleted', 'levelFailed', 'levelRestarted', 'levelQuit')
  AND EVENT_DATE >= DATEADD(day, -30, CURRENT_DATE)
GROUP BY 1
ORDER BY 1;
```

## 8. Fail reasons (bar)

What kills players. No current level has a `Hazard`, so this chart and the "Failed" column in chart 7 stay empty until a hazard is added. Skip this chart until then.

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

## Dashboard layout

| Row | Charts | Question it answers |
|---|---|---|
| 1 | Daily players (full width) | Is anyone playing? |
| 2 | Level funnel · Completion rate | Where do players drop off? |
| 3 | Attempts to first clear · Clear time · Reveals per clear | Which level is hardest, and why? |
| 4 | How runs end · Fail reasons | How do failed runs end? |

The dashboard picks the colors. In the stacked bar, keep the column order above (Completed first) so wins always sit at the base and the same outcome keeps the same color across levels.
