---
globs: ".framework/**"
description: Rules for handling Athena framework files
---

## Framework Files Are Read-Only References

Files in `.framework/` define the project's Core Identity and operating principles.

- **Read** these files to understand the project's values and constraints
- **Never modify** these files during normal work (they are integrity-checked via SHA-384)
- If a change to Core Identity is needed, flag it explicitly and get user approval first
- Reference these principles when making architectural or strategic decisions
