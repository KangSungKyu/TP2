using System.Collections.Generic;
using CsvHelper;
using System.IO;
using System.Globalization;
using UnityEngine;

/// <summary>
/// Addressable 키 리소스 참조 데이터 테이블 (Type 1: 1001~)
/// </summary>
public class ResourceDataTable : IDataLoad
{
    private readonly Dictionary<uint, ResourceData> dataDict = new Dictionary<uint, ResourceData>();

    public int GetDataCount() => this.dataDict.Count;

    public void LoadData(string csvText)
    {
        this.dataDict.Clear();
        using (var reader = new StringReader(csvText))
        using (var csv = new CsvReader(reader, CultureInfo.InvariantCulture))
        {
            var records = csv.GetRecords<ResourceData>();
            foreach (var item in records)
            {
                this.dataDict[item.Idx] = item;
            }
        }
        Debug.Log($"[ResourceDataTable] 총 {this.dataDict.Count}개의 리소스 경로 데이터 로드 완료.");
    }

    public bool TryGetResource(uint idx, out ResourceData data)
    {
        return this.dataDict.TryGetValue(idx, out data);
    }

    public string GetResourcePath(uint idx)
    {
        return this.dataDict.TryGetValue(idx, out var data) ? data.Path : string.Empty;
    }

    public void Release() => this.dataDict.Clear();
}
