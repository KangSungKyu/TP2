using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

public interface IPool
{
    public void Clear();
}

public class SimplePool<T> : IPool where T : Component

{
    // Optional helpers
    public int Available => pool.Count;
    public int TotalOwned => ownedInstanceIds.Count;
    public int TotalCreated => totalCount;
    public int Capacity => capacity;

    private T prefab = null;
    private Transform parent = null;

    // Pooled object with reference to the prefab it was created from
    private struct PooledObject
    {
        public T Instance;
        public T Prefab;

        public PooledObject(T instance, T prefab)
        {
            Instance = instance;
            Prefab = prefab;
        }
    }

    private bool isAddressable = false;
    private string addressableKey = string.Empty;

    private Queue<PooledObject> pool = null;
    private System.Action<T> onGet = null;
    private System.Action<T> onRelease = null;

    // Track ownership and currently pooled instances to prevent double-release or cross-pool release
    private HashSet<EntityId> ownedInstanceIds = new HashSet<EntityId>();
    private HashSet<EntityId> pooledInstanceIds = new HashSet<EntityId>();

    // capacity: maximum total instances that may be created by this pool
    private int capacity = 0;
    private int totalCount = 0; // number of instances created by this pool

    public SimplePool(int capacity, T prefab, Transform parent = null, System.Action<T> onGet = null, System.Action<T> onRelease = null)
    {
        this.capacity = capacity;
        this.prefab = prefab;
        this.parent = parent;
        this.pool = new Queue<PooledObject>();
        this.onGet = onGet;
        this.onRelease = onRelease;
        this.isAddressable = false;
        this.addressableKey = string.Empty;
    }

    /// <summary>
    /// Create a pool that uses Addressables via ResourceManager to instantiate items.
    /// Note: this constructor does not synchronously create instances. Call PrewarmAsync to populate the pool.
    /// </summary>
    public SimplePool(int capacity, string addressableKey, Transform parent = null, System.Action<T> onGet = null, System.Action<T> onRelease = null)
    {
        this.capacity = capacity;
        this.prefab = null;
        this.parent = parent;
        this.pool = new Queue<PooledObject>();
        this.onGet = onGet;
        this.onRelease = onRelease;
        this.isAddressable = true;
        this.addressableKey = addressableKey;
    }

    public T Get()
    {
        if (pool.Count > 0)
        {
            PooledObject p = pool.Dequeue();
            T obj = p.Instance;

            // remove from pooled ids
            EntityId id = obj.GetEntityId();
            pooledInstanceIds.Remove(id);

            obj.gameObject.SetActive(true);
            onGet?.Invoke(obj);

            return obj;
        }

        // create new instance and record ownership if capacity allows
        if (totalCount < capacity)
        {
            if (!isAddressable && prefab != null)
            {
                T newObj = UnityEngine.Object.Instantiate(prefab, parent);
                EntityId nid = newObj.GetEntityId();
                ownedInstanceIds.Add(nid);
                totalCount++;

                onGet?.Invoke(newObj);

                return newObj;
            }

            Debug.LogWarning($"SimplePool.Get: addressable pool empty and synchronous creation is not available for {typeof(T).Name}. Consider calling PrewarmAsync before Get.");
            return null;
        }

        Debug.LogWarning($"SimplePool.Get: reached capacity ({capacity}) for {typeof(T).Name}, cannot create new instance.");
        return null;
    }

    public void Release(T obj)
    {
        if (obj == null)
            return;

        EntityId id = obj.GetEntityId();

        // If this instance was not created by this pool, destroy it to avoid cross-pool reuse
        if (!ownedInstanceIds.Contains(id))
        {
            Debug.LogWarning($"SimplePool.Release: object (id:{id}) was not created by this pool. Destroying it to avoid cross-pool issues.");
            UnityEngine.Object.Destroy(obj.gameObject);
            return;
        }

        // Prevent double-release
        if (pooledInstanceIds.Contains(id))
        {
            Debug.LogWarning($"SimplePool.Release: object (id:{id}) is already pooled. Ignoring release.");
            return;
        }

        onRelease?.Invoke(obj);
        obj.gameObject.transform.SetParent(parent);
        obj.gameObject.SetActive(false);

        pool.Enqueue(new PooledObject(obj, prefab));
        pooledInstanceIds.Add(id);
    }


    public void Clear()
    {
        while (pool.Count > 0)
        {
            PooledObject pooled = pool.Dequeue();

            if (pooled.Instance is GameObject gobj)
            {
                if (isAddressable)
                {
                    ResourceManager.Instance.ReleaseInstance(gobj);
                }
                else
                {
                    GameObject.Destroy(gobj);
                }
            }
        }
    }

    // Create inactive instances up to requested count (bounded by capacity)
    public void Prewarm(int count)
    {
        int canCreate = Mathf.Max(0, capacity - totalCount);
        int toCreate = Mathf.Min(count, canCreate);

        for (int i = 0; i < toCreate; i++)
        {
            T newObj = UnityEngine.Object.Instantiate(prefab, parent);
            newObj.gameObject.SetActive(false);
            EntityId nid = newObj.GetEntityId();
            ownedInstanceIds.Add(nid);
            pooledInstanceIds.Add(nid);
            pool.Enqueue(new PooledObject(newObj, prefab));
            totalCount++;
        }
    }

    /// <summary>
    /// Async prewarm for addressable-backed pool. Instantiates up to count items using ResourceManager.InstantiateAsyncTask.
    /// </summary>
    public async Task PrewarmAsync(int count)
    {
        if (!isAddressable || string.IsNullOrEmpty(addressableKey))
        {
            Prewarm(count);
            return;
        }

        int canCreate = Mathf.Max(0, capacity - totalCount);
        int toCreate = Mathf.Min(count, canCreate);

        for (int i = 0; i < toCreate; i++)
        {
            GameObject go = await ResourceManager.Instance.InstantiateAsyncTask(addressableKey, parent);
            if (go == null)
            {
                Debug.LogError($"PrewarmAsync: failed to instantiate addressable '{addressableKey}'");
                break;
            }

            T newObj = go.GetComponent<T>();
            if (newObj == null)
            {
                Debug.LogError($"PrewarmAsync: instantiated object does not contain component {typeof(T).Name}");
                GameObject.Destroy(go);
                break;
            }

            newObj.gameObject.SetActive(false);
            EntityId nid = newObj.GetEntityId();
            ownedInstanceIds.Add(nid);
            pooledInstanceIds.Add(nid);
            pool.Enqueue(new PooledObject(newObj, prefab));
            totalCount++;
        }
    }
}