# Offline video results as the Excel source of truth

## Behavior

Live inference results are still persisted during streaming for runtime monitoring, but they are no longer used by Excel export.

When offline video inference completes successfully after STOP, the UI writes its aggregate to `measurement_information` as the session's **finalized result**:

- accumulated counts from sampled frames: `MiSang`, `1x2`, `2x4`, and `4x6`;
- one aggregate XGBoost prediction from the `Weight_v2` pipeline (stored in the first legacy weight column); and
- the current session ID;
- analysis status (`Complete`, `Partial`, or `Unknown`); and
- expected, processed, skipped, and recording-loss counts.

The Excel export query selects only rows marked `isFinalizedResult = TRUE`. It produces one offline aggregate per finalized session. Usable partial analysis is retained under the skip-and-continue policy; Excel columns J-N explicitly show status and frame/loss counts. A finalized row does not by itself mean a complete recording.

## Database change

`PostgreSqlRepository.EnsureTableExists` adds this backward-compatible column when needed:

```sql
isFinalizedResult BOOLEAN NOT NULL DEFAULT FALSE
```

Before inserting a new final result, the repository deletes an earlier finalized row for that session. This keeps one unambiguous authoritative record while retaining live snapshots as non-final telemetry.

## Failure behavior

If every frame fails, recording cannot finalize, offline processing fails as a whole, or final-result persistence fails, no new finalized record is written. Export does not fall back to live estimates. Recoverable frame failures allow a partial aggregate with explicit quality metadata. Existing rows without quality metadata default to `Unknown`.

The recorder writes a JSON manifest listing finalized AVI segments and expected frame counts. Offline processing follows `Weight_v2/test.py` and samples each segment every 3.13 seconds. The reported expected/processed/skipped values refer to those sampled positions, not every recorded frame. Each sampled frame contributes detections at the v2 count calibration and aggregate features at the v2 weight calibration; XGBoost predicts once from the aggregate feature row. A segment that cannot be opened has unknown sampled-position coverage because the manifest does not store its frame rate.
