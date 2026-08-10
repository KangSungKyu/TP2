using Cysharp.Threading.Tasks;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.ResourceManagement.ResourceLocations;
using UnityEngine.U2D;

/// <summary>
/// Addressables 기반 리소스 관리자 (Singleton)
/// TP1의 카탈로그 검사, 의존성 다운로드 및 SpriteAtlas 연동 구조를 표준 채택하였습니다.
/// 예외 발생 시 Fallback 없이 strict 에러 처리를 수행합니다.
/// </summary>
public class ResourceManager : Singleton<ResourceManager>
{
    // =========================================================================
    // 1. CONST & PRIVATE FIELDS (camelCase, No '_' prefix)
    // =========================================================================

    private const string TargetLabel = "Datas";
    private readonly Dictionary<string, AsyncOperationHandle> loadHandles = new Dictionary<string, AsyncOperationHandle>();
    private readonly List<AsyncOperationHandle> instantiateHandles = new List<AsyncOperationHandle>();
    private bool isInitialized = false;


    // =========================================================================
    // 2. PUBLIC METHODS (PascalCase)
    // =========================================================================

    /// <summary>
    /// InitScene의 부팅 단계에서 호출되는 Addressables 초기화, 카탈로그 검사 및 다운로드 프로세스입니다.
    /// </summary>
    public async UniTask InitAsync(Action onComplete = null, CancellationToken cancellationToken = default)
    {
        if (this.isInitialized)
        {
            onComplete?.Invoke();
            return;
        }

        try
        {
            // 1. Addressables 시스템 비동기 초기화
            await Addressables.InitializeAsync().ToUniTask(cancellationToken: cancellationToken);

            // 2. 최신 원격 카탈로그 검사
            var updateHandle = Addressables.CheckForCatalogUpdates(false);
            await updateHandle.ToUniTask(cancellationToken: cancellationToken);

            if (updateHandle.Status == AsyncOperationStatus.Succeeded)
            {
                var catalogs = updateHandle.Result;
                if (catalogs != null && catalogs.Count > 0)
                {
                    Debug.Log($"[ResourceManager] 카탈로그 업데이트 발견 ({catalogs.Count}개). 업데이트를 진행합니다.");
                    await Addressables.UpdateCatalogs(catalogs).ToUniTask(cancellationToken: cancellationToken);
                }
            }

            if (updateHandle.IsValid())
            {
                Addressables.Release(updateHandle);
            }

            // 3. 라벨 기반 의존성 다운로드 및 준비
            await this.startDownloadAsync(onComplete, cancellationToken);

            this.isInitialized = true;
            Debug.Log("<color=green><b>[ResourceManager] Addressables 초기화 및 카탈로그 동기화 완결!</b></color>");
        }
        catch (Exception ex)
        {
            Debug.LogError($"[ResourceManager Error] InitAsync 초기화 실패: {ex.Message}");
            throw;
        }
    }

    public void LoadAssetAsync<T>(string key, Action<T> onLoaded) where T : class
    {
        if (this.loadHandles.TryGetValue(key, out var handle))
        {
            onLoaded?.Invoke(handle.Result as T);
            return;
        }

        try
        {
            var loadHandle = Addressables.LoadAssetAsync<T>(key);
            loadHandle.Completed += (AsyncOperationHandle<T> op) =>
            {
                if (op.Status == AsyncOperationStatus.Succeeded)
                {
                    if (!this.loadHandles.ContainsKey(key))
                    {
                        this.loadHandles.Add(key, loadHandle);
                    }
                    onLoaded?.Invoke(op.Result);
                }
                else
                {
                    Debug.LogWarning($"[ResourceManager Warning] Addressables Key '{key}' 로드 실패: {op.OperationException?.Message}");
                    onLoaded?.Invoke(null);
                }
            };
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[ResourceManager Warning] Addressables Key '{key}' 유효하지 않음 (InvalidKey): {ex.Message}");
            onLoaded?.Invoke(null);
        }
    }

