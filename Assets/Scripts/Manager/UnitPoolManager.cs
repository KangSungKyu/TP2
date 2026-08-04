using Cysharp.Threading.Tasks;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;

/// <summary>
/// 유닛 전용 오브젝트 풀링 매니저.
/// 플레이어 및 몬스터(SpearSentry, ShadowStalker, WaveHeavy, Garon)의 Instantiate/Destroy 수명주기를
/// 풀링 시스템으로 100% 통합 관리하여 GC 할당 최적화를 수행합니다.
/// </summary>
public class UnitPoolManager : Singleton<UnitPoolManager>
{
    private readonly Dictionary<string, Queue<GameObject>> poolDictionary = new Dictionary<string, Queue<GameObject>>();
    private readonly List<Monster> activeMonsterList = new List<Monster>();

    public async UniTask<Player> SpawnPlayerAsync(Vector3 position)
    {
        if (Player.Instance != null)
        {
            var motor = Player.Instance.GetComponent<KinematicMotor2D>();
            if (motor != null)
            {
                motor.Teleport(position);
                motor.SetTargetVelocityX(0f);
                motor.SetVelocityY(0f);
            }
            else
            {
                Player.Instance.transform.position = position;
            }

            var stats = Player.Instance.GetComponent<CombatStats>();
            if (stats != null) stats.InitStats();

            Player.Instance.gameObject.SetActive(true);
            return Player.Instance;
        }

        string poolKey = "Player";
        GameObject playerObj = GetFromPool(poolKey);

        if (playerObj == null && ResourceManager.Instance != null)
        {
            try
            {
                playerObj = await ResourceManager.Instance.InstantiateAsyncTask(poolKey, null, position, Quaternion.identity);
            }
            catch { }
        }

        if (playerObj == null)
        {
            // 1단계 폴백: Resources.Load
            GameObject resPrefab = Resources.Load<GameObject>("Prefabs/Player");
            if (resPrefab == null) resPrefab = Resources.Load<GameObject>("Player");

#if UNITY_EDITOR
            // 2단계 폴백: UnityEditor AssetDatabase 로드 (에디터 테스트 환경)
            if (resPrefab == null)
            {
                resPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Player.prefab");
            }
#endif

            if (resPrefab != null)
            {
                playerObj = Instantiate(resPrefab, position, Quaternion.identity);
                Debug.Log("<color=green>[UnitPoolManager] Resources / AssetDatabase 폴백 플레이어 스폰 성공!</color>");
            }
            else
            {
                // 3단계 폴백: 동적 GameObject 및 Player 컴포넌트 신설
                playerObj = new GameObject("Player");
                playerObj.transform.position = position;
                playerObj.AddComponent<Player>();
                Debug.LogWarning("[UnitPoolManager] 플레이어 프리팹 미발견 ➔ 기본 Player 런타임 폴백 생성!");
            }
        }

        if (playerObj != null)
        {
            playerObj.name = "Player";
            playerObj.transform.position = position;
            playerObj.SetActive(true);

            var pComp = playerObj.GetComponent<Player>();
            var pStats = playerObj.GetComponent<CombatStats>();
            if (pStats != null) pStats.InitStats();

            return pComp;
        }

        return null;
    }

    public async UniTask<Monster> SpawnMonsterAsync(uint unitId, Vector3 position)
    {
        string prefabKey = ResolveMonsterPrefabKey(unitId);
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
            var motor = monsterObj.GetComponent<KinematicMotor2D>();
            if (motor != null)
            {
                motor.enabled = true;
                motor.Teleport(position);
                motor.SetTargetVelocityX(0f);
                motor.SetVelocityY(0f);
            }

            var cols = monsterObj.GetComponentsInChildren<Collider2D>(true);
            foreach (var c in cols) c.enabled = true;

            var monsterComp = monsterObj.GetComponent<Monster>();
            if (monsterComp != null)
            {
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

        if (unit is Monster monster)
        {
            activeMonsterList.Remove(monster);
            string poolKey = ResolveMonsterPrefabKey(unit.UnitIdx);
            ReturnToPool(poolKey, unit.gameObject);
        }
        else if (unit is Player)
        {
            ReturnToPool("Player", unit.gameObject);
        }
        else
        {
            unit.gameObject.SetActive(false);
        }
    }

    public void DespawnAllMonsters()
    {
        for (int i = activeMonsterList.Count - 1; i >= 0; i--)
        {
            var monster = activeMonsterList[i];
            if (monster != null && monster.gameObject != null)
            {
                string poolKey = ResolveMonsterPrefabKey(monster.UnitIdx);
                monster.gameObject.SetActive(false);
                ReturnToPool(poolKey, monster.gameObject);
            }
        }
        activeMonsterList.Clear();
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

    private string ResolveMonsterPrefabKey(uint unitId)
    {
        switch (unitId)
        {
            case 3101: case 1003: case 5101: return "SpearSentry";
            case 3102: case 1004: case 5102: return "ShadowStalker";
            case 3103: case 1005: case 5103: return "WaveHeavy";
            case 5001: case 6001: return "Garon";
            default: return "SpearSentry";
        }
    }
}
