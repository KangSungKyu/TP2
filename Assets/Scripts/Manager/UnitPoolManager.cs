using Cysharp.Threading.Tasks;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using Gameplay.Combat;

/// <summary>
/// 유닛 전용 오브젝트 풀링 매니저.
/// 플레이어 및 몬스터(SpearSentry, ShadowStalker, WaveHeavy, Garon)의 Instantiate/Destroy 수명주기를
/// 풀링 시스템으로 100% 통합 관리하여 GC 할당 최적화를 수행합니다.
/// </summary>
public class UnitPoolManager : Singleton<UnitPoolManager>
{
    private const uint PlayerUnitIdx = 3001;
    private readonly Dictionary<string, Queue<GameObject>> poolDictionary = new Dictionary<string, Queue<GameObject>>();
    private readonly List<Monster> activeMonsterList = new List<Monster>();
    private readonly Dictionary<uint, Queue<UnitProjectile2D>> projectilePools = new Dictionary<uint, Queue<UnitProjectile2D>>();
    private readonly HashSet<UnitProjectile2D> activeProjectiles = new HashSet<UnitProjectile2D>();

    public async UniTask<Player> SpawnPlayerAsync(Vector3 position)
    {
        if (Player.Instance != null)
        {
            Player.Instance.ResetAfterDeath(position);

            var stats = Player.Instance.GetComponent<CombatStats>();
            if (stats != null) stats.InitStats();

            Player.Instance.gameObject.SetActive(true);
            return Player.Instance;
        }

        if (!TryResolveUnitPrefabKey(PlayerUnitIdx, out string poolKey)) return null;
        GameObject playerObj = GetFromPool(poolKey);

        if (playerObj == null && ResourceManager.Instance != null)
        {
            playerObj = await ResourceManager.Instance.InstantiateAsyncTask(poolKey, null, position, Quaternion.identity);
        }

        if (playerObj == null)
        {
            Debug.LogError($"[UnitPoolManager] Player unit idx {PlayerUnitIdx} failed to instantiate resource '{poolKey}'.");
            return null;
        }

        if (playerObj != null)
        {
            playerObj.name = "Player";
            playerObj.transform.position = position;
            playerObj.SetActive(true);

            var pComp = playerObj.GetComponent<Player>();
            if (pComp != null) pComp.ResetAfterDeath(position);
            var pStats = playerObj.GetComponent<CombatStats>();
            if (pStats != null) pStats.InitStats();

            return pComp;
        }

        return null;
    }

    public async UniTask<Monster> SpawnMonsterAsync(uint unitId, Vector3 position)
    {
        if (!TryResolveUnitPrefabKey(unitId, out string prefabKey)) return null;
        GameObject monsterObj = GetFromPool(prefabKey);

        if (monsterObj == null && ResourceManager.Instance != null)
        {
            monsterObj = await ResourceManager.Instance.InstantiateAsyncTask(prefabKey, null, position, Quaternion.identity);
        }

        if (monsterObj != null)
        {
            monsterObj.name = $"{prefabKey}_{unitId}";
            monsterObj.transform.position = position;
            monsterObj.transform.rotation = Quaternion.identity;

            // 물리 및 충돌체 복원
            var monsterComp = monsterObj.GetComponent<Monster>();
            if (monsterComp != null)
            {
                monsterComp.ResetAfterDeath(position);
                await monsterComp.InitUnitAsync(unitId);
            }

            var stats = monsterObj.GetComponent<CombatStats>();
            if (stats != null) stats.InitStats();

            monsterObj.SetActive(true);

            if (monsterComp != null && !activeMonsterList.Contains(monsterComp))
            {
                activeMonsterList.Add(monsterComp);
            }

            return monsterComp;
        }

        return null;
    }

    public void DespawnUnit(UnitBase unit)
    {
        if (unit == null || unit.gameObject == null) return;
        unit.ClearLocalHitStop();

        if (unit is Monster monster)
        {
            activeMonsterList.Remove(monster);
            if (TryResolveUnitPrefabKey(unit.UnitIdx, out string poolKey)) ReturnToPool(poolKey, unit.gameObject);
            else unit.gameObject.SetActive(false);
        }
        else if (unit is Player)
        {
            if (TryResolveUnitPrefabKey(PlayerUnitIdx, out string poolKey)) ReturnToPool(poolKey, unit.gameObject);
            else unit.gameObject.SetActive(false);
        }
        else
        {
            unit.gameObject.SetActive(false);
        }
    }

    public void DespawnAllMonsters()
    {
        DespawnAllProjectiles();
        for (int i = activeMonsterList.Count - 1; i >= 0; i--)
        {
            var monster = activeMonsterList[i];
            if (monster != null && monster.gameObject != null)
            {
                monster.gameObject.SetActive(false);
                if (TryResolveUnitPrefabKey(monster.UnitIdx, out string poolKey)) ReturnToPool(poolKey, monster.gameObject);
            }
        }
        activeMonsterList.Clear();
    }

