# Skill Attack Effect Visual-Only Rework 명세

- Status: `COMPLETED`
- 발주일: `2026-08-25`
- 범위: `EffectData` 공격 이펙트 `8014~8032`
- 런타임 권위: Unity `EffectData.Shape`, `ActiveCenter`, `ActiveSize`, `Scale`

## 목적

기존 공격 이펙트에서 판정·디버그 표현을 제거하고 순수 시각 이펙트만 재제작한다. 기존 completed 패키지와 기존 PNG는 감사 증거로 보존하며 수정·이동·덮어쓰기하지 않는다.

## 재작업 대상

| Unit idx | Effect idx | Pattern idx | Skill idx | Hit tick | 출력 파일명 |
| :---: | :---: | :---: | :---: | :---: | :--- |
| 3001 | 8014 | 0 | 7001 | 0 | `2026-08-25_U3001_E8014_skill_effect.png` |
| 3101 | 8015 | 6001 | 7001 | 0 | `2026-08-25_U3101_E8015_skill_effect.png` |
| 3102 | 8016 | 6008 | 7005 | 0 | `2026-08-25_U3102_E8016_skill_effect.png` |
| 3102 | 8017 | 6009 | 7006 | 0 | `2026-08-25_U3102_E8017_skill_effect.png` |
| 3103 | 8018 | 6001 | 7001 | 0 | `2026-08-25_U3103_E8018_skill_effect.png` |
| 3103 | 8019 | 6010 | 7007 | 0 | `2026-08-25_U3103_E8019_skill_effect.png` |
| 3104 | 8020 | 6003 | 7001 | 0 | `2026-08-25_U3104_E8020_skill_effect.png` |
| 3104 | 8021 | 6004 | 7001 | 0 | `2026-08-25_U3104_E8021_skill_effect.png` |
| 3105 | 8022 | 6005 | 7002 | 0 | `2026-08-25_U3105_E8022_skill_effect.png` |
| 3201 | 8023 | 6103 | 7013 | 0 | `2026-08-25_U3201_E8023_skill_effect.png` |
| 3105 | 8024 | 6006 | 7002 | 0 | `2026-08-25_U3105_E8024_skill_effect.png` |
| 3201 | 8025 | 6100 | 7012 | 0 | `2026-08-25_U3201_E8025_skill_effect.png` |
| 3201 | 8026 | 6102 | 7010 | 0 | `2026-08-25_U3201_E8026_skill_effect.png` |
| 3001 | 8027 | 0 | 7003 | 1 | `2026-08-25_U3001_E8027_skill_effect.png` |
| 3001 | 8028 | 0 | 7003 | 2 | `2026-08-25_U3001_E8028_skill_effect.png` |
| 3201 | 8029 | 6101 | 7011 | 1 | `2026-08-25_U3201_E8029_skill_effect.png` |
| 3201 | 8030 | 6101 | 7011 | 2 | `2026-08-25_U3201_E8030_skill_effect.png` |
| 3106 | 8031 | 6007 | 7003 | 0 | `2026-08-25_U3106_E8031_skill_effect.png` |
| 3001 | 8032 | 0 | 7002 | 0 | `2026-08-25_U3001_E8032_skill_effect.png` |

목록 밖 파일은 재작업하지 않는다. 다단 공격은 Effect idx·Hit tick별 독립 PNG로 납품하며 한 strip에 합치지 않는다.

## 시각 전용 계약

- PNG/스프라이트시트에는 순수 시각 이펙트만 포함한다.
- 공격 hitbox, collider 윤곽, 디버그 사각형·원·캡슐, 중심선, 좌표 가이드, 색상 마스크, 수치·텍스트·메타데이터를 그리지 않는다.
- 판정 도형을 연상시키는 고정 외곽선이나 반투명 채움도 추가하지 않는다.
- 실제 hitbox는 Unity `EffectData.Shape`, `ActiveCenter`, `ActiveSize`, `Scale`에서 별도 관리한다. PNG alpha, 픽셀 경계, `Renderer.bounds`는 판정 권위가 아니다.
- 시각 이펙트의 시작 → 활성 → 소멸 흐름은 프레임 순서로 식별 가능해야 한다. 판정 영역 자체는 표시하지 않는다.
- 시각 자산의 Collider와 Damage는 0이며 코드·CSV·Prefab·idx를 생성하거나 수정하지 않는다.

## 자산 규격

- RGBA 및 완전 투명 배경.
- 기존 프레임 수, 캔버스, 셀 크기, PPU100 import target, 중앙 pivot `(0.5,0.5)`을 유지한다.
- 일반 유닛 8-frame strip: `1024×128` (`128×128` 셀).
- Boss 3201 8-frame strip: `2048×256` (`256×256` 셀).
- 셀 경계 안전 여백을 유지하고 인접 프레임 침범·잘림을 0으로 한다.
- 기존 completed 자산을 원본으로 참고할 수 있으나 원본 파일을 수정·이동·덮어쓰기하지 않는다.

## Acceptance Criteria

1. 대상 19개와 출력 PNG 19개의 Unit/Effect idx가 1:1로 일치한다.
2. 모든 PNG가 RGBA 투명 배경이고 기존 프레임·캔버스·PPU·pivot 규격을 유지한다.
3. 각 strip에서 시작·활성·소멸이 식별되며 다단 Effect는 Hit tick별 독립이다.
4. hitbox/collider/debug shape/중심선/좌표 가이드/색상 마스크/메타데이터 픽셀이 0이다.
5. Unity 코드·CSV·Prefab·Addressables·기존 completed 파일 변경이 0이다.
6. 산출물 목록과 자체 검수표에서 파일명, 크기, 프레임, alpha, SHA-256을 확인한다.

## 제출 형식

```markdown
Status: COMPLETED | IN_PROGRESS | BLOCKED

## Produced Files
| file | unitidx | effectidx | frames | dimensions | alpha | sha256 |

## Self QA
| check | result | evidence |

## Unresolved Risks
- risk or `none`
```

산출물과 자체 검수표만 제출한다. 원문 장문, 생성 프롬프트 로그, 인증정보, 내부 추론은 제출하지 않는다.

## 완료 프로토콜

1. 19개 재작업과 검증이 모두 성공한 경우에만 이 발주 폴더와 결과물을 `doc/ai_orders/completed/2026-08-25_skill_attack_effect_visual_only_rework/`로 이동한다.
2. 완료 폴더에 `manifest.md`, `result.md`, `qa_notes.md`, `assets/`를 생성한다.
3. `manifest.md`에는 파일별 SHA-256 또는 크기를 기록하고, `result.md`에는 수행 결과, `qa_notes.md`에는 Acceptance Criteria 검증과 미해결 위험을 기록한다.
4. 부분 완료·차단·검증 실패는 pending에 유지하고 Status만 `IN_PROGRESS` 또는 `BLOCKED`로 갱신한다.
5. 완료 이동 시 pending 중복본을 남기지 않는다. 기존 completed 패키지, OpenWiki generated 파일, 다른 발주는 이동하지 않는다.
