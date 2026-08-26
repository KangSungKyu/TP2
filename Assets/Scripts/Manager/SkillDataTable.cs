using System;
using System.Collections.Generic;
using System.Globalization;
using CsvHelper;
using CsvHelper.Configuration;
using CsvHelper.TypeConversion;
using UnityEngine;

public sealed class AttackSubjectConverter : DefaultTypeConverter
{
    public override object ConvertFromString(string text, IReaderRow row, MemberMapData memberMapData)
    {
        if (uint.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out uint value) && value <= 1)
            return (AttackSubject)value;
        throw new FormatException($"attacksubject must be integer 0..1, but was '{text}'.");
    }
}

public sealed class BodyPartRoleConverter : DefaultTypeConverter
{
    public override object ConvertFromString(string text, IReaderRow row, MemberMapData memberMapData)
    {
        if (uint.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out uint value) && value <= 1)
            return (BodyPartRole)value;
        throw new FormatException($"bodypart must be integer 0..1, but was '{text}'.");
    }
}

public sealed class SkillMotionPhaseConverter : DefaultTypeConverter
{
    public override object ConvertFromString(string text, IReaderRow row, MemberMapData memberMapData)
    {
        if (uint.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out uint value) &&
            (value & ~0x0Fu) == 0u)
            return (SkillMotionPhase)value;
        throw new FormatException($"Skill idx {row.GetField("idx")} motionphasemask must be integer bits 0x00..0x0F, but was '{text}'.");
    }
}

/// <summary>
/// DataTableManager에서 관리하는 스킬 테이블 데이터클래스입니다. (Type 7: 7001~)
/// CsvHelper 기반의 SkillData 파싱 및 하위 호환성을 위한 SkillInfo 매핑을 지원합니다.
/// </summary>
public class SkillDataTable : IDataLoad
{
    private readonly Dictionary<uint, SkillData> skillDict = new Dictionary<uint, SkillData>();

    public int GetDataCount()
    {
        return this.skillDict.Count;
    }

    public void LoadData(string csvText)
    {
        var replacement = new Dictionary<uint, SkillData>();
        var records = Util.ParseFromCSV<SkillData>(csvText);
        foreach (SkillData item in records)
        {
            if (float.IsNaN(item.AttackMotionTime) || float.IsInfinity(item.AttackMotionTime) ||
                item.AttackMotionTime < 0f)
            {
                Debug.LogWarning($"[SkillDataTable] Skill idx {item.Idx} has invalid attackmotiontime; using 0.");
                item.AttackMotionTime = 0f;
            }
            if (!IsValidAttackSubject(item))
                throw new InvalidOperationException($"Invalid attack subject/body part contract for Skill idx {item.Idx}.");
            replacement.Add(item.Idx, item);
        }
        this.skillDict.Clear();
        foreach (var pair in replacement) this.skillDict.Add(pair.Key, pair.Value);
        Debug.Log($"[SkillDataTable] 총 {this.skillDict.Count}개의 스킬 데이터가 로드되었습니다.");
    }

    private static bool IsValidAttackSubject(SkillData skill)
    {
        if (!Enum.IsDefined(typeof(AttackSubject), skill.AttackSubject) ||
            !Enum.IsDefined(typeof(BodyPartRole), skill.BodyPartRole)) return false;
        return skill.AttackSubject == AttackSubject.Weapon
            ? skill.BodyPartRole == BodyPartRole.None
            : skill.BodyPartRole == BodyPartRole.Torso;
    }

    public bool TryGetSkillData(uint skillId, out SkillData data)
    {
        return this.skillDict.TryGetValue(skillId, out data);
    }

    public SkillData GetById(uint skillId)
    {
        this.skillDict.TryGetValue(skillId, out var data);
        return data;
    }

    // 하위 호환성 메서드
    public bool TryGetSkill(int skillId, out SkillInfo info)
    {
        info = default;
        if (this.skillDict.TryGetValue((uint)skillId, out var data))
        {
            info = new SkillInfo
            {
                Id = (int)data.SkillId,
                Name = ResolveDisplayName(data.NameTextIdx, data.Idx),
                Range = data.Range,
                CastTime = data.CastTime,
                Cooldown = data.CooldownSec,
                MpCost = data.MpCost,
                DamageMultiplier = data.DamageMultiplier,
                IsBasicAttack = data.IsBasicAttack,
                HitWindowPre = data.HitWindowPre,
                HitWindowPost = data.HitWindowPost,
                AnimState = data.AnimState
            };
            return true;
        }
        return false;
    }

    public void Release()
    {
        this.skillDict.Clear();
    }

    private static string ResolveDisplayName(uint textIdx, uint skillIdx)
    {
        var textTable = DataTableManager.Instance != null
            ? DataTableManager.Instance.GetDB<TextDataTable>(DataTableType.Text)
            : null;
        string displayName = textTable != null ? textTable.GetText(textIdx) : string.Empty;
        if (string.IsNullOrEmpty(displayName))
            Debug.LogWarning($"[SkillDataTable] Skill idx {skillIdx} has no TextData mapping for idx {textIdx}.");
        return displayName;
    }
}
