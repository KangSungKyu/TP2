# Codex ↔ Antigravity 통신·감사 프로토콜

## 목적

TP2의 실시간 작업 지시와 비동기 감사·복구를 분리하고, 공식 역할 세션·문서·Git 결과가 같은 작업을 중복 수행하지 않도록 최소 통신 구조를 정의한다.

## 채널 비교

| 채널 | 용도 | 강점 | 한계·금지 | 권위 |
| :--- | :--- | :--- | :--- | :--- |
| 공식 Conversation ID 직접 발주·완료 회신 | 역할 선택, 실시간 작업 지시, 승인·차단 회신 | 현재 문맥과 책임자가 명확함 | 신규 중복 세션 생성 금지; 장기 감사 원장으로 사용하지 않음 | 실시간 제어 채널 |
| Google Sheets 역할별 turn summary | 완료 turn의 모델 채널별 비동기 공유·검색 | 역할·Conversation·Turn 단위 증분 감사 가능 | 원문 전체·secret·내부 추론 복제 금지; `Conversation ID + Turn ID` 중복 금지 | 감사·복구 색인 |
| `doc/ai_order/pending|complete` | 외부 AI 자산 발주, 결과물 인수, manifest·QA 보존 | 세션 비종속 전달과 부분 완료 추적 | 성공·검증 전 complete 이동 금지; 다른 발주·OpenWiki 이동 금지 | 파일 기반 발주·인수 |
| Git branch·commit·PM/CI table | 코드·자산 결과의 정확한 변경 집합과 배포 상태 전달 | 재현 가능한 diff·commit·병합 증거 | 대화나 문서만으로 commit·merge를 추정하지 않음; Git은 CI 전담 | 결과물·배포 증거 |
| 문서작업자 단일 heartbeat | 공식 Codex 완료 turn의 1시간 idle 후 자동 증분 기록 | 조용한 정기 동기화와 누락 재시도 | heartbeat·작업·하위 에이전트 추가 생성 금지 | 자동 감사 보조 |

## 권장 최소 구조

| 단계 | 채널 | 최소 데이터 | 완료 기준 |
| :--- | :--- | :--- | :--- |
| 1. 발주 | 공식 역할 Conversation ID | 목적, 범위, 금지, Assert | 올바른 기존 역할 세션 1곳에 전달 |
| 2. 실행·회신 | 동일 Conversation | 변경 파일·메서드, PASS/FAIL/BLOCKED, Git 상태, 위험 | 완료 turn 또는 명시적 차단 |
| 3. 외부 AI 필요 시 | `doc/ai_order/pending/` | spec, 독립 prompt, acceptance criteria | 검증 완료 시만 `complete/`와 manifest/result/qa_notes 보존 |
| 4. 결과 고정 | CI branch·commit + PM/CI table | commit, branch, QA gate, 미병합·dirty 상태 | CI가 실제 Git 상태를 확인 |
| 5. 감사 색인 | Google Sheets 역할 sheet | KST, completed, 요청·결과 요약, Conversation ID, Turn ID | 중복 키 0, secret·원문 전체 0 |

실시간 제어는 공식 Conversation ID만 사용한다. Google Sheets·문서·Git 표는 완료 결과를 찾고 복구하는 채널이며 새 지시를 암묵적으로 실행시키는 채널이 아니다. 외부 AI 파일 발주는 시각 자산처럼 세션 밖 인수물이 필요한 경우에만 추가한다.

## TP2 Coordination MCP 현행 계약

| 항목 | 현행 사양 | 경계 |
| :--- | :--- | :--- |
| 접속 | 로컬 전용 `http://127.0.0.1:8765/mcp` | 외부 인터페이스 공개 금지 |
| 인증 | `TP2_COORDINATION_TOKEN` 환경변수 | 토큰 값은 설정·문서·로그에 기록 금지 |
| 도구 | `submit_order`, `list_pending`, `claim_order`, `complete_order`, `get_status` | 발주 상태 저장·조회·claim·완료 기록만 수행 |
| 작업량 힌트 | `recommended_max_files` | 초과 시 soft warning만 반환하며 발주·claim을 차단하지 않음 |
| 실행 권한 | Antigravity가 승인된 scoped shell command를 실행할 수 있음 | Git merge·push와 Unity 직접 실행은 권한 밖 |
| 동시성 | SQLite `version`, `claim_token`, lease로 claim 소유권 검증 | 만료 lease는 `pending`으로 복구하고 stale token 완료를 거부 |
| 멱등성 | 동일 idempotency key의 `submit_order`와 동일 claim 결과의 `complete_order` 재호출을 중복으로 처리 | payload·revision 또는 완료 결과가 다른 재사용은 성공으로 간주하지 않음 |

현재 Coordination MCP는 Codex와 Antigravity가 같은 발주 상태를 안전하게 저장·회수하기 위한 감사 가능한 queue다. 1단계의 상태 저장·회수 계약은 유지하며, 2단계 dispatcher가 제한된 `agy` 단발 실행 경로를 추가했다. MCP에 발주가 저장됐다는 사실만으로 대상 세션이 실행됐다고 판정하지 않는다.

