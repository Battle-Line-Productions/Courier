---
description: Boot the Athena session system
---

# /start

Run the Athena boot command to initialize the session:

```bash
athena
```

The boot sequence will:
1. Verify Core Identity and workspace integrity
2. Recall the last session's context and deferred items
3. Create a new timestamped session log
4. Initialize the Committee of Seats (COS) reasoning framework

After boot completes, read the output carefully — it contains your session context.

Then open today's session log (the file just created in `.context/memories/session_logs/`) and:
1. Fill in `**Focus**:` based on what the user says they're working on (or ask them)
2. Add initial items under `## 1. Agenda (The Plan)` based on the stated goals
3. Fill in `## 8. Cross-Session Links` → `**Continues from**:` with the previous session filename shown in boot output

If the `athena` command is not available, fall back to:
1. Read `.framework/modules/Core_Identity.md`
2. Read `.context/project_state.md`
3. Find the latest session log in `.context/memories/session_logs/`
4. Confirm: "Ready. Session loaded."
