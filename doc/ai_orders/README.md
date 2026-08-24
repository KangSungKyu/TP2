# 외부 AI 발주 관리

`doc/ai_orders/`는 외부 AI에 전달하는 비실행형 제작 발주와 검수 결과만 보관한다. 프로젝트 런타임 데이터, 코드, 에셋의 권위 저장소가 아니다.

## 상태와 디렉터리

- `pending/`: `PENDING`, `IN_PROGRESS`, `BLOCKED` 발주. 부분 완료·차단·검증 실패도 이 위치를 유지한다.
- `completed/YYYY-MM-DD_<slug>/`: 산출물 생성과 검증이 모두 성공한 발주만 이동한다.
- 파일명: `YYYY-MM-DD_<snake_case_slug>_<spec|prompt>.md`.

## 완료 프로토콜

1. pending 문서를 참고한 작업은 성공적으로 완료되고 산출물 검증까지 끝난 경우에만 해당 spec/prompt와 결과물을 `completed/YYYY-MM-DD_<slug>/`로 이동한다.
2. 완료 폴더에 `manifest.md`(파일 목록과 SHA-256 또는 파일 크기), `result.md`(수행 결과), `qa_notes.md`(검증 결과와 미해결 위험), `assets/`(납품 자산)를 둔다.
3. 부분 완료, 차단, 검증 실패는 pending에 유지하고 해당 문서의 `Status`만 `IN_PROGRESS` 또는 `BLOCKED`로 갱신한다.
4. 완료 이동 시 원본 pending 중복본을 남기지 않는다.
5. OpenWiki generated 파일과 다른 발주 파일은 이동하지 않는다.

## 결과물 체크리스트

- 발주 대상과 실제 파일 목록 일치
- PNG RGBA 알파, 프레임 수·셀 크기·피벗·PPU·필터 규격 확인
- 파일명과 유닛/Pattern/Skill 매핑 확인
- 시각 이펙트에 Collider·Damage·게임플레이 판정 없음 확인
- `manifest.md`, `result.md`, `qa_notes.md` 작성 및 미해결 위험 명시
- API 키, 토큰, 인증정보, 개인 경로, 비공개 원문 미포함

