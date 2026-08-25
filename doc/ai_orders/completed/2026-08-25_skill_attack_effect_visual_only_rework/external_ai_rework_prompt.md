# 외부 AI 독립 프롬프트 — Skill Attack Effect Visual-Only Rework

- Status: `COMPLETED`

`rework_spec.md`의 대상 19개 공격 이펙트 PNG만 재작업하라. 기존 completed 패키지와 기존 리소스는 감사 증거이므로 수정·이동·덮어쓰기하지 마라.

## 필수 작업

- 출력은 날짜·Unit idx·Effect idx가 포함된 `2026-08-25_U<unitidx>_E<effectidx>_skill_effect.png` 이름을 사용한다.
- 각 PNG에는 순수 시각 이펙트만 넣는다.
- 시작 → 활성 → 소멸이 프레임 순서로 식별되게 한다.
- Effect `8027/8028`, `8029/8030`은 Hit tick별 독립 파일로 유지한다.
- RGBA 투명 배경, 기존 프레임 수·캔버스·PPU100·중앙 pivot 규격과 셀 안전 여백을 유지한다.

## 절대 금지

- 공격 hitbox, collider 윤곽, 디버그 사각형·원·캡슐, 중심선, 좌표 가이드, 색상 마스크, 수치·텍스트·메타데이터 추가.
- 판정 도형 자체 또는 판정 도형처럼 보이는 고정 외곽선·반투명 채움 추가.
- PNG alpha scan, 픽셀 경계, `Renderer.bounds`를 공격 판정 권위로 사용.
- 코드, CSV, Prefab, Addressables, EffectData, idx 생성·수정.
- 대상 목록 밖 자산 재작업 또는 기존 completed 파일 덮어쓰기.

실제 hitbox는 Unity `EffectData.Shape`, `ActiveCenter`, `ActiveSize`, `Scale`에서 별도 관리된다. 시각 Effect의 Collider와 Damage는 0이다.

## 규격

- 일반 유닛: 8-frame `1024×128`, 셀 `128×128`.
- Boss 3201: 8-frame `2048×256`, 셀 `256×256`.
- 모든 PNG: RGBA, alpha 투명, PPU100 import target, pivot `(0.5,0.5)`.
- 인접 프레임 침범·잘림 0.

## 응답

산출물과 자체 검수표만 제출하라.

```markdown
Status: COMPLETED | IN_PROGRESS | BLOCKED

## Produced Files
| file | unitidx | effectidx | frames | dimensions | alpha | sha256 |

## Self QA
| check | result | evidence |

## Unresolved Risks
- risk or `none`
```

## 완료 프로토콜

1. 대상 19개 제작과 검증이 모두 성공한 경우에만 spec/prompt와 결과물을 `doc/ai_orders/completed/2026-08-25_skill_attack_effect_visual_only_rework/`로 이동한다.
2. 완료 폴더에 `manifest.md`, `result.md`, `qa_notes.md`, `assets/`를 생성한다.
3. 부분 완료·차단·검증 실패는 pending 유지 후 Status만 `IN_PROGRESS` 또는 `BLOCKED`로 갱신한다.
4. 완료 이동 뒤 pending 중복본을 남기지 않는다.
5. 기존 completed 패키지, OpenWiki generated 파일, 다른 발주는 이동하지 않는다.
