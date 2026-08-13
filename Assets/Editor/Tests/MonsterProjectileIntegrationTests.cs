using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using Gameplay.Combat;
using NUnit.Framework;
using UnityEngine;

namespace QA.Tests
{
    public class MonsterProjectileIntegrationTests
    {
        [Test]
        public void ProjectileRequiredMonsterSet_IsExactly3105()
        {
            var monsters = new MonsterDataTable();
            var patterns = new MonsterPatternDataTable();
            monsters.LoadData(File.ReadAllText("Assets/Datas/MonsterBaseData.csv"));
            patterns.LoadData(File.ReadAllText("Assets/Datas/MonsterPatternData.csv"));

            var required = new HashSet<uint>();
            foreach (uint unitIdx in new[] { 3101u, 3102u, 3103u, 3104u, 3105u, 3201u })
            {
                Assert.IsTrue(monsters.TryGetMonsterData(unitIdx, out var monster), $"Missing MonsterBaseData for {unitIdx}.");
                bool usesProjectile = monster.PatternIdxList.Any(patternIdx =>
                    patterns.TryGetPatternData(patternIdx, out var pattern) && pattern.ProjectileResourceIdx != 0);
                if (usesProjectile) required.Add(unitIdx);
            }

            CollectionAssert.AreEquivalent(new[] { 3105u }, required);
        }

