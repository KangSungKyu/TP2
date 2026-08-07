# Production HUD UI Plan

## AlertMessage runtime contract (2026-08-06)

- `AlertMessage.Show(uint textIdx, uint englishFallbackTextIdx, float durationSeconds)` is the production API. Direct strings are restricted to `ShowDevelopmentFallback`.
- A scene-local component uses replace semantics. Repeating the same visible `textIdx` does not restart its timer; a different message invalidates the previous delay by generation.
- `OnDisable` invalidates pending callbacks and hides the CanvasGroup, so Hub/Main scene unload cannot revive a stale alert.
- Unsupported glyphs or missing localized TextData may use the supplied English TextData idx and emit a warning, not an error.
- Hub uses TextData 2040/2041; Main warning uses 2042/2043. Inventory, Equipment, LockOn, and SkillTree remain outside this implementation scope.

## Production Main HUD contract (2026-08-06)

- `ProductionMainHUD` is a scene-local `MainHUDRoot` component. Missing scene binding is an explicit MainScene error; runtime Canvas creation is forbidden.
- Player HP/Posture/MP fills bind to `CombatStats.OnHpChanged`, `OnPostureChanged`, and `OnMpChanged`. Player pooled activation refreshes the same listeners without duplication.
- Regular Monster and Boss HP/Posture bind through `Monster.ActiveMonsters` activation/deactivation events. Boss name reuses `UnitBase.UnitName`, already resolved from `TextData.idx`.
- `StageManager.ProgressChanged` publishes stage idx and visited/total chunks once on run creation and connected-room movement.
- Prompt and Warning both reuse the scene's `AlertMessage` uint TextData API and replace policy.
- `OnDisable` removes Player, Monster, Boss, and Stage listeners. Editor-time static subscriptions are blocked by `Application.isPlaying`.
- Development `CoreTestHUD`, `TestPlayerHUDUI`, and `MonsterOverheadHUD` runtime creation is removed. Inventory, Equipment, SkillTree, and LockOn remain excluded.

## Scene별 범위

### HubScene — 준비·관리 UI

- 상단: Player 상태 요약
- 좌측: Inventory / Skill / Equipment·Status 탭
- 중앙: 항목 목록 또는 슬롯
- 우측: 선택 항목 상세·비교
- 하단: Stage 9001 진입·확인
- Inventory와 Equipment는 데이터 모델이 없으므로 현재 작업과 HUD 제작 대상에서 제외한다.
- Skill은 기존 공격·MonsterPattern 목록을 직접 노출하지 않고 별도 `PlayerSkillNodeData`와 소유·unlock·equip·slot·save 계약 이후 구현한다.
- 현재 player-visible 후보는 7002 하나뿐이므로 분기형 SkillTree UI는 전용 스킬 3개 이상 확정 전까지 제작하지 않는다.
- 데이터 계약이 없는 메뉴와 fake 데이터는 만들지 않는다.

### MainScene — 전투 HUD

- 좌상단: Player HP/Posture/MP
- 일반 Monster: 활성 개체 머리 위 HP/Posture
- 상단 중앙: Boss HP/Posture(조우부터 사망까지)
- 우상단: Stage/방문 Chunk 진행
- 하단 중앙: 상호작용 prompt
- 중앙: 이벤트 기반 warning
- 제외: 미니맵, 버프 목록, 피해 숫자, 퀘스트, 강제 룸 이동, 디버그 문자열
- LockOn 시스템과 선택형 Monster 상세 패널은 현재 범위에서 제외한다.

## 갱신 계약

