using Cysharp.Threading.Tasks;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

/// <summary>
/// 데이터 테이블 매니저 (Singleton)
/// CSV 데이터 테이블 전반의 비동기 로드, 파싱 및 캐싱을 총괄 관리합니다.
/// 데이터 식별 및 검증은 오직 idx 기반(Util.GetDataTableType)으로만 수행합니다.
/// </summary>
public class DataTableManager : Singleton<DataTableManager>
{
    // =========================================================================
    // 1. PRIVATE FIELDS (camelCase, No '_' prefix)
    // =========================================================================

    private readonly Dictionary<DataTableType, IDataLoad> dataList = new Dictionary<DataTableType, IDataLoad>();
    private readonly UniTaskCompletionSource loadCompletionSource = new UniTaskCompletionSource();
    private bool isLoaded = false;


    // =========================================================================
    // 2. PUBLIC METHODS (PascalCase)
    // =========================================================================

    /// <summary>
    /// 데이터 테이블이 모두 로드 및 캐싱될 때까지 비동기로 안전하게 대기합니다.
    /// </summary>
    public async UniTask EnsureDataLoadedAsync()
    {
        if (this.isLoaded) return;
        await this.loadCompletionSource.Task;
    }

    public T GetDB<T>(uint idx) where T : class, IDataLoad
    {
        DataTableType dtt = Util.GetDataTableType(idx);
        if (idx <= 0)
        {
            dtt = this.dataList.FirstOrDefault(kv => kv.Value is T).Key;
        }
        return this.GetDB<T>(dtt);
    }

    public T GetDB<T>(DataTableType dataTableType) where T : class, IDataLoad
    {
        return this.dataList.TryGetValue(dataTableType, out var value) ? value as T : null;
    }

    public int GetDataCount<T>(DataTableType dataTableType) where T : class, IDataLoad
    {
        var db = this.GetDB<T>(dataTableType);
        return db?.GetDataCount() ?? 0;
    }


    // =========================================================================
    // 3. PROTECTED & PRIVATE METHODS (camelCase)
    // =========================================================================

    protected override void OnSingletonAwake()
    {
        base.OnSingletonAwake();

        // [우선순위 순서 정렬 등록]
        this.dataList[DataTableType.Resource] = new ResourceDataTable();
        this.dataList[DataTableType.Text] = new TextDataTable();
        this.dataList[DataTableType.UnitBase] = new UnitBaseDataTable();
        this.dataList[DataTableType.MonsterData] = new MonsterDataTable();
        this.dataList[DataTableType.MonsterPattern] = new MonsterPatternDataTable();
        this.dataList[DataTableType.Skill] = new SkillDataTable();
        this.dataList[DataTableType.EffectData] = new EffectDataTable();
        this.dataList[DataTableType.StageData] = new StageDataTable();
        this.dataList[DataTableType.AttackMotionProfile] = new AttackMotionProfileDataTable();
        this.dataList[DataTableType.StageLayout] = new StageLayoutDataTable();
        this.dataList[DataTableType.ChunkResource] = new ChunkResourceDataTable();
        this.dataList[DataTableType.MonsterEncounter] = new MonsterEncounterDataTable();

        this.preloadDataTablesAsync().Forget();
    }

    protected override void OnSingletonDestroyed()
    {
        foreach (var pair in this.dataList)
        {
            pair.Value?.Release();
        }
        this.dataList.Clear();
        base.OnSingletonDestroyed();
    }

