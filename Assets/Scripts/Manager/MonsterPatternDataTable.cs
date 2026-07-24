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

    public int GetDataCount() => this.dataDict.Count;

    public void LoadData(string csvText)
    {
        this.dataDict.Clear();
        using (var reader = new StringReader(csvText))
        using (var csv = new CsvReader(reader, CultureInfo.InvariantCulture))
        {
            var records = csv.GetRecords<MonsterPatternData>();
            foreach (var item in records)
            {
                this.dataDict[item.Idx] = item;
            }
        }
        Debug.Log($"[MonsterPatternDataTable] 총 {this.dataDict.Count}개의 패턴 데이터 로드 완료.");
    }

    public bool TryGetPatternData(uint idx, out MonsterPatternData data)
    {
        return this.dataDict.TryGetValue(idx, out data);
    }

    public void Release() => this.dataDict.Clear();
}