    public async UniTask<T> LoadAssetAsync<T>(string key) where T : UnityEngine.Object
    {
        if (this.loadHandles.TryGetValue(key, out var handle))
        {
            if (handle.Status == AsyncOperationStatus.Succeeded)
            {
                return handle.Result as T;
            }
        }

        var loadHandle = Addressables.LoadAssetAsync<T>(key);
        await loadHandle.Task;

        if (loadHandle.Status == AsyncOperationStatus.Succeeded)
        {
            if (!this.loadHandles.ContainsKey(key))
            {
                this.loadHandles.Add(key, loadHandle);
            }
            return loadHandle.Result;
        }

        Debug.LogError($"[ResourceManager Error] LoadAssetAsync 실패 (Key: {key}): {loadHandle.OperationException}");
        return null;
    }

    public async Task<T> LoadAssetAsyncTask<T>(string key) where T : class
    {
        if (this.loadHandles.TryGetValue(key, out var handle))
        {
            return (T)handle.Result;
        }

        var tcs = new TaskCompletionSource<T>();
        try
        {
            var loadHandle = Addressables.LoadAssetAsync<T>(key);
            loadHandle.Completed += (op) =>
            {
                if (op.Status == AsyncOperationStatus.Succeeded)
                {
                    if (!this.loadHandles.ContainsKey(key))
                    {
                        this.loadHandles.Add(key, loadHandle);
                    }
                    tcs.SetResult(op.Result);
                }
                else
                {
                    Debug.LogError($"[ResourceManager Error] LoadAssetAsyncTask<{typeof(T).Name}> 실패 (Key: {key}): {op.OperationException}");
                    tcs.SetException(op.OperationException ?? new Exception($"Addressables load failed for key={key}"));
                }
            };
        }
        catch (Exception ex)
        {
            tcs.SetException(ex);
        }

        return await tcs.Task;
    }

    public void LoadAssetsAsync<T>(IList<IResourceLocation> locList, Action<T> onComp, CancellationToken cancellationToken = default) where T : UnityEngine.Object
    {
        if (locList == null || locList.Count == 0) return;

        foreach (var location in locList)
        {
            if (cancellationToken.IsCancellationRequested) break;

            string key = location.PrimaryKey;
            if (this.loadHandles.TryGetValue(key, out var handle))
            {
                if (handle.Status == AsyncOperationStatus.Succeeded)
                {
                    onComp?.Invoke(handle.Result as T);
                    continue;
                }
            }

            var loadHandle = Addressables.LoadAssetAsync<T>(location);
            loadHandle.Completed += (op) =>
            {
                if (op.Status == AsyncOperationStatus.Succeeded)
                {
                    if (!this.loadHandles.ContainsKey(key))
                    {
                        this.loadHandles.Add(key, op);
                    }
                    onComp?.Invoke(op.Result);
                }
                else
                {
                    Debug.LogError($"[ResourceManager Error] LoadAssetsAsync 위치 로드 실패 (Location: {key}): {op.OperationException}");
                }
            };
        }
    }

    public async Task<GameObject> InstantiateAsyncTask(string key, Transform parent = null, Vector3? position = null, Quaternion? rotation = null)
    {
        try
        {
            AsyncOperationHandle<GameObject> handle;

            if (position.HasValue || rotation.HasValue)
            {
                handle = Addressables.InstantiateAsync(key, position ?? Vector3.zero, rotation ?? Quaternion.identity, parent);
            }
            else
            {
                handle = Addressables.InstantiateAsync(key, parent);
            }

            await handle.ToUniTask();

            if (handle.Status == AsyncOperationStatus.Succeeded)
            {
                this.instantiateHandles.Add(handle);
                return handle.Result;
            }

            throw handle.OperationException ?? new Exception($"Addressables instantiate failed for key={key}");
        }
        catch (Exception ex)
        {
            Debug.LogError($"[ResourceManager Error] '{key}' 리소스 로드 실패! Addressables 어드레스 등록을 확인하세요. (사유: {ex.Message})");
            return null;
        }
    }

    public T GetResource<T>(string key) where T : class
    {
        if (this.loadHandles.TryGetValue(key, out var handle))
        {
            return handle.Result as T;
        }
        return null;
    }

