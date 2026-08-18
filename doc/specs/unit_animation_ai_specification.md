# TP2 유닛별 AI 애니메이션 제작 명세서

- 기준일: 2026-08-18
- 기준 콘셉트: Gothic Clockwork Steam Dark Fantasy
- 기준 샘플링: 12 FPS
- 용도: 외부 이미지·영상 생성 AI 및 3D 모션 생성 AI 작업 지시
- 참조: `concept_design_conference.md`, `concept_art_gallery.md`, `plan_unit_combat.md`, `player_256_animation_sheets_specification.md`

## 1. 공통 생성 계약

```yaml
canvas:
  frame_size: 256x256
  background: transparent
  camera: fixed orthographic side view
  full_body_visible: true
  ground_line_fixed: true
animation:
  sample_rate_fps: 12
  chronological_frames: true
  preserve_identity: true
  preserve_weapon_scale: true
  preserve_hand_grip: true
  preserve_facing_direction: true
effects:
  character_sheet_only: true
  exclude_vfx: true
  exclude_projectiles: true
```

모든 생성 요청에 해당 유닛의 콘셉트 이미지를 Identity Anchor로 첨부한다. 먼저 준비·접촉·후속·회수 핵심 포즈를 승인한 뒤 중간 프레임을 생성한다.

### 공통 네거티브 프롬프트

```text
extra limbs, missing limbs, changing costume, changing weapon, extra weapon,
bent weapon, shrinking weapon, floating weapon, broken grip, mirrored character,
inconsistent proportions, perspective change, camera movement, cropped body,
feet sliding, floating feet, changing ground height, root drift, random spin,
duplicate frames, wrong frame order, motion blur, afterimage, particles,
projectile, attack trail, background, text, UI, watermark
```

## 2. 공통 동작과 유닛 전용 동작의 구분

`Idle`, `Run`, `Jump_Start`, `Jump_Loop`, `Fall`, `Land`는 공통 상태명만 공유한다. 아래 명세는 무기·체형·전투 역할 때문에 유닛마다 달라져야 하는 동작만 정의한다.

## 3. Unit_3001 — Puppet Hunter

참조 이미지: `doc/images/concepts/Player_Concept_Gothic.png`

고정 외형: 암갈색 고딕 롱코트, 왼팔 황동·흑철 금속 의수, 오른손 태엽 톱날 편수도, 등 뒤 증기 배기 밸브. 움직임은 짧고 정확한 검술과 기계 관절의 묵직한 정지를 결합한다.

