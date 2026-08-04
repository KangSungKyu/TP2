# 데이터 파이프라인 서브 명세서 (CSV 파서 및 컨버터)

## 개요
- 목표: CSV 파싱 중 오염된 데이터(비수치, 누락, 포맷 오류)가 주입되더라도 런타임 크래시를 방지하고, 안전한 디폴트 값을 반환하며 최대한 많은 레코드를 보존한다.

## 핵심 인터페이스 (함수 시그니처)
- void LoadData(string csvText)  // IDataLoad 구현부
- int GetDataCount()
- void Release()
- object ConvertFromString(string text, IReaderRow row, MemberMapData memberMapData)  // CsvHelper TypeConverter

## 현재 발견된 취약점
- FloatArrayConverter / IntArrayConverter / UIntArrayConverter는 Parse 계열을 직접 호출하여 포맷 오류 시 예외를 throw함. 이는 CSV파일 한 줄의 오류가 전체 파싱을 중단시키는 원인이 될 수 있음.

## 방어적 제약 및 규칙
- 파서 구현 규칙:
  1) 모든 숫자 변환은 TryParse 계열로 처리한다.
  2) 변환 실패 시 해당 배열 원소는 아래 우선순위에 따라 대체값을 사용한다:
	 - 숫자 배열: 0 (uint의 경우 0u)
	 - float 배열: 0.0f
  3) 전체 레코드 파싱 실패(헤더/기본 필드 추출 불가)는 해당 레코드를 스킵하고 경고 로그를 남김
  4) 추출된 첫 행 idx가 0 또는 유효한 DataTableType으로 매핑되지 않으면 해당 파일은 로깅 후 스킵. 프로세스 전체 중단 금지.

## 예외/오류 처리 행동강령
- CSV 파서 내 예외 발생 시:
  - 에러 레벨: 파싱 중 개별 레코드 오류 -> Debug.LogWarning + 레코드 스킵 또는 보정
  - 에러 레벨: 헤더/첫 idx 오류 -> Debug.LogError + 파일 스킵
  - 절대 금지: CSV 파싱 예외가 앱 전체를 크래시시키거나 DataTableManager의 로딩 플로우를 막아서는 안 됨

## 테스트 및 검증
- 제공되는 Editor/Tests/CSVDataPipelineTests.cs와 유사한 단위 테스트를 작성하여 다음 케이스 커버:
  - 정상 포맷 CSV
  - 숫자 필드에 비숫자 혼입
  - 빈 셀
  - 첫 데이터 idx 누락
