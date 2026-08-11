# 📐 12x12m 전면 확대 모듈 & 11종 룸 청크 아키텍처 사양서

## 📌 1. 개요 (Overview)
본 사양서는 `TP2` 프로젝트의 1스테이지 룸 생성을 위해 구축된 **12m x 12m (12x12 cells) 전면 확대 모듈 44종** 및 **11종 Stage 1 룸 청크(Room Chunk) Prefab**의 그리드 규격, 통로 통과 폭(3-4m) 및 BFS 위상 검증 아키텍처를 정의합니다.

---

## 🏗️ 2. 12x12m 모듈 규격 & 통과 폭 보장 (12x12m Module Specs)

### 2.1 독립 주행 모듈 규격 (12m x 12m)
- **모듈 규격**: **`12m x 12m` (12x12 cells)** 독립 주행/점프 완결 모듈 (총 44종)
- **발판-지형 분리**: 1-Way 발판(`Tilemap_Platforms`)과 지형 타일(`Tilemap_Ground`) 물리 레이어 완벽 분리 (`1c0e898`)
- **통로 통과 폭 (Passage Clearance)**: 모듈 경계 및 룸 청크 연결부의 통로 통과 폭을 **`3 ~ 4m`**로 확장하여 플레이어 끼임 현상 근본 차단 (`1c0e898`)

### 2.2 룸 청크 규격 & 11종 라인업 (11 Room Chunk Prefabs)
- **`Prefab_1040` (Entry Room)**: 36m x 24m
- **`Prefab_1041` (Battle Room)**: 48m x 24m
- **`Prefab_1042` (Boss Room)**: 60m x 36m
- 외 8종의 전용 룸 청크 Prefab (NxM 가변 그리드 규격 적용: `3 <= N, M <= 20`) (`2b17588`)

---

## 🗺️ 3. 위상 검증 & 렌더링 파이프라인 (Topological Path & Rendering)

### 3.1 BFS 위상 경로 연결 보장 (BFS Topological Validation)
- 룸 청크 내 모듈 간 동서남북(West, East, North, South) 관문 진출입 경로의 100% 이동 가능성을 BFS 위상 검증 로직으로 자동 보증 (`8634eaa`)

### 3.2 unityMCP 자동 생성 & 배포
- 에디터 파이프라인(`ModuleChunkBuilder.cs`) 및 `unityMCP`를 통한 44종 모듈 및 11종 룸 청크 Prefab 0-Error 자동 빌드 & Addressables 등록 (`ebae731`, `c112384`)
