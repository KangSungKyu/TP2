using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using UnityEditor;
using UnityEngine.TestTools;
using UnityEngine;

namespace QA.Tests
{
    public class PrefabNamingMigrationTests
    {
        [Test]
        public void OwnedPrefabs_HaveIdxNamesAndUniqueAddressableEntries()
        {
            var expected = new Dictionary<uint, string>
            {
                { 1001, "Unit_3001" }, { 1002, "Unit_3201" }, { 1003, "Unit_3101" },
                { 1004, "Unit_3102" }, { 1005, "Unit_3103" }, { 1006, "Unit_3104" }, { 1007, "Unit_3105" },
                { 1020, "Effect_8001" }, { 1021, "Effect_8002" }, { 1022, "Effect_8003" },
                { 1030, "Effect_8010" }, { 1031, "Effect_8011" }, { 1032, "Effect_8012" }, { 1033, "Effect_8013" },
                { 1040, "Prefab_1040" }, { 1041, "Prefab_1041" }, { 1042, "Prefab_1042" },
                { 1050, "Room_11050" }, { 1051, "Room_11051" }, { 1052, "Room_11052" }, { 1053, "Room_11053" },
                { 1056, "Room_11056" }, { 1057, "Room_11057" }, { 1061, "Room_11061" }, { 1063, "Room_11063" }
            };
            var paths = File.ReadAllLines("Assets/Datas/ResourceData.csv").Skip(1)
                .Select(line => line.Split(',')).ToDictionary(columns => uint.Parse(columns[0]), columns => columns[1]);
            string group = File.ReadAllText("Assets/AddressableAssetsData/AssetGroups/Prefabs.asset");

            foreach (var pair in expected)
            {
                Assert.AreEqual(pair.Value, paths[pair.Key]);
                string guid = AssetDatabase.FindAssets($"{pair.Value} t:Prefab", new[] { "Assets/Prefabs" })
                    .Single(candidate => Path.GetFileNameWithoutExtension(AssetDatabase.GUIDToAssetPath(candidate)) == pair.Value);
                string assetPath = AssetDatabase.GUIDToAssetPath(guid);
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
                Assert.AreEqual(pair.Value, prefab.name);
                StringAssert.Contains($"m_GUID: {guid}", group);
                StringAssert.Contains($"m_Address: {pair.Value}", group);
            }

            string[] addresses = Regex.Matches(group, @"m_Address: (.+)")
                .Cast<Match>().Select(match => match.Groups[1].Value.Trim()).ToArray();
            Assert.AreEqual(addresses.Length, addresses.Distinct().Count(), "Duplicate prefab Addressable address found.");
        }

        [UnityTest]
        public IEnumerator UnitPrefabFk_InstantiatesThroughResourceManager()
        {
            var resourceTable = new ResourceDataTable();
            resourceTable.LoadData(File.ReadAllText("Assets/Datas/ResourceData.csv"));
            var managerObject = new GameObject("ResourceManager_PrefabNaming_QA");
            var manager = managerObject.AddComponent<ResourceManager>();
            foreach (uint resourceIdx in new[] { 1001u, 1003u, 1006u })
            {
                string key = resourceTable.GetResourcePath(resourceIdx);
                var task = manager.InstantiateAsyncTask(key);
                while (!task.IsCompleted) yield return null;
                Assert.IsFalse(task.IsFaulted, task.Exception?.ToString());
                Assert.NotNull(task.Result);
                manager.ReleaseInstance(task.Result);
            }
            Object.DestroyImmediate(managerObject);
        }

