using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using TMPro;

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
        public void CombatPrefabs_HaveValidSpawnZones_AndSafeTemplatesHaveNone()
        {
            foreach (uint resourceIdx in new uint[] { 1050, 1051, 1052, 1053 })
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/Prefabs/Rooms/Room_{10000u + resourceIdx}.prefab");
                Assert.NotNull(prefab);
                SpawnPointMarker[] zones = prefab.GetComponentsInChildren<SpawnPointMarker>(true)
                    .Where(marker => marker.EnableSpawn && marker.Type == SpawnType.Monster).ToArray();
                Assert.GreaterOrEqual(zones.Length, 3, $"Combat {resourceIdx} requires at least three SpawnZones.");
                Assert.IsTrue(UnitSpawner.ValidateSpawnZones(prefab, zones, null, out string error),
                    $"Combat {resourceIdx}: {error}");
                for (int i = 0; i < zones.Length; i++)
                    for (int j = i + 1; j < zones.Length; j++)
                        Assert.GreaterOrEqual(Vector2.Distance(zones[i].transform.position, zones[j].transform.position), 15f);
            }

            foreach (uint resourceIdx in new uint[] { 1056, 1057, 1061, 1063 })
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/Prefabs/Rooms/Room_{10000u + resourceIdx}.prefab");
                Assert.NotNull(prefab);
                Assert.AreEqual(0, prefab.GetComponentsInChildren<SpawnPointMarker>(true)
                    .Count(marker => marker.EnableSpawn && marker.Type == SpawnType.Monster));
            }

            var boss = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Rooms/Prefab_1042.prefab");
            Assert.NotNull(boss);
            Assert.AreEqual(1, boss.GetComponentsInChildren<SpawnPointMarker>(true)
                .Count(marker => marker.EnableSpawn && marker.Type == SpawnType.Boss && marker.MonsterId == 3201u));
        }

        [Test]
        public void Phase4_FontTextScenesAndPrefabs_HaveNoMissingOrDuplicatedReferences()
        {
            var texts = new TextDataTable();
            texts.LoadData(File.ReadAllText("Assets/Datas/TextData.csv"));
            GameLanguageSettings.Current = GameLanguage.En;
            Assert.AreEqual("Enter Stage 1 through the portal.", texts.GetText(2040));
            Assert.AreEqual("Warning: You entered a combat zone.", texts.GetText(2042));
            GameLanguageSettings.Current = GameLanguage.Kr;
            Assert.AreEqual("Stage 1 포탈을 통해 입장하세요.", texts.GetText(2040));
            Assert.AreEqual("경고: 전투 구역에 진입했습니다.", texts.GetText(2042));

            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Fonts/BMJUA_UI.asset");
            var material = AssetDatabase.LoadAssetAtPath<Material>("Assets/Fonts/BMJUA_UI_Shared.mat");
            Assert.NotNull(font);
            Assert.NotNull(material);
            bool supports2040 = font.HasCharacters(texts.GetText(2040));
            bool supports2042 = font.HasCharacters(texts.GetText(2042));
            GameLanguageSettings.Current = GameLanguage.En;
            if (!supports2040) Assert.IsTrue(font.HasCharacters(texts.GetText(2040)), "BMJUA_UI also misses English 2040.");
            if (!supports2042) Assert.IsTrue(font.HasCharacters(texts.GetText(2042)), "BMJUA_UI also misses English 2042.");
            TestContext.WriteLine($"BMJUA glyph readback: 2040={supports2040}, 2042={supports2042}");

            string[] scenePaths =
            {
                "Assets/Scenes/InitScene.unity", "Assets/Scenes/LoadingScene.unity",
                "Assets/Scenes/HubScene.unity", "Assets/Scenes/MainScene.unity"
            };
            foreach (string scenePath in scenePaths)
            {
                var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive);
                try
                {
                    foreach (GameObject root in scene.GetRootGameObjects()) AssertNoMissingReferences(root, scenePath);
                    foreach (TMP_Text text in scene.GetRootGameObjects()
                        .SelectMany(root => root.GetComponentsInChildren<TMP_Text>(true)))
                    {
                        Assert.AreSame(font, text.font, $"{scenePath}/{text.name} uses a duplicated font asset.");
                        Assert.AreSame(material, text.fontSharedMaterial,
                            $"{scenePath}/{text.name} uses a duplicated font material.");
                    }
                }
                finally
                {
                    EditorSceneManager.CloseScene(scene, true);
                }
            }

            foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Prefabs" }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                Assert.NotNull(prefab, path);
                if (prefab != null) AssertNoMissingReferences(prefab, path);
            }
        }

        private static void AssertNoMissingReferences(GameObject root, string path)
        {
            foreach (Component component in root.GetComponentsInChildren<Component>(true))
            {
                Assert.NotNull(component, $"Missing script in {path}/{root.name}");
                var iterator = new SerializedObject(component).GetIterator();
                while (iterator.NextVisible(true))
                {
                    if (iterator.propertyType == SerializedPropertyType.ObjectReference &&
                        iterator.objectReferenceValue == null && iterator.objectReferenceInstanceIDValue != 0)
                        Assert.Fail($"Missing reference in {path}/{component.name}: {iterator.propertyPath}");
                }
            }
        }

        [Test]
        public void Phase4_ScenesAndPrefabs_HaveNoMissingReferences()
        {
            foreach (string scenePath in new[]
            {
                "Assets/Scenes/InitScene.unity", "Assets/Scenes/LoadingScene.unity",
                "Assets/Scenes/HubScene.unity", "Assets/Scenes/MainScene.unity"
            })
            {
                var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive);
                try
                {
                    foreach (GameObject root in scene.GetRootGameObjects()) AssertNoMissingReferences(root, scenePath);
                }
                finally
                {
                    EditorSceneManager.CloseScene(scene, true);
                }
            }

            foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Prefabs" }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                Assert.NotNull(prefab, path);
                if (prefab != null) AssertNoMissingReferences(prefab, path);
            }
        }

        [Test]
        public void NewMonsters_HaveCompleteUniqueImportedAssets()
        {
            AssertMonster(3104, "ShieldSentinel", "Idle", "Move", "Hit", "Death", "Attack6003", "Attack6004");
            AssertMonster(3105, "OrbitalMarksman", "Idle", "Move", "Hit", "Death", "Attack6005", "Attack6006");

            Assert.AreNotEqual(
                AssetDatabase.AssetPathToGUID("Assets/Prefabs/Unit_3104.prefab"),
                AssetDatabase.AssetPathToGUID("Assets/Prefabs/Unit_3105.prefab"));
        }

        [Test]
        public void NormalizedUnitPrefabs_HaveSingleVisualRendererAndIdxPaths()
        {
            StringAssert.Contains("1003,Unit_3101", File.ReadAllText("Assets/Datas/ResourceData.csv"));
            StringAssert.Contains("1006,Unit_3104", File.ReadAllText("Assets/Datas/ResourceData.csv"));

            var units = new UnitBaseDataTable();
            units.LoadData(File.ReadAllText("Assets/Datas/UnitBaseData.csv"));
            foreach (uint unitIdx in new uint[] { 3001, 3101, 3102, 3103, 3104, 3105, 3201 })
            {
                Assert.IsTrue(units.TryGetUnitData(unitIdx, out var unitData));
                AssertUnitVisualFit(unitIdx, unitData.HitboxRadius);
            }

            AssertMonster(3101, "SpearSentry", "Idle", "Move", "Attack", "Death");
            AssertMonster(3104, "ShieldSentinel", "Idle", "Move", "Hit", "Death", "Attack6003", "Attack6004");
        }

        private static void AssertUnitVisualFit(uint unitIdx, float hitboxRadius)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/Prefabs/Unit_{unitIdx}.prefab");
            Assert.NotNull(prefab);
            Assert.AreEqual(Vector3.one, prefab.transform.localScale);

            var renderers = prefab.GetComponentsInChildren<SpriteRenderer>(true);
            Assert.AreEqual(1, renderers.Length);
            var renderer = renderers[0];
            var visual = renderer.transform;
            Assert.AreEqual("Visual", visual.name);
            Assert.AreEqual(Vector3.zero, visual.localPosition);
            Assert.AreEqual(visual.localScale.x, visual.localScale.y, 0.0001f);
            Assert.AreEqual(1f, visual.localScale.z, 0.0001f);
            Assert.Greater(visual.localScale.x, 0f);
            Assert.IsFalse(float.IsNaN(visual.localScale.x) || float.IsInfinity(visual.localScale.x));
            Assert.NotNull(renderer.sprite);

            string texturePath = AssetDatabase.GetAssetPath(renderer.sprite);
            var importer = AssetImporter.GetAtPath(texturePath) as TextureImporter;
            Assert.NotNull(importer);
            Assert.AreEqual(100f, importer.spritePixelsPerUnit);
            Assert.AreEqual(renderer.sprite.rect.width * 0.5f, renderer.sprite.pivot.x, 0.001f);
            Assert.AreEqual(0f, renderer.sprite.pivot.y, 0.001f);

            float hitboxWidth = 2f * hitboxRadius;
            float hitboxHeight = 4f * hitboxRadius;
            Vector2 visualBounds = renderer.bounds.size;
            const float tolerance = 0.001f;
            Assert.LessOrEqual(visualBounds.x, hitboxWidth + tolerance);
            Assert.LessOrEqual(visualBounds.y, hitboxHeight + tolerance);
            Assert.IsTrue(
                Mathf.Abs(visualBounds.x - hitboxWidth) <= tolerance ||
                Mathf.Abs(visualBounds.y - hitboxHeight) <= tolerance,
                $"Unit_{unitIdx} Visual must touch at least one hitbox axis. " +
                $"Visual={visualBounds}, Hitbox=({hitboxWidth}, {hitboxHeight})");
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
            var visual = renderers[0].transform;
            var importer = AssetImporter.GetAtPath(texturePath) as TextureImporter;
            Assert.NotNull(importer);
            Assert.AreEqual(100f, importer.spritePixelsPerUnit);
            Assert.AreEqual(Vector3.zero, visual.localPosition);
            Assert.AreEqual(visual.localScale.x, visual.localScale.y, 0.0001f);
            Assert.AreEqual(1f, visual.localScale.z, 0.0001f);
            Assert.Greater(visual.localScale.x, 0f);
            Assert.IsFalse(float.IsNaN(visual.localScale.x) || float.IsInfinity(visual.localScale.x));

            var collider = prefab.GetComponent<BoxCollider2D>();
            Assert.NotNull(collider);
            TestContext.WriteLine($"{prefab.name} collider/renderer ratio: " +
                $"{collider.size.x / (renderers[0].sprite.bounds.size.x * visual.localScale.x):F3} x " +
                $"{collider.size.y / (renderers[0].sprite.bounds.size.y * visual.localScale.y):F3}");
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
