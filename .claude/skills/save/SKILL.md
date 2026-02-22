---
description: Quicksave a checkpoint to the current Athena session log
argument-hint: "[summary of work completed]"
---

# /save

Save a checkpoint to the current session log:

```bash
athena save "$ARGUMENTS"
```

If no summary is provided, generate one from the recent work.

After saving the checkpoint, also update the session log with anything new since the last save:
- `## 2. Key Decisions & Insights` — append any decisions made or lessons learned
- `## 3. Action Items` — update statuses (Pending → Done) and add new items
- `## 7. Artifacts & Outputs` — list any files created or modified

Only add entries that are meaningful. Skip sections where nothing changed.

If the `athena` command is not available, fall back to:
1. Find today's session log in `.context/memories/session_logs/`
2. Append a checkpoint block with timestamp and summary
3. Confirm: "Checkpoint saved."

Proactively suggest `/save` when:
- A meaningful chunk of work is completed
- Before large refactors or schema changes
- Before switching topics
- The conversation has been running for a while without a save
