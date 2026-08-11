# QA Test Report

최종 갱신: 2026-08-10 18:22 KST

## 2026-08-10 Stage1 module/chunk 신규 Assert

| 계약 | 신규 통과 기준 |
|---|---|
| OneWay 재착지 | 실제 `Unit_3001/KinematicMotor2D`가 좌·우 각 2회 착지 → 하향 이탈 → collider 완전 이탈 → 동일 발판 재착지 |
| Socket 안전성 | Stage1 room 11, socket 44/44, 실제 지지면 3-cell landing, EntryMarker `surface + 0.51m`, head clearance 2m |
| Room 도달성 | 11 room × 12 ordered pairs = 실제 motor replay 132/132 PASS |
| Module/Room OneWay | authoritative module 20/20 및 room 11/11이 layer 10, collider effector, one-way surface arc 계약 유지 |
| Spawn/Camera | combat room 11050~11053 각 SpawnZone 3개, room 11/11 CameraBounds 60×30 |
| Stage graph | 200 seeds에서 reciprocal connection과 room resource loadability PASS |
| 회귀 | 신규·관련 18/18, PlayMode 1/1, QATestRunner 81/81 PASS |

### 인프라 차단

| 테스트 | 결과 | 분류 |
|---|---:|---|
| `PlayerPool_DespawnAndRespawnReuseSameIdentity` | 180초 TIMEOUT | `editor_unfocused` Addressables async 대기; Assert 실패·제품 예외 0 |
| `UnitPrefabFk_InstantiatesThroughResourceManager` | 180초 TIMEOUT | `editor_unfocused` Addressables async 대기; Assert 실패·제품 예외 0 |

`QATestRunner`는 `[UnityTest]`, `[TestCaseSource]`, `Stage1P0ResourceTests`, `Stage1TraversalGateTests`를 포함하지 않으므로 보조 지표로만 사용한다.

최종 갱신: 2026-08-07 18:57 KST

## 2026-08-07 Portal/Landing·비동기 회귀 신규 Assert

| 계약 | Assert |
|---|---|
| Portal surface | Stage1 11 rooms의 marker 44/44가 `portal center = supporting surface + 1.0m`, trigger bottom이 surface 이상을 만족한다. |
| Entry/head clearance | EntryMarker null 0, `surface + 0.51m`, portal head clearance 2m 이상을 유지한다. |
| High landing | 고지 Portal은 solid Ground 3×2 이상이며 접근 step ≤1m, gap ≤2m이다. |
| One-way role | 끊긴 1~2셀 구간은 0이며 최종 one-way 42 cells, 신규 solid 124 cells이다. 셀 총개수 자체를 고정 Assert로 사용하지 않는다. |
| Spawn clearance | Combat spawn과 socket 사이 최솟값은 7.8103m로 7.75m 계약을 만족한다. |
| Corrected rooms | Room_11056 East와 Room_11052의 Portal/Landing surface 계약을 동일 역할 Assert로 검증한다. |
| Particle async | pending load는 Error 0, success는 prefab 할당, completed-null은 단일 Error, disable 이후 stale completion은 무시한다. |
| DataTable fixture | ResourceData 1001은 `Unit_3001`을 유지하며 실패 경로 테스트가 기존 UnitBase/Resource 테이블 참조를 `finally`에서 복원한다. |

## 2026-08-07 최종 집계

| Gate | 결과 |
|---|---:|
| Compile Error | 0 |
| 신규 전용 | 4/4 PASS |
| 전체 EditMode | 112/112 PASS — 포커스 의존 2건은 환경 분리 |
| 기존 PlayMode | 1/1 PASS |
| QATestRunner | 80/80 PASS |
| Console 제품 Error | 0 |
| target7 / Particle / Unit3001 Error | 0 |
| 포커스 의존 테스트 2건 | 미실행·환경 분리, 제품 수정 대상 아님 |

최종 갱신: 2026-08-07 17:31 KST

