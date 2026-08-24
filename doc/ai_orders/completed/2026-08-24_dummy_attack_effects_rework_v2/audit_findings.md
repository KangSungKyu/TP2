# Dummy Attack Effects Rework V2 감사 결과

- Status: `COMPLETED`
- 재작업: `5`
- 제외 PASS: `10`
- 기존 completed 변경: `0`

## V2 결함

| 대상 | 감사 결과 |
| :--- | :--- |
| `U3105/P6006/S7002` | lower aim 공격인데 산출물 파일명과 type이 `Ring`; `Line`으로 정정 필요 |
| `U3201/P6100/S7012` | OverheadSmash인데 `DirectedBox`; `Arc`로 정정 필요 |
| `U3201/P6102/S7010` | Charge인데 `Arc`; `DirectedBox`로 정정 필요 |
| `U3001/S7003` | 2-hit 각각에 완전한 Draw 1–4 / Erase 5–8 수명주기 필요 |
| `U3201/P6101/S7011` | 2-hit 각각에 완전한 Draw 1–4 / Erase 5–8 수명주기 필요 |

## PASS 10

`U3001/S7001`, `U3101/P6001`, `U3102/P6008`, `U3102/P6009`, `U3103/P6001`, `U3103/P6010`, `U3104/P6003`, `U3104/P6004`, `U3105/P6005`, `U3201/P6103`.

PASS 10은 재작업·복제·이름 변경 대상이 아니다. `U3102/P6009`의 2-hit/3연속 금지 계약도 유지한다.

## 판정 권위 결함 방지

- 실제 Active 공간은 후속 `EffectData center/size` 직렬화 값만 권위로 사용한다.
- PNG alpha scan, `Renderer.bounds`, 문자열 lookup은 판정 계산 근거로 사용할 수 없다.
- 프레임별 trail 크기는 hit tick별 고정 승인 bounds를 변경하지 않는다.
- 모든 시각 trail은 Collider/Damage 0이다.
