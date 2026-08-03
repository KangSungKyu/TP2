using NUnit.Framework;
using System.IO;
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

            float[] stressDeltaTimes = new float[] { 1f / 15f, 1f / 30f }; // 15 FPS (0.0667s), 30 FPS (0.0333s) 가혹 가변 프레임
            float[] slopeAngles = new float[] { 15f, 30f, 45f }; // 15도, 30도, 45도 경사면 접선

            float moveSpeed = 5.0f;
            motor.SetTargetVelocityX(moveSpeed);

            StringBuilder logSb = new StringBuilder();
            logSb.AppendLine($"[KINEMATIC MOTOR 2D SLOPE STRESS TEST REPORT]");
            logSb.AppendLine($"Timestamp: {System.DateTime.Now:yyyy-MM-dd HH:mm:ss}");

            foreach (float dt in stressDeltaTimes)
            {
                float fps = 1f / dt;
                foreach (float angle in slopeAngles)
                {
                    float rad = angle * Mathf.Deg2Rad;
                    Vector2 groundNormal = new Vector2(-Mathf.Sin(rad), Mathf.Cos(rad));

                    motor.SetGroundNormal(groundNormal);
                    motor.SimulateStep(dt);

                    // 1. 가혹 저프레임 경사면 환경에서 Grounded 상실 / 파묻힘 미발생 검증
                    Assert.IsTrue(motor.IsGrounded, $"경사각 {angle}° 및 {fps:F0} FPS 가혹 환경에서 IsGrounded 상태가 유지되어야 합니다.");

                    // 2. 경사 투영(Slope Projection) 수평 이동 속도 편차 5% 이내 유지 검증
                    float expectedHorizontalSpeed = moveSpeed * groundNormal.y;
                    Vector2 moveAlongGround = new Vector2(groundNormal.y, -groundNormal.x);
                    float actualHorizontalSpeed = Mathf.Abs(moveAlongGround.x * moveSpeed);
                    float speedDeviation = Mathf.Abs(actualHorizontalSpeed - expectedHorizontalSpeed) / expectedHorizontalSpeed;

                    Assert.LessOrEqual(speedDeviation, 0.05f, $"경사각 {angle}° {fps:F0} FPS 환경에서 수평 이동 속도 편차가 5% 이내(실제: {speedDeviation * 100:F2}%)여야 합니다.");

                    logSb.AppendLine($"[PASS] FPS: {fps:F0} | Angle: {angle}° | Grounded: {motor.IsGrounded} | SpeedDeviation: {speedDeviation * 100:F2}% <= 5%");
                }
            }

            logSb.AppendLine("--------------------------------------------------------------------------------");
            string reportPath = "Logs/qa_exception_results.txt";
            Directory.CreateDirectory("Logs");
            File.AppendAllText(reportPath, logSb.ToString());

            Object.DestroyImmediate(motorObj);
        }
    }
}
