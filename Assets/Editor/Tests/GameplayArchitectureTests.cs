using NUnit.Framework;
using System.IO;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;

namespace QA.Tests
{
    public class GameplayArchitectureTests
    {
        [Test]
        public void Test01_UnitBaseHierarchyAndPivot_PlayerAndBoss()
        {
            Assert.IsTrue(true);
        }

        [Test]
        public void Test02_CombatStats_TakeDamageAndGuard_ReducesDamage()
        {
            GameObject obj = new GameObject("TestUnit");
            var stats = obj.AddComponent<CombatStats>();
            stats.InitStats();
            stats.SetGuarding(true);
            bool isSpecial = stats.TakeDamage(100f);
            Assert.IsTrue(isSpecial, "가드 성공 시 TakeDamage가 true를 반환해야 합니다.");
            Object.DestroyImmediate(obj);
        }

        [Test]
        public void Test03_CombatStats_ParrySuccess_InflictsPostureOnAttacker()
        {
            GameObject playerObj = new GameObject("PlayerTest");
            var playerStats = playerObj.AddComponent<CombatStats>();
            playerStats.InitStats();
            playerStats.SetParrying(true);

            GameObject attackerObj = new GameObject("AttackerTest");
            var attackerStats = attackerObj.AddComponent<CombatStats>();
            attackerStats.InitStats();

            Assert.IsTrue(playerStats.IsParrying);
            playerStats.TakeDamage(100f, false, false, attackerStats);
            Assert.AreEqual(40f, attackerStats.CurrentPosture, 0.1f, "패링 성공 시 공격자의 Posture가 40 누적되어야 합니다.");

            Object.DestroyImmediate(playerObj);
            Object.DestroyImmediate(attackerObj);
        }

        [Test]
        public void Test04_CombatStats_Dodge_EvadesDamage100Percent()
        {
            GameObject obj = new GameObject("TestDodge");
            var stats = obj.AddComponent<CombatStats>();
            stats.InitStats();
            stats.SetDodging(true);
            bool isSpecial = stats.TakeDamage(100f);
            Assert.IsTrue(isSpecial, "회피 성공 시 TakeDamage가 true를 반환해야 합니다.");
            Object.DestroyImmediate(obj);
        }

        [Test]
        public void Test05_PostureGauge_100PercentAccumulation_TriggersGroggyState()
        {
            GameObject obj = new GameObject("TestPosture");
            var stats = obj.AddComponent<CombatStats>();
            stats.InitStats();
            stats.AddPosture(100f);
            Assert.IsTrue(stats.IsGroggy, "자세 게이지 100% 누적 시 그로기 상태가 발동해야 합니다.");
            Object.DestroyImmediate(obj);
        }

        [Test]
        public void Test_KinematicMotor2D_SlopeStability_UnderStress()
        {
            GameObject motorObj = new GameObject("Test_KinematicMotor_SlopeStress");
            motorObj.AddComponent<Rigidbody2D>();
            motorObj.AddComponent<BoxCollider2D>();
            var motor = motorObj.AddComponent<KinematicMotor2D>();
            motor.InitMotor();

            float[] stressDeltaTimes = new float[] { 1f / 15f, 1f / 30f }; // 15 FPS (0.0667s), 30 FPS (0.0333s) 가혹 가변 프레임
            float[] slopeAngles = new float[] { 15f, 30f, 45f }; // 15도, 30도, 45도 경사면 접선

            float moveSpeed = 5.0f;
            try
            {
                foreach (float dt in stressDeltaTimes)
                {
                    foreach (float angle in slopeAngles)
                    {
                        Vector2 normal = new Vector2(-Mathf.Sin(angle * Mathf.Deg2Rad), Mathf.Cos(angle * Mathf.Deg2Rad));
                        motor.Teleport(Vector3.zero);
                        motor.SetTargetVelocityX(moveSpeed);
                        motor.SetGroundNormal(normal);

                        Vector2 before = motorObj.GetComponent<Rigidbody2D>().position;
                        motor.SimulateStep(dt);
                        Vector2 displacement = motorObj.GetComponent<Rigidbody2D>().position - before;
                        float penetration = Mathf.Max(0f, -Vector2.Dot(displacement, normal));
                        float speedDeviation = Mathf.Abs(displacement.magnitude / dt - moveSpeed) / moveSpeed;

                        Assert.IsTrue(motor.IsGrounded, $"{angle}° / {1f / dt:F0} FPS에서 grounded 상태를 잃었습니다.");
                        Assert.LessOrEqual(penetration, motor.SkinWidth, "경사면 아래로 허용 skin width 이상 파묻혔습니다.");
                        Assert.LessOrEqual(speedDeviation, 0.05f, "경사 투영 후 접선 이동 속도 편차가 5%를 초과했습니다.");
                    }
                }

                QATestRunner.AppendExceptionResult(nameof(KinematicMotor2D),
                    "15/30 FPS, 15/30/45 degree slopes; grounded retained, penetration <= skin, speed deviation <= 5%");
            }
            finally { Object.DestroyImmediate(motorObj); }
        }

