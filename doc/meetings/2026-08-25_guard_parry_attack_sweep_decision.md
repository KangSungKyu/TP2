# Guard/Parry Attack Sweep 의사결정 안건

- 일자: 2026-08-25
- 상태: `APPROVED`
- 목적: 구현 승인 전 Guard/Parry/Body 선접촉 판정과 공격 경로 보존 정책 확정
- 범위: 기획·기술 최소안·QA 승인 기준

## 1. 문제 정의

긴 thrust가 Active에 진입하는 순간 Guard와 Body를 동시에 overlap할 수 있다. 이 경우 두 후보의 sweep fraction이 모두 `0/0`으로 관측되며, 현행 tie 정책은 Body를 우선한다. 정면 방어 입력이 유효해도 Body 피해로 확정될 수 있으므로, 경로 시작점과 동률 우선순위를 구현 전에 결정해야 한다.

## 2. 기획 권고 계약

1. 공격 경로의 시작은 Startup으로 본다.
2. Active 진입 즉시 공격 facing을 고정한다.
3. Guard·Parry·Body 후보 중 sweep fraction이 작은 후보를 우선한다.
4. epsilon 범위의 정면 동률에만 `Parry > Guard > Body`를 적용한다.
5. 후면 접촉, 방어 비활성, 공격·방어 방향 불일치는 Body로 판정한다.
6. 다단 공격은 Hit tick마다 독립 경로와 독립 판정을 사용한다.
7. 이동 공격은 `Chase 종료 → Step/Lunge 단일 Motor writer → Active 정지` 순서를 기본으로 한다.
8. cancel, death, groggy, pool 반환 시 보존된 경로 history를 즉시 폐기한다.

## 3. 기술 최소안

- 마지막 exterior pose 1개만 보존한다. ring buffer와 신규 manager는 만들지 않는다.
- 매 FixedStep 현재 공격 shape의 overlap을 검사한다.
  - 비접촉 상태에서는 exterior pose를 현재 pose로 갱신한다.
  - 접촉이 시작되면 마지막 비접촉 exterior pose를 고정한다.
- Active 판정은 `exterior pose → current pose` 구간에 기존 `AttackSweep2D` cast를 적용한다.
- face flip, teleport, attack generation 변화 시 exterior history를 폐기한다.
- `ExecuteSkillHitsAsync`에 nullable initial exterior pose를 추가한다.
- CSV, schema, 신규 idx는 추가하지 않는다.
- 기존 Guard/Body fraction 비교와 tick별 피해 dedupe는 유지한다.

### 승인 정책: 시작부터 overlap

Reservation 또는 Startup 시작 시점부터 공격 shape가 Guard/Body와 overlap하여 마지막 exterior pose가 없으면 Body로 판정한다. 추정 pose, Body collider 재사용, 역방향 보간으로 exterior를 합성하지 않는다.

## 4. QA 계획 — 36 Case

기본 36개는 `판정 대상 3 × 공격 유형 4 × FPS 3`으로 고정한다.

| 축 | 값 | 수 |
| :--- | :--- | ---: |
| 판정 대상 | Guard, Parry, Body | 3 |
| 공격 유형 | thrust, arc, multi-hit, moving attack | 4 |
| 프레임 환경 | 60 FPS, 30 FPS, 15 FPS | 3 |
| 합계 | `3 × 4 × 3` | 36 |

각 case에 다음 공통 Assert를 적용한다.

- 좌우 facing 대칭 결과
- static target과 moving target의 fraction 순서 보존
- Startup overlap과 epsilon tie 정책 준수
- multi-hit의 tick별 history·dedupe 독립
- Active 진입 후 기본 이동 정지
- cancel/death/groggy/pool 반환 후 history 0
- face flip, teleport, generation 변화 후 이전 history 재사용 0
- 후면·비활성·방향 불일치 시 Body 판정
- 60/30/15 FPS에서 판정 우선순위 동일

## 5. 결정표

PM이 승인한 항목만 구현 권위로 사용한다.

| ID | 결정 항목 | 선택지 | 권고 |
| :--- | :--- | :--- | :---: |
| D1 | 정면 fraction epsilon tie | [x] `Parry > Guard > Body` | 승인 |
| D2 | exterior history 없음 | [x] Body | 승인 |
| D3 | attack direction | [x] Startup snapshot | 승인 |
| D4 | moving attack | [x] Active 진입 기본 정지; 명시 이동공격만 Active 이동 | 승인 |
| D5 | 기술 최소안 | [x] 마지막 exterior pose 1개 | 승인 |

### 승인 기록

| 항목 | 결정 | 결정자 | 일시 (KST) | 비고 |
| :--- | :--- | :--- | :--- | :--- |
| D1 | 정면 epsilon tie `Parry > Guard > Body` | PM | 2026-08-25 | 작은 fraction 우선; 정면 동률만 방어 우선 |
| D2 | exterior pose 없음은 Body | PM | 2026-08-25 | 합성·역보간 금지 |
| D3 | Startup snapshot facing | PM | 2026-08-25 | Active 중 flip 금지 |
| D4 | Active 기본 정지, 명시 이동공격만 예외 | PM | 2026-08-25 | Motor 단일 writer 유지 |
| D5 | 마지막 exterior pose 1개 | PM | 2026-08-25 | ring buffer·신규 manager 없음 |

## 6. 구현 순서와 책임자

| 순서 | 책임자 | 산출물·완료 조건 |
| ---: | :--- | :--- |
| 1 | 게임플레이기획자 | D1–D5 확정, epsilon과 방향·이동 정책 승인 |
| 2 | 메인프로그래머 | 마지막 exterior pose 1개 기반 최소 구현, 기존 sweep·dedupe 재사용, CSV/schema 변경 0 |
| 3 | QA | 36 case 및 공통 Assert 검증, PASS/FAIL/BLOCKED 분리 |
| 4 | PM·문서작업자 | 승인 결과와 구현·QA 근거를 관련 명세 및 회의 결정 기록에 동기화 |
| 5 | CI | 승인 범위만 diff 감사 후 commit·push 및 결과 보고 |

## 7. 구현 승인 게이트

- D1–D5 승인 완료로 구현 착수 가능하다.
- 신규 manager, ring buffer, CSV/schema 추가는 별도 재승인 없이는 금지한다.
- QA 36 case 중 실행 0, stale, timeout은 PASS가 아니라 `BLOCKED`다.

### 🔄 [PM/CI 동기화] 변경 이력 테이블

| 최근 수정 일시 | 수정자 (역할) | 수정 및 추가된 파일/메서드 명세 | QA 검증 통과 기준 (Assert) |
| :--- | :--- | :--- | :--- |
| 2026-08-25 | 문서작업자 | `guard_parry_attack_sweep_decision.md` 의사결정 안건 신규 작성 | D1–D5 승인 전 구현 금지, QA 36 case 및 history 폐기 계약 명시 |
| 2026-08-25 | 게임플레이기획자 | D1–D5 PM 승인 반영, 시작 overlap fallback 확정 | 정면 epsilon tie 방어 우선, exterior 없음 Body, Startup facing, Active 정지, history 1개 |
