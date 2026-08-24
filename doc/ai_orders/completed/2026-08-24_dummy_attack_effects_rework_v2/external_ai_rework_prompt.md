# 외부 AI 독립 프롬프트 — Dummy Attack Effects Rework V2

- Status: `COMPLETED`

비실행형 공격 이펙트 PNG 5종만 재작업하라. 기존 completed 두 패키지와 PASS 자산은 수정·이동·복제하지 마라.

## 제작 대상

1. `U3105 P6006 S7002`: lower aim `Line`. 기존 `_Ring.png`가 아니라 `_Line.png`로 납품한다.
2. `U3201 P6100 S7012`: OverheadSmash `Arc`. 기존 `_DirectedBox.png`가 아니라 `_Arc.png`로 납품한다.
3. `U3201 P6102 S7010`: Charge `DirectedBox`. 기존 `_Arc.png`가 아니라 `_DirectedBox.png`로 납품한다.
4. `U3001 S7003`: 2-hit 각각 독립 Draw 1–4 / Erase 5–8. hit별 8-frame strip 2개 또는 순서가 명시된 16-frame 단일 strip으로 납품한다.
5. `U3201 P6101 S7011`: 2-hit 각각 독립 Draw 1–4 / Erase 5–8. hit별 8-frame strip 2개 또는 순서가 명시된 16-frame 단일 strip으로 납품한다.

2-hit는 1타 trail 반환 후 2타를 재생한다. 두 타격을 하나의 8-frame trail에 겹치거나 압축하지 마라.

## PASS 10 — 재작업 금지

`U3001/S7001`, `U3101/P6001`, `U3102/P6008`, `U3102/P6009`, `U3103/P6001`, `U3103/P6010`, `U3104/P6003`, `U3104/P6004`, `U3105/P6005`, `U3201/P6103`.

`U3102/P6009`는 정확히 2-hit이며 3연속 제작 금지다.

## 판정 공간 계약

- 실제 Active bounds는 후속 `EffectData`에 직렬화할 승인 `center/size`가 권위다.
- PNG alpha scan, `Renderer.bounds`, 문자열 lookup으로 판정 공간을 계산하지 마라.
- 시각 trail의 Collider/Damage는 0이다.
- 프레임별 시각 크기와 관계없이 hit tick별 승인 bounds는 고정이다.
- 이번 작업에서 `EffectData`, idx, 코드, CSV, Prefab을 만들거나 수정하지 마라.

## PNG 규격

- 시작점→끝점 Draw 1–4, 같은 방향 Erase 5–8
- RGBA, 중앙 피벗 `(0.5,0.5)`, PPU100 import target
- 일반 8-frame `1024×128`, Boss 8-frame `2048×256`
- 16-frame 선택 시 일반 `2048×128`, Boss `4096×256`
- 셀 경계 안전 여백, 인접 프레임 침범·잘림 0
- 신규 파일은 `doc/ai_orders/completed/2026-08-24_dummy_attack_effects_rework_v2/assets/`에만 생성

## 완료 프로토콜

1. 5종 제작과 검증이 모두 성공한 경우에만 spec/prompt/audit/assets를 `doc/ai_orders/completed/2026-08-24_dummy_attack_effects_rework_v2/`로 이동한다.
2. 완료 폴더에 `manifest.md`(파일 목록과 신규 SHA-256 또는 크기), `result.md`, `qa_notes.md`를 생성한다.
3. partial, BLOCKED, 검증 실패는 pending 유지 후 Status만 `IN_PROGRESS` 또는 `BLOCKED`로 갱신한다.
4. 완료 이동 후 pending 중복본을 남기지 않는다.
5. 기존 completed 두 패키지, OpenWiki generated 파일, 다른 발주는 이동하지 않는다.

## 응답 형식

```markdown
Status: COMPLETED | IN_PROGRESS | BLOCKED

## Produced Files
| file | target | hit | frames | dimensions | sha256 |

## Validation
| check | result | evidence |

## Unresolved Risks
- risk or `none`
```

`COMPLETED`는 5종 제작, 2-hit 독립성, type/파일명, 경계 여백, 신규 해시 검증이 모두 끝난 경우에만 사용하라.
