#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace QA.Tests
{
    public class Candidate1uTechnicalGateTests
    {
        private const string Path = "Assets/Prefabs/Development/Tilemap_Room_Candidate1u_ImageReconstructed.prefab";
        private const string CombatReservedPath = "Assets/Prefabs/Development/Tilemap_Room_Candidate1u_CombatReserved.prefab";
        private const string GoldenTrialPath = "Assets/Prefabs/Development/Tilemap_Room_GoldenDerived_Trial01.prefab";
        private const string EmptyFirstTrial02Path = "Assets/Prefabs/Development/Tilemap_Room_EmptyFirst_Trial02.prefab";
        private const string EmptyFirstAngularTrial03Path = "Assets/Prefabs/Development/Tilemap_Room_EmptyFirstAngular_Trial03.prefab";
        private const string EmptyFirstAngularTrial04Path = "Assets/Prefabs/Development/Tilemap_Room_EmptyFirstAngular_Trial04.prefab";

        [Test]
        public void EmptyFirstAngularTrial04_ReachableStaticGenerationContracts()
        {
            string[] preserved = { Path, GoldenTrialPath, EmptyFirstTrial02Path, EmptyFirstAngularTrial03Path };
            byte[][] before = preserved.Select(File.ReadAllBytes).ToArray();
            GameObject original = AssetDatabase.LoadAssetAtPath<GameObject>(EmptyFirstAngularTrial04Path);
            Tilemap originalGround = original.GetComponentsInChildren<Tilemap>(true).Single(x => x.name == "Tilemap_Ground");
            Tilemap originalPlatforms = original.GetComponentsInChildren<Tilemap>(true).Single(x => x.name == "Tilemap_Platforms");
            Vector3Int[] groundBefore = TileCells(originalGround);
            Vector3Int[] platformsBefore = TileCells(originalPlatforms);
            Vector3[] portalsBefore = original.GetComponentsInChildren<ChunkSocketMarker>(true)
                .Select(marker => marker.transform.localPosition).OrderBy(position => position.x).ThenBy(position => position.y).ToArray();
            BoxCollider2D boundsBefore = original.GetComponentsInChildren<BoxCollider2D>(true).Single(x => x.name == "CameraBounds");
            Vector2 cameraSizeBefore = boundsBefore.size;
            Vector3 cameraPositionBefore = boundsBefore.transform.localPosition;
            ModuleChunkBuilder.BuildEmptyFirstAngularTrial04();
            for (int i = 0; i < preserved.Length; i++) CollectionAssert.AreEqual(before[i], File.ReadAllBytes(preserved[i]));

            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(EmptyFirstAngularTrial04Path);
            Assert.NotNull(prefab);
            Tilemap ground = prefab.GetComponentsInChildren<Tilemap>(true).Single(x => x.name == "Tilemap_Ground");
            Tilemap platforms = prefab.GetComponentsInChildren<Tilemap>(true).Single(x => x.name == "Tilemap_Platforms");
            CollectionAssert.AreEquivalent(groundBefore, TileCells(ground));
            CollectionAssert.AreEquivalent(platformsBefore, TileCells(platforms));
            CollectionAssert.AreEqual(portalsBefore, prefab.GetComponentsInChildren<ChunkSocketMarker>(true)
                .Select(marker => marker.transform.localPosition).OrderBy(position => position.x).ThenBy(position => position.y).ToArray());
            BoxCollider2D boundsAfter = prefab.GetComponentsInChildren<BoxCollider2D>(true).Single(x => x.name == "CameraBounds");
            Assert.AreEqual(cameraSizeBefore, boundsAfter.size);
            Assert.AreEqual(cameraPositionBefore, boundsAfter.transform.localPosition);
            int solid = 0;
            for (int y = 0; y < 60; y++)
            for (int x = 0; x < 84; x++)
            {
                if (ground.HasTile(new Vector3Int(x, y, 0))) solid++;
                if (x < 3 || x >= 81 || y < 3 || y >= 57)
                    Assert.IsTrue(ground.HasTile(new Vector3Int(x, y, 0)), $"shell gap ({x},{y})");
            }
            Assert.LessOrEqual(solid, 3063);
            Assert.GreaterOrEqual(5040 - solid, 1977);
            Assert.AreEqual(1, EmptyRegions(ground));
            SpawnPointMarker[] spawns = prefab.GetComponentsInChildren<SpawnPointMarker>(true);
            Assert.AreEqual(4, spawns.Length);
            GameObject playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Unit_3001.prefab");
            CapsuleCollider2D playerCollider = playerPrefab.GetComponent<CapsuleCollider2D>();
            KinematicMotor2D playerMotor = playerPrefab.GetComponent<KinematicMotor2D>();
            float colliderBottom = (playerCollider.offset.y - playerCollider.size.y * .5f) *
                Mathf.Abs(playerCollider.transform.lossyScale.y);
            foreach (SpawnPointMarker spawn in spawns)
            {
                int x = Mathf.RoundToInt(spawn.transform.localPosition.x);
                int supportY = Mathf.FloorToInt(spawn.transform.localPosition.y - .51f) - 1;
                for (int supportX = x - 2; supportX < x + 2; supportX++)
                    Assert.IsTrue(ground.HasTile(new Vector3Int(supportX, supportY, 0)), $"unsupported spawn {spawn.name}");
                float groundTop = supportY + 1f;
                float gap = spawn.transform.localPosition.y + colliderBottom - groundTop;
                Assert.GreaterOrEqual(gap, 0f, $"embedded spawn {spawn.name}");
                Assert.LessOrEqual(gap, playerMotor.SkinWidth + .0001f, $"airborne spawn {spawn.name}");
            }

            RectInt[] combatRooms =
            {
                new RectInt(3, 5, 30, 16), new RectInt(51, 5, 30, 16), new RectInt(27, 41, 30, 16)
            };
            foreach (RectInt room in combatRooms)
            {
                int longest = 0, run = 0;
                for (int x = room.xMin; x < room.xMax; x++)
                {
                    run = ground.HasTile(new Vector3Int(x, room.yMin - 1, 0)) ? run + 1 : 0;
                    longest = Mathf.Max(longest, run);
                }
                Assert.GreaterOrEqual(longest, 12, $"combat floor {room}");
            }

            Player player = playerPrefab.GetComponent<Player>();
            KinematicMotor2D motor = playerPrefab.GetComponent<KinematicMotor2D>();
            var unitTable = new UnitBaseDataTable();
            unitTable.LoadData(AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/Datas/UnitBaseData.csv").text);
            Assert.IsTrue(unitTable.TryGetUnitData(3001u, out UnitBaseData playerData));
            float jump = new SerializedObject(player).FindProperty("jumpForce").floatValue;
            float height = jump * jump / (2f * motor.Gravity);
            float maxRise = height - motor.SkinWidth * 2f;
            float maxHorizontal = playerData.MoveSpeed * (jump / motor.Gravity +
                Mathf.Sqrt(2f * height / (motor.Gravity * motor.FallGravityMultiplier))) - motor.SkinWidth * 2f;
            var surfaces = new List<Vector2>();
            for (int y = 3; y < 57; y++)
            for (int x = 3; x < 81; x++)
                if (ground.HasTile(new Vector3Int(x, y, 0)) && !ground.HasTile(new Vector3Int(x, y + 1, 0)))
                    surfaces.Add(new Vector2(x + .5f, y + 1f));
            var platformsByHeight = new List<Vector2>();
            foreach (Vector3Int cell in platforms.cellBounds.allPositionsWithin)
                if (platforms.HasTile(cell) && !platforms.HasTile(cell + Vector3Int.left))
                    platformsByHeight.Add(new Vector2(cell.x + 1.5f, cell.y + 1f));
            platformsByHeight.Sort((a, b) => a.y.CompareTo(b.y));
            foreach (Vector2 platform in platformsByHeight)
            {
                Assert.IsTrue(surfaces.Any(previous => platform.y > previous.y && platform.y - previous.y <= maxRise &&
                    Mathf.Abs(platform.x - previous.x) <= maxHorizontal), $"unreachable platform {platform}");
                Assert.LessOrEqual(platformsByHeight.Count(other => other.y > platform.y && other.y - platform.y <= maxRise &&
                    Mathf.Abs(other.x - platform.x) <= maxHorizontal), 2, $"platform density {platform}");
                surfaces.Add(platform);
            }
        }

        [Test]
        public void EmptyFirstAngularTrial03_StaticGenerationContracts()
        {
            byte[] trial02Before = File.ReadAllBytes(EmptyFirstTrial02Path);
            byte[] goldenBefore = File.ReadAllBytes(GoldenTrialPath);
            ModuleChunkBuilder.BuildEmptyFirstAngularTrial03();
            CollectionAssert.AreEqual(trial02Before, File.ReadAllBytes(EmptyFirstTrial02Path));
            CollectionAssert.AreEqual(goldenBefore, File.ReadAllBytes(GoldenTrialPath));

            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(EmptyFirstAngularTrial03Path);
            Assert.NotNull(prefab);
            Tilemap ground = prefab.GetComponentsInChildren<Tilemap>(true).Single(x => x.name == "Tilemap_Ground");
            Tilemap platforms = prefab.GetComponentsInChildren<Tilemap>(true).Single(x => x.name == "Tilemap_Platforms");
            Assert.AreEqual(new Vector3Int(84, 60, 1), ground.cellBounds.size);
            Assert.AreEqual(4, prefab.GetComponentsInChildren<ChunkSocketMarker>(true).Length);
            Assert.AreEqual(4, prefab.GetComponentsInChildren<SpawnPointMarker>(true).Count(x => x.Type == SpawnType.Monster));
            Assert.AreEqual(1, EmptyRegions(ground));
            Assert.IsFalse(TileRegionCells(ground).Any(region => region.Count <= 2), "solid island");

            RectInt[] combatRooms =
            {
                new RectInt(6, 8, 24, 12), new RectInt(54, 8, 24, 12), new RectInt(30, 40, 24, 12)
            };
            foreach (RectInt room in combatRooms)
            {
                Assert.GreaterOrEqual(room.width * room.height, 288);
                int longestFloor = 0, floorRun = 0;
                for (int x = room.xMin; x < room.xMax; x++)
                {
                    floorRun = ground.HasTile(new Vector3Int(x, room.yMin - 1, 0)) ? floorRun + 1 : 0;
                    longestFloor = Mathf.Max(longestFloor, floorRun);
                    for (int y = room.yMin; y < room.yMin + 8; y++)
                        Assert.IsFalse(ground.HasTile(new Vector3Int(x, y, 0)), $"combat headroom {room}");
                }
                Assert.GreaterOrEqual(longestFloor, 12, $"combat floor {room}");
            }
            foreach (RectInt part in new[] { new RectInt(24, 26, 36, 8), new RectInt(24, 20, 8, 20) })
                foreach (Vector3Int cell in new BoundsInt(part.x, part.y, 0, part.width, part.height, 1).allPositionsWithin)
                    Assert.IsFalse(ground.HasTile(cell), $"Traversal L blocked {cell}");

            for (int y = 0; y < 60; y++)
            for (int x = 0; x < 84; x++)
                if (x < 3 || x >= 81 || y < 3 || y >= 57)
                    Assert.IsTrue(ground.HasTile(new Vector3Int(x, y, 0)), $"shell gap ({x},{y})");
            foreach (Vector3Int cell in ground.cellBounds.allPositionsWithin)
            {
                if (!ground.HasTile(cell) || cell.x < 3 || cell.x >= 81 || cell.y < 3 || cell.y >= 57) continue;
                int neighbors = 0;
                foreach (Vector3Int direction in new[] { Vector3Int.left, Vector3Int.right, Vector3Int.up, Vector3Int.down })
                    if (ground.HasTile(cell + direction)) neighbors++;
                Assert.Greater(neighbors, 1, $"solid spur {cell}");
            }
            GameObject playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Unit_3001.prefab");
            Player player = playerPrefab.GetComponent<Player>();
            KinematicMotor2D motor = playerPrefab.GetComponent<KinematicMotor2D>();
            var unitTable = new UnitBaseDataTable();
            unitTable.LoadData(AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/Datas/UnitBaseData.csv").text);
            Assert.IsTrue(unitTable.TryGetUnitData(3001u, out UnitBaseData playerData));
            float jumpVelocity = new SerializedObject(player).FindProperty("jumpForce").floatValue;
            float jumpHeight = jumpVelocity * jumpVelocity / (2f * motor.Gravity);
            float maxRise = jumpHeight - motor.SkinWidth * 2f;
            float maxHorizontal = playerData.MoveSpeed * (jumpVelocity / motor.Gravity +
                Mathf.Sqrt(2f * jumpHeight / (motor.Gravity * motor.FallGravityMultiplier))) - motor.SkinWidth * 2f;
            var platformSurfaces = new List<Vector2>();
            foreach (Vector3Int cell in platforms.cellBounds.allPositionsWithin)
            {
                if (!platforms.HasTile(cell)) continue;
                for (int y = cell.y; y <= cell.y + 4; y++)
                    Assert.IsFalse(ground.HasTile(new Vector3Int(cell.x, y, 0)), $"OneWay headroom {cell}");
                if (!platforms.HasTile(cell + Vector3Int.left))
                    platformSurfaces.Add(new Vector2(cell.x + 1.5f, cell.y + 1f));
            }
            Assert.Greater(platformSurfaces.Count, 0);
            platformSurfaces.Sort((a, b) => a.y.CompareTo(b.y));
            var predecessors = new List<Vector2>();
            for (int y = 3; y < 57; y++)
            for (int x = 3; x < 81; x++)
                if (ground.HasTile(new Vector3Int(x, y, 0)) && !ground.HasTile(new Vector3Int(x, y + 1, 0)))
                    predecessors.Add(new Vector2(x + .5f, y + 1f));
            foreach (Vector2 platform in platformSurfaces)
            {
                Assert.IsTrue(predecessors.Any(surface => platform.y > surface.y &&
                    platform.y - surface.y <= maxRise && Mathf.Abs(platform.x - surface.x) <= maxHorizontal),
                    $"OneWay has no reachable predecessor {platform}");
                Assert.LessOrEqual(platformSurfaces.Count(other => other.y > platform.y && other.y - platform.y <= maxRise &&
                    Mathf.Abs(other.x - platform.x) <= maxHorizontal), 2,
                    $"OneWay density exceeds two per jump height at {platform}");
                predecessors.Add(platform);
            }
        }

        [Test]
        public void EmptyFirstTrial02_StaticGenerationContracts()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(EmptyFirstTrial02Path);
            Assert.NotNull(prefab);
            Tilemap ground = prefab.GetComponentsInChildren<Tilemap>(true).Single(x => x.name == "Tilemap_Ground");
            Tilemap platforms = prefab.GetComponentsInChildren<Tilemap>(true).Single(x => x.name == "Tilemap_Platforms");
            Assert.AreEqual(new Vector3Int(84, 60, 1), ground.cellBounds.size);
            Assert.AreEqual(4, prefab.GetComponentsInChildren<ChunkSocketMarker>(true).Length);
            Assert.AreEqual(4, prefab.GetComponentsInChildren<SpawnPointMarker>(true).Count(x => x.Type == SpawnType.Monster));
            Assert.AreEqual(1, EmptyRegions(ground));
            Assert.IsFalse(TileRegionCells(ground).Any(region => region.Count <= 2), "1-2 cell solid artifact");

            for (int y = 0; y < 60; y++)
            for (int x = 0; x < 84; x++)
                if (x < 3 || x >= 81 || y < 3 || y >= 57)
                    Assert.IsTrue(ground.HasTile(new Vector3Int(x, y, 0)), $"shell gap ({x},{y})");
            foreach (Vector3Int cell in ground.cellBounds.allPositionsWithin)
            {
                if (!ground.HasTile(cell) || cell.x < 3 || cell.x >= 81 || cell.y < 3 || cell.y >= 57) continue;
                int neighbors = 0;
                foreach (Vector3Int direction in new[] { Vector3Int.left, Vector3Int.right, Vector3Int.up, Vector3Int.down })
                    if (ground.HasTile(cell + direction)) neighbors++;
                Assert.Greater(neighbors, 1, $"solid spur {cell}");
            }
            int oneWayCount = 0;
            foreach (Vector3Int cell in platforms.cellBounds.allPositionsWithin)
            {
                if (!platforms.HasTile(cell)) continue;
                oneWayCount++;
                for (int y = cell.y; y <= cell.y + 4; y++)
                    Assert.IsFalse(ground.HasTile(new Vector3Int(cell.x, y, 0)), $"OneWay headroom {cell}");
            }
            Assert.Greater(oneWayCount, 0);
        }

        [Test]
        public void EmptyFirstTrial02_Motor_PortalAndSpawn_60And15Fps()
        {
            GameObject room = null, player = null;
            SimulationMode2D previous = Physics2D.simulationMode;
            try
            {
                Physics2D.simulationMode = SimulationMode2D.Script;
                room = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(EmptyFirstTrial02Path));
                player = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Unit_3001.prefab"));
                KinematicMotor2D motor = player.GetComponent<KinematicMotor2D>();
                Collider2D body = player.GetComponents<Collider2D>().First(x => !x.isTrigger);
                motor.InitMotor();
                List<Vector2> surfaces = Stage1TraversalGateTests.CollectStandableSurfaces(room);
                ChunkSocketMarker[] sockets = room.GetComponentsInChildren<ChunkSocketMarker>(true);
                SpawnPointMarker[] spawns = room.GetComponentsInChildren<SpawnPointMarker>(true).Where(x => x.Type == SpawnType.Monster).ToArray();
                foreach (float dt in new[] { 1f / 60f, 1f / 15f })
                {
                    foreach (ChunkSocketMarker from in sockets)
                    foreach (ChunkSocketMarker to in sockets)
                    {
                        if (from == to) continue;
                        Vector2 start = new(from.EntryMarker.position.x, from.EntryMarker.position.y - .51f);
                        Vector2 target = new(to.EntryMarker.position.x, to.EntryMarker.position.y - .51f);
                        List<Vector2> route = Stage1TraversalGateTests.FindSurfaceRoute(surfaces, start, target);
                        Assert.NotNull(route, $"Portal {from.Direction}->{to.Direction} dt={dt}; start={start}; target={target}");
                        motor.Teleport(new Vector3(start.x, start.y + .51f)); motor.SetGroundNormal(Vector2.up);
                        Follow(motor, body, route, target, dt);
                        float error = Mathf.Abs(player.transform.position.x - target.x);
                        Assert.LessOrEqual(error, .9f, $"Portal {from.Direction}->{to.Direction} dt={dt}; last={player.transform.position}; error={error}");
                    }
                    foreach (SpawnPointMarker spawn in spawns)
                    foreach (ChunkSocketMarker socket in sockets)
                    {
                        Vector2 start = new(spawn.transform.position.x, spawn.transform.position.y - .51f);
                        Vector2 target = new(socket.EntryMarker.position.x, socket.EntryMarker.position.y - .51f);
                        List<Vector2> route = Stage1TraversalGateTests.FindSurfaceRoute(surfaces, start, target);
                        Assert.NotNull(route, $"Spawn {spawn.name}->{socket.Direction} dt={dt}; start={start}; target={target}");
                        motor.Teleport(spawn.transform.position); motor.SetGroundNormal(Vector2.up);
                        Follow(motor, body, route, target, dt);
                        float error = Mathf.Abs(player.transform.position.x - target.x);
                        Assert.LessOrEqual(error, .9f, $"Spawn {spawn.name}->{socket.Direction} dt={dt}; last={player.transform.position}; error={error}");
                    }
                }
            }
            finally
            {
                Physics2D.simulationMode = previous;
                if (player != null) Object.DestroyImmediate(player);
                if (room != null) Object.DestroyImmediate(room);
            }
        }

        [Test]
        public void EmptyFirstTrial02_OneWay25_LandDropReland()
        {
            GameObject room = null, player = null;
            SimulationMode2D previous = Physics2D.simulationMode;
            try
            {
                Physics2D.simulationMode = SimulationMode2D.Script;
                room = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(EmptyFirstTrial02Path));
                player = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Unit_3001.prefab"));
                KinematicMotor2D motor = player.GetComponent<KinematicMotor2D>();
                Collider2D body = player.GetComponents<Collider2D>().First(x => !x.isTrigger);
                Tilemap platforms = room.GetComponentsInChildren<Tilemap>(true).Single(x => x.name == "Tilemap_Platforms");
                motor.InitMotor();
                List<List<Vector3Int>> regions = TileRegionCells(platforms);
                Assert.AreEqual(25, regions.Count);
                foreach (List<Vector3Int> region in regions)
                {
                    Vector3Int cell = region.OrderBy(x => x.x).ElementAt(region.Count / 2);
                    Vector3 top = platforms.CellToWorld(cell + Vector3Int.up);
                    motor.Teleport(new Vector3(platforms.GetCellCenterWorld(cell).x, top.y + 2f)); motor.SetVelocityY(-5f);
                    Simulate(motor, 90, 1f / 60f);
                    Assert.IsTrue(motor.IsGrounded, $"landing {cell}; last={player.transform.position}");
                    float landed = body.bounds.min.y;
                    motor.PassThroughOneWayPlatformAsync(.12f).Forget(); Simulate(motor, 20, 1f / 60f);
                    Assert.Less(body.bounds.min.y, landed - .25f, $"drop {cell}; last={player.transform.position}");
                    motor.Teleport(new Vector3(platforms.GetCellCenterWorld(cell).x, top.y + 2f)); motor.SetVelocityY(-5f);
                    Simulate(motor, 90, 1f / 60f);
                    Assert.IsTrue(motor.IsGrounded, $"reland {cell}; last={player.transform.position}");
                }
            }
            finally
            {
                Physics2D.simulationMode = previous;
                if (player != null) Object.DestroyImmediate(player);
                if (room != null) Object.DestroyImmediate(room);
            }
        }

        [Test]
        public void GoldenModulesAndTrial01_PreserveSourceAndMeetContracts()
        {
            byte[] sourceBefore = File.ReadAllBytes(Path);
            byte[] sourceMetaBefore = File.ReadAllBytes(Path + ".meta");
            string sourceGuid = AssetDatabase.AssetPathToGUID(Path);

            ModuleChunkBuilder.BuildGoldenModulesAndTrial01();

            CollectionAssert.AreEqual(sourceBefore, File.ReadAllBytes(Path), "source prefab changed");
            CollectionAssert.AreEqual(sourceMetaBefore, File.ReadAllBytes(Path + ".meta"), "source meta changed");
            Assert.AreEqual(sourceGuid, AssetDatabase.AssetPathToGUID(Path));
            GameObject source = AssetDatabase.LoadAssetAtPath<GameObject>(Path);
            Tilemap sourceGround = source.GetComponentsInChildren<Tilemap>(true).Single(x => x.name == "Tilemap_Ground");
            RectInt[] regions =
            {
                new RectInt(36, 0, 10, 8), new RectInt(66, 0, 18, 24),
                new RectInt(0, 13, 12, 20), new RectInt(8, 43, 10, 10),
                new RectInt(46, 32, 14, 12)
            };
            for (int i = 0; i < regions.Length; i++)
            {
                string modulePath = $"Assets/Prefabs/Development/GoldenModules/Module_Golden_{i + 1}u.prefab";
                GameObject module = AssetDatabase.LoadAssetAtPath<GameObject>(modulePath);
                Assert.NotNull(module, modulePath);
                BoxCollider2D bounds = module.GetComponentsInChildren<BoxCollider2D>(true).Single(x => x.name == "ModuleBounds");
                Assert.AreEqual(new Vector2(regions[i].width, regions[i].height), bounds.size);
                Tilemap moduleGround = module.GetComponentsInChildren<Tilemap>(true).Single(x => x.name == "Tilemap_Ground");
                int sourceSolid = 0, moduleSolid = 0, xor = 0;
                for (int y = 0; y < regions[i].height; y++)
                for (int x = 0; x < regions[i].width; x++)
                {
                    bool a = sourceGround.HasTile(new Vector3Int(regions[i].x + x, regions[i].y + y, 0));
                    bool b = moduleGround.HasTile(new Vector3Int(x, y, 0));
                    if (a) sourceSolid++;
                    if (b) moduleSolid++;
                    if (a != b) xor++;
                }
                float area = regions[i].width * regions[i].height;
                Assert.GreaterOrEqual(xor / area, .15f, $"Module {i + 1}u XOR");
                Assert.LessOrEqual(Mathf.Abs(moduleSolid - sourceSolid) / area, .05f, $"Module {i + 1}u occupancy");
            }

            GameObject trial = AssetDatabase.LoadAssetAtPath<GameObject>(GoldenTrialPath);
            Assert.NotNull(trial);
            Assert.AreNotEqual(sourceGuid, AssetDatabase.AssetPathToGUID(GoldenTrialPath));
            Tilemap ground = trial.GetComponentsInChildren<Tilemap>(true).Single(x => x.name == "Tilemap_Ground");
            Tilemap platforms = trial.GetComponentsInChildren<Tilemap>(true).Single(x => x.name == "Tilemap_Platforms");
            Assert.AreEqual(new Vector3Int(84, 60, 1), ground.cellBounds.size);
            int sourceSolidCount = 0, trialSolidCount = 0, trialXor = 0;
            for (int y = 0; y < 60; y++)
            for (int x = 0; x < 84; x++)
            {
                bool a = sourceGround.HasTile(new Vector3Int(x, y, 0));
                bool b = ground.HasTile(new Vector3Int(x, y, 0));
                if (a) sourceSolidCount++;
                if (b) trialSolidCount++;
                if (a != b) trialXor++;
            }
            Assert.GreaterOrEqual(trialXor / 5040f, .15f, "trial XOR");
            Assert.LessOrEqual(Mathf.Abs(sourceSolidCount - trialSolidCount) / 5040f, .05f, "trial occupancy");
            Assert.AreEqual(1, EmptyRegions(ground));
            Assert.IsFalse(TileRegionCells(ground).Any(region => region.Count <= 2), "1-2 cell solid artifact");

            Vector3[] expectedSpawns =
            {
                new Vector3(12f, 48.51f), new Vector3(32f, 36.51f),
                new Vector3(52f, 48.51f), new Vector3(72f, 36.51f)
            };
            Vector3[] spawns = trial.GetComponentsInChildren<SpawnPointMarker>(true)
                .Where(x => x.Type == SpawnType.Monster).OrderBy(x => x.transform.position.x)
                .Select(x => x.transform.position).ToArray();
            CollectionAssert.AreEqual(expectedSpawns, spawns);
            for (int i = 0; i < expectedSpawns.Length; i++)
            {
                Vector3 spawn = expectedSpawns[i];
                int centerX = Mathf.RoundToInt(spawn.x), floorY = Mathf.FloorToInt(spawn.y) - 1;
                int floorStartX = i == 3 ? centerX - 1 : centerX - 2;
                for (int x = floorStartX; x < floorStartX + 4; x++)
                    Assert.IsTrue(ground.HasTile(new Vector3Int(x, floorY, 0)), $"spawn floor {spawn}");
                for (int y = floorY + 1; y <= floorY + 2; y++)
                    Assert.IsFalse(ground.HasTile(new Vector3Int(centerX, y, 0)), $"spawn corridor {spawn}");
            }

            Vector3[] expectedPortals =
            {
                new Vector3(8.5f, 8f), new Vector3(28.5f, 8f),
                new Vector3(55.5f, 8f), new Vector3(75.5f, 8f)
            };
            ChunkSocketDirection[] directions =
            {
                ChunkSocketDirection.West, ChunkSocketDirection.East,
                ChunkSocketDirection.South, ChunkSocketDirection.North
            };
            ChunkSocketMarker[] sockets = trial.GetComponentsInChildren<ChunkSocketMarker>(true);
            Assert.AreEqual(4, sockets.Length);
            for (int i = 0; i < directions.Length; i++)
            {
                ChunkSocketMarker socket = sockets.Single(x => x.Direction == directions[i]);
                Assert.AreEqual(expectedPortals[i], socket.transform.position);
                Assert.AreEqual(expectedPortals[i].y - .49f, socket.EntryMarker.position.y, .001f);
                int centerX = Mathf.FloorToInt(expectedPortals[i].x);
                int floorY = Mathf.FloorToInt(expectedPortals[i].y) - 2;
                for (int x = centerX - 1; x <= centerX + 1; x++)
                    Assert.IsTrue(ground.HasTile(new Vector3Int(x, floorY, 0)), $"portal floor {directions[i]}");
                for (int y = floorY + 1; y <= floorY + 2; y++)
                    Assert.IsFalse(ground.HasTile(new Vector3Int(centerX, y, 0)), $"portal corridor {directions[i]}");
            }
            foreach (Vector3Int cell in platforms.cellBounds.allPositionsWithin)
            {
                if (!platforms.HasTile(cell)) continue;
                for (int x = cell.x - 2; x <= cell.x + 2; x++)
                for (int y = cell.y - 2; y <= cell.y + 4; y++)
                    Assert.IsFalse(ground.HasTile(new Vector3Int(x, y, 0)), $"OneWay clearance {cell}");
            }
        }

        [Test]
        public void Candidate1u_CombatReserved_PreservesSourceAndAppliesReservations()
        {
            byte[] sourceBefore = File.ReadAllBytes(Path);
            byte[] sourceMetaBefore = File.ReadAllBytes(Path + ".meta");

            CollectionAssert.AreEqual(sourceBefore, File.ReadAllBytes(Path), "source prefab changed");
            CollectionAssert.AreEqual(sourceMetaBefore, File.ReadAllBytes(Path + ".meta"), "source meta changed");
            Assert.AreEqual("1ce3dbb99173a9b419f6c6fbe83afe6e", AssetDatabase.AssetPathToGUID(CombatReservedPath));
            Assert.AreNotEqual(AssetDatabase.AssetPathToGUID(Path), AssetDatabase.AssetPathToGUID(CombatReservedPath));
            GameObject source = AssetDatabase.LoadAssetAtPath<GameObject>(Path);
            GameObject reserved = AssetDatabase.LoadAssetAtPath<GameObject>(CombatReservedPath);
            Assert.NotNull(reserved);
            Assert.AreEqual(4, reserved.GetComponentsInChildren<ChunkSocketMarker>(true).Length);

            Tilemap sourcePlatforms = source.GetComponentsInChildren<Tilemap>(true).Single(x => x.name == "Tilemap_Platforms");
            Tilemap reservedPlatforms = reserved.GetComponentsInChildren<Tilemap>(true).Single(x => x.name == "Tilemap_Platforms");
            foreach (Vector3Int cell in sourcePlatforms.cellBounds.allPositionsWithin)
                Assert.AreEqual(sourcePlatforms.HasTile(cell), reservedPlatforms.HasTile(cell), $"OneWay changed at {cell}");

            Tilemap ground = reserved.GetComponentsInChildren<Tilemap>(true).Single(x => x.name == "Tilemap_Ground");
            int[] centers = { 12, 32, 52, 72 };
            Vector3[] spawnPositions = reserved.GetComponentsInChildren<SpawnPointMarker>(true)
                .Where(x => x.Type == SpawnType.Monster).OrderBy(x => x.transform.position.x)
                .Select(x => x.transform.position).ToArray();
            Assert.AreEqual(4, spawnPositions.Length);
            for (int i = 0; i < centers.Length; i++)
            {
                int center = centers[i];
                Assert.AreEqual(new Vector3(center, 50.51f, 0f), spawnPositions[i]);
                for (int x = center - 2; x <= center + 1; x++)
                    Assert.IsTrue(ground.HasTile(new Vector3Int(x, 49, 0)), $"missing S{i + 1} floor ({x},49)");
                for (int x = center - 3; x <= center + 2; x++)
                for (int y = 50; y <= 53; y++)
                    Assert.IsFalse(ground.HasTile(new Vector3Int(x, y, 0)), $"blocked S{i + 1} ({x},{y})");
            }
            Transform[] entries = reserved.GetComponentsInChildren<ChunkSocketMarker>(true).Select(x => x.EntryMarker).ToArray();
            for (int i = 0; i < spawnPositions.Length; i++)
            {
                Assert.Greater(spawnPositions[i].x, 1f);
                Assert.Greater(83f - spawnPositions[i].x, 1f);
                foreach (Transform entry in entries)
                    Assert.Greater(Vector2.Distance(spawnPositions[i], entry.position), 14f, $"Spawn{i + 1}/Entry {entry.name}");
                for (int j = i + 1; j < spawnPositions.Length; j++)
                    Assert.Greater(Vector2.Distance(spawnPositions[i], spawnPositions[j]), 15f, $"Spawn{i + 1}/Spawn{j + 1}");
            }
        }

        [Test]
        public void Candidate1u_CombatReserved_Motor_60And15Fps()
        {
            GameObject room = null, player = null;
            SimulationMode2D previous = Physics2D.simulationMode;
            try
            {
                Physics2D.simulationMode = SimulationMode2D.Script;
                room = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(CombatReservedPath));
                player = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Unit_3001.prefab"));
                KinematicMotor2D motor = player.GetComponent<KinematicMotor2D>();
                Collider2D body = player.GetComponents<Collider2D>().First(x => !x.isTrigger);
                motor.InitMotor();
                List<Vector2> surfaces = Stage1TraversalGateTests.CollectStandableSurfaces(room);
                ChunkSocketMarker[] sockets = room.GetComponentsInChildren<ChunkSocketMarker>(true);
                SpawnPointMarker[] spawns = room.GetComponentsInChildren<SpawnPointMarker>(true).Where(x => x.Type == SpawnType.Monster).ToArray();
                foreach (float dt in new[] { 1f / 60f, 1f / 15f })
                {
                    foreach (ChunkSocketMarker from in sockets)
                    foreach (ChunkSocketMarker to in sockets)
                    {
                        if (from == to) continue;
                        Vector2 start = new(from.EntryMarker.position.x, from.EntryMarker.position.y - .51f);
                        Vector2 target = new(to.EntryMarker.position.x, to.EntryMarker.position.y - .51f);
                        List<Vector2> route = Stage1TraversalGateTests.FindSurfaceRoute(surfaces, start, target);
                        Assert.NotNull(route, $"Portal {from.Direction}->{to.Direction} dt={dt}");
                        motor.Teleport(new Vector3(start.x, start.y + .51f)); motor.SetGroundNormal(Vector2.up);
                        Follow(motor, body, route, target, dt);
                        float error = Mathf.Abs(player.transform.position.x - target.x);
                        Assert.LessOrEqual(error, .9f, $"Portal {from.Direction}->{to.Direction} dt={dt}; last={player.transform.position}; error={error}");
                    }
                    foreach (SpawnPointMarker spawn in spawns)
                    foreach (ChunkSocketMarker socket in sockets)
                    {
                        Vector2 start = spawn.transform.position;
                        Vector2 target = new(socket.EntryMarker.position.x, socket.EntryMarker.position.y - .51f);
                        List<Vector2> route = Stage1TraversalGateTests.FindSurfaceRoute(surfaces, start, target);
                        Assert.NotNull(route, $"Spawn {spawn.name}->{socket.Direction} dt={dt}");
                        motor.Teleport(spawn.transform.position); motor.SetGroundNormal(Vector2.up);
                        Follow(motor, body, route, target, dt);
                        float error = Mathf.Abs(player.transform.position.x - target.x);
                        Assert.LessOrEqual(error, .9f, $"Spawn {spawn.name}->{socket.Direction} dt={dt}; last={player.transform.position}; error={error}");
                    }
                }
            }
            finally
            {
                Physics2D.simulationMode = previous;
                if (player != null) Object.DestroyImmediate(player);
                if (room != null) Object.DestroyImmediate(room);
            }
        }

        [Test]
        public void Candidate1u_CombatReserved_OneWay7_LandDropReland()
        {
            GameObject room = null, player = null;
            SimulationMode2D previous = Physics2D.simulationMode;
            try
            {
                Physics2D.simulationMode = SimulationMode2D.Script;
                room = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(CombatReservedPath));
                player = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Unit_3001.prefab"));
                KinematicMotor2D motor = player.GetComponent<KinematicMotor2D>();
                Collider2D body = player.GetComponents<Collider2D>().First(x => !x.isTrigger);
                Tilemap platforms = room.GetComponentsInChildren<Tilemap>(true).Single(x => x.name == "Tilemap_Platforms");
                motor.InitMotor();
                List<List<Vector3Int>> regions = TileRegionCells(platforms);
                Assert.AreEqual(7, regions.Count);
                foreach (List<Vector3Int> region in regions)
                {
                    Vector3Int cell = region.OrderBy(x => x.x).ElementAt(region.Count / 2);
                    Vector3 top = platforms.CellToWorld(cell + Vector3Int.up);
                    motor.Teleport(new Vector3(platforms.GetCellCenterWorld(cell).x, top.y + 2f)); motor.SetVelocityY(-5f);
                    Simulate(motor, 90, 1f / 60f);
                    Assert.IsTrue(motor.IsGrounded, $"landing {cell}; last={player.transform.position}");
                    float landed = body.bounds.min.y;
                    motor.PassThroughOneWayPlatformAsync(.12f).Forget(); Simulate(motor, 20, 1f / 60f);
                    Assert.Less(body.bounds.min.y, landed - .25f, $"drop {cell}; last={player.transform.position}");
                    motor.Teleport(new Vector3(platforms.GetCellCenterWorld(cell).x, top.y + 2f)); motor.SetVelocityY(-5f);
                    Simulate(motor, 90, 1f / 60f);
                    Assert.IsTrue(motor.IsGrounded, $"reland {cell}; last={player.transform.position}");
                }
            }
            finally
            {
                Physics2D.simulationMode = previous;
                if (player != null) Object.DestroyImmediate(player);
                if (room != null) Object.DestroyImmediate(room);
            }
        }

        [Test]
        public void Candidate1u_StaticContracts()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Path);
            Assert.NotNull(prefab);
            Assert.IsTrue(File.Exists(Path + ".meta"));
            Assert.AreEqual(4, prefab.GetComponentsInChildren<SpawnPointMarker>(true).Count(x => x.Type == SpawnType.Monster));
            Assert.AreEqual(4, prefab.GetComponentsInChildren<ChunkSocketMarker>(true).Length);

            Tilemap ground = prefab.GetComponentsInChildren<Tilemap>(true).Single(x => x.name == "Tilemap_Ground");
            Tilemap platforms = prefab.GetComponentsInChildren<Tilemap>(true).Single(x => x.name == "Tilemap_Platforms");
            Assert.AreEqual(1, EmptyRegions(ground), "isolated cavity/trap empty region");
            Assert.AreEqual(3, TileRegions(platforms));
            BoundsInt bounds = ground.cellBounds;
            for (int x = bounds.xMin; x < bounds.xMax; x++)
            {
                Assert.IsTrue(ground.HasTile(new Vector3Int(x, bounds.yMin, 0)), $"open perimeter ({x},{bounds.yMin})");
                Assert.IsTrue(ground.HasTile(new Vector3Int(x, bounds.yMax - 1, 0)), $"open perimeter ({x},{bounds.yMax - 1})");
            }
            for (int y = bounds.yMin; y < bounds.yMax; y++)
            {
                Assert.IsTrue(ground.HasTile(new Vector3Int(bounds.xMin, y, 0)), $"open perimeter ({bounds.xMin},{y})");
                Assert.IsTrue(ground.HasTile(new Vector3Int(bounds.xMax - 1, y, 0)), $"open perimeter ({bounds.xMax - 1},{y})");
            }
            foreach (Vector3Int cell in platforms.cellBounds.allPositionsWithin)
                if (platforms.HasTile(cell)) Assert.IsFalse(ground.HasTile(cell), $"Ground/OneWay contact {cell}");

            int narrowCandidates = 0;
            foreach (Vector3Int cell in ground.cellBounds.allPositionsWithin)
            {
                if (ground.HasTile(cell)) continue;
                bool horizontal = ground.HasTile(cell + Vector3Int.left) && ground.HasTile(cell + Vector3Int.right);
                bool vertical = ground.HasTile(cell + Vector3Int.up) && ground.HasTile(cell + Vector3Int.down);
                if (horizontal || vertical) narrowCandidates++;
            }
            TestContext.Progress.WriteLine($"Candidate1u static narrow candidates={narrowCandidates}; motor-classified false positives are reported by dynamic gates.");

            string[] roots = { "Assets/AddressableAssetsData", "Assets/Datas", "Assets/Scripts" };
            string name = System.IO.Path.GetFileNameWithoutExtension(Path);
            foreach (string root in roots)
            foreach (string file in Directory.GetFiles(root, "*", SearchOption.AllDirectories))
                if (!file.EndsWith(".meta")) StringAssert.DoesNotContain(name, File.ReadAllText(file), file);
        }

        [Test]
        public void Candidate1u_PortalOrderedPairs_60Fps() =>
            new Stage1TraversalGateTests().Room_AllOrderedSocketPairs_ReplayWithActualUnit3001Motor(Path);

        [Test]
        public void Candidate1u_SpawnsReachEveryPortal_At15Fps()
        {
            GameObject room = null, player = null;
            SimulationMode2D previous = Physics2D.simulationMode;
            try
            {
                Physics2D.simulationMode = SimulationMode2D.Script;
                room = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Path));
                player = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Unit_3001.prefab"));
                KinematicMotor2D motor = player.GetComponent<KinematicMotor2D>();
                Collider2D body = player.GetComponents<Collider2D>().First(x => !x.isTrigger);
                motor.InitMotor();
                List<Vector2> surfaces = Stage1TraversalGateTests.CollectStandableSurfaces(room);
                SpawnPointMarker[] spawns = room.GetComponentsInChildren<SpawnPointMarker>(true).Where(x => x.Type == SpawnType.Monster).ToArray();
                ChunkSocketMarker[] sockets = room.GetComponentsInChildren<ChunkSocketMarker>(true);
                foreach (SpawnPointMarker spawn in spawns)
                foreach (ChunkSocketMarker socket in sockets)
                {
                    motor.Teleport(spawn.transform.position);
                    motor.SetGroundNormal(Vector2.up);
                    Vector2 start = new(player.transform.position.x, body.bounds.min.y);
                    Vector2 target = new(socket.EntryMarker.position.x, socket.EntryMarker.position.y - .51f);
                    List<Vector2> route = Stage1TraversalGateTests.FindSurfaceRoute(surfaces, start, target);
                    Assert.NotNull(route, $"spawn {spawn.name} -> {socket.Direction}");
                    Follow(motor, body, route, target, 1f / 15f);
                    Assert.LessOrEqual(Mathf.Abs(player.transform.position.x - target.x), .9f, $"spawn {spawn.name} -> {socket.Direction}");
                    Assert.GreaterOrEqual(body.bounds.min.y, -.05f, $"fell: {spawn.name} -> {socket.Direction}");
                }
            }
            finally
            {
                Physics2D.simulationMode = previous;
                if (player != null) Object.DestroyImmediate(player);
                if (room != null) Object.DestroyImmediate(room);
            }
        }

        [Test]
        public void Candidate1u_OneWayThree_LandDropReland()
        {
            GameObject room = null, player = null;
            SimulationMode2D previous = Physics2D.simulationMode;
            try
            {
                Physics2D.simulationMode = SimulationMode2D.Script;
                room = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Path));
                player = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Unit_3001.prefab"));
                KinematicMotor2D motor = player.GetComponent<KinematicMotor2D>();
                Collider2D body = player.GetComponents<Collider2D>().First(x => !x.isTrigger);
                motor.InitMotor();
                Tilemap platforms = room.GetComponentsInChildren<Tilemap>(true).Single(x => x.name == "Tilemap_Platforms");
                List<List<Vector3Int>> regions = TileRegionCells(platforms);
                Assert.AreEqual(3, regions.Count);
                foreach (List<Vector3Int> region in regions)
                {
                    Vector3Int cell = region.OrderByDescending(x => x.y).First();
                    Vector3 top = platforms.CellToWorld(cell + Vector3Int.up);
                    motor.Teleport(new Vector3(platforms.GetCellCenterWorld(cell).x, top.y + 2f));
                    motor.SetVelocityY(-5f);
                    Simulate(motor, 90, 1f / 60f);
                    Assert.IsTrue(motor.IsGrounded, $"landing {cell}");
                    float landedBottom = body.bounds.min.y;
                    motor.PassThroughOneWayPlatformAsync(.12f).Forget();
                    Simulate(motor, 20, 1f / 60f);
                    Assert.Less(body.bounds.min.y, landedBottom - .25f, $"drop {cell}");
                    motor.Teleport(new Vector3(platforms.GetCellCenterWorld(cell).x, top.y + 2f));
                    motor.SetVelocityY(-5f);
                    Simulate(motor, 90, 1f / 60f);
                    Assert.IsTrue(motor.IsGrounded, $"reland {cell}");
                }
            }
            finally
            {
                Physics2D.simulationMode = previous;
                if (player != null) Object.DestroyImmediate(player);
                if (room != null) Object.DestroyImmediate(room);
            }
        }

        [Test]
        public void Candidate1u_CentralTraverse_LeftRight_60And15Fps()
        {
            GameObject room = null, player = null;
            SimulationMode2D previous = Physics2D.simulationMode;
            try
            {
                Physics2D.simulationMode = SimulationMode2D.Script;
                room = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Path));
                player = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Unit_3001.prefab"));
                KinematicMotor2D motor = player.GetComponent<KinematicMotor2D>();
                Collider2D body = player.GetComponents<Collider2D>().First(x => !x.isTrigger);
                motor.InitMotor();
                List<Vector2> surfaces = Stage1TraversalGateTests.CollectStandableSurfaces(room);
                Vector2 left = new(42.5f, 42f), right = new(72.5f, 33f);
                foreach (float dt in new[] { 1f / 60f, 1f / 15f })
                foreach ((Vector2 start, Vector2 target, string label) in new[]
                {
                    (left, right, "left->right"), (right, left, "right->left")
                })
                {
                    List<Vector2> route = Stage1TraversalGateTests.FindSurfaceRoute(surfaces, start, target);
                    Assert.NotNull(route, $"{label} dt={dt}");
                    motor.Teleport(new Vector3(start.x, start.y + .51f));
                    motor.SetGroundNormal(Vector2.up);
                    Follow(motor, body, route, target, dt);
                    float error = Mathf.Abs(player.transform.position.x - target.x);
                    Assert.LessOrEqual(error, .9f, $"{label} dt={dt}; last={player.transform.position}; error={error}");
                    Assert.GreaterOrEqual(body.bounds.min.y, -.05f, $"{label} dt={dt}; fell at {player.transform.position}");
                    foreach (Collider2D obstacle in room.GetComponentsInChildren<Collider2D>(true).Where(x => !x.isTrigger))
                        Assert.IsFalse(Physics2D.Distance(body, obstacle).isOverlapped, $"{label} dt={dt}; penetrated {obstacle.name} at {player.transform.position}");
                }
            }
            finally
            {
                Physics2D.simulationMode = previous;
                if (player != null) Object.DestroyImmediate(player);
                if (room != null) Object.DestroyImmediate(room);
            }
        }

        [Test]
        public void Candidate1u_CentralOneWay_B1ToB4_LandDropReland()
        {
            GameObject room = null, player = null;
            SimulationMode2D previous = Physics2D.simulationMode;
            try
            {
                Physics2D.simulationMode = SimulationMode2D.Script;
                room = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Path));
                player = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Unit_3001.prefab"));
                KinematicMotor2D motor = player.GetComponent<KinematicMotor2D>();
                Collider2D body = player.GetComponents<Collider2D>().First(x => !x.isTrigger);
                Tilemap platforms = room.GetComponentsInChildren<Tilemap>(true).Single(x => x.name == "Tilemap_Platforms");
                motor.InitMotor();
                Vector3Int[] cells =
                {
                    new(49, 39, 0), new(55, 37, 0), new(61, 35, 0), new(67, 33, 0)
                };
                for (int i = 0; i < cells.Length; i++)
                {
                    Vector3Int cell = cells[i];
                    Assert.IsTrue(platforms.HasTile(cell), $"B{i + 1} missing {cell}");
                    Vector3 top = platforms.CellToWorld(cell + Vector3Int.up);
                    motor.Teleport(new Vector3(platforms.GetCellCenterWorld(cell).x, top.y + 2f));
                    motor.SetVelocityY(-5f);
                    Simulate(motor, 90, 1f / 60f);
                    Assert.IsTrue(motor.IsGrounded, $"B{i + 1} landing {cell}; last={player.transform.position}");
                    float landedBottom = body.bounds.min.y;
                    motor.PassThroughOneWayPlatformAsync(.12f).Forget();
                    Simulate(motor, 20, 1f / 60f);
                    Assert.Less(body.bounds.min.y, landedBottom - .25f, $"B{i + 1} drop {cell}; last={player.transform.position}");
                    motor.Teleport(new Vector3(platforms.GetCellCenterWorld(cell).x, top.y + 2f));
                    motor.SetVelocityY(-5f);
                    Simulate(motor, 90, 1f / 60f);
                    Assert.IsTrue(motor.IsGrounded, $"B{i + 1} reland {cell}; last={player.transform.position}");
                }
            }
            finally
            {
                Physics2D.simulationMode = previous;
                if (player != null) Object.DestroyImmediate(player);
                if (room != null) Object.DestroyImmediate(room);
            }
        }

        private static void Follow(KinematicMotor2D motor, Collider2D body, List<Vector2> route, Vector2 target, float dt)
        {
            foreach (Vector2 waypoint in route.Skip(1))
            {
                Vector2 feet = new(motor.transform.position.x, body.bounds.min.y);
                if ((waypoint.y > feet.y + .2f || Mathf.Abs(waypoint.x - feet.x) > 1.15f) && motor.IsGrounded)
                { motor.SetVelocityY(11.5f); motor.SetJumpHeld(true); }
                for (int step = 0; step < 45 && Mathf.Abs(motor.transform.position.x - waypoint.x) > .3f; step++)
                {
                    motor.SetTargetVelocityX(Mathf.Sign(waypoint.x - motor.transform.position.x) * 6f);
                    if (motor.WallDir != 0 && motor.IsGrounded) motor.SetVelocityY(11.5f);
                    motor.SimulateStep(dt); Physics2D.Simulate(dt);
                    Assert.GreaterOrEqual(body.bounds.min.y, -.05f, $"15 FPS fall toward {target}");
                }
            }
            motor.SetTargetVelocityX(0f);
        }

        private static void Simulate(KinematicMotor2D motor, int steps, float dt)
        { for (int i = 0; i < steps; i++) { motor.SimulateStep(dt); Physics2D.Simulate(dt); } }

        private static int EmptyRegions(Tilemap ground)
        {
            BoundsInt bounds = ground.cellBounds; var seen = new HashSet<Vector3Int>(); int count = 0;
            foreach (Vector3Int start in bounds.allPositionsWithin)
            {
                if (ground.HasTile(start) || !seen.Add(start)) continue;
                count++; var queue = new Queue<Vector3Int>(); queue.Enqueue(start);
                while (queue.Count > 0)
                {
                    Vector3Int p = queue.Dequeue();
                    foreach (Vector3Int d in Dirs)
                    { Vector3Int n = p + d; if (bounds.Contains(n) && !ground.HasTile(n) && seen.Add(n)) queue.Enqueue(n); }
                }
            }
            return count;
        }

        private static Vector3Int[] TileCells(Tilemap tilemap)
        {
            var result = new List<Vector3Int>();
            foreach (Vector3Int cell in tilemap.cellBounds.allPositionsWithin)
                if (tilemap.HasTile(cell)) result.Add(cell);
            return result.ToArray();
        }

        private static int TileRegions(Tilemap tilemap) => TileRegionCells(tilemap).Count;
        private static List<List<Vector3Int>> TileRegionCells(Tilemap tilemap)
        {
            var result = new List<List<Vector3Int>>(); var seen = new HashSet<Vector3Int>();
            foreach (Vector3Int start in tilemap.cellBounds.allPositionsWithin)
            {
                if (!tilemap.HasTile(start) || !seen.Add(start)) continue;
                var region = new List<Vector3Int>(); var queue = new Queue<Vector3Int>(); queue.Enqueue(start);
                while (queue.Count > 0)
                {
                    Vector3Int p = queue.Dequeue(); region.Add(p);
                    foreach (Vector3Int d in Dirs)
                    { Vector3Int n = p + d; if (tilemap.HasTile(n) && seen.Add(n)) queue.Enqueue(n); }
                }
                result.Add(region);
            }
            return result;
        }

        private static readonly Vector3Int[] Dirs = { Vector3Int.left, Vector3Int.right, Vector3Int.up, Vector3Int.down };
    }
}
#endif
