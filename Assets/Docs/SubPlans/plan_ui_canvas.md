# UI 자동화 및 캔버스 아키텍처 명세서

## 개요
- 목표: Canvas 계층 및 Panel(창) 생명주기, 우선순위(레이어) 관리, 비동기 애니메이션(Show/Hide) 및 리소스 기반 UI 프리팹 로드를 중앙화하여 중복 코드를 제거하고 씬 전환/풀링에 안전하도록 설계한다.

## 핵심 인터페이스 (함수 시그니처)
- void RegisterPanel<T>(T panel) where T : PanelBase
- UniTask ShowPanelAsync(string panelId, CancellationToken cancellationToken = default)
- UniTask HidePanelAsync(string panelId, CancellationToken cancellationToken = default)
- T GetPanel<T>(string panelId) where T : PanelBase
- void SetCanvasSortingOrder(int baseOrder)

## 제약(Constraints)
- 캔버스 스케일 모드: CanvasScaler는 Scale With Screen Size로 설정, Reference Resolution 고정(예: 1920x1080). 모든 UI 레이아웃은 Anchors 기반으로 작성되어야 함.
- Show/Hide 애니메이션은 UniTask 비동기 체인으로 구현하고, 애니메이션 완료 실패(애니메이션 파라미터 없음 또는 오류) 시에는 즉시 상태를 강제 완료(즉시 Visible/Hidden)로 변경하여 블로킹을 방지한다.
- UI Prefab 로드는 ResourceManager를 통해 Addressables 키로만 수행. 문자열 하드코딩 Key 사용 금지: 반드시 ResourceData.idx 참조를 통과해 Path를 취득한다.
- Modal(모달) 패널은 블록 입력 영역을 명시적으로 소유하며, 모달 스택은 LIFO로 관리한다. 모달 오버랩 시 Input 인터럽트는 최상위 모달만 허용.

## 방어적 규칙
- PanelManager는 씬 전환 시 DontDestroyOnLoad Canvas 인스턴스를 유지하고, Scene 변경 시 자동으로 Panel을 Rebind(필요시 재초기화)한다.
- PanelBase.OnShowAsync/OnHideAsync는 CancellationToken을 수용해야 하며, 호출자가 토큰을 취소하면 반드시 즉시 정리(visual state reset)되어야 한다.
- Panel 풀링 사용 시 ResetState() 인터페이스를 구현하여 이전 상태 잔존을 방지한다.
