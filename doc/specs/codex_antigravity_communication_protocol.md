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

현재 Coordination MCP는 Codex와 Antigravity가 같은 발주 상태를 안전하게 저장·회수하기 위한 감사 가능한 queue다. 중지된 세션 wake, `agy` 실행, 실제 Conversation ID로 prompt dispatch는 구현하지 않았으며 별도 승인·QA가 필요한 후속 게이트다. 따라서 MCP에 발주가 저장됐다는 사실만으로 대상 세션이 실행됐다고 판정하지 않는다.

검증 기준은 격리 테스트 `1/1 PASS`, Antigravity 설정 URL과 서버 bind URL 일치, 테스트 DB·프로세스 residue `0`이다.

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
