# Stage 1 무작위 청크 작업 명세서

## 1. 작업 범위

| 담당 | 작업 |
|---|---|
| 메인 프로그래머 | 데이터 구조, 생성기, 룸 전환, 저장, 전투 예산, Fallback, QA |
| 리소스 작업자 | 청크 프리팹, 출입구 소켓, 스폰 마커, 신규 몬스터 프리팹·애니메이션·VFX |

## 2. 공통 데이터 규칙

- PK/FK는 `uint idx`만 사용한다.
- 런타임 문자열 키, 몬스터 이름 조회, 직접 Addressables 호출을 금지한다.
- 청크는 `ResourceData.idx`, 몬스터는 `UnitBaseData.idx`로 참조한다.
- 일반 몬스터와 보스 패턴은 모두 `MonsterPatternData`에서 관리한다.
- `BossPatternData` 테이블과 전용 로더는 사용하지 않는다.
- 신규 예약값은 실제 CSV와 `ResourceData` 등록 완료 후 사용한다.
- 누락 데이터는 해당 항목만 제외하고 전체 Stage를 중단하지 않는다.

## 3. 메인 프로그래머 작업

### 3.1 데이터 테이블

#### StageLayoutData.csv

| 필드 | 형식 | Stage 1 값 |
|---|---|---:|
| `idx` | `uint` | `9101` |
| `stagedataidx` | `uint` | `9001` |
| `minrows` | `byte` | `3` |
| `maxrows` | `byte` | `4` |
| `mincolumns` | `byte` | `3` |
| `maxcolumns` | `byte` | `4` |
| `minactivechunks` | `byte` | `9` |
| `maxactivechunks` | `byte` | `11` |
| `bossroomresourceidx` | `uint` | `1042` |
| `nextstageidx` | `uint` | `9002` |

허용 배열은 `3×4`, `4×3`만 추첨한다. `3×3`, `4×4`는 Stage 1 정상 생성 후보에서 제외한다.

#### ChunkResourceData.csv

| 필드 | 형식 | 설명 |
|---|---|---|
| `idx` | `uint` | PK |
| `resourceidx` | `uint` | `ResourceData.idx` |
| `chunktype` | `byte` | `1–11` |
| `supportedconnectionmask` | `byte` | 허용 출입구 방향 |
| `minstageidx` | `uint` | 최소 Stage |
| `maxuseperrun` | `byte` | 기본 `2` |
| `weight` | `ushort` | 추첨 가중치 |

#### MonsterEncounterData.csv

| 필드 | 형식 | 설명 |
|---|---|---|
| `idx` | `uint` | PK |
| `stageidx` | `uint` | `9001` |
| `variant` | `byte` | 전투 변형 |
| `unitidxlist` | `uint[]` | `_` 구분 정수 목록 |
| `threatcost` | `byte` | `2–7` |
| `weight` | `ushort` | 추첨 가중치 |

#### MonsterPatternData.csv 통합 규칙

| 구분 | `idx` 범위 | Stage 1 사용값 |
|---|---:|---|
| 일반 몬스터 패턴 | `6001–6099` | `6001–6008` |
| 보스 패턴 | `6100–6199` | 가론 `6100–6103` |

- `BossPatternData.csv`의 `6201–6204`는 런타임 데이터에서 제외한다.
- 가론은 `MonsterBaseData.idx 5201`의 `patternidxlist=6100_6101_6102_6103`을 사용한다.
- 일반 몬스터와 보스는 동일한 `MonsterPatternDataTable`과 `MonsterPatternData` 스키마를 사용한다.
- 보스 여부는 패턴 테이블이 아니라 `UnitBaseData.unittype=3`으로 판단한다.
- 보스 전용 선택 로직은 `BossMonster`가 담당하되 패턴 수치는 공용 테이블에서 조회한다.
- CSV 로드 시 동일 `idx`가 발견되면 후행 데이터로 덮어쓰지 않고 해당 레코드를 거부한다.

### 3.2 런 데이터

```text
StageRunData
- stagedataidx: uint
- layoutdataidx: uint
- seed: uint
- rows: byte
- columns: byte
- startslotidx: byte
- bossgateslotidx: byte
- currentslotidx: byte
- buildpower: byte
- completionlocked: bool
- slots: ChunkSlotData[]
```

```text
ChunkSlotData
- slotidx: byte
- chunkresourceidx: uint
- chunktype: byte
- connectionmask: byte
- encounteridx: uint
- visited: bool
- cleared: bool
- rewardclaimed: bool
```

### 3.3 생성기

