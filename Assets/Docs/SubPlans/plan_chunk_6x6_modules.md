# [SubPlan] 6x6 스테이지 청크 모듈 템플릿 명세서 (6x6 Chunk Module Templates)

## 1. 개요 및 기획/프로그래머 검증 수치 (Player Physics Baseline)

본 문서 6x6 청크 모듈 템플릿은 `Player.cs` 및 `KinematicMotor2D.cs`의 정밀 물리 이동 스펙을 기반으로 설계되었습니다.

### 1.1 플레이어 이동 물리 매개변수 (Empirical Physics Specs)
| 항목 | 검증 수치 | 설명 / 도약 제약 |
|---|---:|---|
| **이동 속도 (`Speed`)** | `6.0 m/s` | 표준 수평 이동 속도 |
| **대시 속도 (`DodgeDashSpeed`)** | `12.0 m/s` | 무적 대시 속도 (지속시간 `0.30s`) |
| **대시 이동 거리 (`Dash Distance`)** | **`3.6 m` (3.6 타일)** | 평지 수평 순간 대시 거리 |
| **수직 점프력 (`JumpForce`)** | `11.5 m/s` | 수직 이탈 속도 (중력 `Gravity = 30.0m/s²`) |
| **최대 수직 점프 높이 (`Max Jump Height`)** | **`2.2 m ~ 2.5 m` (약 2.5 타일)** | 단일 점프 도달 가능 최고 수직 높이 |
| **최대 수평 점프 거리 (`Max Jump Range`)** | **`4.5 m` (약 4.5 타일)** | 기본 이동 중 수평 도약 거리 |
| **대시 점프 도약 거리 (`Dash Jump Range`)** | **`5.2 m ~ 5.5 m` (약 5.5 타일)** | 대시 후 수평 도약 최대 거리 |
| **벽 점프 (`WallJumpForce`)** | `X: 9.5 m/s, Y: 12.5 m/s` | 벽면 수직 상승 반동 (`3.0m` 도약) |

---

## 2. 6x6 모듈 규격 및 범례 (Module Grid Specification)

- **규격**: $6 \text{ cells} \times 6 \text{ cells}$ ($6\text{m} \times 6\text{m}$)
- **타일 범례**:
  - `■` : Solid Ground / Wall Tile (고체 지형 타일)
  - `═` : One-Way Platform (1-Way 하향 통과 가능 발판)
  - `▲` : Ground Spike Trap (바닥 가시 함정, Upward Spike)
  - `▼` : Ceiling Spike Trap (천장 가시 함정, Downward Spike)
  - `◄` / `►` : Wall Spike Trap (좌/우 벽면 가시 함정)
  - `◎` : Circular Saw Blade Trap (둥근 톱날 함정 - 고정/이동)
  - `·` : Open Air / Passable Space (통과 가능 빈 공간)
  - `S` / `E` : Module Entry / Exit Socket (모듈 진입/진출 지점)

---

## 3. 6x6 청크 모듈 템플릿 모음 (12가지 유형)

### [Type A: 평지 & 지면 함정 모듈]

#### Module A1: 평지 가시 건너뛰기 (Standard Spike Jump)
```text
· · · · · · (Y=5)
· · · · · · (Y=4)
· · · · · · (Y=3)
S · · · · E (Y=2) - 3타일 가시 건너뛰기 (Jump Range 4.5m 통과)
■ ■ ▲ ▲ ▲ ■ (Y=1)
■ ■ ■ ■ ■ ■ (Y=0)
```

#### Module A2: 중앙 톱날 & 하단 가시 회피 (Saw Blade High Pass)
```text
· · · · · · (Y=5)
· · · ◎ · · (Y=4) - 둥근 톱날 자가 회전
· · ═ ═ · · (Y=3) - 발판을 통한 고지대 우회
S · · · · E (Y=2)
■ ▲ ▲ ▲ ▲ ■ (Y=1) - 4타일 지면 가시
■ ■ ■ ■ ■ ■ (Y=0)
```

---

### [Type B: 고저차 발판 & 1-Way 이동 모듈]

#### Module B1: 지그재그 1-Way 점프 (Zig-Zag One-Way Ascent)
```text
· · · E · · (Y=5) - 상단 이탈 Socket
· · ═ ═ ═ · (Y=4) - 2.0m 높이 발판
· · · · · · (Y=3)
· ═ ═ ═ · · (Y=2) - 2.0m 높이 발판
S · · · · · (Y=1)
■ ■ ■ ■ ■ ■ (Y=0)
```

