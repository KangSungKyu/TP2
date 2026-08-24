# Dummy Attack Effect 재작업 명세

- Status: `COMPLETED`
- Date: `2026-08-24`
- 기존 완료 증거: [`../2026-08-24_dummy_attack_effects/`](../2026-08-24_dummy_attack_effects/)
- 방향 참고: [`references/directional_slash_draw_erase.png`](references/directional_slash_draw_erase.png)

기존 완료본은 감사 증거로 보존하며 수정하거나 이동하지 않는다. 이번 패키지는 REWORK 5종과 REJECT 4종만 새로 제작한다.

## 방향성 생성·소거 계약

- 베기와 찌르기는 전체 도형의 일괄 scale/fade를 금지한다.
- 전반부에는 시작점에서 끝점 방향으로 선두가 진행하며 trail이 점차 그려진다.
- 후반부에는 시작점부터 같은 방향으로 꼬리가 선두를 따라가며 trail이 점차 소거된다.
- 권장 8프레임 점유율: `1=20%`, `2=40%`, `3=70%`, `4=100%/Active`, `5=앞 20% 소거`, `6=50% 소거`, `7=80% 소거`, `8=완전 투명`.
- 실제 `AttackCollider`와 sweep bounds가 판정 권위다. 시각 trail에는 Collider와 Damage가 모두 0이다.
- trail 선두가 실제 hitbox 선두보다 먼저 끝점에 도착하면 안 된다.
- 다단 공격은 hit window마다 독립 trail을 1회 재생하고, 이전 trail을 반환한 뒤 다음 trail을 재생한다.

## 방향 정의

| 동작 | 시작점 → 끝점 |
| :--- | :--- |
| `VerticalDown` | 머리 위 → 전방 지면 |
| `ReverseVerticalUpswing` | 후방 저점 → 머리 → 전방 상단 |
| Thrust/Line | 몸체 근처 → 전방 끝점 |
| Shockwave/Ring | 중심 → 외곽 생성, 같은 방향으로 소거 |

## 감사 판정과 작업 범위

| 판정 | 대상 |
| :--- | :--- |
| PASS 6 | `3101/P6001`, `3102/P6008`, `3102/P6009`, `3103/P6010`, `3104/P6003`, `3201/P6103` |
| REWORK 5 | `3001/S7001`, `3001/S7003`, `3104/P6004`, `3105/P6005`, `3201/P6101` |
| REJECT 4 | `3103/P6001`, `3105/P6006`, `3201/P6100`, `3201/P6102` |

- PASS 6종은 다시 그리지 않는다. 기존 완료본을 수정·이동·복제하지 않고 방향성·타이밍 회귀 검증만 기록한다.
- REWORK 5종은 기존 구도를 보존할 수 있으나 방향성 생성·소거 계약에 맞게 프레임을 다시 구성한다.
- REJECT 4종은 기존 시안을 기반으로 부분 수선하지 않고 아래 권위 동작에 맞춰 새로 제작한다.

## 동작 권위 정정

| 대상 | 권위 동작 |
| :--- | :--- |
| `3001/S7001` | `ReverseVerticalUpswing` |
| `3103/P6001` | `VerticalDown` |
| `3105/P6006` | lower aim `Line` |
| `3201/P6100` | OverheadSmash `Arc` |
| `3201/P6101` | Down→Upswing `Arc` |
| `3201/P6102` | Charge `DirectedBox` |
| `3201/P6103` | Shockwave `Ring` |

`3102/P6009`는 정확히 2-hit이다. 3연속 trail 또는 3펄스 제작을 금지한다.

## 납품 규격

- 투명 배경 RGBA PNG, 가로 8프레임 strip
- 일반: 셀 `128×128 px`, 시트 `1024×128 px`
- Boss: 셀 `256×256 px`, 시트 `2048×256 px`
- 각 셀 중앙 피벗 `(0.5, 0.5)`, Unity import 목표 PPU `100`
- Point 필터와 mipmap 없음 전제
- 각 셀 경계에 안전 여백을 두어 인접 프레임 침범과 외곽 잘림을 방지
- 재작업 파일명은 기존 대상 파일명을 유지하되 새 pending/completed 패키지 안에서만 생성

## 완료 조건

- REWORK 5종과 REJECT 4종 산출물 및 검증 완료
- 방향 진행·동일 방향 소거, frame 4 Active, frame 8 완전 투명 확인
- 다단 hit window 독립 trail 및 `6009` 2-hit 확인
- 실제 판정 선행 0, Collider/Damage 0
- 모든 신규 자산 SHA-256 재산출. 기존 manifest는 실제 파일과 `0/15` 일치였으므로 값을 복사하지 않는다.
- 모두 성공한 경우에만 spec/prompt/audit/references/assets를 `completed/2026-08-24_dummy_attack_effects_rework/`로 이동하고 `manifest.md`, `result.md`, `qa_notes.md`를 생성
- 부분 완료, BLOCKED, 검증 실패는 pending 유지. 이동 후 pending 원본 중복본 금지
- 기존 첫 완료본과 OpenWiki generated 파일은 이동 금지
