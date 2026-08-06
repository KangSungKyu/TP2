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
