# Golden Map 선택 준비

## 목적과 범위

후보 2종을 사용자가 직접 비교·평가하고, Golden Map 확정 이후 변형 생성 기준을 누적한다. 이 문서와 연결된 CSV는 검토 기록이며 Runtime 데이터가 아니다.

## 후보 현황

| candidateidx | 상태 | 생성 방식 | 사전 사실 |
|---:|---|---|---|
| `1u` | `reviewed` | CA 기반 MinModuleBlob | 시각 평균 4.0, 사용자 판정 `accept`, 기술 `PENDING` |
| `2u` | `reviewed` | CA 0, Room+Terrain 분리 | 시각 평균 3.2, 기술 `UNKNOWN`, 판정 `hold` |

### Candidate 1u — MinModuleBlob

![Candidate 1u MinModuleBlob](../../Assets/Screenshots/StageChunkV10BlobGraph/StageChunkV10BlobGraph_XLarge_Comparison_MinModuleBlob.png)

#### Candidate 1u 최신 사용자 평가 — 2026-08-18

| 공간리듬 | 밀도 | 자연스러움 | 탐험성 | 랜드마크성 | 평균 | 기술 상태 | 사용자 판정 |
|---:|---:|---:|---:|---:|---:|---|---|
| 4 | 4 | 4 | 4 | 4 | 4.0 | `PENDING` | 시각 승인 (`accept`) |

- 사용자 의견: 몬스터가 스폰될 위치가 좁다. 스폰과 전투를 위한 공간을 먼저 확보하는 것도 방법.
- 최신 Candidate 프리팹: [Tilemap_Room_Candidate1u_ImageReconstructed.prefab](../../Assets/Prefabs/Development/Tilemap_Room_Candidate1u_ImageReconstructed.prefab)
- 최신 검증 이미지: [candidate_1u_reconstructed_validation.png](../../Assets/Screenshots/GoldenMapValidation/candidate_1u_reconstructed_validation.png)
- 기술 상태가 `PENDING`인 이유: Portal ordered/Spawn motor QA가 미완료이거나 기존 FAIL·인프라 BLOCKED가 남아 있다.
- `accept`는 사용자 시각 승인이다. Golden 최종 승격은 Technical `PASS` 이후에만 가능하다.

#### Candidate 1u 이전 사용자 평가 — 2026-08-14

#### Candidate 1u 사용자 평가 — 2026-08-14

| 공간리듬 | 밀도 | 자연스러움 | 탐험성 | 랜드마크성 | 평균 | 기술 상태 | 판정 |
|---:|---:|---:|---:|---:|---:|---|---|
| 4 | 4 | 4 | 4 | 3 | 3.8 | `UNKNOWN` | 보류 (`hold`) |

![Candidate 1u review annotations](Annotations/candidate_1u_review_2026-08-14.png)

- 유지: 좌측 ㄱ형 수직 지형.
- 변경: 1칸 돌출·오목·테두리를 정규화한다. 시각적으로 연결됐지만 Player가 통과할 수 없는 1칸 폭/높이 공간은 메우거나 통과 가능한 폭으로 확장한다.
- 주석 해석: 빨강은 제거 대상인 고립·돌출 solid, 노랑은 변경 대상인 이동 불가 협소 공간이다.
- 기술 상태가 `UNKNOWN`인 이유: 실험 Prefab/Generator가 정리되어 현재 Motor 재검증 대상이 없다. Candidate 1u는 현재 시각 Golden 후보일 뿐이며 기술 PASS로 추정하지 않는다.

### Candidate 2u — RoomTerrainBlob

![Candidate 2u RoomTerrainBlob](../../Assets/Screenshots/StageChunkV10BlobGraph/StageChunkV10BlobGraph_XLarge_Comparison_RoomTerrainBlob.png)

#### Candidate 2u 사용자 평가 — 2026-08-18

| 공간리듬 | 밀도 | 자연스러움 | 탐험성 | 랜드마크성 | 평균 | 기술 상태 | 판정 |
|---:|---:|---:|---:|---:|---:|---|---|
| 3 | 3 | 3 | 3 | 4 | 3.2 | `UNKNOWN` | 보류 (`hold`) |

![Candidate 2u review annotations](Annotations/candidate_2u_review_2026-08-18.png)