        [Test]
        public void PlayerPoolRouting_UsesUnitAndResourceForeignKeys()
        {
            var units = new UnitBaseDataTable();
            units.LoadData(File.ReadAllText("Assets/Datas/UnitBaseData.csv"));
            var resources = new ResourceDataTable();
            resources.LoadData(File.ReadAllText("Assets/Datas/ResourceData.csv"));

            Assert.IsTrue(units.TryGetUnitData(3001, out UnitBaseData player));
            Assert.AreEqual(1001u, player.PrefabId);
            Assert.IsTrue(resources.TryGetResource(player.PrefabId, out ResourceData prefab));
            Assert.AreEqual("Unit_3001", prefab.Path);
            Assert.IsFalse(units.TryGetUnitData(0, out _));
            Assert.IsFalse(resources.TryGetResource(uint.MaxValue, out _));

            string source = File.ReadAllText("Assets/Scripts/Manager/UnitPoolManager.cs");
            StringAssert.DoesNotContain("InstantiateAsyncTask(\"Player\"", source);
            StringAssert.DoesNotContain("ReturnToPool(\"Player\"", source);
            StringAssert.Contains("TryResolveUnitPrefabKey(PlayerUnitIdx, out string poolKey)", source);
            StringAssert.DoesNotContain("catch { }", source);

            int reuse = source.IndexOf("if (Player.Instance != null)");
            int resolve = source.IndexOf("TryResolveUnitPrefabKey(PlayerUnitIdx", reuse);
            int instantiate = source.IndexOf("InstantiateAsyncTask(poolKey", resolve);
            Assert.Greater(resolve, reuse);
            Assert.Greater(instantiate, resolve,
                "Existing Player.Instance must return before resource resolution and instantiate.");
        }

        [UnityTest]
        public IEnumerator PlayerPool_DespawnAndRespawnReuseSameIdentity()
        {
            UnitPoolManager previousPool = UnitPoolManager.Instance;
            Player previousPlayer = Player.Instance;
            SetSingletonInstance<UnitPoolManager>(null);
            typeof(Player).GetProperty("Instance", BindingFlags.Static | BindingFlags.Public)
                .GetSetMethod(true).Invoke(null, new object[] { null });
            GameObject resourceManagerObject = null;
            if (ResourceManager.Instance == null)
            {
                resourceManagerObject = new GameObject("ResourceManager_PlayerPool_QA");
                SetSingletonInstance(resourceManagerObject.AddComponent<ResourceManager>());
            }
            GameObject dataManagerObject = null;
            if (DataTableManager.Instance == null)
            {
                dataManagerObject = new GameObject("DataTableManager_PlayerPool_QA");
                SetSingletonInstance(dataManagerObject.AddComponent<DataTableManager>());
            }
            var dataManager = DataTableManager.Instance;
            InstallUnitResourceTables(dataManager);
            dataManager.GetDB<UnitBaseDataTable>(DataTableType.UnitBase)
                .LoadData(File.ReadAllText("Assets/Datas/UnitBaseData.csv"));
            dataManager.GetDB<ResourceDataTable>(DataTableType.Resource)
                .LoadData(File.ReadAllText("Assets/Datas/ResourceData.csv"));
            GameObject poolObject = new GameObject("UnitPoolManager_PlayerPool_QA");
            SetSingletonInstance(poolObject.AddComponent<UnitPoolManager>());
            var pool = UnitPoolManager.Instance;

            var firstTask = pool.SpawnPlayerAsync(Vector3.zero).AsTask();
            while (!firstTask.IsCompleted) yield return null;
            Assert.IsFalse(firstTask.IsFaulted, firstTask.Exception?.ToString());
            Player first = firstTask.Result;
            Assert.NotNull(first);

            pool.DespawnUnit(first);
            var secondTask = pool.SpawnPlayerAsync(Vector3.one).AsTask();
            while (!secondTask.IsCompleted) yield return null;
            Assert.AreSame(first.gameObject, secondTask.Result.gameObject);

            Object.DestroyImmediate(first.gameObject);
            Object.DestroyImmediate(poolObject);
            SetSingletonInstance(previousPool);
            typeof(Player).GetProperty("Instance", BindingFlags.Static | BindingFlags.Public)
                .GetSetMethod(true).Invoke(null, new object[] { previousPlayer });
            if (dataManagerObject != null) Object.DestroyImmediate(dataManagerObject);
            if (resourceManagerObject != null) Object.DestroyImmediate(resourceManagerObject);
        }

