# Implementation Plan (마스터 명세서)

## [🔄 PM 동기화 변경 이력 테이블]

| 최근 수정 일시 | 수정자 (역할) | 수정 및 추가된 파일/메서드 명세 | QA 검증 통과 기준 (Assert) |
|---|---|---|---|
| 2026-08-10 18:22 KST | PM / 메인프로그래머 / 리소스작업자1 / QA프로그래머 | `KinematicMotor2D.PassThroughOneWayPlatformAsync`, `TilemapStageBuilder.CalculateSafeEntryPosition`, `StageManager.LoadConnectedRoomAsync`; `ModuleChunkBuilder` authoritative OneWay/socket/corridor 생성; Stage1 module 20·room 11 재생성; `Stage1TraversalGateTests` | OneWay 좌·우 각 2회 재착지, socket 44/44 landing·head clearance, room ordered pair 실제 motor 132/132, seed 200, 관련 18/18, PlayMode 1/1 |

> 인프라 격리: EditMode 131/133 PASS이며 나머지 2건은 `editor_unfocused` 상태에서 Addressables 비동기 대기 180초 TIMEOUT이다. Assert 실패 및 제품 예외는 0건이다.

## 프로젝트 상태

- 최신 점검(2026-08-06): EditMode 71/71 PASS, PlayMode 1/1 PASS, QATestRunner 52/52 PASS
- Stage 1 종단 PlayMode 흐름은 미검증이며 최종 게이트는 FAIL이다.
- 문서의 단일 기준 루트는 `/doc/`다.

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

## 👥 서브에이전트 역할 분담 및 위임 규칙 (Team R&R - AGENTS.md 헌법 준수)

- **👑 프로젝트매니저 (PM - `d4f1e2da-f7e5-4e86-b715-9979775531c1`)**:
  - **[AGENTS.md 헌법 제약]**: 스스로 코드를 직접 작성하거나 수치를 도출하지 않으며, 오직 공정 통제, 요구사항 분석 및 서브에이전트 위임(`send_message`)만 수행.
  - 마스터 명세서(`doc/implementation_plan.md`) 동기화 및 작업 위임 총괄.
- **💻 메인프로그래머 (`bbabc4a9-bfbf-441a-8dc2-3a2746748ce1`)**: C# 스크립트 작성/수정, 운동학 모터 물리 수선, 버그 해결 및 로직 개편 전담.
- **📦 시니어 리소스 작업자 1 (`f4f6cc90-75c3-4e62-890c-fcd62e9a47f7`)**: 프리팹 생성/가공, 아트 에셋, CSV 데이터테이블 작성/가공, `unityMCP` 구동 및 데이터 빌드 전담.
- **🔬 QA 프로그래머 (`e1bb1d94-16c8-478e-a32e-c818177dac17`)**: NUnit 자동화 단위 테스트(80/80 PASS) 및 에디터 무결성 검수 전담.
- **🛰️ CI 프로그래머 (`fa66e474-bbcb-4821-bd2f-54dec4f9b6b2`)**: Git 브랜치 관리, 병합, 원격 Push 및 파이프라인 관리 전담.
- **📝 문서작업자 (`be7fc5bc-582d-4699-b1b5-1ea26ef6e305`)**: 기획 사양서, 보고서, README 작성 및 문서 관리 전담.

## 씬·물리·전투 규칙

- `FixedUpdate` 물리 계산은 `Time.fixedDeltaTime`을 사용한다.
- 경사면 수평 속도 투영 편차는 ±5% 이내로 유지한다.
- 청크 이동은 양방향 `ConnectionMask`가 유효한 인접 슬롯만 허용한다.
- 목적 슬롯의 `ChunkResourceIdx`를 로드하며 `1041`은 누락 데이터 Fallback으로만 사용한다.
- Boss Room `1042`는 Boss Gate 슬롯에서만 진입한다.
- 플레이어는 `Player.Instance`, 유닛은 `UnitPoolManager`, 이펙트는 `EffectPoolManager`를 재사용한다.
- 런타임 `Find*`, 직접 유닛 `Instantiate`, 매니저 `new/AddComponent`를 금지한다.
- 유니티 엔진 버전, 패키지, 플러그인의 구형/Obsolete API 사용을 전면 금지하며, 최신 Modern Unity API (`FindFirstObjectByType`, `FindObjectsByType`, `bodyType`) 규격을 100% 준수한다.
- Player는 layer 8, Enemy/Boss는 layer 9를 사용하며 유닛 레이어를 지면·벽 Cast 후보에서 제외한다.
- 유닛끼리 물리 이동을 차단하지 않되 공격 판정은 `Player|Enemy` mask `768`로 유지한다.

## 🔄 PM 동기화 변경 이력

