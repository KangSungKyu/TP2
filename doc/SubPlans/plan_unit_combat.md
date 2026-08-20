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

## 공격 주체별 애니메이션 히트박스 P0 계약 (2026-08-19)

### 1. 판정 권위

- Animation Event는 추가하지 않는다. 기존 `SkillData.hittimings`, `hitcount`, `activeduration`과 `MonsterPatternData.skillidx`를 단일 타이밍 권위로 유지한다.
- 각 hit timing에서 몸체가 아니라 공격 주체 collider의 직전 FixedStep pose→현재 pose를 `AttackSweep2D`로 검사한다.
- 활성 window는 해당 hit timing이 포함된 FixedStep 1회이며, 15 FPS에서 여러 timing이 경과해도 tick 순서대로 모두 처리하고 합치지 않는다.
- 공격 주체 child GameObject와 Collider2D는 활성 window 진입 직전에만 함께 ON, 해당 FixedStep 판정 직후 `finally`에서 함께 OFF한다. 대기·전조·후딜에는 OFF다.
- 공격 주체가 누락·비활성·소유 Unit 밖이면 body fallback을 금지하고 해당 tick만 무효 처리한다.

### 2. Unit별 공격 주체

| Unit idx | 공격·데이터 | P0 공격 주체 | 비고 |
|---:|---|---|---|
| `3001` | 기본 검격 `7001/7003` | 우측 손 도검의 blade collider | Guard/Parry collider와 분리 |
| `3101` | 창 찌르기 `6001` | 피스톤 창의 shaft 제외 tip/blade collider | 이동 `6002`는 공격 hitbox 없음 |
| `3102` | 쌍검 공격 | 좌·우 단검 blade collider를 한 composite source로 취급 | 같은 tick에 두 날이 닿아도 1회 |
| `3103` | 해머·지면 충격 역할 | 해머 head; 충격파는 GroundImpact anchor에서 생성된 effect collider | 현 `5103→6001/6002` 역할 불일치는 데이터 선행 수정 대상 |
| `3104` | 방패 밀치기 `6003`, 내려치기 `6004` | `6003` 방패 전면, `6004` 손 무기 blade/head | 몸체·방패 후면 판정 금지 |
| `3105` | 탄환 `6005/6006` | 석궁 muzzle은 생성점만 제공; 피해 권위는 `1045` projectile collider | 근접 hitbox 생성 금지 |
| `3106` | 지면 충격 `6007` | GroundImpact anchor에서 생성된 effect collider | 문서상 `6008`은 현재 CSV 부재이므로 활성화 금지 |
| `3201` | `6100–6103` | 돌진·콤보·내려찍기=`대검 blade/leading edge`; 충격파=`GroundImpact effect` | Boss body contact damage 금지 |

현재 Stage 1에는 머리·꼬리를 공격 주체로 쓰는 Unit이 없다. 향후 해당 패턴이 승인될 때 동일 collider role만 추가한다.

### 3. hit dedupe·방향·방어 우선순위

- 동일 `(sourceId, actionGeneration, tick, target)`은 공격 주체 collider 수와 접촉 횟수에 관계없이 최대 1회 피해를 준다.
- `hitcount>1`은 서로 다른 tick만 추가 피해를 허용한다. 패링 성공 시 기존 `(sourceId, generation)` 차단으로 후속 tick 전체를 무효화한다.
- 좌·우 composite 무기와 다중 collider는 같은 source/generation/tick을 공유한다. 투사체는 projectile instance의 source와 tick을 독립 사용한다.
- face direction 변경은 Unit root/Visual flip과 같은 프레임에 공격 주체 local X·sweep 방향을 한 번만 반전한다. collider scale을 음수로 누적하지 않는다.
- 기존 `DoesGuardIntersectFirst`의 sweep fraction을 유지하여 공격 진행 방향에서 guard/parry collider가 body collider보다 먼저, 그리고 유효 거리 epsilon보다 앞서 접촉한 경우에만 방어가 우선한다.
- guard와 body가 같은 fraction이거나 sweep 길이가 `0`이면 방어 우선으로 승격하지 않고 body 판정을 사용한다.

