# 더미 공격 이펙트 시안 제작 명세

- Status: `COMPLETED`
- Date: `2026-08-24`
- Scope: 7개 유닛의 비실행형 시각 자산 시안
- Runtime authority: 실제 Attack/Projectile hitbox

## 목적과 경계

외부 AI는 공격 가독성 비교용 PNG 시안만 제작한다. 프로젝트 파일 수정, 코드 생성, CSV 편집, Prefab/Animator/Addressables 구성, `idx` 배정은 범위 밖이다. 이펙트는 Collider와 Damage를 모두 갖지 않으며 실제 타격 범위·타이밍을 변경하지 않는다.

## 공용 도형

| 도형 | 용도 |
| :--- | :--- |
| `Line` | 찌르기, 조준선, 발사 방향 |
| `Arc` | 수평·수직 베기 궤적 |
| `DirectedBox` | 전진 타격, 방패 밀기, 몸통 돌진의 방향성 면적 |
| `Ring` | 충격파, 총구·재장전의 방사형 강조 |

## 유닛별 Pattern/Skill 매핑

| 유닛 | Pattern | Skill | 도형·펄스 | 시안 의도 |
| :--- | :--- | :--- | :--- | :--- |
| Player `3001` | 해당 없음 | `7001` / `7003` | `Arc` 1회 / 2회 | 짧고 얇은 검의 단타·연타 |
| Monster `3101` | `6001` / `6002` | `7001` / `7001` | `Line` / 없음 | 창 찌르기. `6002`는 이동 utility이므로 공격 이펙트 없음 |
| Monster `3102` | `6008` / `6009` | `7005` / `7006` | `Line` 또는 `DirectedBox` 1회 / `DirectedBox` 2회 | Charging Thrust / Barrage. `6009`는 현재 정확히 2-hit이며 3연속 시안 금지 |
| Monster `3103` | `6001`, `6002`, `6010` | `7001`, `7001`, `7007` | `Arc`, 없음, `DirectedBox` 1회 | heavy 수평베기 / utility / Torso Ram |
| Monster `3104` | `6003` / `6004` | `7001` / `7001` | `DirectedBox` / `Arc` | 방패·무기의 수직·대각 공격 구분 |
| Monster `3105` | `6005` / `6006` | `7002` / `7002` | `Line` + 작은 `Ring`, 각 1회 | Aim-Fire-Reload 가독성. Projectile 판정과 독립 |
| Boss `3201` | `6100`, `6101`, `6102`, `6103` | `7012`, `7011`, `7010`, `7013` | `DirectedBox` 1회, `Arc` 2회, `Arc` 1회, `Ring` 1회 | Charge / ComboSlash / OverheadSmash / Shockwave |

## 표시 계약

- `Telegraph`: 도형 윤곽을 낮은 알파로 먼저 표시한다. 현행 디버그 기준색은 노랑 `(1.0, 0.8, 0.1, 0.25)`이다.
- `Active`: 동일 도형을 선명하게 표시한다. 현행 디버그 기준색은 빨강 `(1.0, 0.2, 0.2, 0.5)`이다.
- `Recovery`: 신규 공격 면적을 표시하지 않고 잔광만 감쇠한다.
- `Cancel`: 즉시 소거하며 잔존 프레임·루프를 남기지 않는다.
- 청록 `(0.0, 1.0, 1.0, 0.3)`은 현행 sweep 디버그 구분색이며 최종 미술색 권위가 아니다.

## 자산 규격

| 항목 | 납품 규격 |
| :--- | :--- |
| 형식 | 투명 배경 RGBA PNG, 픽셀 아트, Point 필터 전제, mipmap 없음 |
| 프레임 | 가로 8프레임 strip, 8 FPS 시안 |
| 일반 셀/시트 | Player·Monster: 셀 `128×128 px`, 시트 `1024×128 px` |
| Boss 셀/시트 | Boss `3201`: 셀 `256×256 px`, 시트 `2048×256 px` |
| 피벗 | 각 셀 중앙 `(0.5, 0.5)` |
| PPU | 납품 목표 `100` |
| 알파 | 배경 완전 투명, 외곽 잘림·불투명 배경 없음 |
| 파일명 | `VFX_DummyAttack_U<unit>_P<pattern-or-NA>_S<skill>_<shape>.png` |
| 납품 위치 | `doc/ai_orders/completed/2026-08-24_dummy_attack_effects/assets/` |

현행 공격·Boss effect 원본은 동일한 8프레임/중앙 피벗 구조지만 legacy import가 PPU `128`이다. 해당 `.meta`를 복제하지 말고 이번 납품 규격 PPU `100`은 후속 Unity 통합자가 별도 검증한다.

## 식별자 보호

- Effect `8001`은 재사용·복제·재배정 금지다.
- Response effect `8010` Parry, `8011` Guard, `8012` Dodge, `8013` Hit은 현행 용도로 유지하며 공격 시안에 전용하지 않는다.
- 신규 Effect/Resource `idx` 후보를 만들지 않는다. 파일명은 전달 식별용이며 런타임 routing이 아니다.

## 납품 검증

- 7개 유닛 매핑 누락 0, `6009` 2-hit 펄스 2개
- 규격별 8프레임, 셀 크기, 중앙 피벗, PPU100 메타 요구사항 명시
- PNG 알파·잘림·프레임 경계 이상 0
- Collider/Damage/코드/CSV/Prefab/Addressables/idx 산출물 0
- 완전 성공과 검증 완료 전에는 이 문서의 Status와 위치를 pending으로 유지