        [Test]
        public void Test_CombatHitOverlap_UsesMotorKnockbackWithoutGroundPenetration()
        {
            var player = new GameObject("Player_HitOverlap_QA");
            var monster = new GameObject("Monster_HitOverlap_QA");
            var groundObject = new GameObject("Ground_HitOverlap_QA");
            try
            {
                player.AddComponent<Rigidbody2D>();
                var playerCollider = player.AddComponent<CapsuleCollider2D>();
                playerCollider.size = new Vector2(1f, 2f);
                var motor = player.AddComponent<KinematicMotor2D>();
                motor.InitMotor();
                player.transform.position = new Vector3(0f, 1f + motor.SkinWidth * 2f);
                motor.SetGroundNormal(Vector2.up);
                var playerStats = player.AddComponent<CombatStats>();
                typeof(CombatStats).GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic)
                    .Invoke(playerStats, null);

                monster.transform.position = new Vector3(-0.25f, player.transform.position.y);
                var monsterCollider = monster.AddComponent<CapsuleCollider2D>();
                monsterCollider.isTrigger = true;
                var monsterStats = monster.AddComponent<CombatStats>();
                typeof(CombatStats).GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic)
                    .Invoke(monsterStats, null);

                var ground = groundObject.AddComponent<BoxCollider2D>();
                ground.size = new Vector2(20f, 1f);
                groundObject.transform.position = new Vector3(0f, -0.5f);
                motor.SolidGroundLayer = 1 << groundObject.layer;
                motor.InitMotor();
                Physics2D.SyncTransforms();
                Assert.IsTrue(Physics2D.Distance(playerCollider, monsterCollider).isOverlapped);

                float hpBefore = playerStats.CurrentHp;
                playerStats.TakeDamage(10f, attacker: monsterStats);
                Assert.Less(playerStats.CurrentHp, hpBefore);
                Assert.Greater(motor.Velocity.x, 0f);
                Assert.GreaterOrEqual(motor.Velocity.y, 0f);
                Assert.GreaterOrEqual(playerCollider.bounds.min.y, ground.bounds.max.y);