| 동작 | 프레임 | Loop | AI 동작 서술 |
|---|---:|:---:|---|
| `Attack_01` | 8 | N | 검을 후방 허리 높이로 짧게 당긴 뒤 골반과 어깨를 함께 회전해 전방을 한 번 수평으로 벤다. 4프레임에서 검이 가슴 아래의 접촉점을 통과하고, 5프레임 후속 궤적 뒤 즉시 회수한다. 양발은 접지한다. |
| `Attack_02` | 10 | N | 검을 후방 무릎 높이까지 낮추고 뒷발로 지면을 밀어 저점에서 전방 상단으로 45도 올려벤다. 6프레임이 접촉 자세이며 검은 앞쪽 어깨 위에서 멈춘다. 점프와 전신 회전은 금지한다. |
| `Charge` | 6 | Y | 검을 몸 앞의 낮은 위치에서 안정시키고 금속 의수로 톱날의 태엽 장치를 조작한다. 검의 미세 진동이 손목과 코트로 전달되지만 발과 검 중심은 고정한다. |
| `Guard_Start` | 3 | N | 금속 의수를 전방에 올리고 검의 옆면을 의수 가까이에 세워 신체 정면을 가리는 교차 방어 자세로 전환한다. |
| `Guard_Loop` | 4 | Y | 금속 의수와 검을 캐릭터 전방에 유지하고 무릎과 팔꿈치만 미세하게 움직인다. 방어면과 발 위치는 고정한다. |
| `Guard_Hit` | 4 | N | 전방 충격으로 의수와 검이 몸쪽으로 짧게 밀리지만 뒷발과 허리로 충격을 받아내고 마지막 프레임에 Guard_Loop로 복귀한다. |
| `Parry` | 6 | N | 공격 도달 순간 의수와 검의 옆면을 전방 바깥쪽으로 짧게 밀어 무기를 쳐낸다. 4프레임만 접촉 자세로 사용하고 마지막은 즉시 반격 가능한 자세로 끝낸다. |
| `Dodge` | 6 | N | 머리와 가슴을 공격선에서 먼저 빼는 짧은 후방 스텝. 검은 몸 가까이에 접고 코트가 한 박자 늦게 따라온다. 구르기와 공중제비는 금지한다. |
| `Dash` | 6 | N | 몸을 낮추고 뒷발과 등 뒤 밸브 추진으로 전방에 급가속한다. 검은 몸 뒤에 낮게 유지하고 코트는 반대 방향으로 펼쳐진다. 증기 표현은 별도 VFX다. |
| `Execution` | 16 | N | 1~3f 한 걸음 빠르게 접근해 금속 의수로 대상의 목/어깨를 강하게 움켜쥐어 고정, 4~7f 태엽 톱날 도검을 대상 몸통 중심에 깊숙이 직선 찌르기, 8~12f 의수 톱니를 회전시키며 도검 톱날 구동 및 압력 버티기, 13~15f 검을 같은 선으로 힘있게 뽑아내며 후속 베기 궤적, 16f 절도 있는 납도/기본 대기세 마감. (시네마틱 타격 연출) |
| `Executed` | 16 | N | 1~4f 상대의 접근과 잡기에 금속 의수를 뻗어 결사적으로 저항하지만 고정당함, 5~8f 치명타 관통 충격으로 눈과 흉부 코어가 흔들리며 상체가 굳어짐, 9~12f 무릎이 꺾이고 고개가 떨궈지며 힘이 급격히 빠짐, 13~15f 검과 팔을 지면에 떨어뜨리며 앞으로 무너짐, 16f 지면에 엎어진 채 완전히 정지 (Death 정지 자세로 자연스럽게 연결). |
| `Hit` | 4 | N | 팔과 검이 먼저 흔들리고 이어서 어깨와 코트가 뒤로 젖혀지는 짧은 피격 반응. 발은 접지한다. |
| `Groggy` | 8 | Y | 한쪽 무릎이 거의 꺾이고 검 끝이 지면 가까이 내려가며 금속 의수 관절이 불안정하게 떨린다. 이동과 공격 준비 동작은 금지한다. |
| `Death` | 10 | N | 의수 동력이 먼저 끊겨 팔과 검이 처지고 한쪽 무릎을 짚은 뒤 옆으로 무겁게 쓰러져 완전히 정지한다. |

`Aim`, `Shoot`, `Cast_Start`, `Cast_Loop`, `Cast_End`는 현재 플레이어 무기·스킬 계약이 없어 제작 대상에서 제외한다.

## 4. Unit_3101 — SpearSentry

참조 이미지: `doc/images/concepts/Monster_3101_SpearSentry_Concept.png`

고정 외형: 황동 마스크, 낡은 파수병 제복, 신축식 피스톤 스피어. 움직임은 정렬된 군인형처럼 직선적이고 반복적이다.

| 동작 | 프레임 | Loop | AI 동작 서술 |
|---|---:|:---:|---|
| `Attack_01` | 8 | N | 창끝을 목표 흉부 높이에 정렬하고 뒷다리를 압축한 뒤 피스톤이 전개되며 전방을 한 번 직선 찌르기 한다. 5프레임 접촉 후 같은 선으로 회수한다. |
| `Attack_02` | 10 | N | 첫 찌르기 회수 직후 앞발을 반 보 전진하며 더 긴 피스톤 재찌르기를 수행한다. 창끝 높이와 방향은 흔들리지 않는다. |
| `Charge` | 6 | Y | 창 내부 피스톤을 압축하며 몸과 창이 미세하게 진동한다. 창끝은 목표를 계속 가리킨다. |
| `Hit` | 4 | N | 창축이 먼저 들리고 상체가 짧게 뒤로 밀리지만 파수병 자세를 유지한다. |
| `Groggy` | 8 | Y | 창을 지지대처럼 지면에 대고 피스톤 압력이 빠지며 상체가 반복적으로 내려앉는다. |
| `Death` | 10 | N | 창을 놓치지 않은 채 무릎이 꺾이고 창축을 따라 몸이 옆으로 쓰러진다. |

