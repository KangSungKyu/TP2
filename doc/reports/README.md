# 📑 TP2 프로젝트 작업 보고서 대시보드 (Reports Index)

본 디렉터리는 `TP2` 프로젝트의 일일/주간 마감 보고서 및 세션 대화 이력서를 관리하는 공간입니다.

---

## 📋 세션 대화 & 작업 이력 관리서
- 📋 [작업자 세션별 대화 및 작업 이력 기록서](file:///c:/Users/PC/Projects/TP2/doc/reports/worker_session_histories.md)
- 📋 [세션별 대화 로그 총괄 관리서](file:///c:/Users/PC/Projects/TP2/doc/reports/session_conversation_logs.md)
- 📋 [chat_db 공식 대화 이력 기록표](file:///c:/Users/PC/Projects/TP2/doc/reports/chat_db_conversation_records.md)

---

## 🏛️ 주요 기획 & 기술 사양서 (Key Specs)
- 📄 [캐릭터 & 몬스터 세계관 콘셉트 디자인 확정 명세서](file:///c:/Users/PC/Projects/TP2/doc/specs/concept_design_conference.md)
- 📄 [12x12m 전면 확대 모듈 & 11종 룸 청크 아키텍처 사양서](file:///c:/Users/PC/Projects/TP2/doc/specs/module_12x12_chunk_spec.md)
- 📄 [2D 함정 & 트랩(Hazard) 시스템 기술 사양서](file:///c:/Users/PC/Projects/TP2/doc/specs/hazard_and_trap_spec.md)
- 📄 [9종 CSV 데이터 테이블 & 파서 사양서](file:///c:/Users/PC/Projects/TP2/doc/specs/csv_datatable_spec.md)

---

## 🗓️ 주간 보고서 (Weekly Reports)

- 📄 [2026년 8월 1주차 (2026-W32) 주간 보고서](file:///c:/Users/PC/Projects/TP2/doc/reports/weekly/2026-W32_주간보고서.md)
- 📄 [2026년 7월 5주차 (2026-W31) 주간 보고서](file:///c:/Users/PC/Projects/TP2/doc/reports/weekly/2026-W31_주간보고서.md)
- 📄 [2026년 7월 4주차 (2026-W30) 주간 보고서](file:///c:/Users/PC/Projects/TP2/doc/reports/weekly/2026-W30_주간보고서.md)

---

## 📅 일일 보고서 (Daily Reports)

### 2026년 08월
- 📄 [2026-08-19 (수) 일일 보고서](file:///c:/Users/PC/Projects/TP2/doc/reports/2026-08/2026-08-19_일일보고서.md)
- 📄 [2026-08-18 (화) 일일 보고서](file:///c:/Users/PC/Projects/TP2/doc/reports/2026-08/2026-08-18_일일보고서.md)
- 📄 [2026-08-11 (화) 일일 보고서](file:///c:/Users/PC/Projects/TP2/doc/reports/2026-08/2026-08-11_일일보고서.md)
- 📄 [2026-08-10 (월) 일일 보고서](file:///c:/Users/PC/Projects/TP2/doc/reports/2026-08/2026-08-10_일일보고서.md)
- 📄 [2026-08-07 (금) 일일 보고서](file:///c:/Users/PC/Projects/TP2/doc/reports/2026-08/2026-08-07_일일보고서.md)
- 📄 [2026-08-06 (목) 일일 보고서](file:///c:/Users/PC/Projects/TP2/doc/reports/2026-08/2026-08-06_일일보고서.md)
- 📄 [2026-08-05 (수) 일일 보고서](file:///c:/Users/PC/Projects/TP2/doc/reports/2026-08/2026-08-05_일일보고서.md)
- 📄 [2026-08-03 (월) 일일 보고서](file:///c:/Users/PC/Projects/TP2/doc/reports/2026-08/2026-08-03_일일보고서.md)

---

## ⚙️ 보고서 및 관리 운용 안내
- **자동 생성 스케줄**:
  - 평일 마감 보고서: 평일(월~금) 23:00 실행 (`0 23 * * 1-5`)
  - 임시 파일 수명 주기 정리: 매일 자정 00:00 실행 (`0 0 * * *`)
- **대화 동기화 Heartbeat**: AGENTS.md Section 5에 따른 GAS Web App JSON POST 파이프라인 (증분 추가 `mode: "append"`)
- **Git 연동**: 작성 완료 후 CI 프로그래머(`fa66e474-bbcb-4821-bd2f-54dec4f9b6b2`)를 통한 Git 커밋 및 관리
