using System.IO;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace QA.Tests
{
    public class Stage1P0ResourceTests
    {
        [Test]
        public void StageRunCsvFiles_UseDedicatedTypesAndParse()
        {
            var chunks = new ChunkResourceDataTable();
            var layout = new StageLayoutDataTable();
            var encounters = new MonsterEncounterDataTable();
            chunks.LoadData(File.ReadAllText("Assets/Datas/ChunkResourceData.csv"));
            layout.LoadData(File.ReadAllText("Assets/Datas/StageLayoutData.csv"));
            encounters.LoadData(File.ReadAllText("Assets/Datas/MonsterEncounterData.csv"));

            Assert.AreEqual(8, chunks.GetDataCount());
            Assert.AreEqual(1, layout.GetDataCount());
            Assert.AreEqual(4, encounters.GetDataCount());
            Assert.AreEqual(DataTableType.ChunkResource, Util.GetDataTableType(11050));
            Assert.AreEqual(DataTableType.StageLayout, Util.GetDataTableType(12001));
            Assert.AreEqual(DataTableType.MonsterEncounter, Util.GetDataTableType(13001));
        }

        [Test]
        public void DisplayTextCsvs_ParseAndBossPatternIsAbsent()
        {
            var effects = new EffectDataTable();
            var skills = new SkillDataTable();
            var texts = new TextDataTable();
            effects.LoadData(File.ReadAllText("Assets/Datas/EffectData.csv"));
            skills.LoadData(File.ReadAllText("Assets/Datas/SkillData.csv"));
            texts.LoadData(File.ReadAllText("Assets/Datas/TextData.csv"));

            uint[] effectIds = { 8001, 8002, 8003, 8010, 8011, 8012, 8013 };
            for (int i = 0; i < effectIds.Length; i++)
            {
                Assert.IsTrue(effects.TryGetEffectData(effectIds[i], out var effect));
                Assert.AreEqual((uint)(2020 + i), effect.EffectNameTextIdx);
                Assert.IsNotEmpty(texts.GetText(effect.EffectNameTextIdx));
            }

            uint[] skillIds = { 7001, 7002, 7003, 7004, 7010, 7011, 7012, 7013 };
            for (int i = 0; i < skillIds.Length; i++)
            {
                Assert.IsTrue(skills.TryGetSkillData(skillIds[i], out var skill));
                Assert.AreEqual((uint)(2030 + i), skill.NameTextIdx);
                Assert.IsNotEmpty(texts.GetText(skill.NameTextIdx));
            }

            Assert.IsFalse(File.Exists("Assets/Datas/BossPatternData.csv"));
            StringAssert.DoesNotContain("b25a4000b4f874042afc7cb1962d1054",
                File.ReadAllText("Assets/AddressableAssetsData/AssetGroups/Datas.asset"));
        }

        [Test]
        public void P0Resources_AreIntegerLinkedAndPrefabComplete()
        {
            uint[] ids = { 1050, 1051, 1052, 1053, 1056, 1057, 1061, 1063 };
            string resources = File.ReadAllText("Assets/Datas/ResourceData.csv");
            foreach (uint idx in ids)
            {
                uint chunkIdx = 10000u + idx;
                StringAssert.Contains($"{idx},Room_{chunkIdx}", resources);
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/Prefabs/Rooms/Room_{chunkIdx}.prefab");
                Assert.NotNull(prefab, $"Missing chunk prefab {idx}");
                ChunkSocketMarker[] sockets = prefab.GetComponentsInChildren<ChunkSocketMarker>(true);
                Assert.AreEqual(4, sockets.Length);
                CollectionAssert.AreEquivalent(
                    new[] { ChunkSocketDirection.North, ChunkSocketDirection.East, ChunkSocketDirection.South, ChunkSocketDirection.West },
                    System.Array.ConvertAll(sockets, socket => socket.Direction));
                Assert.IsFalse(System.Array.Exists(sockets, socket => socket.EntryMarker == null));
                Assert.LessOrEqual(prefab.GetComponentsInChildren<SpawnPointMarker>(true).Length - 1, 6);
                Assert.NotNull(prefab.transform.Find("CameraBounds"));
            }

            var unitTable = new UnitBaseDataTable();
            unitTable.LoadData(File.ReadAllText("Assets/Datas/UnitBaseData.csv"));
            Assert.IsTrue(unitTable.TryGetUnitData(3104, out var unit3104));
            Assert.IsTrue(unitTable.TryGetUnitData(3105, out var unit3105));
            Assert.AreEqual(1006u, unit3104.PrefabId);
            Assert.AreEqual(1015u, unit3104.AnimatorId);
            Assert.AreEqual(1007u, unit3105.PrefabId);
            Assert.AreEqual(1016u, unit3105.AnimatorId);

            Assert.NotNull(AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>("Assets/Anims/Monster/ShieldSentinelAnimatorController.controller"));
            Assert.NotNull(AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>("Assets/Anims/Monster/OrbitalMarksmanAnimatorController.controller"));
        }

        [Test]
        public void NewMonsters_HaveCompleteUniqueImportedAssets()
        {
            AssertMonster(3104, "ShieldSentinel", "Idle", "Move", "Hit", "Death", "Attack6003", "Attack6004");
            AssertLegacyMonster(3105, "OrbitalMarksman", "Attack6005", "Attack6006");

            Assert.AreNotEqual(
                AssetDatabase.AssetPathToGUID("Assets/Prefabs/Unit_3104.prefab"),
                AssetDatabase.AssetPathToGUID("Assets/Prefabs/Unit_3105.prefab"));
        }

        [Test]
        public void NormalizedUnitPrefabs_HaveSingleVisualRendererAndIdxPaths()
        {
            StringAssert.Contains("1003,Unit_3101", File.ReadAllText("Assets/Datas/ResourceData.csv"));
            StringAssert.Contains("1006,Unit_3104", File.ReadAllText("Assets/Datas/ResourceData.csv"));
            AssertMonster(3101, "SpearSentry", "Idle", "Move", "Attack", "Death");
            AssertMonster(3104, "ShieldSentinel", "Idle", "Move", "Hit", "Death", "Attack6003", "Attack6004");
        }

        private static void AssertMonster(uint unitIdx, string name, params string[] actions)
        {
            string prefabPath = $"Assets/Prefabs/Unit_{unitIdx}.prefab";
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            Assert.NotNull(prefab);
            Assert.AreEqual($"Unit_{unitIdx}", prefab.name);
            Assert.IsNull(prefab.GetComponent<SpriteRenderer>());
            Assert.IsNull(prefab.GetComponent<Animator>());
            AssertMonsterVisual(prefab, name, actions);
        }

        private static void AssertLegacyMonster(uint unitIdx, string name, string attackA, string attackB)
        {
            string prefabPath = $"Assets/Prefabs/Unit_{unitIdx}.prefab";
            string texturePath = $"Assets/Textures/Characters/Monsters/{name}/{name}_Idle.png";
            string controllerPath = $"Assets/Anims/Monster/{name}AnimatorController.controller";
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            var renderer = prefab != null ? prefab.GetComponent<SpriteRenderer>() : null;
            var animator = prefab != null ? prefab.GetComponent<Animator>() : null;
            var importer = AssetImporter.GetAtPath(texturePath) as TextureImporter;

            Assert.AreEqual($"Unit_{unitIdx}", prefab.name);
            Assert.NotNull(renderer);
            Assert.NotNull(renderer.sprite);
            Assert.AreEqual(texturePath, AssetDatabase.GetAssetPath(renderer.sprite));
            Assert.NotNull(animator);
            Assert.AreEqual(controllerPath, AssetDatabase.GetAssetPath(animator.runtimeAnimatorController));
            Assert.NotNull(importer);
            Assert.AreEqual(64f, importer.spritePixelsPerUnit);
            Assert.AreEqual(4f, renderer.sprite.bounds.size.y, 0.01f);
            Assert.NotNull(prefab.GetComponent<Monster>());
            Assert.IsNotEmpty(AssetDatabase.AssetPathToGUID(prefabPath));
            Assert.IsNotEmpty(AssetDatabase.AssetPathToGUID(texturePath));
            Assert.IsNotEmpty(AssetDatabase.AssetPathToGUID(controllerPath));

            AssertMonsterClips(name, "", "Idle", "Move", "Hit", "Death", attackA, attackB);
        }

        private static void AssertMonsterVisual(GameObject prefab, string name, params string[] actions)
        {
            string texturePath = $"Assets/Textures/Characters/Monsters/{name}/{name}_Idle.png";
            string controllerPath = $"Assets/Anims/Monster/{name}AnimatorController.controller";
            var renderers = prefab.GetComponentsInChildren<SpriteRenderer>(true);
            var animators = prefab.GetComponentsInChildren<Animator>(true);
            Assert.AreEqual(1, renderers.Length);
            Assert.AreEqual("Visual", renderers[0].transform.name);
            Assert.NotNull(renderers[0].sprite);
            Assert.AreEqual(texturePath, AssetDatabase.GetAssetPath(renderers[0].sprite));
            Assert.AreEqual(1, animators.Length);
            Assert.AreEqual(renderers[0].transform, animators[0].transform);
            Assert.AreEqual(controllerPath, AssetDatabase.GetAssetPath(animators[0].runtimeAnimatorController));
            var collider = prefab.GetComponent<BoxCollider2D>();
            Assert.NotNull(collider);
            Assert.That(collider.size.x / renderers[0].sprite.bounds.size.x, Is.InRange(0.5f, 1f));
            Assert.That(collider.size.y / renderers[0].sprite.bounds.size.y, Is.InRange(0.5f, 1f));
            AssertMonsterClips(name, "Visual", actions);
        }

        private static void AssertMonsterClips(string name, string expectedPath, params string[] actions)
        {
            foreach (string action in actions)
            {
                var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>($"Assets/Anims/Monster/{name}_{action}.anim");
                Assert.NotNull(clip);
                var bindings = AnimationUtility.GetObjectReferenceCurveBindings(clip);
                Assert.AreEqual(1, bindings.Length);
                Assert.AreEqual(expectedPath, bindings[0].path);
                var frames = AnimationUtility.GetObjectReferenceCurve(clip, bindings[0]);
                Assert.AreEqual(8, frames.Length);
                Assert.IsFalse(System.Array.Exists(frames, frame => frame.value == null));
            }
        }
    }
}
