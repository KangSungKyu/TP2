# 무작위 청크 스테이지 구현 명세

## 1. 구현 범위

- `N×M` 슬롯 배열 생성
- 활성 청크 연결 그래프 생성
- `ResourceData.idx` 기반 청크 로드
- 자유 이동, 방문·클리어 상태 저장
- Boss Room 진입 및 다음 Stage 전환
- 생성·로딩·데이터 누락 Fallback

## 2. 데이터 거버넌스

- 모든 PK/FK와 런타임 참조는 `uint idx`를 사용한다.
- `StageData`, 청크, 몬스터, 보스, 효과를 이름 문자열로 조회하지 않는다.
- Addressable 경로는 `ResourceData.idx` 해석 결과로만 사용한다.
- `SpawnPointMarker.MonsterId`는 `UnitBaseData.idx`다.
- 데이터 누락 시 이름 추정, `TryParse`, 직접 Addressables 호출을 금지한다.

## 3. 데이터 계약

### StageLayoutData

| 필드 | 형식 | 제약 |
|---|---|---|
| `idx` | `uint` | PK, `>0` |
| `stagedataidx` | `uint` | 유효한 `StageData.idx` |
| `rows` | `byte` | `1–6` |
| `columns` | `byte` | `1–6` |
| `minactivechunks` | `byte` | `1–18` |
| `maxactivechunks` | `byte` | `min–18` |
| `bossroomresourceidx` | `uint` | 유효한 `ResourceData.idx` |
| `nextstageidx` | `uint` | 마지막 Stage는 `0` 허용 |

### StageRunData

| 필드 | 형식 | 설명 |
|---|---|---|
| `stagedataidx` | `uint` | 현재 Stage |
| `layoutdataidx` | `uint` | 사용한 레이아웃 규칙 |
| `seed` | `uint` | 재현용 시드 |
| `rows` | `byte` | 확정 행 수 |
| `columns` | `byte` | 확정 열 수 |
| `startslotidx` | `byte` | 시작 슬롯 |
| `bossgateslotidx` | `byte` | Boss Gate 슬롯 |
| `currentslotidx` | `byte` | 현재 슬롯 |
| `buildpower` | `byte` | 안내·분석용 성장 점수 |
| `completionlocked` | `bool` | 중복 보상 방지 |

### ChunkSlotData

| 필드 | 형식 | 제약 |
|---|---|---|
| `slotidx` | `byte` | `row × columns + column` |
| `chunkresourceidx` | `uint` | `ResourceData.idx` |
| `chunktype` | `byte` | `1–11` |
| `connectionmask` | `byte` | 하위 4비트만 사용 |
| `visited` | `bool` | 현재 런 상태 |
| `cleared` | `bool` | 현재 런 상태 |
| `rewardclaimed` | `bool` | 중복 지급 방지 |
| `runtimevariant` | `byte` | 몬스터·지형 변형 번호 |

### ConnectionMask

| 비트 | 값 | 방향 | 반대 방향 |
|---:|---:|---|---:|
| `0` | `1` | 상 | `4` |
| `1` | `2` | 우 | `8` |
| `2` | `4` | 하 | `1` |
| `3` | `8` | 좌 | `2` |

연결된 두 슬롯은 대응하는 양방향 비트를 모두 가져야 한다.

## 4. 생성 절차

```text
Generate(stageDataIdx, seed)
1. StageLayoutData 검증
2. rows, columns 결정
3. Start 외곽 셀 선정
4. 거리 2–4의 Boss Gate 셀 선정
5. 두 셀 사이 직행 경로 생성
6. 선택 분기 최소 3개 생성
7. 순환 연결 최소 2개 추가
8. 청크 유형 배정
9. ResourceData.idx 후보 배정
10. 몬스터 위협 예산 배정
11. 그래프·FK 검증
12. StageRunData 저장
```

### 크기 결정

| Stage 구간 | 후보 배열 | 활성 청크 |
|---|---|---:|
| 튜토리얼 | `1×2`, `2×2` | `2–4` |
| 초반 | `3×4`, `4×3` | `9–11` |
| 표준 | `4×4`, `4×5`, `5×4` | `11–15` |
| 후반 | `5×5`, `5×6`, `6×5` | `14–18` |
| 특수 | `6×6` | `16–18` |

### 그래프 조건

```text
startSlot != bossGateSlot
activeChunkCount <= 18
startToBossShortestPath >= 2
startToBossShortestPath <= 4
optionalBranchCount >= 3
cycleCount >= 2
allActiveChunksReachable == true
emptyDeadEndCount == 0
```

- Boss Gate 접근에 Combat 클리어 수를 요구하지 않는다.
- 막다른 활성 셀은 Reward, Event, Challenge 또는 Treasure여야 한다.
- 한 슬롯의 연결 수는 최대 `3`이다.
- 분기 길이는 `1–4개` 슬롯으로 제한한다.

## 5. 청크 후보 선택

청크 후보는 다음 조건을 모두 만족해야 한다.

```text
ResourceData.idx 유효
요구 ChunkType 일치
ConnectionMask 수용 가능
현재 Stage 허용 범위
동일 리소스 사용 횟수 ≤ 2
```

Stage 1 신규 청크는 `ResourceData.idx 1050–1099` 범위에 등록한다. `1040–1042`는 기존 Entry, Battle, Boss 리소스로 유지한다.

## 6. 룸 전환

```text
출구 진입
→ 전환 잠금
→ 목적 slotidx 및 chunkresourceidx 검증
→ Fade-Out
→ 기존 비인접 청크 해제
→ 목적 청크 로드·인스턴스화
→ 플레이어 재배치
→ 몬스터 스폰
→ 카메라 SnapToTarget()
→ 최소 전환 버퍼
→ Fade-In
→ 전환 잠금 해제
```

