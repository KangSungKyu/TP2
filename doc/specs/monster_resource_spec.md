# 🎨 1스테이지 일반 몬스터 3종 리소스 & 프레임 규격서

## 📌 1. 개요 (Overview)
본 규격서는 `TP2` 프로젝트 1스테이지에 배치되는 일반 몬스터 3종(`SpearSentry`, `ShadowStalker`, `WaveHeavy`)의 아트 스프라이트 슬라이싱, 프레임 해상도 및 PPU(Pixels Per Unit) 세팅 표준을 정의합니다.

---

## 📐 2. 몬스터 3종 프레임 규격 (Frame Specifications)

### 2.1 표준 텍스처 규격
- **프레임 해상도**: **`128 x 256 px`** (가로 128px, 세로 256px) (`4fa7e60`)
- **PPU (Pixels Per Unit)**: **`64`**
- **월드 스케일**: `2.0m` 월드 유닛 비율 적용
- **슬라이싱 방식**: Grid By Cell Count (`128x256`)

### 2.2 몬스터 라인업 (Monster Lineup)
| 몬스터 ID | 이름 (Name) | 역할 (Role) | 프레임 규격 | PPU |
| :--- | :--- | :--- | :--- | :--- |
| `SpearSentry` | 창병 가드 | 근거리 돌진 및 창 공격 | 128x256 px | 64 |
| `ShadowStalker` | 그림자 추적자 | 암습 & 은신 백스텝 공격 | 128x256 px | 64 |
| `WaveHeavy` | 중갑 파동병 | 중갑 방어 및 파동 충격파 | 128x256 px | 64 |

---

## 🛠️ 3. 연관 시스템
- `Assets/Scripts/Gameplay/MonsterOverheadHUD.cs`: 몬스터 머리 위 HP / Posture UI 오버레이 매니저 (`c992598`)
- `Assets/Scripts/Manager/EffectPoolManager.cs`: 몬스터 피격 및 이펙트 최적화 풀링 (`4fa7e60`)
- `Assets/Scripts/Scene/UnitSpawner.cs`: 1스테이지 룸별 스폰 마커 배치 연동 (`4eb0569`)
