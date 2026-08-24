using System;
using System.Collections.Generic;
using CsvHelper.Configuration.Attributes;
using CsvHelper.TypeConversion;
using UnityEngine;

// =========================================================================
// 1. ENUMS (열거형 데이터 - None = 0 ~ TypeName_End 규칙)
// =========================================================================

/// <summary>
/// 플레이어 캐릭터의 동작 및 상태 열거형
/// </summary>
public enum PlayerState
{
    None = 0,       // 기본값 / 미지정
    Idle = 1,       // 대기
    Run = 2,        // 이동
    Jump = 3,       // 점프
    Parry = 4,      // 패링 윈도우 (0.15s)
    Guard = 5,      // 가드 유지
    Dodge = 6,      // 관통 회피 대시
    Attack = 7,     // 검술 콤보 1타 및 기본 스킬
    Execution = 8,  // 보스 무방비 처형
    Attack2 = 9,    // 검술 콤보 2타
    Attack3 = 10,   // 검술 콤보 3타
    Hit = 11,       // 피격 및 사망
    PlayerState_End
}

/// <summary>
/// 유닛 종류 (1: Player, 2: Monster, 3: Boss, 4: Npc)
/// </summary>
public enum UnitType : uint
{
    None = 0,
    Player = 1,
    Monster = 2,
    Boss = 3,
    Npc = 4,
    UnitType_End
}

/// <summary>
/// 피아 식별 진영 (1: PlayerAlly, 2: Enemy, 3: Neutral)
/// </summary>
public enum FactionType : uint
{
    None = 0,
    PlayerAlly = 1,
    Enemy = 2,
    Neutral = 3,
    FactionType_End
}

/// <summary>
/// 몬스터 패턴 실행 방식 모드 (Simple, Sequence, Random, Trigger)
/// </summary>
public enum PatternExecutionType : uint
{
    None = 0,
    Simple = 1,    // 단순 AI (잡몹 정찰/추적/기본공격)
    Sequence = 2,  // 순차 실행 (Pattern 1 -> 2 -> 3)
    Random = 3,    // 가중치(RandomWeight) 기반 랜덤 선택 실행
    Trigger = 4,   // 조건부(HP %, 거리, Posture) 우선 실행
    PatternExecutionType_End
}

/// <summary>
/// 몬스터 패턴 발동 조건 종류
/// </summary>
public enum PatternTriggerType : uint
{
    None = 0,
    HpRatioUnder = 1,   // 체력 비율 이하 시 (예: 0.5f 이하 2페이즈)
    DistanceOver = 2,   // 플레이어 거리 이상 시 (예: 8m 이상 돌진)
    DistanceUnder = 3,  // 플레이어 거리 이하 시 (예: 근접 난타)
    TargetGroggy = 4,   // 타겟 자세 100% 무방비 시 (예: 처형/강공격)
    PatternTriggerType_End
}

/// <summary>
/// DataTableManager에서 관리하는 데이터 테이블 타입 열거형 (우선순위 순서대로 정렬)
/// </summary>
public enum DataTableType : uint
{
    None = 0,
    
    // [1순위: 기반 공용 데이터]
    Resource = 1,         // 1순위: Addressable 에셋 리소스 데이터 (1001~)
    Text = 2,             // 2순위: 다국어/표기 텍스트 데이터 (2001~)
    UnitBase = 3,         // 3순위: 유닛 공용 마스터 데이터 (3001~ 최우선)
    
    // [2순위: 유닛 파생/개별 데이터]
    PlayerData = 4,       // 4순위: 플레이어 파생 데이터 (4001~)
    MonsterData = 5,      // 5순위: 몬스터 파생 데이터 (5001~)
    MonsterPattern = 6,   // 6순위: 몬스터 패턴 데이터 (6001~)
    Skill = 7,            // 7순위: 스킬 데이터 (7001~)
    EffectData = 8,       // 8순위: 스킬 이펙트 연동 데이터 (8001~)
    StageData = 9,        // 9순위: 스테이지 및 룸 시퀀스 데이터 (9001~)
    AttackMotionProfile = 10,
    ChunkResource = 11,   // 청크 후보와 ResourceData FK (11001~)
    StageLayout = 12,     // 스테이지 절차 생성 설정 (12001~)
    MonsterEncounter = 13,// 스테이지 전투 조합 (13001~)
    
    DataTableType_End
}

public enum PatternTriggerSubject : uint
{
    Self = 0,
    CurrentTarget = 1
}

public enum PatternState : uint
{
    Idle = 0,
    Reserved = 1,
    Chase = 2,
    Startup = 3,
    Active = 4,
    Recovery = 5,
    Returning = 6
}

public enum PatternCancelReason : uint
{
    None = 0,
    Cancelled = 1,
    Timeout = 2,
    TargetInvalid = 3,
    UnsafeRetreat = 4,
    Groggy = 5,
    Death = 6,
    Disabled = 7,
    Returning = 8,
    Exception = 9
}

