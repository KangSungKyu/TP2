using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 몬스터 파생 데이터 테이블 (Type 5: 5001~)
/// </summary>
public class MonsterDataTable : IDataLoad
{
    private readonly Dictionary<uint, MonsterBaseData> dataDict = new Dictionary<uint, MonsterBaseData>();

    public int GetDataCount() => this.dataDict.Count;

    public void LoadData(string csvText)
    {
        this.dataDict.Clear();
        var records = Util.ParseFromCSV<MonsterBaseData>(csvText);
        if (records != null)
        {
            foreach (var item in records)
            {
                this.dataDict[item.Idx] = item;
            }
        }
        Debug.Log($"[MonsterDataTable] 총 {this.dataDict.Count}개의 몬스터 파생 데이터 로드 완료.");
    }

    public bool TryGetMonsterData(uint idx, out MonsterBaseData data)
    {
        if (this.dataDict.TryGetValue(idx, out data)) return true;

        // 3000번대 UnitBase Idx (3101 등)로 5000번대 MonsterBaseData (5101 등) 조회 지원
        uint monsterDataIdx = 5000 + Util.GetDataInnerId(idx);
        return this.dataDict.TryGetValue(monsterDataIdx, out data);
    }

    public void Release() => this.dataDict.Clear();
}
