# Knockback and Monster/Boss HUD Runtime Contract

Updated: 2026-08-07

## Knockback

- `SetTargetVelocityX` stores Player input or Monster AI intent; knockback never overwrites that target.
- `CombatStats` reuses its existing 0.15-second hit-feedback duration when calling `KinematicMotor2D.ApplyKnockback(Vector2, float)`.
- The motor counts the override in fixed steps. A later hit increments the generation and replaces the active override; target velocity resumes after the newest window.
- `KinematicMotor2D` remains the single movement authority. `Rigidbody2D.AddForce`, direct dynamic movement, and non-fixed knockback callbacks are forbidden.

## HUD

- A regular Monster binds serialized `MonsterOverheadHUD` HP/Posture fills on enable, initializes them from Current/Max, and removes listeners on disable or pool return.
- `BossMonster` is rejected by the overhead component. Boss spawn binds only `ProductionMainHUD` BossGroup; death, pool return, and chunk unload unbind and hide it.
- Global `OnGUI`/`Update`, runtime Canvas creation, sprite generation, `Find`, and component lookup are forbidden. UI assets remain resource-authored serialized references.

## QA

- Knockback must end after the fixed-step reaction window, consecutive hits must be latest-wins, and slope/ground collision rules must remain unchanged.
- Regular Monster and Boss visibility, immediate Current/Max fill, HP/Posture change, death/unload hide, and pooled listener duplication must be asserted.