검증 기준은 격리 테스트 `1/1 PASS`, Antigravity 설정 URL과 서버 bind URL 일치, 테스트 DB·프로세스 residue `0`이다.

### 2단계 dispatcher 상태

| 항목 | 현행 사양 | 상태·제약 |
| :--- | :--- | :--- |
| 역할 매핑 | `AGENTS.md` 기준 Codex 8 ID, Antigravity 9 ID | 중복·누락 `0` |
| ID namespace | 메인프로그래머 UI Conversation과 `cli_mainprogrammer` CLI trajectory를 별도 식별 | dispatcher는 CLI trajectory ID만 `agy --conversation`에 전달 |
| 기본 실행 | dry-run으로 pending과 실행 예정 argv만 검증 | claim·`agy`·complete 수행 `0` |
| 명시 실행 | `--execute`에서 pending 정확히 1건을 claim한 뒤 `agy` 호출 결과로 complete | 반복 처리·자동 retry 금지 |
| 프로세스 경계 | argv 배열, `shell=False`, 실행 파일·옵션 allowlist | shell 문자열 조합과 임의 명령 실행 금지 |
| 실패 방어 | timeout, nonzero exit, invalid JSON을 성공 완료와 분리 | 실패 결과를 정상 dispatch로 기록 금지 |
| 합성 QA | dispatcher 격리 테스트 `2/2 PASS` | 실제 계정·conversation 전달 증거를 대체하지 않음 |
| 실제 smoke | Antigravity CLI 미로그인 및 대상 conversation 조회 실패 | `BLOCKED`; 작업 실행·완료 증거 없음 |

실제 smoke 재시도 조건은 동일 Antigravity 계정 로그인, 대상 Conversation ID 존재 확인, 사용자 명시 승인 세 가지를 모두 충족하는 것이다. 중지 세션 wake, 자동 heartbeat dispatch, 반복 retry는 여전히 미구현이다.

### `cli_mainprogrammer` 등록

| 항목 | 값·계약 |
| :--- | :--- |
| 이름 | `cli_mainprogrammer` |
| CLI trajectory | `edb1a3dd-9480-440f-9d90-282e1ec134d4` |
| 기존 UI Conversation | `bbabc4a9-bfbf-441a-8dc2-3a2746748ce1`; CLI trajectory와 별도 namespace |
| 계층 | Antigravity 메인프로그래머 하위 실행자, 상위 승인자는 Codex 메인프로그래머·PM |
| 실행 경계 | scoped shell·allowlist 파일 수정만 허용; Git·Unity 프로세스·하위 agent/conversation 생성 금지 |
| 완료 경로 | MCP `complete_order` 회수 대상으로 보고 |
| 재사용 | 역할별 CLI trajectory 1개를 유지하고 자동화마다 새 Conversation을 생성하지 않음 |
| 증거 | 역할 초기화 실제 CLI 1회 `SUCCESS / ACCEPTED` |

### E2E·저비용 ruleset 계약

지속 CLI Conversation에는 역할을 최초 1회만 주입한다. 이후 발주는 compact ruleset의 `version`·SHA-256 `hash`와 해당 작업의 `objective`·`allowed_files`·`acceptance`·`delta`만 전달하며, 전체 `AGENTS.md`나 `doc/` 내용을 반복 주입하지 않는다. 작업자는 적용한 version/hash를 결과에 echo해야 하고, 어느 하나라도 불일치하면 완료 처리를 금지한다.

| 항목 | 완료 게이트·경계 |
| :--- | :--- |
| 실제 E2E | MCP 발주→claim→동일 CLI Conversation 실행→완료 회수 `1/1 PASS` |
| 독립 리뷰 환류 | Codex 독립 리뷰 후 동일 Conversation에 delta feedback 1회 전달 `SUCCESS` |
| worker 결과 | `SUCCESS`, nonempty summary, 고정 schema, 최대 `32 KiB`, secret 미포함을 모두 검증 |
| 프로세스 출력 | `agy` stdout 최대 `64 KiB`; raw 대화 원문은 저장하지 않음 |
| 실패 상태 | `BLOCKED`·`FAILED` 또는 ruleset echo 불일치는 complete 금지; lease 만료 뒤 pending으로 재회수 |
| 실행 진입점 | `scripts/coordination_mcp/run.ps1`을 portable 진입점으로 사용 |
| 보류 | 역할별 세분화 인증, Windows process-tree 종료의 동적 검증 |

#### MCP 도구 `inputSchema`

모든 도구는 object 입력, `additionalProperties: false`를 적용한다.