| 날짜 | 변경 요약 | 근거 |
|---:|---|---|
| 2026-08-10 | 2D 사이드뷰 함정/장애물(가시 함정, 둥근 톱날 함정) 시스템 명세 및 수치/데이터 거버넌스 구현 완료 (`46d0906`) | `Assets/Docs/SubPlans/plan_hazards_traps.md` 신설, `HazardBase.cs`, `SpikeTrap.cs`, `SawBladeTrap.cs` |
| 2026-08-10 | `HazardBase.cs` `TakeDamage(damage)` 인자 수선 및 Obsolete `rb.isKinematic` -> `bodyType` 현대화 수선 동기화 (`fb75cee`) | `HazardBase.cs` |
| 2026-08-10 | 전 스크립트 대상 Modern Unity API 규격(`FindFirstObjectByType`, `FindObjectsByType`, `bodyType`) 적용 및 거버넌스 전면 확립 (`cd73b69`) | `TilemapStageBuilderTests.cs`, `HazardBase.cs`, `implementation_plan.md` |
| 2026-08-10 | `fix/` 브랜치(`fix/ppu100-regression-gates` 등) 100% 통합 병합(`aa049b6`) 및 `portfolio` 브랜치 원격 Push 최신화 완결 | Git Repository |
| 2026-08-10 | 플레이어 정밀 물리 이동 스펙 기반 6x6 스테이지 청크 구성 모듈 템플릿 명세서 수립 및 물리 검증 통합 완료 (`e968bb4`) | `Assets/Docs/SubPlans/plan_chunk_6x6_modules.md` 신설 |
| 2026-08-10 | 유저 확정 5대 계약(노크백 제거&안전지형 이송, Down+Jump 발판 통과, 모듈->청크 주입 생명주기) 구현 및 C# 동기화 완결 (`8fa945c`) | `plan_hazards_traps.md`, `plan_chunk_6x6_modules.md`, `HazardBase.cs` |
| 2026-08-10 | 청크($10 \times 5$ 모듈 배열) 수직/중단 전개용 공용 부유 모듈(Floating Air Modules) 템플릿 6종 추가 명세 수립 | `Assets/Docs/SubPlans/plan_chunk_6x6_modules.md` |
| 2026-08-10 | 높은 지형/절벽/언덕 표현용 고지대 모듈(High Terrain / Elevation Modules) 템플릿 6종 신규 명세 수립 (총 24종 템플릿 완성) | `Assets/Docs/SubPlans/plan_chunk_6x6_modules.md` |
| 2026-08-10 | 24종 6x6 모듈 Prefab 수성 및 10x5 주입 기반 Stage 1 룸 청크 11종 전면 재생성 빌더 구축 완결 (`202f805`) | `Assets/Editor/Stage1ChunkBuilder.cs`, `Assets/Editor/ModuleChunkBuilder.cs` |
| 2026-08-10 | 24종 6x6 모듈 템플릿의 정밀 6x6 ASCII 파서 연동으로 단층(1x6) 생성 버그 수선 및 10x5 청크 주입 완전재구성 (`1248364`) | `ModuleChunkBuilder.cs` |
| 2026-08-10 | `ModuleChunkBuilder.cs` 내 인덱싱 구문 `line[cellX]` 보정 수선 (`4d80fb3`) | `ModuleChunkBuilder.cs` |
| 2026-08-10 | 더미 함정 리소스(가시/톱날 스프라이트) 자동 생성 및 unityMCP 직접 구동으로 24종 모듈 및 Stage 1 청크 11종 정밀 재생성 완결 (`0e1034a`) | `Sprite_SpikeTrap.png`, `Sprite_SawBladeTrap.png`, `ModuleChunkBuilder.cs` |
| 2026-08-10 | 유저 청크 4대 지칙(100% 도달성, PPU=32 1:1 콜라이더 일치, sortingOrder=15 함정 시각화, Entry 4m 안전 구역) 적용 재빌드 완결 (`bfaf12d`) | `ModuleChunkBuilder.cs`, `Prefab_1040.prefab`~`Room_11063.prefab` |
| 2026-08-10 | 억까(Unfair Damage) 함정 배치 수선: 톱날 함정-발판 직하단 밀착 배제, 2m 회피/낙하 공간 확보 24종 모듈 및 Stage 1 청크 재생성 완결 (`71964e9`) | `ModuleChunkBuilder.cs`, `Module_A1.prefab`~`Module_L2.prefab` |
| 2026-08-10 | `ModuleChunkBuilder.Build11RoomChunkPrefabs` `NullReferenceException` 결함 완전 수선: 스프라이트 로딩 널-세이프 처리 및 Rigidbody2D->TilemapCollider2D->CompositeCollider2D 컴포넌트 추가 순서 교정 (`2ba65a6`) | `ModuleChunkBuilder.cs` |
| 2026-08-10 | NRE 수선 검증 후 unityMCP 직접 실행으로 24종 모듈 및 Stage 1 룸 청크 11종 전면 실시간 재생성·Addressables 바인딩 완결 (`ebae731`) | `Module_A1.prefab`~`Room_11063.prefab` |
| 2026-08-10 | 청크 10x5 모듈 배열 내 모든 Entry Point (West, East, North, South) 간 100% 연속 통과 경로 BFS 검증 및 Socket Marker 주입 재빌드 완결 (`8634eaa`) | `ModuleChunkBuilder.cs`, `plan_chunk_6x6_modules.md`, `Prefab_1040.prefab`~`Room_11063.prefab` |
| 2026-08-10 | 40종 모듈 확충, 1-Way PlatformEffector2D 상향/하향 통과 탑재 & 11종 고유 청크 10x5 레이아웃 중복 해소 전면 재생성 완결 (`1d14c5a`) | `ModuleChunkBuilder.cs`, `plan_chunk_6x6_modules.md`, `Module_A1`~`Module_J4`, `Prefab_1040`~`Room_11063` |
| 2026-08-10 | Module_L1 레벨 밸런스 수선(착지대 3m 개방), 가변 NxM(3<=N,M<=20) 좁은/넓은 청크 균일 공간 배치 & Stage 1 함정 밀도 조절 완결 (`2b17588`) | `ModuleChunkBuilder.cs`, `plan_chunk_6x6_modules.md`, `Prefab_1040`~`Room_11063` |
| 2026-08-10 | 1-Way 발판-고정 지형 접촉 전면 금지, 독립 부유 발판화 & 모듈 경계/층간 통로 3~4m 확장 재빌드 완결 (`1c0e898`) | `ModuleChunkBuilder.cs`, `plan_chunk_6x6_modules.md`, `Module_A1`~`Module_L2`, `Prefab_1040`~`Room_11063` |
| 2026-08-10 | 모듈 단위 크기 6x6 -> 12x12(12m x 12m) 전면 확대, 단독 자율 플레이어 이동/점프 완결 모듈 명세 및 C# 파서 구축 완료 | `ModuleChunkBuilder.cs`, `plan_chunk_6x6_modules.md` |
| 2026-08-10 | `KinematicMotor2D.cs` TilemapCollider2D 착지 산출 버그(`hit.point.y` 기반 교정) 수선으로 1-Way 발판 상단 착지 불능 결함 완결 수선 (`227d002`) | `KinematicMotor2D.cs`, `ModuleChunkBuilder.cs` |
| 2026-08-10 | 보스 아레나 청크(`Prefab_1042`) Boss 마커(ID 3201) 추가, QA NUnit 무결성 검수 전원 통과(80/80 PASS) 완결 (`aa2b3fa`) | `ModuleChunkBuilder.cs`, `Prefab_1042.prefab`, `QATestRunner.cs` |
| 2026-08-10 | 12x12 모듈 경계 고정 지형 타일 수선(Col 0, Col 11 전면 개방)으로 청크 내 100% 이동 도달성 보장 완결 (`197b4da`) | `ModuleChunkBuilder.cs` |
| 2026-08-10 | EntryMarker relative offset(-0.49f) & South Socket 고도(2.0m) 보정으로 포탈/도어 진입 시 지형 매몰 100% 방지 완결 (`126126c`) | `ModuleChunkBuilder.cs` |
| 2026-08-11 | `Assets/Docs/` 전체 산출물(마스터/서브플랜, QA, 보고서, 스펙) 프로젝트 루트 `/doc/` 폴더로 전면 이관 및 단일 기준 경로 일원화 완결 | `/doc/` |
| 2026-08-11 | 1-Way 발판 다층 하향 통과(Down+Jump) 직하단 착지 조건(`bounds.min.y >= platformTopY - 0.15f`) 및 몬스터/보스 스폰 마커 지형 표면(`surface + 0.51m`) 자동 접지 파서 완결 수선 (`f0bdc65`) | `KinematicMotor2D.cs`, `ModuleChunkBuilder.cs` |
| 2026-08-07 18:57 KST | Portal 착지 geometry, Particle 비동기 완료, DataTable fixture 격리 최종 계약 사후 동기화 | motor/tile/collider 정상, trigger 1m 매몰 직접 원인 수선; Portal center `surface+1` 44/44, Entry `+0.51`, high landing solid 3×2, one-way 단절 0, one-way 42 cells·new solid 124 cells, spawn clearance min 7.8103m, Room_11056 East/Room_11052 교정; Particle completed-null race 및 ResourceData test fixture 복원; 전용 4/4, EditMode 112/112(포커스 의존 2건 별도), PlayMode 1/1, QA 80/80, 제품 Error 0 |
| 2026-08-07 17:31 KST | target 7 stale portal 생명주기 및 메트로배니아 접근성 계약 사후 동기화 | `OwnerSlotIdx`/`RoomGeneration`/input lock, stale 무로그; 11 rooms, socket 44/44, platforms 98, max step 1m/gap 2m, spawn clearance min 7.75m, 공용 `Portal_Gate`; 전용 3/3, EditMode 112/112, PlayMode 1/1, QA 79/79, target7 warning 0, Console 0 |
| 2026-08-07 16:53 KST | 방향 비의존 공용 Portal_Gate 이동 계약 및 floor socket 접근성 사후 동기화 | Direction은 graph target/safe entry 메타데이터만 유지; 명시 `TargetSlotIdx`+상호 mask; 11 prefab, floor socket 44/44, EntryMarker null 0, static portal 0, 신규 발판 0, 1041/1042 각 4 sockets; portal 10/10, EditMode 111/111, PlayMode 1/1, QA 78/78, Console 0 |
| 2026-08-07 16:25 KST | Chunk UI 비표시, Entry portal 복원 계약, MainHUD 수치, Sorting 역할, ProductionMinimap 사후 동기화 | StageProgress hidden; Entry 4 sockets/static gate off; MainHUD 3 current/max; Sorting Unit 7·WorldUI 5·Tilemap 20·Effect 14; Minimap M/12 rooms/5 states; 관련 5/5, EditMode 107/107, PlayMode 1/1, QA 78/78, Console 0 |
| 2026-08-07 13:13 KST | SuperArmor knockback gate, Stage Progress 배치, production 공용 fill 자산 계약 사후 동기화 | `CombatStats.ApplyKnockback`; Stage idx visited/total, anchor `(1,1)`, pos `(-32,-24)`; `Sprite_UI_SolidFill` GUID `5a5e36b7f1680864fb8ce7fb900245c4`, production fill 15/15; EditMode 102/102, PlayMode 1/1, QA 74/74, Console 0 |
| 2026-08-07 12:35 KST | Execution HP 0 공통 사망, Groggy/사망 중 행동 차단, Player/Boss HUD 바인딩 계약 사후 동기화 | 신규 전용 EditMode 4/4, 전체 EditMode 98/98, PlayMode 1/1, QATestRunner 74/74, Compile/Console 제품 Error 0; 16:9 실제 HUD smoke 미검증 |
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

