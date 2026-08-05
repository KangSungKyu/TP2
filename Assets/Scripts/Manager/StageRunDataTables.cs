using System.Collections.Generic;
using System.IO;
using UnityEngine;

public sealed class StageLayoutDataTable : IDataLoad
{
    private const string Header = "idx,stagedataidx,minrows,maxrows,mincolumns,maxcolumns,minactivechunks,maxactivechunks,bossroomresourceidx,nextstageidx";
    private readonly Dictionary<uint, StageLayoutData> data = new Dictionary<uint, StageLayoutData>();

    public int GetDataCount() => data.Count;

    public void LoadData(string csvText)
    {
        StageRunCsv.ValidateHeader(csvText, Header);
        var replacement = new Dictionary<uint, StageLayoutData>();
        foreach (StageLayoutData item in Util.ParseFromCSV<StageLayoutData>(csvText))
        {
            StageRunCsv.ValidateIdx(item.Idx, DataTableType.StageLayout, replacement.ContainsKey(item.Idx));
            replacement.Add(item.Idx, item);
        }
        data.Clear();
        foreach (var pair in replacement) data.Add(pair.Key, pair.Value);
    }

    public bool TryGetByStage(uint stageIdx, out StageLayoutData result)
    {
        foreach (StageLayoutData item in data.Values)
        {
            if (item.StageDataIdx == stageIdx) { result = item; return true; }
        }
        result = null;
        return false;
    }

    public void Release() => data.Clear();
}

public sealed class ChunkResourceDataTable : IDataLoad
{
    private const string Header = "idx,resourceidx,chunktype,supportedconnectionmask,minstageidx,maxuseperrun,weight";
    private readonly Dictionary<uint, ChunkResourceData> data = new Dictionary<uint, ChunkResourceData>();

    public int GetDataCount() => data.Count;

    public void LoadData(string csvText)
    {
        StageRunCsv.ValidateHeader(csvText, Header);
        var replacement = new Dictionary<uint, ChunkResourceData>();
        foreach (ChunkResourceData item in Util.ParseFromCSV<ChunkResourceData>(csvText))
        {
            StageRunCsv.ValidateIdx(item.Idx, DataTableType.ChunkResource, replacement.ContainsKey(item.Idx));
            if (item.ResourceIdx == 0) throw new InvalidKeyException("ChunkResourceData resourceidx must be non-zero.");
            replacement.Add(item.Idx, item);
        }
        data.Clear();
        foreach (var pair in replacement) data.Add(pair.Key, pair.Value);
    }

    public List<ChunkResourceData> GetForStage(uint stageIdx)
    {
        var result = new List<ChunkResourceData>();
        foreach (ChunkResourceData item in data.Values)
            if (item.MinStageIdx <= stageIdx) result.Add(item);
        result.Sort((left, right) => left.Idx.CompareTo(right.Idx));
        return result;
    }

    public void Release() => data.Clear();
}

public sealed class MonsterEncounterDataTable : IDataLoad
{
    private const string Header = "idx,stageidx,variant,unitidxlist,threatcost,weight";
    private readonly Dictionary<uint, MonsterEncounterData> data = new Dictionary<uint, MonsterEncounterData>();

    public int GetDataCount() => data.Count;

    public void LoadData(string csvText)
    {
        StageRunCsv.ValidateHeader(csvText, Header);
        var replacement = new Dictionary<uint, MonsterEncounterData>();
        foreach (MonsterEncounterData item in Util.ParseFromCSV<MonsterEncounterData>(csvText))
        {
            StageRunCsv.ValidateIdx(item.Idx, DataTableType.MonsterEncounter, replacement.ContainsKey(item.Idx));
            replacement.Add(item.Idx, item);
        }
        data.Clear();
        foreach (var pair in replacement) data.Add(pair.Key, pair.Value);
    }

    public List<MonsterEncounterData> GetForStage(uint stageIdx)
    {
        var result = new List<MonsterEncounterData>();
        foreach (MonsterEncounterData item in data.Values)
            if (item.StageIdx == stageIdx) result.Add(item);
        result.Sort((left, right) => left.Idx.CompareTo(right.Idx));
        return result;
    }

    public void Release() => data.Clear();
}

internal static class StageRunCsv
{
    public static void ValidateHeader(string csvText, string expected)
    {
        using (var reader = new StringReader(csvText ?? string.Empty))
            if (reader.ReadLine() != expected) throw new HeaderValidationException($"CSV header must be '{expected}'.");
    }

    public static void ValidateIdx(uint idx, DataTableType expectedType, bool duplicate)
    {
        if (Util.GetDataTableType(idx) != expectedType)
            throw new InvalidKeyException($"Idx {idx} must use DataTableType.{expectedType} range.");
        if (duplicate) throw new InvalidKeyException($"Duplicate idx {idx} is not allowed.");
    }
}

public static class StageRunDataExtensions
{
    public static bool TryLockCompletion(this StageRunData run)
    {
        if (run == null || run.CompletionLocked) return false;
        run.CompletionLocked = true;
        return true;
    }
}
