using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Tilemaps;
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
                ChunkSocketMarker[] sockets = prefab.GetComponentsInChildren<ChunkSocketMarker>(true);
                SpawnPointMarker entry = prefab.GetComponentsInChildren<SpawnPointMarker>(true)
                    .Single(marker => marker.Type == SpawnType.Player);
                for (int i = 0; i < zones.Length; i++)
                {
                    Assert.GreaterOrEqual(Vector2.Distance(zones[i].transform.position, entry.transform.position), 14f,
                        $"Combat {resourceIdx} SpawnZone must stay >=14m from entry.");
                    foreach (ChunkSocketMarker socket in sockets)
                        Assert.GreaterOrEqual(Vector2.Distance(zones[i].transform.position, socket.transform.position), 7f,
                            $"Combat {resourceIdx} SpawnZone must stay >=7m from portals.");
                    for (int j = i + 1; j < zones.Length; j++)
                        Assert.GreaterOrEqual(Vector2.Distance(zones[i].transform.position, zones[j].transform.position), 15f,
                            $"Combat {resourceIdx} SpawnZones must stay >=15m apart.");
                }
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
        public void Stage1PortalLandings_HaveSafeSurfacesAndReachableGround()
        {
            var portalPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Structures/Portal_Gate.prefab");
            var portalTrigger = portalPrefab != null ? portalPrefab.GetComponent<BoxCollider2D>() : null;
            Assert.NotNull(portalTrigger);
            Assert.IsTrue(portalTrigger.isTrigger);
            Assert.AreEqual(new Vector2(1f, 2f), portalTrigger.size);

            string[] roomPaths =
            {
                "Assets/Prefabs/Rooms/Prefab_1040.prefab", "Assets/Prefabs/Rooms/Prefab_1041.prefab",
                "Assets/Prefabs/Rooms/Prefab_1042.prefab", "Assets/Prefabs/Rooms/Room_11050.prefab",
                "Assets/Prefabs/Rooms/Room_11051.prefab", "Assets/Prefabs/Rooms/Room_11052.prefab",
                "Assets/Prefabs/Rooms/Room_11053.prefab", "Assets/Prefabs/Rooms/Room_11056.prefab",
                "Assets/Prefabs/Rooms/Room_11057.prefab", "Assets/Prefabs/Rooms/Room_11061.prefab",
                "Assets/Prefabs/Rooms/Room_11063.prefab"
            };

            foreach (string roomPath in roomPaths)
            {
                var room = AssetDatabase.LoadAssetAtPath<GameObject>(roomPath);
                Assert.NotNull(room, roomPath);
                ChunkSocketMarker[] sockets = room.GetComponentsInChildren<ChunkSocketMarker>(true);
                Assert.AreEqual(4, sockets.Length, roomPath);
                var surfaces = sockets.Select(socket => FindSupportingSurface(room, socket, out _, out _)).ToArray();
                float floorSurface = surfaces.Min();
                List<Vector2> walkableSurfaces = GetWalkableSurfaces(room);

                for (int i = 0; i < sockets.Length; i++)
                {
                    ChunkSocketMarker socket = sockets[i];
                    Assert.NotNull(socket.EntryMarker, $"{roomPath}/{socket.Direction} EntryMarker is missing.");
                    float surface = FindSupportingSurface(room, socket, out Tilemap ground, out Vector3Int cell);
                    float expectedCenterY = socket.Direction == ChunkSocketDirection.North ? 4f
                        : socket.Direction == ChunkSocketDirection.East ? 5f
                        : socket.Direction == ChunkSocketDirection.South ? 3f : 2f;
                    Assert.AreEqual(expectedCenterY, socket.transform.position.y, 0.011f,
                        $"{roomPath}/{socket.Direction} portal center must match the authoritative room layout.");
                    Assert.AreEqual(expectedCenterY - 0.49f, socket.EntryMarker.position.y, 0.011f,
                        $"{roomPath}/{socket.Direction} EntryMarker must preserve the Player clearance offset.");
                    Assert.GreaterOrEqual(socket.transform.position.y - portalTrigger.size.y * 0.5f, surface - 0.011f);
                    Assert.IsFalse(HasSolidTileInPortalHeadroom(room, socket.transform.position.x, surface, surface + 2f),
                        $"{roomPath}/{socket.Direction} requires 2m portal head clearance.");

                    if (surface > floorSurface + 0.011f)
                    {
                        AssertSolidFootprint(ground, cell, roomPath, socket.Direction);
                        Assert.IsTrue(HasWalkableRoute(walkableSurfaces, socket.transform.position.x, surface, floorSurface),
                            $"{roomPath}/{socket.Direction} has no route with step <=1m and gap <=2m.");
                    }
                }

                foreach (Tilemap platform in room.GetComponentsInChildren<Tilemap>(true)
                    .Where(tilemap => tilemap.GetComponent<PlatformEffector2D>() != null))
                    AssertNoShortOneWayRuns(platform, roomPath);
            }
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

            const string fontPath = "Assets/Fonts/TP1_BMJUA/BMJUA_ttf SDF.asset";
            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(fontPath);
            var material = font != null ? font.material : null;
            Assert.NotNull(font);
            Assert.NotNull(material);
            Assert.AreEqual("6c71dcc91862372499bc2332a17f2ee4", AssetDatabase.AssetPathToGUID(fontPath));
            Assert.IsTrue(font.HasCharacters(texts.GetText(2040)), "TP1_BMJUA misses Korean TextData 2040.");
            Assert.IsTrue(font.HasCharacters(texts.GetText(2042)), "TP1_BMJUA misses Korean TextData 2042.");
            Assert.IsTrue(font.HasCharacters("체력 자세 마력 스킬 입장 경고"), "TP1_BMJUA misses required HUD/alert glyphs.");
            GameLanguageSettings.Current = GameLanguage.En;
            Assert.AreEqual("Enter Stage 1 through the portal.", texts.GetText(2040));
            Assert.AreEqual("Warning: You entered a combat zone.", texts.GetText(2042));

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
                    TMP_Text[] sceneTexts = scene.GetRootGameObjects()
                        .SelectMany(root => root.GetComponentsInChildren<TMP_Text>(true)).ToArray();
                    foreach (TMP_Text text in sceneTexts)
                    {
                        Assert.NotNull(text.font, $"{scenePath}/{text.name} has no TMP font.");
                        Assert.NotNull(text.fontSharedMaterial, $"{scenePath}/{text.name} has no TMP material.");
                    }

                    if (scenePath == "Assets/Scenes/MainScene.unity")
                    {
                        var hud = scene.GetRootGameObjects()
                            .SelectMany(root => root.GetComponentsInChildren<ProductionMainHUD>(true)).Single();
                        var hudSerialized = new SerializedObject(hud);
                        foreach (string propertyName in new[] { "playerHpText", "playerPostureText", "playerMpText" })
                            Assert.NotNull(hudSerialized.FindProperty(propertyName).objectReferenceValue,
                                $"ProductionMainHUD.{propertyName} must be serialized.");

                        var minimap = scene.GetRootGameObjects()
                            .SelectMany(root => root.GetComponentsInChildren<ProductionMinimap>(true)).Single();
                        var roomViews = new SerializedObject(minimap).FindProperty("roomViews");
                        Assert.AreEqual(12, roomViews.arraySize, "ProductionMinimap requires one view per 4x3 grid cell.");
                        for (int i = 0; i < roomViews.arraySize; i++)
                        {
                            var root = roomViews.GetArrayElementAtIndex(i).FindPropertyRelative("Root").objectReferenceValue as RectTransform;
                            Assert.NotNull(root, $"ProductionMinimap room {i} Root must be serialized.");
                            var label = root.GetComponentInChildren<TMP_Text>(true);
                            Assert.NotNull(label, $"ProductionMinimap room {i} requires a TMP label reference.");
                            Assert.NotNull(label.font, $"ProductionMinimap room {i} label has no font.");
                            Assert.NotNull(label.fontSharedMaterial, $"ProductionMinimap room {i} label has no material.");
                        }
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

        [Test]
        public void Phase5_SortingLayersAndRendererRoles_MatchProductionContract()
        {
            CollectionAssert.AreEqual(new[]
            {
                "Default", "FarBackground", "NearBackground", "Tilemap", "Unit", "Effect", "WorldUI"
            }, SortingLayer.layers.Select(layer => layer.name).ToArray());

            var prefabs = AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Prefabs" })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Select(path => AssetDatabase.LoadAssetAtPath<GameObject>(path))
                .Where(prefab => prefab != null).ToArray();
            int unitGroups = prefabs.SelectMany(prefab => prefab.GetComponentsInChildren<SortingGroup>(true))
                .Count(group => group.sortingLayerName == "Unit");
            int worldUiCanvases = prefabs.SelectMany(prefab => prefab.GetComponentsInChildren<Canvas>(true))
                .Count(canvas => canvas.sortingLayerName == "WorldUI");
            string[] stage1RoomPaths =
            {
                "Assets/Prefabs/Rooms/Prefab_1040.prefab", "Assets/Prefabs/Rooms/Prefab_1041.prefab",
                "Assets/Prefabs/Rooms/Prefab_1042.prefab", "Assets/Prefabs/Rooms/Room_11050.prefab",
                "Assets/Prefabs/Rooms/Room_11051.prefab", "Assets/Prefabs/Rooms/Room_11052.prefab",
                "Assets/Prefabs/Rooms/Room_11053.prefab", "Assets/Prefabs/Rooms/Room_11056.prefab",
                "Assets/Prefabs/Rooms/Room_11057.prefab", "Assets/Prefabs/Rooms/Room_11061.prefab",
                "Assets/Prefabs/Rooms/Room_11063.prefab"
            };
            var nonRoomEffectRenderers = prefabs.Where(prefab => !AssetDatabase.GetAssetPath(prefab).StartsWith("Assets/Prefabs/Rooms/"))
                .SelectMany(prefab => prefab.GetComponentsInChildren<Renderer>(true))
                .Where(renderer => renderer.sortingLayerName == "Effect").ToArray();
            const string projectilePath = "Assets/Prefabs/Projectiles/Projectile_1045.prefab";
            var projectilePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(projectilePath);
            Assert.NotNull(projectilePrefab, projectilePath);
            Renderer[] projectileRenderers = projectilePrefab.GetComponentsInChildren<Renderer>(true);

            Assert.AreEqual(7, unitGroups, "All seven Unit prefabs require a Unit SortingGroup.");
            Assert.AreEqual(5, worldUiCanvases, "Five regular Monster HUD roots require WorldUI Canvases.");
            foreach (string roomPath in stage1RoomPaths)
            {
                var room = AssetDatabase.LoadAssetAtPath<GameObject>(roomPath);
                Assert.NotNull(room, roomPath);
                TilemapRenderer[] tilemaps = room.GetComponentsInChildren<TilemapRenderer>(true);
                Assert.IsNotEmpty(tilemaps, $"{roomPath} requires authored tilemaps/platforms.");
                Assert.IsFalse(tilemaps.Any(renderer => renderer.sortingLayerName != "Tilemap"),
                    $"Every TilemapRenderer in {roomPath} must use the Tilemap layer.");
            }
            Assert.AreEqual(1, projectileRenderers.Length, "Projectile_1045 requires exactly one renderer.");
            Assert.AreEqual("Effect", projectileRenderers[0].sortingLayerName);
            Assert.AreEqual(14, nonRoomEffectRenderers.Count(renderer =>
                !AssetDatabase.GetAssetPath(renderer).StartsWith("Assets/Prefabs/Projectiles/")),
                "The pre-projectile production Effect renderer roles must remain unchanged.");
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

        private static float FindSupportingSurface(GameObject room, ChunkSocketMarker socket,
            out Tilemap supportingTilemap, out Vector3Int supportingCell)
        {
            supportingTilemap = null;
            supportingCell = default;
            float best = float.NegativeInfinity;
            foreach (Tilemap tilemap in room.GetComponentsInChildren<Tilemap>(true)
                .Where(candidate => candidate.GetComponent<TilemapCollider2D>() != null &&
                                    candidate.GetComponent<PlatformEffector2D>() == null))
            {
                foreach (Vector3Int cell in tilemap.cellBounds.allPositionsWithin)
                {
                    if (!tilemap.HasTile(cell)) continue;
                    GetCellBounds(tilemap, cell, out float left, out float right, out _, out float top);
                    if (socket.transform.position.x < left - 0.011f || socket.transform.position.x > right + 0.011f ||
                        top > socket.transform.position.y + 0.011f || top <= best) continue;
                    best = top;
                    supportingTilemap = tilemap;
                    supportingCell = cell;
                }
            }

            Assert.IsNotNull(supportingTilemap, $"{room.name}/{socket.Direction} has no supporting solid Ground tile.");
            return best;
        }

        private static void AssertSolidFootprint(Tilemap ground, Vector3Int center, string roomPath,
            ChunkSocketDirection direction)
        {
            int left = center.x;
            int right = center.x;
            while (ground.HasTile(new Vector3Int(left - 1, center.y, center.z))) left--;
            while (ground.HasTile(new Vector3Int(right + 1, center.y, center.z))) right++;
            Assert.GreaterOrEqual(right - left + 1, 3, $"{roomPath}/{direction} landing must be at least 3 cells wide.");
            Assert.GreaterOrEqual(Enumerable.Range(left, right - left + 1)
                .Count(x => ground.HasTile(new Vector3Int(x, center.y - 1, center.z))), 3,
                $"{roomPath}/{direction} landing must have a 3x2 solid Ground footprint.");
        }

        private static bool HasSolidTileInPortalHeadroom(GameObject room, float centerX, float bottom, float top)
        {
            foreach (Tilemap tilemap in room.GetComponentsInChildren<Tilemap>(true)
                .Where(candidate => candidate.GetComponent<TilemapCollider2D>() != null &&
                                    candidate.GetComponent<PlatformEffector2D>() == null))
                foreach (Vector3Int cell in tilemap.cellBounds.allPositionsWithin)
                {
                    if (!tilemap.HasTile(cell)) continue;
                    GetCellBounds(tilemap, cell, out float left, out float right, out float cellBottom, out float cellTop);
                    if (right > centerX - 0.5f + 0.011f && left < centerX + 0.5f - 0.011f &&
                        cellTop > bottom + 0.011f && cellBottom < top - 0.011f) return true;
                }
            return false;
        }

        private static List<Vector2> GetWalkableSurfaces(GameObject room)
        {
            var result = new List<Vector2>();
            foreach (Tilemap tilemap in room.GetComponentsInChildren<Tilemap>(true)
                .Where(candidate => candidate.GetComponent<TilemapCollider2D>() != null))
                foreach (Vector3Int cell in tilemap.cellBounds.allPositionsWithin)
                {
                    if (!tilemap.HasTile(cell) || tilemap.HasTile(cell + Vector3Int.up)) continue;
                    GetCellBounds(tilemap, cell, out float left, out float right, out _, out float top);
                    result.Add(new Vector2((left + right) * 0.5f, top));
                }
            return result;
        }

        private static bool HasWalkableRoute(List<Vector2> surfaces, float startX, float startY, float floorY)
        {
            var visited = new HashSet<int>();
            var pending = new Queue<int>(Enumerable.Range(0, surfaces.Count)
                .Where(i => Mathf.Abs(surfaces[i].y - startY) <= 0.011f && Mathf.Abs(surfaces[i].x - startX) <= 1.5f));
            while (pending.Count > 0)
            {
                int current = pending.Dequeue();
                if (!visited.Add(current)) continue;
                if (surfaces[current].y <= floorY + 0.011f) return true;
                for (int i = 0; i < surfaces.Count; i++)
                    if (!visited.Contains(i) && Mathf.Abs(surfaces[i].y - surfaces[current].y) <= 1.011f &&
                        Mathf.Max(0f, Mathf.Abs(surfaces[i].x - surfaces[current].x) - 1f) <= 2.011f)
                        pending.Enqueue(i);
            }
            return false;
        }

        private static void AssertNoShortOneWayRuns(Tilemap platform, string roomPath)
        {
            var rows = new Dictionary<int, List<int>>();
            foreach (Vector3Int cell in platform.cellBounds.allPositionsWithin)
            {
                if (!platform.HasTile(cell)) continue;
                if (!rows.TryGetValue(cell.y, out List<int> cells)) rows[cell.y] = cells = new List<int>();
                cells.Add(cell.x);
            }
            foreach (List<int> row in rows.Values)
            {
                int[] cells = row.OrderBy(x => x).ToArray();
                int run = 1;
                for (int i = 1; i <= cells.Length; i++)
                {
                    if (i < cells.Length && cells[i] == cells[i - 1] + 1) { run++; continue; }
                    Assert.GreaterOrEqual(run, 3, $"{roomPath}/{platform.name} has a disconnected {run}-cell one-way run.");
                    run = 1;
                }
            }
        }

        private static void GetCellBounds(Tilemap tilemap, Vector3Int cell, out float left, out float right,
            out float bottom, out float top)
        {
            Grid grid = tilemap.transform.root.GetComponent<Grid>();
            Assert.NotNull(grid, $"{tilemap.name} requires a Grid root.");
            Vector3 origin = grid.CellToWorld(cell);
            Vector3 x = grid.CellToWorld(cell + Vector3Int.right);
            Vector3 y = grid.CellToWorld(cell + Vector3Int.up);
            left = Mathf.Min(origin.x, x.x);
            right = Mathf.Max(origin.x, x.x);
            bottom = Mathf.Min(origin.y, y.y);
            top = Mathf.Max(origin.y, y.y);
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
