# [SubPlan] 6x6 스테이지 청크 모듈 템플릿 명세서 (가변 NxM 청크 & 초반 함정 밀도 조절)

## 1. 개요 및 유저 확정 요구사항

본 문서는 플레이어의 경험(UX) 및 스테이지 난이도 곡선(Progression Curve)을 반영한 **모듈 패턴 수선**, **가변 규격 청크 아키텍처($N \times M$ 모듈, $3 \le N, M \le 20$)**, 및 **Stage 1 함정/플랫포밍 난이도 완화 지칙**을 정의합니다.

### 1.1 유저 확정 3대 개선 지칙 (Confirmed Directives)
1. **`Module_L1` 및 함정 모듈 구조 개선**:
   - 함정, 지형, 발판이 인접하여 밀집된 억까 구조 전면 철폐.
   - 함정 주변 최소 **2.5m 이상의 착지/회피 여유 공간** 확보 및 직관적 도약 곡선 제공.
2. **가변 청크 규격 ($N \times M$ 모듈, $3 \le N, M \le 20$) & 공간 균일 배치**:
   - 모든 청크를 동일한 $10 \times 5$ 규격으로 강제하지 않고, **좁은 공간(좁은 통로/샤프트/쉼터)**과 **넓은 공간(광장/아레나/통로)**을 균일하게 배정.
   - 청크별 고유 크기 지정:
     - 좁은 룸: $4 \times 5$ ($24\text{m} \times 30\text{m}$), $5 \times 3$ ($30\text{m} \times 18\text{m}$)
     - 중간 룸: $6 \times 3$ ($36\text{m} \times 18\text{m}$), $6 \times 4$ ($36\text{m} \times 24\text{m}$), $7 \times 4$ ($42\text{m} \times 24\text{m}$)
     - 넓은 룸: $8 \times 4$ ($48\text{m} \times 24\text{m}$), $10 \times 5$ ($60\text{m} \times 30\text{m}$)
3. **Stage 1 함정 & 복잡 플랫포밍 난이도 조절**:
   - 낮은 스테이지(Stage 1) 특성을 고려하여 **가시/톱날 함정 수 및 복잡 플랫포밍 밀도를 최소화**.
   - 쾌적하고 쾌감 있는 전투 및 탐험 중심 동선 보장.

---

## 2. 11종 가변 규격 청크 공간 배치 사전 (Variable Chunk Grid Dimensions)

| 청크 Name | 용도 분류 | 공간 성격 | 모듈 규격 ($N \times M$) | 실제 월드 크기 |
|---|---|---|:---:|:---:|
| **`Prefab_1040`** | Entry Safe Room | 입구 쉼터 | **$6 \times 3$** | $36\text{m} \times 18\text{m}$ |
| **`Prefab_1041`** | Battle Room A | 주 전투 룸 | **$8 \times 4$** | $48\text{m} \times 24\text{m}$ |
| **`Prefab_1042`** | Boss Arena | 보스 광장 아레나 | **$10 \times 5$** | $60\text{m} \times 30\text{m}$ |
| **`Room_11050`** | Ascent Shaft | 수직 상승 샤프트 | **$4 \times 5$** | $24\text{m} \times 30\text{m}$ |
| **`Room_11051`** | Descent Drop | 수직 낙하 샤프트 | **$4 \times 5$** | $24\text{m} \times 30\text{m}$ |
| **`Room_11052`** | Corridor East-West | 수평 횡단 복도 | **$8 \times 3$** | $48\text{m} \times 18\text{m}$ |
| **`Room_11053`** | Elite Arena | 정예 아레나 | **$8 \times 4$** | $48\text{m} \times 24\text{m}$ |
| **`Room_11056`** | High Cliffs | 고지대 절벽 | **$6 \times 4$** | $36\text{m} \times 24\text{m}$ |
| **`Room_11057`** | Platform Maze | 부유 발판 코스 | **$6 \times 4$** | $36\text{m} \times 24\text{m}$ |
| **`Room_11061`** | Rest Shelter | 휴식 아늑한 쉼터 | **$5 \times 3$** | $30\text{m} \times 18\text{m}$ |
| **`Room_11063`** | Challenge Room | 라이트 플랫포머 | **$7 \times 4$** | $42\text{m} \times 24\text{m}$ |

---

## 3. 함정 밀도 조절 계약 (Low Stage 1 Hazard Density)

- **Stage 1 평균 함정 수**: 청크당 **0 ~ 2개 이하**로 제한 (초반 가파른 피로도 방지).
- **Module_L1/L2 개선**:
  - 기존 톱날-발판 인접 배치를 분리하여 **착지대 중앙 3m 개방**, 톱날은 벽면 가장자리에만 미세 배치.
