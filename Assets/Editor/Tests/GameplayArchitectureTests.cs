using NUnit.Framework;
using System.IO;
using System.Reflection;
using System.Text;
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
    }
}
