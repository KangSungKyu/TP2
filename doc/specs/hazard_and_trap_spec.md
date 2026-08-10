# 📄 2D 함정 & 트랩(Hazard) 시스템 기술 사양서

## 📌 1. 개요 (Overview)
본 사양서는 `TP2` 프로젝트의 스테이지 룸 청크 내에 설치되는 2D 함정 및 트랩 객체(`SpikeTrap`, `SawBladeTrap`)의 물리 감지, CSV 데이터 매핑, 데미지 및 리스폰 메카닉을 정의합니다.

---

## ⚙️ 2. 함정 유형 및 메카닉 (Hazard Mechanics)

### 2.1 함정 종류 및 구조
- **가시 함정 (`SpikeTrap`)**:
  - 지형 및 발판 상단에 배치되는 접촉 데미지 함정
  - `sortingOrder = 15` 시각적 가시성 보장 및 PPU=32 1:1 트랩 콜라이더 정렬 (`bfaf12d`)
- **톱날 함정 (`SawBladeTrap`)**:
  - 이동 경로 및 공중 궤적에 설치되는 회전형 장애물
  - 지속적 접촉 판정 및 넉백(Knockback) 처리

### 2.2 피격 및 안전 리스폰 (Hazard Respawn)
- **데미지 판정**: 플레이어 접촉 시 지정 데미지 부여 및 피격 이펙트 재생
- **안전 리스폰 파이프라인**: 함정 피격 시 플레이어를 최근 통과한 안전 지점(Safe Entry Buffer)으로 복귀

---

## 🛠️ 3. 연관 클래스 & 데이터
- `Assets/Scripts/Gameplay/HazardBase.cs`: 함정 기본 추상 클래스
- `Assets/Scripts/Gameplay/SpikeTrap.cs`, `SawBladeTrap.cs`: 함정 구현체
- Modern Unity API 적용: `FindFirstObjectByType`, `RigidbodyType2D.Kinematic` (`cd73b69`)
