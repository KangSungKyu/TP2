# CSV 데이터 테이블 및 정수 idx 명세

## 1. 식별 규칙

- 모든 테이블의 PK 컬럼은 소문자 `idx`다.
- 테이블 간 런타임 참조는 문자열 키가 아닌 `uint idx` PK/FK로 수행한다.
- 리소스 주소는 공용 `ResourceData`의 정수 `idx`를 통해서만 해석한다.
- 파싱 실패, 중복 idx, 누락 FK, 빈 리소스 경로는 해당 레코드를 사용할 수 없는 데이터로 처리하고 오류를 기록한다.
- 매니저와 유닛은 Addressables API를 직접 호출하지 않고 `ResourceManager`에 로딩·인스턴스화를 위임한다.

## 2. 주요 연결

- `StageData.startroomidx`, `bossroomidx`, `roomsequenceidxlist` -> `ResourceData.idx`
- `UnitBaseData.prefabid`, `animatorid` -> `ResourceData.idx`
- `EffectData.prefabidx` -> `ResourceData.idx`
- `SpawnPointMarker.MonsterId` -> `UnitBaseData.idx` -> `ResourceData.idx`
- `SkillData.effectidx` -> `ResourceData.idx`
- `SkillData.nametextidx` -> `TextData.idx`

1스테이지(`9001`) 기본값:

- `themetype = 1`
- `startroomidx = 1040`
- `bossroomidx = 1042`
- `roomsequenceidxlist = 1040_1041_1042`

## 3. 테이블 및 스키마 명세

- `StageData.csv`: 스테이지 / 룸 연계 데이터
- `SkillData.csv`: 유닛/플레이어 스킬 및 공격 타이밍 데이터
  - **헤더 규격 (2026-08-20 개편)**: `idx,nametextidx,range,casttime,cooldownsec,mpcost,damagemultiplier,isbasicattack,hitcount,hittimings,hitwindowpre,hitwindowpost,effectidx,animstate`
  - `animationclip`, `activeduration` 제거
  - `hitwindowpre`, `hitwindowpost` 신규 추가 (정밀 히트 윈도우 판정)
- `ResourceData.csv`: 에셋 주소 1:1 무결성 매핑 데이터
- `UnitBaseData.csv`: 유닛 기본 스탯 및 프리팹/애니메이터 바인딩 데이터
- `MonsterBaseData.csv`: 몬스터 세부 스탯 및 패턴 연계 데이터
- `MonsterPatternData.csv`: 몬스터 AI 패턴 시퀀스 데이터
- `BossPatternData.csv`: 보스 페이즈별 특수 패턴 데이터
- `EffectData.csv`: VFX 이펙트 파라미터 데이터
- `TextData.csv`: 다국어 텍스트 데이터 (`idx,en,kr`)

실제 파일 추가·삭제 시 이 목록과 `DataTableManager` 등록을 함께 갱신한다.

## 4. 검증

- `Assets/Editor/Tests/CSVDataPipelineTests.cs`: 파싱, 정수 키, FK 및 스테이지 룸 연결 검증
- `Assets/Editor/Tests/TilemapStageBuilderTests.cs`: 룸 `ResourceData.idx`와 스폰 마커 검증
- 과거 PASS 숫자를 고정 기록하지 않는다. 최신 결과는 `Logs/qa_test_results.txt`를 기준으로 한다.

최종 소급 점검: 2026-08-20
