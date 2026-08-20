# 룸 시퀀서 및 스테이지 명세서 (Stage Sequencer)

## 개요
- HubScene -> MainScene 전환 시 Addressables 기반 비동기 동적 룸 청크 로딩 및 씬 빌딩을 책임진다.
- 스테이지 메타데이터는 StageData.csv (Type 9: 9001~)에서 idx 기반으로 로드되어야 한다.

## 핵심 인터페이스 (함수 시그니처)
- UniTask EnsureStageLoadedAsync(uint stageIdx, CancellationToken cancellationToken = default)
- UniTask<GameObject> LoadRoomChunkAsync(uint roomResourceIdx, CancellationToken cancellationToken = default)
- void BuildSceneFromChunks(IEnumerable<GameObject> roomChunks)

## 로딩 아키텍처
- 단계:
  1) DataTableManager에서 StageDataTable을 통해 StageBaseData를 조회
  2) StageBaseData.RoomSequenceIdxList를 순회하여 각 RoomChunk의 ResourceData.Path를 ResourceManager에 요청
  3) ResourceManager.LoadAssetAsync<GameObject>(path)를 통해 Addressables 비동기 로드 (await)
  4) 로드한 RoomChunk들은 씬 빌더에게 전달되어 순차적으로 배치/초기화

## 동시성 및 장애 허용(방어적 제약)
- 동시 로드 제한: 동시에 Addressables에 요청 가능한 최대 동시성 N = 6 (권장)
- 타임아웃: 개별 룸 청크 로드 타임아웃 T_chunk = 10초. 타임아웃 발생 시 해당 청크는 placeholder로 대체하고 로드 실패 로그 남김
- 누락/오염 데이터: ResourceDataTable.TryGetResource가 false를 반환하면 빈 placeholder(프리팹: 'Placeholder_MissingRoom')를 즉시 사용

## 씬 빌딩 순서 보장
- 룸 순서(Sequence)를 유지하여 BuildSceneFromChunks에서 인덱스 순으로 배치
- 의존성(예: DoorPortal <-> NeighborRoom 링크)은 빌드 시점에 후처리 매칭 단계로 분리하여 순환 참조 문제를 방지

## 검증 포인트
- Stage 전환 실패 시 복구 절차:
  - 모든 청크 로드 실패 -> Hub로 복귀 및 사용자 친화적 오류 메시지(개발 빌드: 강력한 로그)
  - 일부 청크 실패 -> 실패 청크를 Placeholder로 대체하고 Play 계속
# Phase 3 Multi-SpawnZone Runtime Contract (2026-08-06)

- `UnitSpawner` consumes authored `SpawnPointMarker` components under the loaded chunk root; it never creates runtime markers.
- Combat/Elite encounters require at least three Monster zones. Zone centers stay at least 15m apart, the player entry stays at least 14m from combat zones, and each portal socket keeps a 7m combat-free radius.
- Encounter allocation is deterministic from the run seed and current slot, uses each selected zone once, caps active monsters at four, caps simultaneous attack tokens at two, and permits at most one threat-cost-3 unit (`3103` or `3106`).
- Entry/Rest chunks produce no monsters. Resource `1042` preserves its authored Boss marker flow.
- Invalid or insufficient authored zones emit an explicit error and may spawn one monster at one existing marker as the only fallback; silent same-point stacking is forbidden.
# Runtime Encounter/SpawnZone Fix (2026-08-07)

- Stage1RunGenerator assigns MonsterEncounterData only to authored Combat/Elite chunk types. Entry, Reward, Rest, Treasure, and BossGate slots always carry an empty encounter.
- UnitSpawner searches the loaded chunk root with inactive children included. A non-combat chunk with no Monster marker exits silently; a combat encounter with no marker logs ChunkResourceIdx, ResourceData path, root name, and active state.
- Combat 1050-1053 consume their three authored SpawnPointMarker components. Runtime marker creation and silent fallback for a zero-marker combat chunk remain forbidden.

# Stage 1 SpawnArea 활동·어그로 계약 (2026-08-19)

