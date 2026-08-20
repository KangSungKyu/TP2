# 몬스터 AI 패턴 및 상태 머신 명세서

## 개요

- 목표: `MonsterPatternData` 기반 패턴 선택·예약·실행·취소와 이동 writer의 단일성을 보장한다.
- 패턴 실행은 100ms 직렬 AI cycle이 소유하며 동시 queue를 지원하지 않는다.

## 핵심 인터페이스

- `ExecutePatternAsync(MonsterPatternData pattern, CancellationToken cancellationToken)`
- `CancelCurrentPattern(PatternCancelReason reason)`
- `PatternState CurrentPatternState`
- `PatternSnapshot CurrentPatternSnapshot`
- `SupportsPatternQueue == false`; `EnqueuePattern(...)`은 `NotSupportedException`을 던진다.

`PatternSnapshot`은 State, Pattern/Skill idx, generation, elapsed, token 보유 상태를 읽기 전용으로 제공한다.

## 데이터 검증

1. `PatternIdxList`는 1개 이상 16개 이하만 허용한다.
2. 각 idx는 유효한 `MonsterPatternData`와 `SkillData` FK를 가져야 한다.
3. `RandomWeight == 0`은 현행 그대로 선택 확률 0이며 Sequence로 강제 전환하지 않는다.
4. `TriggerSubject`는 `Self(0)` 또는 `CurrentTarget(1)`만 허용한다.
5. HP ratio Trigger의 subject는 Self, 거리·groggy Trigger의 subject는 CurrentTarget이다.

## 100ms 직렬 AI 선택 규칙

선택 우선순위는 `Trigger > Random > Sequence > Simple`이다.

- Trigger 조건은 collider center 기준 detection distance 또는 상태값으로 평가한다.
- 현재 실행 중인 패턴을 Trigger가 선점 취소하지 않는다. 실행·정리가 끝난 다음 AI cycle에서 다시 선택한다.
- Random 후보 총 weight가 0이면 Random 선택은 발생하지 않는다.
- Sequence는 성공 후보의 다음 index를 보존하고, Simple은 마지막 fallback이다.
- 선택된 패턴은 종료될 때까지 직렬 실행하며 queue·병렬 패턴을 만들지 않는다.

## 거리·이동 책임 분리

| 목적 | 기준 | writer |
|---|---|---|
| Trigger detection | 양측 collider center 거리 | 읽기 전용 |
| 공격 시작 가능성 | 양측 collider surface gap과 start-distance band | 읽기 전용 |
| Chase·공격 이동 | 발 위치·벽·SpawnArea bounds를 따르는 motor 이동 | `KinematicMotor2D` 단일 writer |

- collider center detection, surface-gap 공격 판정, foot 기반 이동을 서로 대체하지 않는다.
- AI·Animator·SkillExecutor가 Transform을 직접 경합하지 않으며 공격 이동도 motor velocity writer를 사용한다.

## Pattern lifecycle 및 Reservation

1. `Reserved`: Skill FK, start-distance band 및 reservation 가능성을 확인한다.
2. `Chase`: Trigger·Random·Sequence·Simple 모두 `ChaseTimeout` 전체를 reservation 기간으로 사용한다. 도중에 band에 진입해도 공격을 조기 실행하지 않는다.
3. 매 AI step은 현재 gap의 `correction`과 `remaining = ChaseTimeout - elapsed`로 필요한 속도를 다시 계산하고 motor를 구동한다.
4. timeout 종료 시 band 안이면 `Startup + AttackMotion`을 실행하고, band 밖·벽·SpawnArea 이탈이면 공격 없이 취소한다.
5. 실패한 reservation은 같은 frame 재선택을 차단하고 다음 100ms AI cycle까지 backoff한다.
6. attack token은 Startup부터 마지막 Active window까지 보유하고 Recovery 전에 해제한다.
7. telegraph와 multihit은 현행 SkillData timing/window를 사용하며 각 Active window를 순서대로 실행한다.
8. cooldown과 Sequence 다음 index는 패턴 성공 시에만 commit한다. 선택·reservation·실행 실패에서는 기존 값과 cooldown을 소비하지 않는다.
9. 종료 후 `Recovery`를 거쳐 `Idle`로 복귀한다.

Reservation 판단 순서는 `Reservation → full-duration ChaseTimeout(correction/remaining) → start-distance band 재확인 → Startup + AttackMotion → success commit`이다. token·telegraph·motion·hitbox·projectile의 소유 generation은 동일 pattern lifecycle을 따른다.

## 취소·예외 정리

- disable, death, groggy, Returning, timeout, 명시적 취소 및 예외는 `CancelCurrentPattern(reason)`으로 수렴한다.
- 취소 시 linked token, AttackMotion, hitbox, telegraph, active effect/projectile, attack token, current pattern을 정리한다.
- 예외는 Unit/Pattern/Skill idx와 함께 기록하고 같은 frame의 재선택을 차단한다.
- 정리 후 Returning 사유만 `Returning`, 나머지는 `Idle` 상태로 복귀한다.
- `finally`에서도 motion·telegraph·hitbox·token을 다시 안전 정리해 잔존 writer와 token을 남기지 않는다.

## QA 상태

| 게이트 | 결과 |
|---|---:|
| Compile·제품 Console | Error 0 |
| CSV/FK·PatternList 정적 검증 | PASS |
| API·State·Snapshot·Queue 정적 계약 | PASS |
| Unity runner | stale/실행 0건으로 `BLOCKED` |

Unity runner의 stale 또는 `0/0` 결과는 runtime PASS로 간주하지 않는다. focused 실행에서 lifecycle, Trigger 다음-cycle 선택, reservation/chase band, token·telegraph·multihit 및 예외 cleanup을 재검증해야 한다.
