# 플레이어 점프·벽점프·회피 상호작용 명세

## 1. 운동학 기준

- 플레이어 이동은 `KinematicMotor2D`의 `FixedUpdate`와 `Collider2D.Cast()` 기반 커스텀 운동학 해석을 사용한다.
- Rigidbody2D Dynamic 물리 이동을 게임플레이 이동의 기준으로 사용하지 않는다.
- 수평·수직 이동은 분리된 Cast pass로 충돌 거리를 보정하고, 피부 폭보다 안쪽으로 진입하지 않는다.

## 2. 점프와 벽점프

- 일반 점프는 접지, 코요테 타임, 점프 버퍼를 사용한다.
- 벽점프는 공중에서 `WallDir != 0`이고 `WallJumpSurface.CanWallJump`가 허용할 때만 가능하다.
- 벽점프 직후 `0.18초` 동안 수평 입력을 잠가 반동 궤적을 보존한다.
- `AllowSameWall = false`인 벽에서는 동일 벽 연속 점프를 허용하지 않는다.
- 1-Way 플랫폼 측면은 벽점프 표면으로 판정하지 않는다.

## 3. 회피·가드와 입력 배타성

- 회피, 가드, 패링 중에는 일반 점프 입력과 일반 수평 이동 입력을 무시한다.
- 공중 회피 중 방향키를 바꿔도 회피 시작 시 결정된 수평 대시 속도를 덮어쓰지 않는다.
- 회피 중 벽에 접촉하면 회피 상태를 해제하고 유효한 경우 벽점프로 전이할 수 있다.
- 점프·회피·가드 입력 연타로 비동기 동작이 중첩되지 않도록 각 상태 진입 전에 `IsDodging`, `IsGuarding`, `IsParrying`, 공격 상태를 검증한다.
- 지면 관통은 명시적인 하향 점프 입력에서만 요청하며 일반 점프·회피·가드 연타로 활성화하지 않는다.
- 취소 토큰 발생 시 상태가 영구 고정되지 않도록 비동기 방어 동작은 상태 복구 경로를 유지한다.

## 4. 관련 구현 및 검증

- `Assets/Scripts/Gameplay/Player.cs`
- `Assets/Scripts/Gameplay/KinematicMotor2D.cs`
- `Assets/Scripts/Gameplay/WallJumpSurface.cs`
- `Assets/Scripts/Gameplay/OneWayPlatformPassThrough.cs`
- `Assets/Editor/Tests/TilemapStageBuilderTests.cs`

최종 소급 점검: 2026-08-05
