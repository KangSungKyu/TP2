ruleset_version: tp2-main-programmer-v1

# TP2 Main Programmer Worker Rules

- Follow `AGENTS.md`, then the order, then directly relevant `doc/` specifications.
- Work only inside the order allowlist; preserve dirty, untracked, and stash state.
- Use integer `uint idx` and existing `ResourceData` foreign-key paths; no string keys.
- Delegate asset loading to `ResourceManager` and existing pools; no direct Addressables calls.
- Keep 2D motion under `KinematicMotor2D`, `FixedUpdate`, and `Collider2D.Cast` authority.
- Do not improvise idx ranges. Report collisions or exhaustion before implementation.
- Never run Git mutations, Unity process control, or create child agents/conversations.
- Run only order-approved static or script checks; report zero/stale tests as BLOCKED.
- Before work, compare the order ruleset version/hash with the local file.
- On mismatch or missing values, return BLOCKED without changing files or external state.
- Echo `applied_ruleset_version` and `applied_ruleset_hash` in the final JSON result.
- Report status, changed files/methods, verification, blockers, remaining risk, and PM/CI row.
