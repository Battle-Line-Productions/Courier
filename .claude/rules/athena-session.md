---
globs: "*"
description: Athena session management discipline
---

## Athena Session Discipline

This workspace uses the Athena SDK for session management and long-term memory.

### Session Lifecycle

1. **Boot**: Run `athena` (or `/start`) at the start of every session to load context and create a session log.
2. **Save**: Run `athena save "summary"` (or `/save`) after completing meaningful work.
3. **End**: Run `athena --end` (or `/end`) when the session is complete.

### Session Log Maintenance

The session log (in `.context/memories/session_logs/`) is a living document. Update it continuously as you work — don't wait for `/save` or `/end`.

**Update as they happen:**
- `## 2. Key Decisions & Insights` — When a decision is made or something important is learned, add it immediately
- `## 3. Action Items` — When a task is completed, mark it `Done`. When new work is identified, add it as `Pending`
- `## 7. Artifacts & Outputs` — When files are created or significantly modified, log them

**Update at save points (`/save`):**
- All of the above, plus a checkpoint entry in `## 4. Checkpoints`

**Update at session end (`/end`):**
- `## 5. Session Performance Review (AAR)` — Self-corrections, calibration notes, one-line verdict
- `## 6. Synthetic RLHF Log` — What you learned about the user, what worked, what to adjust
- `## 8. Cross-Session Links` — Related sessions or protocols
- `## 9. Parking Lot (Deferred)` — Unfinished items or ideas to revisit

### Memory-First Principle

Before answering questions about past decisions, prior work, or project history:
- Check `.context/memories/session_logs/` for relevant session logs
- Check `.context/project_state.md` for current project status
- If Athena MCP is available, use `smart_search` to find relevant context

### Committee of Seats (COS)

For non-trivial decisions, spawn the appropriate COS agents from `.claude/agents/`:
- Simple changes: No committee needed
- Medium changes (new feature, refactor): Spawn 2-3 relevant agents
- Complex changes (architecture, security, deploy): Convene the full committee
