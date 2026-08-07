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
