# 외부 AI 독립 프롬프트 — Dummy Attack Effect 재작업

- Status: `COMPLETED`

아래 범위만 수행하라. 기존 완료 패키지와 프로젝트 파일을 수정하지 말고, 비실행형 PNG 시안만 새 재작업 패키지에 생성하라.

## 입력과 참고

- 기존 완료본: `doc/ai_orders/completed/2026-08-24_dummy_attack_effects/` — 감사 증거이므로 수정·이동 금지
- 방향 참고 이미지: `doc/ai_orders/completed/2026-08-24_dummy_attack_effects_rework/references/directional_slash_draw_erase.png`
- 실제 AttackCollider와 sweep bounds가 판정 권위다. 시각 trail에는 Collider와 Damage가 없다.

## 필수 애니메이션 계약

1. 베기·찌르기의 전체 도형 일괄 scale/fade를 금지한다.
2. 프레임 전반에는 시작점에서 끝점으로 선두가 진행하며 점차 그린다.
3. 프레임 후반에는 시작점부터 같은 방향으로 꼬리가 따라가며 점차 지운다.
4. 권장 8프레임은 `20%`, `40%`, `70%`, `100%/Active`, `앞 20% 소거`, `50% 소거`, `80% 소거`, `완전 투명` 순서다.
5. trail 선두가 실제 hitbox보다 먼저 끝점에 도착하면 안 된다.
6. 다단 공격은 hit window마다 독립 trail을 1회 재생하고 이전 trail 반환 후 다음 trail을 재생한다.
7. 수직 내려베기는 머리 위→전방 지면, `ReverseVerticalUpswing`은 후방 저점→머리→전방 상단, 찌르기는 몸체 근처→전방 끝점이다.
8. 충격파 Ring은 중심→외곽 방향으로 생성하고 같은 방향으로 소거한다.

## 감사 결과와 제작 범위

- PASS 6, 재제작 금지: `3101/P6001`, `3102/P6008`, `3102/P6009`, `3103/P6010`, `3104/P6003`, `3201/P6103`.
- REWORK 5: `3001/S7001`, `3001/S7003`, `3104/P6004`, `3105/P6005`, `3201/P6101`.
- REJECT 후 신규 제작 4: `3103/P6001`, `3105/P6006`, `3201/P6100`, `3201/P6102`.

동작 권위는 다음과 같다.

- `3001/S7001`: `ReverseVerticalUpswing`
- `3103/P6001`: `VerticalDown`
- `3105/P6006`: lower aim `Line`
- Boss `6100`: OverheadSmash `Arc`
- Boss `6101`: Down→Upswing `Arc`
- Boss `6102`: Charge `DirectedBox`
- Boss `6103`: Shockwave `Ring`
- `3102/P6009`: 정확히 2-hit. 3연속 trail/펄스 금지

## PNG 규격

- 투명 RGBA PNG, 가로 8프레임 strip
- 일반 셀 `128×128 px`, 시트 `1024×128 px`
- Boss 셀 `256×256 px`, 시트 `2048×256 px`
- 중앙 피벗 `(0.5,0.5)`, Unity import 목표 PPU100
- Point 필터/mipmap 없음 전제
- 셀 경계 안전 여백 필수, 인접 셀 침범·잘림 금지
- 기존 대상 파일명을 유지하되 `doc/ai_orders/completed/2026-08-24_dummy_attack_effects_rework/assets/`에만 생성

## 금지사항

- 코드, CSV, Prefab, Animator, Addressables, idx 생성·수정 금지
- 기존 첫 완료본, OpenWiki generated 파일, 다른 발주 파일 수정·이동 금지
- PASS 6종 재제작 금지
- 기존 manifest SHA-256 복사 금지. 기존 값은 실제 PNG와 `0/15` 일치였으므로 신규 산출물에서 직접 재산출

## 완료 프로토콜

1. 모든 재작업과 검증이 성공한 경우에만 spec/prompt/audit/references/assets를 `doc/ai_orders/completed/2026-08-24_dummy_attack_effects_rework/`로 이동한다.
2. 완료 폴더에 `manifest.md`(파일 목록과 신규 SHA-256 또는 크기), `result.md`(수행 결과), `qa_notes.md`(검증과 미해결 위험)를 생성한다.
3. partial, BLOCKED, 검증 실패는 pending을 유지하고 문서 Status만 `IN_PROGRESS` 또는 `BLOCKED`로 갱신한다.
4. 완료 이동 시 원본 pending 중복본을 남기지 않는다.
5. 기존 첫 완료본과 OpenWiki generated 파일은 이동하지 않는다.

## 응답 형식

```markdown
Status: COMPLETED | IN_PROGRESS | BLOCKED

## Produced Files
| file | target | verdict-source | frames | dimensions | sha256 |

## Validation
| check | result | evidence |

## Unresolved Risks
- risk or `none`
```

`COMPLETED`는 9개 재작업 PNG 생성, 방향성·안전 여백·판정 비선행 검증, 신규 SHA-256 산출이 모두 끝난 경우에만 사용하라.