        [UnityTest]
        public IEnumerator MonsterDeath_FadesInPlaceDespawnsOnceAndResetsOnReuse()
        {
            GameObject dataManagerObject = null;
            if (DataTableManager.Instance == null)
            {
                dataManagerObject = new GameObject("DataTableManager_MonsterDeath_QA");
                SetSingletonInstance(dataManagerObject.AddComponent<DataTableManager>());
            }
            InstallUnitResourceTables(DataTableManager.Instance);
            DataTableManager.Instance.GetDB<UnitBaseDataTable>(DataTableType.UnitBase)
                .LoadData(File.ReadAllText("Assets/Datas/UnitBaseData.csv"));
            DataTableManager.Instance.GetDB<ResourceDataTable>(DataTableType.Resource)
                .LoadData(File.ReadAllText("Assets/Datas/ResourceData.csv"));

            GameObject poolObject = null;
            if (UnitPoolManager.Instance == null)
            {
                poolObject = new GameObject("UnitPoolManager_MonsterDeath_QA");
                SetSingletonInstance(poolObject.AddComponent<UnitPoolManager>());
            }

            var monsterObject = new GameObject("Monster_DeathPool_QA");
            monsterObject.AddComponent<Rigidbody2D>();
            var collider = monsterObject.AddComponent<BoxCollider2D>();
            var motor = monsterObject.AddComponent<KinematicMotor2D>();
            monsterObject.AddComponent<CombatStats>();
            var monster = monsterObject.AddComponent<Monster>();
            typeof(Monster).GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(monster, null);
            motor.InitMotor();
            typeof(UnitBase).GetField("<UnitIdx>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(monster, 3101u);
            var renderer = (SpriteRenderer)typeof(UnitBase)
                .GetField("spriteRenderer", BindingFlags.Instance | BindingFlags.NonPublic)
                .GetValue(monster);

            Vector3 deathPosition = new Vector3(3f, 2f);
            monsterObject.transform.position = deathPosition;
            motor.Teleport(deathPosition);
            monster.Die();
            monster.Die();

            float timeout = Time.realtimeSinceStartup + 3f;
            while (monsterObject.activeSelf && Time.realtimeSinceStartup < timeout)
            {
                Assert.AreEqual(deathPosition, monsterObject.transform.position);
                yield return null;
            }

            Assert.IsFalse(monsterObject.activeSelf);
            var pools = (Dictionary<string, Queue<GameObject>>)typeof(UnitPoolManager)
                .GetField("poolDictionary", BindingFlags.Instance | BindingFlags.NonPublic)
                .GetValue(UnitPoolManager.Instance);
            Assert.AreEqual(1, pools["Unit_3101"].Count);

            var getFromPool = typeof(UnitPoolManager)
                .GetMethod("GetFromPool", BindingFlags.Instance | BindingFlags.NonPublic);
            var reused = (GameObject)getFromPool.Invoke(UnitPoolManager.Instance, new object[] { "Unit_3101" });
            Assert.AreSame(monsterObject, reused);
            monster.ResetAfterDeath(Vector3.one);
            reused.SetActive(true);
            Assert.AreEqual(1f, renderer.color.a);
            Assert.IsTrue(motor.enabled);
            Assert.IsTrue(collider.enabled);
            StringAssert.Contains("monsterComp.ResetAfterDeath(position);",
                File.ReadAllText("Assets/Scripts/Manager/UnitPoolManager.cs"));

            Object.DestroyImmediate(monsterObject);
            if (poolObject != null) Object.DestroyImmediate(poolObject);
            if (dataManagerObject != null) Object.DestroyImmediate(dataManagerObject);
        }

        [Test]
        public void PlayerPool_InvalidMappingsReturnFalseWithDistinctErrors()
        {
            GameObject resourceManagerObject = null;
            if (ResourceManager.Instance == null)
            {
                resourceManagerObject = new GameObject("ResourceManager_PlayerErrors_QA");
                SetSingletonInstance(resourceManagerObject.AddComponent<ResourceManager>());
            }
            GameObject dataManagerObject = null;
            if (DataTableManager.Instance == null)
            {
                dataManagerObject = new GameObject("DataTableManager_PlayerErrors_QA");
                SetSingletonInstance(dataManagerObject.AddComponent<DataTableManager>());
            }
            GameObject poolObject = null;
            if (UnitPoolManager.Instance == null)
            {
                poolObject = new GameObject("UnitPoolManager_PlayerErrors_QA");
                SetSingletonInstance(poolObject.AddComponent<UnitPoolManager>());
            }
            var dataManager = DataTableManager.Instance;
            var pool = UnitPoolManager.Instance;
            var tableField = typeof(DataTableManager).GetField("dataList", BindingFlags.Instance | BindingFlags.NonPublic);
            var tables = (Dictionary<DataTableType, IDataLoad>)tableField.GetValue(dataManager);
            bool hadUnits = tables.TryGetValue(DataTableType.UnitBase, out IDataLoad previousUnits);
            bool hadResources = tables.TryGetValue(DataTableType.Resource, out IDataLoad previousResources);
            try
            {
                InstallUnitResourceTables(dataManager);
                var units = dataManager.GetDB<UnitBaseDataTable>(DataTableType.UnitBase);
                var resources = dataManager.GetDB<ResourceDataTable>(DataTableType.Resource);
                MethodInfo resolve = typeof(UnitPoolManager).GetMethod(
                    "TryResolveUnitPrefabKey", BindingFlags.Instance | BindingFlags.NonPublic);

                units.Release();
                LogAssert.Expect(LogType.Error, "[UnitPoolManager] Missing UnitBaseData for unit idx 3001.");
                Assert.IsFalse((bool)resolve.Invoke(pool, new object[] { 3001u, null }));

                units.LoadData(File.ReadAllText("Assets/Datas/UnitBaseData.csv"));
                resources.Release();
                LogAssert.Expect(LogType.Error, "[UnitPoolManager] Missing ResourceData idx 1001 for unit idx 3001.");
                Assert.IsFalse((bool)resolve.Invoke(pool, new object[] { 3001u, null }));

                resources.LoadData("idx,path\n1001,");
                LogAssert.Expect(LogType.Error, "[UnitPoolManager] Empty ResourceData path at idx 1001 for unit idx 3001.");
                Assert.IsFalse((bool)resolve.Invoke(pool, new object[] { 3001u, null }));
            }
            finally
            {
                if (hadUnits) tables[DataTableType.UnitBase] = previousUnits;
                else tables.Remove(DataTableType.UnitBase);
                if (hadResources) tables[DataTableType.Resource] = previousResources;
                else tables.Remove(DataTableType.Resource);
                if (poolObject != null) Object.DestroyImmediate(poolObject);
                if (dataManagerObject != null) Object.DestroyImmediate(dataManagerObject);
                if (resourceManagerObject != null) Object.DestroyImmediate(resourceManagerObject);
            }
        }

        private static void InstallUnitResourceTables(DataTableManager dataManager)
        {
            var field = typeof(DataTableManager).GetField("dataList", BindingFlags.Instance | BindingFlags.NonPublic);
            var tables = (Dictionary<DataTableType, IDataLoad>)field.GetValue(dataManager);
            tables[DataTableType.UnitBase] = new UnitBaseDataTable();
            tables[DataTableType.Resource] = new ResourceDataTable();
        }

        private static void SetSingletonInstance<T>(T component) where T : MonoBehaviour
        {
            typeof(Singleton<T>).GetField("<Instance>k__BackingField", BindingFlags.Static | BindingFlags.NonPublic)
                .SetValue(null, component);
        }
    }
}
