using System.Collections.Generic;
using CsvHelper;
using System.IO;
using System.Globalization;
using UnityEngine;

public enum GameLanguage
{
    En,
    Kr
}

public static class GameLanguageSettings
{
    public const GameLanguage RuntimeDefault = GameLanguage.En;
    public const GameLanguage PrototypeDefault = GameLanguage.Kr;
    public static GameLanguage Current { get; set; } =
        Debug.isDebugBuild ? PrototypeDefault : RuntimeDefault;
}

/// <summary>
/// 다국어/표기 텍스트 데이터 테이블 (Type 2: 2001~)
/// </summary>
public class TextDataTable : IDataLoad
{
    private readonly Dictionary<uint, TextData> dataDict = new Dictionary<uint, TextData>();
    private readonly HashSet<uint> warnedMissingEnglish = new HashSet<uint>();

    public int GetDataCount() => this.dataDict.Count;

    public void LoadData(string csvText)
    {
        this.dataDict.Clear();
        this.warnedMissingEnglish.Clear();
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
        if (!this.dataDict.TryGetValue(idx, out var data)) return string.Empty;
        if (string.IsNullOrEmpty(data.En))
        {
            if (this.warnedMissingEnglish.Add(idx))
                Debug.LogWarning($"[TextDataTable] TextData idx {idx} has no English text.");
            return string.Empty;
        }
        if (GameLanguageSettings.Current == GameLanguage.Kr && !string.IsNullOrEmpty(data.Kr))
            return data.Kr;
        return data.En;
    }

    public void Release()
    {
        this.dataDict.Clear();
        this.warnedMissingEnglish.Clear();
    }
}
