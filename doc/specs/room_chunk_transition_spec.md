# 룸 청크 전환 명세

## 1. 데이터 기준

- 스테이지와 룸은 `StageData.idx`, `ResourceData.idx` 정수 식별자로만 연결한다.
- 1스테이지(`9001`) 룸 순서는 Entry `1040` -> Battle `1041` -> Boss `1042`다.
- `RoomDoorPortal.TargetRoomResourceIdx`는 목적지 `ResourceData.idx`다.
- 룸 에셋은 `StageManager`가 `ResourceDataTable`로 경로를 해석하고 `TilemapStageBuilder`가 `ResourceManager`에 로딩을 위임한다.
- 데이터가 없거나 목적지 idx가 0이면 전환을 시작하지 않고 오류를 기록한다.

## 2. 포탈과 문

- `Portal`, `Door`, `Portal_Gate` 프리팹은 `Collider2D.isTrigger = true`와 식별 가능한 `SpriteRenderer.sprite`를 가진다.
- 플레이어 본체 또는 플레이어의 자식 콜라이더가 트리거에 들어오면 자동 전환한다.
- `RoomDoorPortal`의 전환 잠금은 중복 트리거를 무시하고 성공·실패와 관계없이 `finally`에서 해제한다.
- Entry 관문은 `1041`, Battle 관문은 `1042`, Boss 관문은 `1040`을 대상으로 한다.

## 3. 전환 완료 순서

다음 작업은 검은 화면 상태에서 완료되어야 하며, 마지막에만 Fade-In한다.

1. 입력된 목적지 idx 검증 및 현재 룸 시퀀스 인덱스 갱신
2. 이전 청크, 활성 몬스터, 활성 이펙트 정리
3. 목적지 룸 로드·인스턴스화 및 등록
4. 마커 기반 플레이어 재배치와 몬스터·보스 스폰
5. 카메라 타겟·바운드 갱신 후 `SnapToTarget()`
6. 저프레임에서도 최소 전환 버퍼를 기다린 뒤 Fade-In 완료

- 플레이어는 `Player.Instance`를 재사용하므로 룸 이동마다 중복 생성하지 않는다.
- 카메라 이동은 Fade-In 전에 스냅되어 이전 위치에서 새 위치로 이동하는 과정이 노출되지 않아야 한다.
- 전환 중 재호출은 `StageManager`와 `RoomDoorPortal`의 잠금으로 무시한다.

## 4. 관련 구현 및 검증

- `Assets/Scripts/Gameplay/RoomDoorPortal.cs`
- `Assets/Scripts/Gameplay/MetroidvaniaCamera2D.cs`
- `Assets/Scripts/Manager/StageManager.cs`
- `Assets/Scripts/Scene/TilemapStageBuilder.cs`
- `Assets/Editor/Stage1ChunkBuilder.cs`
- `Assets/Editor/PortalPrefabBuilder.cs`
- `Assets/Editor/Tests/TilemapStageBuilderTests.cs`

최종 소급 점검: 2026-08-05