## 5. Unit_3102 — ShadowStalker

참조 이미지: `doc/images/concepts/Monster_3102_ShadowStalker_Concept.png`

고정 외형: 경량 스팀 암살 인형, 와이어와 톱니 날개 장치, 톱니 쌍단검. 다른 유닛보다 낮은 중심과 빠른 방향 전환을 사용한다.

| 동작 | 프레임 | Loop | AI 동작 서술 |
|---|---:|:---:|---|
| `Attack_01` | 6 | N | 낮게 접근해 앞손 단검으로 아래에서 위로 짧게 벤다. 접촉 후 몸을 지나치게 회전하지 않는다. |
| `Attack_02` | 8 | N | 첫 베기 반대쪽 단검으로 상단에서 하단을 교차해 베고 즉시 뒤로 빠질 자세를 만든다. 두 접촉 프레임은 분리한다. |
| `Dodge` | 6 | N | 상체를 낮춘 채 후방으로 빠르게 미끄러지며 두 단검을 몸 앞에 유지한다. 구르지 않는다. |
| `Dash` | 6 | N | 날개형 와이어 장치가 접히고 지면 가까이 순간 가속해 대상 바로 앞에서 정지한다. |
| `Hit` | 4 | N | 가벼운 몸체가 크게 흔들리지만 한 발을 뒤로 짚어 즉시 균형을 회복한다. |
| `Groggy` | 8 | Y | 양팔과 날개 장치가 처지고 낮은 자세에서 좌우로 불규칙하게 흔들린다. |
| `Death` | 8 | N | 와이어 장력이 끊기며 몸이 접히듯 앞으로 무너지고 단검이 지면에 닿는다. |

## 6. Unit_3103 — WaveHeavy

참조 이미지: `doc/images/concepts/Monster_3103_WaveHeavy_Concept.png`

고정 외형: 보일러 내장 중장갑 골렘, 거대 증기 분쇄 해머. 모든 공격은 긴 예비 동작과 큰 관성 회수를 사용한다.

| 동작 | 프레임 | Loop | AI 동작 서술 |
|---|---:|:---:|---|
| `Attack_01` | 12 | N | 해머를 머리 위로 천천히 들어 올린 뒤 양발을 고정하고 전방 지면에 수직으로 내리친다. 8프레임이 지면 접촉이며 충격파는 별도 VFX다. |
| `Attack_02` | 12 | N | 해머 머리를 지면 가까이 끌어 후방에서 전방으로 한 번 넓게 횡스윙한다. 골반 회전은 크지만 발은 지면을 벗어나지 않는다. |
| `Charge` | 8 | Y | 등과 몸통 보일러 압력이 올라가며 해머를 양손으로 고정한다. 몸통 팽창과 진동만 반복한다. |
| `Hit` | 4 | N | 일반 피격은 장갑과 해머만 짧게 흔들리고 몸 중심은 거의 이동하지 않는다. 슈퍼아머 상태와 호환되는 작은 반응이다. |
| `Groggy` | 10 | Y | 해머 머리를 지면에 두고 양손으로 손잡이에 기대며 보일러 압력이 불안정하게 몸을 흔든다. |
| `Death` | 12 | N | 보일러 동력이 꺼지고 해머가 먼저 지면에 떨어진 뒤 무릎과 몸통이 순차적으로 무너진다. |

## 7. Unit_3104 — ShieldSentinel

참조 이미지: `doc/images/concepts/Monster_3104_ShieldSentinel_Concept.png`

고정 외형: 철문 형태의 대형 증기 방패를 든 중장 수호 인형. 방패가 항상 몸보다 먼저 움직이며 정면 실루엣을 지배한다.

