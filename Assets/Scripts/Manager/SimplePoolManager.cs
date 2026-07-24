using Cysharp.Threading.Tasks;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SimplePoolManager : Singleton<SimplePoolManager>
{
    private readonly Dictionary<string, IPool> poolContainer = new Dictionary<string, IPool>();

    public async UniTask<bool> CreatePoolAsync<T>(string addressableKey, int capacity, int prewarmCount, Transform parent = null, Action<T> onGet = null, Action<T> onRelease = null) where T : MonoBehaviour
    {
        if (poolContainer.ContainsKey(addressableKey))
            return true;

        var pool = new SimplePool<T>(capacity, addressableKey, parent, onGet, onRelease);

        try
        {
            await pool.PrewarmAsync(prewarmCount);
            poolContainer.Add(addressableKey, pool);

            return true;
        }
        catch (Exception ex)
        {
            Debug.LogError($"[ObjectPoolManager] Prewarm failed for key '{addressableKey}': {ex}");
            return false;
        }
    }

    public T Get<T>(string addressableKey) where T : MonoBehaviour
    {
        if (poolContainer.TryGetValue(addressableKey, out var poolObj))
        {
            return ((SimplePool<T>)poolObj).Get();
        }

        Debug.LogError($"[ObjectPoolManager] Pool not found for key: {addressableKey}");
        
        return null;
    }

    public void Release<T>(string addressableKey, T instance) where T : MonoBehaviour
    {
        if (poolContainer.TryGetValue(addressableKey, out var poolObj))
        {
            ((SimplePool<T>)poolObj).Release(instance);
            return;
        }

        Debug.LogError($"[ObjectPoolManager] Pool not found for key: {addressableKey}");
    }

    public void ClearPool(string addressableKey)
    {
        if (poolContainer.TryGetValue(addressableKey, out var poolObj))
        {
            poolObj.Clear();
            poolContainer.Remove(addressableKey);
        }
    }

    public void ClearAll()
    {
        foreach (var pair in poolContainer.Values)
        {
            pair.Clear();
        }

        poolContainer.Clear();
    }

    public bool TryGetPool<T>(string addressableKey, out SimplePool<T> pool) where T : MonoBehaviour
    {
        if (poolContainer.TryGetValue(addressableKey, out var p) && p is SimplePool<T> typed)
        {
            pool = typed;

            return true;
        }

        pool = null;

        return false;
    }

    protected override void OnSingletonDestroyed()
    {
        ClearAll();
        base.OnSingletonDestroyed();
    }
}
