# TP2 Stage/Chunk 생성 규칙 설계 회의록

- 일시: 2026-08-14 (KST)
- 상태: 최종 의사결정 완료, 구현 승격 전 `NO-GO`
- 목적: Stage/Chunk 증설 규칙과 생성·이동·성능 승인 기준을 확정하고 후속 작업 순서를 결정한다.

## 참여 역할

- 사용자: 형태 후보 및 기본 Profile 최종 선택
- 프로젝트매니저: 범위·승인 게이트·후속 공정 통제
- 게임플레이기획자: Room 역할과 생성 규칙 설계
- 메인프로그래머: 생성기·실제 Kinematic motor 계약 검토
- 리소스작업자: prototype, QA prefab, PNG/meta 산출
- QA: seed 결정성, traversal, 성능 상한 검증
- CI: 승인 이후에만 Git 발행 담당

## 하드 제약

- 문자열 키를 사용하지 않고 Stage 이동·해금·리소스 관계는 `uint idx`로 연결한다.
- 개별 생성물이 Addressables를 직접 호출하지 않으며 공용 리소스 경로를 따른다.
- 이동 가능성의 최종 권위는 `KinematicMotor2D` 실제 경로 검증이다.
- 기존 제품 Room, CSV, Addressables, `_Recovery` 및 unrelated dirty 변경을 보존한다.
- generated OpenWiki 페이지는 직접 수정하지 않는다.

## 최종 결정 — 생성 규칙 v0.8

| 대상 | 크기 | CA Profile | 점유율 | 평활화 |
|---|---:|---|---:|---:|
| 일반 Room | 각 축 2배 | Sparse | 0.400 | 4회 |
| Landmark Room | 각 축 3배 | Balanced | 0.4375 | 6회 |
| Boss Room | 각 축 3배 | Sparse | 0.400 | 4회 |

- CA 판정은 8-neighbor 중 solid 5개 이상을 기준으로 한다.
- Portal safe zone은 출입구마다 최소 2 modules를 확보한다.
- 하단에는 평지·언덕을 제공하거나, 현재 허용된 이동 능력으로 탈출 가능한 구멍만 허용한다.
- 지형 Module을 먼저 생성한 뒤 최종 Chunk 단계에서 발판을 후처리한다.
- 발판 길이는 최소 3칸, 발판 사이 간격은 2~4칸으로 제한한다.
- 생성 결과 보정은 최대 10회까지만 허용하며 초과 seed는 실패로 기록한다.
- Boss 처치 완료 후 `next stage uint idx`를 해금한다.
- 성능 상한은 현행 baseline 대비 1.5배다.
- 현재 개발 PC는 성능 등급 4/5로 가정한다.
- 목표 크기와 콘텐츠 구조를 먼저 증설한 뒤 실측 병목을 기준으로 최적화한다.

## 사용자 결정

- 일반 2배 Room 기본 Profile로 Sparse를 채택했다.
- Landmark는 Balanced, Boss는 전투 시야와 이동 공간 확보를 위해 Sparse로 확정했다.
- 형태 축소나 사전 최적화보다 증설 완료 후 계측 기반 최적화를 선택했다.

## Prototype·QA 증거

| 게이트 | 결과 |
|---|---:|
| Sparse/Balanced/Dense 생성 | 각 200/200 PASS |
| 전체 seed·결정성 hash | 600/600 PASS |
| Portal safe zone | 결과별 4/4 PASS |
| 최대 보정 | 10회 이하 계약 PASS |
| 일반 2배 motor, 60/15 FPS | 각각 12/12 PASS |
| 3배 Landmark motor, 60/15 FPS | 각각 12/12 PASS |
| 3배 Boss motor, 60/15 FPS | 각각 12/12 PASS |
| PNG/meta | 8/8 |
| Compile·제품 Console | Error 0 |

초기 North→South 실패는 실제 EntryMarker가 아닌 인접 타일 중심을 종점으로 사용한 QA helper 오류였으며, 공용 QA helper 수정 후 60/15 FPS 검증이 통과했다.

## 성능 결과

| 비교 | GameObject | Collider | Runtime memory | Instantiate | 계약 검증 |
|---|---:|---:|---:|---:|---:|
| 일반→2배 | 1.063× PASS | 2.000× FAIL | 7.588× FAIL | 5.185× FAIL | 0.956× PASS |
| Boss→3배 Landmark | 1.214× PASS | 2.000× FAIL | 6.624× FAIL | 4.617× FAIL | 0.829× PASS |
| Boss→3배 Boss | 1.286× PASS | 2.000× FAIL | 6.030× FAIL | 3.792× FAIL | 0.849× PASS |

- CPU/GPU frame time, drawcall, batch 및 300 rendered frames는 PlayMode 렌더링 harness가 없어 미검증이다.
- 생성 결정성과 motor traversal 통과만으로 성능 승인을 대체하지 않는다.

## Stage2 판정

`NO-GO`.

제품 Room 증설과 병목 최적화가 남아 있고, Collider·Runtime memory·Instantiate가 1.5배 상한을 초과했으며 렌더링 성능도 미검증이다. Stage2 명세 승격, 제작 및 Addressables 등록은 후속 게이트 통과 전까지 금지한다.

## 후속 작업 DAG

1. 일반 Room 각 축 2배 증설
2. Landmark·Boss Room 각 축 3배 증설
3. Portal safe zone, 탈출 가능 하단 지형, 발판 후처리, 최대 10회 보정 계약 적용
4. 실제 `KinematicMotor2D` 60/15 FPS traversal 재검증
5. 증설 결과 기준 Collider·Runtime memory·Instantiate 병목 계측
6. Collider·메모리·instantiate 구조 최적화
7. CPU/GPU frame time, drawcall, batch, 300 rendered frames PlayMode 측정
8. baseline×1.5 성능 게이트 재검증
9. Boss 처치 후 `next stage uint idx` 해금 계약 검증
10. 전체 통과 시에만 Stage2 Go/No-Go 재판정

## 보존·변경 범위

- 본 회의록만 추가했다.
- 제품 코드·자산·CSV·Addressables·Git은 수정하지 않았다.
- 기존 `_Recovery`와 unrelated dirty 변경을 보존했다.

### 🔄 [PM/CI 동기화] 변경 이력 테이블

| 최근 수정 일시 | 수정자 (역할) | 수정 및 추가된 파일/메서드 명세 | QA 검증 통과 기준 (Assert) |
| :--- | :--- | :--- | :--- |
| 2026-08-14 | 문서작업자 | `stage_chunk_generation_rules_meeting_minutes_2026-08-14.md` 신규 회의록 | v0.8 결정, 600/600 결정성, motor 12/12, 성능 FAIL 수치, Stage2 NO-GO 및 후속 DAG 대조 |
