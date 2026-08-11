# 메카닉 및 전투 서브 명세서 (Unit Combat)

## 개요
- 플레이어 입력 및 상태(짧게 누름 = 패링/솟구침, 길게 누름 = 가드 홀드)와 연계되는 상태기계 규칙을 정의한다.

## 핵심 인터페이스 (함수 시그니처)
- bool TryPlaySkillAnimation(Animator animator, uint skillId)
- UniTaskVoid ExecuteSkillDataAsync(uint skillId, Vector3 position, Quaternion rotation = default, CancellationToken cancellationToken = default)
- SkillEffect SpawnSkillEffect(string effectName, Vector3 position, Vector2 size, float damage, float lifetime, FactionType faction, Color color)

## 가드 홀드(Guard Hold) 및 패링(Parry) 상태 머신 규칙
- 패링(Parry):
  - 입력 짧게 누름(입력 지속시간 <= ParryWindowDuration = 0.15s)
  - 패링 성공 시 상대의 공격을 무효화하고 짧은 무적 프레임(0.12s) 부여
  - 패링 실패 시 일반 피격 판정

- 가드 홀드(Guard Hold):
  - 입력 길게 유지(입력 지속시간 > ParryWindowDuration)
  - 가드 상태는 MP 또는 Stamina 같은 자원 소모 검증 필요(현행 데이터 모델은 MP만 표준화된 상태)
  - 가드 유지 중 받는 피해는 Posture로 전환하여 일정 비율(예: 30%)로 경감

## 시간차 롱프레임 처리
- 다수의 히트 타이밍(HitTimings[]) 처리는 ExecuteSkillDataAsync에서 시간차로 비동기 스폰으로 처리한다.
- 방어적 제약:
  - HitTimings 간격이 0 이하(비정상)인 경우 즉시 정렬 및 중복 제거
  - 총 누적 활성 시간(ActiveDuration)이 각 히트간 합계보다 작으면 ActiveDuration을 합계+0.01초로 보정

## 애니메이션-상태 동기성
- TryPlaySkillAnimation은 Animator에 'State' int 파라미터 존재 여부를 필수로 검증한다.
  - 없다면 false 반환 및 에러 로그
  - 상태 전환은 animator.SetInteger("State", targetState)로 수행

## 방어적 제약(Fault-Tolerance)
- 모든 스킬/이펙트 생성 루틴은 null-check 및 ResourceData 존재 검증을 수행할 것
- 이펙트 prefabKey가 비어있거나 ResourceManager 호출이 실패하면 스킬 이펙트 스폰은 실패로만 로그를 남기고 런타임 예외를 발생시키지 않는다.
- 스킬 쿨다운 및 다음 사용 가능 시간 관리: nextAvailable 딕셔너리는 Time.time 기준 비교; 음수나 이상값 방지