## 신규 Assert

| 계약 | Assert |
|---|---|
| Execution | HP가 0 이하가 되는 공통 경로에서 `Monster.Die`가 정확히 1회 실행된다. |
| Groggy | 이동·공격 중 Groggy 진입 시 즉시 중단되고 해제 전 재개하지 않는다. |
| Death | 공격 중 사망하면 generation이 무효화되며 fade 동안 hit/projectile/effect callback이 실행되지 않는다. |
| Player HUD | spawn/reuse 직후 HP/Posture/MP 현재값과 최댓값을 바인딩하고 변경 이벤트를 반영한다. |
| Boss HUD | 미조우 시 숨김, spawn 시 초기화·표시, 사망/despawn/chunk unload 시 해제·숨김 처리한다. |
| Pool reuse | 재사용 시 listener가 중복 등록되지 않는다. |
| SuperArmor 4 combinations | Monster/Boss 모두 armor off에서는 damage를 유지하며 기존 knockback을 허용하고, armor on에서는 동일 damage를 유지하며 knockback과 이전 override 잔존을 0으로 만든다. (4/4 PASS) |
| Chunk/Room UI | Production StageProgress는 데이터·바인더를 유지한 채 기본 hidden이다. |
| Stage1 Entry | 4방향 socket이 존재하고 정적 gate는 off이며 유효 연결만 기존 portal 전환 경로로 처리한다. |
| MainHUD stats | HP/MP/Posture 배경과 current/max text가 초기화·변경·reuse에서 동일 stats에 바인딩된다. |
| Sorting roles | SortingLayer 순서와 Unit 7, WorldUI 5, Tilemap 20, Effect 14 역할 참조를 검증한다. Far/Near 실제 renderer는 현재 0이다. |
| ProductionMinimap | M toggle, Canvas 1, 12 room과 unknown/visited/cleared/current/boss 상태를 기존 StageRun으로 표시한다. |
| Common Portal contract | 4방향 모두 공용 `Portal_Gate/RoomDoorPortal`과 명시 `TargetSlotIdx`를 사용하며, 상호 mask가 없는 target과 `byte.MaxValue`는 거부한다. 왕복 target과 safe entry를 유지한다. |
| Portal asset accessibility | 11 prefab의 floor socket 44/44 접근 가능, EntryMarker null 0, static portal 0, 신규 발판 0이며 1041/1042는 각각 4 sockets이다. |
| Portal lifecycle | `OwnerSlotIdx`와 `RoomGeneration`이 현재 room과 일치할 때만 입력을 허용하고, stale target 7 portal과 중복 trigger는 무로그로 차단한다. |
| Metroidvania accessibility | Stage1 room 11종은 동일 `Portal_Gate`, socket 44/44 접근, platform 98, max step 1m/gap 2m, spawn clearance min 7.75m 계약을 사용한다. |
| Tilemap role | 추가 접근 platform을 포함한 모든 Stage1 room `TilemapRenderer`가 `Tilemap` layer를 사용하며 총개수에는 의존하지 않는다. |

## 최종 집계

| Gate | 결과 |
|---|---:|
| Compile Error | 0 |
| 신규 Portal lifecycle/role EditMode | 3/3 PASS |
| 전체 EditMode | 112/112 PASS |
| 기존 PlayMode | 1/1 PASS |
| QATestRunner | 79/79 PASS |
| Console 제품 Error | 0 |
| target 7 warning | 0 |
| 종료 상태 | InitScene 비Play |
| 16:9 실제 UI 캡처 / 물리 M 입력 / 실전 portal 전환 | 하네스 부재로 미검증 |
| 실제 floor portal 물리 입력 / 왕복 smoke | 하네스 부재로 미검증 |
| 저·중·고 실제 motor path probe | 입력 하네스 부재로 미실행 |
| platform 98 / max step 1m·gap 2m | 리소스 정적 검증 근거이며 최종 QA에서 독립 재계측하지 않음 |
