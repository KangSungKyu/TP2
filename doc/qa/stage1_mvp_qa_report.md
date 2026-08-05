# Stage 1 MVP QA Report

- 실행일: 2026-08-05 (Asia/Seoul)
- 환경: Unity 6000.4.8f1, WindowsEditor, EditMode
- 실제 결과: 41건 실행 / PASS 40 / FAIL 0 / 미구현(Inconclusive) 1
- Stage 1 신규 슈트: 7건 / PASS 6 / FAIL 0 / 미구현 1

> Unity MCP의 summary는 Inconclusive 1건을 total에서 제외해 `40/40 Passed`로 표시했다. 본 보고서는 progress의 41건과 개별 Inconclusive 결과를 기준으로 분리한다.

## PASS

| 검증 | 결과 | 근거 |
|---|---|---|
| Boss Gate 무조건 접근 | PASS | `RoomDoorPortal`에 방문·전투·클리어·BuildPower 조건 필드가 없음 |
| 잘못된 단일 Resource idx | PASS | `uint.MaxValue` 입력이 예외 없이 Entry fallback 반환 |
| 15 FPS 판정 계약 | PASS(코드 계약) | 패링 150 ms ≥ 134 ms, 회피는 누적 `Time.deltaTime` 0.30 s |
| 3×4/4×3 각각 100 seed | PASS | seed 0–199에서 각 규격 정확히 100건 생성·검증 성공 |
| 동일 seed 슬롯·연결·청크 | PASS | seed 77 재생성 결과 일치 |
| BFS·Boss 거리·분기·순환 | PASS | 전 슬롯 접근, 거리 3–4, 분기≥3, 순환≥1 |
| 재방문 및 Boss 완료 중복 금지 | PASS | visit/clear/reward/completion 두 번째 요청 모두 거부 |

## FAIL

없음. 구현된 범위에서 재현된 실패는 없다.

## 미구현

| 검증 | 상태 | 저장소 근거 |
|---|---|---|
| 동일 seed 몬스터 일치 | 미구현 | `StageRunData` 슬롯에 `MonsterEncounterData.idx` 배정 결과가 없음 |

`MonsterEncounterData.csv`는 존재하지만 런 생성 결과에 encounter/monster 식별자가 저장되지 않아 결정성을 계측할 대상이 없다.

## 재현 절차

1. Unity에서 프로젝트를 연다.
2. `Window > General > Test Runner`를 연다.
3. EditMode에서 `QA.Tests.Stage1MvpRegressionTests`를 실행한다.
4. 신규 결과가 PASS 6, Inconclusive 1인지 확인한다.
5. EditMode 전체를 실행하고 진행 카운트 41, 실패 0, Inconclusive 1을 확인한다.

CLI/MCP 자동화 시 테스트 이름은 `QA.Tests.Stage1MvpRegressionTests`이며 결과 XML은 `%LOCALAPPDATA%Low/DefaultCompany/TP2/TestResults.xml`에 저장된다.

## 🔄 [PM/CI 동기화] 변경 이력 테이블

|상태|검증|변경 파일|커밋|후속 위험|
|---|---|---|---|---|
|완료|200 seed, 결정성·그래프, 중복 방지, Boss Gate, 잘못된 idx, 15 FPS 계약|`Assets/Editor/Tests/Stage1MvpRegressionTests.cs`|미커밋|몬스터 encounter 배정은 아직 없음; 타이밍은 EditMode 코드 계약 검증|
|완료|Stage 1 슈트 QA 메뉴 실행 포함|`Assets/Editor/Tests/QATestRunner.cs`|미커밋|수동 runner는 Inconclusive를 FAIL로 집계하므로 CI는 Unity Test Runner 결과 사용|
|완료|PASS/FAIL/미구현 및 재현 절차|`doc/qa/stage1_mvp_qa_report.md`|미커밋|encounter 배정 구현 후 Inconclusive 1건 교체 필요|
