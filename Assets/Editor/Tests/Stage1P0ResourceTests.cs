using System.IO;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace QA.Tests
{
    public class Stage1P0ResourceTests
    {
        [Test]
        public void P0Resources_AreIntegerLinkedAndPrefabComplete()
        {
            uint[] ids = { 1050, 1051, 1052, 1053, 1056, 1057, 1061, 1063 };
            string resources = File.ReadAllText("Assets/Datas/ResourceData.csv");
            foreach (uint idx in ids)
            {
                StringAssert.Contains($"{idx},Tilemap_Room_Stage1_{idx}", resources);
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/Prefabs/Rooms/Tilemap_Room_Stage1_{idx}.prefab");
                Assert.NotNull(prefab, $"Missing chunk prefab {idx}");
                Assert.AreEqual(4, prefab.GetComponentsInChildren<ChunkSocketMarker>(true).Length);
                Assert.LessOrEqual(prefab.GetComponentsInChildren<SpawnPointMarker>(true).Length - 1, 6);
                Assert.NotNull(prefab.transform.Find("CameraBounds"));
            }

            var unitTable = new UnitBaseDataTable();
            unitTable.LoadData(File.ReadAllText("Assets/Datas/UnitBaseData.csv"));
            Assert.IsTrue(unitTable.TryGetUnitData(3104, out var unit3104));
            Assert.IsTrue(unitTable.TryGetUnitData(3105, out var unit3105));
            Assert.AreEqual(1006u, unit3104.PrefabId);
            Assert.AreEqual(1012u, unit3104.AnimatorId);
            Assert.AreEqual(1007u, unit3105.PrefabId);
            Assert.AreEqual(1013u, unit3105.AnimatorId);

            Assert.NotNull(AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>("Assets/Anims/Monster/SpearSentryAnimatorController.controller"));
            Assert.NotNull(AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>("Assets/Anims/Monster/ShadowStalkerAnimatorController.controller"));
        }
    }
}
