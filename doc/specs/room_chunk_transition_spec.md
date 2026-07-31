# 📐 룸 청크 연계(Room Chunk Transition) 및 챕터별 스테이지 진속 구조 설계 명세서

## 📌 1. 개요 (Overview)
본 명세서는 `TP2` 프로젝트의 본 스테이지 개발을 위해 도입된 **룸 청크 연계 시스템(Room Chunk Transition System)** 및 **챕터별 스테이지 연결/전환 아키텍처**의 기술 사양 및 데이터 구조를 정의합니다.

---

## 🏗️ 2. 룸 청크 연계 아키텍처 (Room Chunk Transition)

### 2.1 룸 연결 및 그리드 매핑 (Grid & Gate Mapping)
- **룸 그리드 좌표계**: 스테이지 내 룸(Room Chunk) 단위의 상대적 그리드 좌표 기반 배치
- **연결 게이트 (Transition Gate)**:
  - 룸 경계면에 배치되는 `RoomTransitionGate` (Left/Right/Top/Bottom)
  - 플레이어가 게이트 트리거 영역에 진입 시 다음 룸 이송 및 카메라 타겟 전환 수행

### 2.2 카메라 & 스크린 페이드 전환 (`MetroidvaniaCamera2D`)
- **카메라 바운드 (Camera Bounding Box)**: 현재 활성화된 룸의 타일맵 구역 내로 `MetroidvaniaCamera2D` 클램프 적용 (`a5877dd`)
- **화면 전환 연출**: 룸 전환 시 짧은 화면 페이드(Fade-In/Out) 및 유닛 조작 잠금 처리로 부드러운 화면 연결 제공

### 2.3 지형 및 유닛 스폰 보정
- **Auto-Hop & 지형 끼임 방지**: 룸 경계 및 단차 이동 시 지형 끼임 방지 Auto-Hop 오프셋 보정 (`963c026`)
- **중복 스폰 예외 방지**: 룸 재진입 시 유닛 및 중복 매니저 생성 방지 로직 적용 (`a5877dd`)

---

## 🗺️ 3. 챕터별 스테이지 구조 설계 (Chapter Progression)

```text
[Chapter 1: 성벽 외곽]
 └── Room_C1_01 (스폰/튜토리얼) ➡️ Room_C1_02 (벽점프 퍼즐) ➡️ Room_C1_03 (보스 아레나: 가론)

[Chapter 2: 지하 과수원 / 하수도]
 └── Room_C2_01 (발판 이동) ➡️ Room_C2_02 (트랩 멀티존) ➡️ Room_C2_03 (중간 보스)
```

---

## 🛠️ 4. 관련 코드 및 툴 파이프라인
- `Assets/Scripts/Scene/MetroidvaniaCamera2D.cs`: 룸 카메라 바운딩 및 추적
- `Assets/Scripts/Scene/TilemapStageBuilder.cs`: 룸 청크 배치 및 동적 바인딩
- `Assets/Editor/TilemapRoomPrefabBuilder.cs`: 룸 프리팹 생성 에디터 툴