public enum AttackMotionType : uint
{
    Stationary = 0,
    Step = 1,
    AcceleratingLunge = 2
}

public enum AttackTargetPolicy : uint
{
    SnapshotAtStartup = 0,
    TrackUntilActive = 1
}

public enum AttackSubject : uint
{
    Weapon = 0,
    BodyPart = 1
}

public enum BodyPartRole : uint
{
    None = 0,
    Torso = 1
}


// =========================================================================
// 2. INTERFACES (인터페이스 데이터)
// =========================================================================

/// <summary>
/// CSV 데이터 테이블 로딩 표준 인터페이스
/// </summary>
public interface IDataLoad
{
    public int GetDataCount();
    public void LoadData(string csvText);
    public void Release();
}


// =========================================================================
// 3. STRUCTS (구조체 데이터)
// =========================================================================

/// <summary>
/// 스킬 데이터 구조체 (CSV 파싱 데이터)
/// </summary>
public struct SkillInfo
{
    public int Id;
    public string Name;
    public float Range;
    public float CastTime;
    public float Cooldown;
    public float MpCost;
    public float DamageMultiplier;
    public bool IsBasicAttack;
    public float HitWindowPre;
    public float HitWindowPost;
    public int AnimState;
}


// =========================================================================
// 4. CLASSES (CsvHelper 모델 및 DTO 데이터)
// =========================================================================

/// <summary>
/// 전역 공통 상수 및 설정 클래스
/// </summary>
public static class CommonConstants
{
    public const string AddressableLabelDatas = "Datas";
    public const string AddressableLabelAnims = "Anims";
    public const string AddressableLabelPrefabs = "Prefabs";
    public const float ParryWindowDuration = 0.15f;
}

/// <summary>
/// Addressable 에셋 참조 데이터 (ResourceData.csv 1:1 매핑, Type 1: 1001~)
/// </summary>
[Serializable]
public class ResourceData
{
    [Name("idx")]
    public uint Idx { get; set; }

    [Name("path")]
    public string Path { get; set; } // Addressable Key ("Player", "GaronAnimatorController" 등)
}

/// <summary>
/// 텍스트 데이터 (TextData.csv 1:1 매핑, Type 2: 2001~)
/// </summary>
[Serializable]
public class TextData
{
    [Name("idx")]
    public uint Idx { get; set; }

    [Name("en")]
    public string En { get; set; } // 다국어 / UI 표기 텍스트
    [Name("kr")]
    public string Kr { get; set; }
}

/// <summary>
/// 모든 UnitBase의 공용 마스터 데이터 (UnitBaseData.csv 1:1 매핑, Type 3: 3001~)
/// string 필드를 배제하고 NameTextIdx, PrefabId, AnimatorId (uint)로 2원화 참조
/// </summary>
[Serializable]
public class UnitBaseData
{
    [Name("idx")]
    public uint Idx { get; set; }

    [Name("nametextidx")]
    public uint NameTextIdx { get; set; } // TextData.csv ID 참조 (Type 2: 2001~)

    [Name("unittype")]
    public uint UnitType { get; set; } // 1: Player, 2: Monster, 3: Boss, 4: Npc

    [Name("prefabid")]
    public uint PrefabId { get; set; } // ResourceData.csv ID 참조 (Type 1: 1001~)

    [Name("animatorid")]
    public uint AnimatorId { get; set; } // ResourceData.csv ID 참조 (Type 1: 1010~)

    [Name("maxhp")]
    public float MaxHp { get; set; }

    [Name("maxmp")]
    public float MaxMp { get; set; }

    [Name("maxposture")]
    public float MaxPosture { get; set; }

    [Name("atk")]
    public float Atk { get; set; }

    [Name("def")]
    public float Def { get; set; }

    [Name("movespeed")]
    public float MoveSpeed { get; set; }

    [Name("visualoffsety")]
    public float VisualOffsetY { get; set; } // Root 지면 피벗 기준 Visual 자식 높이 오프셋

    [Name("hitboxradius")]
    public float HitboxRadius { get; set; }

    [Name("faction")]
    public uint Faction { get; set; } // 1: PlayerAlly, 2: Enemy, 3: Neutral
}

/// <summary>
/// 몬스터 파생 데이터 (MonsterBaseData.csv 1:1 매핑, Type 5: 5001~)
/// </summary>
[Serializable]
public class MonsterBaseData
{
    [Name("idx")]
    public uint Idx { get; set; } // UnitBaseData의 Idx와 1:1 동기화 (3101, 3201 등)

    [Name("detectrange")]
    public float DetectRange { get; set; }

    [Name("attackrange")]
    public float AttackRange { get; set; }