1. `StageData.idx 9001`과 `StageLayoutData.idx 9101`을 검증한다.
2. 시드로 `3×4` 또는 `4×3`을 결정한다.
3. 외곽 Start와 거리 `3–4`의 Boss Gate를 배치한다.
4. 두 셀 사이 직행 경로를 만든다.
5. 선택 분기 최소 `3개`, 순환 연결 최소 `1개`를 추가한다.
6. 활성 청크를 `9–11개`로 제한한다.
7. 막다른 길에 Reward, Event, Challenge 또는 Treasure를 배정한다.
8. `ConnectionMask`에 맞는 청크 `ResourceData.idx`를 배정한다.
9. Combat에 `MonsterEncounterData.idx`를 배정한다.
10. 전체 접근성·FK를 검증하고 확정 결과를 저장한다.

### 3.4 생성 검증

```text
rows, columns ∈ {(3,4), (4,3)}
9 <= activeChunkCount <= 11
startSlot != bossGateSlot
3 <= startToBossShortestPath <= 4
optionalBranchCount >= 3
cycleCount >= 1
allActiveChunksReachable == true
emptyDeadEndCount == 0
```

### 3.5 룸 전환

- 기존 `StageManager`, `TilemapStageBuilder`, `RoomDoorPortal` 흐름을 재사용한다.
- 포털은 목적 `slotidx`와 `ResourceData.idx`를 보유한다.
- `ConnectionMask`에 없는 출입구는 비활성화한다.
- 현재 청크와 인접 청크만 유지하며 동시 인스턴스는 최대 `4개`다.
- `Player.Instance`를 재사용한다.
- 카메라는 Fade-In 전에 `SnapToTarget()`을 완료한다.
- 중복 진입 잠금은 `finally`에서 해제한다.

### 3.6 전투 생성

- `SpawnPointMarker.MonsterId`에는 `UnitBaseData.idx`만 기록한다.
- 생성 경로는 `UnitSpawner → UnitPoolManager → ResourceManager`로 고정한다.
- 활성 몬스터 최대 `4`, 동시 공격 토큰 최대 `2`다.
- 위협 비용 `3` 개체는 동시 활성 최대 `1`이다.
- 전투 종료 시 해당 청크 출구만 해제한다.
- 이미 클리어한 청크 재방문 시 몬스터를 재생성하지 않는다.

### 3.7 Boss와 다음 Stage

```text
Boss Gate 진입
→ ResourceData.idx 1042 검증
→ Boss Room 로드
→ UnitBaseData.idx 3201 스폰
→ 승리 시 completionlocked 설정
→ 보상·Stage 완료 저장
→ StageData.idx 9002 검증
→ 다음 Stage 생성
```

`9002` 데이터가 유효하지 않으면 런 완료 처리 후 HubScene으로 복귀한다.

### 3.8 저장

- 확정된 슬롯 배열을 저장하고 시드만으로 재생성하지 않는다.
- 룸 전환 완료, 보상 획득, Boss 승리 직후 저장한다.
- `visited`, `cleared`, `rewardclaimed`를 슬롯별로 복원한다.
- Rest 사용 횟수와 Shop 재고를 런 동안 유지한다.

## 4. 리소스 작업자 작업

### 4.1 청크 공통 규격

| 항목 | 규격 |
|---|---|
| 기본 크기 | `60×30 Tile` |
| 출입구 소켓 | 상·우·하·좌 각 `1개` |
| 출입구 식별 | 방향 enum, 문자열 검색 금지 |
| 플레이어 진입 마커 | 방향별 `1개` |
| 몬스터 마커 | 최대 `6개` |
| 카메라 경계 | 청크 내부 고정 |
| 낙사 | Stage 1 사용 금지 |
| 안전 스폰 반경 | 플레이어 진입점 기준 `5m` |

### 4.2 청크 납품 목록

| 우선순위 | `ResourceData.idx` | 작업 |
|---:|---:|---|
| P0 | `1050` | 패링 전투 청크 |
| P0 | `1051` | 회피 전투 청크 |
| P0 | `1052` | 가드·점프 전투 청크 |
| P0 | `1053` | 혼합 전투 청크 |
| P0 | `1056` | Reward 청크 |
| P0 | `1057` | Rest 청크 |
| P0 | `1061` | Treasure 청크 |
| P0 | `1063` | Boss Gate 청크 |
| P1 | `1054` | 수직 전투 청크 |
| P1 | `1055` | Elite 청크 |
| P1 | `1058` | Event 청크 |
| P1 | `1059` | Shop 청크 |
| P1 | `1060` | Challenge 청크 |
| P1 | `1062` | Shortcut 청크 |

