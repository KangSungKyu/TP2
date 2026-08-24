# 재작업 V2 수행 결과 (Rework V2 Result)

- 발주명: `2026-08-24_dummy_attack_effects_rework_v2`
- 수행 상태: `COMPLETED`
- 수행 일시: `2026-08-24 17:44:41 KST`
- 보존 완료 증거:
  - [`../2026-08-24_dummy_attack_effects/`](../2026-08-24_dummy_attack_effects/)
  - [`../2026-08-24_dummy_attack_effects_rework/`](../2026-08-24_dummy_attack_effects_rework/)

## 1. 재작업 V2 개요

감사 결과 V2에 따라 파일명/도형 불일치 3종을 정정하고, 2-hit 다단 공격 2종에 대해 단일 압축을 배제하고 각 타격별 완전한 8프레임 Draw 1–4 / Erase 5–8 수명주기를 갖는 16프레임 단일 스트립으로 재제작하여 납품 완료하였습니다.

## 2. 정정 및 제작 내역

1. **`U3105 P6006 S7002`**:
   - 기존 `_Ring.png` ➔ lower aim `Line` 규격 및 `VFX_DummyAttack_U3105_P6006_S7002_Line.png` (1024×128, 8 frames)로 정정 제작 완료.
2. **`U3201 P6100 S7012`**:
   - 기존 `_DirectedBox.png` ➔ OverheadSmash `Arc` 규격 및 `VFX_DummyAttack_U3201_P6100_S7012_Arc.png` (2048×256, 8 frames)로 정정 제작 완료.
3. **`U3201 P6102 S7010`**:
   - 기존 `_Arc.png` ➔ Charge `DirectedBox` 규격 및 `VFX_DummyAttack_U3201_P6102_S7010_DirectedBox.png` (2048×256, 8 frames)로 정정 제작 완료.
4. **`U3001 S7003`**:
   - 2-hit 각 타격별 완전 분리: 1타(1~8f: Draw 1~4, Erase 5~8) ➔ 2타(9~16f: Draw 9~12, Erase 13~16)를 담은 `VFX_DummyAttack_U3001_PNA_S7003_Arc.png` (2048×128, 16 frames) 제작 완료.
5. **`U3201 P6101 S7011`**:
   - 2-hit 각 타격별 완전 분리: 1타 내려베기(1~8f) ➔ 2타 올려베기(9~16f)를 담은 `VFX_DummyAttack_U3201_P6101_S7011_Arc.png` (4096×256, 16 frames) 제작 완료.

## 3. 계약 및 무결성 준수

- PASS 10종(`U3001/S7001`, `U3101/P6001`, `U3102/P6008`, `U3102/P6009`, `U3103/P6001`, `U3103/P6010`, `U3104/P6003`, `U3104/P6004`, `U3105/P6005`, `U3201/P6103`)은 기존 증거 패키지에 온전히 보존하며 재작업 패키지에서 재제작·복제·이름 변경하지 않음.
- 모든 신규 5종 자산의 SHA-256 Checksum은 생성 파일에서 100% 직접 산출하여 매니페스트에 등록.
- 런타임 공격 판정 권위 침범 없음 (시각 trail Collider/Damage 0).
