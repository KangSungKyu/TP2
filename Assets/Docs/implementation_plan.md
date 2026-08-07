# Implementation Plan (마스터 명세서)

## 프로젝트 상태

- 최신 점검(2026-08-06): EditMode 71/71 PASS, PlayMode 1/1 PASS, QATestRunner 52/52 PASS
- Stage 1 종단 PlayMode 흐름은 미검증이며 최종 게이트는 FAIL이다.
- 문서의 단일 기준 루트는 `Assets/Docs/`다.

## 코어 데이터 및 리소스 규칙

- 데이터테이블 식별은 파일명이 아닌 `uint idx / 1000` 기반 `DataTableType`으로 결정한다.
- 모든 런타임 리소스 참조는 `ResourceData.idx → Path → ResourceManager`를 거친다.
- 신규 Stage 1 테이블은 `ChunkResource=11`, `StageLayout=12`, `MonsterEncounter=13`을 사용한다.
- CSV bool은 `0/1`만 허용하며 문자열 `true/false`를 금지한다.
- Skill/Effect 표시 이름은 `TextData.idx`로 참조하고 Animator 기술 식별자는 문자열을 유지한다.
- CSV 파일 하나의 실패가 전체 데이터 로딩을 중단시키지 않도록 파일 단위로 예외를 격리한다.
- 유닛 스프라이트는 PPU 100, 프레임 Pivot BottomCenter `(0.5, 0.0)`로 통일한다.
- 유닛의 시각 크기는 원점의 `Visual` 자식에서 균일 Scale로만 조정하고 위치 오프셋을 금지한다.
- 아이템 스프라이트 Pivot은 별도 사유가 없으면 Center `(0.5, 0.5)`를 사용한다.

## 씬·물리·전투 규칙

- `FixedUpdate` 물리 계산은 `Time.fixedDeltaTime`을 사용한다.
- 경사면 수평 속도 투영 편차는 ±5% 이내로 유지한다.
- 청크 이동은 양방향 `ConnectionMask`가 유효한 인접 슬롯만 허용한다.
- 목적 슬롯의 `ChunkResourceIdx`를 로드하며 `1041`은 누락 데이터 Fallback으로만 사용한다.
- Boss Room `1042`는 Boss Gate 슬롯에서만 진입한다.
- 플레이어는 `Player.Instance`, 유닛은 `UnitPoolManager`, 이펙트는 `EffectPoolManager`를 재사용한다.
- 런타임 `Find*`, 직접 유닛 `Instantiate`, 매니저 `new/AddComponent`를 금지한다.
- Player는 layer 8, Enemy/Boss는 layer 9를 사용하며 유닛 레이어를 지면·벽 Cast 후보에서 제외한다.
- 유닛끼리 물리 이동을 차단하지 않되 공격 판정은 `Player|Enemy` mask `768`로 유지한다.

## 🔄 PM 동기화 변경 이력

| 날짜 | 변경 요약 | 근거 |
|---:|---|---|
| 2026-08-04 | 전수 검사 및 마스터/서브플랜 생성 | 초기 32/32 점검 |
| 2026-08-04 | 유닛 사망·이펙트 풀링·HUD Registry 적용 | `9483a67` |
| 2026-08-04 | StageBuilder/Player 컴파일 오류 수선 | `49068a3` |
| 2026-08-04 | 플레이어 중복 생성 및 Battle 스폰 필터 수선 | `a1a9025` |
| 2026-08-04 | 전체 유닛 풀링 전환 | `487f309` |
| 2026-08-04 | EffectPool/UnitIdx 컴파일 오류 수선 | `b470e2a` |
| 2026-08-04 | `Resources.Load` 폴백 제거 | `b4c4612` |
| 2026-08-04 | InitScene 영속 매니저 7종 일원화 | `bc277b9` |
| 2026-08-05 | Stage 1 무작위 청크, CSV 타입·0/1 bool·TextData, P0 포탈·몬스터 placeholder 통합 | 미커밋 작업 트리 |
| 2026-08-05 | `doc/`와 `Assets/Docs/`를 `Assets/Docs/`로 일원화 | `Assets/Docs` |
| 2026-08-06 | Stage 1 청크 4방향 안전 진입 배치 및 South 지면 침투 수선 | `adbb6f5`, 병합 `e97adc3` |
| 2026-08-06 | 유닛 PPU100·BottomCenter·Visual 구조 통일 및 피격 지면 관통/Death Animator 계약 수선 | EditMode 71/71, PlayMode 1/1, QA 52/52 |
| 2026-08-06 | Player/Enemy 레이어 분리로 유닛 간 물리 고착 제거 | `ae77a83`, 병합 `49e56ac`; EditMode 72/72, QA 53/53 |

