# Dummy Attack Effects Rework V2 명세

- Status: `COMPLETED`
- Scope: 재작업 5종만
- 보존 대상: `doc/ai_orders/completed/2026-08-24_dummy_attack_effects/`, `doc/ai_orders/completed/2026-08-24_dummy_attack_effects_rework/`

기존 completed 두 패키지는 감사 증거다. 파일·문서·자산을 수정하거나 이동하지 않는다.

## 재작업 대상 5종

| 대상 | 기존 오류 | V2 계약 |
| :--- | :--- | :--- |
| `U3105 P6006 S7002` | 파일명/type `Ring` | lower aim `Line`; 파일명도 `_Line.png` |
| `U3201 P6100 S7012` | `DirectedBox` | OverheadSmash `Arc`; 파일명도 `_Arc.png` |
| `U3201 P6102 S7010` | `Arc` | Charge `DirectedBox`; 파일명도 `_DirectedBox.png` |
| `U3001 S7003` | 2-hit가 한 trail에 결합 | 각 hit별 독립 Draw 1–4 / Erase 5–8 |
| `U3201 P6101 S7011` | 2-hit가 한 trail에 결합 | 각 hit별 독립 Draw 1–4 / Erase 5–8 |

2-hit 자산은 hit별 8-frame strip 2개 또는 hit 순서가 명시된 16-frame 단일 strip으로 납품한다. 각 hit는 이전 trail 반환 후 다음 trail을 재생한다. 두 hit를 하나의 8-frame 진행에 겹치거나 압축하지 않는다.

## 재작업 제외 PASS 10

`U3001/S7001`, `U3101/P6001`, `U3102/P6008`, `U3102/P6009`, `U3103/P6001`, `U3103/P6010`, `U3104/P6003`, `U3104/P6004`, `U3105/P6005`, `U3201/P6103`.

PASS 10의 자산·파일명·문서·해시는 수정하지 않는다. `U3102/P6009`는 기존 계약대로 정확히 2-hit이며 3연속 제작을 금지한다.

## 공격 판정 공간 권위

- 실제 Active bounds는 후속 `EffectData`에 직렬화할 승인된 `center/size`를 사용한다.
- PNG alpha scan, `Renderer.bounds`, 문자열 lookup으로 판정 공간을 계산하지 않는다.
- 시각 trail은 Collider와 Damage가 모두 0이다.
- 프레임별 시각 크기와 무관하게 hit tick별 승인 bounds는 고정한다.
- 이번 발주는 시각 PNG만 제작하며 `EffectData`, idx, 코드, CSV, Prefab을 생성하거나 수정하지 않는다.

## 시각 자산 계약

- 방향성 trail은 시작점→끝점으로 Draw 1–4, 같은 방향으로 Erase 5–8
- trail 선두가 실제 승인 bounds보다 먼저 도착하지 않음
- RGBA, 중앙 피벗 `(0.5,0.5)`, PPU100 import target
- 일반 8-frame strip `1024×128`, Boss 8-frame strip `2048×256`
- 16-frame 단일 strip 선택 시 일반 `2048×128`, Boss `4096×256`
- 셀 경계 안전 여백 유지, 인접 프레임 침범·잘림 0

## 완료 프로토콜

1. 5종 재작업과 검증이 모두 성공한 경우에만 이 패키지를 `doc/ai_orders/completed/2026-08-24_dummy_attack_effects_rework_v2/`로 이동한다.
2. 완료 폴더에 `manifest.md`, `result.md`, `qa_notes.md`, `assets/`를 생성하고 신규 자산 SHA-256 또는 파일 크기를 직접 기록한다.
3. partial, BLOCKED, 검증 실패는 pending에 유지하고 Status만 `IN_PROGRESS` 또는 `BLOCKED`로 갱신한다.
4. 완료 이동 후 pending 중복본을 남기지 않는다.
5. 기존 completed 두 패키지, OpenWiki generated 파일, 다른 발주는 이동하지 않는다.