    public Sprite GetSpriteFromAtlas(string atlasKey, string key)
    {
        if (this.loadHandles.TryGetValue(atlasKey, out var handle))
        {
            if (handle.Result is SpriteAtlas atlas)
            {
                return atlas.GetSprite(key);
            }
        }
        return null;
    }

    public void ReleaseInstance(GameObject go)
    {
        if (go == null) return;

        try
        {
            int idx = this.instantiateHandles.FindIndex(o => o.IsValid() && o.Result == (object)go);
            if (idx < 0) return;
            var handle = this.instantiateHandles[idx];
            this.instantiateHandles.RemoveAt(idx);
            if (handle.IsValid()) Addressables.ReleaseInstance(handle);
        }
        catch (Exception ex)
        {
            Debug.LogError($"[ResourceManager Error] ReleaseInstance 실패: {ex}");
        }
    }

    public void Release(string key)
    {
        if (this.loadHandles.TryGetValue(key, out var handle))
        {
            try
            {
                if (handle.IsValid())
                {
                    Addressables.Release(handle);
                }
                this.loadHandles.Remove(key);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ResourceManager Error] Release 실패 (Key: {key}): {ex}");
            }
        }
    }

    public void ReleaseAll()
    {
        foreach (var pair in this.loadHandles)
        {
            try
            {
                if (pair.Value.IsValid())
                {
                    Addressables.Release(pair.Value);
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ResourceManager Error] ReleaseAll 로드 핸들 해제 실패: {ex}");
            }
        }
        this.loadHandles.Clear();

        foreach (var handle in this.instantiateHandles)
        {
            try
            {
                if (handle.IsValid() && handle.Result != null)
                {
                    Addressables.ReleaseInstance(handle.Result as GameObject);
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ResourceManager Error] ReleaseAll 인스턴스 핸들 해제 실패: {ex}");
            }
        }
        this.instantiateHandles.Clear();
    }


    // =========================================================================
    // 3. PROTECTED & PRIVATE METHODS (camelCase)
    // =========================================================================

    protected override void OnSingletonAwake()
    {
        base.OnSingletonAwake();
        SpriteAtlasManager.atlasRequested += this.onAtlasRequested;
    }

    protected override void OnSingletonDestroyed()
    {
        SpriteAtlasManager.atlasRequested -= this.onAtlasRequested;
        this.ReleaseAll();
        base.OnSingletonDestroyed();
    }

    private async UniTask startDownloadAsync(Action onResourceLoad, CancellationToken cancellationToken = default)
    {
        var locationHandle = Addressables.LoadResourceLocationsAsync(TargetLabel, typeof(object));
        await locationHandle.ToUniTask(cancellationToken: cancellationToken);

        if (locationHandle.Status == AsyncOperationStatus.Succeeded)
        {
            if (locationHandle.Result != null && locationHandle.Result.Count > 0)
            {
                var downloadHandle = Addressables.DownloadDependenciesAsync(locationHandle.Result);
                try
                {
                    await downloadHandle.ToUniTask(progress: Progress.Create<float>(p =>
                    {
                        Debug.Log($"[ResourceManager] Addressables 의존성 다운로드 중: {p * 100:F1}%");
                    }), cancellationToken: cancellationToken);

                    if (downloadHandle.Status == AsyncOperationStatus.Succeeded)
                    {
                        Debug.Log("[ResourceManager] 의존성 다운로드 완결!");
                        onResourceLoad?.Invoke();
                    }
                    else
                    {
                        Debug.LogError($"[ResourceManager Error] 의존성 다운로드 실패: {downloadHandle.OperationException}");
                    }
                }
                finally
                {
                    if (downloadHandle.IsValid())
                    {
                        Addressables.Release(downloadHandle);
                    }
                }
            }
            else
            {
                onResourceLoad?.Invoke();
            }
        }
        else
        {
            Debug.LogError($"[ResourceManager Error] 라벨 '{TargetLabel}' 리소스 위치 탐색 실패 (상태: {locationHandle.Status})");
        }

        if (locationHandle.IsValid())
        {
            Addressables.Release(locationHandle);
        }
    }

    private void onAtlasRequested(string tag, Action<SpriteAtlas> onComplete)
    {
        this.LoadAssetAsync(tag, onComplete);
    }
}
