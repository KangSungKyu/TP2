# 재작업 수행 결과 (Rework Result)

- 발주명: `2026-08-24_dummy_attack_effects_rework`
- 수행 상태: `COMPLETED`
- 수행 일시: `2026-08-24 17:23:45 KST`
- 원본 완료 증거: [`../2026-08-24_dummy_attack_effects/`](../2026-08-24_dummy_attack_effects/) (보존 완료)

## 1. 재작업 개요

감사 결과에 따라 전체 도형 일괄 scale/fade 결함을 해소하고, `directional_slash_draw_erase.png` 기준의 **방향성 Draw-then-Erase(선두 진행 ➔ 꼬리 소거) 애니메이션 계약**을 완벽하게 적용한 REWORK 5종 및 REJECT 4종(총 9종) 시안을 제작하였습니다.

## 2. 동작 권위 정정 및 제작 결과

1. **Player `3001`**:
   - `S7001` Arc (REWORK): `ReverseVerticalUpswing` (후방 저점 ➔ 머리 ➔ 전방 상단 방향성 궤적)
   - `S7003` Arc (REWORK): 2-Hit Combo (1타 하향 베기 ➔ 2타 상향 베기 독립 2펄스 궤적)
2. **Monster `3103` (Wave Heavy)**:
   - `P6001` Arc (REJECT ➔ 신규): `VerticalDown` (머리 위 ➔ 전방 지면 수직 하향 베기)
3. **Monster `3104` (Shield Sentinel)**:
   - `P6004` Arc (REWORK): `VerticalDown` (머리 위 ➔ 지면 내려치기 및 방향성 소거)
4. **Monster `3105` (Orbital Marksman)**:
   - `P6005` Line (REWORK): Crossbow Aim & Line Shot (총구 ➔ 전방 방향성 발사 궤적)
   - `P6006` Line (REJECT ➔ 신규): lower aim `Line` (하단 조준 사격 궤적)
5. **Boss `3201` (Garon)**:
   - `P6100` Arc (REJECT ➔ 신규): OverheadSmash `Arc` (고점 ➔ 전방 지면 대검 강타)
   - `P6101` Arc (REWORK): Down→Upswing `Arc` (1타 하향 ➔ 2타 상향 2연속 대검 콤보)
   - `P6102` DirectedBox (REJECT ➔ 신규): Charge `DirectedBox` (대검 돌진 전진 면적 및 후방 소거)

## 3. 계약 준수 확인

- PASS 6종(`3101/P6001`, `3102/P6008`, `3102/P6009`, `3103/P6010`, `3104/P6003`, `3201/P6103`)은 기존 증거를 온전히 보존하고 재작업 패키지에서 재제작하지 않음.
- `3102/P6009`는 정확히 2-hit이며 3연속 제작 금지 준수.
- 모든 파일 SHA-256 Checksum 신규 생성 파일 기반 100% 직접 재산출.
- 코드/CSV/Prefab/Animator/Addressables/idx 수정 0건 (비실행형 자산 엄수).
