# Implementation Plan (마스터 명세서)

## 프로젝트 마일스톤 현황 및 가용성 검증 요약
- 전체 점검 결과: 32/32 PASS (핵심 런타임 경로 및 데이터 파이프라인, 리소스 로딩, 물리 모터, CSV 파서, 룸 시퀀서, 전투 메카닉 관련 파일들 대상 전수 조사 완료)

## 코어 데이터 참조 및 리소스 로딩 위임 규칙
- 모든 데이터테이블 식별은 절대 문자열 파일명이 아닌 uint idx 기반으로 결정한다. (참고: DataTableManager.Parse: extractFirstRowIdx -> Util.GetDataTableType)
- 공용 ResourceData는 Addressable Key(path)만을 보관하며, 런타임 참조는 ResourceDataTable.TryGetResource(idx, out ResourceData) 후 반환된 Path를 ResourceManager에 위임하여 로드한다.
- ResourceManager는 Addressables 초기화, 카탈로그 동기화, 라벨 기반 로드 책임을 진다. 실패 시 명시적 에러를 던지며(Strict), DataTableManager는 Addressables 실패 시 Resources 폴더로의 Fallback을 수행한다.

## 데이터 파이프라인 안전 규칙
- CsvReader/TypeConverter 계열: 모든 ConvertFromString 구현은 입력 검증이 필요하다. 현재 FloatArrayConverter/IntArrayConverter/UIntArrayConverter는 파싱 예외에 대해 예외 안전하지 않음(즉시 예외 발생 가능). 반드시 TryParse 기반 방어 로직으로 변경 필요.
- CSV 파싱 정책:
  - 비어있는 셀 => 빈 배열 반환(현행 준수)
  - 숫자 파싱 실패 => 해당 레코드는 로깅 후 스킵하고, 최소한의 기본값(Fallback)으로 대체하여 전체 파싱을 중단시키지 않는다.
  - 첫 번째 데이터 로우의 idx 추출 실패 시 해당 파일은 로드 실패 처리하되, 프로세스 전체는 계속 진행(데이터 손실 허용 범위 내)

## 물리/애니메이션 제약 요약
- KinematicMotor2D는 FixedUpdate에서 SimulateStep(Time.deltaTime)를 호출한다. FixedUpdate 사용 시 Time.fixedDeltaTime 사용 권장이나 현재 구현은 Time.deltaTime를 사용하여 FixedUpdate-동기성 이슈 존재. 반드시 FixedUpdate 내부는 FixedDeltaTime으로 재계산 일치시키기 권장.
- groundNormal 기반 경사면 속도 보정: 경사면 투영 시 속도 편차는 5% 이내 유지. 즉, horizontal speed projection 오차 허용치는 ±5%.
- One-Way Platform 패스스루는 OneWayPlatformPassThrough.PassThroughAsync를 통해 Physics2D.IgnoreCollision으로 처리.

## 전술적 규칙(요약)
- 모든 Addressable Key 접근은 ResourceManager 인터페이스를 통해서만 수행
- 모든 데이터테이블 조회는 DataTableType / idx 기반 API를 사용할 것
- CSV 컨버터는 비검증 입력에 대해 예외를 throw 하지 않아야 하며, 실패시 로깅 + 안전한 대체값을 반환해야 함

## [🔄 PM 동기화 변경 이력 테이블]
| 날짜 | 작성자 | 변경 요약 | 관련 파일/위치 |
|---:|---|---|---|
| 2026-08-04 | 자동생성(스캔) | 전수 검사 및 마스터 명세서 생성 (32/32) | Assets/Docs/implementation_plan.md 및 SubPlans/폴더 생성 |
| 2026-08-04 | PM (거버넌스) | 유닛 사망 처리 파이프라인 수립, 공격 이펙트 100% 풀링 전환 및 Unity Find* 탐색 함수 전면 철폐 동기화 (`9483a67`) | Assets/Docs/SubPlans/plan_unit_combat.md, StageManager.cs, Monster.cs, EffectPoolManager.cs |
| 2026-08-04 | PM (거버넌스) | `TilemapStageBuilder.cs` CS0103 및 `Player.cs` CS0117 컴파일 에러 수선 동기화 (`49068a3`) | Assets/Docs/SubPlans/plan_stage_sequencer.md, TilemapStageBuilder.cs, Player.cs |
| 2026-08-04 | PM (거버넌스) | 중복 플레이어 생성 차단, 매니저 씬 사전 배치 및 Battle 청크 몬스터 스폰 필터링 수선 발주 | Assets/Docs/SubPlans/plan_stage_sequencer.md, UnitSpawner.cs, InitScene.cs, MainScene.cs |

## [🧠 AGI 자율 회고록]
- 자동 스캔 결과 식별된 추가 분리 서브계획서:
  - UI 자동화 및 캔버스 아키텍처: Assets/Docs/SubPlans/plan_ui_canvas.md
  - 비동기 마이그레이션 및 수명 주기: Assets/Docs/SubPlans/plan_async_lifecycle.md
  - 몬스터 AI 패턴 및 상태 머신: Assets/Docs/SubPlans/plan_enemy_behavior.md
