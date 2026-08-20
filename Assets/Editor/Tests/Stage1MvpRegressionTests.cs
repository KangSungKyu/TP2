using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace QA.Tests
{
    public class Stage1MvpRegressionTests
    {
        [Test]
        public void Test_RoomDoorPortal_ConsumesManualPlayerInputOnce()
        {
            Player previousPlayer = Player.Instance;
            var playerProperty = typeof(Player).GetProperty("Instance", BindingFlags.Static | BindingFlags.Public);
            playerProperty.GetSetMethod(true).Invoke(null, new object[] { null });
            var portalObject = new GameObject("ManualPortal_QA");
            var playerObject = new GameObject("PortalPlayer_QA");
            var monsterObject = new GameObject("PortalMonster_QA");
            try
            {
                RoomDoorPortal portal = portalObject.AddComponent<RoomDoorPortal>();
                playerObject.AddComponent<Rigidbody2D>();
                Collider2D playerCollider = playerObject.AddComponent<BoxCollider2D>();
                KinematicMotor2D motor = playerObject.AddComponent<KinematicMotor2D>();
                Player player = playerObject.AddComponent<Player>();
                playerProperty.GetSetMethod(true).Invoke(null, new object[] { player });
                typeof(UnitBase).GetField("motor", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(player, motor);
                motor.InitMotor();
                Collider2D monsterCollider = monsterObject.AddComponent<BoxCollider2D>();
                MethodInfo enter = typeof(RoomDoorPortal).GetMethod("OnTriggerEnter2D", BindingFlags.Instance | BindingFlags.NonPublic);
                MethodInfo exit = typeof(RoomDoorPortal).GetMethod("OnTriggerExit2D", BindingFlags.Instance | BindingFlags.NonPublic);
                MethodInfo consume = typeof(RoomDoorPortal).GetMethod("TryConsumeInteraction", BindingFlags.Instance | BindingFlags.NonPublic);
                FieldInfo lastFrame = typeof(RoomDoorPortal).GetField("lastInteractionFrame", BindingFlags.Instance | BindingFlags.NonPublic);

                Assert.IsFalse((bool)consume.Invoke(portal, new object[] { true }), "Input before trigger entry must be ignored.");
                enter.Invoke(portal, new object[] { monsterCollider });
                Assert.IsFalse((bool)consume.Invoke(portal, new object[] { true }), "Monster collider must never become a portal candidate.");
                enter.Invoke(portal, new object[] { playerCollider });
                Assert.IsFalse((bool)consume.Invoke(portal, new object[] { false }), "Contact alone must not transition.");
                Assert.IsFalse((bool)consume.Invoke(portal, new object[] { true }), "Airborne or falling player must not transition.");
                motor.SetGroundNormal(Vector2.up);
                Assert.IsTrue((bool)consume.Invoke(portal, new object[] { true }), "W press must be consumed once.");
                Assert.IsFalse((bool)consume.Invoke(portal, new object[] { true }), "Held or duplicate input in one frame must be ignored.");

                lastFrame.SetValue(portal, -1);
                Assert.IsTrue((bool)consume.Invoke(portal, new object[] { true }), "UpArrow re-press after a failed transition must be accepted.");
                exit.Invoke(portal, new object[] { playerCollider });
                lastFrame.SetValue(portal, -1);
                Assert.IsFalse((bool)consume.Invoke(portal, new object[] { true }), "Input outside the trigger must be ignored.");
                portalObject.SetActive(false);
                Assert.IsFalse((bool)consume.Invoke(portal, new object[] { true }), "Disabled portal input must be ignored.");

                string source = File.ReadAllText("Assets/Scripts/Gameplay/RoomDoorPortal.cs");
                StringAssert.Contains("keyboard.wKey.wasPressedThisFrame", source);
                StringAssert.Contains("keyboard.upArrowKey.wasPressedThisFrame", source);
                StringAssert.DoesNotContain("if (!AutoTriggerOnTouch || isTransitioning)", source);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(monsterObject);
                UnityEngine.Object.DestroyImmediate(playerObject);
                UnityEngine.Object.DestroyImmediate(portalObject);
                playerProperty.GetSetMethod(true).Invoke(null, new object[] { previousPlayer });
            }
        }

        [Test]
        public void Test_RoomDoorPortal_DestinationIdxLabel_IsOptionalAndNeverRoutesByText()
        {
            var portalObject = new GameObject("PortalDestinationLabel_QA");
            try
            {
                RoomDoorPortal portal = portalObject.AddComponent<RoomDoorPortal>();
                portal.Configure(7, 3, 11, 1057);
                Assert.AreEqual((byte)7, portal.TargetSlotIdx);
                Assert.AreEqual(1057u, portal.DestinationChunkResourceIdx);
                Assert.AreEqual("Chunk 1057", portal.GetDestinationLabelText());
                portal.SetDestinationLabelVisible(false);
                Assert.IsFalse(portal.ShowPrototypeDestination);

                string source = File.ReadAllText("Assets/Scripts/Gameplay/RoomDoorPortal.cs");
                StringAssert.Contains("LoadConnectedRoomAsync(TargetSlotIdx)", source);
                StringAssert.DoesNotContain("LoadConnectedRoomAsync(GetDestinationLabelText", source);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(portalObject);
            }
        }

        [Test]
        public void Test_ConfigureSocketPortal_ReusesPrefabComponentAndSerializedDestinationLabel()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Structures/Portal_Gate.prefab");
            GameObject portalObject = UnityEngine.Object.Instantiate(prefab);
            GameObject socketObject = new GameObject("PortalSocket_QA", typeof(ChunkSocketMarker));
            try
            {
                RoomDoorPortal original = portalObject.GetComponent<RoomDoorPortal>();
                Assert.NotNull(original);
                FieldInfo labelField = typeof(RoomDoorPortal).GetField("destinationLabel", BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.NotNull(labelField.GetValue(original), "Prefab destinationLabel serialization must be preserved.");

                RoomDoorPortal configured = TilemapStageBuilder.ConfigureSocketPortal(
                    socketObject.GetComponent<ChunkSocketMarker>(), portalObject, 7, 3, 11, 1057);

                Assert.AreSame(original, configured);
                Assert.AreEqual(1, portalObject.GetComponents<RoomDoorPortal>().Length);
                Assert.NotNull(labelField.GetValue(configured));
                Assert.AreEqual((byte)7, configured.TargetSlotIdx);
                Assert.AreEqual(1057u, configured.DestinationChunkResourceIdx);
                configured.SetDestinationLabelVisible(false);
                Assert.IsFalse(configured.ShowPrototypeDestination);
                configured.SetDestinationLabelVisible(true);
                Assert.IsTrue(configured.ShowPrototypeDestination);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(socketObject);
                UnityEngine.Object.DestroyImmediate(portalObject);
            }
        }

        [Test]
        public void Test_BossGate_Access_HasNoProgressOrBuildPowerDependency()
        {
            var fields = typeof(RoomDoorPortal).GetFields()
                .Select(field => field.Name.ToLowerInvariant()).ToArray();

            Assert.Contains(nameof(RoomDoorPortal.TargetRoomResourceIdx).ToLowerInvariant(), fields);
            Assert.Contains(nameof(RoomDoorPortal.AutoTriggerOnTouch).ToLowerInvariant(), fields);
            Assert.IsFalse(fields.Any(name => name.Contains("visit") || name.Contains("combat") ||
                                              name.Contains("clear") || name.Contains("buildpower")));
        }

        [Test]
        public void Test_InvalidSingleResourceIdx_FallsBackWithoutCrash()
        {
            var gameObject = new GameObject("Stage1_InvalidIdx_QA");
            try
            {
                var manager = gameObject.AddComponent<StageManager>();
                Assert.DoesNotThrow(() => manager.ResolveAddressableKey(uint.MaxValue));
                Assert.AreEqual("Prefab_1040", manager.ResolveAddressableKey(uint.MaxValue));
            }
            finally { UnityEngine.Object.DestroyImmediate(gameObject); }
        }

        [Test]
        public void Test_15FPS_ParryAndDodgeTimingContracts()
        {
            string source = File.ReadAllText("Assets/Scripts/Gameplay/Player.cs");
            Assert.IsTrue(source.Contains("UniTask.Delay(150"), "패링 판정 시간이 0.134초 이상이어야 합니다.");
            Assert.IsTrue(source.Contains("float duration = 0.3f") && source.Contains("elapsed += Time.deltaTime"),
                "회피 판정은 누적 deltaTime 기준 0.30초여야 합니다.");
        }

        [Test]
        public void Test_SeedGeneration_3x4_And_4x3_100Seeds()
        {
            int wideCount = 0;
            int tallCount = 0;
            for (uint seed = 0; seed < 200; seed++)
            {
                StageRunData run = Stage1RunGenerator.Generate(seed);
                Assert.IsTrue(Stage1RunGenerator.Validate(run), $"Seed {seed} must produce a valid Stage 1 graph.");
                Assert.That(run.Slots.Length, Is.InRange(9, 11));
                if (run.Rows == 3 && run.Columns == 4) wideCount++;
                if (run.Rows == 4 && run.Columns == 3) tallCount++;
            }
            Assert.AreEqual(100, wideCount);
            Assert.AreEqual(100, tallCount);
        }

        [Test]
        public void Test_Determinism_Bfs_BossDistance_BranchAndCycle()
        {
            StageRunData first = Stage1RunGenerator.Generate(77);
            StageRunData second = Stage1RunGenerator.Generate(77);
            var path = Stage1RunGenerator.FindPath(first, first.StartSlotIdx, first.BossGateSlotIdx);

            Assert.That(path.Count - 1, Is.InRange(3, 4));
            Assert.AreEqual(first.Rows, second.Rows);
            Assert.AreEqual(first.Columns, second.Columns);
            Assert.AreEqual(first.BossGateSlotIdx, second.BossGateSlotIdx);
            CollectionAssert.AreEqual(first.Slots.Select(slot => slot.SlotIdx), second.Slots.Select(slot => slot.SlotIdx));
            CollectionAssert.AreEqual(first.Slots.Select(slot => slot.ConnectionMask), second.Slots.Select(slot => slot.ConnectionMask));
            CollectionAssert.AreEqual(first.Slots.Select(slot => slot.ChunkResourceIdx), second.Slots.Select(slot => slot.ChunkResourceIdx));

            foreach (var slot in first.Slots)
                Assert.IsNotNull(Stage1RunGenerator.FindPath(first, first.StartSlotIdx, slot.SlotIdx));

            int branches = first.Slots.Count(slot => CountBits(slot.ConnectionMask) >= 3);
            int edges = first.Slots.Sum(slot => CountBits(slot.ConnectionMask)) / 2;
            Assert.GreaterOrEqual(branches, 3);
            Assert.GreaterOrEqual(edges - first.Slots.Length + 1, 1);
        }

        [Test]
        public void Test_DeterministicMonsterAssignments()
        {
            for (uint seed = 0; seed < 10000; seed++)
            {
                StageRunData first = Stage1RunGenerator.Generate(seed);
                StageRunData second = Stage1RunGenerator.Generate(seed);
                for (int i = 0; i < first.Slots.Length; i++)
                {
                    CollectionAssert.AreEqual(first.Slots[i].MonsterUnitIdxList, second.Slots[i].MonsterUnitIdxList);
                    foreach (uint unitIdx in first.Slots[i].MonsterUnitIdxList)
                        Assert.That(unitIdx, Is.InRange(3101u, 3105u));
                }

                Assert.IsEmpty(first.Slots.Single(slot => slot.SlotIdx == first.StartSlotIdx).MonsterUnitIdxList);
                Assert.IsEmpty(first.Slots.Single(slot => slot.SlotIdx == first.BossGateSlotIdx).MonsterUnitIdxList);
            }

            foreach (uint seed in new[] { uint.MaxValue, uint.MaxValue - 1 })
                foreach (var slot in Stage1RunGenerator.Generate(seed).Slots)
                    foreach (uint unitIdx in slot.MonsterUnitIdxList)
                        Assert.That(unitIdx, Is.InRange(3101u, 3105u));
        }

        [Test]
        public void Test_BossRoom_IsBlockedBeforeGate_AndAllowedAtGate()
        {
            string source = File.ReadAllText("Assets/Scripts/Manager/StageManager.cs");
            StringAssert.Contains("Boss room transition rejected before reaching the BossGate slot", source);
            StringAssert.DoesNotContain("roomResourceIdx = 1041", source);
            StringAssert.Contains("CurrentRun.CurrentSlotIdx != CurrentRun.BossGateSlotIdx", source);
            Assert.AreEqual("Prefab_1042", CreateManager().ResolveAddressableKey(1042));
        }

        [Test]
        public void Test_100Seeds_SixMoves_AreMutualAdjacentAndLoadDestinationResource()
        {
            for (uint seed = 0; seed < 100; seed++)
            {
                var manager = CreateManager();
                try
                {
                    StageRunData run = Stage1RunGenerator.Generate(seed);
                    foreach (var slot in run.Slots) slot.ChunkResourceIdx = 1050u + slot.SlotIdx;
                    SetCurrentRun(manager, run);

                    for (int move = 0; move < 6; move++)
                    {
                        byte previous = run.CurrentSlotIdx;
                        byte targetSlotIdx = run.Slots.First(slot => IsMutualAdjacent(run, previous, slot.SlotIdx)).SlotIdx;
                        Assert.IsTrue(manager.TryMoveToConnectedSlot(targetSlotIdx, out uint resourceIdx));
                        Assert.AreNotEqual(previous, run.CurrentSlotIdx);
                        Assert.IsTrue(run.TryGetSlot(run.CurrentSlotIdx, out var destination));
                        Assert.AreEqual(destination.ChunkResourceIdx, resourceIdx);
                        Assert.IsTrue(IsMutualAdjacent(run, previous, run.CurrentSlotIdx));
                    }
                }
                finally { UnityEngine.Object.DestroyImmediate(manager.gameObject); }
            }
        }

        [TestCase(ChunkSocketDirection.North, 1)]
        [TestCase(ChunkSocketDirection.East, 5)]
        [TestCase(ChunkSocketDirection.South, 7)]
        [TestCase(ChunkSocketDirection.West, 3)]
        public void Test_CommonPortalGate_UsesFixedTargetIndependentOfSocketDirection(
            ChunkSocketDirection socketDirection, byte expectedTarget)
        {
            var manager = CreateManager();
            var socketObject = new GameObject("Socket_QA");
            var portalObject = new GameObject("Portal_Gate");
            var returnPortalObject = new GameObject("Portal_Gate_Return");
            try
            {
                var run = new StageRunData
                {
                    Rows = 3,
                    Columns = 3,
                    CurrentSlotIdx = 4,
                    Slots = new[]
                    {
                        new ChunkSlotData { SlotIdx = 1, ConnectionMask = 4, ChunkResourceIdx = 1051 },
                        new ChunkSlotData { SlotIdx = 3, ConnectionMask = 2, ChunkResourceIdx = 1053 },
                        new ChunkSlotData { SlotIdx = 4, ConnectionMask = 15, ChunkResourceIdx = 1054 },
                        new ChunkSlotData { SlotIdx = 5, ConnectionMask = 8, ChunkResourceIdx = 1055 },
                        new ChunkSlotData { SlotIdx = 7, ConnectionMask = 1, ChunkResourceIdx = 1057 }
                    }
                };
                SetCurrentRun(manager, run);
                var socket = socketObject.AddComponent<ChunkSocketMarker>();
                socket.Direction = socketDirection;
                Assert.IsTrue(manager.TryGetConnectedSlot(socket.Direction, out byte targetSlotIdx));
                Assert.AreEqual(expectedTarget, targetSlotIdx);

                RoomDoorPortal portal = TilemapStageBuilder.ConfigureSocketPortal(socket, portalObject, targetSlotIdx);
                socket.Direction = (ChunkSocketDirection)(((int)socketDirection + 1) % 4);
                Assert.AreEqual(1, portalObject.GetComponents<RoomDoorPortal>().Length);
                Assert.IsTrue(manager.TryMoveToConnectedSlot(portal.TargetSlotIdx, out uint resourceIdx));
                Assert.AreEqual(expectedTarget, run.CurrentSlotIdx);
                Assert.AreEqual(1050u + expectedTarget, resourceIdx);
                Assert.IsFalse(manager.TryMoveToConnectedSlot(byte.MaxValue, out _));

                RoomDoorPortal returnPortal = TilemapStageBuilder.ConfigureSocketPortal(socket, returnPortalObject, 4);
                Assert.IsTrue(manager.TryMoveToConnectedSlot(returnPortal.TargetSlotIdx, out _));
                Assert.AreEqual(4, run.CurrentSlotIdx);
                Assert.AreNotEqual(socket.transform.position,
                    TilemapStageBuilder.CalculateSafeEntryPosition(socket, new Vector3(0.5f, 1f), 0.01f));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(returnPortalObject);
                UnityEngine.Object.DestroyImmediate(portalObject);
                UnityEngine.Object.DestroyImmediate(socketObject);
                UnityEngine.Object.DestroyImmediate(manager.gameObject);
            }
        }

        [Test]
        public void Test_PortalOwnerAndGeneration_IsolateStaleTarget7AndDuplicateTriggers()
        {
            var manager = CreateManager();
            var staleObject = new GameObject("Portal_Stale_QA");
            var currentObject = new GameObject("Portal_Current_QA");
            var invalidObject = new GameObject("Portal_Invalid_QA");
            try
            {
                var run = new StageRunData
                {
                    Rows = 3,
                    Columns = 3,
                    CurrentSlotIdx = 4,
                    Slots = new[]
                    {
                        new ChunkSlotData { SlotIdx = 4, ConnectionMask = 4, ChunkResourceIdx = 1054 },
                        new ChunkSlotData { SlotIdx = 7, ConnectionMask = 1, ChunkResourceIdx = 1057 }
                    }
                };
                SetCurrentRun(manager, run);
                SetRoomGeneration(manager, 10);
                var stalePortal = staleObject.AddComponent<RoomDoorPortal>();
                stalePortal.Configure(7, 4, 10);

                Assert.IsTrue(stalePortal.TryAcquireTransition(manager));
                Assert.IsFalse(stalePortal.TryAcquireTransition(manager), "A duplicate trigger must not start a second transition.");
                manager.CancelPortalTransition(4, 10);
                Assert.IsTrue(manager.TryMoveToConnectedSlot(7, out uint resourceIdx));
                Assert.AreEqual(1057u, resourceIdx);
                Assert.IsFalse(stalePortal.TryAcquireTransition(manager));
                Assert.IsFalse(staleObject.activeSelf, "A stale owner portal must disable itself without warning.");

                run.CurrentSlotIdx = 4;
                SetRoomGeneration(manager, 11);
                Assert.IsFalse(stalePortal.TryAcquireTransition(manager), "A previous room generation must stay stale after return.");
                var currentPortal = currentObject.AddComponent<RoomDoorPortal>();
                currentPortal.Configure(7, 4, 11);
                Assert.IsTrue(currentPortal.TryAcquireTransition(manager));
                manager.CancelPortalTransition(4, 11);

                var invalidPortal = invalidObject.AddComponent<RoomDoorPortal>();
                invalidPortal.Configure(8, 4, 11);
                Assert.IsTrue(invalidPortal.TryAcquireTransition(manager));
                Assert.IsFalse(manager.TryMoveToConnectedSlot(invalidPortal.TargetSlotIdx, out _));
                manager.CancelPortalTransition(4, 11);
                string stageSource = File.ReadAllText("Assets/Scripts/Manager/StageManager.cs");
                string portalSource = File.ReadAllText("Assets/Scripts/Gameplay/RoomDoorPortal.cs");
                Assert.AreEqual(1, stageSource.Split(new[] { "Invalid portal graph target" }, StringSplitOptions.None).Length - 1);
                StringAssert.DoesNotContain("Slot transition rejected", portalSource);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(invalidObject);
                UnityEngine.Object.DestroyImmediate(currentObject);
                UnityEngine.Object.DestroyImmediate(staleObject);
                UnityEngine.Object.DestroyImmediate(manager.gameObject);
            }
        }

        [Test]
        public void Test_1041_IsUsedOnlyWhenDestinationChunkResourceIsMissing()
        {
            var manager = CreateManager();
            try
            {
                var run = new StageRunData
                {
                    Rows = 1, Columns = 2, CurrentSlotIdx = 0,
                    Slots = new[]
                    {
                        new ChunkSlotData { SlotIdx = 0, ChunkResourceIdx = 1040, ConnectionMask = 2 },
                        new ChunkSlotData { SlotIdx = 1, ChunkResourceIdx = 0, ConnectionMask = 8 }
                    }
                };
                SetCurrentRun(manager, run);
                Assert.IsTrue(manager.TryMoveToConnectedSlot(1, out uint fallback));
                Assert.AreEqual(1041u, fallback);

                run.CurrentSlotIdx = 0;
                run.Slots[1].ChunkResourceIdx = 1056;
                Assert.IsTrue(manager.TryMoveToConnectedSlot(1, out uint configured));
                Assert.AreEqual(1056u, configured);
            }
            finally { UnityEngine.Object.DestroyImmediate(manager.gameObject); }
        }

        [Test]
        public void Test_ConnectedSlotNavigation_ChangesSlotAndResource_SupportsReverseAndRejectsDisconnected()
        {
            var gameObject = new GameObject("Stage1_Navigation_QA");
            try
            {
                var manager = gameObject.AddComponent<StageManager>();
                var run = new StageRunData
                {
                    Rows = 3,
                    Columns = 4,
                    CurrentSlotIdx = 0,
                    Slots = new[]
                    {
                        new ChunkSlotData { SlotIdx = 0, ChunkResourceIdx = 1040, ConnectionMask = 2, Visited = true },
                        new ChunkSlotData { SlotIdx = 1, ChunkResourceIdx = 1050, ConnectionMask = 10 },
                        new ChunkSlotData { SlotIdx = 2, ChunkResourceIdx = 1051, ConnectionMask = 8 },
                        new ChunkSlotData { SlotIdx = 8, ChunkResourceIdx = 1052, ConnectionMask = 0 }
                    }
                };
                SetCurrentRun(manager, run);

                Assert.IsTrue(manager.TryMoveToConnectedSlot(1, out uint firstResource));
                Assert.AreEqual(1, run.CurrentSlotIdx);
                Assert.AreEqual(1050u, firstResource);
                Assert.IsTrue(manager.TryMoveToConnectedSlot(2, out uint secondResource));
                Assert.AreEqual(2, run.CurrentSlotIdx);
                Assert.AreEqual(1051u, secondResource);
                Assert.IsTrue(manager.TryMoveToConnectedSlot(1, out uint reverseResource));
                Assert.AreEqual(1050u, reverseResource);
                Assert.IsFalse(manager.TryMoveToConnectedSlot(8, out _));
                Assert.AreEqual(1, run.CurrentSlotIdx);
            }
            finally { UnityEngine.Object.DestroyImmediate(gameObject); }
        }

        [Test]
        public void Test_ChunkResourceMaxUse_IsRespectedAndBossGateRequiresArrival()
        {
            var layout = new StageLayoutData { StageDataIdx = 9001, MinActiveChunks = 9, MaxActiveChunks = 11 };
            var chunks = new[] { new ChunkResourceData { Idx = 11050, ResourceIdx = 1050, SupportedConnectionMask = 15, MinStageIdx = 9001, MaxUsePerRun = 2 } };
            StageRunData run = Stage1RunGenerator.Generate(4, layout, chunks, null);
            Assert.LessOrEqual(run.Slots.Count(slot => slot.ChunkResourceIdx == 1050), 2);
            Assert.AreNotEqual(run.BossGateSlotIdx, run.CurrentSlotIdx);
            var path = Stage1RunGenerator.FindPath(run, run.StartSlotIdx, run.BossGateSlotIdx);
            Assert.AreEqual(run.BossGateSlotIdx, path[path.Count - 1]);
        }

        [Test]
        public void Test_ChunkSockets_BindOnlyReciprocalConnections_WithTargetAndTriggerVisual()
        {
            var managerObject = new GameObject("Stage1_Socket_Manager_QA");
            var socketObject = new GameObject("Socket_East_QA");
            var portalObject = new GameObject("Portal_QA");
            try
            {
                var manager = managerObject.AddComponent<StageManager>();
                var run = new StageRunData
                {
                    Rows = 3,
                    Columns = 4,
                    CurrentSlotIdx = 0,
                    Slots = new[]
                    {
                        new ChunkSlotData { SlotIdx = 0, ConnectionMask = 2 },
                        new ChunkSlotData { SlotIdx = 1, ConnectionMask = 8 }
                    }
                };
                SetCurrentRun(manager, run);

                Assert.IsTrue(manager.TryGetConnectedSlot(ChunkSocketDirection.East, out byte target));
                Assert.AreEqual(1, target);
                Assert.IsFalse(manager.TryGetConnectedSlot(ChunkSocketDirection.North, out _));

                var socket = socketObject.AddComponent<ChunkSocketMarker>();
                var collider = portalObject.AddComponent<BoxCollider2D>();
                collider.isTrigger = true;
                var renderer = portalObject.AddComponent<SpriteRenderer>();
                RoomDoorPortal portal = TilemapStageBuilder.ConfigureSocketPortal(socket, portalObject, target);

                Assert.AreEqual(target, portal.TargetSlotIdx);
                Assert.IsTrue(collider.isTrigger);
                Assert.NotNull(renderer);
                Assert.IsFalse(portalObject.activeSelf, "Portal remains disabled until the player is repositioned.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(portalObject);
                UnityEngine.Object.DestroyImmediate(socketObject);
                UnityEngine.Object.DestroyImmediate(managerObject);
            }
        }

        [Test]
        public void Test_SouthEntry_TeleportClearsGroundAndResidualVelocity()
        {
            var playerObject = new GameObject("SouthEntry_Player_QA");
            var socketObject = new GameObject("SouthEntry_Socket_QA");
            var groundObject = new GameObject("SouthEntry_Ground_QA");
            try
            {
                var playerCollider = playerObject.AddComponent<CapsuleCollider2D>();
                playerCollider.size = new Vector2(1f, 2f);
                playerObject.AddComponent<Rigidbody2D>();
                var motor = playerObject.AddComponent<KinematicMotor2D>();
                motor.InitMotor();
                motor.SetTargetVelocityX(8f);
                motor.SetVelocityY(-10f);

                var socket = socketObject.AddComponent<ChunkSocketMarker>();
                socket.Direction = ChunkSocketDirection.South;
                socketObject.transform.position = new Vector3(0f, 1f, 0f);

                var ground = groundObject.AddComponent<BoxCollider2D>();
                ground.size = new Vector2(10f, 1f);
                groundObject.transform.position = new Vector3(0f, 0.5f, 0f);
                Physics2D.SyncTransforms();

                Vector3 safePosition = TilemapStageBuilder.CalculateSafeEntryPosition(
                    socket, playerCollider.bounds.extents, motor.SkinWidth);
                motor.Teleport(safePosition);
                motor.SetGroundNormal(Vector2.up);
                Physics2D.SyncTransforms();

                Assert.IsFalse(Physics2D.Distance(playerCollider, ground).isOverlapped,
                    "Teleport must place the South entry above the ground.");
                Assert.AreEqual(Vector2.zero, motor.Velocity);
                motor.SimulateStep(Time.fixedDeltaTime);
                Physics2D.SyncTransforms();
                Assert.IsFalse(Physics2D.Distance(playerCollider, ground).isOverlapped,
                    "The first motor step must preserve ground clearance.");
                Assert.IsTrue(motor.IsGrounded);
                Assert.GreaterOrEqual(motor.Velocity.y, -0.1f);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(groundObject);
                UnityEngine.Object.DestroyImmediate(socketObject);
                UnityEngine.Object.DestroyImmediate(playerObject);
            }
        }

        [TestCase(ChunkSocketDirection.North, 0f, 29f, 0f, 27.99f)]
        [TestCase(ChunkSocketDirection.East, 28f, 1f, 27.49f, 2.01f)]
        [TestCase(ChunkSocketDirection.South, 0f, 1f, 0f, 2.01f)]
        [TestCase(ChunkSocketDirection.West, -29f, 1f, -28.49f, 2.01f)]
        public void Test_EntryPosition_UsesActualP0SocketAndMovesInside(
            ChunkSocketDirection direction, float socketX, float socketY, float expectedX, float expectedY)
        {
            var socketObject = new GameObject($"Entry_{direction}_QA");
            try
            {
                var socket = socketObject.AddComponent<ChunkSocketMarker>();
                socket.Direction = direction;
                socketObject.transform.position = new Vector3(socketX, socketY);

                Vector3 position = TilemapStageBuilder.CalculateSafeEntryPosition(
                    socket, new Vector3(0.5f, 1f), 0.01f);

                Assert.AreEqual(expectedX, position.x, 0.001f);
                Assert.AreEqual(expectedY, position.y, 0.001f);
                Assert.AreNotEqual(socketObject.transform.position, position,
                    "Entry position must not remain on the socket.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(socketObject);
            }
        }

        [Test]
        public void Test_EntryPosition_UsesEntryMarkerAsAuthoritativePosition()
        {
            var socketObject = new GameObject("EntryMarker_Authority_QA");
            var markerObject = new GameObject("Entry_QA");
            try
            {
                markerObject.transform.SetParent(socketObject.transform);
                markerObject.transform.position = new Vector3(7.25f, 3.5f, 0f);
                var socket = socketObject.AddComponent<ChunkSocketMarker>();
                socket.Direction = ChunkSocketDirection.North;
                socket.EntryMarker = markerObject.transform;

                Assert.AreEqual(markerObject.transform.position,
                    TilemapStageBuilder.CalculateSafeEntryPosition(socket, Vector3.one, 0.01f));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(socketObject);
            }
        }

        [Test]
        public void Test_BossCompletion_SurvivesBossObjectDestructionDuringDelay()
        {
            string source = File.ReadAllText("Assets/Scripts/Gameplay/BossMonster.cs");
            Assert.IsFalse(source.Contains("CompleteStageAfterDeathAsync(this.GetCancellationTokenOnDestroy())"),
                "Boss 파괴 token이 1.5초 지연을 취소하여 완료 처리가 유실됩니다.");
        }

        [Test]
        public void Test_HubFailure_DoesNotPermanentlyConsumeCompletionLock()
        {
            string source = File.ReadAllText("Assets/Scripts/Manager/StageManager.cs");
            int methodIndex = source.IndexOf("CompleteStage1Async", StringComparison.Ordinal);
            int lockIndex = source.IndexOf("TryLockCompletion()", methodIndex, StringComparison.Ordinal);
            int hubIndex = source.IndexOf("await ReturnToHubAsync", methodIndex, StringComparison.Ordinal);
            Assert.Greater(lockIndex, hubIndex,
                "Hub 전환 성공 전에 completion lock을 소비하여 실패 후 재시도가 불가능합니다.");
        }

        [Test]
        public void Test_RevisitAndBossCompletion_AreIdempotent()
        {
            StageRunData run = Stage1RunGenerator.Generate(1);
            byte slot = run.BossGateSlotIdx;

            Assert.IsTrue(run.TryVisit(slot));
            Assert.IsFalse(run.TryVisit(slot));
            Assert.IsTrue(run.TryClear(slot));
            Assert.IsFalse(run.TryClear(slot));
            Assert.IsTrue(run.TryClaimReward(slot));
            Assert.IsFalse(run.TryClaimReward(slot));
            Assert.IsTrue(run.TryLockCompletion());
            Assert.IsFalse(run.TryLockCompletion());
        }

        [Test]
        public void Test_MultiSpawnZones_DeterministicAllocationAndClearanceContracts()
        {
            var room = new GameObject("SpawnZoneRoom_QA");
            var entry = new GameObject("Entry_QA");
            entry.transform.SetParent(room.transform);
            entry.transform.position = new Vector3(-25f, 1f);
            var zones = new List<SpawnPointMarker>();
            try
            {
                foreach (float x in new[] { -10f, 5f, 20f })
                {
                    var zone = new GameObject("Zone_QA");
                    zone.transform.SetParent(room.transform);
                    zone.transform.position = new Vector3(x, 1f);
                    zones.Add(zone.AddComponent<SpawnPointMarker>());
                }
                foreach (float x in new[] { -29f, 29f })
                {
                    var socket = new GameObject("Socket_QA");
                    socket.transform.SetParent(room.transform);
                    socket.transform.position = new Vector3(x, 1f);
                    socket.AddComponent<ChunkSocketMarker>();
                }

                Assert.IsTrue(UnitSpawner.ValidateSpawnZones(room, zones, entry.transform, out string error), error);
                uint[] encounter = { 3103, 3106, 3101, 3102, 3104 };
                uint[] first = UnitSpawner.BuildEncounterAllocation(encounter, zones.Count, 123u, 4);
                uint[] second = UnitSpawner.BuildEncounterAllocation(encounter, zones.Count, 123u, 4);
                CollectionAssert.AreEqual(first, second);
                Assert.LessOrEqual(first.Length, 3);
                Assert.LessOrEqual(System.Array.FindAll(first, idx => idx == 3103u || idx == 3106u).Length, 1);

                zones[1].transform.position = zones[0].transform.position + Vector3.right;
                Assert.IsFalse(UnitSpawner.ValidateSpawnZones(room, zones, entry.transform, out _));
                zones[1].transform.position = new Vector3(5f, 1f);
                zones[0].transform.position = new Vector3(-24f, 1f);
                Assert.IsFalse(UnitSpawner.ValidateSpawnZones(room, zones, entry.transform, out _));

                StageRunData run = Stage1RunGenerator.Generate(1);
                Assert.AreEqual(0, run.Slots[run.StartSlotIdx].MonsterUnitIdxList.Length);
                Assert.IsTrue(run.TryGetSlot(run.BossGateSlotIdx, out ChunkSlotData bossGate));
                Assert.AreEqual(0, bossGate.MonsterUnitIdxList.Length);

                var layout = new StageLayoutData
                {
                    StageDataIdx = 9001,
                    MinActiveChunks = 9,
                    MaxActiveChunks = 11
                };
                var chunks = new[]
                {
                    new ChunkResourceData { ResourceIdx = 1050, ChunkType = 1, SupportedConnectionMask = 15, MaxUsePerRun = 20 },
                    new ChunkResourceData { ResourceIdx = 1057, ChunkType = 4, SupportedConnectionMask = 15, MaxUsePerRun = 20 }
                };
                var encounters = new[]
                {
                    new MonsterEncounterData { UnitIdxList = new uint[] { 3101, 3102 } }
                };
                StageRunData typedRun = Stage1RunGenerator.Generate(2, layout, chunks, encounters);
                foreach (ChunkSlotData slot in typedRun.Slots)
                {
                    if (slot.ChunkResourceIdx == 1050) Assert.AreEqual(2, slot.MonsterUnitIdxList.Length);
                    if (slot.ChunkResourceIdx == 1057) Assert.AreEqual(0, slot.MonsterUnitIdxList.Length);
                }

                string spawnerSource = File.ReadAllText("Assets/Scripts/Manager/UnitSpawner.cs");
                string monsterSource = File.ReadAllText("Assets/Scripts/Gameplay/Monster.cs");
                StringAssert.Contains("MaximumActiveMonsters = 4", spawnerSource);
                StringAssert.Contains("SpawnFallbackOnce(zones, encounter, movementBounds)", spawnerSource);
                StringAssert.Contains("if (encounter.Length == 0) return;", spawnerSource);
                StringAssert.Contains("Combat chunk has no SpawnPointMarker", spawnerSource);
                StringAssert.Contains("GetComponentsInChildren<SpawnPointMarker>(true)", spawnerSource);
                StringAssert.Contains("MaximumAttackTokens = 2", monsterSource);
                StringAssert.Contains("activeAttackTokens >= MaximumAttackTokens", monsterSource);
                StringAssert.Contains("finally", monsterSource);
                StringAssert.Contains("ReleaseAttackToken();", monsterSource);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(room);
            }
        }

        private static int CountBits(byte value)
        {
            int count = 0;
            while (value != 0) { count += value & 1; value >>= 1; }
            return count;
        }

        private static void SetCurrentRun(StageManager manager, StageRunData run)
        {
            typeof(StageManager).GetProperty(nameof(StageManager.CurrentRun))
                .GetSetMethod(true).Invoke(manager, new object[] { run });
        }

        private static void SetRoomGeneration(StageManager manager, uint generation)
        {
            typeof(StageManager).GetProperty(nameof(StageManager.RoomGeneration))
                .GetSetMethod(true).Invoke(manager, new object[] { generation });
        }

        [Test]
        public void Test_BossGate1063_IsExclusiveAcross200Seeds()
        {
            var layout = new StageLayoutData { StageDataIdx = 9001, MinActiveChunks = 9, MaxActiveChunks = 11 };
            var chunks = new[]
            {
                new ChunkResourceData { ResourceIdx = 1050, ChunkType = 1, SupportedConnectionMask = 15, MaxUsePerRun = 20 },
                new ChunkResourceData { ResourceIdx = 1063, ChunkType = 1, SupportedConnectionMask = 15, MaxUsePerRun = 20 }
            };
            for (uint seed = 0; seed < 200; seed++)
            {
                StageRunData run = Stage1RunGenerator.Generate(seed, layout, chunks, null);
                Assert.IsTrue(run.TryGetSlot(run.BossGateSlotIdx, out ChunkSlotData bossGate));
                Assert.AreEqual(1063u, bossGate.ChunkResourceIdx, $"seed {seed}");
                Assert.AreEqual(1, run.Slots.Count(slot => slot.ChunkResourceIdx == 1063u), $"seed {seed}");
            }
        }

        [Test]
        public void Test_BossGateDoor_ReceivesOwnerGenerationAndSentinelRoute()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Rooms/Room_11063.prefab");
            GameObject room = UnityEngine.Object.Instantiate(prefab);
            StageManager manager = CreateManager();
            try
            {
                StageRunData run = Stage1RunGenerator.Generate(7);
                run.CurrentSlotIdx = run.BossGateSlotIdx;
                typeof(StageManager).GetProperty(nameof(StageManager.CurrentRun))?.SetValue(manager, run);
                typeof(StageManager).GetProperty(nameof(StageManager.RoomGeneration))?.SetValue(manager, 9u);
                typeof(TilemapStageBuilder).GetMethod("ConfigureBossGateDoor", BindingFlags.Static | BindingFlags.NonPublic)
                    ?.Invoke(null, new object[] { room, manager });

                RoomDoorPortal door = room.GetComponentsInChildren<RoomDoorPortal>(true)
                    .Single(candidate => candidate.TargetRoomResourceIdx == 1042u);
                Assert.AreEqual(byte.MaxValue, door.TargetSlotIdx);
                Assert.AreEqual(run.BossGateSlotIdx, door.OwnerSlotIdx);
                Assert.AreEqual(9u, door.RoomGeneration);
                Assert.IsTrue(door.TryAcquireTransition(manager));

                string source = File.ReadAllText("Assets/Scripts/Gameplay/RoomDoorPortal.cs");
                StringAssert.Contains("CurrentRun.CurrentSlotIdx == stageManager.CurrentRun.BossGateSlotIdx", source);
                StringAssert.Contains("await stageManager.LoadNextRoomAsync(1042)", source);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(room);
                UnityEngine.Object.DestroyImmediate(manager.gameObject);
            }
        }

        private static StageManager CreateManager()
        {
            return new GameObject("Stage1_CodeGate_QA").AddComponent<StageManager>();
        }

        private static bool IsMutualAdjacent(StageRunData run, byte fromIdx, byte toIdx)
        {
            if (!run.TryGetSlot(fromIdx, out var from) || !run.TryGetSlot(toIdx, out var to)) return false;
            int delta = toIdx - fromIdx;
            if (delta == -run.Columns) return (from.ConnectionMask & 1) != 0 && (to.ConnectionMask & 4) != 0;
            if (delta == 1 && fromIdx / run.Columns == toIdx / run.Columns) return (from.ConnectionMask & 2) != 0 && (to.ConnectionMask & 8) != 0;
            if (delta == run.Columns) return (from.ConnectionMask & 4) != 0 && (to.ConnectionMask & 1) != 0;
            if (delta == -1 && fromIdx / run.Columns == toIdx / run.Columns) return (from.ConnectionMask & 8) != 0 && (to.ConnectionMask & 2) != 0;
            return false;
        }

    }
}
