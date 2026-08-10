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

## Combat state gate update (2026-08-07)

- Normal and execution damage share `CombatStats.ApplyHpDamage`; HP zero publishes death once, and further damage is ignored until `InitStats` resets pooled state.
- Monster Groggy, death, disable, and pool reset invalidate `actionGeneration`. Movement is zeroed immediately and every delayed pattern callback checks the captured generation before animation, hit, projectile, or effect work.
- `CanAct` is the single AI gate. Groggy blocks movement and attacks until its existing `OnGroggyEnded` contract, while death remains permanently blocked for that pool generation.

## HUD bind update (2026-08-07)

- Main HUD repairs a zero-authored root scale to `Vector3.one` at runtime without modifying the scene asset, then binds an existing or later activated Player and refreshes HP/Posture/MP immediately.
- Boss activation is ignored until `UnitData` initialization is complete and the object is active. Death, pool disable, and chunk unload reuse `Monster.Deactivated` to unbind and hide the panel.
