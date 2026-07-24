# 🌲 TP2 Project - Git & Branch Workflow Guidelines

본 문서는 `TP2` Unity 2D 프로젝트 개발 시 모든 대화 세션(Conversation) 및 개발자가 반드시 준수해야 하는 Git 브랜치 전략 및 작업 수칙입니다.

---

## 🏛️ 1. Branch 구조 및 역할

- **`portfolio`**:
  - 실제로 모든 Request-Merge가 일어나는 **실질적 메인 통합 브랜치**.
  - 개발 기능 통합, 아트 리소스 통합, QA 및 검증, CI 자동화 검증 대상입니다.
- **`main`**: 
  - 프로젝트의 **최종 검증 Base 브랜치**.
  - `portfolio`의 최신화 및 QA 검증이 완료되면 `main`으로 Merge/Request하여 `main` 역시 최신 동기화 상태를 유지합니다.
- **`art`**:
  - 아트 담당자 및 리소스 작업자 전용 **아트 리소스 전용 브랜치**.
  - 아트 리소스 수령 시 `art` 브랜치를 `fetch`/`pull` 하여 `portfolio`에 병합(Merge)합니다.
- **`feature/*`, `fix/*`, `refactor/*` (작업 브랜치)**:
  - 대규모 패치나 신규 기능 구현 시 파생 생성하는 작업 전용 브랜치입니다.

---

## 🎨 2. 아트 리소스 표준 사양 및 동기화 수칙

1. **아트 파이프라인 동기화**:
   - 아트 리소스 업데이트 수령 시 `art` 브랜치를 `fetch`/`pull` 하여 `portfolio` 브랜치에 병합(Merge).
   - `.png` + `.meta` 짝 파일 동기화 상태를 철저히 유지.

2. **유닛 리소스 표준 사양**:
   - **기본 시선**: 우측(East) 바라보기 기준
   - **해상도 캔버스**: 플레이어/보스 (128x128), 일반 몬스터 (64x64)

---

## 🚀 3. Branch 생성 및 Flow 수칙

1. **브랜치 흐름 (Branch Flow)**:
   - **`feature/*` / `fix/*` ➔ `portfolio`**: 메인 프로그래머 지시로 작업 브랜치 생성 ➔ 개발 및 QA 완료 후 `portfolio`로 Merge ➔ 작업 브랜치 삭제.
   - **`art` ➔ `portfolio`**: 아트 업데이트 수령 ➔ `art` 브랜치 최신화 ➔ `portfolio` 브랜치로 병합.
   - **`portfolio` ➔ `main`**: `portfolio` 브랜치의 최신화 및 검증이 완결되면 `main`으로 PR / Merge Request를 전달하여 `main` 브랜치를 최신화 동기화.

2. **브랜치 생성 주체**:
   - **브랜치 생성은 메인 프로그래머가 주로 지시**합니다.
   - 메인 프로그래머의 지시가 있을 때 브랜치를 생성합니다.

3. **브랜치 생성 보고 규칙**:
   - 브랜치를 생성했을 경우, 아래 템플릿에 따라 대화창에 보고합니다:
     ```text
     📢 [브랜치 생성 보고]
     - 생성 브랜치: feature/<기능명>
     - 기준 브랜치 (Base): portfolio (또는 현재 작업 브랜치)
     - 작업 목표: <작업 내용 요약>
     ```

---

## 🔄 4. Multi-Conversation (다중 세션) 협업 지침

- `pull`, `push` 요청은 **다른 Conversation 세션에서도 발생**할 수 있습니다.
- **작업 시작 전**: 반드시 `git fetch` 및 `git pull origin <작업브랜치>`를 수행하여 최신 원격 변경 사항을 반영합니다.
- **작업 완료 후**: 변경 사항을 Push하고 즉시 커밋 로그 및 상태를 명확히 남깁니다.