| 동작 | 프레임 | Loop | AI 동작 서술 |
|---|---:|:---:|---|
| `Attack_01` | 8 | N | 방패를 몸쪽으로 짧게 당긴 뒤 앞발을 내딛고 방패 면 전체로 전방을 한 번 밀쳐낸다. 5프레임이 접촉 자세다. |
| `Attack_02` | 10 | N | 방패 하단을 낮춘 뒤 대각선 위로 들어 올리는 방패 어퍼컷을 수행한다. 방패가 몸에서 분리되거나 회전하지 않는다. |
| `Guard_Start` | 4 | N | 방패를 지면에 가깝게 세우고 몸 전체를 뒤에 숨기는 정면 방어 자세로 전환한다. |
| `Guard_Loop` | 4 | Y | 방패 중심을 고정하고 무릎과 어깨만 미세하게 압축한다. 얼굴과 몸통을 과도하게 노출하지 않는다. |
| `Guard_Hit` | 5 | N | 방패가 충격으로 몸쪽에 밀리고 뒷발이 지면을 버틴 뒤 원래 방어각으로 복구된다. |
| `Hit` | 4 | N | 비방어 방향 피격에서만 상체가 짧게 틀어지고 방패 하단은 접지를 유지한다. |
| `Groggy` | 8 | Y | 방패가 옆으로 열려 몸통이 노출되고 한쪽 무릎을 꿇은 채 손잡이에 기대어 흔들린다. |
| `Death` | 12 | N | 방패가 먼저 옆으로 쓰러지고 몸체가 그 반대 방향으로 무너지며 완전히 정지한다. |

## 8. Unit_3105 — OrbitalMarksman

참조 이미지: `doc/images/concepts/Monster_3105_OrbitalMarksman_Concept.png`

고정 외형: 렌즈형 기계 눈, 태엽 조준경, 연발 석궁 또는 총열. 탄환과 조준선은 캐릭터 시트에서 제외한다.

| 동작 | 프레임 | Loop | AI 동작 서술 |
|---|---:|:---:|---|
| `Aim` | 6 | Y | 총열을 어깨 높이에 고정하고 렌즈형 눈과 조준경을 같은 축에 정렬한다. 호흡 대신 기계식 미세 보정만 반복한다. |
| `Shoot` | 6 | N | 1~3프레임 조준을 유지하고 4프레임 발사 반동으로 총열과 어깨가 짧게 뒤로 밀린 뒤 6프레임에 Aim으로 복귀한다. 투사체는 별도 객체다. |
| `Attack_02` | 10 | N | 총열을 재장전하며 태엽 레버를 당긴 뒤 두 번째 발사를 준비하는 동작. 실제 다단 발사는 동일 Shoot 클립과 타이밍 이벤트를 재사용한다. |
| `Dodge` | 6 | N | 총열을 몸 가까이 접고 후방으로 한 번 빠르게 이동해 사격 거리를 확보한다. |
| `Hit` | 4 | N | 조준축이 크게 벗어나고 렌즈와 총열이 흔들린 뒤 중립 자세로 돌아온다. |
| `Groggy` | 8 | Y | 총열이 지면을 향하고 렌즈 초점과 태엽 장치가 불안정하게 떨린다. |
| `Death` | 10 | N | 총열이 먼저 내려가고 렌즈가 꺼진 뒤 몸체가 뒤쪽으로 접히며 쓰러진다. |

## 9. Unit_3201 — Clockwork Commander Garon

참조 이미지: `doc/images/concepts/Garon_Concept_Gothic.png`

고정 외형: 플레이어보다 훨씬 큰 황동 중갑 기사, 등 뒤 3연장 증기 보일러, 양손 증기 대검. Phase 2에서는 자세가 낮아지고 동작 간 회수 시간이 짧아지지만 체형과 무기는 바뀌지 않는다.

