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

> **Superseded 범위:** 이 섹션의 공격 주체·타이밍·sweep 책임은 유지한다. 다만 체형, Body/Defense 크기, 실제 Attack bounds, 무기 길이·두께 분류 및 Unit별 크기는 아래 `Monster 체형·무기·공격 방식 단일 권위 (2026-08-24)`가 대체한다.

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
| `3102` | `6008` Charging Thrust, `6009` Barrage | 기존 hitbox placeholder를 prototype 공격 주체로 재사용 | `6008/6009` 모두 Step `10002`(`maxdistance=0.81`, `maxspeed=9.0`)를 사용한다. |
| `3103` | 해머·지면 충격 역할 | 해머 head; 충격파는 GroundImpact anchor에서 생성된 effect collider | 현 `5103→6001/6002` 역할 불일치는 데이터 선행 수정 대상 |
| `3104` | 방패 밀치기 `6003`, 내려치기 `6004` | `6003` 방패 전면, `6004` 손 무기 blade/head | 몸체·방패 후면 판정 금지 |
| `3105` | 탄환 `6005/6006` | 석궁 muzzle은 생성점만 제공; 피해 권위는 `1045` projectile collider | 근접 hitbox 생성 금지 |
| `3106` | 지면 충격 `6007` | GroundImpact anchor에서 생성된 effect collider | `6008`은 Unit `3102` 전용으로 분리 |
| `3201` | `6100–6103` | 돌진·콤보·내려찍기=`대검 blade/leading edge`; 충격파=`GroundImpact effect` | Boss body contact damage 금지 |

현재 Stage 1에는 머리·꼬리를 공격 주체로 쓰는 Unit이 없다. 향후 해당 패턴이 승인될 때 동일 collider role만 추가한다.

### 2.1 Unit3102 Prototype Dummy 공격 계약

| 연결 | 동작 계약 |
|---|---|
| `3102: 5102→6008 Charging Thrust→7005→State 14→Motion 10002` | center-contact 목표, 이동 `≤0.81m`, 속도 `≤9`; wall/ledge/spawn/target crossing clamp, 미도달 시 whiff 허용 |
| `3102: 5102→6009 Barrage→7006→State 15→Motion 10002` | Step `maxdistance=0.81`, `maxspeed=9.0`; 공격 window 2회. PrototypeDummy HP `7.5/tick`·총 `15`; Guard posture `3.75/tick`·총 `7.5`; Parry attacker posture `40` |

- PrototypeDummy clip 2개만 추가하며 기존 sprite, `FX 8001`, hitbox placeholder를 재사용한다.
- 기존 공유 `6001/6002/7001`은 수정·재배정하지 않는다.
- 프로덕션 교체 대상은 전용 sprite sheet, 전용 VFX, hitbox 궤적 계측, damage/cooldown 밸런싱이다.
- QA 상태는 compile·제품 Error 0 및 CSV/Animator 정적 확인 완료다. Unity runner는 `0/0 timeout`으로 `BLOCKED`이며 PASS로 간주하지 않는다.

### 3. hit dedupe·방향·방어 우선순위

- 동일 `(sourceId, actionGeneration, tick, target)`은 공격 주체 collider 수와 접촉 횟수에 관계없이 최대 1회 피해를 준다.
- `hitcount>1`은 서로 다른 tick만 추가 피해를 허용한다. 패링 성공 시 기존 `(sourceId, generation)` 차단으로 후속 tick 전체를 무효화한다.
- 좌·우 composite 무기와 다중 collider는 같은 source/generation/tick을 공유한다. 투사체는 projectile instance의 source와 tick을 독립 사용한다.
- 공격 facing은 Startup 시작 pose에서 snapshot하고 Active 종료까지 고정한다. collider scale을 음수로 누적하지 않는다.
- fraction이 작은 후보를 우선하며, 정면 접촉에서 차이가 `epsilon = motor.SkinWidth`, motor가 없으면 `Physics2D.defaultContactOffset` 이하일 때만 `Parry > Guard > Body`를 적용한다.
- Startup부터 overlap하여 마지막 exterior pose가 없으면 Body로 판정한다. pose 합성·역보간·Body collider fallback은 금지한다.
- 공격마다 마지막 비접촉 exterior pose 1개만 보존하고 Active에서 `exterior → current`를 sweep한다. face flip, teleport, generation 변경, 취소·사망·pool 반환 시 폐기한다.
- Chase 종료 후 Step/Lunge만 Motor를 소유하며 Active 진입 시 기본 정지한다. 명시된 이동공격만 Active 이동을 유지하고 이중 Motor writer를 금지한다.

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
7. `3102` PrototypeDummy는 `6008/6009`만 사용하고 기존 공유 `6001/6002/7001` 및 머리·꼬리 공격 주체를 변경하지 않는다.
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

