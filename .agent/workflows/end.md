---
description: Close session and save learnings
---

# /end — Session Close

> **Automation**: This workflow runs the Athena SDK shutdown sequence.

## Execution

// turbo

```bash
athena --end
```

The shutdown sequence will:
1. Close the current session log
2. Update session status to "Closed"
3. Optionally trigger Supabase sync

## Manual Override (if SDK unavailable)

- [ ] Read all `### ⚡ Checkpoint` entries from current session log
- [ ] Fill in Key Topics, Decisions Made, Action Items
- [ ] Git add and commit the session log
- [ ] Output: "✅ Session XX closed and committed."
