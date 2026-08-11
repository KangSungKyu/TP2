using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.Tilemaps;

namespace QA.Tests
{
    public class Stage1TraversalGateTests
    {
        private static readonly string[] RoomPaths =
        {
            "Assets/Prefabs/Rooms/Prefab_1040.prefab", "Assets/Prefabs/Rooms/Prefab_1041.prefab",
            "Assets/Prefabs/Rooms/Prefab_1042.prefab", "Assets/Prefabs/Rooms/Room_11050.prefab",
            "Assets/Prefabs/Rooms/Room_11051.prefab", "Assets/Prefabs/Rooms/Room_11052.prefab",
            "Assets/Prefabs/Rooms/Room_11053.prefab", "Assets/Prefabs/Rooms/Room_11056.prefab",
            "Assets/Prefabs/Rooms/Room_11057.prefab", "Assets/Prefabs/Rooms/Room_11061.prefab",
            "Assets/Prefabs/Rooms/Room_11063.prefab"
        };

        [UnityTest]
        public IEnumerator Unit3001_DropsThroughAndRelandsTwice_FromBothDirections()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Unit_3001.prefab");
            Assert.NotNull(prefab);
            GameObject player = Object.Instantiate(prefab);
            GameObject platform = new GameObject("OneWay_Traversal_QA");
            try
            {
                var motor = player.GetComponent<KinematicMotor2D>();
                var playerCollider = player.GetComponents<Collider2D>().First(collider => !collider.isTrigger);
                Assert.NotNull(motor);

                platform.layer = LayerMask.NameToLayer("OneWayPlatform");
                var platformCollider = platform.AddComponent<BoxCollider2D>();
                platformCollider.size = new Vector2(8f, 0.5f);
                platformCollider.usedByEffector = true;
                var effector = platform.AddComponent<PlatformEffector2D>();
                effector.useOneWay = true;
                effector.surfaceArc = 180f;
                platform.AddComponent<OneWayPlatformPassThrough>();

                motor.InitMotor();
                for (int cycle = 0; cycle < 2; cycle++)
                {
                    motor.Teleport(new Vector3(cycle == 0 ? -2f : 2f, 2.5f));
                    motor.SetTargetVelocityX(cycle == 0 ? 2f : -2f);
                    for (int frame = 0; frame < 90 && !motor.IsGrounded; frame++)
                    {
                        motor.SimulateStep(Time.fixedDeltaTime);
                        Physics2D.SyncTransforms();
                    }
                    Assert.IsTrue(motor.IsGrounded, $"cycle {cycle}: initial one-way landing failed");

                    motor.PassThroughOneWayPlatformAsync(0.5f).Forget();
                    for (int frame = 0; frame < 90 && playerCollider.bounds.max.y >= platformCollider.bounds.max.y; frame++)
                    {
                        motor.SimulateStep(Time.fixedDeltaTime);
                        Physics2D.SyncTransforms();
                    }
                    Assert.Less(playerCollider.bounds.max.y, platformCollider.bounds.max.y,
                        $"cycle {cycle}: collider did not fully leave the platform");
                    motor.Teleport(player.transform.position);
                    Assert.IsFalse(motor.IsPassingThrough, $"cycle {cycle}: pass-through did not finish");

                    motor.Teleport(new Vector3(cycle == 0 ? 2f : -2f, 2.5f));
                    motor.SetVelocityY(8f);
                    for (int frame = 0; frame < 150 && !motor.IsGrounded; frame++)
                    {
                        motor.SimulateStep(Time.fixedDeltaTime);
                        Physics2D.SyncTransforms();
                    }
                    Assert.IsTrue(motor.IsGrounded, $"cycle {cycle}: same-platform relanding failed");
                    Assert.GreaterOrEqual(playerCollider.bounds.min.y, platformCollider.bounds.max.y - motor.SkinWidth * 2f);
                }
                yield return null;
            }
            finally
            {
                Object.DestroyImmediate(platform);
                Object.DestroyImmediate(player);
            }
        }

