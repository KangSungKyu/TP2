using System.Collections.Generic;
using CsvHelper;
using System.IO;
using System.Globalization;
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
        using (var reader = new StringReader(csvText))
        using (var csv = new CsvReader(reader, CultureInfo.InvariantCulture))
        {
            var records = csv.GetRecords<MonsterBaseData>();
            foreach (var item in records)
            {
                this.dataDict[item.Idx] = item;
            }
        }
        Debug.Log($"[MonsterDataTable] 총 {this.dataDict.Count}개의 몬스터 파생 데이터 로드 완료.");
    }

    public bool TryGetMonsterData(uint idx, out MonsterBaseData data)
    {
        return this.dataDict.TryGetValue(idx, out data);
    }

    public void Release() => this.dataDict.Clear();
}