## Monster 체형·무기·공격 방식 단일 권위 (2026-08-24)

### 1. 기준과 단일 권위

- Player Body `1×2`를 중형 기준으로 사용한다.
- Player 실제 Attack bounds `1.5×0.6`을 짧고 얇은 무기 기준(`PWL×PWT`)으로 사용한다.
- Body/Player 폭 비율은 소형 `<0.8`, 중형 `0.8–1.2`, 대형 `>1.2–1.8`, 특대 `>1.8`로 분류한다.
- `UnitBaseData.hitboxradius = r`이 Body/Defense bounds `2r×4r`의 단일 권위다.
- 실제 attack bounds가 무기 길이·두께 및 `1.5×sweep`의 단일 권위다. 무기 visual이 아직 분리되지 않았으므로 현 단계에서는 collider proxy로 분류한다.
- Long은 실제 attack 길이 `≥1.25 PWL`, Thick은 실제 attack 두께 `≥1.5 PWT`다.
- `SuperArmor`와 knockback은 체형에서 자동 산출하지 않고 Unit 또는 Pattern override로만 지정한다.

### 2. Unit 분류 및 실측 ratio

| Unit | 체형 | 무기·공격 방식 | Body bounds | Body/Player | Attack bounds | 길이/PWL | 두께/PWT | 분류 |
|---:|---|---|---:|---:|---:|---:|---:|---|
| `3101` | 대형 | 긴 얇은 창, 찌르기 | `1.5×3` | `1.50` | `2.2×0.45` | `1.47` | `0.75` | Long / Thin |
| `3102` | 중형 | 짧은 얇은 단검, Step 찌르기+2연타 | `1×2` | `1.00` | `1.2×0.55` | `0.80` | `0.92` | Short / Thin |
| `3103` | 대형 | 긴 heavy proxy, Torso Ram BodyPart 공격 | `1.6×3.2` | `1.60` | `2.5×0.8` | `1.67` | `1.33` | Long / Thin; 두께 `0.9` 적용 시 Thick 후보(`1.50 PWT`) |
| `3104` | 대형 | 짧고 두꺼운 방패·무기, 수직·대각 공격 | `1.5×3` | `1.50` | `1.8×1` | `1.20` | `1.67` | Short / Thick |
| `3105` | 중형 | Projectile Aim→Fire→Reload | `1×2` | `1.00` | muzzle `0.12`, melee `0` | N/A | muzzle `0.20` | Projectile / melee 없음 |
| `3201` | 특대 | 긴 두꺼운 대검, Charge·Horizontal·Vertical·Shockwave | `2×4` | `2.00` | `3×1.2` | `2.00` | `2.00` | Long / Thick |

비율은 표의 bounds를 Player 기준으로 나눈 실측값이며 추정 visual 크기가 아니다.

### 3. 공격 단계와 기본 궤적

| 단계 | Attack collider |
|---|---|
| Telegraph | OFF |
| Startup | OFF |
| Active | ON |
| Recovery | OFF |

- 수직 공격은 기본 회전각 `≥180°`를 확보한다.
- 수평 공격은 반대편 `±180°`에서 정면 방향으로 진행한다.
- 찌르기는 `pull → thrust → recover` 순서를 따른다.
- 단계와 궤적은 실제 clip·collider sweep으로 검증하며 placeholder visual의 외형으로 판정을 바꾸지 않는다.

### 4. Collider 책임

| Collider | 단일 책임 |
|---|---|
| Body | 이동·지형 충돌 |
| Defense | 피격·Guard·Parry·surface distance |
| Attack | Active window의 공격 sweep |
| Projectile | 소유 Unit의 melee collider와 분리된 독립 이동·피해·수명주기 |

### 5. P0 상충 및 금지

- Pattern `6004`와 `6006`은 clip State `5`와 Skill State `7`이 상충한다.
- State/Data를 정수 idx 권위로 일치시키기 전에는 production 승격을 금지한다.
- clip명, GameObject명, 파일명 등 문자열 fallback으로 상충을 우회하지 않는다.

