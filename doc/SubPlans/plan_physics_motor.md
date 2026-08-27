# 물리 모터 서브 명세서 (KinematicMotor2D)

## 개요
- Non-Physics(키네마틱) 모터로서 FixedUpdate 기반 2-pass 이동을 사용한다.
- 핵심 목표: 프레임 독립성 유지, 경사면 이동 오차(속도 편차) ±5% 내 보정, 벽/발판 예외 안전성 확보

## 핵심 인터페이스 (함수 시그니처)
- void InitMotor()
- void SetTargetVelocityX(float vx)
- void SetVelocityY(float vy)
- void SetJumpHeld(bool held)
- UniTask PassThroughOneWayPlatformAsync(float durationSec = 0.35f, CancellationToken cancellationToken = default)
- void Teleport(Vector3 position)
- void SetGroundNormal(Vector2 normal)
- void SimulateStep(float dt)

## FixedUpdate 분리 이동 아키텍처
- FixedUpdate는 반드시 고정 델타 시간(Time.fixedDeltaTime)을 사용하여 물리 시뮬레이션을 진행한다.
  - 권장 변경: SimulateStep(Time.fixedDeltaTime)으로 호출을 고정
  - 현재 코드: FixedUpdate에서 Time.deltaTime 사용(위반)

## 경사면 투영 및 속도 보정
- moveAlongGround = new Vector2(groundNormal.y, -groundNormal.x)로 수평 성분을 계산한다.
- 방어적 제약: 경사면에서의 수평 속도 투영 결과가 원래 targetVelocityX 대비 ±5% 이내여야 함.
  - 수식 검증: projectedSpeed = Vector2.Dot(moveAlongGround.normalized, Vector2.right) * targetVelocityX
  - 허용범위: |projectedSpeed - targetVelocityX| <= 0.05 * max(1.0f, |targetVelocityX|)
  - 위반 시 즉시 보정: targetVelocityX = Mathf.Sign(targetVelocityX) * Mathf.Abs(projectedSpeed) (또는 보정 펙터 적용)

## 1-Way 발판 옆면 벽점프 필터링 수칙
- 1) 발판 옆면(Edge) 충돌로 인한 벽점프 오탐 발생 시 이중 필터 적용:
   - 충돌 정상법선(groundNormal.y) 검사: 착지로 간주하려면 groundNormal.y >= MinGroundNormalY
   - 충돌 레이어 검사: OneWayPlatformLayer에 해당하는 충돌은 수직(하향) 이동에서만 무시/통과 처리
- 2) WallJump 허용 조건:
   - WallSurface != null && WallSurface.CanWallJump
   - 충돌 표면의 법선이 수직 축에서 ±75° 이내 (즉, normal.x 절대값 > 0.2 권장)
   - 옆면 충돌의 경우 발판과의 동시 충돌이면 벽점프를 무시

## 안전성(예외 및 방어)
- Collider2D.Cast 사용 시 결과 버퍼(hitBuffer)의 크기(현재 16)는 초과 가능성 점검. 만약 충돌 수가 버퍼 용량 초과 시 로그 경고 및 처리(최상위 충돌만 처리).
- Rigidbody2D 설정 권장값: body.bodyType = Kinematic, useFullKinematicContacts = true
- 중대한 위반 발견 시: 로그 에러 + 예외 던지지 않고 안전한 기본상태(groundNormal = Vector2.up, Velocity.y = Mathf.Min(Velocity.y, 0))로 복구

## Player Dodge Charges P0 계약 (2026-08-27)

### 충전·소비 수명주기

- 최대 충전량은 `3`이다. 실제 Dodge가 승인된 시점에만 `1`을 소비하며, charge가 `0`이면 입력·상태·타이머를 변경하지 않는다.
- 소비된 각 charge는 서로 독립된 scaled time `2.0s` 타이머로 FIFO 재충전한다. `Groggy`와 hitstop 중에도 재충전은 지속한다.
- 세 slot은 각자의 read-only scaled progress `0→1`을 노출한다. 미소비 slot은 `1`, 소비 직후 `0`, 독립 재충전 완료 시 `1`이다.
- death, Hub 진입, pool 반환·재사용, 명시적 reset, `MainScene` entry에서는 charge를 `3`으로 복구하고 진행 중 타이머·이전 generation을 모두 폐기한다.
- charge 개수 통지는 `OnDodgeChargesChanged(current, max)` 이벤트만 사용한다. progress polling은 MainHUD가 미완료 slot을 표시하는 동안의 단일 루프만 허용한다.

