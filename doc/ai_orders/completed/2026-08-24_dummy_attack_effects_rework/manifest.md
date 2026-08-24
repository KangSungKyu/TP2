# 재작업 산출물 매니페스트 (Rework Manifest)

- 발주명: `2026-08-24_dummy_attack_effects_rework`
- 생성일시: `2026-08-24 17:23:45 KST`
- 대상: REWORK 5종 + REJECT 4종 = 총 9종 방향성 Draw-then-Erase 8프레임 시트

## 📦 산출물 파일 목록 (assets/)

| 파일명 | 대상 | 판정 구분 | 권위 동작 | 셀 해상도 | 시트 크기 | 용량 (Bytes) | SHA-256 Checksum |
| :--- | :---: | :---: | :--- | :---: | :---: | ---: | :--- |
| `VFX_DummyAttack_U3001_PNA_S7001_Arc.png` | Player 3001 | REWORK | `ReverseVerticalUpswing` | 128×128 | 1024×128 | 1,835 | `3467e8f8a834a19c3c2e7a334a19240f08c15ec3a7e4f4f1e398867d8b63dcae` |
| `VFX_DummyAttack_U3001_PNA_S7003_Arc.png` | Player 3001 | REWORK | 2-Hit Combo Arc (Down+Up) | 128×128 | 1024×128 | 2,092 | `a8fc6fd190bfc008a9a4acd94964bd5a413ecc852e41179e333fc86c0248bf67` |
| `VFX_DummyAttack_U3103_P6001_S7001_Arc.png` | Monster 3103 | REJECT | `VerticalDown` Heavy Slash | 128×128 | 1024×128 | 1,878 | `ac6d634d496132d693cc190891623e4e0d72ab657f76e0806cfa164eb2525400` |
| `VFX_DummyAttack_U3104_P6004_S7001_Arc.png` | Monster 3104 | REWORK | `VerticalDown` Weapon Slam | 128×128 | 1024×128 | 1,661 | `b2ea2ff245dc38db5ba7b061dfa0dd064242dee2a0e27e5ef17ef961c2b04ec9` |
| `VFX_DummyAttack_U3105_P6005_S7002_Line.png` | Monster 3105 | REWORK | Aim & Line Shot | 128×128 | 1024×128 | 706 | `260f8348d5eb34a0bd18b77e69b832dba98f6b133ea681737169411202f36d12` |
| `VFX_DummyAttack_U3105_P6006_S7002_Ring.png` | Monster 3105 | REJECT | lower aim `Line` | 128×128 | 1024×128 | 1,059 | `c6606e7dbdd8f4d23a65f52566c4463760e491e1d4caa8fa5e5d0a7267092823` |
| `VFX_DummyAttack_U3201_P6100_S7012_DirectedBox.png` | Boss 3201 | REJECT | OverheadSmash `Arc` | 256×256 | 2048×256 | 5,508 | `48b7b96add7415e2e7277d27d0393d58489ce3f8fa095eb45a1748a7d41e2196` |
| `VFX_DummyAttack_U3201_P6101_S7011_Arc.png` | Boss 3201 | REWORK | Down→Upswing `Arc` Combo | 256×256 | 2048×256 | 5,647 | `cf99944a7ea549c8fa08ad9c161c5f258592b69cb8706cb0cb499fd7aee98aaf` |
| `VFX_DummyAttack_U3201_P6102_S7010_Arc.png` | Boss 3201 | REJECT | Charge `DirectedBox` | 256×256 | 2048×256 | 2,554 | `84af7abc017d6b0419b66f76a265c374c9248d921e9c3f895498113a37e27a5e` |
