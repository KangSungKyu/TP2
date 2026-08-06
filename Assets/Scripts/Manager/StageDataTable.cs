using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

public class InvalidKeyException : Exception
{
    public InvalidKeyException(string message) : base(message) { }
}

public class HeaderValidationException : Exception
{
    public HeaderValidationException(string message) : base(message) { }
}

/// <summary>
/// DataTableManager에서 관리하는 스테이지 데이터 클래스입니다. (Type 9: 9001~)
/// CsvHelper 기반의 StageData.csv 파싱 및 룸 시퀀스 조회를 지원합니다.
/// </summary>
public class StageDataTable : IDataLoad
{
    private readonly Dictionary<uint, StageBaseData> stageDict = new Dictionary<uint, StageBaseData>();

    public int GetDataCount()
    {
        return this.stageDict.Count;
    }

    public void LoadData(string csvText)
    {
        Validate(csvText);
        var records = Util.ParseFromCSV<StageBaseData>(csvText);
        var replacement = new Dictionary<uint, StageBaseData>();
        foreach (var item in records)
        {
            if (item.Idx == 0 || item.Idx / 1000 != (uint)DataTableType.StageData)
                throw new InvalidKeyException($"Invalid StageData key: '{item.Idx}'.");
            if (item.ThemeType < 1 || item.ThemeType > 2)
                throw new InvalidKeyException($"Undefined themetype: '{item.ThemeType}'.");
            replacement[item.Idx] = item;
        }

        this.stageDict.Clear();
        foreach (var item in replacement) this.stageDict[item.Key] = item.Value;
        Debug.Log($"[StageDataTable] 총 {this.stageDict.Count}개의 스테이지 마스터 데이터가 로드되었습니다.");
    }

    private static void Validate(string csvText)
    {
        const string expected = "idx,nametextidx,chapter,themetype,startroomidx,bossroomidx,roomsequenceidxlist";
        using (var reader = new StringReader(csvText ?? string.Empty))
        {
            if (reader.ReadLine() != expected)
                throw new HeaderValidationException("StageData header must exactly match the lowercase schema.");

            string row;
            while ((row = reader.ReadLine()) != null)
            {
                if (string.IsNullOrWhiteSpace(row)) continue;
                string key = row.Split(',')[0];
                if (string.IsNullOrWhiteSpace(key) || !uint.TryParse(key, out _))
                    throw new InvalidKeyException($"Invalid StageData key: '{key}'.");
            }
        }
    }

    public void Release()
    {
        this.stageDict.Clear();
    }

    public bool TryGetStageData(uint idx, out StageBaseData data)
    {
        return this.stageDict.TryGetValue(idx, out data);
    }

    public StageBaseData GetById(uint idx)
    {
        this.stageDict.TryGetValue(idx, out var data);
        return data;
    }
}
