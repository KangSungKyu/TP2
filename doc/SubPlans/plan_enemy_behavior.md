# 몬스터 AI 패턴 및 상태 머신 명세서

## 개요
- 목표: MonsterPatternData 기반 패턴 실행의 시간/우선순위/취소 규칙을 정의하고, BossMonster/Monster 클래스의 상태머신 무결성을 보장한다.

## 핵심 인터페이스 (함수 시그니처)
- UniTask ExecutePatternAsync(uint patternIdx, CancellationToken cancellationToken = default)
- void CancelCurrentPattern()
- void EnqueuePattern(uint patternIdx)
- PatternState GetCurrentPatternState()

## 데이터 제약
- MonsterPatternData의 ExecutionType 및 RandomWeight 검증:
  - Random 모드 가중치 합이 0이면 해당 패턴 그룹은 Sequence 모드로 강제 전환.
  - PatternIdxList는 null/빈 배열일 수 없으며, 길이 최대값 N_pattern_max = 16 제한

## 실행 규칙
- 패턴 실행 주기:
  - PreDelay -> 액션(애니/이펙트) -> PostDelay 순으로 보장
  - PreDelay/PostDelay는 0.0s 이상
- Trigger 모드:
  - HpRatioUnder: 플레이어/타겟의 체력 비율을 실시간으로 비교하여 실행
  - DistanceOver/Under: 거리 계산은 유닛의 Collider 중심 기준으로 측정

## 동시성 및 취소 정책
- 개별 패턴 작업은 CancellationToken으로 취소 가능해야 하며, 취소 시 반드시 애니메이션/이펙트/피격 콜백을 안전하게 정리해야 함.
- 패턴 실행 중 우선순위가 더 높은 패턴이 트리거되면 현재 패턴은 CancelCurrentPattern 호출로 우아하게 종료.

## AI 틱 및 검사 주기
- AI는 호스트 환경의 고정 틱(FixedUpdate 또는 UniTask 기반 고정 주기)으로 동작해야 하며, 권장 폴링 간격 T_tick = 0.1s 이하.
- 긴 연산(경로탐색 등)은 별도 작업으로 분리하여 비동기 수행하고, 메인 AI 틱 루프는 블로킹하지 않는다.

## 방어적 제약
- 패턴 실행 중 예외 발생 시 로그 후 해당 패턴을 종료하고 안전 대기 상태(Idle)로 복귀
- 패턴 데이터 불일치 (SkillIdx/EffectIdx 미존재) 발생 시 즉시 스킵하고 로그 기록