P0 8종으로 1차 플레이 테스트가 가능하다. P1은 경로 다양성 확장분이다.

### 4.3 신규 몬스터 납품

| 우선순위 | `UnitBaseData.idx` | `ResourceData.idx` | 필수 애니메이션 |
|---:|---:|---:|---|
| P0 | `3104` | `1006` | Idle, Move, Hit, Death, `6003`, `6004` |
| P0 | `3105` | `1007` | Idle, Move, Hit, Death, `6005`, `6006` |
| P1 | `3106` | `1008` | Idle, Move, Hit, Death, `6007`, `6008` |

- 일반 몬스터 프레임 기준은 `128×256 px`, PPU `64`, 월드 높이 `2.0m`다.
- 실루엣, 전조 색상, 공격 방향이 배경과 분리되어야 한다.
- 패링 공격과 점프 공격은 전조 색상·바닥 표시를 구분한다.
- 공격 판정과 VFX는 애니메이션 프레임이 아니라 수치 타이밍에 맞춘다.

## 5. CSV 등록 순서

1. `ResourceData.csv`: `1006–1008`, `1050–1063`
2. `UnitBaseData.csv`: `3104–3106`
3. `MonsterBaseData.csv`: `5104–5106`
4. `MonsterPatternData.csv`: `6003–6008`
5. 스킬 데이터: 신규 공격용 정수 `idx`
6. `StageLayoutData.csv`: `9101`
7. `ChunkResourceData.csv`: 청크 후보
8. `MonsterEncounterData.csv`: 전투 조합
9. `StageData.csv`: `9001` 레이아웃 연결과 `nextstageidx`

모든 FK 검증이 통과하기 전 신규 데이터를 활성화하지 않는다.

`BossPatternData.csv`는 Addressables `Datas` 라벨과 Resources Fallback 대상에서 제거한다. 기존 `6201–6204` 데이터는 삭제하지 않더라도 런타임 로드 대상에는 포함하지 않는다.

## 6. Fallback

| 실패 | 처리 |
|---|---|
| 생성 검증 3회 실패 | `3×4` 안전 그래프 생성 |
| 신규 청크 누락 | 동일 유형 후보 또는 `1041` 사용 |
| 신규 몬스터 누락 | `3101–3103`으로 예산 재구성 |
| 일부 스폰 실패 | 유효 개체만 사용 |
| 전원 스폰 실패 | 청크 자동 클리어 |
| Boss Room 누락 | Boss Gate 유지 후 Entry 또는 HubScene 복귀 |
| 다음 Stage 누락 | Stage 1 완료 후 HubScene 복귀 |
| 저장 손상 | 새 시드의 안전 그래프로 Stage 1 재시작 |
| 15 FPS | 판정과 전조를 누적 시간으로 처리 |
| 다단 공격 프레임 누락 | 물리 틱당 피해 최대 `1회` |

## 7. QA 완료 조건

- `3×4`, `4×3` 각각 100개 시드 생성 시 검증 실패가 없다.
- 동일 시드에서 슬롯·연결·청크·몬스터 결과가 일치한다.
- 모든 활성 슬롯이 Start에서 접근 가능하다.
- 방문·전투 수와 무관하게 Boss Gate 입장이 가능하다.
- 잘못된 단일 `idx`가 Stage 전체 Crash를 유발하지 않는다.
- `MonsterPatternData`에서 일반 패턴 `6001–6008`과 가론 패턴 `6100–6103`을 함께 조회할 수 있다.
- `BossPatternData.csv`가 런타임 데이터 목록에 포함되지 않는다.
- 중복 패턴 `idx`가 발견되면 오류를 기록하고 중복 레코드를 사용하지 않는다.
- 청크 재방문 시 몬스터와 보상이 중복 생성되지 않는다.
- Boss 승리 보상과 다음 Stage 전환이 중복 실행되지 않는다.
- 15 FPS에서 패링 `0.134초`, 회피 무적 `0.30초`가 유지된다.

### 🧠 [GameDesigner 자율 회고]
- 기획 무결성 비판: 패턴 테이블을 통합해 중복 로더 문제는 제거하지만, 보스 전용 조건과 선택 로직이 공용 패턴 스키마로 표현 가능한지 구현 검증이 필요하다.
- 차기 방어 지침: 가론 `6100–6103`을 공용 로더로 먼저 검증하고, 공용 필드로 표현할 수 없는 보스 조건이 확인될 때만 `MonsterPatternData`에 최소 필드를 추가한다.