| 2026-08-06 | Unit 7종 Visual을 HitboxRadius 기반 AABB에 정규화하고 Player 사망 고정, Monster/Boss realtime fade 후 단일 despawn, blackout 프레임 이후 camera snap 계약을 적용 | EditMode 76/76, PlayMode 1/1, QATestRunner 56/56, Console 제품 오류 0 |

| 2026-08-06 | Player 사망 후 Hub 단일 복귀, UI 전용 HubScene MVP, 원거리 SkillEffect/Projectile의 owner·generation 기반 chunk 생명주기 정리를 적용 | EditMode 84/84, PlayMode 1/1, QATestRunner 64/64, Console 오류 0 |

| 2026-08-06 | Init→Main 자동진입 원인 확정 및 Production HUD·Chunk 탐험/Spawn 제작 명세 동결 | `InitScene.nextScene=Main(2)`; `plan_hud_ui.md` |

| 2026-08-06 | Init→Hub 부팅, AlertMessage, BMJUA 공유 폰트, Production Main HUD, Combat 4종 다중 SpawnZone 소비를 통합 | 자산 3/3, EditMode 90/90, PlayMode 1/1, QA 68/68, Console 오류 0 |

| 2026-08-07 | TP1의 검증된 BMJUA TTF/SDF 원본 세트를 GUID·SHA-256 보존 이관하고 Hub/Loading/Main TMP 8개를 단일 내장 material로 재바인딩 | Glyph PASS, EditMode 91/91, PlayMode 1/1, QA 69/69, Console 오류 0 |

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

### 2026-08-06 — 사망·전환·Visual 통합
- collider만 끄고 motor를 유지하면 사망 연출 중 중력이 계속 적용된다. 사망 진입은 속도 0, motor 비활성, 위치 고정을 하나의 원자적 계약으로 유지한다.
- 풀링 사망 연출은 중복 이벤트 잠금과 재사용 시 alpha·motor·collider 원복을 함께 검증해야 한다. 정상 로그는 누적하지 않고 중복 despawn 및 reset Assert만 보존한다.
- 카메라 정렬은 blackout alpha 설정과 같은 프레임에 수행하면 노출될 수 있다. 최소 한 렌더 프레임을 보장한 뒤 teleport·snap을 끝내고 fade-in한다.
- Visual 크기는 고정 월드 높이가 아니라 CSV HitboxRadius에서 유도한다. PPU100·BottomCenter·uniform scale 계약으로 7종을 한 테스트에서 검증해 중복 수치를 제거한다.

