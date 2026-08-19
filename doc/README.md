# TP2 문서 인덱스

`doc/`는 프로젝트 작업 문서의 단일 기준 루트다. 새 프로젝트 문서는 `Assets/Docs/`가 아니라 이 디렉터리 아래에만 생성한다.

- [마스터 구현 계획](implementation_plan.md)
- [서브플랜](SubPlans/)
- [기획·기술 명세](specs/)
- [QA 보고서](QA/)
- [일일·주간 보고서](reports/)
- [맵 생성 평가](MapGeneration/)
- [레거시 R-Action 문서](Legacy/)

## 관리 규칙

- 현재 동작 계약은 `specs/`, 정량 제약과 인터페이스는 `SubPlans/`에 기록한다.
- 테스트 결과는 `QA/`, 작업 요약은 `reports/`에 기록한다.
- 동일 내용을 복사하지 않고 기존 문서로 상대 링크한다.
- 생성된 OpenWiki 페이지가 도입되면 직접 수정하지 않고 원본 문서를 갱신한다.
