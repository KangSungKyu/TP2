# 외부 AI 전달 프롬프트 — 더미 공격 이펙트 시안

- Status: `COMPLETED`

아래 요구만 수행하라.

## 입력 컨텍스트

Unity 2D 프로젝트의 7개 유닛 공격 가독성을 비교하기 위한 비실행형 픽셀 아트 PNG 시안이 필요하다. 실제 Attack/Projectile hitbox가 판정 권위이며, 시각 이펙트에는 Collider와 Damage가 없다.

공용 도형은 `Line`, `Arc`, `DirectedBox`, `Ring`이다. Telegraph는 낮은 알파 윤곽, Active는 선명한 동일 도형, Recovery는 잔광 감쇠, Cancel은 즉시 소거로 표현한다. 참고용 현행 디버그 색은 Telegraph 노랑 RGBA `(1.0,0.8,0.1,0.25)`, Active 빨강 `(1.0,0.2,0.2,0.5)`, sweep 청록 `(0.0,1.0,1.0,0.3)`이다. 청록은 최종 미술색 권위가 아니다.

## 제작 대상

| 유닛 | Pattern → Skill | 시안 |
| :--- | :--- | :--- |
| Player `3001` | NA → `7001`, `7003` | `Arc` 1회, `Arc` 2회 |
| Monster `3101` | `6001`→`7001`, `6002`→`7001` | `Line` 찌르기, `6002`는 utility이므로 없음 |
| Monster `3102` | `6008`→`7005`, `6009`→`7006` | Thrust 1회, Barrage는 정확히 2-hit/2펄스. 3연속 금지 |
| Monster `3103` | `6001`→`7001`, `6002`→`7001`, `6010`→`7007` | `Arc`, 없음, Torso Ram `DirectedBox` |
| Monster `3104` | `6003`→`7001`, `6004`→`7001` | `DirectedBox`, `Arc` |
| Monster `3105` | `6005`/`6006`→`7002` | 각 1회 `Line`과 작은 `Ring`; Projectile과 독립 |
| Boss `3201` | `6100`→`7012`, `6101`→`7011`, `6102`→`7010`, `6103`→`7013` | Charge `DirectedBox`, Combo `Arc` 2회, Overhead `Arc`, Shockwave `Ring` |

## PNG 납품 규격

- 투명 배경 RGBA PNG, 픽셀 아트, Point 필터/mipmap 없음 전제
- 가로 8프레임 strip, 8 FPS
- Player·Monster: 셀 128×128 px, 시트 1024×128 px
- Boss 3201: 셀 256×256 px, 시트 2048×256 px
- 각 셀 피벗 중앙 `(0.5,0.5)`, PPU100
- 알파 배경과 외곽 잘림 없음
- 파일명 `VFX_DummyAttack_U<unit>_P<pattern-or-NA>_S<skill>_<shape>.png`
- 납품 디렉터리 `doc/ai_orders/completed/2026-08-24_dummy_attack_effects/assets/`

## 금지사항

- 실제 프로젝트 파일 수정, 코드 생성, CSV/Prefab/Animator/Addressables 편집 금지
- Effect/Resource idx 배정·제안 및 문자열 runtime routing 금지
- Effect `8001` 사용 금지
- Response `8010` Parry, `8011` Guard, `8012` Dodge, `8013` Hit의 의미 변경·재사용 금지
- 실제 hitbox보다 공격 범위가 정확하다고 주장하거나 게임플레이 판정을 시각 자산에 포함하지 말 것
- API 키, 토큰, 인증정보, 개인 경로를 결과물에 기록하지 말 것

## 완료 프로토콜

1. pending 문서를 참고한 작업은 성공적으로 완료되고 산출물 검증까지 끝난 경우에만 해당 발주 spec/prompt와 결과물을 `completed/YYYY-MM-DD_<slug>/`로 이동한다.
2. 완료 폴더에 `manifest.md`(파일 목록과 SHA-256 또는 파일 크기), `result.md`(수행 결과), `qa_notes.md`(검증 결과와 미해결 위험), `assets/`를 생성한다.
3. 부분 완료, 차단, 검증 실패는 pending을 유지하고 문서 `Status`만 `IN_PROGRESS` 또는 `BLOCKED`로 갱신한다.
4. 이동 시 원본 pending 중복본을 남기지 않는다.
5. OpenWiki generated 파일과 다른 발주 파일은 이동하지 않는다.

## 응답 형식

```markdown
Status: COMPLETED | IN_PROGRESS | BLOCKED

## Produced Files
| file | unit | pattern | skill | frames | size | sha256-or-bytes |

## Validation
| check | result | evidence |

## Risks
- unresolved risk or `none`
```

`COMPLETED`는 모든 PNG 생성과 규격 검증이 끝난 경우에만 사용하라. 그 외에는 pending 위치를 유지하고 누락·차단 사유를 기록하라.
