# Dummy Attack Effect 감사 결과

- Status: `COMPLETED`
- 감사 대상: [`../../../completed/2026-08-24_dummy_attack_effects/`](../../../completed/2026-08-24_dummy_attack_effects/)
- 기존 완료본 변경: `0`

## 판정 집계

| 판정 | 수 | 대상 |
| :--- | ---: | :--- |
| PASS | 6 | `3101/P6001`, `3102/P6008`, `3102/P6009`, `3103/P6010`, `3104/P6003`, `3201/P6103` |
| REWORK | 5 | `3001/S7001`, `3001/S7003`, `3104/P6004`, `3105/P6005`, `3201/P6101` |
| REJECT | 4 | `3103/P6001`, `3105/P6006`, `3201/P6100`, `3201/P6102` |

## 주요 결함

- 베기·찌르기 일부가 방향성 trail 생성·소거 대신 전체 도형 scale/fade로 표현되었다.
- 동작 의미가 뒤바뀐 대상이 있다: `3001/S7001=ReverseVerticalUpswing`, `3103/P6001=VerticalDown`, `3105/P6006=lower aim Line`.
- Boss 권위 매핑은 `6100=OverheadSmash Arc`, `6101=Down→Upswing Arc`, `6102=Charge DirectedBox`, `6103=Shockwave Ring`이다.
- 셀 경계 안전 여백과 실제 hitbox보다 시각 trail이 선행하지 않는지 재검증이 필요하다.
- 기존 manifest의 SHA-256은 실제 15개 PNG와 대조 결과 `0/15` 일치다. 재작업 완료 시 신규 파일에서 직접 재산출해야 한다.

## 보호 계약

- `3102/P6009`는 2-hit이며 3연속 제작 금지다.
- PASS 6종은 기존 완료 증거를 유지하고 재작업 대상에 포함하지 않는다.
- 실제 AttackCollider/sweep bounds가 판정 권위이며 시각 trail의 Collider/Damage는 0이다.
