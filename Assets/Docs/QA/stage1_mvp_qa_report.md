# Stage 1 최종 통합 QA 보고서

- 실행일: 2026-08-05 (Asia/Seoul)
- 환경: Unity 6000.4.8f1, WindowsEditor
- 범위: QA 테스트 및 보고만 변경, 제품 코드 변경 없음

## 최종 판정

| 구분 | 결과 | 실제 수치/근거 |
|---|---|---|
| EditMode 전체 | PASS | 59/59 |
| QATestRunner | PASS | 50/50 (`Logs/qa_test_results.txt`) |
| PlayMode 전체 | FAIL | 0/1, `AttackHandlerTests.MeleeAttackAppliesDamage` 실패 |
| Unity Console 컴파일 오류 | PASS | 최신 재컴파일 후 0건 |
| 게임 시작 HeaderValidationException | 미검증 | 실제 Stage 1 부팅 PlayMode 자동화가 없음 |

## 요구사항별 결과

| # | 검증 | 판정 | 근거 |
|---|---|---|---|
| 1 | Init→Hub→1040→1057→인접 청크→BossGate→1042 | 미검증 | 해당 실제 종단 흐름을 구동하는 PlayMode 테스트가 없고, 현재 `InitScene.nextScene` 기본값은 `Main`이다. |
| 2 | 1057 활성 연결별 visible Portal_Gate, trigger, TargetSlotIdx | PASS(계약) | EditMode의 소켓/포털 회귀 테스트 통과. 런타임 실주행은 #1과 함께 미검증. |
| 3 | 입장 직후 즉시 재트리거/왕복 없음 | 미검증 | `RoomDoorPortal` 전환 가드와 입장 중 비활성화 계약은 확인했으나 실제 물리 트리거 주행 테스트가 없다. |
| 4 | 비연결 소켓 비활성, 양방향 마스크 일치 | PASS | 100 seed 이동/상호 연결 및 소켓 바인딩 EditMode 테스트 통과. |
| 5 | Portal_Gate Addressables 해석, 누락 로그 0건 | PASS(정적) | `Prefabs.asset`에 `Portal_Gate` 주소 존재. 실제 종단 PlayMode 누락 로그는 미검증. |
| 6 | OrbitalMarksman/ShieldSentinel 스폰·애니메이션·face-left | PASS(자산 계약) | ResourceData, Prefab, Animator, clip 연결 EditMode 테스트 통과. 실제 스폰/화면 방향 PlayMode는 미검증. |
| 7 | HeaderValidationException 및 Console error 0건 | 부분 PASS | 최신 Console 컴파일 Error 0건. 실제 게임 시작 시 HeaderValidationException은 #1 부재로 미검증. |
| 8 | 전체 실제 PASS 수 | 완료 | EditMode 59/59, QATestRunner 50/50, PlayMode 0/1. |

## FAIL

### PlayMode 공격 회귀 테스트

- 재현: Unity Test Runner → PlayMode → 전체 실행.
- 결과: `Tests.PlayMode.AttackHandlerTests.MeleeAttackAppliesDamage`에서 `Expected 90 +/- 0.01, But was 100`.
- 책임 파일: `Assets/Tests/PlayMode/AttackHandlerTests.cs`, 공격 대상 레이어 계약을 확인할 제품 측 책임 후보는 `Assets/Scripts/Gameplay/Combat/AttackHandler.cs`.
- 최소 수선 명세: 테스트 대상 GameObject를 제품 공격 마스크가 탐지하는 레이어에 배치하거나, 제품의 대상 마스크 주입 계약을 테스트 픽스처에서 명시하고 체력 10 감소를 재검증한다.

## 미구현/미검증

### Stage 1 실제 종단 PlayMode 회귀

- 재현 확인: `Assets/Tests/PlayMode`에는 Stage 1 장면 전환·포털 이동 종단 테스트가 없다.
- 책임 파일: 신규 QA 테스트는 `Assets/Tests/PlayMode/Stage1PlayModeRegressionTests.cs`; 부팅 경로가 명세대로 Hub를 거쳐야 한다면 제품 책임은 `Assets/Scripts/Scene/InitScene.cs`.
- 최소 수선 명세: Init부터 1042 진입까지 실제 장면과 `RoomDoorPortal` trigger를 구동하는 단일 PlayMode 테스트를 추가하고, 각 전환 뒤 `CurrentSlotIdx`, 로드된 `ChunkResourceIdx`, Console 오류를 검증한다. `InitScene.nextScene`의 `Main` 기본값과 Init→Hub 요구사항도 한쪽으로 확정한다.

## 🔄 [PM/CI 동기화] 변경 이력 테이블