    [Name("patternidxlist"), TypeConverter(typeof(UIntArrayConverter))]
    public uint[] PatternIdxList { get; set; } // MonsterPatternData.csv Idx 목록 (Type 6: 6001~)
}

/// <summary>
/// 몬스터 패턴 상세 데이터 (MonsterPatternData.csv 1:1 매핑, Type 6: 6001~)
/// </summary>
[Serializable]
public class MonsterPatternData
{
    [Name("idx")]
    public uint Idx { get; set; }

    [Name("patternnametextidx")]
    public uint PatternNameTextIdx { get; set; } // TextData.csv Idx 참조 (2001~)

    [Name("executiontype")]
    public uint ExecutionType { get; set; } // 1: Simple, 2: Sequence, 3: Random, 4: Trigger

    [Name("triggertype")]
    public uint TriggerType { get; set; } // 0: None, 1: HpRatioUnder, 2: DistanceOver, 3: DistanceUnder, 4: TargetGroggy

    // Optional until the resource-owned CSV migration lands. Null preserves the
    // legacy, unambiguous TriggerType mapping without accepting string enums.
    [Name("triggersubject"), Optional]
    public uint? TriggerSubjectValue { get; set; }

    [Ignore]
    public PatternTriggerSubject? TriggerSubject => TriggerSubjectValue.HasValue &&
        TriggerSubjectValue.Value <= (uint)PatternTriggerSubject.CurrentTarget
            ? (PatternTriggerSubject)TriggerSubjectValue.Value : null;

    [Name("triggervalue")]
    public float TriggerValue { get; set; } // 조건 수치

    [Name("randomweight")]
    public int RandomWeight { get; set; } // Random 모드 가중치 확률

    [Name("predelay")]
    public float PreDelay { get; set; } // 전조시간 (초)

    [Name("postdelay")]
    public float PostDelay { get; set; } // 후딜레이 (초)

    [Name("cooldown")]
    public float Cooldown { get; set; } // 재사용 쿨다운 (초)

    [Name("damage")]
    public float Damage { get; set; }

    [Name("chasetimeout")]
    public float ChaseTimeout { get; set; } // 추격 제한 시간 (초)

    [Name("skillidx")]
    public uint SkillIdx { get; set; } // 연동 SkillData Idx (Type 7: 7001~)

    [Name("minstartdistance"), Optional]
    public float MinStartDistance { get; set; }

    [Name("maxstartdistance"), Optional]
    public float MaxStartDistance { get; set; }

    [Name("attackmotionprofileidx"), Optional]
    public uint AttackMotionProfileIdx { get; set; }

    [Name("projectileresourceidx")]
    public uint ProjectileResourceIdx { get; set; }

    [Name("projectilespeed")]
    public float ProjectileSpeed { get; set; }

    [Name("projectilemaxdistance")]
    public float ProjectileMaxDistance { get; set; }
}


/// <summary>
/// 스킬 이펙트 연동 데이터 (EffectData.csv 1:1 매핑, Type 8: 8001~)
/// </summary>
[Serializable]
public class EffectData
{
    [Name("idx")]
    public uint Idx { get; set; }

    [Name("effectnametextidx")]
    public uint EffectNameTextIdx { get; set; }

    [Name("prefabidx")]
    public uint PrefabIdx { get; set; } // ResourceData.csv Idx 참조 (Type 1: 1001~)

    [Name("duration")]
    public float Duration { get; set; } // 이펙트 존속 시간 (초)

    [Name("scale")]
    public float Scale { get; set; } // 스케일 배율

    [Name("loopcount")]
    public int LoopCount { get; set; } // 반복 횟수 (0: 무한)

    [Name("activecenterx")]
    public float ActiveCenterX { get; set; }

    [Name("activecentery")]
    public float ActiveCenterY { get; set; }

    [Name("activesizex")]
    public float ActiveSizeX { get; set; }

    [Name("activesizey")]
    public float ActiveSizeY { get; set; }

    public bool HasValidActiveBounds =>
        float.IsFinite(ActiveCenterX) && float.IsFinite(ActiveCenterY) &&
        float.IsFinite(ActiveSizeX) && float.IsFinite(ActiveSizeY) &&
        ActiveSizeX > 0f && ActiveSizeY > 0f;
}

/// <summary>
/// 스킬 마스터 데이터 (SkillData.csv 1:1 매핑, Type 7: 7001~)
/// </summary>
[Serializable]
public class SkillData
{
    [Name("idx")]
    public uint Idx { get; set; } // Idx 역할 (7001~)

    public uint SkillId => Idx; // 하위 호환성 프로퍼티

    [Name("nametextidx")]
    public uint NameTextIdx { get; set; }

    [Name("range")]
    public float Range { get; set; }

    [Name("casttime")]
    public float CastTime { get; set; }

    [Name("cooldownsec")]
    public float CooldownSec { get; set; }