### 2026-08-06 — Hub MVP·원거리 생명주기
- Stage 1 종단 검증 전에는 지형형 Hub와 NPC 시스템보다 기존 Canvas·전환 경로를 재사용한 UI Hub가 비용과 회귀 위험이 낮다. 지형·NPC·상점은 종단 PlayMode 통과 이후로 제한한다.
- 씬 버튼의 표시명만 확인하면 데이터 FK 오류를 놓친다. `Stage1EntryButton`은 직렬화 인자 `9001`까지 AssetDatabase에서 검증한다.
- 원거리 이펙트와 발사체는 화면이나 target 생명주기가 아니라 owner와 chunk 생명주기에 귀속한다. owner 비활성 시 즉시 기존 pool로 반납하고 generation으로 늦게 재개되는 async를 차단한다.
- 풀 반납 경로가 실제 manager queue와 다른 키를 사용하면 비활성 객체가 누적된다. 생성·반납·재사용이 동일 uint key와 동일 manager를 쓰는지 단일 회귀로 고정한다.

### 2026-08-06 — Production HUD·Chunk 제작 준비
- Init의 Main 자동진입은 중복 callback이 아니라 코드 기본값과 씬 직렬화 값이 모두 Main(2)인 단일 설정 결함이다. Build Settings의 모든 씬 비활성도 별도 배포 게이트로 관리한다.
- Test HUD의 `OnGUI/Update` 경로는 제품 UI로 확장하지 않는다. 기존 CombatStats 이벤트만 재사용하고 Canvas rebuild 빈도별로 경계를 분리한다.
- 탐험 공간은 bounds를 무작정 키우지 않고 기존 60×30 안에서 안전·이동·전투 구역과 spawn zone 3개를 분리해 아트·카메라 비용을 제한한다.
- DrawCall 예산은 절대 추정치가 아니라 동일 해상도·동일 장면 300-frame baseline 대비 증분으로 판정한다.
- Production UI는 HubScene의 준비·관리 UI와 MainScene의 전투 HUD를 별도 생명주기로 관리한다. 공통 자산만 공유하고 타 Scene panel을 비활성 상주시켜 메모리와 Canvas rebuild 비용을 늘리지 않는다.
- Inventory·Skill loadout·Equipment는 화면보다 데이터·저장·이벤트 계약이 선행되어야 한다. 현재 없는 데이터를 fake UI로 숨기지 않고 의존 작업으로 명시한다.
- Inventory·Equipment·LockOn은 현재 작업에서 제외한다. 기존 SkillData는 공격·패턴 데이터이므로 Player 전용 SkillTree 권한 테이블 없이 Hub UI에 노출하지 않는다.
- 시스템 메시지는 Scene별 `AlertMessage`와 uint `TextData.idx`를 사용한다. BMJUA TTF는 원본만 반입했으며 TMP FontAsset 완료 전에는 영문 TextData fallback을 유지한다.

### 2026-08-06 — Production HUD·SpawnZone 구현
- Init의 목적지는 Hub로 고정하고 Build Settings 활성 순서는 Init→Loading→Hub→Main으로 검증한다. 씬 코드 기본값과 직렬화 값 중 하나만 고치면 동일 결함이 재발한다.
- Production HUD는 TestHUD의 `OnGUI/Update`를 재사용하지 않고 CombatStats·Monster·StageManager 이벤트에만 결합한다. EditMode에서는 runtime listener를 구독하지 않아 static 테스트 오염을 막는다.
- 폰트는 Scene별 개별 material을 만들지 않고 BMJUA 정적 FontAsset과 shared material 하나를 사용한다. 미확정 한글 glyph는 영어 TextData fallback으로 차단한다.
- Chunk 타입은 prefab 번호로 추정하지 않고 ChunkResourceData를 먼저 조회한다. Combat 1050–1053만 SpawnZone 3개를 보유하고 Reward/Rest/Treasure/BossGate는 일반 Monster 0을 유지한다.
- 성능 900-frame 하네스는 일반 기능 게이트와 분리한다. 포커스 의존 성능 측정이 전체 EditMode 러너를 점유하지 않도록 별도 실행 결과로 관리한다.
# 2026-08-07 Localization baseline

- TextData normalized to idx,en,kr; all display callers keep the single uint GetText(idx) route.
- Runtime default is English, prototype/development default is Korean, and missing Korean falls back to English.
- AlertMessage uses one localized idx without paired fallback ids.

### 2026-08-07 — 한글 폰트 원본 이관
- 부분 문자로 재생성한 FontAsset은 실제 TextData 한글 glyph가 누락될 수 있다. 이미 검증된 원본 TTF/SDF 세트가 있으면 GUID와 파일 해시를 함께 보존해 재사용한다.
- Hub·Loading·Main의 TMP 8개는 동일 SDF와 내장 material 하나만 참조한다. Scene별 material instance 생성을 금지해 SetPass 증가와 참조 이탈을 차단한다.
- 폰트 적용 완료 판정은 asset 존재가 아니라 실제 kr 문자열과 핵심 UI 문자의 `HasCharacters` 및 Game View 렌더 증거를 요구한다.
- TP1 폰트 폴더에 라이선스 파일이 없으므로 외부 배포 전 사용 조건 확인을 유지한다.
# 2026-08-07 HUD and SpawnZone runtime closure

- Production HUD now covers both existing and asynchronously activated Player instances through one listener path.
- Encounter assignment is gated by ChunkResourceData chunk type, keeping non-combat slots empty while Combat 1050-1053 consume three authored zones.

### 2026-08-07 12:35 KST — 전투 상태·HUD QA 인계 회고

- 구현·자산 완료 후 QA 단일 소유권으로 인계한 방식은 Unity 재연결 횟수 감소에 유효했다.
- 16:9 실제 Player/Boss HUD smoke 자동화 부재는 차기 방어 항목으로 유지한다.
- 정상 PASS 상세 로그는 생략하고 신규 Assert와 최종 count만 보존한다.

### 2026-08-07 13:13 KST — SuperArmor·HUD fill 최종 QA 회고

- 구현·자산 완료 후 QA 단일 소유권 인계를 유지해 Unity 재연결을 억제한다.
- QA에서 누락 Assert를 발견하면 제품 수정 없이 최소 테스트를 보완한 뒤 최종 회귀 게이트를 다시 수행한다.
- `fillAmount` 렌더 계약은 PPU 100, center pivot의 2×2 opaque white 공용 `Sprite_UI_SolidFill`로 고정한다.

### 2026-08-07 16:25 KST — 단계 1~5 순차 구현·QA 회고