| 도구 | 필수 입력 | 선택 입력·제약 |
| :--- | :--- | :--- |
| `submit_order` | `order_id`, `idempotency_key`, `revision=1`, `payload` | payload 필수: `source_conversation`, `target_conversation`, `objective`, `allowed_files`(1+), `forbidden_files`, `acceptance`(1+), `base_branch`, `base_sha`, `recommended_max_files`(uint), `max_revision=1`, `ruleset_version`, `ruleset_hash`(64자 소문자 hex) |
| `list_pending` | `target_conversation` | `limit` 1..100 |
| `claim_order` | `order_id`, `worker_id`, `expected_version`(uint), `lease_seconds` | `lease_seconds` 1..3600 |
| `complete_order` | `order_id`, `claim_token`, `expected_version`(uint), `state`, `result` | `state`는 `submitted` 또는 `complete`; result object 최대 `32 KiB`, secret 금지 |
| `get_status` | `order_id` | 없음 |

검증 결과는 Coordination MCP QA `5/5 PASS`다. 이 결과는 현재 E2E·schema·ruleset 경계를 증명하지만, 보류된 인증 세분화나 Windows process-tree 동적 검증을 완료한 것으로 간주하지 않는다.

## 자동 동기화와 장애 폴백

| 장애 | 1차 조치 | 폴백 | 금지 |
| :--- | :--- | :--- | :--- |
| 공식 역할 세션 응답 없음 | 같은 Conversation에 1회 후속 확인 | PM이 BLOCKED와 누락 증거 기록 | 동일 역할 신규 세션 생성 |
| Google Sheets 연결 실패 | 제품 작업은 계속하고 실패 범위·Turn ID 보존 | 다음 단일 heartbeat 또는 사용자 수동 동기화에서 누락 turn만 재시도 | 중복 append, 다른 모델 문서 혼합 |
| 외부 AI 부분 완료·검증 실패 | pending 유지, Status를 IN_PROGRESS/BLOCKED로 갱신 | 결과·위험을 qa_notes 초안에 보존 | complete 이동, pending 중복본 유지 |
| Git 미발행·미병합 | CI에 실제 branch/commit/dirty 상태 전달 | PM/CI table에 대기 상태 기록 | 문서작업자의 Git 조작, commit 추정 |
| 대화 이력과 Git 결과 불일치 | Git diff·commit과 QA 산출물을 우선 재감사 | Conversation/Turn 요약을 정정하되 원문은 복제하지 않음 | 수치 추정, 성공 과장 |
| Coordination MCP 정지·접속 실패 | 발주 원문과 idempotency key를 보존하고 제품 작업은 계속 | 서버 복구 후 `get_status`로 확인하고 미등록 발주만 재제출 | 새 세션 생성, 중복 dispatch, 토큰 기록 |

## 불변 규칙

1. 공식 역할마다 기존 Conversation ID를 재사용하고 중복 task·독립 세션·하위 에이전트를 생성하지 않는다.
2. 자동 기록은 문서작업자 단일 heartbeat만 사용하며 마지막 응답 후 1시간 idle인 completed turn만 기록한다. 사용자 수동 요청은 즉시 신규 완료 turn을 전수 검사한다.
3. Google Sheets 중복 키는 `Conversation ID + Turn ID`다. 기존 행은 유지하고 신규 turn만 append한다.
4. secret·인증정보·내부 추론·raw tool output·원문 전체를 대화 색인이나 발주 파일에 복제하지 않는다.
5. 실시간 승인, 비동기 감사, 파일 인수, Git 배포 증거의 책임을 서로 대체하지 않는다.

### 🔄 [PM/CI 동기화] 변경 이력 테이블

| 최근 수정 일시 | 수정자 (역할) | 수정 및 추가된 파일/메서드 명세 | QA 검증 통과 기준 (Assert) |
| :--- | :--- | :--- | :--- |
| 2026-08-27 | 문서작업자 | Codex↔Antigravity 최소 통신 구조·감사/복구 폴백 신규 명세 | 채널 책임 분리, 중복 세션 0, Conversation+Turn dedupe, secret·원문 복제 0 |
| 2026-08-28 | 문서작업자 | TP2 Coordination MCP 접속·인증·도구·lease·멱등성·권한 경계 동기화 | 격리 테스트 1/1 PASS, 설정 URL 일치, residue 0 |
| 2026-08-28 | 문서작업자 | Coordination dispatcher dry-run·단발 execute·방어 경계와 smoke BLOCKED 상태 동기화 | 역할 ID 중복·누락 0, synthetic 2/2 PASS, 실제 smoke BLOCKED |
| 2026-08-28 | 문서작업자 | `cli_mainprogrammer` UI/CLI namespace·권한·재사용·완료 회수 계약 등록 | 역할 초기화 실제 CLI 1회 SUCCESS / ACCEPTED |
| 2026-08-28 | 문서작업자 | Coordination MCP E2E·compact ruleset·worker 검증·5개 inputSchema 동기화 | 실제 E2E 1/1 PASS, 동일 Conversation delta feedback SUCCESS, QA 5/5 PASS |
