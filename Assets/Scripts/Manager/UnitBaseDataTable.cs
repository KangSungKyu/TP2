using System.Collections.Generic;
using CsvHelper;
using System.IO;
using System.Globalization;
using UnityEngine;

/// <summary>
/// 유닛 공용 마스터 데이터 테이블 (Type 3: 3001~ 최우선)
/// </summary>
public class UnitBaseDataTable : IDataLoad
{
    private readonly Dictionary<uint, UnitBaseData> dataDict = new Dictionary<uint, UnitBaseData>();

    public int GetDataCount() => this.dataDict.Count;

    public void LoadData(string csvText)
    {
        this.dataDict.Clear();
        var records = Util.ParseFromCSV<UnitBaseData>(csvText);
        if (records != null)
        {
            foreach (var item in records)
            {
                this.dataDict[item.Idx] = item;
            }
        }
        Debug.Log($"[UnitBaseDataTable] 총 {this.dataDict.Count}개의 유닛 공용 데이터 로드 완료.");
    }

    public bool TryGetUnitData(uint idx, out UnitBaseData data)
    {
        return this.dataDict.TryGetValue(idx, out data);
    }

    public void Release() => this.dataDict.Clear();
}