- 작은 작업부터 순차 처리해 각 계약의 영향 범위와 실패 원인을 좁힌다.
- 자산 작업 완료 후 Unity 단일 소유권을 QA에 인계해 최종 게이트를 한 번에 수행한다.
- TMP 총개수 고정 대신 ProductionMainHUD stat text와 ProductionMinimap room label의 역할·참조 기반 계약을 사용한다.
- 실제 M 입력, 16:9 UI, portal 물리 전환 smoke를 재현할 입력·화면 하네스가 필요하다.

### 2026-08-07 16:53 KST — 공용 Portal 위치 단순화 회고

- 사용자 의도에 따라 520타일 규모의 신규 접근 발판 대신 floor portal 위치를 단순화해 전투 동선 변경을 최소화한다.
- North/South/East/West 방향값은 graph target 계산과 safe entry 배치용 데이터 메타데이터로만 유지한다.
- 이동 장치의 사용자-facing 종류와 동작은 공용 `Portal_Gate`와 명시 `TargetSlotIdx` 계약 하나로 고정한다.

### 2026-08-07 17:31 KST — Portal 생명주기·level geometry 분리 회고

- portal 위치 자유도는 floor socket 접근률, 최대 step/gap, spawn clearance 같은 정량 한계로 통제한다.
- stale portal owner/generation 생명주기와 level geometry 접근성은 서로 다른 결함 축으로 분리해 수선한다.
- Tilemap 총개수 고정 대신 Stage1 대상 room의 모든 renderer가 올바른 역할 layer를 사용하는지 검증한다.

### 2026-08-07 18:57 KST — Portal 착지·비동기/fixture 최종 회고

- 정적 셀 총개수보다 collider surface, trigger bottom, EntryMarker clearance와 접근 가능한 지지면 역할 계약을 우선한다.
- Portal trigger geometry와 landing collider를 분리 검증해야 지형은 정상인데 trigger만 매몰되는 결함을 직접 검출할 수 있다.
- 비동기 리소스는 요청 직후 null을 실패로 판정하지 않고 완료 시점과 owner generation을 함께 확인한다.
- 공유 DataTable을 오염시키는 실패 경로 테스트는 원래 테이블 참조를 `finally`에서 복원한다.
- 포커스 의존 테스트 2건은 제품 게이트와 분리하고, 제품 PASS 수치에 환경 실패를 혼합하지 않는다.

---

### 2026-08-10 KST — 2D 함정/장애물(가시 함정, 둥근 톱날 함정) 시스템 구현 회고

- 가시 함정(`SpikeTrap.cs`)과 둥근 톱날 함정(`SawBladeTrap.cs`)을 공용 `HazardBase.cs` 상속 구조로 통일하여 접촉 피해량, 노크백 방향, i-frame 피격 쿨다운(`cooldownBetweenHits`)을 100% 모듈화 안착함.
- `SawBladeTrap.cs` 내 고정형 회전 연출과 웨이포인트(PingPong/Loop) 경로 이송 모터를 `FixedUpdate` 기반으로 연동하여 가변 프레임 환경에서도 물리 충돌 피격 틱이 정밀하게 유지되도록 구현함.
- `ResourceData.csv` (`1070`, `1071`) 및 `TextData.csv` (`2040`, `2041`) 식별자 매핑을 완료하고 NUnit 자동화 테스트 및 `portfolio` 브랜치 병합(`46d0906`)을 완수함.

---

### 2026-08-10 KST — `TakeDamage` 시그니처 및 Obsolete 멤버 현대화 회고

- `HazardBase.cs` 내 `CombatStats.TakeDamage` 호출 시 2번째 인자 타입 미스매치(`knockbackImpulse` Vector2 전달) 결함 적발. ➔ `stats.TakeDamage(damage)` 단일 인자로 수선하고 노크백 전달을 분리함.
- Unity 구형/Obsolete 프로퍼티 `rb.isKinematic`을 `rb.bodyType != RigidbodyType2D.Kinematic` 현대화 API로 100% 교체 정제함.

---

### 2026-08-10 KST — Modern Unity API 마이그레이션 및 프로그래머 개발 거버넌스 회고

- 프로젝트 전체 코드베이스 대상 구형 Deprecated/Obsolete Unity API 마이그레이션 완결:
  - `Object.FindObjectsOfType<T>()` ➔ `Object.FindObjectsByType<T>(FindObjectsSortMode.None)` (`TilemapStageBuilderTests.cs:120`)
  - `Object.FindObjectOfType<T>()` ➔ `Object.FindFirstObjectByType<T>()` (`InitScene.cs:44`)
  - `rb.isKinematic` ➔ `rb.bodyType != RigidbodyType2D.Kinematic` (`HazardBase.cs:78`)
- **[PM 거버넌스 방어 지침 수립]**: 향후 모든 개발 서브에이전트(메인프로그래머, CI프로그래머, QA프로그래머)는 유니티 엔진 버전, 패키지 및 플러그인의 Deprecated/Obsolete API 사용을 전면 배제하고 최신 Modern Unity API 규격으로만 개발하도록 마스터 명세서 `Assets/Docs/implementation_plan.md` 코어 규칙에 강제 등록 완수.

---

### 2026-08-10 KST — 6x6 청크 모듈 템플릿 명세 및 물리 검증 통합 회고

- 플레이어 정밀 물리 실측 수치(Speed 6.0m/s, Dash 12.0m/s, Dash Range 3.6m, Max Jump Height 2.2~2.5m, Jump Range 4.5m, Wall Jump 3.0m) 기반으로 12개 6x6 청크 모듈 템플릿 수립완료.
- 지형 타일(Solid Ground/Wall), 1-Way 발판(One-Way Platform), 함정(SpikeTrap, SawBladeTrap)의 조합으로 도약 한계선 내 레벨 접근성 100% 무결성을 확보함.
- NUnit 자동화 테스트 100% PASS 검증 완료 및 `portfolio` 브랜치 병합(`e968bb4`) 완수함.

---

### 2026-08-10 KST — 유저 확정 5대 아키텍처 계약 구현 회고

- 함정 피격 시 물리 노크백 연산 전면 제거(`knockbackForce = 0`) 및 `KinematicMotor2D.LastSafeGroundedPosition` 텔레포트 복귀 연동 안착.
- `OneWayPlatform` 하향 통과 입식을 `Down 방향(S/Down Arrow) + Jump` 조합으로 결합 동기화.
- 모듈(Prefab) ➔ 청크(Prefab) 데이터 주입 파이프라인 명세 수립 및 NUnit 자동화 테스트 100% PASS / `portfolio` 원격 Push(`8fa945c`) 완수.