### 4. 근접·투사체 경계와 강제 종료

- 근접 collider는 소유 Unit의 action generation과 hit timing에만 활성화하고 발사체 수명·pool에 등록하지 않는다.
- projectile `1045`는 `MonsterProjectile2D`의 collider cast·owner generation·pool return을 그대로 사용하며 근접 공격 주체 시스템에서 재판정하지 않는다.
- 충격파 effect는 owner generation과 tick dedupe를 공유하되 Unit body 또는 무기 collider와 같은 tick에 중복 피해를 주지 않는다.
- death, groggy, SpawnArea Return, owner disable, Door/Portal generation 변경 시 활성 window를 즉시 취소하고 모든 공격 주체 GameObject·Collider를 같은 tick에 OFF한 뒤 active effect/projectile을 기존 pool로 반환한다.

### 4.1 전역 개발 시각화

- 기존 `UnitBase.DebugHitboxLine`의 `LineRenderer` 형식과 `UNITY_EDITOR || DEVELOPMENT_BUILD` 경계를 재사용한다.
- 전역 `DebugVisualizationEnabled` 1개만 사용하며 기본값은 `Debug.isDebugBuild`: Editor/Development 기본 ON, Production/비개발 빌드 기본 OFF다.
- 별도 manager·유닛별 toggle·매프레임 검색을 금지한다. 공격 주체 공용 component가 bind 시 전역값을 읽고 변경 이벤트 1회에만 반영한다.
- 시각화 line은 판정 GameObject의 활성 window와 함께 표시·숨김하며, 디버그 OFF여도 실제 collider 활성 규칙과 피해 결과는 변하지 않는다.
- 비개발 빌드는 debug line/material 생성과 Gizmo 경로를 컴파일·실행하지 않는다.

### 5. Attach·Visual identity

- `UnitRoot`, `Visual`, 해부학적 `Attach` transform은 local position `0`, rotation identity, scale `1`을 유지한다.
- 무기별 grip/회전/scale 보정은 `Attach/PoseOffset/AttackSubject` 계층의 `PoseOffset`에 bind 시 1회 적용하며 Visual·Attach 자체를 수정하지 않는다.
- attach point는 문자열 bone명으로 조회하지 않고 정수 enum/`uint` 참조 또는 Prefab 직렬화 참조로 bind한다. 매프레임 `Find/GetComponent`를 금지한다.
- face flip은 `PoseOffset` 아래 visual과 attack collider가 함께 따르며, VFX만 이동하고 collider가 남는 구성은 납품 거부한다.

### 6. 메인프로그래머 발주서(30줄 이하)

1. 기존 `SkillExecutor.ExecuteSkillHitsAsync`의 root→target 가상 sweep을 serialized 공격 주체 pose sweep으로 교체한다.
2. 공용 attack source component 1개만 Unit 하위에 두고 collider role·owner·generation·tick을 bind한다; manager는 추가하지 않는다.
3. `SkillData.hittimings/activeduration`을 그대로 읽고 Animation Event·신규 CSV 필드를 추가하지 않는다.
4. overlap/cast 결과를 기존 `CombatStats.TakeDamage(...attackSweep)`에 전달하여 guard/parry/body 우선과 dedupe를 재사용한다.
5. composite collider는 동일 source/generation/tick을 공유하고 target별 첫 접촉만 전달한다.
6. `3105`와 모든 projectile pattern은 기존 `MonsterProjectile2D` 경로로 조기 분기한다.
7. face 변경 시 이전 pose를 폐기하지 말고 동일 방향 공간으로 변환해 반전 순간의 허위 장거리 sweep을 막는다.
8. death/groggy/Return/disable/RoomGeneration/ZoneGeneration 변경은 기존 action generation 증가 지점에서 즉시 disable한다.
9. 공격 주체 누락 시 body fallback·임시 collider 생성·Transform 이름 검색 없이 tick skip과 unit/skill idx 오류만 기록한다.
10. window 진입에서 child GameObject→Collider 순으로 ON하고 판정 종료·예외·취소의 공통 `finally`에서 Collider→GameObject 순으로 OFF한다.
11. 기존 `DebugHitboxLine` 렌더 방식을 공용 공격 주체에 재사용하고 static 전역 toggle 기본값을 `Debug.isDebugBuild`로 설정한다.