        [Test]
        public void GeneratedAssets_PreserveStage1StaticContracts()
        {
            var templates = (Dictionary<string, string[]>)typeof(ModuleChunkBuilder)
                .GetField("ModuleTemplates", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
            string[] generatedModules = templates.Keys.Select(name => $"Assets/Prefabs/Modules/{name}.prefab").ToArray();
            Assert.AreEqual(20, generatedModules.Length, "The authoritative generator must produce 20 module templates.");
            foreach (string path in generatedModules) AssertModulePhysics(path);
            foreach (string path in RoomPaths) AssertRoomContracts(path);
        }

        [Test]
        public void Stage1Graph_200Seeds_HasReciprocalLoadableResources()
        {
            for (uint seed = 0; seed < 200; seed++)
            {
                StageRunData run = Stage1RunGenerator.Generate(seed);
                Assert.IsTrue(Stage1RunGenerator.Validate(run), $"seed {seed}");
                foreach (ChunkSlotData slot in run.Slots)
                {
                    string resourceName = slot.ChunkResourceIdx < 11000u
                        ? $"Prefab_{slot.ChunkResourceIdx}"
                        : $"Room_{slot.ChunkResourceIdx}";
                    Assert.IsNotEmpty(AssetDatabase.FindAssets($"{resourceName} t:Prefab",
                        new[] { "Assets/Prefabs/Rooms" }), $"seed {seed}, resource {slot.ChunkResourceIdx}");
                    foreach (ChunkSocketDirection direction in System.Enum.GetValues(typeof(ChunkSocketDirection)))
                    {
                        byte bit = (byte)(1 << (int)direction);
                        if ((slot.ConnectionMask & bit) == 0) continue;
                        int offset = direction == ChunkSocketDirection.North ? -run.Columns
                            : direction == ChunkSocketDirection.East ? 1
                            : direction == ChunkSocketDirection.South ? run.Columns : -1;
                        Assert.IsTrue(run.TryGetSlot((byte)(slot.SlotIdx + offset), out ChunkSlotData neighbor),
                            $"seed {seed}, slot {slot.SlotIdx}, {direction}");
                        ChunkSocketDirection reverse = (ChunkSocketDirection)(((int)direction + 2) % 4);
                        Assert.AreNotEqual(0, neighbor.ConnectionMask & (1 << (int)reverse),
                            $"seed {seed}, {slot.SlotIdx}->{neighbor.SlotIdx}");
                    }
                }
            }
        }

        [TestCaseSource(nameof(RoomPaths))]
        public void Room_AllOrderedSocketPairs_ReplayWithActualUnit3001Motor(string roomPath)
        {
            GameObject room = null;
            GameObject player = null;
            SimulationMode2D previousMode = Physics2D.simulationMode;
            try
            {
                Physics2D.simulationMode = SimulationMode2D.Script;
                room = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(roomPath));
                player = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Unit_3001.prefab"));
                var motor = player.GetComponent<KinematicMotor2D>();
                var playerCollider = player.GetComponents<Collider2D>().First(candidate => !candidate.isTrigger);
                Assert.NotNull(motor, roomPath);
                motor.InitMotor();

                ChunkSocketMarker[] sockets = room.GetComponentsInChildren<ChunkSocketMarker>(true);
                Assert.AreEqual(4, sockets.Length, roomPath);
                List<Vector2> surfaces = CollectStandableSurfaces(room);
                int directedAssertions = 0;
                foreach (ChunkSocketMarker from in sockets)
                foreach (ChunkSocketMarker to in sockets)
                {
                    if (from == to) continue;
                    Vector3 start = from.EntryMarker.position;
                    Vector3 target = to.EntryMarker.position;
                    motor.Teleport(start);
                    motor.SetGroundNormal(Vector2.up);
                    Physics2D.Simulate(1f / 60f);
                    List<Vector2> route = FindSurfaceRoute(surfaces,
                        new Vector2(start.x, start.y - 0.51f), new Vector2(target.x, target.y - 0.51f));
                    Assert.NotNull(route, $"{roomPath} {from.Direction}->{to.Direction}: no standable-surface route");

                    foreach (Vector2 waypoint in route.Skip(1))
                    {
                        Vector2 currentFeet = new Vector2(player.transform.position.x, playerCollider.bounds.min.y);
                        bool requiresJump = waypoint.y > currentFeet.y + 0.2f ||
                                            Mathf.Abs(waypoint.x - currentFeet.x) > 1.15f;
                        if (requiresJump && motor.IsGrounded)
                        {
                            motor.SetVelocityY(11.5f);
                            motor.SetJumpHeld(true);
                        }
                        for (int step = 0; step < 120 && Mathf.Abs(player.transform.position.x - waypoint.x) > 0.3f; step++)
                        {
                            float dx = waypoint.x - player.transform.position.x;
                            motor.SetTargetVelocityX(Mathf.Sign(dx) * 6f);
                            if (motor.WallDir != 0 && motor.IsGrounded) motor.SetVelocityY(11.5f);
                            motor.SimulateStep(1f / 60f);
                            Physics2D.Simulate(1f / 60f);
                            if (playerCollider.bounds.max.y < -5f) break;
                        }
                    }
                    motor.SetTargetVelocityX(0f);

                    Assert.LessOrEqual(Mathf.Abs(player.transform.position.x - target.x), 0.75f,
                        $"{roomPath} {from.Direction}->{to.Direction}; last={player.transform.position}");
                    Assert.GreaterOrEqual(playerCollider.bounds.min.y, -0.05f,
                        $"{roomPath} {from.Direction}->{to.Direction}; fell out at {player.transform.position}");
                    directedAssertions++;
                }
                Assert.AreEqual(12, directedAssertions, roomPath);
            }
            finally
            {
                Physics2D.simulationMode = previousMode;
                if (player != null) Object.DestroyImmediate(player);
                if (room != null) Object.DestroyImmediate(room);
            }
        }