- 유지: 유닛이 끼일 틈이 전혀 없는 chunk 전반 마감.
- 변경: 좌측 상단 끼임 후보를 정규화하고 중앙 빈 공간에 발판을 추가한다.
- 폐기: chunk 곳곳의 작은 타일 조각.
- 주석 해석: 빨강은 작은 tile 제거, 노랑은 끼임 후보 정규화 및 중앙 발판 보강 대상이다.
- 기술 상태는 `UNKNOWN`이며 기술 PASS로 추정하지 않는다.

## 후보 비교 결론

- 최신 시각 평균은 1u `4.0`, 2u `3.2`로 1u가 높다.
- 잠정 기반 후보(`provisional base`)는 1u이며, 2u의 끼임 방지 마감 규칙을 하드 제약으로 차용한다.
- 1u는 사용자 시각 승인을 받았지만 기술 상태가 `PENDING`이므로 Golden은 확정되지 않았다. 2u는 기술 `UNKNOWN`이며 시각 평균도 채택 기준에 미달한다.

다음 변형 최대 3종에는 아래 공통 규칙만 적용한다.

- 1u 실루엣과 좌측 ㄱ형을 유지한다.
- 1~2셀 artifact를 0으로 한다.
- Player가 진입할 수 없는 1셀 통로를 0으로 한다.
- 중앙 대공간의 발판을 보강한다.
- 작은 tile을 0으로 한다.

## GoldenDerived 파생 시도 이력

### Trial01 사용자 평가 — 2026-08-18

| 대상 | 사용자 점수 | 사용자 판정 | 기술 근거 | 승격 |
|---|---:|---|---|---|
| [Tilemap_Room_GoldenDerived_Trial01.prefab](../../Assets/Prefabs/Development/Tilemap_Room_GoldenDerived_Trial01.prefab) | `≤2` | 폐기 (`reject`) | Empty graph 2개: main 2691셀 + 하단 격리 212셀 | 금지 |

- 원본 `Tilemap_Room_Candidate1u_ImageReconstructed`와 구조가 지나치게 유사하다.
- 유닛이 갇히는 지형이 너무 많다.
- 타일맵 하단에 이동할 수 없는 지형이 존재한다.
- Runtime·CSV·Addressables·Stage1 참조는 0이다.
- Trial01은 재수선하지 않고 생성 규칙 재설계 대상으로 분류한다.
- 이 폐기 판정은 Candidate 1u의 기존 사용자 시각 승인(`accept`)을 변경하지 않는다.

### Trial04 사용자 평가 — 2026-08-18

| 대상 | 사용자 판정 | 기술 상태 | Golden 승격 |
|---|---|---|---|
| [Tilemap_Room_EmptyFirstAngular_Trial04.prefab](../../Assets/Prefabs/Development/Tilemap_Room_EmptyFirstAngular_Trial04.prefab) | 선호 후보 (`preferred candidate`) | `PENDING` | Spawn focused `PASS` 전 금지 |

- 빨간 영역의 Spawn이 지형에 매립되거나 공중에 있다.
- 그 외에는 지금까지 생성 결과 중 가장 이상적이다.
- Spawn 보정 좌표: S1 `(18,5.51)`, S2 `(66,5.51)`, S3 `(42,41.51)`, S4 `(42,26.51)`.
- Ground·OneWay·Portal·Camera geometry는 동결한다.
- 컴파일 및 제품 Error는 0이다.
- 최종 focused QA는 `editor_unfocused`와 mutating fixture로 실행 0건, `BLOCKED`다.
- Golden 최종 승격 전 Spawn focused QA `PASS`가 필요하다.

## 시각 평가

각 항목을 1~5 정수로 평가한다. 관찰하지 않은 항목은 공란으로 두며 추정하지 않는다.

| 항목 | 1 | 3 | 5 |
|---|---|---|---|
| 공간 리듬 | 단조롭거나 끊김 | 부분적으로 변화 있음 | 이동·휴식·전투 흐름이 명확함 |
| 밀도 | 지나치게 비거나 과밀 | 수용 가능 | 역할과 시야에 적합함 |
| 자연스러움 | 인공적·반복적 | 일부 자연스러움 | 지형 연결과 실루엣이 자연스러움 |
| 탐험성 | 선택지 부족 | 제한적 분기 | 의미 있는 우회·발견 동선 제공 |
| 랜드마크성 | 구분 불가 | 일부 식별 가능 | 기억 가능한 중심·방향 단서 제공 |

평가 항목: `공간리듬`, `밀도`, `자연스러움`, `탐험성`, `랜드마크성`.

## 기술 평가

각 항목은 `PASS`, `FAIL`, `PENDING`, `UNKNOWN` 중 하나로 기록한다.

