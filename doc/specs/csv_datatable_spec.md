# 📄 9종 CSV 데이터 테이블 & 파서 사양서

## 📌 1. 개요 (Overview)
본 사양서는 `TP2` 프로젝트의 `Assets/Datas/` 내 9종 CSV 데이터 테이블, `int idx` PK/FK 표준화 규격 및 Addressables 배포 라벨링 정책을 정의합니다.

---

## ⚙️ 2. 데이터 테이블 규격 & 표준 교정 (CSV Standards)

### 2.1 PK / FK 컬럼명 표준화 (`idx`)
- **PK 컬럼 규칙**: 모든 데이터 테이블의 Primary Key 컬럼명은 예외 없이 **`idx` (소문자)**로 통일
- **교정 내역**: `SkillData.csv` 내 기존 `skillid` 컬럼 ➔ **`idx`**로 100% 교정 (`1a39376`)
- **헤더 규격**: 전체 9종 CSV 파일 헤더 전수 소문자(lowercase) 검증 완료

### 2.2 StageData.csv 정수화 및 Addressables 라벨링
- **`themetype` 정수 변환**:
  - `9001` (1장 TaoShrine): `themetype = 1`
  - `9002` (2장 CyberRuins): `themetype = 2`
- **룸 연계 인덱스**: `startroomidx` (`1040`), `bossroomidx` (`1042`), `roomsequenceidxlist` (`1040_1041_1042`)
- **Addressables Label**: `Datas` 라벨 자동 등록 보장 (`ac90b50`)

---

## 📊 3. 9종 CSV 테이블 목록
1. `StageData.csv`: 스테이지 / 룸 연계 데이터
2. `SkillData.csv`: 유닛/플레이어 스킬 수치 데이터 (PK: `idx`)
3. `ResourceData.csv`: 에셋 주소 1:1 무결성 매핑 데이터
4. `MonsterDataTable.csv`: 몬스터 능력치 및 스폰 데이터
5. `EffectDataTable.csv`: VFX 이펙트 파라미터 데이터
6. `ItemData.csv`, `StatData.csv`, `SoundData.csv`, `DialogueData.csv`

---

## ✅ 4. 검증 결과 (QA Test Suite)
- **CSV 데이터 파서 검증**: 27/27 테스트 항목 100% PASS
- **StageData 룸 런타임 로딩**: 28/28 QA PASS 완료
