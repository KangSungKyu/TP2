using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace QA.Tests
{
    public sealed class PhaseADoorPortalTests
    {
        private static readonly string[] PhaseAPrefabs =
        {
            "Assets/Prefabs/Development/Tilemap_Room_PhaseA_1x1.prefab",
            "Assets/Prefabs/Development/Tilemap_Room_PhaseA_1x2.prefab",
            "Assets/Prefabs/Development/Tilemap_Room_PhaseA_2x1.prefab"
        };

        [Test]
        public void IntraRoomZone_RoundTripPreservesSlotAndRoomGeneration()
        {
            var owner = new GameObject("PhaseA_StageManager");
            try
            {
                StageManager stage = owner.AddComponent<StageManager>();
                var run = new StageRunData
                {
                    CurrentSlotIdx = 3,
                    Slots = new[] { new ChunkSlotData { SlotIdx = 3, ChunkResourceIdx = 1080u, Visited = true } }
                };
                typeof(StageManager).GetProperty(nameof(StageManager.CurrentRun),
                    BindingFlags.Instance | BindingFlags.Public).SetValue(stage, run);
                uint roomGeneration = stage.RoomGeneration;

                Assert.IsTrue(stage.TryEnterIntraRoomZone(1080u, 0u, 10u, 1u));
                Assert.AreEqual((byte)3, run.CurrentSlotIdx);
                Assert.AreEqual(roomGeneration, stage.RoomGeneration);
                Assert.IsTrue(stage.GetZoneState(3, 10u).Visited);
                Assert.IsTrue(stage.TryClearIntraRoomZone(10u));
                Assert.IsTrue(stage.TryEnterIntraRoomZone(1080u, 10u, 0u, 1u));
                Assert.AreEqual(0u, stage.CurrentZoneIdx);
                Assert.IsFalse(stage.TryEnterIntraRoomZone(9999u, 0u, 10u, 1u));
                Assert.AreEqual(0u, stage.CurrentZoneIdx);
            }
            finally { Object.DestroyImmediate(owner); }
        }

        [Test]
        public void PhaseA_GeneratorHasThreeModuleIndependentDimensions()
        {
            string source = File.ReadAllText("Assets/Editor/ModuleChunkBuilder.cs");
            int start = source.IndexOf("private static void BuildPhaseAEmptyFirstPrototype");
            int end = source.IndexOf("[MenuItem(\"TP2/Development/Rebuild Candidate", start);
            string phaseA = source.Substring(start, end - start);
            StringAssert.Contains("BuildPhaseAEmptyFirstPrototype(84, 60", source);
            StringAssert.Contains("BuildPhaseAEmptyFirstPrototype(84, 120", source);
            StringAssert.Contains("BuildPhaseAEmptyFirstPrototype(168, 60", source);
            StringAssert.DoesNotContain("ModuleTemplates", phaseA);
            StringAssert.DoesNotContain("Build12x12ModulePrefabs", phaseA);

            (int width, int height, int pairs)[] contracts =
            {
                (84, 60, 1), (84, 120, 2), (168, 60, 2)
            };
            foreach ((int width, int height, int pairs) contract in contracts)
            {
                Assert.AreEqual(contract.pairs,
                    ModuleChunkBuilder.GetPhaseAPortalPairCount(contract.width, contract.height));
                float groundClearance = ModuleChunkBuilder.GetPhaseAPlayerGroundClearance();
                Vector2[] spawns = ModuleChunkBuilder.GetPhaseASpawnPositions(
                    ModuleChunkBuilder.GetPhaseARooms(contract.width, contract.height), groundClearance);
                Assert.AreEqual(.51f, groundClearance, .001f);
                Assert.AreEqual(4, spawns.Length);
                Assert.AreEqual(4, new System.Collections.Generic.HashSet<Vector2>(spawns).Count);
                for (int i = 0; i < spawns.Length; i++)
                for (int j = i + 1; j < spawns.Length; j++)
                    Assert.GreaterOrEqual(Vector2.Distance(spawns[i], spawns[j]), 15f,
                        $"{contract.width}x{contract.height} Spawn {i}/{j}");

                var solid = new bool[contract.width, contract.height];
                var protectedNavigation = new bool[contract.width, contract.height];
                for (int y = 0; y < contract.height; y++)
                for (int x = 0; x < contract.width; x++) solid[x, y] = true;
                int centerX = contract.width / 2, centerY = contract.height / 2;
                solid[centerX, centerY] = false;
                for (int x = centerX - 2; x <= centerX + 1; x++) solid[x, centerY + 2] = false;
                for (int y = centerY - 1; y <= centerY + 1; y++)
                {
                    solid[centerX + 4, y] = false;
                    protectedNavigation[centerX + 4, y] = true;
                }
                ModuleChunkBuilder.NormalizePhaseANarrowGaps(solid, protectedNavigation, 3);
                Assert.IsTrue(solid[centerX, centerY], $"{contract.width}x{contract.height} 1-cell narrow");
                for (int x = centerX - 2; x <= centerX + 1; x++)
                    Assert.IsTrue(solid[x, centerY + 2], $"{contract.width}x{contract.height} 4-cell narrow");
                for (int y = centerY - 1; y <= centerY + 1; y++)
                    Assert.IsFalse(solid[centerX + 4, y], $"{contract.width}x{contract.height} protected landing");

                RectInt[] rooms = ModuleChunkBuilder.GetPhaseARooms(contract.width, contract.height);
                var doorSolid = new bool[contract.width, contract.height];
                var doorEmpty = new bool[contract.width, contract.height];
                var doorSupport = new bool[contract.width, contract.height];
                var doorNavigation = new bool[contract.width, contract.height];
                foreach (RectInt room in rooms)
                {
                    int doorX = Mathf.RoundToInt(room.center.x);
                    ModuleChunkBuilder.ProtectPhaseASurface(doorSolid, doorEmpty, doorSupport, doorNavigation,
                        doorX, room.yMin);
                    for (int x = doorX - 1; x <= doorX + 1; x++)
                    {
                        Assert.IsTrue(doorSolid[x, room.yMin], $"{contract.width}x{contract.height} Door support");
                        for (int y = room.yMin + 1; y <= room.yMin + 4; y++)
                            Assert.IsFalse(doorSolid[x, y], $"{contract.width}x{contract.height} Door headroom");
                    }
                }
                var graph = new bool[contract.width, contract.height];
                var protectedGraph = new bool[contract.width, contract.height];
                for (int y = 0; y < contract.height; y++)
                for (int x = 0; x < contract.width; x++) graph[x, y] = true;
                foreach (RectInt room in rooms)
                    for (int y = room.yMin + 1; y < room.yMax; y++)
                    for (int x = room.xMin; x < room.xMax; x++) graph[x, y] = false;
                RectInt[] annex = contract.pairs == 1
                    ? new[] { rooms[3] }
                    : new[] { rooms[3], rooms[2] };
                ModuleChunkBuilder.ClosePhaseAMainGraph(graph, protectedGraph, annex, 3);
                foreach (RectInt room in rooms.Take(2))
                {
                    Assert.IsTrue(graph[room.xMin, room.yMin - 1],
                        $"{contract.width}x{contract.height} closure must not carve a 1-cell boundary pocket");
                    Assert.IsTrue(graph[room.xMin, room.yMin - 2],
                        $"{contract.width}x{contract.height} closure must not carve a 2-cell boundary pocket");
                }
                Assert.AreEqual(1, ModuleChunkBuilder.CountPhaseAEmptyComponents(graph, annex, 3),
                    $"{contract.width}x{contract.height} main graph");
                Assert.AreEqual(1 + contract.pairs,
                    ModuleChunkBuilder.CountPhaseAEmptyComponents(graph, null, 3),
                    $"{contract.width}x{contract.height} main + annex graphs");

                int[] mainRooms = contract.pairs == 1 ? new[] { 0, 1, 2 } : new[] { 0, 1 };
                var surfaces = new System.Collections.Generic.List<Vector2>();
                foreach (int roomIndex in mainRooms)
                    surfaces.Add(new Vector2(Mathf.RoundToInt(rooms[roomIndex].center.x), rooms[roomIndex].yMin + 1));
                for (int i = 1; i < mainRooms.Length; i++)
                {
                    Vector2 from = surfaces[0], to = surfaces[i];
                    if (!Mathf.Approximately(from.y, to.y)) continue;
                    for (float x = Mathf.Min(from.x, to.x) + 4f; x < Mathf.Max(from.x, to.x); x += 4f)
                        surfaces.Add(new Vector2(x, from.y));
                }
                if (mainRooms.Length > 2)
                {
                    Vector2 from = surfaces[1], to = surfaces[2];
                    for (float x = Mathf.Min(from.x, to.x) + 4f; x < Mathf.Max(from.x, to.x); x += 4f)
                        surfaces.Add(new Vector2(x, from.y));
                }
                foreach (Vector3Int run in ModuleChunkBuilder.GetPhaseAVerticalPlatformRuns(rooms, mainRooms))
                    surfaces.Add(new Vector2(run.x + 1, run.y + 1));
                foreach (Vector2 startSurface in surfaces.Take(mainRooms.Length))
                foreach (Vector2 target in surfaces.Take(mainRooms.Length))
                    Assert.NotNull(Stage1TraversalGateTests.FindSurfaceRoute(surfaces, startSurface, target),
                        $"{contract.width}x{contract.height} motor-reachable main surface {startSurface}->{target}");
            }
            StringAssert.Contains("Mathf.RoundToInt(room.center.x), room.yMin", phaseA);
            StringAssert.Contains("triggerX, room.yMin", phaseA);
            StringAssert.Contains("landingX, room.yMin", phaseA);
            StringAssert.Contains("floorY + 4", source);
        }

        [Test]
        public void PhaseA_LandingResolver_UsesGeneratedColliderGeometry()
        {
            GameObject root = null, probeObject = null;
            try
            {
                root = new GameObject("PhaseA_LandingGeometry_QA", typeof(Grid));
                var terrainObject = new GameObject("Tilemap_Ground", typeof(Tilemap), typeof(TilemapRenderer),
                    typeof(TilemapCollider2D), typeof(Rigidbody2D), typeof(CompositeCollider2D));
                terrainObject.transform.SetParent(root.transform, false);
                terrainObject.GetComponent<Rigidbody2D>().bodyType = RigidbodyType2D.Static;
                terrainObject.GetComponent<TilemapCollider2D>().compositeOperation = Collider2D.CompositeOperation.Merge;
                Tilemap ground = terrainObject.GetComponent<Tilemap>();
                Tile tile = AssetDatabase.LoadAssetAtPath<Tile>("Assets/Textures/Environment/Tiles/Tile_Ground.asset");
                Assert.NotNull(tile);
                for (int x = -1; x <= 1; x++) ground.SetTile(new Vector3Int(x, 0, 0), tile);
                terrainObject.GetComponent<TilemapCollider2D>().ProcessTilemapChanges();
                terrainObject.GetComponent<CompositeCollider2D>().GenerateGeometry();

                Vector3 resolved = ModuleChunkBuilder.ResolvePhaseALandingPosition(ground, 0f, .5f);
                GameObject player = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Unit_3001.prefab");
                CapsuleCollider2D source = player.GetComponent<CapsuleCollider2D>();
                probeObject = new GameObject("PhaseA_LandingAssert", typeof(CapsuleCollider2D));
                CapsuleCollider2D probe = probeObject.GetComponent<CapsuleCollider2D>();
                probe.size = source.size;
                probe.offset = source.offset;
                probe.direction = source.direction;
                probeObject.transform.localScale = source.transform.lossyScale;
                probeObject.transform.position = resolved;
                Physics2D.SyncTransforms();
                ColliderDistance2D distance = Physics2D.Distance(probe,
                    terrainObject.GetComponent<CompositeCollider2D>());
                Assert.IsTrue(distance.isValid);
                Assert.IsFalse(distance.isOverlapped);
                Assert.GreaterOrEqual(distance.distance, player.GetComponent<KinematicMotor2D>().SkinWidth);
                Assert.Greater(resolved.y, 1.5f, "Resolver must remeasure after overlap and contact distance 0.");
            }
            finally
            {
                if (probeObject != null) Object.DestroyImmediate(probeObject);
                if (root != null) Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void DoorAndPortalShareEdgeInputWithoutAutomaticEntry()
        {
            string door = File.ReadAllText("Assets/Scripts/Gameplay/RoomDoorPortal.cs");
            string portal = File.ReadAllText("Assets/Scripts/Gameplay/IntraRoomPortal.cs");
            StringAssert.Contains("wasPressedThisFrame", door);
            StringAssert.Contains("RoomDoorPortal.WasInteractionPressedThisFrame()", portal);
            StringAssert.Contains("!player.Motor.IsGrounded", door);
            StringAssert.Contains("!player.Motor.IsGrounded", portal);
            StringAssert.DoesNotContain("Time.time +", portal);
        }

        public async Task PhaseA_ThreePrefabs_MainTargetsReplayAt60And15Fps()
        {
            foreach (string path in PhaseAPrefabs)
            foreach (float deltaTime in new[] { 1f / 60f, 1f / 15f })
                await AssertMainTargetsReachable(path, deltaTime);
        }

        [Test]
        public void PhaseA_FivePortalPairs_RoundTripPreservesRoomStateAndLanding()
        {
            foreach (string path in PhaseAPrefabs) AssertPortalPairs(path);
        }

        [Test]
        public void PhaseA_LifecycleAndIsolationContracts_AreExplicit()
        {
            string portal = File.ReadAllText("Assets/Scripts/Gameplay/IntraRoomPortal.cs");
            string generator = File.ReadAllText("Assets/Editor/ModuleChunkBuilder.cs");
            StringAssert.Contains("Monster.ActiveMonsters.Count > 0", portal);
            StringAssert.Contains("DespawnAllProjectiles()", portal);
            StringAssert.Contains("ClearAllActiveEffects()", portal);
            StringAssert.Contains("requiresTriggerExit", portal);
            foreach (string path in PhaseAPrefabs)
            {
                string yaml = File.ReadAllText(path);
                StringAssert.DoesNotContain("Module_", yaml, path);
                Assert.IsEmpty(AssetDatabase.GetLabels(AssetDatabase.LoadAssetAtPath<GameObject>(path)), path);
            }
            int start = generator.IndexOf("private static void BuildPhaseAEmptyFirstPrototype");
            int end = generator.IndexOf("[MenuItem(\"TP2/Development/Rebuild Candidate", start);
            StringAssert.DoesNotContain("ModuleTemplates", generator.Substring(start, end - start));
        }

        private static async Task AssertMainTargetsReachable(string path, float deltaTime)
        {
            GameObject room = null, player = null;
            SimulationMode2D previous = Physics2D.simulationMode;
            try
            {
                Physics2D.simulationMode = SimulationMode2D.Script;
                room = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(path));
                player = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Unit_3001.prefab"));
                KinematicMotor2D motor = player.GetComponent<KinematicMotor2D>();
                Collider2D body = player.GetComponents<Collider2D>().First(collider => !collider.isTrigger);
                motor.InitMotor();
                var surfaces = Stage1TraversalGateTests.CollectStandableSurfaces(room);
                ChunkSocketMarker[] doors = room.GetComponentsInChildren<ChunkSocketMarker>(true);
                SpawnPointMarker[] spawns = room.GetComponentsInChildren<SpawnPointMarker>(true);
                IntraRoomPortal[] portals = room.GetComponentsInChildren<IntraRoomPortal>(true)
                    .Where(candidate => candidate.name.Contains("MainToAnnex")).ToArray();
                Assert.AreEqual(4, doors.Length, path);
                Assert.AreEqual(4, spawns.Length, path);

                BoxCollider2D cameraBounds = room.transform.Find("CameraBounds").GetComponent<BoxCollider2D>();
                int width = Mathf.RoundToInt(cameraBounds.size.x), height = Mathf.RoundToInt(cameraBounds.size.y);
                RectInt[] generatedRooms = ModuleChunkBuilder.GetPhaseARooms(width, height);
                int[] mainRooms = ModuleChunkBuilder.GetPhaseAPortalPairCount(width, height) == 1
                    ? new[] { 0, 1, 2 }
                    : new[] { 0, 1 };
                bool IsMain(Vector2 position) => mainRooms.Any(index => generatedRooms[index].Contains(
                    new Vector2Int(Mathf.FloorToInt(position.x), Mathf.FloorToInt(position.y))));
                Vector2 start = doors.Select(door => (Vector2)door.EntryMarker.position).First(IsMain);
                foreach (Vector2 target in doors.Select(door => (Vector2)door.EntryMarker.position).Where(IsMain)
                    .Concat(spawns.Select(spawn => (Vector2)spawn.transform.position).Where(IsMain))
                    .Concat(portals.Select(portal => (Vector2)portal.transform.position)))
                    await ReplayRoute(path, room, player, motor, body, surfaces, start, target, deltaTime);
            }
            finally
            {
                Physics2D.simulationMode = previous;
                if (player != null) Object.DestroyImmediate(player);
                if (room != null) Object.DestroyImmediate(room);
            }
        }

        private static async Task ReplayRoute(string path, GameObject room, GameObject player, KinematicMotor2D motor,
            Collider2D body, System.Collections.Generic.List<Vector2> surfaces, Vector2 start, Vector2 target,
            float deltaTime)
        {
            motor.Teleport(start);
            motor.SetGroundNormal(Vector2.up);
            Physics2D.Simulate(deltaTime);
            var route = Stage1TraversalGateTests.FindSurfaceRoute(surfaces,
                new Vector2(start.x, start.y - 0.51f), new Vector2(target.x, target.y - 0.51f));
            Assert.NotNull(route, $"{path} {start}->{target} dt={deltaTime}");
            float jumpForce = (float)typeof(Player).GetField("jumpForce",
                BindingFlags.Instance | BindingFlags.NonPublic).GetValue(player.GetComponent<Player>());
            foreach (Vector2 waypoint in route.Skip(1))
            {
                Vector2 feet = new Vector2(player.transform.position.x, body.bounds.min.y);
                if (waypoint.y < feet.y - 0.3f && motor.IsGrounded)
                {
                    Physics2D.SyncTransforms();
                    UniTask passThrough = motor.PassThroughOneWayPlatformAsync(0.12f);
                    for (int step = 0; step < 4 && !motor.IsPassingThrough; step++)
                        await UniTask.NextFrame();
                    Assert.IsTrue(motor.IsPassingThrough,
                        $"{path}: grounded lower route has no OneWay contact before {waypoint}");
                    for (int step = 0; step < 120 && motor.IsPassingThrough; step++)
                    {
                        motor.SetTargetVelocityX(Mathf.Sign(waypoint.x - player.transform.position.x) * 6f);
                        motor.SimulateStep(deltaTime);
                        Physics2D.Simulate(deltaTime);
                        await UniTask.NextFrame();
                    }
                    await passThrough;
                    Assert.IsFalse(motor.IsPassingThrough, $"{path}: pass-through did not rearm before {waypoint}");
                }
                else if ((waypoint.y > feet.y + 0.2f || Mathf.Abs(waypoint.x - feet.x) > 1.15f) && motor.IsGrounded)
                {
                    motor.SetVelocityY(jumpForce);
                    motor.SetJumpHeld(true);
                }
                bool Reached() => Mathf.Abs(player.transform.position.x - waypoint.x) <= 0.3f &&
                    Mathf.Abs(body.bounds.min.y - waypoint.y) <= 0.3f && motor.IsGrounded;
                for (int step = 0; step < 120 && !Reached(); step++)
                {
                    motor.SetTargetVelocityX(Mathf.Sign(waypoint.x - player.transform.position.x) * 6f);
                    motor.SimulateStep(deltaTime);
                    Physics2D.Simulate(deltaTime);
                }
                motor.SetJumpHeld(false);
                Assert.IsTrue(Reached(),
                    $"{path} stalled before {waypoint}: position={player.transform.position}, " +
                    $"feet={body.bounds.min.y}, velocity={motor.Velocity}, passThrough={motor.IsPassingThrough}");
            }
            motor.SetTargetVelocityX(0f);
            Assert.LessOrEqual(Mathf.Abs(player.transform.position.x - target.x), 0.75f,
                $"{path} target={target} last={player.transform.position} dt={deltaTime}");
            AssertNoInvalidPenetration(path, room, body);
        }

        private static void AssertPortalPairs(string path)
        {
            GameObject room = null, player = null, stageObject = null;
            Player previousPlayer = Player.Instance;
            StageManager previousStage = StageManager.Instance;
            Monster[] existingActiveMonsters = Monster.ActiveMonsters.Where(monster => monster != null).ToArray();
            typeof(Player).GetProperty("Instance", BindingFlags.Static | BindingFlags.Public)
                .GetSetMethod(true).Invoke(null, new object[] { null });
            SetSingletonInstance<StageManager>(null);
            Monster.ActiveMonsters.Clear();
            try
            {
                room = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(path));
                player = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Unit_3001.prefab"));
                stageObject = new GameObject("PhaseA_PortalStage");
                StageManager stage = stageObject.AddComponent<StageManager>();
                SetSingletonInstance(stage);
                typeof(Player).GetProperty("Instance", BindingFlags.Static | BindingFlags.Public)
                    .GetSetMethod(true).Invoke(null, new object[] { player.GetComponent<Player>() });
                var run = new StageRunData
                {
                    CurrentSlotIdx = 3,
                    Slots = new[] { new ChunkSlotData { SlotIdx = 3, ChunkResourceIdx = 1080u, Visited = true } }
                };
                typeof(StageManager).GetProperty(nameof(StageManager.CurrentRun), BindingFlags.Instance | BindingFlags.Public)
                    .SetValue(stage, run);
                KinematicMotor2D motor = player.GetComponent<KinematicMotor2D>();
                Collider2D body = player.GetComponents<Collider2D>().First(collider => !collider.isTrigger);
                typeof(UnitBase).GetField("motor", BindingFlags.Instance | BindingFlags.NonPublic)
                    .SetValue(player.GetComponent<Player>(), motor);
                motor.InitMotor();
                uint roomGeneration = stage.RoomGeneration;
                IntraRoomPortal[] all = room.GetComponentsInChildren<IntraRoomPortal>(true);

                foreach (var pair in all.GroupBy(portal => GetPortalUInt(portal, "portalPairIdx")))
                {
                    Assert.AreEqual(2, pair.Count(), $"{path} pair {pair.Key}");
                    IntraRoomPortal outward = pair.Single(portal => GetPortalUInt(portal, "sourceZoneIdx") == 0u);
                    IntraRoomPortal inward = pair.Single(portal => GetPortalUInt(portal, "destinationZoneIdx") == 0u);
                    uint before = stage.ZoneGeneration;
                    ConfigurePortal(outward, 1080u);
                    ConfigurePortal(inward, 1080u);
                    motor.Teleport(outward.transform.position);
                    motor.SetGroundNormal(Vector2.up);
                    Assert.AreSame(stage, StageManager.Instance, "Fixture StageManager singleton");
                    Assert.AreSame(player.GetComponent<Player>(), Player.Instance, "Fixture Player singleton");
                    Assert.AreSame(motor, Player.Instance.Motor, "Fixture Player motor binding");
                    Assert.IsTrue(motor.IsGrounded, "Portal requires grounded player");
                    Assert.Zero(Monster.ActiveMonsters.Count, "Portal requires combat inactive");
                    Assert.AreEqual(0u, stage.CurrentZoneIdx, "Outward portal requires main zone");
                    Assert.AreNotEqual(0u, GetPortalUInt(outward, "portalIdx"));
                    Assert.AreNotEqual(0u, GetPortalUInt(outward, "chunkResourceIdx"));
                    Assert.AreNotEqual(0u, GetPortalUInt(outward, "portalPairIdx"));
                    Assert.NotNull(typeof(IntraRoomPortal).GetField("destinationEndpoint",
                        BindingFlags.Instance | BindingFlags.NonPublic).GetValue(outward));
                    Assert.IsTrue(run.TryGetSlot(run.CurrentSlotIdx, out ChunkSlotData currentSlot));
                    Assert.AreEqual(1080u, currentSlot.ChunkResourceIdx);
                    Assert.IsTrue(outward.TryTeleport(), $"{path} pair {pair.Key} outward");
                    SyncEditModeBody(player);
                    AssertNoInvalidPenetration(path, room, body);
                    Assert.IsTrue(stage.GetZoneState(3, GetPortalUInt(outward, "destinationZoneIdx")).Visited);
                    Assert.IsTrue(inward.TryTeleport(), $"{path} pair {pair.Key} inward");
                    SyncEditModeBody(player);
                    AssertNoInvalidPenetration(path, room, body);
                    Assert.AreEqual(before + 2, stage.ZoneGeneration);
                    Assert.AreEqual(roomGeneration, stage.RoomGeneration);
                    Assert.AreEqual((byte)3, run.CurrentSlotIdx);
                }
            }
            finally
            {
                Monster.ActiveMonsters.Clear();
                foreach (Monster monster in existingActiveMonsters)
                    if (monster != null && monster.isActiveAndEnabled) Monster.ActiveMonsters.Add(monster);
                if (stageObject != null) Object.DestroyImmediate(stageObject);
                if (player != null) Object.DestroyImmediate(player);
                if (room != null) Object.DestroyImmediate(room);
                SetSingletonInstance(previousStage);
                typeof(Player).GetProperty("Instance", BindingFlags.Static | BindingFlags.Public)
                    .GetSetMethod(true).Invoke(null, new object[] { previousPlayer });
            }
        }

        private static void SetSingletonInstance<T>(T component) where T : MonoBehaviour =>
            typeof(Singleton<T>).GetField("<Instance>k__BackingField", BindingFlags.Static | BindingFlags.NonPublic)
                .SetValue(null, component);

        private static void SyncEditModeBody(GameObject player)
        {
            Rigidbody2D body = player.GetComponent<Rigidbody2D>();
            player.transform.position = body.position;
            Physics2D.SyncTransforms();
        }

        private static uint GetPortalUInt(IntraRoomPortal portal, string field) =>
            (uint)typeof(IntraRoomPortal).GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(portal);

        private static void ConfigurePortal(IntraRoomPortal portal, uint chunkIdx)
        {
            Transform endpoint = (Transform)typeof(IntraRoomPortal)
                .GetField("destinationEndpoint", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(portal);
            portal.Configure(GetPortalUInt(portal, "portalIdx"), chunkIdx, GetPortalUInt(portal, "sourceZoneIdx"),
                GetPortalUInt(portal, "destinationZoneIdx"), GetPortalUInt(portal, "portalPairIdx"), endpoint);
        }

        private static void AssertNoInvalidPenetration(string path, GameObject room, Collider2D body)
        {
            Physics2D.SyncTransforms();
            KinematicMotor2D motor = body.GetComponent<KinematicMotor2D>();
            Assert.NotNull(motor);
            foreach (Collider2D obstacle in room.GetComponentsInChildren<Collider2D>(true).Where(collider => !collider.isTrigger))
            {
                ColliderDistance2D distance = Physics2D.Distance(body, obstacle);
                bool supportsTop = obstacle.name.Contains("Ground") ||
                    obstacle.GetComponent<PlatformEffector2D>() != null ||
                    obstacle.GetComponent<OneWayPlatformPassThrough>() != null;
                bool safeGroundContact = supportsTop && distance.isValid && distance.isOverlapped &&
                    distance.distance >= -motor.SkinWidth && -distance.normal.y >= motor.MinGroundNormalY;
                Assert.IsTrue(!distance.isOverlapped || safeGroundContact,
                    $"{path}: penetration with {obstacle.name} at {body.transform.position}; " +
                    $"depth={distance.distance}, normal={distance.normal}, pointA={distance.pointA}, pointB={distance.pointB}");
            }
            BoxCollider2D bounds = room.transform.Find("CameraBounds")?.GetComponent<BoxCollider2D>();
            Assert.NotNull(bounds, path);
            Assert.GreaterOrEqual(body.bounds.min.x, bounds.bounds.min.x - 0.01f, path);
            Assert.LessOrEqual(body.bounds.max.x, bounds.bounds.max.x + 0.01f, path);
            Assert.GreaterOrEqual(body.bounds.min.y, bounds.bounds.min.y - 0.01f, path);
            Assert.LessOrEqual(body.bounds.max.y, bounds.bounds.max.y + 0.01f, path);
        }
    }
}
