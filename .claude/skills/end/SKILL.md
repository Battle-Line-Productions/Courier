---
description: Close the Athena session and persist context
---

# /end

Before running the shutdown command, open today's session log and fill in the reflective sections:

1. `## 5. Session Performance Review (AAR)` — Self-corrections made, calibration notes, one-line verdict
2. `## 6. Synthetic RLHF Log` — What you learned about the user, what worked, what to adjust
3. `## 8. Cross-Session Links` — Related sessions or protocols if applicable
4. `## 9. Parking Lot (Deferred)` — Any unfinished items or ideas to revisit

Also verify that earlier sections are up to date:
- `## 2. Key Decisions & Insights` — any final decisions from this session
- `## 3. Action Items` — mark completed items, add any remaining

Then run the shutdown command:

```bash
athena --end
```

The shutdown sequence will:
1. Close the current session log with a summary
2. Update session status to "Closed"
3. Persist context for the next session

If the `athena` command is not available, fall back to:
1. Review all checkpoint entries from the current session log
2. Fill in the reflective sections listed above
3. Append the summary to the session log
4. Git add and commit the session log
5. Confirm: "Session closed and committed."