    private async UniTaskVoid preloadDataTablesAsync()
    {
        const string targetLabel = "Datas";
        var locationsHandle = Addressables.LoadResourceLocationsAsync(targetLabel, typeof(TextAsset));
        await locationsHandle;

        if (locationsHandle.Status == AsyncOperationStatus.Succeeded && locationsHandle.Result.Count > 0)
        {
            int loadedCount = 0;
            int totalLocations = locationsHandle.Result.Count;

            ResourceManager.Instance.LoadAssetsAsync<TextAsset>(locationsHandle.Result, asset =>
            {
                try
                {
                    if (asset != null && !string.IsNullOrEmpty(asset.text))
                        this.parseAndCacheCsv(asset.name, asset.text);
                }
                catch (Exception exception)
                {
                    Debug.LogError($"[DataTableManager] CSV '{asset?.name}' rejected: {exception.Message}");
                }
                finally
                {
                    loadedCount++;
                    if (loadedCount >= totalLocations)
                    {
                        this.isLoaded = true;
                        this.loadCompletionSource.TrySetResult();
                        Debug.Log("<color=cyan><b>[DataTableManager] CSV preload completed with file-level isolation.</b></color>");
                    }
                }
            }, this.GetCancellationTokenOnDestroy());
        }
        else
        {
            // Addressables 미준비 시 Resources Fallback 캐싱
            Debug.LogWarning($"[DataTableManager] Addressables '{targetLabel}' 라벨을 찾지 못함. Fallback 로드 실행.");
            this.fallbackLoadFromResources();
            this.isLoaded = true;
            this.loadCompletionSource.TrySetResult();
        }

        Addressables.Release(locationsHandle);
    }

    private void parseAndCacheCsv(string assetName, string csvText)
    {
        if (string.IsNullOrWhiteSpace(csvText))
        {
            Debug.LogError($"[DataTableManager Error] {assetName} CSV 내용이 비어있습니다.");
            return;
        }

        // 오직 idx 기반으로만 DataTableType 식별 (파일명 검사 없음)
        uint firstIdx = this.extractFirstRowIdx(csvText);
        DataTableType dtt = Util.GetDataTableType(firstIdx);

        if (dtt == DataTableType.None)
        {
            Debug.LogError($"[DataTableManager Error] {assetName}의 첫 번째 Idx({firstIdx})에 해당하는 올바른 DataTableType을 찾을 수 없습니다! (idx 규칙 위반)");
            return;
        }

        if (!this.dataList.TryGetValue(dtt, out var loader))
        {
            Debug.LogError($"[DataTableManager Error] DataTableType.{dtt} (에셋: {assetName}) 에 매핑된 파서(IDataLoad)가 dataList에 등록되지 않았습니다.");
            return;
        }

        loader.LoadData(csvText);
        Debug.Log($"<color=green>[DataTableManager] 데이터 캐싱 성공: {assetName} (Type: {dtt}, Count: {loader.GetDataCount()})</color>");
    }

    private uint extractFirstRowIdx(string csvText)
    {
        using (var reader = new StringReader(csvText))
        {
            string headerLine = reader.ReadLine();
            if (headerLine == null) return 0;

            string firstDataLine = reader.ReadLine();
            while (firstDataLine != null && string.IsNullOrWhiteSpace(firstDataLine))
            {
                firstDataLine = reader.ReadLine();
            }

            if (firstDataLine == null) return 0;

            string[] cols = firstDataLine.Split(',');
            if (cols.Length > 0 && uint.TryParse(cols[0].Trim(), out uint idx))
            {
                return idx;
            }
        }
        return 0;
    }

    private void fallbackLoadFromResources()
    {
        TextAsset[] csvAssets = Resources.LoadAll<TextAsset>("datas");
        if (csvAssets != null && csvAssets.Length > 0)
        {
            foreach (var asset in csvAssets)
            {
                try { this.parseAndCacheCsv(asset.name, asset.text); }
                catch (Exception exception) { Debug.LogError($"[DataTableManager] CSV '{asset.name}' rejected: {exception.Message}"); }
            }
        }

#if UNITY_EDITOR
        string[] guids = UnityEditor.AssetDatabase.FindAssets("t:TextAsset", new[] { "Assets/datas" });
        foreach (var guid in guids)
        {
            string path = UnityEditor.AssetDatabase.GUIDToAssetPath(guid);
            TextAsset asset = UnityEditor.AssetDatabase.LoadAssetAtPath<TextAsset>(path);
            if (asset != null)
            {
                try { this.parseAndCacheCsv(asset.name, asset.text); }
                catch (Exception exception) { Debug.LogError($"[DataTableManager] CSV '{asset.name}' rejected: {exception.Message}"); }
            }
        }
#endif
    }
}
