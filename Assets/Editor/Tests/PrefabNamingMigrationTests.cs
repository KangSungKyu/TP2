using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
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
        }
    }
}