### 6. 후속 DAG

`기획 승인 → 리소스 visual/weapon 분리 → Animator State·정수 데이터 동기화 → 코드 소비 → QA → production 승격`

선행 단계가 미완료면 후행 단계는 시작하지 않는다.

### 7. 완료 Assert

1. 모든 대상 Unit의 `hitboxradius`에서 계산한 `2r×4r`가 표의 Body/Defense bounds와 일치한다.
2. 실제 attack bounds를 Player `1.5×0.6`으로 나눈 길이·두께 ratio가 표와 일치한다.
3. Long/Thick 경계값 `1.25/1.5`에서 분류 누락·중복이 없다.
4. Telegraph·Startup·Recovery의 Attack collider enabled는 `0`, Active에서만 `1`이다.
5. 수직·수평·찌르기 궤적이 승인 clip의 실제 sweep과 일치하고 body fallback은 `0`이다.
6. Body·Defense·Attack·Projectile 책임이 교차하지 않으며 같은 tick 중복 피해는 `0`이다.
7. `6004/6006`의 clip State와 Skill State가 정수 데이터로 일치하고 문자열 fallback 호출은 `0`이다.
8. 체형 변경만으로 SuperArmor·knockback 값이 자동 변경되는 경로는 `0`이다.

### 8. Weapon Idle Pose 매칭 — Provisional

현재 대상 7종의 actual weapon visual은 `0/7`이므로 아래 값은 collider proxy 기반 후보다. 실제 무기 sprite 제작 후 수동 재측정·승인하기 전까지 production 권위로 사용하지 않는다.

#### Pose uint 후보

| uint | Pose | Pivot | 각도 후보 |
|---:|---|---|---|
| `0` | `RearVertical` | Defense rear | `80~100°` |
| `1` | `RearHorizontal` | Defense rear | `-10~10°`; Face Right tip/forward axis `+X` |
| `2` | `FrontDiagonalDown` | Body center | `-55~-25°` |
| `3` | `FrontDiagonalUp` | Body center | `25~55°` |

#### Unit 매핑

| Unit | 무기 분류 | Pose | 승인 전 각도 | 공격 연결 |
|---:|---|---|---|---|
| `3001` | Short / Thin sword | `2 FrontDiagonalDown` | `-45~-25°` | State7 `ReverseVerticalUpswing` |
| `3101` | Long / Thin spear | `1 RearHorizontal` | `-10~5°`, tip forward | Thrust |
| `3102` | Short / Thin dagger | `1 RearHorizontal` | `-15~5°`, tip forward | `6008/6009` 공용 |
| `3103` | Long heavy proxy | `1 RearHorizontal` | `-10~10°`, tip forward | Torso Ram idle proxy |
| `3104` | Short / Thick shield weapon | `3 FrontDiagonalUp` | `30~55°` | Bash / Overhead |
| `3105` | Projectile muzzle | `2 FrontDiagonalDown` | `-35~-20°` | Aim / Fire |
| `3201` | Long / Thick greatsword | `1 RearHorizontal` | `-15~10°` | State11 `ReverseVerticalUpswing` |

#### Pose 계약

- 기존 `AttackAttach·Visual identity` 계약을 유지한다. 최소 계층 후보는 `AttackAttach(identity) → WeaponVisual(PoseOffset)`이며 `AttackCollider`는 sibling으로 둔다.
- face left는 `AttackAttach` local X mirror만 적용한다. `WeaponVisual`의 추가 flip·negative scale은 금지하며 좌우 상대 위치는 유지한다.
- Idle에서 `AttackCollider`는 OFF이며 Body·Defense·Motor 결과에 미치는 영향은 `0`이다.
- Idle pose와 Telegraph 첫 key는 동일해야 하며 Startup은 연속 보간한다. Active 중 face 또는 pivot 변경은 금지한다.
- pose 식별에 문자열을 사용하지 않는다. 실제 데이터화가 승인되면 `uint enum`으로만 정의하며 현재 공정에서는 신규 table·idx를 생성하지 않는다.

#### 위험

- Rear pose는 좁은 통로를 가리거나 벽과 겹쳐 보일 수 있다.
- Front diagonal은 벽·지면을 침범하거나 Unit silhouette 가독성을 낮출 수 있다.
- collider proxy 각도는 actual weapon visual의 grip·길이·무게중심과 불일치할 수 있다.

