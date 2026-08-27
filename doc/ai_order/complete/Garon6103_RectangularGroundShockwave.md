# Garon6103 Rectangular Ground Shockwave

상태: `COMPLETE`
완성 자산: `doc/ai_order/complete/assets/Garon6103_RectangularGroundShockwave.png`

## 출력 명세

| 항목 | 값 |
|---|---:|
| 형식 | 투명 배경 PNG sprite sheet |
| 시점 | 2D side-view |
| PPU | 100 |
| 프레임 | 8, horizontal 8×1 |
| 프레임 셀 | 1000×176px |
| 전체 시트 | 8000×176px |
| 월드 크기 | 10m×1.76m |
| Pivot 인계값 | bottom-center `(0.5, 0)` |

## 제작 Prompt

```text
Create an 8-frame horizontal pixel-art sprite sheet of a ground-attached rectangular energy shockwave for a 2D side-view game. Each frame uses the same 1000×176 transparent canvas and bottom-center anchor. Expand symmetrically and monotonically from the horizontal center with alpha widths 10%, 25%, 45%, 70%, 100%, 100%, 100%, 100%. Hold full width for frame 5, then fade opacity in frames 6 and 7. Use only a cyan outer glow and a bright near-white center line. No circle, radial ring, vertical pillar, one-way travel, hitbox, guide box, text, arrow, cropping, or cast shadow.
```

## 프레임 계약

| Frame | Alpha bbox width | 단계 |
|---:|---:|---|
| 0 | 100px | draw |
| 1 | 250px | expand |
| 2 | 450px | expand |
| 3 | 700px | expand |
| 4 | 1000px | full width |
| 5 | 1000px | hold |
| 6 | 1000px | fade |
| 7 | 1000px | near-transparent |

판정 및 VFX 타이밍은 이미지에 내장하지 않고 런타임 수치에 위임한다.

## 검수 결과

| Frame | Alpha bbox `(L,R,W,H,B)` | 평균 alpha |
|---:|---|---:|
| 0 | `(450,549,100,176,175)` | 69.6 |
| 1 | `(375,624,250,176,175)` | 77.2 |
| 2 | `(275,724,450,176,175)` | 83.5 |
| 3 | `(150,849,700,176,175)` | 89.4 |
| 4 | `(0,999,1000,176,175)` | 89.7 |
| 5 | `(0,999,1000,176,175)` | 89.7 |
| 6 | `(0,999,1000,176,175)` | 36.9 |
| 7 | `(0,999,1000,176,175)` | 8.3 |

모든 프레임 중심 `X=499.5`, bottom `Y=175`, 수평 크롭 없음, 투명 배경 확인.