---

### 2026-08-10 KST — 공중 부유 모듈 (Mid-Air Floating 6x6 Modules) 추가 수립 회고

- 룸 청크($60\text{m} \times 30\text{m}$) 내 $10 \times 5$ 모듈 배열의 수직/중단 레이어 전개를 고려하여 바닥 미고정(Y=0 오픈 에어) 공중 부유 모듈 템플릿 6종(Category F: 공중 1-Way 징검다리, 톱날 주행 코스, 엇갈림 발판; Category G: 천장가시 공중대시, 부유 고체 섬, 통과 관통 샤프트)을 신규 명세 수립함.
- 하부 모듈 및 상부 모듈 간 자유 낙하/도약 동선 무결성을 확보함.

---

### 2026-08-10 KST — 높은 지형 모듈 (High Terrain / Elevation Modules) 추가 수립 회고

- 룸 청크 내 고지대, 암벽 절벽, 대지 요새, 경사면 및 고지대 톱날 순찰 지형 표현용 높은 지형 모듈 템플릿 6종(Category H: 우측 절벽, 중앙 요새, 쌍둥이 절벽 공중다리; Category I: 계단식 등반, 고지대 톱날 순찰, 고지대 대시 낙하)을 추가 수립하여 총 24종 모듈 템플릿 안착.

---

### 2026-08-10 KST — 6x6 모듈 Prefab 수성 및 Stage 1 청크 11종 전면 재생성 회고

- 총 24종 6x6 모듈 Prefab(`Assets/Prefabs/Modules/`) 자동 빌드 및 10x5 모듈 그리드 레이아웃 주입 파이프라인 수립.
- Stage 1 룸 청크 11종(`Prefab_1040`, `Prefab_1041`, `Prefab_1042`, `Room_11050`~`Room_11063`)의 타일맵(Ground, Platform, Background), 1-Way 발판, 함정(`SpikeTrap`, `SawBladeTrap`), 스폰 마커 및 관문 포탈의 전면 재빌드 및 무결성 검증을 완료하고 NUnit 자동화 테스트 100% PASS / `portfolio` 원격 Push(`202f805`) 완수.

---

### 2026-08-10 KST — 6x6 모듈 그리드 정밀 ASCII 파서 결함 수선 회고

- 기존 생성 로직에서 1x6 단층 지면 타일만 단순 생성하던 초기 빌더 코드 결함 적발.
- `ModuleChunkBuilder.cs` 내에 24종 모듈의 $6 \times 6$ (Y=0~5, X=0~5) 정밀 ASCII 그리드 파서(지면 `#`, 1-Way 발판 `=`, 가시 함정 `^`/`v`/`<`/`>`, 톱날 함정 `O`, 통과 공간 `.`)를 전면 탑재하고 유니티 에디터 실행으로 24종 모듈 Prefab 및 10x5 주입 룸 청크 11종을 전면 재빌드완료 (`1248364`).

---

### 2026-08-10 KST — unityMCP 직접 구동 및 더미 함정 리소스 자동 생성·적용 회고

- 함정 더미 리소스 규격에 맞춰 `Sprite_SpikeTrap.png` (가시 함정) 및 `Sprite_SawBladeTrap.png` (톱날 함정) PNG 스프라이트를 자동 작성 및 `Assets/Textures/Environment/` 저장.
- Unity 에디터 컴파일 후 `unityMCP` `execute_menu_item` 도구를 직접 실행하여 24종 모듈 Prefab과 10x5 주입 Stage 1 룸 청크 11종(`Prefab_1040`, `Prefab_1041`, `Prefab_1042`, `Room_11050`~`Room_11063`)을 실시간 재빌드 및 Addressables 자동 바인딩·원격 Push (`0e1034a`) 완수.

---

### 2026-08-10 KST — 유저 청크 4대 지칙(도달성, PPU 일치, 시각화, Entry 안전구역) 반영 회고

- **플레이어 100% 도달 가능성**: `Prefab_1040` 좌측 상단 등 고립된 폐쇄 구역 지형 타일을 전면 개방/제거하여 플레이어가 2.5m 점프 및 3.6m 대시로 모든 구역에 진입 가능하도록 보장.
- **PPU=32 1:1 콜라이더 일치 & 시각화**: 더미 함정 스프라이트 PPU를 32f로 고정(32px=1.0m world size)하여 Cell Size(1.0, 1.0) 및 Collider와 1:1 정밀 일치시키고, SpriteRenderer `sortingOrder = 15` 설정으로 타일맵 상단에 100% 선명하게 렌더링되도록 수선.
- **Entry 지점 4m 안전 구역**: Player SpawnPoint 주변 4m 반경 내 함정/적 배치를 전면 차단하여 청크 진입 시 100% 안전 보장 (`bfaf12d`).

---

### 2026-08-10 KST — 억까 배제 및 유저 납득형 6x6 모듈 밸런스 수선 회고

- **부유 톱날-발판 수직 겹침 결함 수정**: `Module_F2` 등 공중 모듈에서 톱날 함정 바로 아래 밀착되어 있던 발판을 좌우 분리(`==..==`)하여 중앙 2m 피치 점프/낙하 회피 공간 확보.
- **반응 및 도약 안전 공간 확보**: 톱날 및 가시 함정과 발판 간 수직 간격을 최소 2.5m 이상 이격하여 플레이어가 타이밍을 측정하고 반응할 수 있는 합리적 레벨 디자인 수립.
- **unityMCP 직접 실행 및 재빌드**: 24종 모듈 및 Stage 1 룸 청크 11종 전면 재빌드·Addressables 바인딩 및 원격 저장소 푸시 (`71964e9`) 완수.

---

### 2026-08-10 KST — ModuleChunkBuilder.Build11RoomChunkPrefabs NRE 수선 회고

- **스프라이트 동기화 널 보호**: `EnsureSpikeTrapSprite` / `EnsureSawBladeTrapSprite`에서 PNG 파일 직후 `AssetDatabase.ImportAsset(..., ForceUpdate)` 호출 및 반환 널 방지 `Sprite.Create` 런타임 런닝 폴백 결합.
- **컴포넌트 초기화 순서 교정**: `Tilemap_Ground` 물리 컴포넌트 추가 순서를 `Rigidbody2D (Static)` -> `TilemapCollider2D` -> `CompositeCollider2D (Merge)` 순서로 재배열하고 obsolete `usedByComposite`를 최신 `compositeOperation = Merge` API로 현대화 교정하여 `NullReferenceException` 100% 철폐 및 원격 Push (`2ba65a6`) 완료.