        [Test]
        public async Task Projectile1045_AddressablePoolDamageAndLifecycleContracts()
        {
            DataTableManager previousData = DataTableManager.Instance;
            ResourceManager previousResource = ResourceManager.Instance;
            UnitPoolManager previousPool = UnitPoolManager.Instance;
            Player previousPlayer = Player.Instance;
            GameObject dataObject = null, resourceObject = null, poolObject = null, ownerObject = null, playerObject = null;
            var instantiated = new HashSet<GameObject>();
            try
            {
                dataObject = new GameObject("ProjectileData_QA");
                dataObject.SetActive(false);
                var dataManager = dataObject.AddComponent<DataTableManager>();
                SetSingletonInstance(dataManager);
                var tables = (Dictionary<DataTableType, IDataLoad>)typeof(DataTableManager)
                    .GetField("dataList", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(dataManager);
                tables[DataTableType.Resource] = new ResourceDataTable();
                tables[DataTableType.Resource].LoadData(File.ReadAllText("Assets/Datas/ResourceData.csv"));
                typeof(DataTableManager).GetField("isLoaded", BindingFlags.Instance | BindingFlags.NonPublic)
                    .SetValue(dataManager, true);

                if (previousResource == null)
                {
                    resourceObject = new GameObject("ProjectileResource_QA");
                    resourceObject.SetActive(false);
                    SetSingletonInstance(resourceObject.AddComponent<ResourceManager>());
                }

                poolObject = new GameObject("ProjectilePool_QA");
                poolObject.SetActive(false);
                var pool = poolObject.AddComponent<UnitPoolManager>();
                SetSingletonInstance(pool);

                ownerObject = new GameObject("ProjectileOwner_QA");
                ownerObject.transform.position = new Vector3(-10f, 10f);
                ownerObject.AddComponent<CombatStats>();
                var owner = ownerObject.AddComponent<Monster>();

                typeof(Player).GetProperty("Instance", BindingFlags.Static | BindingFlags.Public)
                    .GetSetMethod(true).Invoke(null, new object[] { null });
                playerObject = new GameObject("ProjectileTarget_QA");
                playerObject.transform.position = new Vector3(2f, 10f);
                var playerCollider = playerObject.AddComponent<BoxCollider2D>();
                playerCollider.size = Vector2.one;
                var playerStats = playerObject.AddComponent<CombatStats>();
                playerStats.MaxHp = 100f;
                playerStats.InitStats();
                playerStats.SetDefenseBodyCollider(playerCollider);
                var player = playerObject.AddComponent<Player>();
                typeof(Player).GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic)
                    .Invoke(player, null);
                Physics2D.SyncTransforms();
                Assert.AreSame(player, Player.Instance, "Fixture must reproduce Player.Awake singleton initialization.");
                Assert.AreSame(playerStats, player.Stats, "Fixture must reproduce UnitBase combat stats binding.");
                Assert.AreEqual(new Vector3(2f, 10f), player.transform.position);
                Assert.IsTrue(playerObject.GetComponent<BoxCollider2D>().enabled);

                uint generation = GetGeneration(owner);
                var firstTask = pool.SpawnMonsterProjectileAsync(1045, owner, generation, new Vector2(0f, 10f),
                    Vector2.right, 15f, 25f, 14f).AsTask();
                await firstTask;
                Assert.IsFalse(firstTask.IsFaulted, firstTask.Exception?.ToString());
                MonsterProjectile2D first = firstTask.Result;
                Assert.NotNull(first);
                instantiated.Add(first.gameObject);
                Assert.AreEqual(10f, first.transform.position.y);
                Assert.AreEqual(15f, first.Speed, 0.01f);
                Assert.AreEqual(25f, first.MaxDistance, 0.01f);

                MethodInfo fixedUpdate = typeof(MonsterProjectile2D).GetMethod("FixedUpdate", BindingFlags.Instance | BindingFlags.NonPublic);
                for (int i = 0; i < 10 && first.gameObject.activeSelf; i++)
                {
                    fixedUpdate.Invoke(first, null);
                    Physics2D.SyncTransforms();
                }
                Assert.AreSame(player, Player.Instance);
                Assert.AreSame(playerStats, Player.Instance.Stats);
                Assert.AreEqual(new Vector3(2f, 10f), player.transform.position);
                Assert.AreEqual(86f, playerStats.CurrentHp, 0.01f, "Pattern damage 14 must be authoritative.");
                AssertQueueCount(pool, 1045, 1);

                playerStats.InitStats();
                generation = GetGeneration(owner);
                var secondTask = pool.SpawnMonsterProjectileAsync(1045, owner, generation, new Vector2(0f, 10f),
                    Vector2.right, 15f, 25f, 16f).AsTask();
                await secondTask;
                MonsterProjectile2D second = secondTask.Result;
                Assert.AreSame(first, second, "Spawn-return-spawn must reuse identity.");
                Assert.AreEqual(0f, second.TravelledDistance);
                for (int i = 0; i < 10 && second.gameObject.activeSelf; i++)
                {
                    fixedUpdate.Invoke(second, null);
                    Physics2D.SyncTransforms();
                }
                Assert.AreEqual(84f, playerStats.CurrentHp, 0.01f, "Pattern damage 16 must be authoritative.");
                AssertQueueCount(pool, 1045, 1);

                playerObject.transform.position = new Vector3(100f, 10f);
                playerStats.InitStats();
                Physics2D.SyncTransforms();
                generation = GetGeneration(owner);
                var distanceTask = pool.SpawnMonsterProjectileAsync(1045, owner, generation, new Vector2(0f, 10f),
                    Vector2.right, 15f, 25f, 16f).AsTask();
                await distanceTask;
                MonsterProjectile2D distanceProjectile = distanceTask.Result;
                Assert.AreSame(first, distanceProjectile);
                Assert.AreEqual(0f, distanceProjectile.TravelledDistance);
                for (int i = 0; i < 100 && distanceProjectile.gameObject.activeSelf; i++)
                {
                    fixedUpdate.Invoke(distanceProjectile, null);
                    Physics2D.SyncTransforms();
                }
                Assert.LessOrEqual(distanceProjectile.transform.position.x, 25f + 15f * Time.fixedDeltaTime);
                Assert.AreEqual(100f, playerStats.CurrentHp, "A max-distance return must not apply stale damage.");
                AssertQueueCount(pool, 1045, 1);

                generation = GetGeneration(owner);
                var sceneProjectileTask = pool.SpawnMonsterProjectileAsync(1045, owner, generation, Vector2.zero,
                    Vector2.right, 15f, 25f, 14f).AsTask();
                await sceneProjectileTask;
                sceneProjectileTask.Result.gameObject.SetActive(false);
                AssertQueueCount(pool, 1045, 1);

                generation = GetGeneration(owner);
                var chunkProjectileTask = pool.SpawnMonsterProjectileAsync(1045, owner, generation, Vector2.zero,
                    Vector2.right, 15f, 25f, 14f).AsTask();
                await chunkProjectileTask;
                pool.DespawnAllProjectiles();
                pool.DespawnAllProjectiles();
                AssertQueueCount(pool, 1045, 1);

                generation = GetGeneration(owner);
                var ownerProjectileTask = pool.SpawnMonsterProjectileAsync(1045, owner, generation, Vector2.zero,
                    Vector2.right, 15f, 25f, 14f).AsTask();
                await ownerProjectileTask;
                ownerObject.SetActive(false);
                AssertQueueCount(pool, 1045, 1);

                MonsterProjectile2D pooled = Dequeue(pool, 1045);
                ResourceManager.Instance.ReleaseInstance(pooled.gameObject);
                instantiated.Remove(pooled.gameObject);
                ownerObject.SetActive(true);
                generation = GetGeneration(owner);
                var lateTask = pool.SpawnMonsterProjectileAsync(1045, owner, generation, Vector2.zero,
                    Vector2.right, 15f, 25f, 14f).AsTask();
                ownerObject.SetActive(false);
                await lateTask;
                Assert.IsNull(lateTask.Result, "A late Addressables completion must not activate for a stale owner generation.");
                AssertQueueCount(pool, 1045, 1);
            }
            finally
            {
                if (UnitPoolManager.Instance != null)
                {
                    var pools = GetPools(UnitPoolManager.Instance);
                    foreach (var projectile in pools.Values.SelectMany(queue => queue).Where(item => item != null).ToArray())
                    {
                        instantiated.Remove(projectile.gameObject);
                        ResourceManager.Instance?.ReleaseInstance(projectile.gameObject);
                    }
                    pools.Clear();
                }
                foreach (GameObject instance in instantiated.Where(item => item != null))
                    ResourceManager.Instance?.ReleaseInstance(instance);
                if (playerObject != null) Object.DestroyImmediate(playerObject);
                if (ownerObject != null) Object.DestroyImmediate(ownerObject);
                if (poolObject != null) Object.DestroyImmediate(poolObject);
                if (resourceObject != null) Object.DestroyImmediate(resourceObject);
                if (dataObject != null) Object.DestroyImmediate(dataObject);
                SetSingletonInstance(previousData);
                SetSingletonInstance(previousResource);
                SetSingletonInstance(previousPool);
                typeof(Player).GetProperty("Instance", BindingFlags.Static | BindingFlags.Public)
                    .GetSetMethod(true).Invoke(null, new object[] { previousPlayer });
            }
        }

        private static uint GetGeneration(Monster owner) => (uint)typeof(Monster)
            .GetField("actionGeneration", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(owner);

        private static Dictionary<uint, Queue<MonsterProjectile2D>> GetPools(UnitPoolManager pool) =>
            (Dictionary<uint, Queue<MonsterProjectile2D>>)typeof(UnitPoolManager)
                .GetField("projectilePools", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(pool);

        private static void AssertQueueCount(UnitPoolManager pool, uint idx, int expected)
        {
            Assert.IsTrue(GetPools(pool).TryGetValue(idx, out var queue));
            Assert.AreEqual(expected, queue.Count, "Projectile pool contains a duplicate or missing return.");
        }

        private static MonsterProjectile2D Dequeue(UnitPoolManager pool, uint idx) => GetPools(pool)[idx].Dequeue();

        private static void SetSingletonInstance<T>(T component) where T : MonoBehaviour =>
            typeof(Singleton<T>).GetField("<Instance>k__BackingField", BindingFlags.Static | BindingFlags.NonPublic)
                .SetValue(null, component);
    }
}
