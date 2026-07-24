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

// Resource manager using Commons.Singleton<T>
public class ResourceManager : Singleton<ResourceManager>
{
    // Keep track of handles to allow safe release
    private readonly static string labelName = "Remote";
    private readonly Dictionary<string, AsyncOperationHandle> loadHandles = new Dictionary<string, AsyncOperationHandle>();
    private readonly List<AsyncOperationHandle> instantiateHandles = new List<AsyncOperationHandle>();

    public async UniTask InitAsync(Action onComplete = null, CancellationToken cancellationToken = default)
    {
        await Addressables.InitializeAsync().ToUniTask(cancellationToken: cancellationToken);

        var updateHandle = Addressables.CheckForCatalogUpdates(false);

        await updateHandle;

        if (updateHandle.Status == AsyncOperationStatus.Succeeded)
        {
            var catalogs = updateHandle.Result;

            if (catalogs.Count > 0)
            {
                await Addressables.UpdateCatalogs(catalogs).ToUniTask(cancellationToken: cancellationToken);
            }
        }

        if(updateHandle.IsValid())
        {
            Addressables.Release(updateHandle);
        }

        await StartDownloadAsync(onComplete, cancellationToken);
    }


    public void LoadAssetAsync<T>(string key, Action<T> onLoaded) where T : class
    {
        if(loadHandles.ContainsKey(key))
        {
            onLoaded?.Invoke((T)loadHandles[key].Result);
        }
        else
        {
            try
            {
                var handle = Addressables.LoadAssetAsync<T>(key);

                handle.Completed += (AsyncOperationHandle<T> op) =>
                {
                    if (op.Status == AsyncOperationStatus.Succeeded)
                    {
                        if(!loadHandles.ContainsKey(key))
                        {
                            loadHandles.Add(key, handle);
                        }

                        onLoaded?.Invoke(op.Result);
                    }
                    else
                    {
                        Debug.LogError($"LoadAssetAsync<{typeof(T).Name}> failed for key={key}: {op.OperationException}");
                        onLoaded?.Invoke(null);
                    }
                };
            }
            catch (Exception ex)
            {
                Debug.LogError($"LoadAssetAsync<{typeof(T).Name}> threw for key={key}: {ex}");
                onLoaded?.Invoke(null);
            }
        }
    }

    public T GetResource<T>(string key) where T : class
    {
        T resource = null;

        if(loadHandles.ContainsKey(key))
        {
            resource = (T)loadHandles[key].Result;
        }

        return resource;
    }

    public Sprite GetSpriteFromAtlas(string atlasKey, string key)
    {
        Sprite resource = null;

        if(loadHandles.ContainsKey(atlasKey))
        {
            SpriteAtlas atlas = loadHandles[atlasKey].Result as SpriteAtlas;

            resource = atlas.GetSprite(key);
        }

        return resource;
    }

    public Task<T> LoadAssetAsyncTask<T>(string key) where T : class
    {
        if(loadHandles.ContainsKey(key))
        {
            return Task.FromResult((T)loadHandles[key].Result);
        }
        else
        {
            var tcs = new TaskCompletionSource<T>();
            try
            {
                var handle = Addressables.LoadAssetAsync<T>(key);

                handle.Completed += (AsyncOperationHandle<T> op) =>
                {
                    if (op.Status == AsyncOperationStatus.Succeeded)
                    {
                        loadHandles.Add(key, handle);
                        tcs.SetResult(op.Result);
                    }
                    else
                    {
                        tcs.SetException(op.OperationException ?? new Exception("Addressables load failed"));
                    }
                };
            }
            catch (Exception ex)
            {
                tcs.SetException(ex);
            }

            return tcs.Task;
        }
    }

