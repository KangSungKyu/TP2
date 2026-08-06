using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace QA.Tests
{
    public class Stage1MvpRegressionTests
    {
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
                        Assert.IsTrue(manager.TryMoveToConnectedSlot(byte.MaxValue, out uint resourceIdx));
                        Assert.AreNotEqual(previous, run.CurrentSlotIdx);
                        Assert.IsTrue(run.TryGetSlot(run.CurrentSlotIdx, out var destination));
                        Assert.AreEqual(destination.ChunkResourceIdx, resourceIdx);
                        Assert.IsTrue(IsMutualAdjacent(run, previous, run.CurrentSlotIdx));
                    }
                }
                finally { UnityEngine.Object.DestroyImmediate(manager.gameObject); }
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