        private static List<Vector2> CollectStandableSurfaces(GameObject room)
        {
            var result = new List<Vector2>();
            foreach (Tilemap tilemap in room.GetComponentsInChildren<Tilemap>(true)
                .Where(candidate => candidate.GetComponent<TilemapCollider2D>() != null))
            foreach (Vector3Int cell in tilemap.cellBounds.allPositionsWithin)
            {
                if (!tilemap.HasTile(cell) || tilemap.HasTile(cell + Vector3Int.up)) continue;
                Vector3 center = tilemap.GetCellCenterWorld(cell);
                Vector3 top = tilemap.CellToWorld(cell + Vector3Int.up);
                result.Add(new Vector2(center.x, top.y));
            }
            return result;
        }

        private static List<Vector2> FindSurfaceRoute(List<Vector2> nodes, Vector2 start, Vector2 target)
        {
            if (nodes.Count == 0) return null;
            int startIndex = Enumerable.Range(0, nodes.Count).OrderBy(i => Vector2.SqrMagnitude(nodes[i] - start)).First();
            int targetIndex = Enumerable.Range(0, nodes.Count).OrderBy(i => Vector2.SqrMagnitude(nodes[i] - target)).First();
            var previous = Enumerable.Repeat(-1, nodes.Count).ToArray();
            var queue = new Queue<int>();
            previous[startIndex] = startIndex;
            queue.Enqueue(startIndex);
            while (queue.Count > 0 && previous[targetIndex] < 0)
            {
                int current = queue.Dequeue();
                for (int i = 0; i < nodes.Count; i++)
                {
                    if (previous[i] >= 0) continue;
                    float dx = Mathf.Abs(nodes[i].x - nodes[current].x);
                    float dy = nodes[i].y - nodes[current].y;
                    if (dx > 4f || dy > 2.25f || dy < -6f) continue;
                    previous[i] = current;
                    queue.Enqueue(i);
                }
            }
            if (previous[targetIndex] < 0) return null;
            var route = new List<Vector2>();
            for (int at = targetIndex; ; at = previous[at])
            {
                route.Add(nodes[at]);
                if (at == startIndex) break;
            }
            route.Reverse();
            return route;
        }

        private static void AssertModulePhysics(string path)
        {
            GameObject module = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            Assert.NotNull(module, path);
            Assert.NotNull(module.GetComponentInChildren<TilemapCollider2D>(true), path);
            foreach (TilemapCollider2D collider in module.GetComponentsInChildren<TilemapCollider2D>(true)
                .Where(candidate => candidate.GetComponent<PlatformEffector2D>() != null))
            {
                Assert.AreEqual(LayerMask.NameToLayer("OneWayPlatform"), collider.gameObject.layer, path);
                Assert.IsTrue(collider.usedByEffector, path);
                Assert.NotNull(collider.GetComponent<OneWayPlatformPassThrough>(), path);
            }
        }

        private static void AssertRoomContracts(string path)
        {
            GameObject room = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            Assert.NotNull(room, path);
            ChunkSocketMarker[] sockets = room.GetComponentsInChildren<ChunkSocketMarker>(true);
            Assert.AreEqual(4, sockets.Length, path);
            Assert.AreEqual(12, sockets.Length * (sockets.Length - 1), $"{path}: directed socket-pair count");
            Assert.IsFalse(sockets.Any(socket => socket.EntryMarker == null), path);
            var bounds = room.transform.Find("CameraBounds")?.GetComponent<BoxCollider2D>();
            Assert.NotNull(bounds, path);
            Assert.AreEqual(new Vector2(60f, 30f), bounds.size, path);
            foreach (TilemapCollider2D collider in room.GetComponentsInChildren<TilemapCollider2D>(true)
                .Where(candidate => candidate.GetComponent<PlatformEffector2D>() != null))
            {
                Assert.AreEqual(LayerMask.NameToLayer("OneWayPlatform"), collider.gameObject.layer, path);
                Assert.IsTrue(collider.usedByEffector, path);
                Assert.NotNull(collider.GetComponent<OneWayPlatformPassThrough>(), path);
            }
        }
    }
}