| 영역 | 구현 계약 | 실패 완화·Assert |
|---|---|---|
| 정의 | `UnitSpawner.ResolveMovementBounds(roomInstance)`가 선택한 청크 trigger bounds를 단일 `SpawnArea`로 사용하고, 각 Monster의 생성 위치를 `spawnOrigin`으로 1회 저장한다. 신규 manager·CSV·문자열 키를 추가하지 않는다. | bounds가 없거나 spawnOrigin이 Monster collider extents만큼 inset한 bounds 밖이면 해당 marker를 거부하고 기존 1개체 fallback만 허용한다. |
| 활동 경계 | Monster body bounds가 inset SpawnArea 안일 때만 추격·공격한다. 이동은 기존 `KinematicMotor2D.SetHorizontalMovementBounds`와 AI 이동을 사용한다. | 한 `FixedUpdate`의 swept body가 외곽을 넘기기 전에 속도 `0` 및 Return 전환; Transform 직접 이동 금지. |
| 어그로 획득 | Player body bounds가 inset SpawnArea 안에 있고 Monster가 Return 중이 아닐 때 기존 AI 루프가 target을 사용한다. | 경계 접촉만으로 재획득하지 않는다. Player가 raw bounds 밖으로 완전히 나갔다가 다시 inset bounds에 완전히 들어와야 재획득한다. |
| 어그로 해제 | Player body bounds가 raw SpawnArea 밖, Monster swept body 이탈, Door/Portal 전환 시작, Player 사망·비활성 중 하나면 즉시 해제한다. | 같은 tick에 `actionGeneration++`, telegraph 종료, 공격 토큰 반환, 속도 `0`, 활성 SkillEffect 취소, 소유 projectile 전량 pool return. |
| 안전 복귀 | 기존 Idle/이동 상태로 `spawnOrigin`까지 복귀하고 도착 후 Idle·재획득 가능 상태가 된다. | 경계 밖 또는 지형 단절로 motor 복귀가 불가능하면 `KinematicMotor2D.Teleport(spawnOrigin)` 1회; origin 지지면이 무효면 해당 Monster를 pool return한다. |
| 전투 상태 | 일반 Monster는 Return 시작 시 HP를 MaxHp, Posture를 `0`으로 복구하고 패턴 cooldown·sequence를 초기 상태로 되돌린다. | 복귀 중 피격·피해·공격·보상 발생 금지; 사망 개체는 복귀하지 않는다. |
| 전환 | Door/Portal 전환 generation이 바뀌면 현재 청크 일반 Monster와 그 effect/projectile을 기존 pool 경로로 정리한다. cleared 청크 재방문 시 재생성하지 않는다. | 이전 청크 객체·투사체·공격 토큰 잔존 `0`. Stage 1 `hazardCount == 0` 유지. |
| Boss 예외 | `3201`은 Player 경계 이탈에 따른 HP/Posture reset과 일반 Return을 사용하지 않는다. Boss arena movement bounds만 강제하고 Door는 전투 중 잠근다. | Boss가 arena swept bounds를 넘으면 속도 `0` 후 authored Boss origin으로 복귀하되 HP/Posture 유지; 씬 전환 시에는 일반 pool 정리 계약 적용. |
| 15 FPS | 경계 판정은 렌더 프레임이 아니라 `FixedUpdate` 누적 이동과 swept body로 수행한다. | 15/60 FPS에서 외벽 이탈 `0`, 경계 왕복 30회당 어그로 전환은 완전 진입·이탈당 각 `1회`, 토큰·telegraph·projectile 잔존 `0`. |

## 메인프로그래머 P0 구현 순서

1. `UnitSpawner`가 기존 movement bounds와 spawnOrigin을 일반 Monster에 bind한다.
2. `Monster`에 `Idle/Combat/Return` 최소 상태와 경계 판정을 추가하되 별도 leash manager는 만들지 않는다.
3. Return 진입은 기존 action generation·telegraph·token·effect/projectile 정리 메서드를 재사용한다.
4. Boss는 같은 bounds 검사만 재사용하고 일반 Monster의 HP/Posture reset 분기는 제외한다.
5. QA는 일반 Monster 경계·Player 경계·Portal 전환·Boss 이탈을 15/60 FPS에서 각각 검증한다.
