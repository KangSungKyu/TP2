using System.Collections.Generic;
using CsvHelper;
using System.IO;
using System.Globalization;
using UnityEngine;

/// <summary>
/// 다국어/표기 텍스트 데이터 테이블 (Type 2: 2001~)
/// </summary>
public class TextDataTable : IDataLoad
{
    private readonly Dictionary<uint, TextData> dataDict = new Dictionary<uint, TextData>();

    public int GetDataCount() => this.dataDict.Count;

    public void LoadData(string csvText)
    {
        this.dataDict.Clear();
        using (var reader = new StringReader(csvText))
        using (var csv = new CsvReader(reader, CultureInfo.InvariantCulture))
        {
            var records = csv.GetRecords<TextData>();
            foreach (var item in records)
            {
                this.dataDict[item.Idx] = item;
            }
        }
        Debug.Log($"[TextDataTable] 총 {this.dataDict.Count}개의 텍스트 데이터 로드 완료.");
    }

    public string GetText(uint idx)
    {
        return this.dataDict.TryGetValue(idx, out var data) ? data.Text : string.Empty;
    }

    public void Release() => this.dataDict.Clear();
}
