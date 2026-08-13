# 🖼️ 2D 프레임 스프라이트 시트(Sprite Sheet) 전용 AI 생성 도구 리서치 & 프로젝트 데이터 보정 보고서

본 보고서는 프로젝트 내 실제 데이터 파이프라인(`UnitBaseData.csv`, `ResourceData.csv`)의 **공식 유닛/보스 명세**를 정밀 반영하고, **2D 프레임 스프라이트 시트(Frame-by-Frame Sprite Sheet)를 AI로 직접 생성할 수 있는 전용 도구 및 제작 파이프라인**을 리서치하여 제시합니다.

---

## 1. 📌 프로젝트 공식 유닛 & 보스 데이터 검증 (`UnitBaseData.csv` 기준)

이전 보고서의 임의 명칭을 배제하고, 프로젝트 공식 CSV 데이터 테이블에 등록된 실제 유닛 식별자를 정밀 반영합니다.

| 유닛 ID (`idx`) | 공식 데이터 명칭 | 역할 및 타입 | 현재 보유 컨트롤러 |
| :--- | :--- | :--- | :--- |
| **`3001`** | **`Unit_3001`** | 플레이어 메인 캐릭터 | `PlayerAnimatorController` (`1010`) |
| **`3101`** | **`SpearSentry`** | 근거리 창병 (찌르기/방어) | `SpearSentryAnimatorController` (`1012`) |
| **`3102`** | **`ShadowStalker`** | 암습·추적형 (순간이동/쌍검) | `ShadowStalkerAnimatorController` (`1013`) |
| **`3103`** | **`WaveHeavy`** | 중갑 충격파형 (방패/지진) | `WaveHeavyAnimatorController` (`1014`) |
| **`3104`** | **`ShieldSentinel`** | 중갑 방패 파수꾼 | `ShieldSentinelAnimatorController` (`1015`) |
| **`3105`** | **`OrbitalMarksman`** | 원거리 원형 사격수 | `OrbitalMarksmanAnimatorController` (`1016`) |
| **`3201`** | **`Garon` (가론)** | **스테이지 1 메인 보스 유닛** | `GaronAnimatorController` (`1011`) |

---

## 🖼️ 2. 2D 프레임 스프라이트 시트(Sprite Sheet) 생성 전용 AI 도구 BEST 4

Spine 뼈대 컷팅 방식의 복잡함을 배제하고, **2D 픽셀 애니메이션 프레임 스트립(Sprite Sheet PNG)을 직접 생성**해 주는 AI 도구 분석입니다.

### 1) 🎨 `RetroDiffusion` (Aseprite AI 플러그인 - **1순위 강추!**)
- **개요**: 업계 표준 픽셀 아트 편집기 **Aseprite**에 직접 탑재되는 2D 픽셀 전용 AI 엔진.
- **핵심 기능**:
  - **Animation Generator**: 베이스 키프레임 1장만 넣으면 Idle, Walk, Attack, Hit 애니메이션 프레임(Inbetween)을 Aseprite 타임라인에 자동 생성.
  - **Palette Lock & Pixel Perfect**: 픽셀 이중화(Doubles) 방지 및 지정한 컬러 팔레트(Color Palette) 강제 구속 기능 지원.
- **장점**: AI가 생성한 프레임을 Aseprite 내에서 **즉시 픽셀 단위 수정(Pixel Cleaning) 후 `.png` 스프라이트 시트로 1초 만에 내보내기** 가능.

### 2) 🕹️ `PixelLab / PixelVibe AI` (픽셀 2D 전문 AI)
- **개요**: 2D 사이드뷰/이소메트릭 픽셀 아트 및 스프라이트 전용 AI 플랫폼.
- **핵심 기능**:
  - **Sprite Sheet Mode**: 8~16 프레임 연속 동작 스프라이트 시트 격자(Grid) 생성.
  - **Transparent Background**: 투명 알파 배경 PNG 자동 생성.
- **장점**: PPU (32/64) 및 Cell Size(128x128) 제어가 용이하여 유니티 Sprite Editor Slicing에 최적화됨.

### 3) 🎞️ `SpriteDiffusion / SpriteAI` (Stable Diffusion 2D 전용)
- **개요**: ControlNet(포즈 제어) 기반 2D 게임 프레임 스프라이트 전용 AI 생성기.
- **핵심 기능**:
  - OpenPose / LineArt 포즈 맵을 통해 캐릭터 형태 붕괴 없이 프레임 간 포즈를 일정하게 유지하며 8~12프레임 동작 생성.
- **장점**: 공격 연타, 검기 궤적 등 복잡한 액션 프레임 생성에 탁월함.

### 4) 🎭 `Scenario.ai` (프로젝트 전용 LoRA 파인튜닝 AI)
- **개요**: 커스텀 게임용 AI 아티스트 플랫폼.
- **핵심 기능**:
  - `SpearSentry`, `Garon` 등 프로젝트 픽셀 스프라이트 이미지 10~20장을 학습(LoRA)시켜 일관된 화풍으로 신규 몬스터 프레임 생성.

---

## ⚙️ 3. 실무 AI 스프라이트 시트 제작 & 유니티 연동 파이프라인 (4-Step Workflow)

1. **Step 1: 베이스 키프레임 생성 (`Scenario` / `PixelLab`)**
   - `128x128 px` 투명 배경에 `SpearSentry` 기본 Idle 스탠스 이미지 생성.
2. **Step 2: 애니메이션 프레임 스트립 생성 (`RetroDiffusion` in Aseprite)**
   - Aseprite에서 RetroDiffusion을 구동하여 Idle(4f), Walk(6f), Attack(8f), Death(8f) 애니메이션 프레임 자동 생성.
3. **Step 3: 픽셀 노이즈 정돈 & 팔레트 동기화 (Aseprite)**
   - AI 노이즈(외곽선 뭉개짐)를 픽셀 단위로 정돈하고 프로젝트 표준 팔레트와 동기화 후 `SpearSentry_SpriteSheet.png` 저장.
4. **Step 4: 유니티 에셋 자동 임포트 & 슬라이싱**
   - `Assets/Textures/Characters/Monsters/SpearSentry/`에 배치.
   - Sprite Mode: `Multiple`, Cell Size: `128x128`, PPU: `64` 슬라이싱 후 `AnimatorController`에 바인딩.

---

## 💰 4. 프레임 스프라이트 시트 + AI 파이프라인 적용 시 현실적 견적

- **AI 도구 구독비**: RetroDiffusion + Scenario.ai (월 약 5~8만원)
- **인건비/공수**: AI 프레임 생성 후 Aseprite 픽셀 정돈(Cleaning) 작업 (프레임당 약 8,000원 ~ 12,000원 수공수 소요)
- **비용 비교**:
  - 100% 수작업 드로잉 외주: 프레임당 **35,000원**
  - **AI Sprite Sheet + Aseprite 정돈 파이프라인**: 프레임당 **약 10,000원** (**약 70% 예산 절감!**)
