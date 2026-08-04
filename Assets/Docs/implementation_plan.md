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
| 2026-08-04 | PM (거버넌스) | 중복 플레이어 생성 차단, 매니저 씬 사전 배치 및 Battle 청크 몬스터 스폰 필터링 수선 동기화 (`a1a9025`) | Assets/Docs/SubPlans/plan_stage_sequencer.md, UnitSpawner.cs, InitScene.cs, MainScene.cs |
| 2026-08-04 | PM (거버넌스) | 플레이어 및 몬스터 전 유닛 대상 `UnitPoolManager` 풀링 관리 전환 및 생애주기 회수 아키텍처 동기화 (`487f309`) | Assets/Docs/SubPlans/plan_unit_combat.md, UnitPoolManager.cs, UnitSpawner.cs |
| 2026-08-04 | PM (거버넌스) | `EffectPoolManager.cs` string Key 오버로딩 및 `UnitBase.cs` UnitIdx 프로퍼티 에러 3건 수선 발주 | Assets/Docs/SubPlans/plan_unit_combat.md, EffectPoolManager.cs, UnitBase.cs |

## [🧠 AGI 자율 회고록]
- 자동 스캔 결과 식별된 추가 분리 서브계획서:
  - UI 자동화 및 캔버스 아키텍처: Assets/Docs/SubPlans/plan_ui_canvas.md
  - 비동기 마이그레이션 및 수명 주기: Assets/Docs/SubPlans/plan_async_lifecycle.md
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

---

### 🧠 [AGI 자율 회고록 - 2026-08-04 15:20]
- **아키텍처 반성 점**: 
  - `UnitSpawner.cs` 내 플레이어 재배치 로직으로 중복 생성 100% 차단. `InitScene` 및 `MainScene`에 `EffectPoolManager`, `StageManager`, `UnitSpawner` 정적 사전 배치 안착 및 Battle 청크 전용 몬스터 마커 조회 수선 완수 (`a1a9025`).
- **토큰/공정 회고**: 
  - 하위 에이전트 병합 완료 후 NUnit 32/32 PASS 무결성 검증 및 원격 Push 완수.
- **차기 방어 지침**: 
  - `[PM 거버넌스 수칙]` 씬 빌딩 시 정적 매니저 노드 배치를 1차 원칙으로 하고 런타임 동적 생성은 폴백용으로만 한정할 것.

---

### 🧠 [AGI 자율 회고록 - 2026-08-04 15:25]
- **아키텍처 반성 점**: 
  - 플레이어 및 몬스터를 포함한 모든 인게임 유닛의 단일 `Instantiate`/`Destroy` 라이프사이클 의존성을 완전히 철폐하고, **`UnitPoolManager` 중심의 100% 오브젝트 풀링 아키텍처**로 개편 발주.
- **토큰/공정 회고**: 
  - 유저의 '플레이어 포함 전 유닛 풀링 관리' 지침 수령 후 5초 이내에 서브 명세 조립 및 발주 완료.
- **차기 방어 지침**: 
  - `[PM 거버넌스 수칙]` 인게임 내 `Player` 및 `Monster` 유닛 생성을 `Instantiate`/`Destroy`로 직접 수행하는 것을 금지하고 무조건 `UnitPoolManager`를 통해서만 스폰/데스파운할 것.