|상태|PlayMode|EditMode|QATestRunner|Console|후속 위험|
|---|---|---|---|---|---|
|최종 게이트 FAIL|0/1 (공격 회귀 실패, Stage 1 종단 미구현)|59/59 PASS|50/50 PASS|컴파일 Error 0; 게임 시작 HeaderValidationException 미검증|실제 포털 재트리거, 종단 Addressables 로드, 몬스터 화면 방향은 PlayMode 증거 없음|

## QA 변경 파일

- `Assets/Tests/PlayMode/AttackHandlerTests.cs`: Unity가 실행 가능한 NUnit `async Task` 테스트로 정정. 제품 코드는 변경하지 않음.
- `Assets/Docs/QA/stage1_mvp_qa_report.md`: 실제 실행 결과와 재현 절차 갱신.

## 4방향 진입 배치 후행 QA — 2026-08-06

| 검증 | 판정 | 실제 결과 |
|---|---|---|
| 실제 1050/1057의 North/East/South/West | PASS | 두 프리팹 모두 4개 방향 소켓을 직접 로드해 확인 |
| North 내부 배치 | PASS | socket `(0,29)` → safe `(0,27.99)` |
| East/West/South clearance | PASS | East `(27.49,2.01)`, West `(-28.49,2.01)`, South `(0,2.01)` |
| South 진입 penetration | PASS | `Physics2D.Distance(...).isOverlapped == false` |
| 첫 FixedUpdate 속도/침투 | PASS | `Velocity == 0` 후 simulation, 비침투·grounded 유지 |
| 첫 FixedUpdate 재트리거 | 미검증 | 실제 `RoomDoorPortal` 연속 왕복 PlayMode smoke가 없음 |
| 컴파일/Console Error | PASS | 0건 |
| Stage1 / EditMode / QA | PASS | 20/20, 64/64, 51/51 |
| PlayMode 전체 | FAIL(기존 결함) | 0/1, 공격 테스트 체력 Expected 90 / Actual 100 |

실제 프리팹 경로: `Assets/Prefabs/Rooms/Room_11050.prefab`, `Assets/Prefabs/Rooms/Room_11057.prefab`.

PlayMode 실패 재현: Test Runner에서 PlayMode 전체 실행 → `Tests.PlayMode.AttackHandlerTests.MeleeAttackAppliesDamage`. 책임 파일과 최소 수선 명세는 위 FAIL 절과 동일하다. 이번 범위의 제품·에셋·CSV는 수정하지 않았다.

|상태|검증|변경 파일|커밋|후속 위험|
|---|---|---|---|---|
|부분 PASS|실제 1050/1057 4방향 좌표, South 비침투, 첫 step 속도·grounded, 기준 테스트 수|`Assets/Docs/QA/stage1_mvp_qa_report.md`|본 문서 동기화 커밋|실제 포털 연속 왕복/즉시 재트리거는 PlayMode 자동화 부재로 미검증; 기존 공격 PlayMode 1건 FAIL|

## Player Unit_3001 uint 라우팅 후행 QA — 2026-08-06

| 검증 | 판정 | 근거 |
|---|---|---|
| Player.Instance null → Unit_3001 Addressables 생성 | PASS | 전용 `UnitPrefabFk_InstantiatesThroughResourceManager`에서 실제 생성 성공 |
| Despawn 후 동일 pool 객체 재사용 | 미검증 | 코드는 `Unit_3001` key로 반납·조회하지만 전용 테스트가 객체 identity를 Assert하지 않음 |
| 기존 Player.Instance → 신규 instantiate 0 | 미검증 | 조기 반환 코드는 확인했으나 instantiate 호출 횟수 계측 테스트 없음 |
| missing UnitBase/ResourceData/Path | FAIL(검증 누락) | 전용 테스트는 잘못된 idx 조회만 수행하며 `SpawnPlayerAsync` null·로그를 주입 검증하지 않음. 구현 로그도 세 원인을 하나의 `no valid ... mapping` 문구로 합침 |
| missing Addressable | PASS(공통 계층 계약) | `ResourceManager.InstantiateAsyncTask`가 key와 원인을 Error로 기록하고 null 반환. Player 전용 주입 테스트는 없음 |
| Resource 1001/GUID 연결 | PASS | `1001,Unit_3001`; prefab GUID `2fd025f20559e3e48b94409388d85c52`가 Addressables entry와 일치 |
| 빈 catch 제거·표시명 Player | PASS | `catch { }` 없음, 생성 후 `playerObj.name = "Player"` |
| 전용 / EditMode / QA / 컴파일 | PASS | 3/3, 68/68, 51/51, 컴파일 오류 0 |

누락 검증 재현: `PrefabNamingMigrationTests.PlayerPoolRouting_UsesUnitAndResourceForeignKeys`에는 실제 `SpawnPlayerAsync` 호출, 동일 객체 Assert, instantiate 횟수 Assert, 누락별 `LogAssert.Expect`가 없다. 최소 수선은 해당 QA 파일의 기존 3개 테스트 안에서 런타임 Assert를 추가하는 것이며 제품 확장은 하지 않는다.

