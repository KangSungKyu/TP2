# 📊 Antigravity & Codex 작업자 세션별 대화 내역 및 공정 이력 기록서

본 문서는 프로젝트 Antigravity & Codex 내 각 작업자 세션(Conversation ID)별 수행 임무, 핵심 대화 내역, 주요 커밋 및 무결성 검수 이력을 시트/섹션별로 정리한 공식 이력서입니다.

---

## 👑 1. 프로젝트매니저 (PM)
- **Primary Conversation ID**: `d4f1e2da-f7e5-4e86-b715-9979775531c1`
- **Secondary Conversation ID**: `019fd0ab-4bf2-7140-882e-bbba088431a0`
- **전담 역할**: 요구사항 분석, 공정 통제, 서브플랜 및 implementation_plan.md 동기화, 각 전문 작업자 대화방으로 작업 위임(`send_message`).
- **주요 대화 및 처리 이력**:
  1. 12x12m 전면 확대 모듈 및 가변 NxM 룸 청크 명세 수립 및 파서 설계.
  2. 서브에이전트 R&R 헌법(AGENTS.md) 준수 프로토콜 확립 (PM 직접 소스 작성 금지, 전문 대화방 100% 위임).
  3. 1-Way 발판 착지 Y 좌표(`hit.point.y`) 및 EntryMarker 스폰 고도(`surface + 0.51m`) 보정 지칙 전달.
  4. 프로젝트 문서 단일 기준 루트 `/doc/` 통합 이관 지시.

---

## 💻 2. 메인프로그래머 (Main Programmer)
- **Primary Conversation ID**: `bbabc4a9-bfbf-441a-8dc2-3a2746748ce1`
- **Secondary Conversation ID**: `019fcfdf-4b01-7061-b54a-e03147c3f4b5`
- **전담 역할**: 코어 게임플레이 C# 스크립트 작성/수정, 운동학 모터(`KinematicMotor2D.cs`) 물리 연동, 1-Way 발판 하향 이동 및 충돌 버그 수선.
- **주요 대화 및 처리 이력**:
  1. `KinematicMotor2D.cs` 커스텀 물리 운동학 모터 2-pass 스텝 구현.
  2. `PlatformEffector2D` 1-Way 상향/하향 통과 수선 및 `physicsCollider.bounds.min.y >= platformTopY - 0.15f` 직하단 착지 조건 교정 (`f0bdc65`).
  3. Modern Unity API (`FindFirstObjectByType`, `FindObjectsByType`, `bodyType`) 적용.

---

## 📜 3. 게임플레이 기획자 (Gameplay Designer)
- **Primary Conversation ID**: `96f4c1ce-8240-4eeb-9c13-a83668c9574a`
- **Secondary Conversation ID**: `019fd066-3701-7c40-962e-6e58e2462f1d`
- **전담 역할**: 레벨 디자인, 룸 시퀀싱, 함정/장애물 밸런스 수치 설계, 정수형 `idx` 데이터 구조 매핑.
- **주요 대화 및 처리 이력**:
  1. 12x12m 독립 자율 플레이 가능 20종 모듈 템플릿(Category A~L) 패턴 설계.
  2. 가시 함정(`SpikeTrap`), 둥근 톱날 함정(`SawBladeTrap`) 배치 수치 및 피로도 조절 지칙 수립.
  3. 가변 NxM ($3 \le N, M \le 20$) 룸 청크 세트 스펙 설계.

---

## 📦 4. 시니어 리소스 작업자 1 (Resource Worker 1)
- **Primary Conversation ID**: `f4f6cc90-75c3-4e62-890c-fcd62e9a47f7`
- **Secondary Conversation ID**: `019fd06a-df4c-7f60-8332-2ebf5b54f407`
- **전담 역할**: 프리팹(Prefab) 생성 및 수선, 타일/스프라이트 데이터 가공, **CSV 데이터테이블 파일 생성/가공**, `unityMCP` 구동 및 Addressables 로컬 배포.
- **주요 대화 및 처리 이력**:
  1. 12x12m 모듈 Prefab (44종) 및 Stage 1 가변 NxM 룸 청크 Prefab 11종 디스크 생성 및 재빌드.
  2. `Tilemap_Platforms` OneWayPlatform 레이어 및 `EntryMarker` +0.51m 오프셋 적용 에셋 재생성.
  3. `AddressablePipeline.BuildAndDeploy()` 구동으로 `TP2LocalServer\ServerData` 번들 동기화 배포 완결.

---

## 🔬 5. QA 오토메이션 러너 (QA Programmer)
- **Primary Conversation ID**: `e1bb1d94-16c8-478e-a32e-c818177dac17`
- **Secondary Conversation ID**: `019fd0b1-e1e4-7130-983f-677d00d2350e`
- **전담 역할**: `QATestRunner.cs` 기반 NUnit 단위/통합 자동화 테스트 슈트 실행, 에디터 무결성 검수, QA 보고서 작성.
- **주요 대화 및 처리 이력**:
  1. NUnit 80개 테스트 케이스 전원 통과 (**80/80 100% PASS**) 검수 완결.
  2. 포탈 소켓 44/44 도달성, 1-Way 발판 착지 geometry 및 2m 헤드룸 무결성 검증.
  3. `/doc/QA/qa_test_report.md` 동기화 갱신.

---

## 🛰️ 6. CI 프로그래머 (CI Programmer)
- **Primary Conversation ID**: `fa66e474-bbcb-4821-bd2f-54dec4f9b6b2`
- **Secondary Conversation ID**: `019fcfe2-04d9-7653-adef-374d45861feb`
- **전담 역할**: Git 커밋, 브랜치 병합, 원격 Push(`git push -u origin portfolio`), 파이프라인 무결성 관리.
- **주요 대화 및 처리 이력**:
  1. `portfolio` 브랜치 원격 Push 최신화 및 충돌 방지 클린업.
  2. 문서/에셋/코드 변경 이력 트래킹 및 파이프라인 안전성 확보.

---

## 📝 7. 문서작업자 (Document Worker)
- **Primary Conversation ID**: `be7fc5bc-582d-4699-b1b5-1ea26ef6e305`
- **Secondary Conversation ID**: `019fee44-7f00-7e81-81a0-f0557e75ef8b`
- **전담 역할**: 프로젝트 문서 작성, 일일/주간 보고서 관리, `/doc/` 통합 이관 관리.
- **주요 대화 및 처리 이력**:
  1. `Assets/Docs/` 산출물 전면 프로젝트 루트 `/doc/` 디렉토리 통합 이관 (`13e4612`).
  2. 일일/주간 보고서 및 기술 사양서 유지 보수.

---

## 🎨 8. 비주얼 아트 디자이너 (Visual Art Designer)
- **Primary Conversation ID**: `63c3f691-87f9-4235-a4f6-b78479705ddb` / `0625e4e7-91c3-4cca-bf23-c0844cae8319`
- **Secondary Conversation ID**: `019fd09b-0439-7133-8d4e-3153a9044a83`
- **전담 역할**: 128x128px 2D Side-View 픽셀 아트 콘셉트, 모션 스프라이트 시트 생성, 알파 투명 PNG 검수.
- **주요 대화 및 처리 이력**:
  1. 더미 가시 함정(`Sprite_SpikeTrap.png`), 톱날 함정(`Sprite_SawBladeTrap.png`) 32x32 알파 투명 스프라이트 가공.
  2. 지형/발판 스프라이트 텍스처 PPU=32 규격검수.
