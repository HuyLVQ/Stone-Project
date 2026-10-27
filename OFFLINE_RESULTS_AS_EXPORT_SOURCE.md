# Offline video results as the Excel source of truth

## Behavior

Live inference results are still persisted during streaming for runtime monitoring, but they are no longer used by Excel export.

When offline video inference completes successfully after STOP, the UI writes its aggregate to `measurement_information` as the session's **finalized result**:

- accumulated counts: `MiSang`, `1x2`, `2x4`, and `4x6`;
- average per-frame values from the four weight models; and
- the current session ID.

The Excel export query now selects only rows marked `isFinalizedResult = TRUE`. It therefore produces one row per completed session from the offline evaluation, which is the required source of truth.

## Database change

`PostgreSqlRepository.EnsureTableExists` adds this backward-compatible column when needed:

```sql
isFinalizedResult BOOLEAN NOT NULL DEFAULT FALSE
```

Before inserting a new final result, the repository deletes an earlier finalized row for that session. This keeps one unambiguous authoritative record while retaining live snapshots as non-final telemetry.

## Failure behavior

If offline inference or final-result persistence fails, no finalized record is written. The Excel exporter omits that incomplete session rather than falling back to live estimates.