#### 완료 Assert

1. 대상 7종 모두 승인된 pose uint·pivot·각도 범위와 일치하며 문자열 pose lookup은 `0`이다.
2. Idle의 `AttackCollider.enabled == false`이고 Body·Defense bounds 및 Motor 결과 변화는 `0`이다.
3. 좌우 facing에서 X mirror·rotation sign은 대칭이며 `AttackAttach` identity 누적 오차는 `0`이다.
4. Idle 마지막 pose와 Telegraph 첫 key의 위치·회전 오차는 `0`, Startup 중 불연속 snap은 `0`이다.
5. Active 동안 face·pivot 변경은 `0`이고 attack sweep은 기존 Collider 단일 권위를 유지한다.
6. actual weapon visual 제작 후 수동 재측정·승인 전 모든 매핑은 `Provisional`로 표시된다.

#### 후속 DAG

`weapon visual 제작 → pose binding → animation transition → QA`

선행 단계가 완료되지 않으면 다음 단계로 승격하지 않는다.

### 9. BodyPart AttackSubject 계약 — P0

#### AttackSubject uint 후보

| uint | AttackSubject | 판정 주체 |
|---:|---|---|
| `0` | `Weapon` | 무기·방패·CarriedObject 전용 AttackCollider |
| `1` | `BodyPart` | 공격 전용 serialized `bodyPartAttackCollider` |
| `2` | `Projectile` | 독립 projectile collider와 수명주기 |

- Shield와 CarriedObject는 `Weapon`으로 분류한다.
- P0에서는 BodyPart 세부 enum을 추가하지 않고 Unit당 serialized `bodyPartAttackCollider` 1개만 허용한다.
- Body movement collider와 Defense hit·Guard·Parry collider를 공격 판정에 재사용하지 않는다.
- 실제 데이터화 전에는 신규 table·idx를 만들지 않는다. 필요 시 기존 `DataTableType`, PK 대역, `idx/1000` routing, FK를 감사한 뒤 PM 승인으로 `uint enum`과 데이터를 함께 동기화한다.

#### 단계·수명주기

| 단계 | BodyPart AttackCollider |
|---|---|
| Telegraph | OFF |
| Startup | OFF |
| Active | ON |
| Recovery | OFF |

Idle Pose의 `AttackCollider sibling` 계약을 그대로 적용한다. `bodyPartAttackCollider`는 visual·Body·Defense와 분리된 sibling이며 face left에서 local X mirror와 sweep 방향을 함께 반전한다.

#### P0 구현 대상: Unit `3103` Torso Ram

`후퇴/숙임 → KinematicMotor Step/Lunge → Torso collider Active → Recovery`

- 이동 writer는 `KinematicMotor2D` 하나만 사용한다.
- wall, ledge, SpawnArea 이탈 또는 target crossing이 감지되면 공격 이동과 Active window를 취소한다.
- teleport 보정, self-contact damage, Body/Defense fallback을 금지한다.
- 공유 utility Pattern `6002`는 `3101/3103`이 함께 사용하므로 Torso Ram으로 전환하거나 재배정하지 않는다.
- Torso Ram은 기존 `6010/7007/8019/10002`를 유지하며 신규 Pattern·Skill·Text·Motion row를 만들지 않는다.

#### Unit `3103` 패턴 개편 승인 계약 (2026-08-26)

