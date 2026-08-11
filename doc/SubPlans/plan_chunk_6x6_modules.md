# [SubPlan] 12x12 스테이지 청크 모듈 템플릿 명세서 (12x12 Expanded Self-Contained Modules)

## 1. 개요 및 모듈 규격 확대 조율

본 문서는 단일 모듈 내에서 플레이어가 이동, 점프, 대시, 착지 등 **독립적인 단독 물리 액션을 100% 자유롭게 수행**할 수 있도록 모듈의 단위 크기를 기존 $6 \times 6\text{m}$에서 **$12 \times 12\text{m}$ ($12 \times 12\text{ cells}$)**로 전면 확대한 명세서입니다.

### 1.1 모듈 단위 크기 확대 조율 결과 ($12 \times 12\text{m}$)
1. **단일 모듈 이동 거리**: $6.0\text{m} \longrightarrow 12.0\text{m}$ (2초 주행, 대시 $3.6\text{m}$ 3회 연계 가능).
2. **단일 모듈 수직 높이**: $6.0\text{m} \longrightarrow 12.0\text{m}$ (수직 점프 $2.5\text{m}$ 3단 층간 입체 이동 가능).
3. **독립 자율 액션 공간 보장**:
   - 모듈 진입 시 수평 달리기 트랙 **최소 3m ~ 4m** 확보
   - 독립 점프 도약 갭 **3m ~ 4m** 및 착지대 **3m ~ 4m** 완비
   - 머리 위 천장 여유 고도 **3m ~ 4m** 보장
4. **가변 청크 내 모듈 주입 수**:
   - 기존 $6 \times 6\text{m}$ 모듈 50개 조립 ➔ **$12 \times 12\text{m}$ 모듈 $4 \times 2$ ~ $5 \times 3$ 조립** (동선 가시성 대폭 향상)

---

## 2. 12x12 자율 단독 모듈 레이아웃 구조 예시

```
Row 0  :  . . . . . . . . . . . . (Ceiling Clearance 3m)
Row 1  :  . . . . . . . . . . . .
Row 2  :  . . . . . . . . . . . .
Row 3  :  . . . . = = = = . . . . (Floating Platform 4m)
Row 4  :  . . . . . . . . . . . . (Headroom 3m)
Row 5  :  . . . . . . . . . . . .
Row 6  :  . = = = = . . . . . . . (Floating Platform 4m)
Row 7  :  . . . . . . . . . . . . (Jump Clearance 3m)
Row 8  :  S . . . . . . . . . . E (Player Track 12m)
Row 9  :  . . . . . . . . . . . .
Row 10 :  # # # # # # # # # # # # (Solid Ground)
Row 11 :  # # # # # # # # # # # #
```

---

## 3. R&R 서브에이전트 위임 절차 (Delegation Protocol)

- **메인 에이전트 (기획자 / 리드 프로그래머)**: 12x12 모듈 템플릿 명세 설계 및 `ModuleChunkBuilder.cs` C# 파서 작성.
- **리소스 작업자 1 (`f4f6cc90-75c3-4e62-890c-fcd62e9a47f7`)**: 12x12 모듈 Prefab 20종 및 가변 룸 청크 Prefab 11종 디스크 생성, Addressables 바인딩, `unityMCP` 에디터 재빌드 구동 전담.