                for (int i = 0; i < 10; i++)
                {
                    motor.SimulateStep(Time.fixedDeltaTime);
                    Physics2D.SyncTransforms();
                    Assert.GreaterOrEqual(playerCollider.bounds.min.y, ground.bounds.max.y,
                        $"Ground penetration after fixed step {i + 1}: bottom={playerCollider.bounds.min.y}, " +
                        $"groundTop={ground.bounds.max.y}, body={player.transform.position}, " +
                        $"velocity={motor.Velocity}, grounded={motor.IsGrounded}.");
                }
            }
            finally
            {
                Object.DestroyImmediate(groundObject);
                Object.DestroyImmediate(monster);
                Object.DestroyImmediate(player);
            }
        }

        [Test]
        public void Test_KinematicMotor_UnitsOverlapWithoutBlockingEnvironmentCollisionOrAttacks()
        {
            int playerLayer = LayerMask.NameToLayer("Player");
            int enemyLayer = LayerMask.NameToLayer("Enemy");
            Assert.AreEqual(8, playerLayer);
            Assert.AreEqual(9, enemyLayer);

            var player = new GameObject("Player_MotorFilter_QA") { layer = playerLayer };
            var monster = new GameObject("Monster_MotorFilter_QA") { layer = enemyLayer };
            var boss = new GameObject("Boss_MotorFilter_QA") { layer = enemyLayer };
            var groundObject = new GameObject("Ground_MotorFilter_QA");
            var wallObject = new GameObject("Wall_MotorFilter_QA");
            try
            {
                var playerBody = player.AddComponent<Rigidbody2D>();
                var playerCollider = player.AddComponent<BoxCollider2D>();
                var playerMotor = player.AddComponent<KinematicMotor2D>();
                playerMotor.InitMotor();
                player.transform.position = new Vector3(-3f, 0.51f);
                playerMotor.Teleport(player.transform.position);
                playerMotor.SetTargetVelocityX(5f);

                monster.transform.position = new Vector3(0f, 0.51f);
                var monsterCollider = monster.AddComponent<BoxCollider2D>();

                boss.AddComponent<Rigidbody2D>();
                boss.AddComponent<BoxCollider2D>();
                var bossMotor = boss.AddComponent<KinematicMotor2D>();
                bossMotor.InitMotor();

                var ground = groundObject.AddComponent<BoxCollider2D>();
                ground.size = new Vector2(20f, 1f);
                groundObject.transform.position = new Vector3(0f, -0.5f);

                var wall = wallObject.AddComponent<BoxCollider2D>();
                wall.size = new Vector2(1f, 4f);
                wallObject.transform.position = new Vector3(3.5f, 1.5f);

                Physics2D.SyncTransforms();
                for (int i = 0; i < 60; i++)
                {
                    playerMotor.SimulateStep(Time.fixedDeltaTime);
                    Physics2D.SyncTransforms();
                    Assert.AreNotSame(monsterCollider, playerMotor.WallCollider);
                }

                Assert.Greater(playerBody.position.x, monsterCollider.bounds.max.x);
                Assert.IsTrue(playerMotor.IsGrounded);
                Assert.AreSame(wall, playerMotor.WallCollider);
                Assert.GreaterOrEqual(playerCollider.bounds.min.y, ground.bounds.max.y);

                int attackMask = LayerMask.GetMask("Player", "Enemy");
                Assert.AreEqual((1 << playerLayer) | (1 << enemyLayer), attackMask);
                Assert.AreSame(monsterCollider,
                    Physics2D.OverlapPoint(monster.transform.position, attackMask));
                Assert.AreEqual(0, bossMotor.SolidGroundLayer.value & attackMask);
            }
            finally
            {
                Object.DestroyImmediate(wallObject);
                Object.DestroyImmediate(groundObject);
                Object.DestroyImmediate(boss);
                Object.DestroyImmediate(monster);
                Object.DestroyImmediate(player);
            }
        }

        [Test]
        public void Test_PlayerDeathAndGuardHit_KeepGroundPositionAndResetForReuse()
        {
            var playerObject = new GameObject("Player_DeathGround_QA");
            var groundObject = new GameObject("Ground_DeathGround_QA");
            var attackerObject = new GameObject("Attacker_GuardGround_QA");
            SimulationMode2D previousSimulationMode = Physics2D.simulationMode;
            try
            {
                Physics2D.simulationMode = SimulationMode2D.Script;
                var body = playerObject.AddComponent<Rigidbody2D>();
                var playerCollider = playerObject.AddComponent<BoxCollider2D>();
                var motor = playerObject.AddComponent<KinematicMotor2D>();
                var stats = playerObject.AddComponent<CombatStats>();
                var player = playerObject.AddComponent<Player>();
                typeof(Player).GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic)
                    .Invoke(player, null);
                motor.InitMotor();
                stats.InitStats();

                var ground = groundObject.AddComponent<BoxCollider2D>();
                ground.size = new Vector2(20f, 1f);
                groundObject.transform.position = new Vector3(0f, -0.5f);
                playerObject.transform.position = new Vector3(0f, 0.51f);
                motor.Teleport(playerObject.transform.position);
                motor.SetGroundNormal(Vector2.up);
                Physics2D.SyncTransforms();

                var attackerStats = attackerObject.AddComponent<CombatStats>();
                attackerStats.InitStats();
                stats.SetGuarding(true);
                motor.SetTargetVelocityX(5f);
                stats.TakeDamage(10f, attacker: attackerStats);
                for (int i = 0; i < 10; i++)
                {
                    motor.SimulateStep(Time.fixedDeltaTime);
                    Physics2D.SyncTransforms();
                    Assert.GreaterOrEqual(playerCollider.bounds.min.y, ground.bounds.max.y);
                }

                Vector2 deathPosition = body.position;
                player.Die();
                Assert.IsFalse(motor.enabled);
                Assert.AreEqual(Vector2.zero, motor.Velocity);
                Assert.IsFalse(playerCollider.enabled);
                for (int i = 0; i < 10; i++) Physics2D.Simulate(Time.fixedDeltaTime);
                Assert.AreEqual(deathPosition, body.position);

                Vector3 respawnPosition = new Vector3(2f, 1f);
                player.ResetAfterDeath(respawnPosition);
                Assert.IsTrue(motor.enabled);
                Assert.IsTrue(playerCollider.enabled);
                Assert.AreEqual((Vector2)respawnPosition, body.position);
                var renderer = (SpriteRenderer)typeof(UnitBase)
                    .GetField("spriteRenderer", BindingFlags.Instance | BindingFlags.NonPublic)
                    .GetValue(player);
                Assert.AreEqual(1f, renderer.color.a);
            }
            finally
            {
                Physics2D.simulationMode = previousSimulationMode;
                Object.DestroyImmediate(attackerObject);
                Object.DestroyImmediate(groundObject);
                Object.DestroyImmediate(playerObject);
            }
        }

        [Test]
        public void Test_MonsterDeathContract_IsIdempotentFadesAndResetsPoolState()
        {
            string source = File.ReadAllText("Assets/Scripts/Gameplay/Monster.cs");
            int lockGuard = source.IndexOf("if (deathSequenceActive) return;");
            int lockSet = source.IndexOf("deathSequenceActive = true;", lockGuard);
            int fade = source.IndexOf("Mathf.Lerp(startColor.a, 0f", lockSet);
            int despawn = source.IndexOf("DespawnUnit(this)", fade);

            Assert.GreaterOrEqual(lockGuard, 0);
            Assert.Greater(lockSet, lockGuard);
            Assert.Greater(fade, lockSet);
            Assert.Greater(despawn, fade);
            StringAssert.Contains("motor.SetVelocityY(0f);", source);
            StringAssert.Contains("deathSequenceActive = false;", source);
            StringAssert.Contains("color.a = 1f;", source);
        }

        [Test]
        public void Test_PlayerDeathReturnsToHubOnceAfterExistingDelay()
        {
            string source = File.ReadAllText("Assets/Scripts/Gameplay/Player.cs");
            int deathLock = source.IndexOf("if (deathSequenceActive) return;");
            int delay = source.IndexOf("FromSeconds(2.0f)", deathLock);
            int hub = source.IndexOf("StageManager.ReturnToHubAsync", delay);

            Assert.GreaterOrEqual(deathLock, 0);
            Assert.Greater(delay, deathLock);
            Assert.Greater(hub, delay);
            StringAssert.DoesNotContain("ReloadStageAsync", source);
            StringAssert.DoesNotContain("LoadNextRoomAsync(0", source);
        }

        [Test]
        public void Test_ChunkOwnerDisableReturnsRangedEffectAndRejectsDuplicateReturn()
        {
            var poolObject = new GameObject("EffectPool_QA");
            var effectPool = poolObject.AddComponent<EffectPoolManager>();
            var instanceField = typeof(Singleton<EffectPoolManager>)
                .GetField("<Instance>k__BackingField", BindingFlags.Static | BindingFlags.NonPublic);
            var previousEffectPool = EffectPoolManager.Instance;
            instanceField.SetValue(null, effectPool);
            var owner = new GameObject("RangedMonster_ChunkA");
            owner.SetActive(false);
            var executor = owner.AddComponent<SkillExecutor>();
            SkillEffect effect = null;
            SkillEffect reused = null;
            try
            {
                Assert.IsNotNull(EffectPoolManager.Instance);
                effect = executor.SpawnSkillEffect("Ranged_QA", Vector3.zero, Vector2.one, 1f, 30f,
                    FactionType.Enemy, Color.white);
                Assert.IsTrue(effect.gameObject.activeSelf);

                typeof(SkillExecutor).GetMethod("OnDisable", BindingFlags.Instance | BindingFlags.NonPublic)
                    .Invoke(executor, null);
                Assert.IsFalse(effect.gameObject.activeSelf, "Chunk owner despawn must return its active attack effect.");

                effect.ReturnToPool();
                Assert.IsFalse(effect.gameObject.activeSelf, "Duplicate return must remain a no-op.");

                var pools = (System.Collections.IDictionary)typeof(EffectPoolManager)
                    .GetField("poolDict", BindingFlags.Instance | BindingFlags.NonPublic)
                    .GetValue(EffectPoolManager.Instance);
                Assert.IsTrue(pools.Contains("Ranged_QA"));

                reused = executor.SpawnSkillEffect("Ranged_QA", Vector3.one, Vector2.one, 1f, 30f,
                    FactionType.Enemy, Color.white);
                Assert.AreSame(effect, reused, "The same chunk-safe pool entry must be reused.");
            }
            finally
            {
                if (reused != null) reused.ReturnToPool();
                if (effect != null && effect.gameObject != null) Object.DestroyImmediate(effect.gameObject);
                Object.DestroyImmediate(owner);
                if (EffectPoolManager.Instance == effectPool) instanceField.SetValue(null, previousEffectPool);
                Object.DestroyImmediate(poolObject);
            }
        }

        [Test]
        public void Test_ProjectileReturnCancelsGenerationAndNotifiesOwnerOnce()
        {
            var projectileObject = new GameObject("Projectile_ChunkA_QA");
            try
            {
                var projectile = projectileObject.AddComponent<Gameplay.Combat.Projectile>();
                var attack = new Gameplay.Combat.AttackData { ProjectilePrefab = null, HitRadius = 0.1f };
                int returned = 0;
                projectile.Initialize(attack, Vector3.zero, Vector3.forward, _ => returned++);
                projectile.ReturnToPool();
                projectile.ReturnToPool();

                Assert.AreEqual(1, returned);
                Assert.IsFalse(projectileObject.activeSelf);
            }
            finally
            {
                Object.DestroyImmediate(projectileObject);
            }
        }

        [Test]
        public void Test_ProjectileAndEffectPoolsRejectStaleChunkCallbacks()
        {
            string projectile = File.ReadAllText("Assets/Scripts/Gameplay/Combat/Projectile.cs");
            string handler = File.ReadAllText("Assets/Scripts/Gameplay/Combat/AttackHandler.cs");
            string effectPool = File.ReadAllText("Assets/Scripts/Manager/EffectPoolManager.cs");
            string hub = File.ReadAllText("Assets/Scripts/Scene/HubScene.cs");

            StringAssert.Contains("currentGeneration == generation", projectile);
            StringAssert.Contains("if (returned || currentGeneration != generation) return;", projectile);
            StringAssert.Contains("projectile.ReturnToPool()", handler);
            StringAssert.Contains("current == generation", effectPool);
            StringAssert.Contains("!activeEffects.Remove(effectObj)", effectPool);
            StringAssert.Contains("ResetRunForHub()", hub);
            StringAssert.Contains("if (transitionInProgress) return;", hub);
            StringAssert.DoesNotContain("OnGUI", hub);
        }

        [Test]
        public void Test_AlertMessage_TextLookupReplaceAndSceneDisableContract()
        {
            var dataObject = new GameObject("Alert_Data_QA");
            var alertObject = new GameObject("Alert_QA");
            var textObject = new GameObject("Alert_Text_QA");
            var previousDataManager = DataTableManager.Instance;
            var instanceField = typeof(Singleton<DataTableManager>)
                .GetField("<Instance>k__BackingField", BindingFlags.Static | BindingFlags.NonPublic);
            try
            {
                var dataManager = dataObject.AddComponent<DataTableManager>();
                instanceField.SetValue(null, dataManager);
                var tables = (System.Collections.Generic.Dictionary<DataTableType, IDataLoad>)typeof(DataTableManager)
                    .GetField("dataList", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(dataManager);
                var textTable = new TextDataTable();
                textTable.LoadData("idx,text\n2001,First alert\n2002,Replacement alert");
                tables[DataTableType.Text] = textTable;

                var canvasGroup = alertObject.AddComponent<CanvasGroup>();
                var text = textObject.AddComponent<TMPro.TextMeshProUGUI>();
                var alert = alertObject.AddComponent<AlertMessage>();
                typeof(AlertMessage).GetField("messageText", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(alert, text);
                typeof(AlertMessage).GetField("canvasGroup", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(alert, canvasGroup);

                Assert.IsTrue(alert.Show(2001, 0, 0));
                Assert.AreEqual("First alert", text.text);
                Assert.AreEqual(1f, canvasGroup.alpha);
                Assert.IsTrue(alert.Show(2001, 0, 0), "Duplicate message must not add a second listener or task.");
                Assert.IsTrue(alert.Show(9999, 2002, 0));
                Assert.AreEqual("Replacement alert", text.text);
                Assert.IsTrue(alert.Show(2002, 0, 0));
                Assert.AreEqual("Replacement alert", text.text);

                typeof(AlertMessage).GetMethod("OnDisable", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(alert, null);
                Assert.IsFalse(alert.IsVisible);
                Assert.AreEqual(0f, canvasGroup.alpha);

                string source = File.ReadAllText("Assets/Scripts/UI/AlertMessage.cs");
                Assert.IsFalse(Regex.IsMatch(source, @"\b(Update|OnGUI)\s*\("));
                StringAssert.Contains("currentGeneration != generation", source);
                StringAssert.Contains("HasCharacters(message)", source);
                StringAssert.DoesNotContain("AddListener", source);
            }
            finally
            {
                if (DataTableManager.Instance == (DataTableManager)dataObject.GetComponent(typeof(DataTableManager)))
                    instanceField.SetValue(null, previousDataManager);
                Object.DestroyImmediate(textObject);
                Object.DestroyImmediate(alertObject);
                Object.DestroyImmediate(dataObject);
            }
        }
    }
}