- `6010 Torso Ram` 예약 band는 양측 `DefenseBodyCollider`의 가까운 표면 간 수평 gap `1.5–10.0m`다. Startup 시작에 target body center/half-width와 facing을 snapshot하며 Active 중 재추적·반전하지 않는다.
- `6010`은 기존 예약 row `Motion 10003 AcceleratingLunge`를 활성화해 `maxdistance=13.1m`, `maxspeed=24m/s`, `acceleration=48m/s²`, `enabled=1`을 사용한다. Player 폭 `1.0m`, 3103 폭 `1.6m`에서 종점은 `snapshotTargetCenter + facing×(0.5+0.8+0.5)=targetCenter+facing×1.8m`; 마지막 `0.5m`는 target 반대편 표면 이후 overshoot다.
- gap `g`에서 요구 이동거리는 `g+3.1m`이므로 `1.5–10m` band는 `4.6–13.1m`다. Skill `7007`은 `HitTiming=0.88`, `Pre/Post=0.06/0.06`, `windowStart=0.82s`, Active `0.82–0.94s`다. 최대거리 연속시간 `0.796s` 뒤 FixedStep `0.02s` 1회 안정 여유를 둔다.
- root telegraph는 이동 전에 정확히 `1.5s` 1회다. 현행 `effectivePreDelay = Pattern.PreDelay - windowStart` 계약에 따라 `6010.predelay=2.32s`로 저장하며, 이동과 합산한 Active 진입은 Pattern 시작 후 `2.32s`다. 종료 즉시 속도 `0`, recovery `0.6s`를 적용한다.
- 50Hz 모사 기준 gap `1.5/10m`에서 target 근접면 통과는 이동 시작 후 약 `0.32/0.68s`, 최종 종점은 `0.82s` 이내다. 플레이어의 최초 시각 반응시간은 약 `1.82–2.18s`이며 damage commit은 Active 진입 시 1회다.
- 6010은 명시 Active 이동공격 예외다. 전 구간 KinematicMotor 단일 writer와 swept cast를 사용하며 wall·ledge·SpawnArea/leash 경계에서 즉시 정지하고 남은 hit를 취소한다. 완전 통과 종점이 bounds 안에 없으면 Startup 전에 예약을 취소하며 clamp·teleport·재조준하지 않는다.
- `8019`도 Effect 판정 권위이므로 확대 대상에 포함한다. damage `15`, cooldown `2`, chase timeout `1`은 유지한다.
- `6011 Charging Thrust`는 `5103.patternidxlist`에서 제거하고 전역 Pattern 행을 삭제한다. 감사 기준 다른 Unit 소비자는 없으며 `8018`을 `6012`로 이관한 뒤 잔여 런타임 FK는 `0`이어야 한다.
- 모든 연속공격은 seconds 기반 등박자를 기본으로 하며 선형 `nextpatternidx`로만 연결한다. 링크 `postdelay`는 최소 `0.05s`, 예외 휴지는 해당 선행 Pattern의 명시값으로만 허용한다.
- 3103 chain은 `6012→6013→6014→0`이다. 각 Pattern은 독립 Skill·Effect·exterior sweep을 소유하며 조건 분기와 PatternStepData는 사용하지 않는다.
- `6012/7009/8018`: Vertical Slash I, damage `4`, guard posture `2`, surface gap `0–1.5m`, Stationary `10001`, startup/active/recovery `0.09/0.12/0.05s`, `nextpatternidx=6013`.
- `6013/7017/8033`: Thrust, damage `5`, guard posture `2.5`, surface gap `0–1.905m`, Step `10002(0.81m,9m/s)`, startup/active/recovery `0.075/0.095/0.15s` 뒤 명시 rest `0.375s`(`postdelay=0.525`), `nextpatternidx=6014`. `8033`은 `Resource 1099`와 `8032` raw bounds를 재사용한다.
- `6014/7018/8034`: Vertical Slash II, damage `6`, guard posture `3`, surface gap `0–2.0m`, Step `10004(0.405m,4.5m/s)`, startup/active/recovery `0.125/0.135/0.60s`, `nextpatternidx=0`. `8034`는 `Resource 1085` visual을 재사용하되 독립 Effect 행과 scale `3.018867924528302`를 사용한다.
- 모든 공격 Effect의 `SpawnPivot=(0.8,0)m`는 Body `1.6×2.0m`의 전방 표면이며 scale을 적용하지 않는다. FaceLeft에서는 SpawnPivotX와 ActiveCenterX만 부호 반전한다.
- `8018`은 scale `2.34375`, raw center `(0.100,0.135)`, raw size `(0.720,0.890)`, Box다. 최종 world center/size는 `(1.034375,0.316406)/(1.6875,2.085938)m`다.
- `8033`은 scale `2.34375`, raw center `(0.050,0.003333333)`, raw size `(0.950,0.250)`, Box다. 최종 world center/size는 `(0.917188,0.007812)/(2.226563,0.585938)m`다.
- `8034`는 scale `3.018867924528302`, `8018` raw bounds, Box다. 최종 world center/size는 `(1.101887,0.407547)/(2.173585,2.686792)m`다.
- `8019`는 scale `1.98419834046611`, raw center `(0.100101,0.00157)`, raw size `(0.806371,0.440269)`, Capsule다. 최종 world center/size는 `(0.998620,0.003115)/(1.600000,0.873581)m`다.
- 신규 uint 후보는 Pattern `6013/6014`, Skill `7017/7018`, Effect `8033/8034`, Motion `10004`, Text `2038/2039`다. Resource는 기존 `1099/1085`를 재사용하며 문자열 key와 신규 Resource 행은 `0`이다.
- chain 총 damage는 기존 위협도와 같은 `15`, 총 guard posture는 `7.5`, MP는 각 Skill `5`, chain cooldown commit은 첫 성공 Pattern에서 1회만 수행한다. Parry·groggy·취소·사망·pool return·generation 변경 시 남은 링크를 즉시 폐기한다.

