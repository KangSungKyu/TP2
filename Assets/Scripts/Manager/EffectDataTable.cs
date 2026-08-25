using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 스킬 이펙트 연동 데이터 테이블 (Type 8: 8001~)
/// </summary>
public class EffectDataTable : IDataLoad
{
    private readonly Dictionary<uint, EffectData> dataDict = new Dictionary<uint, EffectData>();

    public int GetDataCount() => this.dataDict.Count;

    public void LoadData(string csvText)
    {
        this.dataDict.Clear();
        var records = Util.ParseFromCSV<EffectData>(csvText);
        if (records != null)
        {
            foreach (var item in records)
            {
                bool hasAnyBounds = item.ActiveCenterX != 0f || item.ActiveCenterY != 0f ||
                    item.ActiveSizeX != 0f || item.ActiveSizeY != 0f;
                bool requiresBounds = item.HasAttackBinding;
                if ((hasAnyBounds || requiresBounds) && !item.HasValidActiveBounds)
                {
                    Debug.LogError($"[EffectDataTable] Effect idx {item.Idx} has invalid active bounds; row rejected.");
                    continue;
                }
                if (requiresBounds && (
                    (item.UnitIdx != 0u && item.UnitIdx / 1000u != (uint)DataTableType.UnitBase) ||
                    (item.PatternIdx != 0u && item.PatternIdx / 1000u != (uint)DataTableType.MonsterPattern) ||
                    (item.SkillIdx != 0u && item.SkillIdx / 1000u != (uint)DataTableType.Skill)))
                {
                    Debug.LogError($"[EffectDataTable] Attack effect idx {item.Idx} has invalid uint identity binding; row rejected.");
                    continue;
                }
                this.dataDict[item.Idx] = item;
            }
        }
        Debug.Log($"[EffectDataTable] 총 {this.dataDict.Count}개의 스킬 이펙트 데이터 로드 완료.");
    }

    public bool TryGetEffectData(uint idx, out EffectData data)
    {
        return this.dataDict.TryGetValue(idx, out data);
    }

    public bool TryResolveAttackEffect(uint unitIdx, uint patternIdx, uint skillIdx, uint runtimeHitTick,
        out EffectData data)
    {
        data = null;
        int bestScore = -1;
        uint serializedTick = runtimeHitTick + 1u;
        foreach (EffectData candidate in dataDict.Values)
        {
            if (!candidate.HasValidActiveBounds || !candidate.HasAttackBinding ||
                (candidate.UnitIdx != 0u && candidate.UnitIdx != unitIdx) ||
                (candidate.PatternIdx != 0u && candidate.PatternIdx != patternIdx) ||
                (candidate.SkillIdx != 0u && candidate.SkillIdx != skillIdx) ||
                (candidate.HitTick != 0u && candidate.HitTick != serializedTick)) continue;

            int score = (candidate.HitTick != 0u ? 8 : 0) + (candidate.UnitIdx != 0u ? 4 : 0) +
                (candidate.PatternIdx != 0u ? 2 : 0) + (candidate.SkillIdx != 0u ? 1 : 0);
            if (score <= bestScore) continue;
            bestScore = score;
            data = candidate;
        }
        return data != null;
    }

    public string GetDisplayName(uint idx)
    {
        if (!this.dataDict.TryGetValue(idx, out EffectData effectData)) return string.Empty;
        var textTable = DataTableManager.Instance != null
            ? DataTableManager.Instance.GetDB<TextDataTable>(DataTableType.Text)
            : null;
        string displayName = textTable != null ? textTable.GetText(effectData.EffectNameTextIdx) : string.Empty;
        if (string.IsNullOrEmpty(displayName))
            Debug.LogWarning($"[EffectDataTable] Effect idx {idx} has no TextData mapping for idx {effectData.EffectNameTextIdx}.");
        return displayName;
    }

    public EffectData GetById(uint idx)
    {
        this.dataDict.TryGetValue(idx, out var data);
        return data;
    }

    public void Release() => this.dataDict.Clear();
}