### 7. 리소스작업자 발주서(30줄 이하)

1. Unit `3001`, `3101–3106`, `3201` Prefab에 표 2의 공격 주체 child와 trigger collider를 직렬화한다.
2. Root/Visual/Attach는 local identity를 유지하고 grip 보정은 PoseOffset 1계층에만 bake한다.
3. collider는 실제 blade/tip/head/shield face 형상만 감싸며 Unit body·손잡이·장식·trail을 포함하지 않는다.
4. `3102` 좌우 단검은 collider 2개를 한 composite role로, `3105` muzzle은 projectile spawn reference로만 납품한다.
5. `3103/3106/3201` GroundImpact는 지면 접촉 anchor와 effect collider reference를 분리한다.
6. 좌우 facing에서 visual과 collider의 상대 위치가 동일하게 반전되는지 Scene gizmo로 검수한다.
7. 현 데이터에 없는 `6008` 및 머리·꼬리 공격 주체는 제작하지 않는다.
8. texture·Animator 원본을 덮어쓰지 않고 Prefab 직렬화 변경만 별도 인계한다.
9. 모든 공격 주체 child와 Collider는 Prefab 저장 기본 OFF로 납품하고, 개발 시각화 line도 같은 child 아래에 둔다.
10. 유닛별 toggle·독립 debug material을 만들지 않고 기존 `DebugHitboxLine` 재질·색상 규칙을 공유한다.

### 8. QA 발주서(30줄 이하)

1. 모든 Unit/공격 tick에서 body만 접촉하고 공격 주체가 닿지 않으면 피해 `0`을 검증한다.
2. 공격 주체 sweep이 guard→body 순으로 교차하면 Guard/Parry, body→guard 또는 동시 접촉이면 body 결과를 검증한다.
3. 좌우 facing 각각 같은 거리·tick·피해 결과이며 반전 프레임 허위 sweep `0`을 검증한다.
4. `7003/7011` 다단 공격은 tick별 1회, 같은 tick composite 중복 `0`, 패링 후 후속 tick 피해 `0`을 검증한다.
5. projectile `1045`는 muzzle/Unit body 접촉 피해 `0`, projectile collider 접촉만 1회 피해를 검증한다.
6. death/groggy/Return/disable/Door/Portal 전환 직후 collider enabled `0`, effect/projectile/token 잔존 `0`을 검증한다.
7. 15/60 FPS에서 모든 `hittimings` 실행 수가 `hitcount`와 같고 프레임 누락·합산 tick이 없음을 검증한다.
8. Attach/Visual identity와 PoseOffset 단일 적용, pool 재사용 10회 후 위치·scale 누적 오차 `0`을 검증한다.
9. 전조·후딜·Idle에는 모든 공격 child/Collider enabled `0`, 각 window에서만 `1`, 정상 종료·예외·취소 다음 FixedStep에는 `0`을 검증한다.
10. Editor/Development 기본 시각화 ON, 전역 OFF 즉시 전체 숨김, Production 기본 OFF 및 debug object/material 생성 `0`을 검증한다.

### 🧠 [GameDesigner 자율 회고]
- 기획 무결성 비판: 한 FixedStep sweep은 빠른 회전 무기의 곡선 궤적을 직선으로 근사하므로 blade 끝이 넓은 호를 그리는 공격에서 오탐 또는 누락 가능성이 있다.
- 차기 방어 지침: P0는 기존 timing당 1 sweep으로 제한하고 실제 15 FPS 검증에서 누락된 승인 공격만 동일 tick 내 기존 animation sample 사이를 분할하되 신규 전역 시스템은 만들지 않는다.