- Hub는 `HubUIRoot`, Main은 `MainHUDRoot`로 분리하며 Scene 종료 시 Root와 listener를 완전히 해제한다.
- Main은 `BindPlayer(stats)`, `BindBoss(stats, nameTextIdx)`, `SetProgress(stageIdx, visited, total)`, `ShowPrompt(textIdx)`, `ShowWarning(textIdx, duration)`를 사용한다.
- HP/Posture/MP는 기존 `CombatStats.On*Changed`, Boss는 spawn/death, 진행은 chunk 전환, prompt는 trigger enter/exit에서만 갱신한다.
- `OnEnable`에서 bind하고 `OnDisable`에서 대칭 unbind하며 scene generation 불일치 callback은 무시한다.
- `OnGUI`, 매프레임 `Update`, 반복 `Find/GetComponent`, 매프레임 문자열 생성은 금지한다.
- Text는 `TextData.idx`를 최초 이벤트에서 조회·캐시하며 테스트용 HUD 생성 경로는 제거한다.
- 시스템 안내는 `AlertMessage.Show(uint textIdx)`를 사용하며 glyph가 없으면 대응 영문 `TextData.idx`로 fallback한다.

## 렌더링 계약

- 각 Scene의 Canvas는 Static(프레임·아이콘), LowFrequency(목록·진행·경고·prompt), HighFrequency(HP/Posture/MP fill)로 rebuild 경계를 분리한다.
- Scene별 Root Canvas·controller·panel instance·listener·선택 상태는 공유하지 않는다. 공통 color/spacing token, 9-slice prefab, stat bar prefab, SpriteAtlas, TMP font atlas, UI material만 공유한다.
- Hub panel을 MainScene에 비활성 상태로 상주시키지 않는다.
- 목록은 viewport를 넘을 때만 보이는 cell을 생성·재사용하며 작은 목록에는 pooling을 추가하지 않는다.
- HUD SpriteAtlas 최대 1개, TMP font atlas 1개, 공유 UI material 1개를 사용한다.
- 개별 material, 중첩 Mask, 동적 font 추가, Canvas 간 참조를 금지한다.
- TestHUD 제거 후 Draw Calls/Batches는 baseline 대비 +5 이하, SetPass는 +2 이하로 제한한다.
- Hub 목록 선택·상세 전환, Main Player 연속 피격, Boss 조우, prompt+warning 동시 표시를 동일 해상도에서 각각 300 frame 측정한다.

## Chunk 탐험·Spawn 계약

- 기존 60×30 bounds, 카메라 `(-29,-1)–(29,17)`, 4방향 socket/mask를 유지한다.
- Entry는 안전 이동, Combat는 포털 완충·이동·전투, Rest는 무전투, Elite는 Combat 템플릿 재사용, Boss는 1042를 유지한다.
- Combat spawn zone은 3개 이상, 중심 간 15m 이상, 진입점과 첫 spawn 간 14m 이상으로 배치한다.
- 포털 안쪽 7m는 무전투이며 활성 몬스터 4 이하, 공격 토큰 2 이하, 위협비용 3 개체는 1 이하로 유지한다.

## 작업 순서

1. QA가 TestHUD 제거 전/후 측정용 baseline 장면과 300-frame 절차를 고정한다.
2. 리소스작업자가 공유 atlas·font·material 및 9-slice UI 자산을 준비한다.
3. 메인프로그래머가 Main HUD의 이벤트 bind/unbind, MP·Boss·진행 이벤트, TestHUD 제거를 구현한다.
4. 기획자와 메인프로그래머가 `PlayerSkillNodeData`와 저장 계약을 별도 공정으로 확정한다.
5. 리소스작업자가 준비된 Hub Stage/상태 UI와 Main HUD 자산만 Scene별 Root에 배치한다.
6. 리소스작업자가 5종 chunk 템플릿에 spawn zone marker를 배치한다.
7. QA가 Hub/Main 왕복 10회 listener·Canvas 누수 0, Boss HUD 잔류 0, 프레임별 문자열 GC 0 및 렌더 예산을 검증한다.
# Localized AlertMessage Contract (2026-08-07)

- AlertMessage.Show(uint textIdx, float durationSeconds) resolves one TextData idx through the current language.
- Separate English fallback idx fields and the former 2040/2041, 2042/2043 pair routing are removed.
- Hub prompt uses idx 2040; Main warning uses idx 2042. Each row owns both en and kr.