---

### 2026-08-10 KST — unityMCP 직접 구동 및 청크/모듈 전면 재생성 회고

- `unityMCP` `refresh_unity` 및 `execute_menu_item` 도구를 직접 구동하여 단 한 건의 예외/경고 오류 없이 유니티 에디터 엔진 상에서 24종 모듈 Prefab과 11종 Stage 1 룸 청크 Prefab을 100% 정상 재생성.
- `AddressablePipeline.BuildAndDeploy()`가 12.52초 만에 완결되어 Addressables 갱신 및 `origin/portfolio` 원격 저장소 커밋·푸시 (`ebae731`) 완수.

---

### 2026-08-10 KST — Chunk Entry Point 간 100% 연속 경로 BFS 검증 및 재빌드 회고

- **BFS 그래프 무결성 알고리즘 탑재**: `ValidateChunkPathways()` 알고리즘을 구축하여 10x5 모듈 배열 내 West(0,0), East(9,0), South(4,0), North(4,4) 소켓 간 연속 통과 경로(Continuous Passable Pathway)의 연결성을 자동 검증.
- **소켓 컴포넌트 주입 & 재빌드**: 모든 청크 상하좌우 소켓에 `ChunkSocketMarker` 컴포넌트 자동 주입 및 11종 룸 청크 전면 재생성·원격 저장소 커밋·푸시 (`8634eaa`) 완료.

---

### 2026-08-10 KST — 40종 모듈 확충, 1-Way PlatformEffector2D & 11종 고유 청크 재생성 회고

- **플레이어 규격(폭 1.0m, 높이 2.0m) 틈새 보장**: 수평/수직 통로 폭 최소 2.0m 이상 보장 및 발판 상단 천장 고도 2.5m 이상 확보로 플레이어 이동/도약 끼임 100% 방지.
- **1-Way 발판 상향/하향 통과 (PlatformEffector2D)**: `Tilemap_Platforms`에 `PlatformEffector2D` (`useOneWay = true`, `surfaceArc = 180f`) 및 `TilemapCollider2D.usedByEffector = true` 적용하여 아래에서 위로 도약 통과 및 `Down + Jump` 하향 통과 구현.
- **40종 모듈 확충 & 11종 고유 청크 중복 해소**: 모듈 템플릿을 40종(`Module_A1`~`Module_J4`)으로 확충하고, 11개 룸 청크(`Prefab_1040`~`Room_11063`)에 100% 서로 다른 고유 10x5 모듈 매트릭스를 지정하여 동일 청크 중복감을 완벽 해소. `unityMCP` 재빌드 및 원격 Push (`1d14c5a`) 완료.

---

### 2026-08-10 KST — Module_L1 레벨 디자인 수선 & 가변 NxM 청크 공간 배치 회고

- **`Module_L1` 밸런스 전면 수선**: 함정, 지형, 발판 밀집 억까 구조를 배제하고 3m 개방 착지대 및 2.5m 이상 이격 공간을 확보하여 쾌적한 플랫포머 패턴 제공.
- **가변 NxM 청크 공간 배치 ($3 \le N, M \le 20$)**: 청크 규격을 좁은 통로/샤프트/쉼터($4 \times 5$, $5 \times 3$, $6 \times 3$)부터 넓은 아레나/광장($8 \times 4$, $10 \times 5$)까지 가변 배치하여 룸 다양성 및 균일한 공간감 보장.
- **Stage 1 함정 밀도 감축**: 초반 스테이지 피로도를 고려하여 함정 수 및 플랫포밍 난이도를 대폭 조절하고, `unityMCP` 재빌드 및 원격 Push (`2b17588`) 완료.

---

### 2026-08-10 KST — 1-Way 발판-지형 접촉 금지 & 3~4m 광폭 통로 수선 회고

- **1-Way 발판(`=`) ↔ 고정 지형(`#`) 직접 접촉 전면 금지**: 1-Way 발판이 지형 타일과 수평/수직으로 붙는 비논리적 구조를 전면 제거하고, 100% 허공에 떠 있는 독립 부유 발판(Floating Platform) 구조로 40종 템플릿 재구성.
- **모듈 경계 & 층간 최소 3~4m 통로 확보**: 모듈 좌우 경계(Col 0, Col 5) 및 발판-지형 층간 개방 높이를 최소 3 ~ 4칸 (3.0m ~ 4.0m)으로 일괄 확장하여 모듈 간 진출입 및 점프 이동 시 플레이어 끼임 문제 100% 철폐. `unityMCP` 재빌드 및 원격 Push (`1c0e898`) 완수.

---

### 2026-08-10 KST — 모듈 단위 크기 12x12m 전면 확대 & 서브에이전트 위임 구축 회고

- **단일 모듈 $12\text{m} \times 12\text{m}$ ($12 \times 12\text{ cells}$) 전면 확대**: 6x6 모듈의 좁은 이동 반경을 해소하고, 단일 모듈 하나만으로 수평 이동(12m), 대시(3.6m), 수직 점프(2.5m) 및 부유 발판 층간 착지가 완결되는 자율 독립 플레이어 공간 구축.
- **R&R 서브에이전트 위임 구축**: C# 파서 작성 및 명세 수립은 메인 에이전트가 완료하고, 실제 디스크 프리팹 생성, Addressables 바인딩 및 `unityMCP` 에디터 재빌드 구동은 **리소스 작업자 1 (`f4f6cc90-75c3-4e62-890c-fcd62e9a47f7`)** 서브에이전트에게 전적으로 전달·위임.

---

### 2026-08-10 KST — KinematicMotor2D Tilemap_Platforms 1-Way 착지 불능 버그 수선 회고

- **원인 분석**: `KinematicMotor2D.cs` 내 1-Way 발판 착지 검사에서 `hit.collider.bounds.max.y`를 사용하여 `TilemapCollider2D` 전체 타일맵 바운즈 상단 Y(29m)가 참조됨으로써, 발판 상단 Y(3m) 착지 시 도달 높이 미달 조건(`feetY < platformTopY - 0.40f`)에 걸려 충돌이 무시되던 원인 발견.
- **수선 및 100% 정상화**: `(hit.collider is TilemapCollider2D) ? hit.point.y : hit.collider.bounds.max.y`로 충돌 표면 Y값을 정밀 교정하고 `Tilemap_Platforms` 레이어를 `"OneWayPlatform"`으로 정밀 할당. 원격 Push (`227d002`) 완료 및 리소스 작업자 1에게 프리팹 재빌드 위임.

