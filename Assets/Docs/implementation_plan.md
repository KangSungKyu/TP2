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

## [🧠 AGI 자율 회고록]
- 자동 스캔 결과 식별된 추가 분리 서브계획서:
  - UI 자동화 및 캔버스 아키텍처: Assets/Docs/SubPlans/plan_ui_canvas.md
  - 비동기 마이그레이션 및 수명 주기: Assets/Docs/SubPlans/plan_async_lifecycle.md
  - 몬스터 AI 패턴 및 상태 머신: Assets/Docs/SubPlans/plan_enemy_behavior.md
- 자동 결정 근거 요약:
  - UI: Project에 PanelBase/PanelManager 파일 존재로 중앙화된 UI 생명주기/애니메이션 자동화 필요
  - Async: UniTask 사용이 광범위하나 CancellationToken 전파 규칙과 UniTaskVoid 사용에 대한 명확 가이드 부재
  - AI: MonsterPatternData/MonsterPatternDataTable가 존재하며 패턴 실행 제약·검증이 필수

