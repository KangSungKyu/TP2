# 몬스터 리소스·스폰·HUD 명세

## 1. 데이터 식별 및 리소스 해석

- `SpawnPointMarker.MonsterId`는 문자열 이름이 아닌 `UnitBaseData.idx` 정수 식별자다.
- 스폰 가능 여부는 `UnitBaseDataTable.TryGetUnitData(MonsterId)` 성공 후, 해당 데이터의 `PrefabId`로 `ResourceDataTable.TryGetResource(...)`가 성공하고 `ResourceData.Path`가 유효한지로 판정한다.
- 문자열 `TryParse`, 몬스터 이름 하드코딩, 개별 스포너의 Addressables 직접 호출은 허용하지 않는다.
- 실제 인스턴스 생성은 `UnitSpawner` -> `UnitPoolManager` -> `ResourceManager` 경로로만 수행한다.
- 데이터 매핑이 오염되었거나 누락되면 스폰을 중단하고 오류를 기록하며 런타임을 계속한다.

## 2. 룸 마커 스폰

- `SpawnType.Player`: 기존 `Player.Instance`가 있으면 새로 만들지 않고 마커 위치로 텔레포트하고 운동 속도와 전투 상태를 초기화한다.
- `SpawnType.Monster`: `MonsterId`의 검증된 데이터 매핑으로 일반 몬스터를 풀에서 획득한다.
- `SpawnType.Boss`: 보스 마커의 `MonsterId`로 동일한 데이터 검증·풀링 경로를 사용한다. 1스테이지 보스 룸(`1042`)의 보스 마커는 `3201`을 사용한다.
- 룸 전환 전 활성 몬스터는 풀로 회수해 재진입 중복 스폰을 방지한다.

## 3. HUD

- 일반 몬스터는 `MonsterOverheadHUD`에서 HP와 Posture를 머리 위에 표시한다.
- `BossMonster`는 일반 오버헤드 HUD 대상에서 제외한다.
- 보스는 `UnitSpawner.ConfigureMonsterUIAndRewards`에서 `TestPlayerHUDUI.BindBossTarget`으로 화면 상단 HUD에 바인딩한다.
- 보스 HUD는 HP와 Posture를 모두 표시하며, `MaxPosture <= 0` 같은 오염 수치에서도 0으로 안전 처리한다.

## 4. 아트 리소스 규격

- 일반 몬스터 기준 프레임: `128 x 256 px`
- PPU: `64`
- 월드 높이 기준: `2.0m`
- 슬라이싱: Grid By Cell Size (`128 x 256`)

| 유닛 | 역할 | 프레임 | PPU |
| :--- | :--- | :--- | :--- |
| `SpearSentry` | 근거리 창병 | 128x256 px | 64 |
| `ShadowStalker` | 추적·암습형 | 128x256 px | 64 |
| `WaveHeavy` | 중갑·충격파형 | 128x256 px | 64 |

## 5. 관련 구현 및 검증

- `Assets/Scripts/Scene/SpawnPointMarker.cs`
- `Assets/Scripts/Manager/UnitSpawner.cs`
- `Assets/Scripts/Manager/UnitPoolManager.cs`
- `Assets/Scripts/UI/MonsterOverheadHUD.cs`
- `Assets/Scripts/UI/TestPlayerHUDUI.cs`
- `Assets/Editor/Tests/TilemapStageBuilderTests.cs`
- `Assets/Editor/Tests/CSVDataPipelineTests.cs`

최종 소급 점검: 2026-08-05