## 🧠 AGI 자율 회고록

### 2026-08-04 15:02
- 유닛 사망과 공격 이펙트를 풀링하고 HUD 탐색을 Registry로 전환했다.
- 모든 수선 후 실제 QA 결과를 확인하고 변경 이력을 동기화한다.

### 2026-08-04 15:09–15:10
- `TilemapStageBuilder` 식별자와 `PlayerState` 불일치로 컴파일 오류가 발생했다.
- enum·공용 식별자 변경은 관련 파일의 정적 일치 검사를 먼저 수행한다.

### 2026-08-04 15:18–15:20
- 중복 플레이어와 런타임 매니저 생성, Battle/Boss 스폰 혼선을 제거했다.
- 영속 매니저는 InitScene에만 배치하고 후속 씬에 중복 배치하지 않는다.

### 2026-08-04 15:25–15:35
- 유닛 풀링과 EffectPool/UnitIdx 연결을 완료했다.
- 신규 유닛은 `UnitPoolManager`, 신규 이펙트는 `EffectPoolManager`만 경유한다.

### 2026-08-04 16:02
- InitScene의 영속 매니저 7종을 일원화하고 MainScene 중복 노드를 제거했다.
- DontDestroyOnLoad 객체의 씬별 중복 배치를 금지한다.

### 2026-08-05
- 신규 CSV의 타입 충돌과 소비되지 않는 청크 소켓이 실제 실행을 막았다.
- 코드·리소스·아트·QA의 선행 의존성은 유효했지만 PlayMode 종단 검증이 늦었다.
- 이후 완료 판정은 `CSV FK → Addressables → 런타임 소비 → 실제 PlayMode` 증거를 모두 요구한다.

### 2026-08-06
- `EntryMarker` 생성 시 부모 설정의 월드 좌표 유지로 모든 방향 진입점이 원점에 모여 South 진입에서 지면 침투가 발생했다.
- 프리팹 8개를 반복 수정하지 않고 공통 런타임 배치 경로에서 소켓 방향·플레이어 콜라이더·모터를 재사용해 변경 범위와 컨텍스트를 줄였다.
- 좌표 단위 테스트만으로 완료 판정하지 않고 실제 포털 연속 왕복과 즉시 재트리거를 후속 PlayMode 게이트로 유지한다.

### 2026-08-06 — 유닛 렌더·물리 규격
- PPU 혼재와 GameObject 위치 보정은 렌더 크기와 hitbox 비교를 불안정하게 만들므로 PPU100·BottomCenter·Visual 원점 규칙을 단일 기준으로 고정했다.
- 병렬 브랜치 전환 중 자산 변경이 stash에 격리되어 Unity 메모리 상태와 Git 디스크가 달라졌다. 이후 자산 QA는 강제 refresh 뒤 디스크 meta와 AssetDatabase 값을 함께 검사한다.
- KinematicMotor2D와 Rigidbody AddForce의 이동 권한 중복을 제거했으며 Monster Death는 컨트롤러 공통 `State == 8` 계약만 사용한다.
- 모든 유닛이 Default 레이어에 있으면 모터가 다른 유닛을 지면으로 오인한다. Player/Enemy를 환경 Cast mask에서 분리하고 공격 mask만 유지한다.

# 2026-08-07 Localization baseline

- TextData normalized to idx,en,kr; all display callers keep the single uint GetText(idx) route.
- Runtime default is English, prototype/development default is Korean, and missing Korean falls back to English.
- AlertMessage uses one localized idx without paired fallback ids.