#### Unit 매핑

| Unit | AttackSubject | P0 역할 | 판정 collider |
|---:|---|---|---|
| `3001` | Weapon | sword 공격 | weapon AttackCollider |
| `3101` | Weapon | spear thrust | weapon AttackCollider |
| `3102` | Weapon | dagger `6008/6009` | weapon/prototype AttackCollider |
| `3103` | BodyPart | Torso Ram | serialized `bodyPartAttackCollider` |
| `3104` | Weapon | shield Bash·Overhead | shield/weapon AttackCollider |
| `3105` | Projectile | Aim·Fire | projectile collider; muzzle는 spawn reference |
| `3201` | Weapon | greatsword·Shockwave 기점 | weapon AttackCollider; Shockwave는 독립 effect collider |

#### 구현 DAG

`기획 승인 → 8018 이관·8033 Effect FK → 6011 제거·6012/7009 갱신 → Animator 3타 동기화 → Active sweep → QA`

Motion row는 리소스 직렬화와 실제 궤적 계측이 완료된 뒤 별도 승인으로 DAG에 추가한다.

#### 완료 Assert

1. Weapon·BodyPart·Projectile role이 정수 후보와 일치하고 문자열 subject lookup은 `0`이다.
2. `bodyPartAttackCollider`는 Body·Defense와 다른 serialized collider이며 참조·bounds 재사용은 `0`이다.
3. Telegraph·Startup·Recovery에서 collider enabled는 `0`, Active에서만 `1`이다.
4. 좌우 facing에서 Torso bounds·sweep 거리·피해 결과가 대칭이고 허위 장거리 sweep은 `0`이다.
5. 15/60 FPS에서 Active window 실행 수와 hit 결과가 동일하며 누락·중복은 `0`이다.
6. wall·ledge·SpawnArea·target crossing 취소 다음 FixedStep에 Motor velocity, collider, token 잔존은 `0`이다.
7. pool 재사용 10회 후 collider 기본 OFF, PoseOffset·mirror 누적 오차, 이전 owner/generation 잔존은 모두 `0`이다.
8. teleport와 self-contact damage 호출은 `0`이며 공유 `6002`의 PK·FK·소비 Unit은 변경되지 않는다.

### 🧠 [GameDesigner 자율 회고]
- 기획 무결성 비판: 한 FixedStep sweep은 빠른 회전 무기의 곡선 궤적을 직선으로 근사하므로 blade 끝이 넓은 호를 그리는 공격에서 오탐 또는 누락 가능성이 있다.
- 차기 방어 지침: P0는 기존 timing당 1 sweep으로 제한하고 실제 15 FPS 검증에서 누락된 승인 공격만 동일 tick 내 기존 animation sample 사이를 분할하되 신규 전역 시스템은 만들지 않는다.
- 2026-08-24 회고: placeholder visual은 제작 편의를 위한 표현일 뿐 Body·Defense·Attack·Projectile collider 권위와 결합하지 않는다. visual 교체가 판정 수치나 책임 경계를 암묵적으로 바꾸지 않도록 실제 bounds와 정수 데이터를 별도로 검증한다.
- 2026-08-24 Pose 회고: collider proxy 기반 Idle pose는 visual 제작 전 임시 가독성 가설이다. 실제 무기 sprite의 grip·길이·무게중심을 수동 측정하기 전에는 collider 위치나 공격 판정을 pose에 종속시키지 않는다.
- 2026-08-24 BodyPart 회고: 몸통 공격도 Body·Defense를 편의상 재사용하면 이동·피격·공격 책임이 결합된다. 전용 serialized collider 1개와 Active-only 수명주기로 제한하고 세부 enum·Motion 수치는 실제 계측 전까지 추가하지 않는다.
