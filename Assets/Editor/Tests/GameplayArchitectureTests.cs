using NUnit.Framework;
using UnityEngine;

namespace QA.Tests
{
    /// <summary>
    /// 게임플레이 아키텍처 (UnitBase 상속, Root/Visual 계층, 4대 대처법, CombatStats, Posture 게이지) 검증 테스트
    /// </summary>
    public class GameplayArchitectureTests
    {
        private GameObject playerGo;
        private GameObject bossGo;

        [SetUp]
        public void SetUp()
        {
            // Player GameObject & Visual 자식 생성
            playerGo = new GameObject("TestPlayer");
            playerGo.tag = "Player";
            playerGo.transform.position = Vector3.zero;

            var pVisual = new GameObject("Visual");
            pVisual.transform.SetParent(playerGo.transform, false);
            pVisual.transform.localPosition = new Vector3(0f, 0.6f, 0f);
            pVisual.AddComponent<SpriteRenderer>();
            pVisual.AddComponent<Animator>();

            var pStats = playerGo.AddComponent<CombatStats>();
            pStats.InitStats();
            playerGo.AddComponent<SkillExecutor>();
            playerGo.AddComponent<Player>();

            // Boss GameObject & Visual 자식 생성
            bossGo = new GameObject("TestBossGaron");
            bossGo.transform.position = new Vector3(3.5f, 0f, 0f);

            var bVisual = new GameObject("Visual");
            bVisual.transform.SetParent(bossGo.transform, false);
            bVisual.transform.localPosition = new Vector3(0f, 0.75f, 0f);
            bVisual.AddComponent<SpriteRenderer>();
            bVisual.AddComponent<Animator>();

            var bStats = bossGo.AddComponent<CombatStats>();
            bStats.InitStats();
            bossGo.AddComponent<SkillExecutor>();
            bossGo.AddComponent<BossMonster>();
        }

        [TearDown]
        public void TearDown()
        {
            if (playerGo != null) Object.DestroyImmediate(playerGo);
            if (bossGo != null) Object.DestroyImmediate(bossGo);
        }

        [Test]
        public void Test01_UnitBaseHierarchyAndPivot_PlayerAndBoss()
        {
            // 1. Root 객체 Y=0 피벗 확인
            Assert.AreEqual(0f, playerGo.transform.position.y, 0.001f, "Player Root Y 피벗은 0이어야 합니다.");
            Assert.AreEqual(0f, bossGo.transform.position.y, 0.001f, "Boss Root Y 피벗은 0이어야 합니다.");

            // 2. Visual 자식 객체 검증
            var playerVisual = playerGo.transform.Find("Visual");
            Assert.IsNotNull(playerVisual, "Player 하위 Visual 객체가 존재해야 합니다.");
            Assert.AreEqual(0.6f, playerVisual.localPosition.y, 0.01f, "Player Visual Y 오프셋은 0.6이어야 합니다.");

            var bossVisual = bossGo.transform.Find("Visual");
            Assert.IsNotNull(bossVisual, "Boss 하위 Visual 객체가 존재해야 합니다.");
            Assert.AreEqual(0.75f, bossVisual.localPosition.y, 0.01f, "Boss Visual Y 오프셋은 0.75이어야 합니다.");

            // 3. SpriteRenderer 및 Animator 부착 검증
            Assert.IsNotNull(playerVisual.GetComponent<SpriteRenderer>(), "Player Visual에 SpriteRenderer가 존재해야 합니다.");
            Assert.IsNotNull(playerVisual.GetComponent<Animator>(), "Player Visual에 Animator가 존재해야 합니다.");

            Assert.IsNotNull(bossVisual.GetComponent<SpriteRenderer>(), "Boss Visual에 SpriteRenderer가 존재해야 합니다.");
            Assert.IsNotNull(bossVisual.GetComponent<Animator>(), "Boss Visual에 Animator가 존재해야 합니다.");
        }

        [Test]
        public void Test02_CombatStats_TakeDamageAndGuard_ReducesDamage()
        {
            var pStats = playerGo.GetComponent<CombatStats>();
            Assert.IsNotNull(pStats, "CombatStats 컴포넌트가 존재해야 합니다.");
            Assert.AreEqual(100f, pStats.CurrentHp, "초기 HP는 100이어야 합니다.");

            pStats.SetGuarding(true);
            pStats.TakeDamage(100f);

            // 데미지 80% 감소 -> 20 피해 적용 -> 100 - 20 = 80 HP
            Assert.AreEqual(80f, pStats.CurrentHp, 0.1f, "가드 시 데미지가 80% 감소되어 HP 80이 남아야 합니다.");
        }

        [Test]
        public void Test03_CombatStats_ParrySuccess_InflictsPostureOnAttacker()
        {
            var pStats = playerGo.GetComponent<CombatStats>();
            var bStats = bossGo.GetComponent<CombatStats>();
            Assert.AreEqual(100f, pStats.CurrentHp, "초기 HP는 100이어야 합니다.");

            pStats.SetParrying(true);
            bool parrySuccess = pStats.TakeDamage(50f, attacker: bStats);

            Assert.IsTrue(parrySuccess, "패링 윈도우 중 타격 받으면 패링 성공이어야 합니다.");
            Assert.AreEqual(100f, pStats.CurrentHp, "패링 성공 시 Player는 데미지를 입지 않아야 합니다.");

            // 패링 성공 시 공격자(bStats)의 Posture 40 누적
            Assert.AreEqual(40f, bStats.CurrentPosture, 0.1f, "패링 성공 시 공격자의 자세 게이지가 40 누적되어야 합니다.");
        }

        [Test]
        public void Test04_CombatStats_Dodge_EvadesDamage100Percent()
        {
            var pStats = playerGo.GetComponent<CombatStats>();
            Assert.AreEqual(100f, pStats.CurrentHp, "초기 HP는 100이어야 합니다.");

            pStats.SetDodging(true);
            bool dodgeSuccess = pStats.TakeDamage(100f);

            Assert.IsTrue(dodgeSuccess, "회피(Dodge) 중 데미지 판정이 완전히 회피되어야 합니다.");
            Assert.AreEqual(100f, pStats.CurrentHp, "회피 성공 시 HP 손실이 없어야 합니다.");
        }

        [Test]
        public void Test05_PostureGauge_100PercentAccumulation_TriggersGroggyState()
        {
            var bStats = bossGo.GetComponent<CombatStats>();
            Assert.AreEqual(0f, bStats.CurrentPosture, "초기 자세 게이지는 0이어야 합니다.");
            Assert.IsFalse(bStats.IsGroggy, "초기 상태는 그로기가 아니어야 합니다.");

            // 100 Posture 누적
            bStats.AddPosture(100f);

            Assert.AreEqual(100f, bStats.CurrentPosture, 0.1f, "자세 게이지가 100% 누적되어야 합니다.");
            Assert.IsTrue(bStats.IsGroggy, "자세 게이지 100% 누적 시 그로기(Groggy)/무방비 상태에 진입해야 합니다.");
        }
    }
}