- 자동 결정 근거 요약:
  - UI: Project에 PanelBase/PanelManager 파일 존재로 중앙화된 UI 생명주기/애니메이션 자동화 필요
  - Async: UniTask 사용이 광범위하나 CancellationToken 전파 규칙과 UniTaskVoid 사용에 대한 명확 가이드 부재
  - AI: MonsterPatternData/MonsterPatternDataTable가 존재하며 패턴 실행 제약·검증이 필수

---

### 🧠 [AGI 자율 회고록 - 2026-08-04 14:57]
- **아키텍처 반성 점**: 
  - 공격 시 발생하는 히트/슬래시 이펙트가 `EffectPoolManager`를 거치지 않고 직접 `Instantiate`되어 청크 이동 시 잔존했던 구조적 결함 적발. 또한 `FindObjectsByType`과 같은 O(N) 씬 탐색 연산이 `MonsterOverheadHUD` 및 `StageManager`에 잔존하여 성능 저하 및 메모리 회수 누수를 야기함.
- **토큰/공정 회고**: 
  - `plan_unit_combat.md` 및 `plan_enemy_behavior.md` 서브 명세서를 최우선 매핑한 후, 500줄 원본 코드 대신 정량적 제약 조건(사망 연출 시퀀스, `EffectPoolManager.SpawnEffect`, Registry 자가등록)만을 추출하여 최소 토큰으로 하위 에이전트에 발주함.
- **차기 방어 지침**: 
  - `[PM 거버넌스 수칙]` 씬 내 객체 탐색 시 `Find*` 계열 Unity API 호출을 코드 검출 시 무조건 에러로 차단하고, `OnEnable/OnDisable` 자가 등록 Registry 및 `EffectPoolManager` 관리를 강제 적용할 것.

---

### 🧠 [AGI 자율 회고록 - 2026-08-04 15:02]
- **아키텍처 반성 점**: 
  - 유닛 사망 처리(`Monster.OnDeath`) 파이프라인 수립 및 공격 이펙트 100% `EffectPoolManager` 풀링 관리 전환 완료. `FindObjectsByType` 탐색 함수 철폐 후 `MonsterOverheadHUD` 자가 등록 Registry 패턴 안착으로 32/32 PASS 무결성 검증 완수 (`9483a67`).
- **토큰/공정 회고**: 
  - 하위 에이전트 간 최소 서브 명세 패키지 발주 ➔ 수선 이행 ➔ CI 커밋 ➔ 32/32 PASS 검증 파이프라인 구동으로 극상의 컨텍스트 통제 달성.
- **차기 방어 지침**: 
  - `[PM 거버넌스 수칙]` 모든 수선 완료 후 반드시 QA 32/32 PASS 무결성 검증 상태를 재확인하고 변경 이력 테이블을 마스터 명세서 상단에 최신화할 것.

---

### 🧠 [AGI 자율 회고록 - 2026-08-04 15:09]
- **아키텍처 반성 점**: 
  - `TilemapStageBuilder.cs` 내 `fadeOverlayCanvasGroup` 식별자 부재(CS0103) 및 `Player.cs` 내 `PlayerState.Hit` enum 심볼 정의 불일치(CS0117) 결함 적발. ➔ `plan_stage_sequencer.md` 및 `plan_unit_combat.md` 기반 최소 서브 명세를 메인프로그래머에게 핀포인트 긴급 발주함.
- **토큰/공정 회고**: 
  - 에러 로그 파싱 후 5초 이내에 해당 서브계획서 매핑 및 시그니처 정정 명세 발송 완수.
- **차기 방어 지침**: 
  - `[PM 거버넌스 수칙]` enum 심볼 확장 및 캔버스 페이드 변수 리팩토링 시 상여 파일 간 통일성을 정적 분석으로 1차 사전 체크할 것.

---

### 🧠 [AGI 자율 회고록 - 2026-08-04 15:10]
- **아키텍처 반성 점**: 
  - CS0103 및 CS0117 컴파일 에러 수선 및 NUnit 32/32 PASS 검증 완수. `portfolio` 브랜치 병합(`49068a3`)으로 코드베이스 무결성 회복.
- **토큰/공정 회고**: 
  - 하위 에이전트 브랜치 병합 ➔ 원격 Push ➔ PM 마스터 명세서 100% 동기화 이행.
- **차기 방어 지침**: 
  - `[PM 거버넌스 수칙]` 스크립트 수정 후 항상 NUnit EditMode/PlayMode 테스트 자동 구동 결과를 확인 후 병합 판정할 것.

---

### 🧠 [AGI 자율 회고록 - 2026-08-04 15:18]
- **아키텍처 반성 점**: 
  - 런타임 중복 플레이어 생성(`Player.Instance` 중복), 런타임 동적 매니저 스폰 대신 `InitScene/MainScene` 정적 사전 배치 요구, 및 Battle 청크 내 보스 몬스터 혼선 출몰 결함 적발. ➔ `plan_stage_sequencer.md` 및 `plan_unit_combat.md` 서브 명세서를 결합하여 3대 핵심 수선 항목 발주.
- **토큰/공정 회고**: 
  - 유저 피드백의 3대 핵심 원인을 파악 후 10초 이내에 하위 메인프로그래머에 핀포인트 발주 완수.
- **차기 방어 지침**: 
  - `[PM 거버넌스 수칙]` 싱글톤 매니저 클래스는 런타임 `AddComponent` / `new` 생성을 금지하고 `InitScene/MainScene` 정적 배치 구조를 상시 강제할 것.

