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
