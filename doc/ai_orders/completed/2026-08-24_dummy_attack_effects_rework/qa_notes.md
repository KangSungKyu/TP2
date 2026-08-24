# 재작업 품질 검증 보고서 (Rework QA Notes)

- 발주명: `2026-08-24_dummy_attack_effects_rework`
- 검증 일시: `2026-08-24 17:23:55 KST`
- 검증 상태: `PASSED (방향성 생성·소거 계약 100% 충족)`

## 1. 산출물 검증 매트릭스

| 검증 항목 | 기준 규격 | 검증 결과 | 비고 |
| :--- | :--- | :---: | :--- |
| **방향성 Draw-then-Erase** | 전반 1~4f 선두 진행 ➔ 후반 5~8f 꼬리 소거 | **PASS** | 일괄 scale/fade 완전 제거 확인 |
| **Active Frame 정렬** | Frame 4 = 100% Active 타격점 | **PASS** | 4프레임 최고점 붉은색 코어 일치 |
| **Frame 8 완전 투명** | Frame 8 = 완전 소거 (알파 0) | **PASS** | 잔존 루프/잔광 0 확인 |
| **동작 권위 정정** | Upswing, Down, Lower Aim, Overhead, Charge | **PASS** | 9종 전원 명세 권위와 1:1 일치 |
| **다단 hit window 독립** | `S7003`, `P6101` 콤보 독립 2펄스 | **PASS** | 1타 소거 후 2타 재생 분리 |
| **안전 여백 및 외곽 잘림** | 셀 경계 안전 여백 유지 | **PASS** | 인접 프레임 침범 및 잘림 0 |
| **포맷 및 해상도** | 일반 1024×128 / 보스 2048×256 RGBA PNG | **PASS** | 중앙 피벗 `(0.5, 0.5)`, PPU100 목표 |
| **SHA-256 Checksum** | 신규 파일 직접 산출 | **PASS** | 9종 파일 전원 직접 해시 검증 |
| **런타임 비침범** | Collider/Damage/코드/CSV/idx 변경 0 | **PASS** | 비실행형 시각 자산 무결성 |

## 2. 미해결 위험 (Unresolved Risks)

- **위험**: `none` (미해결 위험 없음)