| 항목 | 통과 기준 |
|---|---|
| 끼임 | 실제 이동 경로에서 끼임 0 |
| island/spur | 허용되지 않은 고립 island 및 spur 0 |
| 통로 | 최소 `2×2` 확보 |
| Portal | 4개 계약 충족 |
| Spawn | 4개 계약 충족 |
| Graph | 연결·결정성 계약 충족 |
| Motor | ordered routes `12/12` |
| 기존96 | 기존 회귀 `96/96` |

기술 상태는 위 항목을 근거로만 판정한다. 검증 대상은 있으나 QA가 미완료 또는 인프라 차단 상태면 `PENDING`, 근거가 없으면 `UNKNOWN`, 하나라도 확정 `FAIL`이면 `FAIL`, 전부 통과한 경우에만 `PASS`로 기록한다.

## 채택 판정

| 결정 | 조건 |
|---|---|
| Golden 최종 승격 | 기술 전 항목 `PASS` + 시각 평균 `≥4.00` + 시각 최저점 `≥3` |
| 보류 | 기술 상태 `PENDING` 또는 `UNKNOWN`, 또는 기술 전 항목 PASS 상태에서 시각 평균 `3.00~3.99` |
| 폐기 | 기술 항목 하나 이상 `FAIL`, 또는 시각 평균 `<3.00` |

점수가 비어 있으면 평균과 최저점을 계산하지 않고 `pending`을 유지한다.

## 주석·Issue 규칙

| 색상 | 의미 |
|---|---|
| 녹색 | 유지·채택 요소 |
| 노란색 | 검토·보류 요소 |
| 빨간색 | 결함·폐기 요소 |
| 청록색 | 이동·Portal·Spawn·Graph 기술 주석 |
| 자홍색 | 랜드마크·시각 강조 주석 |

- 모든 주석에는 고유 `uint issueidx`를 부여한다.
- 문자열 이름을 식별자로 사용하지 않는다.
- 동일 이슈의 후속 기록은 같은 `issueidx`를 유지한다.

## Golden 확정 후 변형 규칙

- Golden 후보 확정 후 변형은 최대 3개만 생성한다.
- 허용 변형 축: `Seed`, `density ±5%p`, `landmark 1개`.
- 한 변형에서 두 축을 동시에 변경하지 않는다.
- 각 변형은 Golden과 동일한 시각·기술 평가표를 사용한다.

## 기록 위치

- 평가 입력: [golden_map_evaluation.csv](golden_map_evaluation.csv)
- CSV의 빈 점수·판정은 미평가 상태이며 추정값을 채우지 않는다.

### 🔄 [PM/CI 동기화] 변경 이력 테이블

| 최근 수정 일시 | 수정자 (역할) | 수정 및 추가된 파일/메서드 명세 | QA 검증 통과 기준 (Assert) |
| :--- | :--- | :--- | :--- |
| 2026-08-14 | 문서작업자 | `golden_map_selection.md`, `golden_map_evaluation.csv` 신규 작성 | 후보 링크 2개, candidateidx 1u/2u, 평가·판정·issue·변형 규칙 및 CSV 문법 검증 |
| 2026-08-14 | 문서작업자 | Candidate 1u 사용자 평가와 주석 이미지 링크 반영 | 시각 점수 4/4/4/4/3, 평균 3.8, 기술 UNKNOWN, hold; 2u 공란 보존 |
| 2026-08-18 | 문서작업자 | Candidate 2u 평가·주석 링크 및 후보 비교 결론 반영 | 2u 점수 3/3/3/3/4, 평균 3.2, 기술 UNKNOWN, hold; provisional base 1u, Golden 미확정 |
| 2026-08-18 | 문서작업자 | Candidate 1u 최신 사용자 평가와 프리팹·검증 이미지 링크 반영 | 시각 점수 4/4/4/4/4, 평균 4.0, 사용자 accept, 기술 PENDING; Technical PASS 전 Golden 미승격 |
| 2026-08-18 | 문서작업자 | GoldenDerived Trial01 사용자 평가 이력 추가 | 사용자 점수 ≤2, reject, Empty graph 2개; 승격 금지 및 생성 규칙 재설계 대상으로 분류 |
| 2026-08-18 | 문서작업자 | EmptyFirstAngular Trial04 사용자 평가 이력 추가 | preferred candidate, technical PENDING; Spawn focused QA 0건 BLOCKED, PASS 전 Golden 승격 금지 |