    public async void LoadAssetsAsync<T>(IList<IResourceLocation> locList, Action<T> onComp, CancellationToken cancellationToken = default)
    {
        if (locList == null || locList.Count < 0)
            return;

        var loadHandle = Addressables.LoadAssetsAsync<T>(locList, onComp);

        try
        {
            var resultList = await loadHandle.ToUniTask(cancellationToken: cancellationToken);
        }
        catch(System.Exception ex)
        {
            Debug.LogError(ex);
        }
        finally
        {
            if(loadHandle.IsValid())
            {
                Addressables.Release(loadHandle);
            }
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

            await handle.Task;

            if (handle.Status == AsyncOperationStatus.Succeeded)
            {
                instantiateHandles.Add(handle);

                return handle.Result;
            }
            else
            {
                throw handle.OperationException ?? new Exception("Addressables instantiate failed");
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"InstantiateAsyncTask threw for key={key}: {ex}");
            return null;
        }
    }

    public void ReleaseInstance(GameObject go)
    {
        if (go == null) 
            return;

        try
        {
            int idx = instantiateHandles.FindIndex((o) => (GameObject)o.Result == go);

            Addressables.ReleaseInstance(go);

            if(idx > -1)
            {
                instantiateHandles.RemoveAt(idx);
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"ReleaseInstance threw: {ex}");
        }
    }

    public void Release(string key)
    {
        AsyncOperationHandle handle = default;

        if(loadHandles.ContainsKey(key))
        {
            handle = loadHandles[key];

            try
            {
                if (handle.IsValid())
                {
                    Addressables.Release(handle);
                    loadHandles.Remove(key);
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"Release(handle) threw: {ex}");
            }
        }
    }

    public void ReleaseAll()
    {
        foreach(var pair in loadHandles)
        {
            var h = pair.Value;

            try
            {
                if (h.IsValid())
                { 
                    Addressables.Release(h);
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"ReleaseAll: failed to release handle: {ex}");
            }
        }

        loadHandles.Clear();

        try
        {
            foreach (var h in instantiateHandles)
            {
                ReleaseInstance(h.Result as GameObject);
            }
        }
        catch(Exception ex)
        {
            Debug.LogError($"ReleaseAll: failed to release inst handle:{ex}");
        }

        instantiateHandles.Clear();
    }

    protected override void OnSingletonAwake()
    {
        base.OnSingletonAwake();
        Debug.Log("ResourceMgr initialized");

        SpriteAtlasManager.atlasRequested += OnAtlasRequested;
    }

    protected override void OnSingletonDestroyed()
    {
        SpriteAtlasManager.atlasRequested -= OnAtlasRequested;

        base.OnSingletonDestroyed();
        //ReleaseAll();
    }

    private async UniTask StartDownloadAsync(System.Action onResourceLoad, CancellationToken cancellationToken = default)
    {
        var locationHandle = Addressables.LoadResourceLocationsAsync(labelName, typeof(object));

        await locationHandle;

        if (locationHandle.Status == AsyncOperationStatus.Succeeded)
        {
            Debug.Log($"찾은 결과 개수: {locationHandle.Result.Count}");
            // 2. 다운로드 시작
            AsyncOperationHandle handle = Addressables.DownloadDependenciesAsync(locationHandle.Result);

            try
            {
                await handle.ToUniTask(progress: Progress.Create<float>((progress) =>
                {
                    Debug.Log($"다운로드 중: {progress * 100}%");
                }), cancellationToken: cancellationToken);

                if (handle.Status == AsyncOperationStatus.Succeeded)
                {
                    Debug.Log("다운로드 완료!");
                    // 이제 리소스를 로드해도 됩니다.
                    onResourceLoad?.Invoke();
                }
                else
                {
                    Debug.LogError("다운로드 실패: " + handle.OperationException);
                }
            }
            finally
            {
                if(handle.IsValid())
                {
                    Addressables.Release(handle);
                }
            }
        }
        else
        {
            Debug.LogError($"그룹을 찾을 수 없습니다. (상태: {locationHandle.Status})");
            // 발견된 모든 그룹을 출력해서 이름이 일치하는지 확인
            foreach (var location in locationHandle.Result)
            {
                Debug.Log($"발견된 키: {location.PrimaryKey}");
            }
        }

        if(locationHandle.IsValid())
        {
            Addressables.Release(locationHandle);
        }
    }

    private void OnAtlasRequested(string tag, Action<SpriteAtlas> onComplete)
    {
        string addrKey = tag;

        Debug.Log($"atlas requested: addrKey: {addrKey}");

        LoadAssetAsync(addrKey, onComplete);
    }
}