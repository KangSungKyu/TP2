# 📄 메트로배니아 2D 벽점프(Wall Jump) 메카닉 기술 사양서

## 📌 1. 개요 (Overview)
본 사양서는 `TP2` 프로젝트의 메트로배니아 2D 액션 환경에서 적용되는 벽점프(Wall Jump) 조작 메카닉 및 지형 속성 판정 시스템의 기술적 명세를 정의합니다.

---

## ⚙️ 2. 핵심 메카닉 명세 (Core Mechanics)

### 2.1 벽 접촉 및 감지 판정 (`WallJumpSurface`)
- **지형 속성 지정**: 벽점프가 가능한 지형/벽면에 `WallJumpSurface` 컴포넌트 또는 전용 레이어/태그 부여
- **예외 처리 (Edge Filtering)**: 1-Way 발판 측면 또는 단차 경계면의 무분별한 벽점프 방지를 위한 측면 에지 필터링 적용 (`24ce082`)

### 2.2 벽 슬라이딩 & 반동 가속 (Trajectory & Force)
- **반동 입력 벡터**: 벽 반대 방향 사각(대각선 ~45도) 반동 벡터 가속 적용
- **좌우 이동 잠금 (Lockout)**: 벽점프 발사 직후 **0.18초 동안 수평 조작 입력 잠금**을 적용하여 자연스러운 반동 궤적 보장

### 2.3 캔슬 연계 (Dodge-Cancel)
- **회피(Dodge) 연계**: 벽점프 궤적 진행 도중 회피(Space/Shift) 입력 시 잠금을 캔슬하고 즉시 공중 회피 동작으로 전이 가능

---

## 🛠️ 3. 연관 클래스 및 구현 파일
- `Assets/Scripts/Gameplay/KinematicMotor2D.cs`: 이동 및 벽점프 궤적 갱신 (`FixedUpdate` 동기화)
- `Assets/Scripts/Gameplay/Player.cs`: 벽점프 입력 검증 및 캔슬 큐 파이프라인
- `Assets/Scripts/Gameplay/WallJumpSurface.cs`: 벽점프 전용 감지 컴포넌트