- 현재 청크와 연결된 인접 청크만 사전 로드한다.
- 동시 유지 인스턴스는 최대 `4개`다.
- `Player.Instance`를 재사용하고 중복 생성하지 않는다.
- 전환 중 재진입은 무시한다.
- 전환 잠금은 성공·실패와 무관하게 해제한다.

## 7. 전투 생성

### Stage 1 위협 비용

| `UnitBaseData.idx` | 비용 |
|---:|---:|
| `3101` | `1` |
| `3102` | `2` |
| `3103` | `3` |

### 제약

```text
activeMonsterCount <= 4
simultaneousAttackTokenCount <= 2
activeUnitCount(3103) <= 1
sameCompositionUseCount <= 2
```

- 몬스터 생성은 `UnitSpawner → UnitPoolManager → ResourceManager` 경로만 사용한다.
- 일부 마커 데이터가 누락되면 해당 개체만 제외한다.
- 유효 개체가 없으면 전투 청크를 자동 클리어한다.

## 8. Boss Gate와 Stage 완료

### 입장 조건

```text
currentSlotIdx == bossGateSlotIdx
AND bossRoomResourceIdx 유효
AND transitionLocked == false
```

방문 청크 수, Combat 클리어 수, `buildpower`는 조건에 포함하지 않는다.

### 승리 처리

```text
Boss 사망
→ completionlocked 검사·설정
→ 보상 지급
→ Stage 완료 저장
→ nextstageidx 검증
→ 다음 seed 발급
→ 다음 Stage 생성·진입
```

`nextstageidx == 0`이면 런 완료 후 HubScene으로 복귀한다.

## 9. 저장·복원

- 시드만 저장하고 로드 시 재생성하지 않는다.
- 확정된 모든 `ChunkSlotData`를 저장한다.
- 방문, 클리어, 보상 수령 상태를 개별 저장한다.
- 처치한 몬스터와 수령한 보상은 재생성하지 않는다.
- Rest는 기본 `1회`, Shop 목록은 런 동안 고정한다.
- 저장 단위는 룸 전환 완료와 보상 획득 직후다.

## 10. 15 FPS 판정 규칙

| 시스템 | 규칙 |
|---|---|
| 패링 | 누적 시간 기준, 최소 `0.134초` 보장 |
| 회피 | 무적 시간 `0.30초` 절대 시간 유지 |
| 가드 | 유지 상태를 프레임별 재입력으로 판단하지 않음 |
| 점프 | 충돌 시 지상 상태와 수직 위치 동시 검사 |
| 다단 공격 | 물리 틱당 최대 `1회` 피해 |
| 전조 | 종료 프레임 누락 시 다음 물리 틱에 1회 실행 |
| 전환 | 프레임당 하나의 로딩 단계만 확정 |

## 11. Fallback

| 실패 | 처리 |
|---|---|
| `rows`, `columns` 범위 오류 | `3×4` 사용 |
| 활성 청크 `18개` 초과 | 선택 분기부터 제거 |
| 그래프 검증 실패 | 최대 `3회` 재생성 |
| 재생성 실패 | Start–Boss 직행로와 보상 분기 `2개` 생성 |
| Start 누락 | `slotidx=0` 사용 |
| Boss Gate 누락 | Start에서 거리 `2`인 유효 셀 사용 |
| 청크 `ResourceData.idx` 누락 | 같은 유형 후보로 1회 교체 |
| 교체 후보 누락 | `1041` 사용 |
| 몬스터 데이터 누락 | 해당 개체만 제외 |
| 모든 몬스터 누락 | 청크 자동 클리어 |
| Boss Room 누락 | 입장 차단 후 Entry 또는 HubScene 복귀 |
| 인접 청크 로드 실패 | 현재 청크 유지, 다른 출구 허용 |
| 모든 출구 실패 | 마지막 정상 청크로 복귀 |
| 다음 Stage 누락 | 런 완료 후 HubScene 복귀 |
| 저장 손상 | 현재 Stage를 안전 레이아웃과 새 시드로 재시작 |

## 12. 구현 완료 조건

- 동일 시드에서 동일 배열·연결·청크·몬스터 결과가 생성된다.
- 모든 활성 청크가 Start에서 접근 가능하다.
- Boss Gate는 중간 청크 클리어 없이 접근·입장 가능하다.
- 잘못된 `idx`가 있어도 전체 런이 중단되지 않는다.
- 전환 실패 후 플레이어와 현재 청크가 유지된다.
- 보상과 Stage 완료가 중복 지급되지 않는다.
- 15 FPS에서 패링·회피·가드·점프 판정이 프레임 수에 종속되지 않는다.

## 13. 최소 QA

1. `1×2`, `3×4`, `4×5`, `6×6` 생성 검증
2. 동일 시드 결과 일치 검증
3. 모든 활성 슬롯 BFS 접근성 검증
4. Start–Boss 최단 거리 `2–4` 검증
5. 보스 직행 입장 검증
6. 누락 `ResourceData.idx` 교체 검증
7. 중복 포털 진입 잠금 검증
8. 저장 후 슬롯 상태 복원 검증
9. Boss 보상 중복 방지 검증
10. 15 FPS 입력·다단 공격 스트레스 검증

### 🧠 [GameDesigner 자율 회고]
- 기획 무결성 비판: `ResourceData.idx 1050–1099`의 실제 청크 등록과 ChunkType·ConnectionMask 데이터 테이블이 아직 없으므로 현 상태에서는 생성기를 완전히 구동할 수 없다.
- 차기 방어 지침: 구현 전 데이터 스키마와 Stage 1 청크 리소스 목록을 먼저 확정하고, 생성기보다 접근성·시드 재현·Fallback 검증 테스트를 함께 작성한다.
