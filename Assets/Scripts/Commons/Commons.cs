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
    Attack = 7,     // 검술 콤보 공격 및 스킬
    Execution = 8,  // 보스 무방비 처형
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
    
    DataTableType_End
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
    public string AnimationClip;
    public float Range;
    public float CastTime;
    public float Cooldown;
    public float MpCost;
    public float DamageMultiplier;
    public bool IsBasicAttack;
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

    [Name("text")]
    public string Text { get; set; } // 다국어 / UI 표기 텍스트
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

    [Name("animclipname")]
    public string AnimClipName { get; set; } // Animator 재생 애니메이션 클립 이름

    [Name("executiontype")]
    public uint ExecutionType { get; set; } // 1: Simple, 2: Sequence, 3: Random, 4: Trigger

    [Name("triggertype")]
    public uint TriggerType { get; set; } // 0: None, 1: HpRatioUnder, 2: DistanceOver, 3: DistanceUnder, 4: TargetGroggy

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
}