### Dodge 종료 recovery

- Dodge 종료 직후 scaled `0.1s` recovery 동안 이동·점프·공격·방어·Dodge를 포함한 전 행동 입력을 차단한다.
- `Death`와 `Groggy`가 recovery보다 우선하며, 진입 시 recovery token·generation·입력 잠금을 폐기한다.
- cancel, death, groggy, Hub, pool, reset, MainScene entry 후 이전 recovery callback·입력 잠금·generation 잔존은 `0`이다.

### MainHUD 표시 계약

- `MainScene`의 MainHUD에 black background `3`개와 progress overlay `3`개를 둔다. 두 계층 모두 같은 기존 solid sprite/material을 재사용하며 신규 sprite·material은 만들지 않는다.
- overlay는 `Image.Type=Filled`, `FillMethod=Radial360`, `FillOrigin=Top`, clockwise로 두고 slot별 read-only progress `0→1`을 표시한다.
- charge 이벤트가 미완료 slot 존재를 알릴 때만 단일 polling loop를 시작하고, 전 slot progress가 `1`이면 즉시 중지한다. loop·coroutine 중복은 `0`이다.
- 초기 진입·reset 직후 표시값은 `3/3`, Hub의 Dodge HUD 오브젝트 수는 `0`, pool 재사용 후 stale 아이콘·구독·polling loop·타이머는 `0`이다.

### 검증 기록·잔여 위험

- 메인 recovery/radial HUD 반영 후 compile 및 제품 Console Error `0`; 리소스 HUD focused `1/1 PASS`.
- 메인의 corrected dynamic fixtures를 대상으로 한 독립 QA는 MCP stale 상태로 Started `0`이므로 `BLOCKED`다. 독립 실행 증거가 생기기 전에는 전체 PASS로 승격하지 않는다.
- 후속 위험은 독립 QA에서 recovery `0.1s` 전 입력 차단·Death/Groggy 우선·stale `0`, slot별 FIFO progress, incomplete-only 단일 polling 및 Hub HUD `0`을 재검증하는 것이다.

### 🧠 [Physics/Motor 자율 회고]

- 2026-08-27 Dodge charge 회고: recharge를 단일 공유 타이머나 HUD polling에 결합하면 연속 소비 순서와 reset 경계가 흐려진다. 소비별 FIFO 타이머와 단일 변경 이벤트를 권위로 두고 모든 생명주기 경계에서 stale 상태를 함께 폐기한다.
- 2026-08-27 recovery/radial HUD 회고: charge 개수는 이벤트로 유지하되 연속 progress 표현에 필요한 polling은 미완료 slot이 있는 단일 루프로만 제한한다. recovery는 다른 행동 상태가 아니라 Dodge 종료 잠금이며 Death/Groggy 전이가 항상 우선한다.

### 🔄 [PM/CI 동기화] 변경 이력 테이블

| 최근 수정 일시 | 수정자 (역할) | 수정 및 추가된 파일/메서드 명세 | QA 검증 통과 기준 (Assert) |
| :--- | :--- | :--- | :--- |
| 2026-08-27 | 문서작업자 | `plan_physics_motor.md` / Player Dodge Charges P0·MainHUD 계약 | main core `1/1`, resource HUD `1/1`, compile·제품 Error `0`; independent QA Started `0` BLOCKED |
| 2026-08-27 | 문서작업자 | `plan_physics_motor.md` / Dodge recovery `0.1s`·slot progress·Radial360 HUD | resource HUD `1/1`, compile·제품 Error `0`; corrected dynamic fixture QA Started `0` BLOCKED |
