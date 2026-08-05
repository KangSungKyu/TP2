using System.Collections.Generic;
using UnityEngine;

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
        this.skillDict.Clear();
        var records = Util.ParseFromCSV<SkillData>(csvText);
        if (records != null)
        {
            foreach (var item in records)
            {
                this.skillDict[item.Idx] = item;
            }
        }
        Debug.Log($"[SkillDataTable] 총 {this.skillDict.Count}개의 스킬 데이터가 로드되었습니다.");
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
                AnimationClip = data.AnimationClip,
                Range = data.Range,
                CastTime = data.CastTime,
                Cooldown = data.CooldownSec,
                MpCost = data.MpCost,
                DamageMultiplier = data.DamageMultiplier,
                IsBasicAttack = data.IsBasicAttack
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
