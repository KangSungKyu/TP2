using System.Collections.Generic;
using UnityEngine;

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
        this.stageDict.Clear();
        var records = Util.ParseFromCSV<StageBaseData>(csvText);
        if (records != null)
        {
            foreach (var item in records)
            {
                this.stageDict[item.Idx] = item;
            }
        }
        Debug.Log($"[StageDataTable] 총 {this.stageDict.Count}개의 스테이지 마스터 데이터가 로드되었습니다.");
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