#### Module B2: 하향 드롭 & 가시 회피 (One-Way Drop & Spike Avoidance)
```text
S · · · · · (Y=5) - 상단 진입
■ ■ ═ ═ · · (Y=4) - 하향 드롭 (S키+점프)
· · · · · · (Y=3)
· · · · ═ ═ (Y=2)
· · ▲ ▲ · E (Y=1) - 우측 하단 이탈
■ ■ ■ ■ ■ ■ (Y=0)
```

---

### [Type C: 벽점프 & 수직 샤프트 모듈]

#### Module C1: 수직 벽점프 샤프트 (Wall Jump Shaft)
```text
■ · · · · ■ (Y=5) - E (상단 탈출)
■ ► · · ◄ ■ (Y=4) - 벽 가시 (벽점프 정밀 조작 요구)
■ · · · · ■ (Y=3)
■ ► · · ◄ ■ (Y=2)
■ · · · · ■ (Y=1)
■ ■ S · ■ ■ (Y=0) - S (하단 진입)
```

#### Module C2: 벽점프 & 중앙 톱날 이송 (Wall Jump Saw Corridor)
```text
■ · · · · E (Y=5)
■ · · ◎ · ■ (Y=4) - 톱날 상하 이동 모터
■ ═ · · · ■ (Y=3)
■ · · · · ■ (Y=2)
S · · · · ■ (Y=1)
■ ■ ■ ■ ■ ■ (Y=0)
```

---

### [Type D: 대시 전용 & 좁은 통로 모듈]

#### Module D1: 천장 가시 & 저상 대시 통로 (Low Ceiling Dash Slide)
```text
■ ■ ■ ■ ■ ■ (Y=5)
■ ▼ ▼ ▼ ▼ ■ (Y=4) - 천장 가시
S · · · · E (Y=3) - 높이 1m 좁은 통로 (대시 3.6m 통과)
■ ■ ■ ■ ■ ■ (Y=2)
■ ■ ■ ■ ■ ■ (Y=1)
■ ■ ■ ■ ■ ■ (Y=0)
```

#### Module D2: 장거리 대시 점프 갭 (Long Dash-Jump Gap)
```text
· · · · · · (Y=5)
· · · · · · (Y=4)
S · · · · E (Y=3) - 4.0m 갭 (대시점프 Range 5.2m 필요)
■ ■ · · ■ ■ (Y=2)
■ ■ ▲ ▲ ■ ■ (Y=1) - 낙하 시 가시 대미지
■ ■ ■ ■ ■ ■ (Y=0)
```

---

### [Type E: 경사면 & 복합 함정 모듈]

#### Module E1: 1-Way 이동 발판 & 톱날 왕복 (Moving Saw Platform)
```text
· · · · · · (Y=5)
· ◎ ◄ ═ ► ◎ (Y=4) - 톱날 좌우 PingPong 이동
· · · · · · (Y=3)
S · · · · E (Y=2)
■ ▲ ▲ ▲ ▲ ■ (Y=1)
■ ■ ■ ■ ■ ■ (Y=0)
```

#### Module E2: 계단형 복합 모듈 (Staircase Combat Hazard)
```text
· · · · ═ E (Y=5)
· · · ═ · · (Y=4)
· · ═ · · · (Y=3)
S ═ · · · · (Y=2)
■ ▲ ▲ ▲ ▲ ■ (Y=1)
■ ■ ■ ■ ■ ■ (Y=0)
```

---

## 4. 유저 확인 필요 사항 (User Review Checklist)

유저님께서 씬 및 룸 제작 시 확인·결정해 주셔야 하는 핵심 체크리스트입니다:

1. ✅ **모듈 간 Socket 연결 규칙**:
   - 진입/진출 Socket (`S`, `E`)의 Y축 높이를 기본 `Y=1` 또는 `Y=2`로 표준화할지 여부.
2. ✅ **발판 통과 조작감 (`S Key + Jump`)**:
   - `OneWayPlatform` 하향 통과 시 입력을 `S Key + Jump` 콤보로 확정할지 여부.
3. ✅ **함정 피해 및 피격 반응 밸런스**:
   - 가시 함정(`15 Damage`, `Knockback 9.0m/s`)과 톱날 함정(`20 Damage`, `Knockback 11.0m/s`)의 난이도 수치 승인.
4. ✅ **Tilemap 에셋 팔레트 규격**:
   - 6x6 모듈을 Prefab으로 저장 시 Tilemap `Grid Cell Size = (1.0, 1.0, 0)` 고정 여부.