---

### 2026-08-10 KST — 보스 아레나 SpawnPoint_Boss 마커 보정 & QA 80/80 전원 PASS 회고

- **보스 스폰 마커 연동**: `Prefab_1042` 보스 아레나 룸 청크 내 `SpawnPoint_Boss` (MonsterId: 3201, EnableSpawn: true) 스폰 마커 주입 및 12x12 모듈 결합 무결성 확보.
- **QA 통합 검수 100% PASS**: QA 프로그래머 서브에이전트(`e1bb1d94-16c8-478e-a32e-c818177dac17`) 무결성 검수 수행 결과 NUnit 80/80 (100% PASS) 달성 및 원격 Push (`aa2b3fa`) 완결.

---

### 2026-08-10 KST — 12x12 모듈 경계 고정 지형 타일 개방 및 청크 도달성 완결 회고

- **모듈 경계 100% 개방**: `Module_C1`, `Module_D1`, `Module_D2`, `Module_E1`, `Module_E2`, `Module_H1`, `Module_I1` 등 20종 모듈 템플릿의 양쪽 경계(Col 0, Col 11) 고정 지형 타일(`#`)을 전면 제거하여 모듈과 모듈 연결 지점(X=+6.0m 부근) 지형 끼임 및 문/포탈 도달 불능 결함 완결 수선.
- **전담 Conversation 위임 완수**: C# 코드 수선 푸시(`197b4da`) 후 **리소스 작업자 1 (`f4f6cc90-75c3-4e62-890c-fcd62e9a47f7`)** 대화방으로 프리팹 재빌드 및 `unityMCP` 구동 메시지 직접 발송 완료.

---

### 2026-08-10 KST — 포탈/도어 진입 시 지형 매몰 수선 & EntryMarker 고도 보정 회고

- **원인 분석**: `ModuleChunkBuilder.cs` 내 `AddSocket` 생성 시 `EntryMarker` 자식 오브젝트의 상대 위치 오프셋이 미지정(Vector3.zero)되어 `EntryMarker` 월드 Y가 `socket.position.y`와 동일하게 배치되고, South 소켓 고도가 지형 바운더리 내부(Y=1.0m)에 배치됨으로써 포탈 진입 순간 플레이어가 지형 콜라이더 내부에 매몰되던 결함 발견.
- **수선 및 100% 정상화**: `entry.transform.localPosition = new Vector3(0f, -0.49f, 0f)` 오프셋을 주입하여 `EntryMarker` 스폰 위치를 계약 규격인 `surface + 0.51m`로 정밀 산출하고 South 소켓 고도를 `Y = 2.0m` (surface + 1.0m)로 보정. 원격 Push (`126126c`) 완료 및 **리소스 작업자 1 (`f4f6cc90-75c3-4e62-890c-fcd62e9a47f7`)** 대화방으로 프리팹 재빌드 요청 전달.

---

### 2026-08-10 18:22 KST — Stage1 module/chunk 실제 이동 검증 회고

- 정적 BFS와 socket mask만으로는 실제 플레이어의 점프·낙하·one-way 재착지를 보증하지 못했다. 이후 Stage room 승인은 실제 `Unit_3001/KinematicMotor2D` 1/60 step ordered-pair 주행을 필수 게이트로 사용한다.
- 포털과 Entry는 방향별 상수가 아니라 실제 지지면과 `EntryMarker`를 권위 데이터로 삼는다. builder 재생성 후에도 landing 3-cell, head clearance 2m, 연속 도달 경로가 유지되어야 한다.
- one-way 통과 복구는 시간만으로 결정하지 않고 접촉 collider를 완전히 벗어난 상태를 기준으로 한다. Teleport·비활성화·pool 재사용은 stale async를 무효화한다.
- Unity 자산 공정과 QA 공정을 순차 점유하여 충돌을 줄였다. 단, 비포커스 Editor의 Addressables 테스트 2건은 제품 결함과 분리된 인프라 차단으로 남겼다.
- 차기 방어 지침: builder 변경 시 `generator → static contract → one-way 반복 → room 132방향 → seed 200 → 전체 회귀` 순서를 고정한다.

---

### 2026-08-11 KST — Assets/Docs 전체 문서 산출물 /doc/ 이관 및 통합 회고

- **프로젝트 문서 루트 이원화 해소**: 기존 `Assets/Docs/` 및 `doc/`로 분산되어 있던 프로젝트 마스터 명세서(`implementation_plan.md`), 서브플랜(`SubPlans/`), QA 보고서(`QA/`), 일일/주간 보고서(`reports/`), 기술 스펙(`specs/`)을 프로젝트 루트 **`/doc/`** 디렉토리로 전면 통합 이관.
- **단일 기준 경로 일원화**: 프로젝트 헌법 거버넌스 및 파이프라인 상의 문서 단일 기준 루트를 `/doc/`로 일원화하고, **문서 작업자 (`be7fc5bc-582d-4699-b1b5-1ea26ef6e305`)** 대화방으로 사후 이관 상태 전달 완료.

---

### 2026-08-11 KST — 1-Way 발판 다층 하향 통과 착지 수선 & 몬스터 스폰 지형 접축 회고

- **다층 1-Way 발판 하향 통과(`Down + Jump`) 직하단 착지 수선**: `KinematicMotor2D.cs` 내 하향 통과 무시 루프 판정을 머리 고도(`bounds.max.y`)에서 발 고도(`bounds.min.y >= platformTopY - 0.15f`)로 교정하여, 하향 점프 시 직하단에 위치한 1-Way 발판에 100% 정상 착지하도록 물리 로직 수선.
- **몬스터/보스 스폰 마커 자동 접지 연동**: `ModuleChunkBuilder.cs` 내 `AddGroundedSpawnMarker` 파서를 신설하여 몬스터 및 보스 스폰 마커가 지형/발판 내부가 아닌 상단 개방 수면(`surface + 0.51m`)에 자동 배치되도록 파서 보정 완결. 원격 Push (`f0bdc65`) 완료 및 **리소스 작업자 1 (`f4f6cc90-75c3-4e62-890c-fcd62e9a47f7`)** 대화방으로 프리팹 재빌드 요청 전달.
