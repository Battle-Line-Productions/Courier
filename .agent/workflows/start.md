---
description: Boot the AI assistant with context
---

# /start — Execution Script

> **Automation**: This workflow runs the Athena SDK boot sequence.

## Execution

// turbo

```bash
athena
```

The boot sequence will:
1. Load Core Identity
2. Recall last session context
3. Create a new session log
4. Confirm ready status

## Manual Override (if SDK unavailable)

- [ ] Read `.framework/modules/Core_Identity.md`
- [ ] Find the latest session log in `.context/memories/session_logs/`
- [ ] Create a new session log: `YYYY-MM-DD-session-XX.md`
- [ ] Output: "⚡ Ready. (Session XX started.)"