    public async UniTask<UnitProjectile2D> SpawnUnitProjectileAsync(
        uint resourceIdx, UnitBase owner, uint ownerGeneration, Vector2 position,
        Vector2 direction, float speed, float maxDistance, float damage)
    {
        if (resourceIdx == 0) return null;
        if (speed <= 0f || maxDistance <= 0f)
        {
            Debug.LogError($"[UnitPoolManager] Invalid projectile motion for ResourceData idx {resourceIdx}: speed={speed}, maxDistance={maxDistance}.");
            return null;
        }

        var resourceTable = DataTableManager.Instance != null
            ? DataTableManager.Instance.GetDB<ResourceDataTable>(DataTableType.Resource)
            : null;
        if (resourceTable == null || !resourceTable.TryGetResource(resourceIdx, out var resourceData))
        {
            Debug.LogError($"[UnitPoolManager] Missing projectile ResourceData idx {resourceIdx}.");
            return null;
        }
        if (string.IsNullOrWhiteSpace(resourceData.Path))
        {
            Debug.LogError($"[UnitPoolManager] Empty projectile ResourceData path at idx {resourceIdx}.");
            return null;
        }

        UnitProjectile2D projectile = null;
        if (projectilePools.TryGetValue(resourceIdx, out var queue))
            while (queue.Count > 0 && projectile == null) projectile = queue.Dequeue();

        if (projectile == null)
        {
            if (ResourceManager.Instance == null)
            {
                Debug.LogError($"[UnitPoolManager] ResourceManager is unavailable for projectile ResourceData idx {resourceIdx}.");
                return null;
            }
            GameObject instance = await ResourceManager.Instance.InstantiateAsyncTask(
                resourceData.Path, null, position, Quaternion.identity);
            if (instance == null) return null;
            projectile = instance.GetComponent<UnitProjectile2D>();
            if (projectile == null)
            {
                Debug.LogError($"[UnitPoolManager] Projectile ResourceData idx {resourceIdx} has no UnitProjectile2D component.");
                ResourceManager.Instance.ReleaseInstance(instance);
                return null;
            }
        }

        if (owner == null || !owner.IsActionGenerationCurrent(ownerGeneration))
        {
            ReturnProjectile(resourceIdx, projectile);
            return null;
        }

        activeProjectiles.Add(projectile);
        projectile.Activate(resourceIdx, owner, ownerGeneration, position, direction, speed, maxDistance,
            damage);
        return projectile;
    }

    public void DespawnProjectilesOwnedBy(UnitBase owner)
    {
        if (owner == null || activeProjectiles.Count == 0) return;
        var snapshot = new List<UnitProjectile2D>(activeProjectiles);
        foreach (var projectile in snapshot)
            if (projectile != null && projectile.Owner == owner) projectile.ReturnToPool();
    }

    public void DespawnAllProjectiles()
    {
        if (activeProjectiles.Count == 0) return;
        var snapshot = new List<UnitProjectile2D>(activeProjectiles);
        foreach (var projectile in snapshot) if (projectile != null) projectile.ReturnToPool();
        activeProjectiles.Clear();
    }

    public void ReturnProjectile(uint resourceIdx, UnitProjectile2D projectile)
    {
        if (projectile == null || (!activeProjectiles.Remove(projectile) && !projectile.gameObject.activeSelf)) return;
        projectile.gameObject.SetActive(false);
        if (!projectilePools.TryGetValue(resourceIdx, out var queue))
        {
            queue = new Queue<UnitProjectile2D>();
            projectilePools.Add(resourceIdx, queue);
        }
        if (!queue.Contains(projectile)) queue.Enqueue(projectile);
    }

    private GameObject GetFromPool(string key)
    {
        if (poolDictionary.TryGetValue(key, out var queue) && queue.Count > 0)
        {
            var obj = queue.Dequeue();
            if (obj != null) return obj;
        }
        return null;
    }

    private void ReturnToPool(string key, GameObject obj)
    {
        if (obj == null) return;

        obj.SetActive(false);

        if (!poolDictionary.ContainsKey(key))
        {
            poolDictionary[key] = new Queue<GameObject>();
        }

        poolDictionary[key].Enqueue(obj);
    }

    private bool TryResolveUnitPrefabKey(uint unitId, out string prefabKey)
    {
        prefabKey = string.Empty;
        var dataManager = DataTableManager.Instance;
        var unitTable = dataManager != null ? dataManager.GetDB<UnitBaseDataTable>(DataTableType.UnitBase) : null;
        var resourceTable = dataManager != null ? dataManager.GetDB<ResourceDataTable>(DataTableType.Resource) : null;

        if (unitTable == null || !unitTable.TryGetUnitData(unitId, out var unitData))
        {
            Debug.LogError($"[UnitPoolManager] Missing UnitBaseData for unit idx {unitId}.");
            return false;
        }
        if (resourceTable == null || !resourceTable.TryGetResource(unitData.PrefabId, out var resourceData))
        {
            Debug.LogError($"[UnitPoolManager] Missing ResourceData idx {unitData.PrefabId} for unit idx {unitId}.");
            return false;
        }
        if (string.IsNullOrWhiteSpace(resourceData.Path))
        {
            Debug.LogError($"[UnitPoolManager] Empty ResourceData path at idx {unitData.PrefabId} for unit idx {unitId}.");
            return false;
        }

        prefabKey = resourceData.Path;
        return true;
    }
}