    [Name("mpcost")]
    public float MpCost { get; set; }

    [Name("damagemultiplier")]
    public float DamageMultiplier { get; set; }

    [Name("isbasicattack"), TypeConverter(typeof(ZeroOneBooleanConverter))]
    public bool IsBasicAttack { get; set; }

    [Name("hitcount")]
    public int HitCount { get; set; }

    [Name("hittimings"), TypeConverter(typeof(FloatArrayConverter))]
    public float[] HitTimings { get; set; } // _ 구분자 float 배열

    [Name("hitwindowpre")]
    public float HitWindowPre { get; set; }

    [Name("hitwindowpost")]
    public float HitWindowPost { get; set; }

    [Name("effectidx")]
    public uint EffectIdx { get; set; } // EffectData.csv Idx 참조 (Type 8: 8001~)

    [Name("animstate")]
    public int AnimState { get; set; } // Animator 'State' (int) 파라미터 제어 값

    [Name("attackmotionprofileidx"), Optional]
    public uint AttackMotionProfileIdx { get; set; }

    [Name("attacksubject"), Optional, TypeConverter(typeof(AttackSubjectConverter))]
    public AttackSubject AttackSubject { get; set; }

    [Name("bodypart"), Optional, TypeConverter(typeof(BodyPartRoleConverter))]
    public BodyPartRole BodyPartRole { get; set; }
}

[Serializable]
public class AttackMotionProfileData
{
    [Name("idx")]
    public uint Idx { get; set; }

    [Name("motiontype"), TypeConverter(typeof(AttackMotionTypeConverter))]
    public AttackMotionType MotionType { get; set; }

    [Name("targetpolicy"), TypeConverter(typeof(AttackTargetPolicyConverter))]
    public AttackTargetPolicy TargetPolicy { get; set; }

    [Name("maxdistance")]
    public float MaxDistance { get; set; }

    [Name("maxspeed")]
    public float MaxSpeed { get; set; }

    [Name("acceleration")]
    public float Acceleration { get; set; }

    [Name("enabled"), TypeConverter(typeof(ZeroOneBooleanConverter))]
    public bool Enabled { get; set; }
}

/// <summary>
/// 스테이지 및 룸 시퀀스 데이터 (StageData.csv 1:1 매핑, Type 9: 9001~)
/// </summary>
[Serializable]
public class StageBaseData
{
    [Name("idx")]
    public uint Idx { get; set; } // 9001~

    [Name("nametextidx")]
    public uint NameTextIdx { get; set; }

    [Name("chapter")]
    public int Chapter { get; set; }

    [Name("themetype")]
    public int ThemeType { get; set; }

    [Name("startroomidx")]
    public uint StartRoomIdx { get; set; }

    [Name("bossroomidx")]
    public uint BossRoomIdx { get; set; }

    [Name("roomsequenceidxlist"), TypeConverter(typeof(UIntArrayConverter))]
    public uint[] RoomSequenceIdxList { get; set; }
}

[Serializable]
public class StageLayoutData
{
    [Name("idx")] public uint Idx { get; set; }
    [Name("stagedataidx")] public uint StageDataIdx { get; set; }
    [Name("minrows")] public byte MinRows { get; set; }
    [Name("maxrows")] public byte MaxRows { get; set; }
    [Name("mincolumns")] public byte MinColumns { get; set; }
    [Name("maxcolumns")] public byte MaxColumns { get; set; }
    [Name("minactivechunks")] public byte MinActiveChunks { get; set; }
    [Name("maxactivechunks")] public byte MaxActiveChunks { get; set; }
    [Name("bossroomresourceidx")] public uint BossRoomResourceIdx { get; set; }
    [Name("nextstageidx")] public uint NextStageIdx { get; set; }
}

[Serializable]
public class ChunkResourceData
{
    [Name("idx")] public uint Idx { get; set; }
    [Name("resourceidx")] public uint ResourceIdx { get; set; }
    [Name("chunktype")] public byte ChunkType { get; set; }
    [Name("supportedconnectionmask")] public byte SupportedConnectionMask { get; set; }
    [Name("minstageidx")] public uint MinStageIdx { get; set; }
    [Name("maxuseperrun")] public byte MaxUsePerRun { get; set; }
    [Name("weight")] public ushort Weight { get; set; }
}

[Serializable]
public class MonsterEncounterData
{
    [Name("idx")] public uint Idx { get; set; }
    [Name("stageidx")] public uint StageIdx { get; set; }
    [Name("variant")] public byte Variant { get; set; }
    [Name("unitidxlist"), TypeConverter(typeof(UIntArrayConverter))] public uint[] UnitIdxList { get; set; }
    [Name("threatcost")] public byte ThreatCost { get; set; }
    [Name("weight")] public ushort Weight { get; set; }
}



