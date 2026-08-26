using System.Collections.Generic;
using CsvHelper;
using System.IO;
using System.Globalization;
using UnityEngine;

/// <summary>
/// 몬스터 패턴 상세 데이터 테이블 (Type 6: 6001~)
/// </summary>
public class MonsterPatternDataTable : IDataLoad
{
    private readonly Dictionary<uint, MonsterPatternData> dataDict = new Dictionary<uint, MonsterPatternData>();
    private readonly HashSet<uint> chainChildren = new HashSet<uint>();

    public int GetDataCount() => this.dataDict.Count;

    public void LoadData(string csvText)
    {
        var replacement = new Dictionary<uint, MonsterPatternData>();
        var records = Util.ParseFromCSV<MonsterPatternData>(csvText);
        if (records != null)
        {
            foreach (var item in records)
            {
                replacement.Add(item.Idx, item);
            }
        }

        var invalid = new HashSet<uint>();
        foreach (var item in replacement.Values)
        {
            var path = new List<uint>(16);
            var visited = new HashSet<uint>();
            MonsterPatternData current = item;
            while (current != null)
            {
                if (!visited.Add(current.Idx) || path.Count >= 16)
                {
                    foreach (uint idx in path) invalid.Add(idx);
                    invalid.Add(current.Idx);
                    Debug.LogError($"[MonsterPatternDataTable] Pattern chain rooted at {item.Idx} has a cycle or exceeds 16 steps; chain rejected.");
                    break;
                }
                path.Add(current.Idx);
                if (current.NextPatternIdx == 0u) break;
                uint nextIdx = current.NextPatternIdx;
                if (nextIdx == current.Idx || !replacement.TryGetValue(nextIdx, out current))
                {
                    foreach (uint idx in path) invalid.Add(idx);
                    Debug.LogError($"[MonsterPatternDataTable] Pattern chain rooted at {item.Idx} has invalid next FK {nextIdx}; chain rejected.");
                    break;
                }
            }
        }

        dataDict.Clear();
        chainChildren.Clear();
        foreach (var pair in replacement)
        {
            if (!invalid.Contains(pair.Key)) dataDict.Add(pair.Key, pair.Value);
        }
        foreach (var item in dataDict.Values)
        {
            if (item.NextPatternIdx != 0u && dataDict.ContainsKey(item.NextPatternIdx))
                chainChildren.Add(item.NextPatternIdx);
        }
        Debug.Log($"[MonsterPatternDataTable] 총 {this.dataDict.Count}개의 패턴 데이터 로드 완료.");
    }

    public bool TryGetPatternData(uint idx, out MonsterPatternData data)
    {
        return this.dataDict.TryGetValue(idx, out data);
    }

    public bool IsChainChild(uint idx) => chainChildren.Contains(idx);

    public bool TryBuildPatternChain(uint entryIdx, List<MonsterPatternData> destination)
    {
        if (destination == null) throw new System.ArgumentNullException(nameof(destination));
        destination.Clear();
        if (IsChainChild(entryIdx) || !dataDict.TryGetValue(entryIdx, out MonsterPatternData current)) return false;
        while (current != null && destination.Count < 16)
        {
            destination.Add(current);
            if (current.NextPatternIdx == 0u) return true;
            if (!dataDict.TryGetValue(current.NextPatternIdx, out current)) break;
        }
        destination.Clear();
        return false;
    }

    public void Release()
    {
        dataDict.Clear();
        chainChildren.Clear();
    }
}
