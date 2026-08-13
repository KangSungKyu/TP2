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

        [UnityTest]
        public IEnumerator DropThrough_IgnoresOnlyUpperOneWay_AndRelandsOnLowerTwice()
        {
            GameObject player = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Unit_3001.prefab"));
            GameObject upper = CreateOneWayPlatform("UpperOneWay", 2f);
            GameObject lower = CreateOneWayPlatform("LowerOneWay", -2f);
            SimulationMode2D previousMode = Physics2D.simulationMode;
            try
            {
                Physics2D.simulationMode = SimulationMode2D.Script;
                KinematicMotor2D motor = player.GetComponent<KinematicMotor2D>();
                Collider2D playerCollider = player.GetComponents<Collider2D>().First(candidate => !candidate.isTrigger);
                Collider2D upperCollider = upper.GetComponent<Collider2D>();
                Collider2D lowerCollider = lower.GetComponent<Collider2D>();
                motor.InitMotor();

                for (int cycle = 0; cycle < 2; cycle++)
                {
                    motor.Teleport(new Vector3(0f, upperCollider.bounds.max.y + playerCollider.bounds.size.y));
                    for (int step = 0; step < 120 && !motor.IsGrounded; step++)
                    {
                        motor.SimulateStep(Time.fixedDeltaTime);
                        Physics2D.Simulate(Time.fixedDeltaTime);
                    }
                    Assert.IsTrue(motor.IsGrounded, $"cycle {cycle}: upper platform landing failed");
                    motor.PassThroughOneWayPlatformAsync().Forget();
                    for (int step = 0; step < 120 && !motor.IsGrounded; step++)
                    {
                        motor.SimulateStep(Time.fixedDeltaTime);
                        Physics2D.Simulate(Time.fixedDeltaTime);
                    }

                    Assert.IsTrue(motor.IsGrounded, $"cycle {cycle}: lower platform landing failed");
                    Assert.GreaterOrEqual(playerCollider.bounds.min.y, lowerCollider.bounds.max.y - motor.SkinWidth * 2f);
                    Assert.Less(playerCollider.bounds.min.y, upperCollider.bounds.max.y);
                    yield return null;
                }
            }
            finally
            {
                Physics2D.simulationMode = previousMode;
                Object.DestroyImmediate(lower);
                Object.DestroyImmediate(upper);
                Object.DestroyImmediate(player);
            }
        }

        [UnityTest]
        public IEnumerator DropThrough_DestroyedPhysicsCollider_CancelsWithoutMissingReference()
        {
            GameObject player = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Unit_3001.prefab"));
            GameObject platform = CreateOneWayPlatform("DestroyedColliderOneWay", 0f);
            try
            {
                KinematicMotor2D motor = player.GetComponent<KinematicMotor2D>();
                Collider2D playerCollider = player.GetComponents<Collider2D>().First(candidate => !candidate.isTrigger);
                motor.InitMotor();
                motor.Teleport(new Vector3(0f, platform.GetComponent<Collider2D>().bounds.max.y + playerCollider.bounds.extents.y));
                Physics2D.SyncTransforms();
                motor.SimulateStep(Time.fixedDeltaTime);

                motor.PassThroughOneWayPlatformAsync().Forget();
                Object.DestroyImmediate(player);
                yield return null;
            }
            finally
            {
                Object.DestroyImmediate(platform);
                if (player != null) Object.DestroyImmediate(player);
            }
        }

        [Test]
        public void Player_DropThroughRequiresDownAndJumpKeyDownWhileGrounded()
        {
            MethodInfo predicate = typeof(Player).GetMethod("ShouldStartDropThrough", BindingFlags.Static | BindingFlags.NonPublic);
            Assert.NotNull(predicate);
            Assert.IsFalse((bool)predicate.Invoke(null, new object[] { false, false, true }), "No input");
            Assert.IsFalse((bool)predicate.Invoke(null, new object[] { false, true, true }), "Jump only");
            Assert.IsFalse((bool)predicate.Invoke(null, new object[] { true, false, true }), "Down only or horizontal movement");
            Assert.IsFalse((bool)predicate.Invoke(null, new object[] { true, true, false }), "Falling or coyote-only state");
            Assert.IsTrue((bool)predicate.Invoke(null, new object[] { true, true, true }), "Down + Jump KeyDown");
        }

        [Test]
        public void Room11053_DiagonalOneWay_NoInputHoldsEverySupportFor300FixedSteps()
        {
            GameObject room = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Rooms/Room_11053.prefab"));
            GameObject player = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Unit_3001.prefab"));
            SimulationMode2D previousMode = Physics2D.simulationMode;
            try
            {
                Physics2D.simulationMode = SimulationMode2D.Script;
                Tilemap platforms = room.GetComponentsInChildren<Tilemap>(true)
                    .First(tilemap => tilemap.GetComponent<PlatformEffector2D>() != null);
                KinematicMotor2D motor = player.GetComponent<KinematicMotor2D>();
                Collider2D playerCollider = player.GetComponents<Collider2D>().First(candidate => !candidate.isTrigger);
                motor.InitMotor();
                var supports = new List<Vector3Int>();
                foreach (Vector3Int cell in platforms.cellBounds.allPositionsWithin)
                    if (cell.x >= 11 && cell.x <= 19 && cell.y <= 3 &&
                        platforms.HasTile(cell) && !platforms.HasTile(cell + Vector3Int.up))
                        supports.Add(cell);
                Assert.IsNotEmpty(supports);

                foreach (Vector3Int cell in supports)
                {
                    Vector3 surface = platforms.CellToWorld(cell + Vector3Int.up);
                    Vector3 center = platforms.GetCellCenterWorld(cell);
                    float bodyY = player.transform.position.y + surface.y + motor.SkinWidth - playerCollider.bounds.min.y;
                    motor.Teleport(new Vector3(center.x, bodyY, 0f));
                    Physics2D.SyncTransforms();
                    for (int settle = 0; settle < 10 && !motor.IsGrounded; settle++)
                    {
                        motor.SimulateStep(Time.fixedDeltaTime);
                        Physics2D.Simulate(Time.fixedDeltaTime);
                    }
                    Assert.IsTrue(motor.IsGrounded, $"{cell}: initial support not acquired");

                    motor.SetTargetVelocityX(0f);
                    int generation = (int)typeof(KinematicMotor2D)
                        .GetField("passThroughGeneration", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(motor);

                    for (int step = 0; step < 300; step++)
                    {
                        motor.SimulateStep(Time.fixedDeltaTime);
                        Physics2D.Simulate(Time.fixedDeltaTime);
                        Assert.IsFalse(motor.IsPassingThrough, $"{cell}: no-input pass-through");
                        Assert.AreEqual(generation, (int)typeof(KinematicMotor2D)
                            .GetField("passThroughGeneration", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(motor));
                        Assert.GreaterOrEqual(playerCollider.bounds.min.y, surface.y - motor.SkinWidth * 2f,
                            $"{cell}: lost one-way support at step {step}");
                    }
                }
            }
            finally
            {
                Physics2D.simulationMode = previousMode;
                Object.DestroyImmediate(player);
                Object.DestroyImmediate(room);
            }
        }

        [Test]
        public void Room11053_DiagonalOneWay_ActualMotorAscendsAndReturns()
        {
            GameObject room = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Rooms/Room_11053.prefab"));
            GameObject playerObject = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Unit_3001.prefab"));
            SimulationMode2D previousMode = Physics2D.simulationMode;
            try
            {
                Physics2D.simulationMode = SimulationMode2D.Script;
                Tilemap platforms = room.GetComponentsInChildren<Tilemap>(true)
                    .First(tilemap => tilemap.GetComponent<PlatformEffector2D>() != null);
                Player player = playerObject.GetComponent<Player>();
                KinematicMotor2D motor = playerObject.GetComponent<KinematicMotor2D>();
                Collider2D collider = playerObject.GetComponents<Collider2D>().First(candidate => !candidate.isTrigger);
                float jumpVelocity = (float)typeof(Player).GetField("jumpForce", BindingFlags.Instance | BindingFlags.NonPublic)
                    .GetValue(player);
                Vector3Int[] route =
                {
                    new Vector3Int(11, 1), new Vector3Int(14, 2), new Vector3Int(17, 3),
                    new Vector3Int(14, 2), new Vector3Int(11, 1)
                };
                motor.InitMotor();

                Vector3 firstSurface = platforms.CellToWorld(route[0] + Vector3Int.up);
                Vector3 firstCenter = platforms.GetCellCenterWorld(route[0]);
                motor.Teleport(new Vector3(firstCenter.x,
                    playerObject.transform.position.y + firstSurface.y + motor.SkinWidth - collider.bounds.min.y, 0f));
                Physics2D.SyncTransforms();

                for (int index = 1; index < route.Length; index++)
                {
                    Vector3 targetCenter = platforms.GetCellCenterWorld(route[index]);
                    Vector3 targetSurface = platforms.CellToWorld(route[index] + Vector3Int.up);
                    motor.SetVelocityY(jumpVelocity);
                    motor.SetTargetVelocityX(Mathf.Sign(targetCenter.x - playerObject.transform.position.x) * player.Speed);
                    for (int step = 0; step < 180 && Mathf.Abs(playerObject.transform.position.x - targetCenter.x) > 0.2f; step++)
                    {
                        motor.SimulateStep(Time.fixedDeltaTime);
                        Physics2D.Simulate(Time.fixedDeltaTime);
                        Assert.IsFalse(motor.IsPassingThrough, $"route {index}: implicit pass-through");
                    }
                    motor.SetTargetVelocityX(0f);
                    for (int step = 0; step < 180 && !motor.IsGrounded; step++)
                    {
                        motor.SimulateStep(Time.fixedDeltaTime);
                        Physics2D.Simulate(Time.fixedDeltaTime);
                    }
                    Assert.IsTrue(motor.IsGrounded, $"route {index}: failed to land");
                    Assert.GreaterOrEqual(collider.bounds.min.y, targetSurface.y - motor.SkinWidth * 2f,
                        $"route {index}: fell through diagonal support");
                    int generation = (int)typeof(KinematicMotor2D)
                        .GetField("passThroughGeneration", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(motor);
                    for (int rest = 0; rest < 300; rest++)
                    {
                        motor.SimulateStep(Time.fixedDeltaTime);
                        Physics2D.Simulate(Time.fixedDeltaTime);
                        Assert.IsTrue(motor.IsGrounded, $"route {index}: support lost without input at rest step {rest}");
                        Assert.AreEqual(generation, (int)typeof(KinematicMotor2D)
                            .GetField("passThroughGeneration", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(motor));
                    }
                }
            }
            finally
            {
                Physics2D.simulationMode = previousMode;
                Object.DestroyImmediate(playerObject);
                Object.DestroyImmediate(room);
            }
        }

        [Test]
        public void Room11053_WallJumpArc_LandsOnOneWayFromBothSidesWithoutPassThrough()
        {
            GameObject room = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Rooms/Room_11053.prefab"));
            SimulationMode2D previousMode = Physics2D.simulationMode;
            try
            {
                Physics2D.simulationMode = SimulationMode2D.Script;
                foreach (int side in new[] { -1, 1 })
                foreach (bool shortJump in new[] { false, true })
                {
                    GameObject playerObject = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Unit_3001.prefab"));
                    try
                    {
                        KinematicMotor2D motor = playerObject.GetComponent<KinematicMotor2D>();
                        Collider2D collider = playerObject.GetComponents<Collider2D>().First(candidate => !candidate.isTrigger);
                        motor.InitMotor();
                        motor.Teleport(new Vector3(side < 0 ? 19.4f : 15.6f, 7f));
                        Physics2D.SyncTransforms();
                        motor.SetTargetVelocityX(side * 9.5f);
                        motor.SetVelocityY(12.5f);
                        motor.SetJumpHeld(!shortJump);
                        int generation = (int)typeof(KinematicMotor2D)
                            .GetField("passThroughGeneration", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(motor);

                        for (int step = 0; step < 180 && !motor.IsGrounded; step++)
                        {
                            if (shortJump && step == 2) motor.SetVelocityY(motor.Velocity.y * 0.4f);
                            if (step >= 9) motor.SetTargetVelocityX(Mathf.Sign(17.75f - playerObject.transform.position.x) * 6f);
                            motor.SimulateStep(Time.fixedDeltaTime);
                            Physics2D.Simulate(Time.fixedDeltaTime);
                            Physics2D.SyncTransforms();
                        }

                        Assert.IsTrue(motor.IsGrounded, $"side {side}, short {shortJump}: failed to land");
                        Assert.GreaterOrEqual(collider.bounds.min.y, 7f - motor.SkinWidth * 2f);
                        Assert.AreEqual(generation, (int)typeof(KinematicMotor2D)
                            .GetField("passThroughGeneration", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(motor));
                    }
                    finally
                    {
                        Object.DestroyImmediate(playerObject);
                    }
                }
            }
            finally
            {
                Physics2D.simulationMode = previousMode;
                Object.DestroyImmediate(room);
            }
        }

        [Test]
        public void MonsterMotor_HorizontalRoomBounds_PreventOuterWallEscape()
        {
            var monster = new GameObject("MonsterBoundaryMotor", typeof(Rigidbody2D), typeof(BoxCollider2D), typeof(KinematicMotor2D));
            try
            {
                KinematicMotor2D motor = monster.GetComponent<KinematicMotor2D>();
                Collider2D collider = monster.GetComponent<Collider2D>();
                Bounds roomBounds = new Bounds(Vector3.zero, new Vector3(12f, 8f, 0f));
                motor.InitMotor();
                motor.SetHorizontalMovementBounds(roomBounds);
                motor.Teleport(Vector3.zero);
                motor.SetTargetVelocityX(motor.MaxFallSpeed);

                for (int step = 0; step < 60; step++) motor.SimulateStep(Time.fixedDeltaTime);

                Assert.LessOrEqual(collider.bounds.max.x, roomBounds.max.x + motor.SkinWidth);
                Assert.GreaterOrEqual(collider.bounds.min.x, roomBounds.min.x - motor.SkinWidth);
            }
            finally
            {
                Object.DestroyImmediate(monster);
            }
        }

        [TestCaseSource(nameof(RoomPaths))]
        public void Room_MonsterMotor_LongRunStaysInsideAuthoritativeBounds(string roomPath)
        {
            GameObject room = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(roomPath));
            var monster = new GameObject("MonsterBoundsLongRun", typeof(Rigidbody2D), typeof(BoxCollider2D), typeof(KinematicMotor2D));
            try
            {
                var resolver = typeof(UnitSpawner).GetMethod("ResolveMovementBounds", BindingFlags.Static | BindingFlags.NonPublic);
                Bounds? roomBounds = (Bounds?)resolver.Invoke(null, new object[] { room });
                Assert.IsTrue(roomBounds.HasValue, roomPath);
                KinematicMotor2D motor = monster.GetComponent<KinematicMotor2D>();
                Collider2D collider = monster.GetComponent<Collider2D>();
                motor.InitMotor();
                motor.SetHorizontalMovementBounds(roomBounds.Value);

                foreach (float direction in new[] { -1f, 1f })
                {
                    motor.Teleport(roomBounds.Value.center);
                    motor.SetTargetVelocityX(direction * motor.MaxFallSpeed);
                    for (int step = 0; step < 600; step++) motor.SimulateStep(Time.fixedDeltaTime);
                    Assert.GreaterOrEqual(collider.bounds.min.x, roomBounds.Value.min.x - motor.SkinWidth, roomPath);
                    Assert.LessOrEqual(collider.bounds.max.x, roomBounds.Value.max.x + motor.SkinWidth, roomPath);
                }
            }
            finally
            {
                Object.DestroyImmediate(monster);
                Object.DestroyImmediate(room);
            }
        }

        private static GameObject CreateOneWayPlatform(string name, float y)
        {
            var platform = new GameObject(name);
            platform.layer = LayerMask.NameToLayer("OneWayPlatform");
            platform.transform.position = new Vector3(0f, y, 0f);
            var collider = platform.AddComponent<BoxCollider2D>();
            collider.size = new Vector2(8f, 0.5f);
            collider.usedByEffector = true;
            var effector = platform.AddComponent<PlatformEffector2D>();
            effector.useOneWay = true;
            effector.surfaceArc = 180f;
            platform.AddComponent<OneWayPlatformPassThrough>();
            return platform;
        }

        [Test]
        public void GeneratedAssets_PreserveStage1StaticContracts()
        {
            var templates = (Dictionary<string, string[]>)typeof(ModuleChunkBuilder)
                .GetField("ModuleTemplates", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
            string[] generatedModules = templates.Keys.Select(name => $"Assets/Prefabs/Modules/{name}.prefab").ToArray();
            Assert.AreEqual(46, generatedModules.Length, "The authoritative generator must produce the approved 2x module set.");
            foreach (string path in generatedModules) AssertModulePhysics(path);
            foreach (string path in RoomPaths) AssertRoomContracts(path);
        }

        [Test]
        public void ModuleSelection_200Seeds_CoversAllAuthoritativeTemplatesDeterministically()
        {
            var templates = (Dictionary<string, string[]>)typeof(ModuleChunkBuilder)
                .GetField("ModuleTemplates", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
            MethodInfo select = typeof(ModuleChunkBuilder).GetMethod("SelectModuleTemplate", BindingFlags.Static | BindingFlags.NonPublic);
            MethodInfo role = typeof(ModuleChunkBuilder).GetMethod("GetModuleRole", BindingFlags.Static | BindingFlags.NonPublic);
            var covered = new HashSet<string[]>();

            foreach (string[] requested in templates.Values)
                for (uint seed = 0; seed < 200; seed++)
                {
                    string[] first = (string[])select.Invoke(null, new object[] { requested, seed, 0, null });
                    string[] second = (string[])select.Invoke(null, new object[] { requested, seed, 0, null });
                    Assert.AreSame(first, second, $"seed {seed} must be deterministic");
                    Assert.AreEqual(role.Invoke(null, new object[] { requested }), role.Invoke(null, new object[] { first }));
                    covered.Add(first);
                }

            Assert.AreEqual(46, covered.Count, "Every authoritative template must be selectable within 200 seeds.");

            string[] fallback = templates.Values.First();
            string[] invalid = fallback.Concat(new[] { string.Empty }).ToArray();
            string[] selected = (string[])select.Invoke(null,
                new object[] { fallback, 0u, 0, new List<string[]> { invalid, fallback } });
            Assert.AreSame(fallback, selected, "Invalid same-role candidate must fall through deterministically.");
        }

        [Test]
        public void AllAuthoritativeModules_HaveClearActualPlayerSweep()
        {
            var templates = (Dictionary<string, string[]>)typeof(ModuleChunkBuilder)
                .GetField("ModuleTemplates", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
            string[] names = templates.Keys.OrderBy(name => name).ToArray();
            Assert.AreEqual(46, names.Length);
            var instances = new List<GameObject>();
            try
            {
                for (int i = 0; i < names.Length; i++)
                {
                    GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/Prefabs/Modules/{names[i]}.prefab");
                    Assert.NotNull(prefab, $"{names[i]} source prefab is missing.");
                    GameObject instance = Object.Instantiate(prefab, new Vector3(i * 100f, 0f, 0f), Quaternion.identity);
                    instances.Add(instance);
                }

                Physics2D.SyncTransforms();
                for (int i = 0; i < names.Length; i++)
                {
                    Vector2 origin = new Vector2(i * 100f + 0.51f, 2.52f);
                    RaycastHit2D hit = Physics2D.CapsuleCast(origin, new Vector2(0.52f, 1.02f),
                        CapsuleDirection2D.Vertical, 0f, Vector2.right, 10.98f);
                    Assert.IsNull(hit.collider, $"{names[i]} corridor blocked by {hit.collider?.name} at {hit.distance}");
                }
            }
            finally
            {
                foreach (GameObject instance in instances) Object.DestroyImmediate(instance);
            }
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
            Assert.AreEqual(0, Mathf.RoundToInt(bounds.size.x) % 12, $"{path}: camera width must match whole modules");
            Assert.AreEqual(0, Mathf.RoundToInt(bounds.size.y) % 12, $"{path}: camera height must match whole modules");
            Assert.AreEqual(new Vector2(-0.5f, bounds.size.y * 0.5f), (Vector2)bounds.transform.localPosition,
                $"{path}: camera bounds must use the generated room dimensions");
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
