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
                this.dataDict[item.Idx] = item;
            }
        }
        Debug.Log($"[EffectDataTable] 총 {this.dataDict.Count}개의 스킬 이펙트 데이터 로드 완료.");
    }

    public bool TryGetEffectData(uint idx, out EffectData data)
    {
        return this.dataDict.TryGetValue(idx, out data);
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
