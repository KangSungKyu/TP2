using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using Gameplay.Combat;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace QA.Tests
{
    public class UnitProjectileIntegrationTests
    {
        [Test]
        public void NativeSweep_SelectsOnlyContactedHostileUnitsWithoutGlobalUnitScan()
        {
            Player previousPlayer = Player.Instance;
            var objects = new List<GameObject>();
            try
            {
                typeof(Player).GetProperty("Instance", BindingFlags.Static | BindingFlags.Public)
                    .GetSetMethod(true).Invoke(null, new object[] { null });
                Player owner = CreateUnit<Player>("SweepOwner_QA", Vector2.zero, objects);
                Monster first = CreateUnit<Monster>("SweepFirst_QA", new Vector2(1f, 0f), objects);
                Monster second = CreateUnit<Monster>("SweepSecond_QA", new Vector2(2f, 0f), objects);
                Monster outside = CreateUnit<Monster>("SweepOutside_QA", new Vector2(20f, 0f), objects);
                Physics2D.SyncTransforms();

                var sweep = new CombatStats.AttackSweep2D(new Vector2(-1f, 0f), new Vector2(3f, 0f),
                    Vector2.one * .5f, owner.GetInstanceID(), owner.ActionGeneration, 0u,
                    hasExteriorPose: true);
                var victims = new List<UnitBase>();
                var fractions = new List<float>();
                Assert.AreEqual(2, CombatStats.CollectAttackSweepVictims(owner, sweep, victims, fractions));
                CollectionAssert.AreEquivalent(new UnitBase[] { first, second }, victims);
                CollectionAssert.DoesNotContain(victims, outside);
                CollectionAssert.DoesNotContain(victims, owner);
                Assert.LessOrEqual(fractions[0], fractions[1]);

                var stale = new CombatStats.AttackSweep2D(sweep.Previous, sweep.Current, sweep.HalfExtents,
                    sweep.SourceId, owner.ActionGeneration + 1u, sweep.Tick, hasExteriorPose: true);
                Assert.Zero(CombatStats.CollectAttackSweepVictims(owner, stale, victims, fractions));
            }
            finally
            {
                foreach (GameObject item in objects) if (item != null) Object.DestroyImmediate(item);
                typeof(Player).GetProperty("Instance", BindingFlags.Static | BindingFlags.Public)
                    .GetSetMethod(true).Invoke(null, new object[] { previousPlayer });
            }
        }

        [TestCase(14f, 7f)]
        [TestCase(16f, 8f)]
        public void ReflectableProjectile_FrontParryReflectsOnceWithoutPosture(float damage, float reflectedDamage)
        {
            Player previousPlayer = Player.Instance;
            GameObject ownerObject = null, defenderObject = null, projectileObject = null;
            try
            {
                ownerObject = new GameObject("ReflectionOwner_QA");
                ownerObject.transform.position = new Vector3(-10f, 0f);
                var ownerBody = ownerObject.AddComponent<BoxCollider2D>();
                var ownerStats = ownerObject.AddComponent<CombatStats>();
                var owner = ownerObject.AddComponent<Monster>();
                typeof(Monster).GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic)
                    .Invoke(owner, null);
                ownerStats.MaxHp = 100f;
                ownerStats.InitStats();
                ownerStats.SetDefenseBodyCollider(ownerBody);

                typeof(Player).GetProperty("Instance", BindingFlags.Static | BindingFlags.Public)
                    .GetSetMethod(true).Invoke(null, new object[] { null });
                defenderObject = new GameObject("ReflectionDefender_QA");
                var defenderBody = defenderObject.AddComponent<BoxCollider2D>();
                var defenderStats = defenderObject.AddComponent<CombatStats>();
                var defender = defenderObject.AddComponent<Player>();
                typeof(Player).GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic)
                    .Invoke(defender, null);
                defenderStats.InitStats();
                defenderStats.SetDefenseBodyCollider(defenderBody);
                defender.SetFacingRight(false);
                defender.SetParrying(true);

                GameObject projectilePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                    "Assets/Prefabs/Projectiles/Projectile_1045.prefab");
                Assert.NotNull(projectilePrefab);
                Assert.IsTrue(projectilePrefab.GetComponent<UnitProjectile2D>().IsReflectable,
                    "Projectile_1045 prefab must own reflectable=true.");
                projectileObject = Object.Instantiate(projectilePrefab);
                var projectile = projectileObject.GetComponent<UnitProjectile2D>();
                typeof(UnitProjectile2D).GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic)
                    .Invoke(projectile, null);
                projectile.Activate(1045u, owner, owner.ActionGeneration, new Vector2(-2f, 0f),
                    Vector2.right, 150f, 25f, damage);
                Physics2D.SyncTransforms();

                var expectedSweep = new CombatStats.AttackSweep2D(new Vector2(-2f, 0f),
                    new Vector2(1f, 0f), Vector2.one * .1f, projectile.GetInstanceID(),
                    owner.ActionGeneration, 0u, hasExteriorPose: true);
                Assert.IsTrue(defenderStats.TryGetAttackSweepFraction(expectedSweep, out _),
                    "Fixture sweep must reach the active front defense volume.");
                Assert.AreSame(ownerBody, owner.Stats.DefenseBodyCollider, "Owner defense body must be authoritative.");
                Assert.AreSame(defenderBody, defender.Stats.DefenseBodyCollider, "Defender defense body must be authoritative.");
                Assert.IsTrue(ownerBody.isActiveAndEnabled && defenderBody.isActiveAndEnabled,
                    $"Fixture bodies must be active: owner={ownerBody.isActiveAndEnabled}, defender={defenderBody.isActiveAndEnabled}.");
                Assert.AreEqual(FactionType.Enemy, owner.Faction, "Owner faction must be hostile.");
                Assert.AreEqual(FactionType.PlayerAlly, defender.Faction, "Defender faction must be hostile.");
                object[] reflectionArgs = { defender, Vector2.zero };
                Assert.IsTrue((bool)typeof(UnitProjectile2D).GetMethod("TryGetReflectionDirection",
                    BindingFlags.Instance | BindingFlags.NonPublic).Invoke(projectile, reflectionArgs),
                    "Fixture must provide valid owner/defender body-center reflection geometry.");

                MethodInfo fixedUpdate = typeof(UnitProjectile2D).GetMethod("FixedUpdate",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                fixedUpdate.Invoke(projectile, null);
                Physics2D.SyncTransforms();

                Assert.IsTrue(projectile.IsReflected, "Front parry must keep and redirect a reflectable projectile.");
                Assert.AreSame(defender, projectile.Owner);
                Assert.AreEqual(0f, ownerStats.CurrentPosture);
                Assert.AreEqual(0f, defenderStats.CurrentPosture);
                Assert.IsTrue(projectile.gameObject.activeSelf, "Successful reflection must not return to pool.");

                float reflectedAtX = projectile.transform.position.x;
                fixedUpdate.Invoke(projectile, null);
                Physics2D.SyncTransforms();
                Assert.IsTrue(projectile.gameObject.activeSelf, "Reflected projectile must survive the next FixedStep.");
                Assert.Less(projectile.transform.position.x, reflectedAtX,
                    "Reflected velocity must point toward the original owner snapshot.");

                defender.SetParrying(false);
                for (int i = 0; i < 20 && projectile.gameObject.activeSelf; i++)
                {
                    fixedUpdate.Invoke(projectile, null);
                    Physics2D.SyncTransforms();
                }
                Assert.AreEqual(100f - reflectedDamage, ownerStats.CurrentHp, .01f);
                Assert.IsFalse(projectile.IsReflected, "Pool return must reset reflected state.");
                Assert.IsTrue(projectile.IsReflectable, "Pool return must preserve prefab-owned reflectable.");
            }
            finally
            {
                if (projectileObject != null) Object.DestroyImmediate(projectileObject);
                if (defenderObject != null) Object.DestroyImmediate(defenderObject);
                if (ownerObject != null) Object.DestroyImmediate(ownerObject);
                typeof(Player).GetProperty("Instance", BindingFlags.Static | BindingFlags.Public)
                    .GetSetMethod(true).Invoke(null, new object[] { previousPlayer });
            }
        }

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
        public void ProjectileReflectable_DefaultFalseAndPrefab1045True()
        {
            var temporary = new GameObject("DefaultProjectile_QA");
            try
            {
                temporary.AddComponent<BoxCollider2D>();
                Assert.IsFalse(temporary.AddComponent<UnitProjectile2D>().IsReflectable);
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                    "Assets/Prefabs/Projectiles/Projectile_1045.prefab");
                Assert.NotNull(prefab);
                Assert.IsTrue(prefab.GetComponent<UnitProjectile2D>().IsReflectable);
            }
            finally
            {
                Object.DestroyImmediate(temporary);
            }
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
                tables[DataTableType.UnitBase] = new UnitBaseDataTable();
                tables[DataTableType.UnitBase].LoadData(File.ReadAllText("Assets/Datas/UnitBaseData.csv"));
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
                var firstTask = pool.SpawnUnitProjectileAsync(1045, owner, generation, new Vector2(0f, 10f),
                    Vector2.right, 15f, 25f, 14f).AsTask();
                await firstTask;
                Assert.IsFalse(firstTask.IsFaulted, firstTask.Exception?.ToString());
                UnitProjectile2D first = firstTask.Result;
                Assert.NotNull(first);
                instantiated.Add(first.gameObject);
                typeof(UnitProjectile2D).GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic)
                    .Invoke(first, null);
                Assert.AreEqual(10f, first.transform.position.y);
                Assert.AreEqual(15f, first.Speed, 0.01f);
                Assert.AreEqual(25f, first.MaxDistance, 0.01f);
                MethodInfo fixedUpdate = typeof(UnitProjectile2D).GetMethod("FixedUpdate", BindingFlags.Instance | BindingFlags.NonPublic);
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
                var secondTask = pool.SpawnUnitProjectileAsync(1045, owner, generation, new Vector2(0f, 10f),
                    Vector2.right, 15f, 25f, 16f).AsTask();
                await secondTask;
                UnitProjectile2D second = secondTask.Result;
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
                var distanceTask = pool.SpawnUnitProjectileAsync(1045, owner, generation, new Vector2(0f, 10f),
                    Vector2.right, 15f, 25f, 16f).AsTask();
                await distanceTask;
                UnitProjectile2D distanceProjectile = distanceTask.Result;
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
                var sceneProjectileTask = pool.SpawnUnitProjectileAsync(1045, owner, generation, Vector2.zero,
                    Vector2.right, 15f, 25f, 14f).AsTask();
                await sceneProjectileTask;
                sceneProjectileTask.Result.gameObject.SetActive(false);
                typeof(UnitProjectile2D).GetMethod("OnDisable", BindingFlags.Instance | BindingFlags.NonPublic)
                    .Invoke(sceneProjectileTask.Result, null);
                AssertQueueCount(pool, 1045, 1);

                generation = GetGeneration(owner);
                var chunkProjectileTask = pool.SpawnUnitProjectileAsync(1045, owner, generation, Vector2.zero,
                    Vector2.right, 15f, 25f, 14f).AsTask();
                await chunkProjectileTask;
                pool.DespawnAllProjectiles();
                pool.DespawnAllProjectiles();
                AssertQueueCount(pool, 1045, 1);

                generation = GetGeneration(owner);
                var ownerProjectileTask = pool.SpawnUnitProjectileAsync(1045, owner, generation, Vector2.zero,
                    Vector2.right, 15f, 25f, 14f).AsTask();
                await ownerProjectileTask;
                ownerObject.SetActive(false);
                typeof(Monster).GetMethod("OnDisable", BindingFlags.Instance | BindingFlags.NonPublic)
                    .Invoke(owner, null);
                AssertQueueCount(pool, 1045, 1);

                UnitProjectile2D pooled = Dequeue(pool, 1045);
                ResourceManager.Instance.ReleaseInstance(pooled.gameObject);
                instantiated.Remove(pooled.gameObject);
                ownerObject.SetActive(true);
                generation = GetGeneration(owner);
                ownerObject.SetActive(false);
                typeof(Monster).GetMethod("OnDisable", BindingFlags.Instance | BindingFlags.NonPublic)
                    .Invoke(owner, null);
                var lateTask = pool.SpawnUnitProjectileAsync(1045, owner, generation, Vector2.zero,
                    Vector2.right, 15f, 25f, 14f).AsTask();
                await lateTask;
                Assert.IsNull(lateTask.Result, "Addressables completion must not activate for a stale owner generation.");
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

        private static Dictionary<uint, Queue<UnitProjectile2D>> GetPools(UnitPoolManager pool) =>
            (Dictionary<uint, Queue<UnitProjectile2D>>)typeof(UnitPoolManager)
                .GetField("projectilePools", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(pool);

        private static void AssertQueueCount(UnitPoolManager pool, uint idx, int expected)
        {
            Assert.IsTrue(GetPools(pool).TryGetValue(idx, out var queue));
            Assert.AreEqual(expected, queue.Count, "Projectile pool contains a duplicate or missing return.");
        }

        private static UnitProjectile2D Dequeue(UnitPoolManager pool, uint idx) => GetPools(pool)[idx].Dequeue();

        private static T CreateUnit<T>(string name, Vector2 position, List<GameObject> objects)
            where T : UnitBase
        {
            var item = new GameObject(name);
            objects.Add(item);
            item.transform.position = position;
            var body = item.AddComponent<BoxCollider2D>();
            body.size = Vector2.one;
            var stats = item.AddComponent<CombatStats>();
            T unit = item.AddComponent<T>();
            typeof(T).GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(unit, null);
            stats.InitStats();
            stats.SetDefenseBodyCollider(body);
            return unit;
        }

        private static void SetSingletonInstance<T>(T component) where T : MonoBehaviour =>
            typeof(Singleton<T>).GetField("<Instance>k__BackingField", BindingFlags.Static | BindingFlags.NonPublic)
                .SetValue(null, component);
    }
}
