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
| 2026-08-10 | 2D 사이드뷰 함정/장애물(가시 함정, 둥근 톱날 함정) 시스템 명세 및 수치/데이터 거버넌스 수립 | `Assets/Docs/SubPlans/plan_hazards_traps.md` 신설 |
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