| 동작 | 프레임 | Loop | AI 동작 서술 |
|---|---:|:---:|---|
| `Attack_01` | 14 | N | 대검을 오른쪽 어깨 뒤로 크게 들어 올리고 전방 지면을 향해 사선으로 내려친다. 9프레임 접촉 후 검의 무게를 이용해 천천히 회수한다. |
| `Attack_02` | 16 | N | 대검 끝을 지면 가까이 둔 채 몸 전체를 회전시켜 전방을 한 번 넓게 횡베기한다. 회전은 한 바퀴 미만이며 양발의 축이 보여야 한다. |
| `Charge` | 10 | Y | 3연장 보일러의 압력이 상승하고 대검과 갑옷 이음매가 진동한다. Phase 2 진입 전조로 사용하며 화염과 증기는 별도 VFX다. |
| `Dash` | 10 | N | 몸을 낮추고 대검을 몸 옆에 고정한 뒤 중장갑의 무게를 유지한 채 전방으로 육중하게 돌진한다. 가벼운 달리기처럼 보이지 않게 한다. |
| `Shoot` | 12 | N | Phase 2에서 대검을 크게 휘둘러 전방으로 증기 검기를 방출한다. 캐릭터는 한 번만 휘두르고 3연속 발사체는 런타임 타이밍으로 처리한다. |
| `Hit` | 4 | N | 일반 피격은 갑옷과 대검만 짧게 흔들리고 발과 몸 중심은 거의 움직이지 않는다. 슈퍼아머가 없을 때만 작은 후퇴를 허용한다. |
| `Groggy` | 12 | Y | 대검을 지면에 꽂아 몸을 지탱하고 한쪽 무릎을 꿇으며 보일러와 어깨가 불규칙하게 내려앉는다. |
| `Executed` | 12 | N | 그로기 자세에서 플레이어의 처형 접촉점을 몸통 중심에 제공하고 결정타 후 대검을 놓치며 무릎과 상체가 순차적으로 무너진다. |
| `Death` | 16 | N | 보일러가 정지하고 대검이 먼저 지면에 떨어진 뒤 거대한 갑옷이 무릎부터 옆으로 붕괴한다. 마지막 프레임은 페이드 동안 사용할 정지 자세다. |

## 10. AI 작업 제출 형식

```yaml
unitidx: 3001
unit_name: Puppet Hunter
animation_name: Attack_01
frames: 8
fps: 12
loop: false
identity_reference: doc/images/concepts/Player_Concept_Gothic.png
output:
  format: transparent_png_sequence
  frame_size: 256x256
  naming: Unit_3001_Attack_01_00.png
review_required:
  - identity_consistency
  - weapon_grip_consistency
  - fixed_ground_line
  - readable_contact_pose
  - correct_frame_order
```

## 11. 승인 기준

- 콘셉트 이미지와 얼굴·의상·무기·체형이 일치한다.
- 첫 프레임부터 마지막 프레임까지 손과 무기의 결합 위치가 유지된다.
- 공격은 준비, 접촉, 후속, 회수 단계가 육안으로 구분된다.
- 접촉 프레임은 한 장의 정지 이미지로도 공격 방향을 읽을 수 있다.
- VFX, 투사체, 공격 궤적은 캐릭터 원본 시트에 포함하지 않는다.
- 루프 클립은 첫 프레임과 마지막 프레임 사이 위치 도약이 없다.
- 실제 판정 시간과 다단 공격은 애니메이션 프레임이 아니라 런타임 이벤트 계약으로 관리한다.

## 12. 미확정 사항

- 플레이어의 원거리·마법 스킬 세트는 현재 데이터 계약이 없어 보류한다.
- 몬스터별 추가 패턴과 가론 Phase 2 전용 클립 분리는 실제 패턴 데이터 확정 후 추가한다.
- 외부 AI별 프롬프트 문법과 seed·reference strength는 사용하는 도구가 정해진 뒤 별도 프로파일로 관리한다.

### 🔄 [PM/CI 동기화] 변경 이력 테이블

| 최근 수정 일시 | 수정자 (역할) | 수정 및 추가된 파일/메서드 명세 | QA 검증 통과 기준 (Assert) |
| :--- | :--- | :--- | :--- |
| 2026-08-18 | 문서 초안 | `doc/specs/unit_animation_ai_specification.md` / 유닛 7종 AI 애니메이션 명세 | 콘셉트 참조, 프레임 수, 핵심 포즈, 네거티브 프롬프트 및 제출 형식 포함 |
| 2026-08-18 | PM | `doc/specs/unit_animation_ai_specification.md` / Execution, Executed 프레임 16f로 2배 확장 및 시네마틱 연출 세부화 | 12 FPS 기준 16프레임 시퀀스 무결성 검증 |