|상태|검증|변경 파일|커밋|후속 위험|
|---|---|---|---|---|
|부분 FAIL|Unit_3001 생성·FK/GUID·로그 코드·전체 회귀|`Assets/Docs/QA/stage1_mvp_qa_report.md`|미커밋|pool identity, 기존 Instance 무생성, 누락별 null/로그가 자동화되지 않아 회귀 탐지 불가|

## 사망·카메라·Visual Hitbox 통합 QA — 2026-08-06

신규 예외 및 회귀 통과 기준만 유지한다.

| 검증 | 통과 기준 |
|---|---|
| Player 사망 지면 유지 | 사망 중 motor 속도 0·비활성, reload 전 위치 불변, 중복 사망 무시 |
| Monster/Boss 사망 풀링 | 1.5초 realtime fade 동안 위치 불변, 중복 `Die` 후 pool enqueue 1회, 동일 객체 재사용 시 alpha·motor·collider·lock 원복 |
| Stage/Chunk 카메라 전환 | blackout alpha=1 이후 렌더 프레임을 보장하고 teleport·camera snap 완료 뒤 fade-in |
| Unit Visual bounds | 7종 모두 `width=2r`, `height=4r` 이내, uniform scale, 최소 한 축 tolerance 0.001 이내 접촉 |
| 렌더 자산 규격 | PPU100, BottomCenter, Visual 원점, root scale 1, SpriteRenderer 정확히 1개 |

최종 게이트: EditMode 76/76, PlayMode 1/1, QATestRunner 56/56, 제품 Console Error 0.

## Hub 복귀·원거리 Chunk 생명주기 QA — 2026-08-06

신규 예외 및 통과 기준만 유지한다.

| 검증 | 통과 기준 |
|---|---|
| Player 사망 복귀 | 중복 사망 후 Hub 전환 1회, pool reset 이후 stale generation 전환 0 |
| Hub UI FK | Player 객체 0, Stage1 버튼 1, `EnterStage(9001)` persistent call 1 |
| Hub 전환 실패 | 입력 잠금 해제, Error 기록, 중복 전환 0 |
| Chunk 정리 | run·encounter·활성 SkillEffect·Projectile 잔존 0 |
| 원거리 pool | owner 비활성 시 즉시 반납, async 재개 0, 중복 enqueue 0, 동일 uint key 재사용 |
| Boss 완료 | Garon 완료 후 기존 Hub 복귀 경로 유지 |

최종 게이트: EditMode 84/84, PlayMode 1/1, QATestRunner 64/64, 제품 Console Error 0.

## Production HUD·SpawnZone 통합 QA — 2026-08-06

| 검증 | 통과 기준 |
|---|---|
| 부팅 | Init→Hub, Build Settings `Init/Loading/Hub/Main`, Hub→9001→1040 |
| Main HUD | HP/Posture/MP·Monster·Boss·Progress 이벤트 갱신, listener 누수 0, TestHUD 생성 0 |
| AlertMessage | uint TextData 2040–2043, scene unload 취소, glyph 누락 시 영어 fallback |
| UI 자산 | BMJUA FontAsset/shared material, missing reference 0, material instance 중복 0 |
| Combat Chunk | 1050–1053 marker 3개, zone 간 15m 이상, 최초 Entry 14m 이상, portal 7m 이상 |
| 비전투 Chunk | 1056/1057/1061/1063 일반 Monster marker 0, Boss 1042/3201 유지 |
| Spawn runtime | 결정론 배치, 활성 4 이하, 공격 token 2 이하, 고위협 1 이하 |

최종 게이트: 자산 Assert 3/3, 일반 EditMode 90/90, PlayMode 1/1, QATestRunner 68/68, Console Error 0. 성능 하네스는 동일 Editor harness에서 임계값 PASS했으며 일반 게이트와 분리 실행한다.

## TP1 BMJUA 한글 폰트 이관 QA — 2026-08-07

| 검증 | 통과 기준 |
|---|---|
| 원본 무결성 | TP1 TTF/SDF 4파일 SHA-256 일치, GUID 충돌 0 |
| 한글 glyph | TextData 2040·2042와 `체력 자세 마력 스킬 입장 경고` 모두 `HasCharacters=true` |
| Scene binding | Hub 4/4, Loading 1/1, Main 3/3이 SDF GUID `6c71dcc91862372499bc2332a17f2ee4` 사용 |
| Material | SDF 내장 material 단일 참조, duplicate instance 0 |
| Localization | Prototype/Development Kr, Runtime En 실제 표시 유지 |
| 자산 건전성 | Missing reference/script 0, Console 제품 오류 0 |

최종 게이트: 일반 EditMode 91/91, PlayMode 1/1, QATestRunner 69/69. 성능 하네스는 제외했다.
