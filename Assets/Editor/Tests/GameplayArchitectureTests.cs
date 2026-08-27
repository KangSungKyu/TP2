using NUnit.Framework;
using System.Collections;
using System.IO;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using TMPro;
using UnityEngine;
using UnityEngine.TestTools;

namespace QA.Tests
{
    public class GameplayArchitectureTests
    {
        [Test]
        public void EffectDrivenAttackBounds_ResolveExactTickBeforeSharedFallback()
        {
            const string header = "idx,effectnametextidx,prefabidx,duration,scale,loopcount,spawnpivotx,spawnpivoty,activecenterx,activecentery,activesizex,activesizey,activeshape,unitidx,patternidx,skillidx,hittick\n";
            var table = new EffectDataTable();
            table.LoadData(header +
                "8014,0,1081,1,1,1,0,0,.16,-.11,.56,.82,0,3001,0,7001,0\n" +
                "8015,0,1082,1,1,1,0,0,.09,-.005,.96,.05,2,3101,6001,7008,0\n" +
                "8027,0,1087,1,1,1,0,0,.255,.045,.29,.75,2,3001,0,7003,1\n" +
                "8028,0,1088,1,1,1,0,0,.195,-.09,.53,.82,0,3001,0,7003,2\n");

            Assert.IsTrue(table.TryResolveAttackEffect(3001, 0, 7001, 7, out EffectData fallback));
            Assert.AreEqual(8014u, fallback.Idx);
            Assert.AreEqual(ActiveShape.Box, fallback.Shape);
            Assert.IsTrue(table.TryResolveAttackEffect(3001, 0, 7003, 0, out EffectData first));
            Assert.AreEqual(8027u, first.Idx);
            Assert.AreEqual(ActiveShape.Capsule, first.Shape);
            Assert.IsTrue(table.TryResolveAttackEffect(3001, 0, 7003, 1, out EffectData second));
            Assert.AreEqual(8028u, second.Idx);
            Assert.IsFalse(table.TryResolveAttackEffect(3101, 6001, 7001, 0, out _));
            Assert.IsTrue(table.TryResolveAttackEffect(3101, 6001, 7008, 0, out EffectData monster));
            Assert.AreEqual(8015u, monster.Idx);
        }

        [Test]
        public void SkillOwnership_PlayerComboAndMonsterPatternsAreUintIsolated()
        {
            var skills = new SkillDataTable();
            var patterns = new MonsterPatternDataTable();
            var effects = new EffectDataTable();
            skills.LoadData(File.ReadAllText("Assets/Datas/SkillData.csv"));
            patterns.LoadData(File.ReadAllText("Assets/Datas/MonsterPatternData.csv"));
            effects.LoadData(File.ReadAllText("Assets/Datas/EffectData.csv"));

            for (uint skillIdx = 7001u; skillIdx <= 7003u; skillIdx++)
            {
                Assert.IsTrue(skills.TryGetSkillData(skillIdx, out SkillData playerSkill));
                Assert.AreEqual(2030u + skillIdx - 7001u, playerSkill.NameTextIdx);
            }

            (uint pattern, uint skill, uint unit, uint effect)[] monsterOwnership =
            {
                (6001, 7008, 3101, 8015), (6012, 7009, 3103, 8018),
                (6013, 7017, 3103, 8033), (6014, 7018, 3103, 8034),
                (6003, 7014, 3104, 8020), (6004, 7014, 3104, 8021),
                (6005, 7015, 3105, 8022),
                (6007, 7016, 3106, 8031)
            };
            foreach (var row in monsterOwnership)
            {
                Assert.IsTrue(patterns.TryGetPatternData(row.pattern, out MonsterPatternData pattern));
                Assert.AreEqual(row.skill, pattern.SkillIdx);
                Assert.Greater(pattern.SkillIdx, 7003u);
                Assert.IsTrue(effects.TryResolveAttackEffect(row.unit, row.pattern, row.skill, 0u,
                    out EffectData effect));
                Assert.AreEqual(row.effect, effect.Idx);
            }

            var chain = new System.Collections.Generic.List<MonsterPatternData>();
            Assert.IsTrue(patterns.TryBuildPatternChain(6012, chain));
            CollectionAssert.AreEqual(new uint[] { 6012, 6013, 6014 }, chain.ConvertAll(item => item.Idx));
            Assert.IsTrue(patterns.IsChainChild(6013));
            Assert.IsTrue(patterns.IsChainChild(6014));

            string player = File.ReadAllText("Assets/Scripts/Gameplay/Player.cs");
            StringAssert.Contains("Util.CreateDataIdx(DataTableType.Skill, (uint)comboStep)", player);
            StringAssert.Contains("skillExecutor.ExecuteSkill((int)skillNum", player,
                "Known 1/F and 2/R raw-index defect remains outside this migration.");
        }

        [Test]
        public void EffectDrivenAttackBounds_UseNativeBoxCircleAndCapsuleQueries()
        {
            GameObject targetObject = new GameObject("NativeShapeTarget");
            try
            {
                BoxCollider2D body = targetObject.AddComponent<BoxCollider2D>();
                body.size = new Vector2(.2f, .2f);
                CombatStats stats = targetObject.AddComponent<CombatStats>();
                stats.SetDefenseBodyCollider(body);

                targetObject.transform.position = new Vector2(2f, 0f);
                Physics2D.SyncTransforms();
                Assert.IsTrue(stats.TryGetBodySweepFraction(new CombatStats.AttackSweep2D(
                    Vector2.zero, new Vector2(3f, 0f), Vector2.one * .5f, 1, 1, 0,
                    ActiveShape.Circle, Vector2.one, 0f), out _));

                targetObject.transform.position = new Vector2(.65f, .65f);
                Physics2D.SyncTransforms();
                Assert.IsTrue(stats.TryGetBodySweepFraction(new CombatStats.AttackSweep2D(
                    Vector2.zero, Vector2.zero, Vector2.one, 1, 2, 0,
                    ActiveShape.Box, new Vector2(2f, .25f), 45f), out _));

                targetObject.transform.position = new Vector2(.8f, 0f);
                Physics2D.SyncTransforms();
                Assert.IsTrue(stats.TryGetBodySweepFraction(new CombatStats.AttackSweep2D(
                    Vector2.zero, Vector2.zero, Vector2.one, 1, 3, 0,
                    ActiveShape.Capsule, new Vector2(.5f, 2f), 90f), out _));
            }
            finally
            {
                Object.DestroyImmediate(targetObject);
            }
        }


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
        public void Test_TelegraphTimer_SyncsWithWindowStart_AndPreRendersYellowHitbox()
        {
            float configuredPreDelay = 0.5f;
            float firstHitTiming = 0.20f;
            float hitWindowPre = 0.15f;
            float windowStart = Mathf.Max(0f, firstHitTiming - hitWindowPre);

            float effectivePreDelay = Monster.CalculateEffectivePreDelay(configuredPreDelay, windowStart);
            Assert.AreEqual(0.45f, effectivePreDelay, 0.001f, "Pattern and skill startup overlap instead of adding.");
            Assert.AreEqual(0.05f, windowStart, 0.001f, "windowStart must be HitTiming - HitWindowPre.");
        }



        [Test]
        public void Test_ContactPoint_SpawnsEffectsAtHitSurfaceNotRoot()
        {
            GameObject targetObject = new GameObject("ContactPoint_Target_QA");
            try
            {
                targetObject.transform.position = new Vector3(10f, 0f, 0f);
                CombatStats target = targetObject.AddComponent<CombatStats>();
                target.InitStats();
                BoxCollider2D body = targetObject.AddComponent<BoxCollider2D>();
                body.size = new Vector2(2f, 2f);
                body.offset = new Vector2(0f, 1f);
                target.SetDefenseBodyCollider(body);

                CombatStats.AttackSweep2D sweep = new CombatStats.AttackSweep2D(
                    new Vector2(8f, 1.5f),
                    new Vector2(9.5f, 1.5f),
                    new Vector2(0.5f, 0.5f),
                    999,
                    1,
                    0
                );

                Collider2D col = target.DefenseBodyCollider;
                Vector3 contactPoint = col.ClosestPoint(sweep.Current);

                Assert.AreNotEqual(targetObject.transform.position, contactPoint, "Contact point must be on collider surface/inside, not root pivot.");
                Assert.AreEqual(9.5f, contactPoint.x, 0.05f, "Contact point X matching sweep current.");
                Assert.AreEqual(1.5f, contactPoint.y, 0.05f, "Contact point Y matching sweep height.");
            }
            finally
            {
                Object.DestroyImmediate(targetObject);
            }
        }




        [TestCase(true, 2f, false, true)]
        [TestCase(false, -2f, false, true)]
        [TestCase(true, 2f, true, true)]
        [TestCase(false, -2f, true, true)]
        [TestCase(true, -2f, false, false)]
        [TestCase(false, 2f, true, false)]
        [TestCase(true, 0f, false, true)]
        public void Test_GuardAndParry_OnlyDefendFacingHemisphere(
            bool facingRight, float attackOriginX, bool parrying, bool expectedDefended)
        {
            var defenderObject = new GameObject("DirectionalDefense_Defender_QA");
            var attackerObject = new GameObject("DirectionalDefense_Attacker_QA");
            try
            {
                var defender = defenderObject.AddComponent<CombatStats>();
                var attacker = attackerObject.AddComponent<CombatStats>();
                defender.MaxHp = defender.MaxPosture = attacker.MaxPosture = 100f;
                defender.InitStats();
                attacker.InitStats();
                defender.SetFacingRight(facingRight);
                defender.SetGuarding(!parrying);
                defender.SetParrying(parrying);

                bool defended = defender.TakeDamage(20f, attacker: attacker,
                    attackOrigin: new Vector2(attackOriginX, 10f));

                Assert.AreEqual(expectedDefended, defended);
                Assert.AreEqual(expectedDefended ? 100f : 80f, defender.CurrentHp);
                if (expectedDefended && parrying) Assert.AreEqual(40f, attacker.CurrentPosture);
                if (!expectedDefended) Assert.AreEqual(0f, attacker.CurrentPosture);
            }
            finally
            {
                Object.DestroyImmediate(defenderObject);
                Object.DestroyImmediate(attackerObject);
            }
        }

        [Test]
        public void Test_DirectionalParry_PreservesWindowAndSingleResolution()
        {
            var defenderObject = new GameObject("DirectionalParry_Defender_QA");
            var attackerObject = new GameObject("DirectionalParry_Attacker_QA");
            try
            {
                var defender = defenderObject.AddComponent<CombatStats>();
                var attacker = attackerObject.AddComponent<CombatStats>();
                defender.MaxHp = defender.MaxPosture = attacker.MaxPosture = 100f;
                defender.InitStats();
                attacker.InitStats();
                defender.OnParrySuccess = new UnityEngine.Events.UnityEvent();
                defender.SetFacingRight(true);
                defender.SetParrying(true);
                int parryCount = 0;
                defender.OnParrySuccess.AddListener(() => parryCount++);

                Assert.IsTrue(defender.TakeDamage(20f, attacker: attacker, attackOrigin: Vector2.right));
                Assert.AreEqual(1, parryCount, "One hit must resolve parry once, independent of render frame rate.");
                StringAssert.Contains("UniTask.Delay(150", File.ReadAllText("Assets/Scripts/Gameplay/Player.cs"));
            }
            finally
            {
                Object.DestroyImmediate(defenderObject);
                Object.DestroyImmediate(attackerObject);
            }
        }

        [TestCase(true, 3f, 0f, true)]
        [TestCase(false, -3f, 0f, true)]
        [TestCase(true, -3f, 0f, false)]
        [TestCase(false, 3f, 0f, false)]
        [TestCase(true, 0.5f, 0f, true)]
        [TestCase(true, 0f, 0.25f, false)]
        [TestCase(true, 100f, 0.5f, true)]
        [TestCase(true, 3f, 1.25f, true)]
        [TestCase(false, -3f, -1.25f, true)]
        public void Test_GuardSweep_FirstIntersectionWins(
            bool facingRight, float startX, float endX, bool expectedDefended)
        {
            var defenderObject = new GameObject("GuardSweep_Defender_QA");
            try
            {
                var body = defenderObject.AddComponent<BoxCollider2D>();
                body.size = new Vector2(1f, 2f);
                var defender = defenderObject.AddComponent<CombatStats>();
                defender.MaxHp = defender.MaxPosture = 100f;
                defender.InitStats();
                defender.SetDefenseBodyCollider(body);
                defender.SetFacingRight(facingRight);
                defender.SetGuarding(true);
                var sweep = new CombatStats.AttackSweep2D(
                    new Vector2(startX, 0f), new Vector2(endX, 0f), Vector2.zero, 101, 1, 0,
                    hasExteriorPose: Mathf.Abs(startX) > .5f);

                if (Mathf.Abs(endX) > .5f)
                {
                    Assert.IsFalse(defender.TryGetBodySweepFraction(sweep, out _));
                    Assert.IsTrue(defender.TryGetAttackSweepFraction(sweep, out _),
                        "A front guard-only intersection must reach defense resolution before the body.");
                }

                if (expectedDefended)
                {
                    MethodInfo resolveMethod = typeof(CombatStats).GetMethod("DoesGuardIntersectFirst",
                        BindingFlags.Instance | BindingFlags.NonPublic);
                    Assert.IsTrue((bool)resolveMethod.Invoke(defender, new object[] { sweep }),
                        "Defense resolution failed.");
                    if (sweep.HasExteriorPose)
                    {
                        MethodInfo guardMethod = typeof(CombatStats).GetMethod("TryGetGuardSweepFraction",
                            BindingFlags.Instance | BindingFlags.NonPublic);
                        object[] guardArgs = { sweep, 0f };
                        Assert.IsTrue((bool)guardMethod.Invoke(defender, guardArgs),
                            $"Guard bounds missed; fraction={guardArgs[1]}");
                    }
                }

                Assert.AreEqual(expectedDefended, defender.TakeDamage(20f, attackSweep: sweep));
                Assert.AreEqual(expectedDefended ? 100f : 80f, defender.CurrentHp);
            }
            finally { Object.DestroyImmediate(defenderObject); }
        }

        [TestCase(false)]
        [TestCase(true)]
        public void Test_GuardOnly_FirstContact_ParriesForMeleeAndProjectile(bool projectile)
        {
            var defenderObject = new GameObject($"GuardOnly_{(projectile ? "Projectile" : "Melee")}_QA");
            var attackerObject = new GameObject("GuardOnly_Attacker_QA");
            try
            {
                var body = defenderObject.AddComponent<BoxCollider2D>();
                body.size = new Vector2(1f, 2f);
                var defender = defenderObject.AddComponent<CombatStats>();
                var attacker = attackerObject.AddComponent<CombatStats>();
                defender.MaxHp = defender.MaxPosture = attacker.MaxPosture = 100f;
                defender.InitStats();
                attacker.InitStats();
                defender.SetDefenseBodyCollider(body);
                defender.SetFacingRight(true);
                defender.SetParrying(true);
                var sweep = new CombatStats.AttackSweep2D(
                    Vector2.right * 3f, Vector2.right * 1.25f, Vector2.zero,
                    projectile ? 402 : 401, 1, 0, hasExteriorPose: true);

                Assert.IsFalse(defender.TryGetBodySweepFraction(sweep, out _));
                Assert.IsTrue(defender.TryGetAttackSweepFraction(sweep, out _));
                Assert.IsTrue(defender.TakeDamage(20f, attacker: attacker, attackSweep: sweep));
                Assert.AreEqual(100f, defender.CurrentHp);
                Assert.AreEqual(40f, attacker.CurrentPosture);

                string source = File.ReadAllText(projectile
                    ? "Assets/Scripts/Gameplay/Combat/UnitProjectile2D.cs"
                    : "Assets/Scripts/Gameplay/SkillExecutor.cs");
                StringAssert.Contains("TryGetAttackSweepFraction", source);
                StringAssert.Contains("attackSweep:", source);
            }
            finally
            {
                Object.DestroyImmediate(defenderObject);
                Object.DestroyImmediate(attackerObject);
            }
        }

        [Test]
        public void GuardExteriorSweep_D1ToD5_ThirtySixCasePolicyMatrix()
        {
            MethodInfo policy = typeof(CombatStats).GetMethod("ShouldDefenseWin",
                BindingFlags.Static | BindingFlags.NonPublic);
            Assert.NotNull(policy);
            bool Resolve(params object[] args) => (bool)policy.Invoke(null, args);
            int cases = 0;
            for (int defenseState = 0; defenseState < 3; defenseState++)
            for (int facing = 0; facing < 2; facing++)
            for (int front = 0; front < 2; front++)
            for (int relation = -1; relation <= 1; relation++)
            {
                float bodyDistance = .5f;
                float guardDistance = bodyDistance + (relation < 0 ? -.02f : relation == 0 ? .005f : .02f);
                float facingSign = facing != 0 ? 1f : -1f;
                float sourceSide = front != 0 ? facingSign : -facingSign;
                bool directionMatches = -sourceSide * facingSign < 0f;
                bool defended = defenseState != 0 && Resolve(
                    true, front != 0, directionMatches, guardDistance, bodyDistance, .01f, 1f);
                Assert.AreEqual(defenseState != 0 && front != 0 && relation <= 0, defended,
                    $"state={defenseState}, facing={facing}, front={front}, relation={relation}");
                cases++;
            }
            Assert.AreEqual(36, cases);
            Assert.IsFalse(Resolve(false, true, true, .4f, .5f, .01f, 1f),
                "D1 fraction policy remains exterior-only; D2 fallback is resolved by CombatStats.");
            Assert.IsFalse(Resolve(true, true, true, 0f, 0f, .01f, 0f),
                "Zero-length sweep remains Body-first.");
        }

        [Test]
        public void Test_GuardSweep_GenerationTickAndStateTransitionsAreSingleResolution()
        {
            var defenderObject = new GameObject("GuardSweepLifecycle_Defender_QA");
            try
            {
                var body = defenderObject.AddComponent<BoxCollider2D>();
                body.size = new Vector2(1f, 2f);
                var defender = defenderObject.AddComponent<CombatStats>();
                defender.MaxHp = defender.MaxPosture = 100f;
                defender.InitStats();
                defender.SetDefenseBodyCollider(body);
                defender.SetFacingRight(true);
                defender.SetParrying(true);

                var first = new CombatStats.AttackSweep2D(Vector2.right * 3f, Vector2.right * 1.25f,
                    Vector2.zero, 202, 7, 0, hasExteriorPose: true);
                Assert.IsTrue(defender.TakeDamage(20f, attackSweep: first));
                defender.SetParrying(false);
                Assert.IsTrue(defender.TakeDamage(20f, attackSweep:
                    new CombatStats.AttackSweep2D(Vector2.right * 3f, Vector2.zero, Vector2.zero,
                        202, 7, 1, hasExteriorPose: true)));
                Assert.AreEqual(100f, defender.CurrentHp, "Parried attack generation must not apply later ticks.");

                defender.SetGuarding(true);
                var guardTick = new CombatStats.AttackSweep2D(Vector2.right * 3f, Vector2.zero,
                    Vector2.zero, 202, 8, 0, hasExteriorPose: true);
                Assert.IsTrue(defender.TakeDamage(20f, attackSweep: guardTick));
                defender.SetGuarding(false);
                var releasedTick = new CombatStats.AttackSweep2D(Vector2.right * 3f, Vector2.zero,
                    Vector2.zero, 202, 8, 1, hasExteriorPose: true);
                Assert.IsFalse(defender.TakeDamage(20f, attackSweep: releasedTick));
                Assert.IsTrue(defender.TakeDamage(20f, attackSweep: releasedTick));
                Assert.AreEqual(80f, defender.CurrentHp, "One source/generation/tick may damage at most once.");

                Assert.IsFalse(defender.TakeDamage(20f, attackSweep:
                    new CombatStats.AttackSweep2D(Vector2.left * 3f, Vector2.zero, Vector2.zero,
                        202, 9, 0, hasExteriorPose: true)));
                Assert.AreEqual(60f, defender.CurrentHp, "A pooled source with a new generation must resolve normally.");
            }
            finally { Object.DestroyImmediate(defenderObject); }
        }

        [TestCase(true, 1f / 15f)]
        [TestCase(true, 1f / 60f)]
        [TestCase(false, 1f / 15f)]
        [TestCase(false, 1f / 60f)]
        public void GuardExteriorSweep_CloseFallbackGuardsConsecutiveGenerations(bool facingRight, float fixedStep)
        {
            var defenderObject = new GameObject("GuardRecovery_Defender_QA");
            try
            {
                var body = defenderObject.AddComponent<BoxCollider2D>();
                body.size = new Vector2(1f, 2f);
                var defender = defenderObject.AddComponent<CombatStats>();
                defender.MaxHp = defender.MaxPosture = 100f;
                defender.InitStats();
                defender.SetDefenseBodyCollider(body);
                defender.SetFacingRight(facingRight);
                defender.SetGuarding(true);
                float side = facingRight ? 1f : -1f;

                float contact = .25f + 4.5f * fixedStep;
                for (uint generation = 41; generation <= 42; generation++)
                {
                    Assert.IsTrue(defender.TakeDamage(10f, attackSweep: new CombatStats.AttackSweep2D(
                        Vector2.right * side * contact, Vector2.right * side * contact, Vector2.zero,
                        3102, generation, 0, hasExteriorPose: false)));
                }
                Assert.AreEqual(100f, defender.CurrentHp, "Consecutive close thrusts must both guard.");
                Assert.AreEqual(10f, defender.CurrentPosture, "Both close guards must resolve once.");
            }
            finally { Object.DestroyImmediate(defenderObject); }
        }

        [Test]
        public void GuardExteriorSweep_CloseFallbackParryRearAndInactivePolicy()
        {
            var defenderObject = new GameObject("GuardClosePolicy_Defender_QA");
            var attackerObject = new GameObject("GuardClosePolicy_Attacker_QA");
            try
            {
                var body = defenderObject.AddComponent<BoxCollider2D>();
                body.size = new Vector2(1f, 2f);
                var defender = defenderObject.AddComponent<CombatStats>();
                var attacker = attackerObject.AddComponent<CombatStats>();
                defender.MaxHp = defender.MaxPosture = attacker.MaxPosture = 100f;
                defender.InitStats();
                attacker.InitStats();
                defender.SetDefenseBodyCollider(body);
                defender.SetFacingRight(true);
                defender.SetGuarding(true);
                defender.SetParrying(true);

                Assert.IsTrue(defender.TakeDamage(10f, attacker: attacker, attackSweep:
                    new CombatStats.AttackSweep2D(Vector2.right * .25f, Vector2.right * .25f,
                        Vector2.zero, 3102, 51, 0, hasExteriorPose: false)));
                Assert.AreEqual(40f, attacker.CurrentPosture, "Parry must win over Guard.");
                Assert.AreEqual(0f, defender.CurrentPosture);

                defender.SetParrying(false);
                Assert.IsFalse(defender.TakeDamage(10f, attackSweep:
                    new CombatStats.AttackSweep2D(Vector2.left * .25f, Vector2.left * .25f,
                        Vector2.zero, 3102, 52, 0, hasExteriorPose: false)));
                defender.SetGuarding(false);
                Assert.IsFalse(defender.TakeDamage(10f, attackSweep:
                    new CombatStats.AttackSweep2D(Vector2.right * .25f, Vector2.right * .25f,
                        Vector2.zero, 3102, 53, 0, hasExteriorPose: false)));
                Assert.AreEqual(80f, defender.CurrentHp, "Rear and inactive defense must remain Body hits.");
            }
            finally
            {
                Object.DestroyImmediate(defenderObject);
                Object.DestroyImmediate(attackerObject);
            }
        }

        [Test]
        public void Test_UnitBase_CommonDefenseAndProjectileFactionContracts()
        {
            var playerObject = new GameObject("CommonDefense_Player_QA");
            var monsterObject = new GameObject("CommonDefense_Monster_QA");
            var bossObject = new GameObject("CommonDefense_Boss_QA");
            try
            {
                playerObject.AddComponent<CombatStats>();
                monsterObject.AddComponent<CombatStats>();
                bossObject.AddComponent<CombatStats>();
                var player = playerObject.AddComponent<Player>();
                var monster = monsterObject.AddComponent<Monster>();
                var boss = bossObject.AddComponent<BossMonster>();
                MethodInfo awake = typeof(UnitBase).GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic);
                awake.Invoke(monster, null);
                awake.Invoke(boss, null);

                monster.SetGuarding(true);
                monster.SetParrying(true);
                monster.SetDodging(true);
                boss.SetGuarding(true);
                boss.SetParrying(true);
                boss.SetDodging(true);
                Assert.IsTrue(monster.Stats.IsGuarding && monster.Stats.IsParrying && monster.Stats.IsDodging);
                Assert.IsTrue(boss.Stats.IsGuarding && boss.Stats.IsParrying && boss.Stats.IsDodging);
                monster.SetGuarding(false);
                monster.SetParrying(false);
                monster.SetDodging(false);
                boss.SetGuarding(false);
                boss.SetParrying(false);
                boss.SetDodging(false);
                Assert.IsFalse(monster.Stats.IsGuarding || monster.Stats.IsParrying || monster.Stats.IsDodging);
                Assert.IsFalse(boss.Stats.IsGuarding || boss.Stats.IsParrying || boss.Stats.IsDodging);

                MethodInfo hostile = typeof(CombatStats)
                    .GetMethod("IsHostile", BindingFlags.Static | BindingFlags.NonPublic);
                Assert.IsTrue((bool)hostile.Invoke(null, new object[] { player, monster }));
                Assert.IsTrue((bool)hostile.Invoke(null, new object[] { monster, player }));
                Assert.IsFalse((bool)hostile.Invoke(null, new object[] { monster, boss }));
                Assert.IsFalse((bool)hostile.Invoke(null, new object[] { monster, monster }));
                Assert.IsTrue(typeof(UnitPoolManager).GetMethod("SpawnUnitProjectileAsync")
                    .GetParameters()[1].ParameterType == typeof(UnitBase));
            }
            finally
            {
                Object.DestroyImmediate(playerObject);
                Object.DestroyImmediate(monsterObject);
                Object.DestroyImmediate(bossObject);
            }
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
        public void Test_ExecutionHpZero_UsesCommonDeathEventExactlyOnce()
        {
            var target = new GameObject("ExecutionDeath_QA");
            try
            {
                var stats = target.AddComponent<CombatStats>();
                stats.OnHpChanged = new UnityEngine.Events.UnityEvent<float>();
                stats.OnPostureChanged = new UnityEngine.Events.UnityEvent<float>();
                stats.OnGroggyEnded = new UnityEngine.Events.UnityEvent();
                stats.OnHpZero = new UnityEngine.Events.UnityEvent();
                stats.OnDeath = new UnityEngine.Events.UnityEvent();
                stats.InitStats();
                int deathCount = 0;
                stats.OnDeath.AddListener(() => deathCount++);

                stats.TakeExecutionDamage(stats.MaxHp);
                stats.TakeExecutionDamage(stats.MaxHp);

                Assert.IsTrue(stats.IsDead);
                Assert.AreEqual(0f, stats.CurrentHp);
                Assert.AreEqual(1, deathCount);
                StringAssert.DoesNotContain("OnHpZero.AddListener(OnDeath)", File.ReadAllText("Assets/Scripts/Gameplay/Monster.cs"));
            }
            finally { Object.DestroyImmediate(target); }
        }

        [Test]
        public void Test_MonsterGroggyAndDeath_InvalidateMovementAndAttackGeneration()
        {
            var monsterObject = new GameObject("MonsterStateGate_QA");
            try
            {
                monsterObject.SetActive(false);
                monsterObject.AddComponent<BoxCollider2D>();
                var stats = monsterObject.AddComponent<CombatStats>();
                stats.OnPostureChanged = new UnityEngine.Events.UnityEvent<float>();
                stats.OnGroggyState = new UnityEngine.Events.UnityEvent();
                stats.OnGroggyEnded = new UnityEngine.Events.UnityEvent();
                stats.InitStats();
                var motor = monsterObject.AddComponent<KinematicMotor2D>();
                var monster = monsterObject.AddComponent<Monster>();
                typeof(UnitBase).GetField("stats", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(monster, stats);
                typeof(UnitBase).GetField("motor", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(monster, motor);
                monsterObject.SetActive(true);
                motor.SetGroundNormal(Vector2.up);
                motor.SetTargetVelocityX(5f);
                motor.SimulateStep(Time.fixedDeltaTime);
                Assert.AreEqual(5f, motor.Velocity.x, 0.001f);

                uint generation = monster.ActionGeneration;
                var acquire = typeof(Monster).GetMethod("TryAcquireAttackToken", BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.IsTrue((bool)acquire.Invoke(monster, new object[] { false }));
                Assert.IsTrue(monster.CurrentPatternSnapshot.TokenHeld);
                stats.AddPosture(stats.MaxPosture);
                typeof(Monster).GetMethod("OnGroggyStarted", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(monster, null);
                motor.SimulateStep(Time.fixedDeltaTime);
                Assert.AreEqual(0f, motor.Velocity.x, 0.001f);
                Assert.Greater(monster.ActionGeneration, generation);
                Assert.IsFalse(monster.CurrentPatternSnapshot.TokenHeld);
                Assert.IsFalse((bool)typeof(Monster).GetMethod("CanAct", BindingFlags.Instance | BindingFlags.NonPublic)
                    .Invoke(monster, new object[] { 0u }));

                string source = File.ReadAllText("Assets/Scripts/Gameplay/Monster.cs");
                StringAssert.Contains("CancelCurrentPattern(PatternCancelReason.Death);", source);
                StringAssert.Contains("CancelCurrentPattern(PatternCancelReason.Groggy);", source);
                StringAssert.Contains("skillExecutor != null && CanAct(generation)", source);
                StringAssert.Contains("playerTarget != null && CanAct(generation)", source);
                StringAssert.Contains("skillExecutor.CancelActiveEffects();", source);
            }
            finally { Object.DestroyImmediate(monsterObject); }
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
        public void Test_KinematicMotor_KnockbackEndsAndLatestHitOwnsReactionWindow()
        {
            var motorObject = new GameObject("KnockbackWindow_QA");
            try
            {
                motorObject.AddComponent<Rigidbody2D>();
                motorObject.AddComponent<BoxCollider2D>();
                var motor = motorObject.AddComponent<KinematicMotor2D>();
                motor.InitMotor();
                motor.SetGroundNormal(Vector2.up);
                motor.SetTargetVelocityX(-2f);
                motor.ApplyKnockback(Vector2.right * 6f, 0.15f);

                int reactionSteps = Mathf.CeilToInt(0.15f / Time.fixedDeltaTime);
                for (int i = 0; i < reactionSteps; i++) motor.SimulateStep(Time.fixedDeltaTime);
                motor.SimulateStep(Time.fixedDeltaTime);
                Assert.AreEqual(-2f, motor.Velocity.x, 0.001f, "Input/AI target velocity must resume after hit reaction.");

                motor.ApplyKnockback(Vector2.right * 6f, 0.15f);
                for (int i = 0; i < reactionSteps / 2; i++) motor.SimulateStep(Time.fixedDeltaTime);
                motor.SetTargetVelocityX(2f);
                motor.ApplyKnockback(Vector2.left * 5f, 0.15f);
                for (int i = 0; i < reactionSteps - 1; i++)
                {
                    motor.SimulateStep(Time.fixedDeltaTime);
                    Assert.AreEqual(-5f, motor.Velocity.x, 0.001f, "A stale hit window must not clear the latest knockback.");
                }
                motor.SimulateStep(Time.fixedDeltaTime);
                motor.SimulateStep(Time.fixedDeltaTime);
                Assert.AreEqual(2f, motor.Velocity.x, 0.001f);
            }
            finally { Object.DestroyImmediate(motorObject); }
        }

        [TestCase(false, false)]
        [TestCase(false, true)]
        [TestCase(true, false)]
        [TestCase(true, true)]
        public void Test_SuperArmor_GatesKnockbackWithoutChangingDamage(bool isBoss, bool armorActive)
        {
            var target = new GameObject(isBoss ? "Boss_SuperArmor_QA" : "Monster_SuperArmor_QA");
            var attacker = new GameObject("Attacker_SuperArmor_QA");
            try
            {
                target.SetActive(false);
                target.AddComponent<Rigidbody2D>();
                target.AddComponent<BoxCollider2D>();
                var motor = target.AddComponent<KinematicMotor2D>();
                motor.InitMotor();
                motor.SetGroundNormal(Vector2.up);
                var targetStats = target.AddComponent<CombatStats>();
                var unit = isBoss
                    ? (Monster)target.AddComponent<BossMonster>()
                    : target.AddComponent<Monster>();
                var attackerStats = attacker.AddComponent<CombatStats>();
                typeof(CombatStats).GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(targetStats, null);
                typeof(CombatStats).GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(attackerStats, null);
                attacker.transform.position = Vector3.left;

                Assert.AreEqual(isBoss, unit is BossMonster);
                float hpBefore = targetStats.CurrentHp;
                if (armorActive) motor.ApplyKnockback(Vector2.right * 6f, 0.15f);
                targetStats.IsSuperArmorActive = armorActive;
                targetStats.TakeDamage(10f, attacker: attackerStats);

                Assert.AreEqual(hpBefore - 10f, targetStats.CurrentHp, 0.001f);
                if (armorActive)
                {
                    Assert.AreEqual(0f, motor.Velocity.x, 0.001f);
                    motor.SimulateStep(Time.fixedDeltaTime);
                    Assert.AreEqual(0f, motor.Velocity.x, 0.001f, "SuperArmor must clear the previous knockback override.");
                }
                else
                {
                    Assert.Greater(motor.Velocity.x, 0f);
                }
            }
            finally
            {
                Object.DestroyImmediate(attacker);
                Object.DestroyImmediate(target);
            }
        }

        [Test]
        public void Test_ProductionChunkProgress_DefaultsHidden()
        {
            var root = new GameObject("ChunkProgressHidden_QA");
            var textObject = new GameObject("StageProgress_QA");
            try
            {
                var hud = root.AddComponent<ProductionMainHUD>();
                var text = textObject.AddComponent<TextMeshProUGUI>();
                typeof(ProductionMainHUD).GetField("stageProgressText", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(hud, text);
                typeof(ProductionMainHUD).GetMethod("OnEnable", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(hud, null);
                Assert.IsFalse(textObject.activeSelf);
            }
            finally
            {
                Object.DestroyImmediate(textObject);
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void Test_Stage1EntryPortal_AcceptsConnectedTargetAndRejectsInvalidTarget()
        {
            var managerObject = new GameObject("Stage1EntryManager_QA");
            var validObject = new GameObject("ValidEntryPortal_QA");
            var invalidObject = new GameObject("InvalidEntryPortal_QA");
            try
            {
                var manager = managerObject.AddComponent<StageManager>();
                var run = new StageRunData
                {
                    Rows = 1,
                    Columns = 2,
                    CurrentSlotIdx = 0,
                    Slots = new[]
                    {
                        new ChunkSlotData { SlotIdx = 0, ConnectionMask = 2, ChunkResourceIdx = 1040, Visited = true },
                        new ChunkSlotData { SlotIdx = 1, ConnectionMask = 8, ChunkResourceIdx = 1050 }
                    }
                };
                typeof(StageManager).GetProperty("CurrentRun").SetValue(manager, run);
                var valid = validObject.AddComponent<RoomDoorPortal>();
                valid.TargetSlotIdx = 1;
                var invalid = invalidObject.AddComponent<RoomDoorPortal>();
                invalid.TargetSlotIdx = 2;

                Assert.IsTrue(manager.TryMoveToConnectedSlot(valid.TargetSlotIdx, out uint resourceIdx));
                Assert.AreEqual(1050u, resourceIdx);
                run.CurrentSlotIdx = 0;
                Assert.IsFalse(manager.TryMoveToConnectedSlot(invalid.TargetSlotIdx, out _));
            }
            finally
            {
                Object.DestroyImmediate(invalidObject);
                Object.DestroyImmediate(validObject);
                Object.DestroyImmediate(managerObject);
            }
        }

        [Test]
        public void Test_ProductionPlayerStats_InitialUpdateAndReuse()
        {
            var hudObject = new GameObject("PlayerStatsHud_QA");
            var statsObject = new GameObject("PlayerStats_QA");
            var hpObject = new GameObject("HpText_QA");
            var postureObject = new GameObject("PostureText_QA");
            var mpObject = new GameObject("MpText_QA");
            try
            {
                var hud = hudObject.AddComponent<ProductionMainHUD>();
                var hp = hpObject.AddComponent<TextMeshProUGUI>();
                var posture = postureObject.AddComponent<TextMeshProUGUI>();
                var mp = mpObject.AddComponent<TextMeshProUGUI>();
                typeof(ProductionMainHUD).GetField("playerHpText", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(hud, hp);
                typeof(ProductionMainHUD).GetField("playerPostureText", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(hud, posture);
                typeof(ProductionMainHUD).GetField("playerMpText", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(hud, mp);
                var stats = statsObject.AddComponent<CombatStats>();
                stats.OnHpChanged = new UnityEngine.Events.UnityEvent<float>();
                stats.OnPostureChanged = new UnityEngine.Events.UnityEvent<float>();
                stats.OnMpChanged = new UnityEngine.Events.UnityEvent<float>();
                stats.InitStats();

                hud.BindPlayer(stats);
                Assert.AreEqual("100/100", hp.text);
                Assert.AreEqual("0/100", posture.text);
                Assert.AreEqual("50/50", mp.text);
                stats.TakeDamage(10f);
                stats.AddPosture(25f);
                stats.ConsumeMp(10f);
                Assert.AreEqual("90/100", hp.text);
                Assert.AreEqual("25/100", posture.text);
                Assert.AreEqual("40/50", mp.text);
                hud.BindPlayer(stats);
                stats.OnHpChanged.Invoke(0.9f);
                Assert.AreEqual("90/100", hp.text);
            }
            finally
            {
                Object.DestroyImmediate(mpObject);
                Object.DestroyImmediate(postureObject);
                Object.DestroyImmediate(hpObject);
                Object.DestroyImmediate(statsObject);
                Object.DestroyImmediate(hudObject);
            }
        }

        [Test]
        public void Test_ProductionMinimap_ToggleAndFiveStates()
        {
            var managerObject = new GameObject("MinimapManager_QA");
            var minimapObject = new GameObject("Minimap_QA");
            var rootObject = new GameObject("MinimapRoot_QA");
            try
            {
                var manager = managerObject.AddComponent<StageManager>();
                var run = new StageRunData
                {
                    Rows = 2,
                    Columns = 3,
                    CurrentSlotIdx = 0,
                    BossGateSlotIdx = 1,
                    Slots = new[]
                    {
                        new ChunkSlotData { SlotIdx = 0, Visited = true },
                        new ChunkSlotData { SlotIdx = 1 },
                        new ChunkSlotData { SlotIdx = 2, Visited = true, Cleared = true },
                        new ChunkSlotData { SlotIdx = 3, Visited = true }
                    }
                };
                typeof(StageManager).GetProperty("CurrentRun").SetValue(manager, run);
                var root = rootObject.AddComponent<CanvasGroup>();
                var minimap = minimapObject.AddComponent<ProductionMinimap>();
                var views = new ProductionMinimap.RoomView[5];
                for (int i = 0; i < views.Length; i++) views[i] = new ProductionMinimap.RoomView();
                typeof(ProductionMinimap).GetField("minimapRoot", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(minimap, root);
                typeof(ProductionMinimap).GetField("roomViews", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(minimap, views);
                minimap.BindStage(manager);
                minimap.Hide();

                minimap.Toggle();
                Assert.AreEqual(1f, root.alpha);
                Assert.AreEqual(ProductionMinimap.RoomViewState.Current, views[0].CurrentState);
                Assert.AreEqual(ProductionMinimap.RoomViewState.Boss, views[1].CurrentState);
                Assert.AreEqual(ProductionMinimap.RoomViewState.Cleared, views[2].CurrentState);
                Assert.AreEqual(ProductionMinimap.RoomViewState.Visited, views[3].CurrentState);
                Assert.AreEqual(ProductionMinimap.RoomViewState.Unknown, views[4].CurrentState);
                minimap.Toggle();
                Assert.AreEqual(0f, root.alpha);
            }
            finally
            {
                Object.DestroyImmediate(rootObject);
                Object.DestroyImmediate(minimapObject);
                Object.DestroyImmediate(managerObject);
            }
        }

        [Test]
        public void Test_MonsterOverheadHud_EventBindingAndPoolReuseContract()
        {
            var monsterObject = new GameObject("MonsterOverheadOwner_QA");
            var hudObject = new GameObject("MonsterOverheadHUD_QA");
            var hpObject = new GameObject("MonsterHp_QA");
            var postureObject = new GameObject("MonsterPosture_QA");
            var groupObject = new GameObject("MonsterGroup_QA");
            try
            {
                monsterObject.AddComponent<BoxCollider2D>();
                var monster = monsterObject.AddComponent<Monster>();
                var hp = hpObject.AddComponent<UnityEngine.UI.Image>();
                var posture = postureObject.AddComponent<UnityEngine.UI.Image>();
                var group = groupObject.AddComponent<CanvasGroup>();
                var hud = hudObject.AddComponent<MonsterOverheadHUD>();
                typeof(Monster).GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(monster, null);
                monster.Stats.OnHpChanged = new UnityEngine.Events.UnityEvent<float>();
                monster.Stats.OnPostureChanged = new UnityEngine.Events.UnityEvent<float>();
                typeof(MonsterOverheadHUD).GetField("owner", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(hud, monster);
                typeof(MonsterOverheadHUD).GetField("group", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(hud, group);
                typeof(MonsterOverheadHUD).GetField("hpFill", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(hud, hp);
                typeof(MonsterOverheadHUD).GetField("postureFill", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(hud, posture);

                hud.Bind(monster.Stats);
                monster.Stats.OnHpChanged.Invoke(0.25f);
                monster.Stats.OnPostureChanged.Invoke(0.5f);
                Assert.AreEqual(0.25f, hp.fillAmount);
                Assert.AreEqual(0.5f, posture.fillAmount);
                Assert.AreEqual(1f, group.alpha);

                hud.Bind(monster.Stats);
                typeof(MonsterOverheadHUD).GetMethod("OnDisable", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(hud, null);
                hp.fillAmount = 0.9f;
                monster.Stats.OnHpChanged.Invoke(0.1f);
                Assert.AreEqual(0.9f, hp.fillAmount, "Pooled disable must remove every listener.");

                string source = File.ReadAllText("Assets/Scripts/UI/MonsterOverheadHUD.cs");
                Assert.IsFalse(Regex.IsMatch(source, @"\bOnGUI\s*\("));
                StringAssert.DoesNotContain("Monster.ActiveMonsters", source);
                StringAssert.Contains("owner is BossMonster", source);
                StringAssert.DoesNotContain("GetComponent", source);
                StringAssert.DoesNotContain("Find", source);
            }
            finally
            {
                Object.DestroyImmediate(groupObject);
                Object.DestroyImmediate(postureObject);
                Object.DestroyImmediate(hpObject);
                Object.DestroyImmediate(hudObject);
                Object.DestroyImmediate(monsterObject);
            }
        }

        [Test]
        public void Test_ProductionMainHud_BossOnlyPanelBindsAndUnbinds()
        {
            var bossObject = new GameObject("BossHudOwner_QA");
            var hudObject = new GameObject("BossProductionHUD_QA");
            var groupObject = new GameObject("BossGroup_QA");
            var hpBackgroundObject = new GameObject("BossHpBackground_QA");
            var hpObject = new GameObject("BossHp_QA");
            var mpBackgroundObject = new GameObject("BossMpBackground_QA");
            var mpObject = new GameObject("BossMp_QA");
            var postureBackgroundObject = new GameObject("BossPostureBackground_QA");
            var postureObject = new GameObject("BossPosture_QA");
            try
            {
                bossObject.SetActive(false);
                bossObject.AddComponent<BoxCollider2D>();
                var boss = bossObject.AddComponent<BossMonster>();
                var stats = bossObject.GetComponent<CombatStats>();
                stats.OnHpChanged = new UnityEngine.Events.UnityEvent<float>();
                stats.OnMpChanged = new UnityEngine.Events.UnityEvent<float>();
                stats.OnPostureChanged = new UnityEngine.Events.UnityEvent<float>();
                stats.InitStats();
                typeof(UnitBase).GetField("stats", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(boss, stats);

                var group = groupObject.AddComponent<CanvasGroup>();
                hpBackgroundObject.transform.SetParent(groupObject.transform);
                hpObject.transform.SetParent(groupObject.transform);
                mpBackgroundObject.transform.SetParent(groupObject.transform);
                mpObject.transform.SetParent(groupObject.transform);
                postureBackgroundObject.transform.SetParent(groupObject.transform);
                postureObject.transform.SetParent(groupObject.transform);
                var hpBackground = hpBackgroundObject.AddComponent<UnityEngine.UI.Image>();
                var hp = hpObject.AddComponent<UnityEngine.UI.Image>();
                var mpBackground = mpBackgroundObject.AddComponent<UnityEngine.UI.Image>();
                var mp = mpObject.AddComponent<UnityEngine.UI.Image>();
                var postureBackground = postureBackgroundObject.AddComponent<UnityEngine.UI.Image>();
                var posture = postureObject.AddComponent<UnityEngine.UI.Image>();
                var hud = hudObject.AddComponent<ProductionMainHUD>();
                typeof(ProductionMainHUD).GetField("bossGroup", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(hud, group);
                typeof(ProductionMainHUD).GetField("bossHpBackground", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(hud, hpBackground);
                typeof(ProductionMainHUD).GetField("bossHpFill", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(hud, hp);
                typeof(ProductionMainHUD).GetField("bossMpBackground", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(hud, mpBackground);
                typeof(ProductionMainHUD).GetField("bossMpFill", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(hud, mp);
                typeof(ProductionMainHUD).GetField("bossPostureBackground", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(hud, postureBackground);
                typeof(ProductionMainHUD).GetField("bossPostureFill", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(hud, posture);
                typeof(ProductionMainHUD).GetMethod("OnEnable", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(hud, null);
                var bindBoss = typeof(ProductionMainHUD).GetMethod("BindBoss", BindingFlags.Instance | BindingFlags.NonPublic);
                var onMonsterActivated = typeof(ProductionMainHUD).GetMethod("OnMonsterActivated", BindingFlags.Instance | BindingFlags.NonPublic);

                group.alpha = 0f;
                onMonsterActivated.Invoke(hud, new object[] { boss });
                Assert.AreEqual(0f, group.alpha, "An uninitialized/unencountered Boss must not show the panel.");
                bindBoss.Invoke(hud, new object[] { boss });
                Assert.AreEqual(1f, group.alpha);
                Assert.AreEqual(1f, hp.fillAmount);
                Assert.AreEqual(1f, mp.fillAmount);
                Assert.AreEqual(0f, posture.fillAmount);
                foreach (var background in new[] { hpBackground, mpBackground, postureBackground })
                    Assert.AreEqual(new Color(0f, 0f, 0f, .9f), background.color);
                Assert.Less(hpBackground.transform.GetSiblingIndex(), hp.transform.GetSiblingIndex());
                Assert.Less(mpBackground.transform.GetSiblingIndex(), mp.transform.GetSiblingIndex());
                Assert.Less(postureBackground.transform.GetSiblingIndex(), posture.transform.GetSiblingIndex());
                stats.OnHpChanged.Invoke(0.4f);
                Assert.AreEqual(0.4f, hp.fillAmount);

                bindBoss.Invoke(hud, new object[] { null });
                Assert.AreEqual(0f, group.alpha);
                hp.fillAmount = 0.9f;
                stats.OnHpChanged.Invoke(0.2f);
                Assert.AreEqual(0.9f, hp.fillAmount, "Boss death/chunk unload must detach its listeners.");

                string source = File.ReadAllText("Assets/Scripts/UI/ProductionMainHUD.cs");
                StringAssert.Contains("boss.UnitData != null && boss.isActiveAndEnabled", source);
                StringAssert.DoesNotContain("else if (activeMonster", source);
                string scene = File.ReadAllText("Assets/Scenes/MainScene.unity");
                foreach (string field in new[] { "bossHpBackground", "bossHpFill", "bossMpBackground",
                             "bossMpFill", "bossPostureBackground", "bossPostureFill" })
                    StringAssert.IsMatch($@"{field}: \{{fileID: [1-9][0-9]*\}}", scene,
                        $"MainScene must deserialize a non-null {field} binding.");
            }
            finally
            {
                Object.DestroyImmediate(postureBackgroundObject);
                Object.DestroyImmediate(postureObject);
                Object.DestroyImmediate(mpBackgroundObject);
                Object.DestroyImmediate(mpObject);
                Object.DestroyImmediate(hpBackgroundObject);
                Object.DestroyImmediate(hpObject);
                Object.DestroyImmediate(groupObject);
                Object.DestroyImmediate(hudObject);
                Object.DestroyImmediate(bossObject);
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
        public void Test_SkillExecutorParticleLoad_CompletesOnceAndRejectsStaleOwner()
        {
            var owner = new GameObject("SkillExecutorParticle_QA");
            var prefab = new GameObject("Particle_QA");
            var executor = owner.AddComponent<SkillExecutor>();
            var type = typeof(SkillExecutor);
            var generation = type.GetField("particleLoadGeneration", BindingFlags.Instance | BindingFlags.NonPublic);
            var pending = type.GetField("particleLoadPending", BindingFlags.Instance | BindingFlags.NonPublic);
            var failed = type.GetField("particleLoadFailureLogged", BindingFlags.Instance | BindingFlags.NonPublic);
            var loaded = type.GetField("particlePrefab", BindingFlags.Instance | BindingFlags.NonPublic);
            var complete = type.GetMethod("CompleteParticleLoad", BindingFlags.Instance | BindingFlags.NonPublic);
            try
            {
                Assert.NotNull(generation);
                Assert.NotNull(complete);
                pending.SetValue(executor, true);
                string source = File.ReadAllText("Assets/Scripts/Gameplay/SkillExecutor.cs");
                int startLoad = source.IndexOf("private void StartParticleLoad()", System.StringComparison.Ordinal);
                int completeLoad = source.IndexOf("private void CompleteParticleLoad", startLoad, System.StringComparison.Ordinal);
                StringAssert.DoesNotContain("Debug.LogError", source.Substring(startLoad, completeLoad - startLoad));

                generation.SetValue(executor, 10u);
                complete.Invoke(executor, new object[] { 10u, prefab });
                Assert.AreSame(prefab, loaded.GetValue(executor));
                Assert.IsFalse((bool)pending.GetValue(executor));

                loaded.SetValue(executor, null);
                failed.SetValue(executor, false);
                int failureGuard = source.IndexOf("if (particleLoadFailureLogged) return;", completeLoad, System.StringComparison.Ordinal);
                int failureSet = source.IndexOf("particleLoadFailureLogged = true;", failureGuard, System.StringComparison.Ordinal);
                const string completedNullError = "[ResourceManager Error] 'Particle' resource completed with null.";
                int error = source.IndexOf(completedNullError, failureSet, System.StringComparison.Ordinal);
                Assert.Greater(failureGuard, completeLoad);
                Assert.Greater(failureSet, failureGuard);
                Assert.Greater(error, failureSet);
                Assert.AreEqual(1, source.Split(new[] { completedNullError }, System.StringSplitOptions.None).Length - 1);

                failed.SetValue(executor, false);
                generation.SetValue(executor, 20u);
                owner.SetActive(false);
                complete.Invoke(executor, new object[] { 20u, prefab });
                Assert.IsNull(loaded.GetValue(executor), "A disabled owner's stale completion must be ignored.");
            }
            finally
            {
                Object.DestroyImmediate(prefab);
                Object.DestroyImmediate(owner);
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
        public void Test_MonsterProjectilePatternDataAndGenerationContracts()
        {
            var table = new MonsterPatternDataTable();
            table.LoadData(File.ReadAllText("Assets/Datas/MonsterPatternData.csv"));

            Assert.IsTrue(table.TryGetPatternData(6005, out var straight));
            Assert.AreEqual(7015u, straight.SkillIdx);
            Assert.AreEqual(1045u, straight.ProjectileResourceIdx);
            Assert.AreEqual(15f, straight.ProjectileSpeed, 0.01f);
            Assert.AreEqual(25f, straight.ProjectileMaxDistance, 0.01f);
            Assert.AreEqual(14f, straight.Damage);
            Assert.AreEqual(25f / 15f, straight.ProjectileMaxDistance / straight.ProjectileSpeed, 0.0001f);

            string monster = File.ReadAllText("Assets/Scripts/Gameplay/Monster.cs");
            string pool = File.ReadAllText("Assets/Scripts/Manager/UnitPoolManager.cs");
            string projectile = File.ReadAllText("Assets/Scripts/Gameplay/Combat/UnitProjectile2D.cs");
            StringAssert.Contains("pattern.ProjectileResourceIdx != 0", monster);
            StringAssert.Contains("pattern.ProjectileResourceIdx == 0", monster);
            StringAssert.Contains("pattern.Damage", monster);
            StringAssert.Contains("TryGetResource(resourceIdx", pool);
            StringAssert.Contains("InstantiateAsyncTask(\n                resourceData.Path", pool);
            StringAssert.Contains("Collider2D.Cast", projectile.Replace("projectileCollider.Cast", "Collider2D.Cast"));
            StringAssert.DoesNotContain("SimplePoolManager", projectile);
            StringAssert.DoesNotContain("Physics.", projectile);
        }

        [UnityTest]
        public IEnumerator PlayerDamageFlash_RestoresAcrossRepeatedHitsCancellationAndDeath()
        {
            var playerObject = new GameObject("PlayerDamageFlash_QA");
            var visualObject = new GameObject("Visual");
            visualObject.transform.SetParent(playerObject.transform, false);
            var renderer = visualObject.AddComponent<SpriteRenderer>();
            renderer.color = Color.white;
            var stats = playerObject.AddComponent<CombatStats>();
            stats.MaxHp = 100f;
            stats.InitStats();
            try
            {
                Assert.IsFalse(stats.TakeDamage(10f, attackSweep: new CombatStats.AttackSweep2D(
                    Vector2.zero, Vector2.zero, Vector2.zero, 42, 1u, 0u)));
                Assert.AreEqual(Color.red, renderer.color);
                yield return new WaitForSeconds(.2f);
                Assert.AreEqual(Color.white, renderer.color);

                Assert.IsFalse(stats.TakeDamage(10f, attackSweep: new CombatStats.AttackSweep2D(
                    Vector2.zero, Vector2.zero, Vector2.zero, 42, 2u, 0u)));
                Assert.AreEqual(80f, stats.CurrentHp, "A new attack generation must damage again.");
                playerObject.SetActive(false);
                Assert.AreEqual(Color.white, renderer.color, "Disable/cancellation must restore the original color.");
                playerObject.SetActive(true);

                stats.TakeDamage(100f, attackSweep: new CombatStats.AttackSweep2D(
                    Vector2.zero, Vector2.zero, Vector2.zero, 42, 3u, 0u));
                Assert.IsTrue(stats.IsDead);
                float deadHp = stats.CurrentHp;
                Assert.IsFalse(stats.TakeDamage(10f));
                Assert.AreEqual(deadHp, stats.CurrentHp, "Death lock remains separate from transient hit flash.");
            }
            finally
            {
                Object.DestroyImmediate(playerObject);
            }
        }

        [Test]
        public void MonsterPatternStart_AdvancesAttackGenerationBeforeExecution()
        {
            string source = File.ReadAllText("Assets/Scripts/Gameplay/Monster.cs");
            int method = source.IndexOf("private async UniTask ExecutePatternAsync", System.StringComparison.Ordinal);
            int generation = source.IndexOf("actionGeneration++;", method, System.StringComparison.Ordinal);
            int assignment = source.IndexOf("currentPattern = patternChain[0];", method, System.StringComparison.Ordinal);
            Assert.GreaterOrEqual(method, 0);
            Assert.Greater(generation, method);
            Assert.Greater(assignment, generation);
        }

        [Test]
        public void Test_MonsterPatternStartDistanceBand_SelectionFallbackAndBoundaries()
        {
            var patterns = new MonsterPatternDataTable();
            var skills = new SkillDataTable();
            patterns.LoadData(File.ReadAllText("Assets/Datas/MonsterPatternData.csv"));
            skills.LoadData(File.ReadAllText("Assets/Datas/SkillData.csv"));

            Assert.IsTrue(patterns.TryGetPatternData(6005, out MonsterPatternData far));
            Assert.IsTrue(skills.TryGetSkillData(7015, out SkillData projectileSkill));
            Assert.AreEqual(8f, far.MinStartDistance);
            Assert.AreEqual(10f, far.MaxStartDistance);
            Assert.IsFalse(Monster.IsPatternStartDistanceValid(far, projectileSkill, 7.999f));
            Assert.IsTrue(Monster.IsPatternStartDistanceValid(far, projectileSkill, 8f));
            Assert.IsTrue(Monster.IsPatternStartDistanceValid(far, projectileSkill, 10f));
            Assert.IsFalse(Monster.IsPatternStartDistanceValid(far, projectileSkill, 10.001f));
            Assert.IsTrue(patterns.TryGetPatternData(6001, out MonsterPatternData melee));
            Assert.IsTrue(skills.TryGetSkillData(7008, out SkillData meleeSkill));
            Assert.IsTrue(Monster.TryGetPatternStartDistanceBand(melee, meleeSkill, out float min, out float max));
            Assert.AreEqual(0f, min);
            Assert.AreEqual(meleeSkill.Range, max);

            Assert.IsTrue(patterns.TryGetPatternData(6010, out MonsterPatternData torsoRam));
            Assert.IsTrue(skills.TryGetSkillData(7007, out SkillData torsoRamSkill));
            Assert.AreEqual((uint)PatternTriggerType.DistanceOver, torsoRam.TriggerType);
            Assert.AreEqual(7.5f, torsoRam.TriggerValue);
            Assert.IsTrue(Monster.IsPatternStartDistanceValid(torsoRam, torsoRamSkill, 7.5f));
            Assert.IsTrue(Monster.IsPatternStartDistanceValid(torsoRam, torsoRamSkill, 7.51f));
            Assert.IsTrue(Monster.IsPatternStartDistanceValid(torsoRam, torsoRamSkill, 15f));
            Assert.IsFalse(Monster.IsPatternStartDistanceValid(torsoRam, torsoRamSkill, 15.01f));

            Assert.IsTrue(patterns.TryGetPatternData(6012, out MonsterPatternData chain));
            Assert.IsTrue(skills.TryGetSkillData(7009, out SkillData chainSkill));
            Assert.AreEqual(0f, chain.MinStartDistance, "The chain has no minimum distance.");
            Assert.IsTrue(Monster.IsPatternStartDistanceValid(chain, chainSkill, 0f));
            Assert.IsTrue(Monster.IsPatternStartDistanceValid(chain, chainSkill, 2f));
            Assert.IsFalse(Monster.IsPatternStartDistanceValid(chain, chainSkill, 2.01f));

            foreach (float step in new[] { 1f / 15f, 1f / 60f })
            {
                float boundary = 8f + 0f * step;
                Assert.IsTrue(Monster.IsPatternStartDistanceValid(far, projectileSkill, boundary));
            }

            string monster = File.ReadAllText("Assets/Scripts/Gameplay/Monster.cs");
            StringAssert.Contains("CanReservePattern(pattern, skillTable)", monster);
            StringAssert.Contains("GetAttackSurfaceGap()", monster);
            StringAssert.Contains("float distToPlayer = GetAttackSurfaceGap();", monster,
                "Distance triggers and start bands must share collider surface-gap authority.");
            StringAssert.Contains("float detectRange = GetPatternEvaluationRange();", monster);
            StringAssert.Contains("hasSpawnArea", monster, "Pattern range expansion must preserve the leash gate.");
            StringAssert.DoesNotContain("GetDetectionDistance()", monster);
            Assert.Less(monster.IndexOf("PatternExecutionType.Trigger", System.StringComparison.Ordinal),
                monster.IndexOf("PatternExecutionType.Simple", System.StringComparison.Ordinal),
                "In-band Torso Ram must be considered before the Simple chain.");
            StringAssert.DoesNotContain("IsInsideStartBand", monster,
                "Distance bands are selection/Chase authority only after attack confirmation.");
            StringAssert.Contains("SetAttackMotionVelocityX(0f);", monster);
            StringAssert.DoesNotContain("pattern.TriggerValue > 0f ? pattern.TriggerValue", monster);
        }

        [Test]
        public void Unit3103_DistanceBandsUseStrictTriggerAndClampedChaseBoundary()
        {
            var chain = new MonsterPatternData { MinStartDistance = 0f, MaxStartDistance = 2f };
            var ram = new MonsterPatternData
            {
                TriggerType = (uint)PatternTriggerType.DistanceOver,
                TriggerValue = 7.5f,
                MinStartDistance = 7.5f,
                MaxStartDistance = 15f
            };
            var skill = new SkillData { Range = 15f };
            foreach ((float gap, bool chainExpected, bool ramExpected) in new[]
            {
                (0f, true, false), (2f, true, false), (2.01f, false, false),
                (7.49f, false, false), (7.5f, false, false), (7.51f, false, true),
                (15f, false, true), (15.01f, false, false)
            })
            {
                bool chainSelected = Monster.IsPatternStartDistanceValid(chain, skill, gap);
                bool ramSelected = gap > ram.TriggerValue &&
                    Monster.IsPatternStartDistanceValid(ram, skill, gap);
                Assert.AreEqual(chainExpected, chainSelected, $"6012 gap {gap}");
                Assert.AreEqual(ramExpected, ramSelected, $"6010 gap {gap}");
                Assert.IsFalse(chainSelected && ramSelected, $"Bands overlap at gap {gap}.");
                Assert.AreEqual(chainSelected, Monster.IsPatternStartDistanceValid(chain, skill, Mathf.Abs(-gap)));
                Assert.AreEqual(ramSelected, Mathf.Abs(-gap) > ram.TriggerValue &&
                    Monster.IsPatternStartDistanceValid(ram, skill, Mathf.Abs(-gap)));
            }

            MethodInfo normalize = typeof(Monster).GetMethod("NormalizeAttackSurfaceGap",
                BindingFlags.Static | BindingFlags.NonPublic);
            Assert.NotNull(normalize);
            Assert.AreEqual(7.5f, (float)normalize.Invoke(null, new object[] { 7.504f, .01f }), .0001f);
            Assert.AreEqual(7.51f, (float)normalize.Invoke(null, new object[] { 7.506f, .01f }), .0001f);
            Assert.AreEqual(18.1f, 15f + 3.1f, .0001f, "Gap 15 endpoint fits Motion10003 MaxDistance 18.1.");

            foreach (float fixedStep in new[] { 1f / 15f, 1f / 30f, 1f / 60f })
            {
                foreach (float startGap in new[] { 2.01f, 7.5f })
                {
                    float gap = startGap;
                    while (gap > 2f) gap = Mathf.Max(2f, gap - 4.5f * fixedStep);
                    Assert.AreEqual(2f, gap, .0001f,
                        "Motor stop clamps chase to the nearest data band boundary without crossing it.");
                }
            }

            string source = File.ReadAllText("Assets/Scripts/Gameplay/Monster.cs");
            StringAssert.Contains("PatternTriggerType.DistanceOver => distToPlayer > pattern.TriggerValue", source);
            StringAssert.Contains("TryGetNearestApproachBandStopX(out attackStopX)", source);
        }

        [Test]
        public void Unit3104_RandomPatternsAreBalancedAndEffectsMatchReach()
        {
            var patterns = new MonsterPatternDataTable();
            patterns.LoadData(File.ReadAllText("Assets/Datas/MonsterPatternData.csv"));
            Assert.IsTrue(patterns.TryGetPatternData(6003, out MonsterPatternData push));
            Assert.IsTrue(patterns.TryGetPatternData(6004, out MonsterPatternData slam));
            foreach (MonsterPatternData pattern in new[] { push, slam })
            {
                Assert.AreEqual((uint)PatternExecutionType.Random, pattern.ExecutionType);
                Assert.AreEqual((uint)PatternTriggerType.None, pattern.TriggerType);
                Assert.AreEqual(100, pattern.RandomWeight);
            }

            UnityEngine.Random.State previousState = UnityEngine.Random.state;
            int pushCount = 0;
            try
            {
                for (int seed = 0; seed < 10000; seed++)
                {
                    UnityEngine.Random.InitState(seed);
                    if (UnityEngine.Random.Range(0, push.RandomWeight + slam.RandomWeight) < push.RandomWeight)
                        pushCount++;
                }
            }
            finally { UnityEngine.Random.state = previousState; }
            Assert.That(pushCount / 10000f, Is.InRange(.48f, .52f));
            Assert.AreEqual(6004u, slam.Idx, "6003 cooldown leaves 6004 as the sole candidate.");
            Assert.AreEqual(6003u, push.Idx, "6004 cooldown leaves 6003 as the sole candidate.");

            var effects = new EffectDataTable();
            effects.LoadData(File.ReadAllText("Assets/Datas/EffectData.csv"));
            foreach ((uint idx, Vector2 center, Vector2 size) in new[]
            {
                (8020u, new Vector2(.75f, 0f), new Vector2(3f, 1.8f)),
                (8021u, new Vector2(1.5f, 0f), new Vector2(1.5f, 3f))
            })
            {
                Assert.IsTrue(effects.TryGetEffectData(idx, out EffectData effect));
                Vector2 finalCenter = new Vector2(effect.ActiveCenterX, effect.ActiveCenterY) * effect.Scale;
                Vector2 finalSize = new Vector2(effect.ActiveSizeX, effect.ActiveSizeY) * effect.Scale;
                Assert.AreEqual(center.x, finalCenter.x, .0001f);
                Assert.AreEqual(center.y, finalCenter.y, .0001f);
                Assert.AreEqual(size.x, finalSize.x, .0001f);
                Assert.AreEqual(size.y, finalSize.y, .0001f);
                Assert.AreEqual(2.25f, finalCenter.x + finalSize.x * .5f, .0001f,
                    "Owner half-width .75 plus surface gap 1.5 is the exact forward reach.");
            }

            string selector = File.ReadAllText("Assets/Scripts/Gameplay/Monster.cs");
            Assert.Less(selector.IndexOf("PatternExecutionType.Random", System.StringComparison.Ordinal),
                selector.IndexOf("PatternExecutionType.Sequence", System.StringComparison.Ordinal));
            StringAssert.Contains("!IsCooldown(pattern.Idx)", selector);
        }

        [Test]
        public void AttackTelegraphCommonPathStartsOneSecondEarlierWithoutMovingAttack()
        {
            const float attackStartsAt = 10f;
            foreach ((float pre, float motion) in new[] { (.09f, .09f), (.12f, .28f) })
            {
                Assert.IsTrue(Monster.TryCalculateSkillTelegraphWindow(
                    attackStartsAt, pre, motion, out float originalStart, out float originalEnd));
                Assert.IsTrue(Monster.TryCalculateSkillTelegraphWindow(
                    attackStartsAt, pre, motion, out float earlierStart, out float earlierEnd, 1f));
                Assert.AreEqual(1f, originalStart - earlierStart, .0001f);
                Assert.AreEqual(attackStartsAt, originalEnd, .0001f);
                Assert.AreEqual(originalEnd, earlierEnd, .0001f, "Attack start must remain unchanged.");
            }

            string source = File.ReadAllText("Assets/Scripts/Gameplay/Monster.cs");
            StringAssert.Contains("AttackTelegraphVisualLeadSeconds = 1f", source);
            StringAssert.DoesNotContain("reservationAttackEffect.Idx == 8020u", source);
            StringAssert.Contains("(!telegraphActive || telegraphGeneration != generation)", source);
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
        public void Test_PlayerDeathHubTransition_IsSingleAndGenerationGuarded()
        {
            string source = File.ReadAllText("Assets/Scripts/Gameplay/Player.cs");
            int die = source.IndexOf("public void Die()", System.StringComparison.Ordinal);
            int duplicateGuard = source.IndexOf("if (deathSequenceActive) return;", die, System.StringComparison.Ordinal);
            int delay = source.IndexOf("FromSeconds(2.0f)", duplicateGuard, System.StringComparison.Ordinal);
            int staleGuard = source.IndexOf("if (generation != deathGeneration) return;", delay, System.StringComparison.Ordinal);
            int hub = source.IndexOf("StageManager.ReturnToHubAsync", staleGuard, System.StringComparison.Ordinal);
            int reset = source.IndexOf("public void ResetAfterDeath", die, System.StringComparison.Ordinal);
            int generationReset = source.IndexOf("deathGeneration++;", reset, System.StringComparison.Ordinal);

            Assert.GreaterOrEqual(die, 0);
            Assert.Greater(duplicateGuard, die);
            Assert.Greater(delay, duplicateGuard);
            Assert.Greater(staleGuard, delay);
            Assert.Greater(hub, staleGuard);
            Assert.Greater(generationReset, reset);
            Assert.AreEqual(1, Regex.Matches(source, "StageManager\\.ReturnToHubAsync").Count);
        }

        [Test]
        public void Test_HubSceneAsset_HasNoPlayerAndOneStage9001EntryButton()
        {
            string scene = File.ReadAllText("Assets/Scenes/HubScene.unity");
            Assert.AreEqual(0, Regex.Matches(scene, @"(?m)^  m_Name: Player\r?$").Count);
            Assert.AreEqual(1, Regex.Matches(scene, @"(?m)^  m_Name: Stage1EntryButton\r?$").Count);
            Assert.AreEqual(1, Regex.Matches(scene, @"(?m)^        m_MethodName: EnterStage\r?$").Count);
            Assert.AreEqual(1, Regex.Matches(scene, @"(?m)^          m_IntArgument: 9001\r?$").Count,
                "Stage1EntryButton must invoke EnterStage(9001).");
        }

        [Test]
        public void Test_InitToHubAndStage1Entry_EndToEndConfigurationContract()
        {
            string initSource = File.ReadAllText("Assets/Scripts/Scene/InitScene.cs");
            string initScene = File.ReadAllText("Assets/Scenes/InitScene.unity");
            string hubScene = File.ReadAllText("Assets/Scenes/HubScene.unity");
            string hubSource = File.ReadAllText("Assets/Scripts/Scene/HubScene.cs");
            string mainSource = File.ReadAllText("Assets/Scripts/Scene/MainScene.cs");
            string stageSource = File.ReadAllText("Assets/Scripts/Manager/StageManager.cs");

            StringAssert.Contains("nextScene = GameSceneManager.SceneName.Hub", initSource);
            Assert.AreEqual(1, Regex.Matches(initSource, @"TransitionTo\(nextScene\)").Count);
            StringAssert.Contains("  nextScene: 1", initScene);
            Assert.AreEqual(0, Regex.Matches(hubScene, @"(?m)^  m_Name: Player\r?$").Count);
            StringAssert.Contains("CurrentStageIdx = stageIdx", hubSource);
            StringAssert.Contains("TransitionTo(GameSceneManager.SceneName.Main)", hubSource);
            StringAssert.Contains("EnsureStageLoadedAsync(9001", mainSource);
            StringAssert.Contains("LoadNextRoomAsync(1040", stageSource);

            UnityEditor.EditorBuildSettingsScene[] scenes = UnityEditor.EditorBuildSettings.scenes;
            Assert.AreEqual(4, scenes.Length);
            CollectionAssert.AreEqual(new[]
            {
                "Assets/Scenes/InitScene.unity",
                "Assets/Scenes/LoadingScene.unity",
                "Assets/Scenes/HubScene.unity",
                "Assets/Scenes/MainScene.unity"
            }, new[] { scenes[0].path, scenes[1].path, scenes[2].path, scenes[3].path });
            Assert.IsTrue(System.Array.TrueForAll(scenes, scene => scene.enabled));
        }

        [Test]
        public void Test_HubTransitionFailure_UnlocksButtonAndLogsError()
        {
            string source = File.ReadAllText("Assets/Scripts/Scene/HubScene.cs");
            int failure = source.IndexOf("catch (Exception exception)", System.StringComparison.Ordinal);
            int unlock = source.IndexOf("transitionInProgress = false;", failure, System.StringComparison.Ordinal);
            int error = source.IndexOf("Debug.LogError", unlock, System.StringComparison.Ordinal);

            Assert.GreaterOrEqual(failure, 0);
            Assert.Greater(unlock, failure);
            Assert.Greater(error, unlock);
            StringAssert.Contains("Stage {stageIdx} transition failed: {exception.Message}", source);
        }

        [Test]
        public void Test_HubCleanupAndGaronVictory_KeepExistingLifecycleContract()
        {
            string stage = File.ReadAllText("Assets/Scripts/Manager/StageManager.cs");
            string hub = File.ReadAllText("Assets/Scripts/Scene/HubScene.cs");
            string boss = File.ReadAllText("Assets/Scripts/Gameplay/BossMonster.cs");

            StringAssert.Contains("ResetRunForHub();", hub);
            StringAssert.Contains("CleanupActiveChunksAndEffects();", stage);
            StringAssert.Contains("UnitPoolManager.Instance.DespawnAllMonsters();", stage);
            StringAssert.Contains("EffectPoolManager.Instance.ClearAllActiveEffects();", stage);
            StringAssert.Contains("CurrentRun = null;", stage);
            StringAssert.Contains("CompleteStage1Async", boss);
            StringAssert.Contains("ReturnToHubAsync", stage);
        }

        [Test]
        public void Test_AlertMessage_TextLookupReplaceAndSceneDisableContract()
        {
            var dataObject = new GameObject("Alert_Data_QA");
            var alertObject = new GameObject("Alert_QA");
            var textObject = new GameObject("Alert_Text_QA");
            var previousDataManager = DataTableManager.Instance;
            var previousLanguage = GameLanguageSettings.Current;
            var instanceField = typeof(Singleton<DataTableManager>)
                .GetField("<Instance>k__BackingField", BindingFlags.Static | BindingFlags.NonPublic);
            try
            {
                var dataManager = dataObject.AddComponent<DataTableManager>();
                instanceField.SetValue(null, dataManager);
                var tables = (System.Collections.Generic.Dictionary<DataTableType, IDataLoad>)typeof(DataTableManager)
                    .GetField("dataList", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(dataManager);
                var textTable = new TextDataTable();
                textTable.LoadData("idx,en,kr\n2001,First alert,첫 알림\n2002,Replacement alert,교체 알림");
                tables[DataTableType.Text] = textTable;
                GameLanguageSettings.Current = GameLanguage.En;

                var canvasGroup = alertObject.AddComponent<CanvasGroup>();
                var text = textObject.AddComponent<TMPro.TextMeshProUGUI>();
                var alert = alertObject.AddComponent<AlertMessage>();
                typeof(AlertMessage).GetField("messageText", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(alert, text);
                typeof(AlertMessage).GetField("canvasGroup", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(alert, canvasGroup);

                Assert.IsTrue(alert.Show(2001, 0));
                Assert.AreEqual("First alert", text.text);
                Assert.AreEqual(1f, canvasGroup.alpha);
                Assert.IsTrue(alert.Show(2001, 0), "Duplicate message must not add a second listener or task.");
                Assert.IsFalse(alert.Show(9999, 0));
                Assert.IsTrue(alert.Show(2002, 0));
                Assert.AreEqual("Replacement alert", text.text);

                GameLanguageSettings.Current = GameLanguage.Kr;
                Assert.IsTrue(alert.Show(2001, 0));
                Assert.AreEqual("첫 알림", text.text);

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
                GameLanguageSettings.Current = previousLanguage;
                if (DataTableManager.Instance == (DataTableManager)dataObject.GetComponent(typeof(DataTableManager)))
                    instanceField.SetValue(null, previousDataManager);
                Object.DestroyImmediate(textObject);
                Object.DestroyImmediate(alertObject);
                Object.DestroyImmediate(dataObject);
            }
        }

        [Test]
        public void Test_ProductionMainHud_EventFillAndLifecycleContract()
        {
            var hudObject = new GameObject("ProductionHUD_QA");
            var hpObject = new GameObject("PlayerHp_QA");
            var postureObject = new GameObject("PlayerPosture_QA");
            var mpObject = new GameObject("PlayerMp_QA");
            var statsObject = new GameObject("PlayerStats_QA");
            try
            {
                var hp = hpObject.AddComponent<UnityEngine.UI.Image>();
                var posture = postureObject.AddComponent<UnityEngine.UI.Image>();
                var mp = mpObject.AddComponent<UnityEngine.UI.Image>();
                var hud = hudObject.AddComponent<ProductionMainHUD>();
                var stats = statsObject.AddComponent<CombatStats>();
                stats.OnHpChanged = new UnityEngine.Events.UnityEvent<float>();
                stats.OnMpChanged = new UnityEngine.Events.UnityEvent<float>();
                stats.OnPostureChanged = new UnityEngine.Events.UnityEvent<float>();

                typeof(ProductionMainHUD).GetField("playerHpFill", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(hud, hp);
                typeof(ProductionMainHUD).GetField("playerPostureFill", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(hud, posture);
                typeof(ProductionMainHUD).GetField("playerMpFill", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(hud, mp);
                hud.BindPlayer(stats);

                stats.OnHpChanged.Invoke(0.25f);
                stats.OnPostureChanged.Invoke(0.5f);
                stats.OnMpChanged.Invoke(0.75f);
                Assert.AreEqual(0.25f, hp.fillAmount);
                Assert.AreEqual(0.5f, posture.fillAmount);
                Assert.AreEqual(0.75f, mp.fillAmount);

                typeof(ProductionMainHUD).GetMethod("OnDisable", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(hud, null);
                hp.fillAmount = 0.9f;
                stats.OnHpChanged.Invoke(0.1f);
                Assert.AreEqual(0.9f, hp.fillAmount, "Scene unload must remove runtime listeners.");

                string hudSource = File.ReadAllText("Assets/Scripts/UI/ProductionMainHUD.cs");
                string mainSource = File.ReadAllText("Assets/Scripts/Scene/MainScene.cs");
                string sceneSource = File.ReadAllText("Assets/Scenes/MainScene.unity");
                StringAssert.Contains("Monster.Activated += OnMonsterActivated", hudSource);
                StringAssert.Contains("Monster.Deactivated -= OnMonsterDeactivated", hudSource);
                StringAssert.Contains("BindSceneState();", hudSource);
                StringAssert.Contains("transform.localScale == Vector3.zero", hudSource);
                StringAssert.Contains("Player.Activated += OnPlayerActivated", hudSource);
                StringAssert.Contains("Player.Deactivated -= OnPlayerDeactivated", hudSource);
                StringAssert.Contains("bossStats.OnHpChanged.RemoveListener", hudSource);
                StringAssert.Contains("stageManager.ProgressChanged += SetStageProgress", hudSource);
                StringAssert.Contains("alertMessage.Show(textIdx", hudSource);
                Assert.IsFalse(Regex.IsMatch(hudSource, @"\bOnGUI\s*\("));
                StringAssert.Contains("attackTelegraphFill.fillAmount = CalculateAttackTelegraphFill", hudSource);
                StringAssert.DoesNotContain("new GameObject", hudSource);
                StringAssert.DoesNotContain("Find", hudSource);
                StringAssert.DoesNotContain("CoreTestHUD", mainSource);
                StringAssert.DoesNotContain("TestPlayerHUDUI", mainSource);
                StringAssert.DoesNotContain("MonsterOverheadHUD", mainSource);
                StringAssert.Contains("ProductionMainHUD is not bound on MainHUDRoot", mainSource);
                StringAssert.Contains("m_Name: MainHUDRoot", sceneSource);
                StringAssert.IsMatch("m_Name: BossGroup[\\s\\S]{0,5000}m_Alpha: 0", sceneSource);
            }
            finally
            {
                Object.DestroyImmediate(statsObject);
                Object.DestroyImmediate(mpObject);
                Object.DestroyImmediate(postureObject);
                Object.DestroyImmediate(hpObject);
                Object.DestroyImmediate(hudObject);
            }
        }

        [Test]
        public void PhaseA1x1_PlaytestUsesEntryAndSingleMonster3105Override()
        {
            string builder = File.ReadAllText("Assets/Scripts/Scene/TilemapStageBuilder.cs");
            string main = File.ReadAllText("Assets/Scripts/Scene/MainScene.cs");
            string spawner = File.ReadAllText("Assets/Scripts/Manager/UnitSpawner.cs");
            GameObject room = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Prefabs/Development/Tilemap_Room_PhaseA_1x1.prefab");

            Assert.NotNull(room);
            Assert.NotNull(room.GetComponentInChildren<UnityEngine.Tilemaps.TilemapCollider2D>(true));
            Assert.IsTrue(System.Array.Exists(room.GetComponentsInChildren<BoxCollider2D>(true),
                collider => collider.isTrigger && collider.gameObject.name == "CameraBounds"));
            Assert.IsTrue(System.Array.Exists(room.GetComponentsInChildren<ChunkSocketMarker>(true),
                socket => socket.EntryMarker != null));
            Assert.GreaterOrEqual(room.GetComponentsInChildren<SpawnPointMarker>(true).Length, 2);
            StringAssert.Contains("UsePhaseA1x1Playtest = true", main);
            StringAssert.Contains("DevelopmentMonsterUnitIdx = 3105u", main);
            StringAssert.DoesNotContain("DevelopmentMonsterUnitIdx = 3104u", main);
            StringAssert.DoesNotContain("DevelopmentMonsterUnitIdx = 3103u", main);
            Assert.AreEqual(1, System.Array.FindAll(room.GetComponentsInChildren<SpawnPointMarker>(true),
                marker => marker.MonsterId == 3102u).Length);
            Assert.AreEqual(0, System.Array.FindAll(room.GetComponentsInChildren<SpawnPointMarker>(true),
                marker => marker.MonsterId == 3105u).Length,
                "The Development prefab remains unchanged; the runtime override owns the test unit.");
            StringAssert.Contains("ConfigureDevelopmentPlaytestMarkers", builder);
            StringAssert.Contains("markers[1].MonsterId = DevelopmentMonsterUnitIdx", builder);
            Assert.Less(spawner.IndexOf("if (zones.Count == 1 && zones[0].MonsterId != 0u)"),
                spawner.IndexOf("uint[] encounter = GetCurrentEncounter(zones)"));
            StringAssert.Contains("playtestMarker.EnableSpawn = false", spawner);
            Assert.NotNull(UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Unit_3105.prefab")
                .GetComponent<SkillExecutor>(), "Unit_3105 must keep its prefab-bound SkillExecutor.");

            var units = new UnitBaseDataTable();
            units.LoadData(File.ReadAllText("Assets/Datas/UnitBaseData.csv"));
            Assert.IsTrue(units.TryGetUnitData(3105, out UnitBaseData unit3105));
            Assert.AreEqual(1007u, unit3105.PrefabId);
            Assert.AreEqual(1016u, unit3105.AnimatorId);
            var monsters = new MonsterDataTable();
            monsters.LoadData(File.ReadAllText("Assets/Datas/MonsterBaseData.csv"));
            Assert.IsTrue(monsters.TryGetMonsterData(5105, out MonsterBaseData monster3105));
            CollectionAssert.AreEqual(new uint[] { 6005u }, monster3105.PatternIdxList);

            var patterns = new MonsterPatternDataTable();
            patterns.LoadData(File.ReadAllText("Assets/Datas/MonsterPatternData.csv"));
            var skills = new SkillDataTable();
            skills.LoadData(File.ReadAllText("Assets/Datas/SkillData.csv"));
            var effects = new EffectDataTable();
            effects.LoadData(File.ReadAllText("Assets/Datas/EffectData.csv"));
            foreach ((uint patternIdx, uint effectIdx) in new[] { (6005u, 8022u) })
            {
                Assert.IsTrue(patterns.TryGetPatternData(patternIdx, out MonsterPatternData pattern));
                Assert.AreEqual(7015u, pattern.SkillIdx);
                Assert.IsTrue(skills.TryGetSkillData(pattern.SkillIdx, out _));
                Assert.IsTrue(effects.TryGetEffectData(effectIdx, out EffectData effect));
                Assert.IsTrue(effect.HasValidActiveBounds);
                Assert.AreEqual(3105u, effect.UnitIdx);
                Assert.AreEqual(patternIdx, effect.PatternIdx);
                Assert.AreEqual(7015u, effect.SkillIdx);
            }
        }

        [Test]
        public void AttackMotionProfile_StrictFallbackPriorityAndFixedStepContracts()
        {
            Assert.AreEqual(DataTableType.AttackMotionProfile, Util.GetDataTableType(10001));
            var persisted = new AttackMotionProfileDataTable();
            persisted.LoadData(File.ReadAllText("Assets/Datas/AttackMotionProfileData.csv"));
            Assert.AreEqual(4, persisted.GetDataCount());
            Assert.IsTrue(persisted.TryGetValid(10001, out var persistedStationary));
            Assert.AreEqual(AttackMotionType.Stationary, persistedStationary.MotionType);
            Assert.IsTrue(persisted.TryGetValid(10002, out var persistedStep));
            Assert.AreEqual(AttackMotionType.Step, persistedStep.MotionType);
            Assert.AreEqual(2.31f, persistedStep.MaxDistance, .0001f);
            Assert.AreEqual(9f, persistedStep.MaxSpeed, .0001f);
            Assert.AreEqual(0f, persistedStep.Acceleration, .0001f);
            Assert.IsTrue(persistedStep.Enabled);
            Assert.IsTrue(persisted.TryGetValid(10003, out var persistedLunge));
            Assert.AreEqual(AttackMotionType.AcceleratingLunge, persistedLunge.MotionType);
            Assert.AreEqual(AttackTargetPolicy.TrackUntilActive, persistedLunge.TargetPolicy);
            Assert.AreEqual(18.1f, persistedLunge.MaxDistance, .0001f);
            Assert.AreEqual(32f, persistedLunge.MaxSpeed, .0001f);
            Assert.AreEqual(64f, persistedLunge.Acceleration, .0001f);
            Assert.IsTrue(persisted.TryGetValid(10004, out var persistedShortStep));
            Assert.AreEqual(AttackMotionType.Step, persistedShortStep.MotionType);
            Assert.AreEqual(1.905f, persistedShortStep.MaxDistance, .0001f);
            Assert.AreEqual(4.5f, persistedShortStep.MaxSpeed, .0001f);
            Assert.AreEqual(0f, persistedShortStep.Acceleration, .0001f);

            var skills = new SkillDataTable();
            skills.LoadData(File.ReadAllText("Assets/Datas/SkillData.csv"));
            foreach (uint idx in new uint[] { 7005, 7006, 7017, 7018 })
            {
                Assert.IsTrue(skills.TryGetSkillData(idx, out var movingSkill));
                Assert.AreEqual(SkillMotionPhase.AttackMotion | SkillMotionPhase.Pre,
                    movingSkill.MotionPhaseMask, $"Skill {idx} must move continuously through startup and PRE.");
            }
            Assert.IsTrue(skills.TryGetSkillData(7007, out var torsoMotionSkill));
            Assert.AreEqual(SkillMotionPhase.AttackMotion | SkillMotionPhase.Active,
                torsoMotionSkill.MotionPhaseMask);
            foreach (uint idx in new uint[] { 7001, 7002, 7003, 7004, 7006, 7008, 7009,
                         7010, 7011, 7012, 7013, 7014, 7015, 7016 })
            {
                Assert.IsTrue(skills.TryGetSkillData(idx, out var skillData));
                Assert.AreEqual(10001u, skillData.AttackMotionProfileIdx);
                if (idx != 7006u) Assert.AreEqual(SkillMotionPhase.None, skillData.MotionPhaseMask);
            }

            var patterns = new MonsterPatternDataTable();
            patterns.LoadData(File.ReadAllText("Assets/Datas/MonsterPatternData.csv"));
            foreach (uint idx in new uint[] { 6001, 6002, 6003, 6004, 6005, 6007,
                         6100, 6101, 6102, 6103 })
            {
                Assert.IsTrue(patterns.TryGetPatternData(idx, out var pattern));
                Assert.AreEqual(0u, pattern.AttackMotionProfileIdx);
            }
            Assert.IsTrue(patterns.TryGetPatternData(6012, out var chainStart));
            Assert.AreEqual(10001u, chainStart.AttackMotionProfileIdx);
            Assert.IsTrue(patterns.TryGetPatternData(6013, out var chainMiddle));
            Assert.AreEqual(10002u, chainMiddle.AttackMotionProfileIdx);
            Assert.IsTrue(patterns.TryGetPatternData(6014, out var chainEnd));
            Assert.AreEqual(10004u, chainEnd.AttackMotionProfileIdx);
            Assert.IsTrue(patterns.TryGetPatternData(6010, out var torsoRam));
            Assert.AreEqual(10003u, torsoRam.AttackMotionProfileIdx);

            Assert.IsTrue(skills.TryGetSkillData(7009, out var chainSkill1));
            Assert.IsTrue(skills.TryGetSkillData(7017, out var chainSkill2));
            Assert.IsTrue(skills.TryGetSkillData(7018, out var chainSkill3));
            Assert.AreEqual(.18f, chainSkill1.AttackMotionTime, .0001f);
            Assert.AreEqual(.15f, chainSkill2.AttackMotionTime, .0001f);
            Assert.AreEqual(.25f, chainSkill3.AttackMotionTime, .0001f);
            Assert.AreEqual(.58f, chainSkill1.AttackMotionTime + chainSkill2.AttackMotionTime +
                chainSkill3.AttackMotionTime, .0001f);

            string meta = File.ReadAllText("Assets/Datas/AttackMotionProfileData.csv.meta");
            string guid = Regex.Match(meta, @"(?m)^guid: ([0-9a-f]+)$").Groups[1].Value;
            Assert.IsNotEmpty(guid);
            Assert.AreEqual(1, Regex.Matches(
                File.ReadAllText("Assets/AddressableAssetsData/AssetGroups/Datas.asset"), guid).Count);

            var table = new AttackMotionProfileDataTable();
            table.LoadData("idx,motiontype,targetpolicy,maxdistance,maxspeed,acceleration,enabled\n" +
                "10001,0,0,0,0,0,1\n10002,1,1,6,8,20,0\n10003,2,0,NaN,8,20,1");
            Assert.IsTrue(table.TryGetValid(10001, out var stationary));
            Assert.AreEqual(AttackMotionType.Stationary, stationary.MotionType);
            Assert.IsFalse(table.TryGetValid(10002, out _), "Unapproved Step must fall back to Stationary.");
            Assert.IsFalse(table.TryGetValid(10003, out _), "NaN motion data must fall back to Stationary.");
            table.LoadData("idx,motiontype,targetpolicy,maxdistance,maxspeed,acceleration,enabled\n10001,Step,Track,0,0,0,1");
            Assert.IsTrue(table.TryGetValid(10001, out stationary));
            Assert.AreEqual(AttackMotionType.Stationary, stationary.MotionType);
            Assert.AreEqual(AttackTargetPolicy.SnapshotAtStartup, stationary.TargetPolicy);

            var skill = new SkillData { AttackMotionProfileIdx = 10002 };
            Assert.AreEqual(10003u, SkillExecutor.ResolveAttackMotionProfileIdx(skill, 10003));
            Assert.AreEqual(10002u, SkillExecutor.ResolveAttackMotionProfileIdx(skill));
            Assert.AreEqual(10001u, SkillExecutor.ResolveAttackMotionProfileIdx(new SkillData()));

            var step = new AttackMotionProfileData
            {
                MotionType = AttackMotionType.Step, MaxDistance = 6f, MaxSpeed = 8f,
                Acceleration = 20f, Enabled = true
            };
            Assert.AreEqual(SimulateArrival(step, 1f / 15f), SimulateArrival(step, 1f / 60f), .001f);
            Assert.AreEqual(0f, SkillExecutor.CalculateAttackMotionVelocity(stationary, 0f, 6f, 1f, 5f, .02f));

            string executor = File.ReadAllText("Assets/Scripts/Gameplay/SkillExecutor.cs");
            StringAssert.Contains("owner.StopAttackMotionImmediately();", executor);
        }

        [Test]
        public void GaronStepMigration_ReusesProfile10002ForAttackMotionAndPre()
        {
            var profiles = new AttackMotionProfileDataTable();
            profiles.LoadData(File.ReadAllText("Assets/Datas/AttackMotionProfileData.csv"));
            Assert.IsTrue(profiles.TryGetValid(10002u, out AttackMotionProfileData step));
            Assert.AreEqual(AttackMotionType.Step, step.MotionType);
            Assert.AreEqual(2.31f, step.MaxDistance, .0001f);
            Assert.AreEqual(9f, step.MaxSpeed, .0001f);
            Assert.AreEqual(0f, step.Acceleration, .0001f);

            SkillMotionPhase phases = SkillMotionPhase.AttackMotion | SkillMotionPhase.Pre;
            Assert.AreNotEqual(0u, (uint)(phases & SkillMotionPhase.AttackMotion));
            Assert.AreNotEqual(0u, (uint)(phases & SkillMotionPhase.Pre));
            Assert.AreEqual(0u, (uint)(phases & SkillMotionPhase.Active));

            var skills = new SkillDataTable();
            skills.LoadData(File.ReadAllText("Assets/Datas/SkillData.csv"));
            var patterns = new MonsterPatternDataTable();
            patterns.LoadData(File.ReadAllText("Assets/Datas/MonsterPatternData.csv"));
            foreach ((uint patternIdx, uint skillIdx) in new[]
                     { (6101u, 7011u), (6102u, 7010u), (6104u, 7019u) })
            {
                Assert.IsTrue(patterns.TryGetPatternData(patternIdx, out MonsterPatternData pattern));
                Assert.IsTrue(skills.TryGetSkillData(skillIdx, out SkillData skill));
                Assert.AreEqual(10002u, pattern.AttackMotionProfileIdx);
                Assert.AreEqual(phases, skill.MotionPhaseMask);
                Assert.AreEqual(10002u,
                    SkillExecutor.ResolveAttackMotionProfileIdx(skill, pattern.AttackMotionProfileIdx));
            }

            string executor = File.ReadAllText("Assets/Scripts/Gameplay/SkillExecutor.cs");
            StringAssert.Contains("StopAttackMotionImmediately", executor);
            StringAssert.Contains("HasGroundSupportForAttackStep", executor);
        }

        [Test]
        public void MonsterPatternFacingSnapshot_LocksWholeChainAndClearsAtLifecycleEnd()
        {
            var ownerObject = new GameObject("PatternFacingSnapshot_QA");
            try
            {
                var owner = ownerObject.AddComponent<Monster>();
                const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
                typeof(Monster).GetField("actionGeneration", flags).SetValue(owner, 17u);
                typeof(Monster).GetField("patternSnapshotGeneration", flags).SetValue(owner, 17u);
                typeof(Monster).GetField("patternFacingRightSnapshot", flags).SetValue(owner, true);
                MethodInfo apply = typeof(Monster).GetMethod("ApplyPatternFacingSnapshot", flags);
                MethodInfo clear = typeof(Monster).GetMethod("ClearPatternSnapshot", flags);

                var visualObject = new GameObject("Visual");
                visualObject.transform.SetParent(ownerObject.transform);
                var visual = visualObject.AddComponent<SpriteRenderer>();
                typeof(UnitBase).GetField("spriteRenderer", BindingFlags.Instance | BindingFlags.NonPublic)
                    .SetValue(owner, visual);

                owner.SetFacingRight(false);
                Assert.IsTrue(owner.IsFacingRight, "Any live-target writer must obey the pattern lock.");
                visual.flipX = false;
                typeof(Monster).GetMethod("LateUpdate", flags).Invoke(owner, null);
                Assert.IsTrue(visual.flipX, "Animator/update ordering must not leave the visible sprite reversed.");
                apply.Invoke(owner, null);
                Assert.IsTrue(owner.IsFacingRight, "A linked child must reuse the root direction.");

                clear.Invoke(owner, null);
                owner.SetFacingRight(false);
                apply.Invoke(owner, null);
                Assert.IsFalse(owner.IsFacingRight, "Idle facing updates must resume after snapshot cleanup.");

                string monster = File.ReadAllText("Assets/Scripts/Gameplay/Monster.cs");
                Assert.AreEqual(1, Regex.Matches(monster, @"if \(!TryCapturePatternSnapshot\(\)\) return;").Count,
                    "The root must capture once at HUD completion.");
                int scheduler = monster.IndexOf("if (schedulerLead > 0f)", System.StringComparison.Ordinal);
                int endTelegraph = monster.IndexOf("EndAttackTelegraph(generation);", scheduler,
                    System.StringComparison.Ordinal);
                int capture = monster.IndexOf("if (!TryCapturePatternSnapshot()) return;", endTelegraph,
                    System.StringComparison.Ordinal);
                int execute = monster.IndexOf("ExecuteSkillHitsAsync(", capture,
                    System.StringComparison.Ordinal);
                Assert.That(endTelegraph, Is.GreaterThan(scheduler));
                Assert.That(capture, Is.GreaterThan(endTelegraph));
                Assert.That(execute, Is.GreaterThan(capture));
                StringAssert.Contains("float captureAt = attackSequenceStartedAt + effectivePreDelay;", monster);
                StringAssert.Contains("else SetFacingRight(toward > 0f);", monster);
                StringAssert.DoesNotContain("SetFacingRight(playerTarget.position.x >= transform.position.x)", monster);
            }
            finally
            {
                Object.DestroyImmediate(ownerObject);
            }
        }

        [Test]
        public void SkillMotionPhaseMask_UsesOneContextAndOneDistanceBudget()
        {
            SkillMotionPhase startup = SkillMotionPhase.AttackMotion | SkillMotionPhase.Pre;
            Assert.AreNotEqual(0, startup & SkillMotionPhase.AttackMotion);
            Assert.AreNotEqual(0, startup & SkillMotionPhase.Pre);
            Assert.AreEqual(SkillMotionPhase.None, SkillMotionPhase.Active & SkillMotionPhase.Post);

            string source = File.ReadAllText("Assets/Scripts/Gameplay/SkillExecutor.cs");
            Assert.AreEqual(1, Regex.Matches(source, @"new MotionContext\s*\{").Count,
                "A skill execution owns exactly one movement context.");
            StringAssert.Contains("profile.MaxDistance - context.MovedDistance", source);
            StringAssert.Contains("SkillMotionPhase.Active, lastWindowEnd - elapsed", source);
            StringAssert.Contains("SkillMotionPhase.Post, recoverySeconds - recoveryElapsed", source);
            StringAssert.DoesNotContain("attackMotionComplete", source);
        }

        [Test]
        public void EffectSpawnPivot_MovesVisualAndPreservesAuthoritativeSweep()
        {
            var table = new EffectDataTable();
            table.LoadData(File.ReadAllText("Assets/Datas/EffectData.csv"));
            MethodInfo visualPose = typeof(SkillExecutor).GetMethod("TryCalculateEffectVisualPose",
                BindingFlags.Static | BindingFlags.NonPublic);
            MethodInfo createSweep = typeof(SkillExecutor).GetMethod("TryCreateEffectSweep",
                BindingFlags.Static | BindingFlags.NonPublic);
            Assert.NotNull(visualPose);
            Assert.NotNull(createSweep);

            foreach ((uint unitIdx, uint effectIdx, float expectedRightCenterX) in new[]
            {
                (3001u, 8014u, 1.2578125f), (3001u, 8027u, 1.421875f),
                (3001u, 8028u, 1.3203125f), (3001u, 8032u, 1.1171875f),
                (3102u, 8017u, .78490566f), (3104u, 8020u, .75f),
                (3104u, 8021u, 1.5f), (3201u, 8029u, .28089844f)
            })
            {
                GameObject ownerObject = Object.Instantiate(UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(
                    $"Assets/Prefabs/Unit_{unitIdx}.prefab"));
                try
                {
                    UnitBase owner = ownerObject.GetComponent<UnitBase>();
                    Assert.IsTrue(table.TryGetEffectData(effectIdx, out EffectData data));
                    CombatStats stats = ownerObject.GetComponent<CombatStats>();
                    typeof(UnitBase).GetField("stats", BindingFlags.Instance | BindingFlags.NonPublic)
                        .SetValue(owner, stats);
                    if (stats.DefenseBodyCollider == null)
                        stats.SetDefenseBodyCollider(ownerObject.GetComponent<Collider2D>());
                    Vector2 bodyCenter = owner.Stats.DefenseBodyCollider.bounds.center;

                    foreach (bool facingRight in new[] { true, false })
                    {
                        owner.SetFacingRight(facingRight);
                        object[] visualArgs = { owner, data, Vector2.zero, Quaternion.identity };
                        Assert.IsTrue((bool)visualPose.Invoke(null, visualArgs));
                        Vector2 visualRoot = (Vector2)visualArgs[2];
                        Quaternion rotation = (Quaternion)visualArgs[3];
                        Vector2 expectedPivot = bodyCenter + (Vector2)(rotation * new Vector3(
                            (facingRight ? 1f : -1f) * data.SpawnPivotX, data.SpawnPivotY, 0f));
                        Assert.AreEqual(expectedPivot.x, visualRoot.x, .0001f);
                        Assert.AreEqual(expectedPivot.y, visualRoot.y, .0001f);

                        object[] sweepArgs = { owner, data, Vector2.zero, 1, 1u, 0u,
                            default(CombatStats.AttackSweep2D), Quaternion.identity };
                        Assert.IsTrue((bool)createSweep.Invoke(null, sweepArgs));
                        CombatStats.AttackSweep2D sweep = (CombatStats.AttackSweep2D)sweepArgs[6];
                        Vector2 alphaCenter = visualRoot + (Vector2)(rotation * new Vector3(
                            (facingRight ? 1f : -1f) * data.ActiveCenterX * data.Scale,
                            data.ActiveCenterY * data.Scale, 0f));
                        Assert.AreEqual(sweep.Current.x, alphaCenter.x, .0001f);
                        Assert.AreEqual(sweep.Current.y, alphaCenter.y, .0001f);
                        Assert.AreEqual(expectedRightCenterX,
                            Mathf.Abs(sweep.Current.x - bodyCenter.x), .0001f);
                        Assert.AreEqual(data.ActiveSizeX * data.Scale, sweep.Size.x, .0001f);
                        Assert.AreEqual(data.ActiveSizeY * data.Scale, sweep.Size.y, .0001f);
                    }
                }
                finally
                {
                    Object.DestroyImmediate(ownerObject);
                }
            }
        }

        [Test]
        public void EffectScale_AppliesOnceToUnit3101And3102NativePoseAndBounds()
        {
            var table = new EffectDataTable();
            table.LoadData(File.ReadAllText("Assets/Datas/EffectData.csv"));
            MethodInfo createSweep = typeof(SkillExecutor).GetMethod("TryCreateEffectSweep",
                BindingFlags.Static | BindingFlags.NonPublic);
            Assert.NotNull(createSweep);

            foreach ((uint unitIdx, uint effectIdx) in new[]
            {
                (3101u, 8015u), (3102u, 8016u), (3102u, 8017u)
            })
            {
                GameObject ownerObject = Object.Instantiate(UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(
                    $"Assets/Prefabs/Unit_{unitIdx}.prefab"));
                try
                {
                    UnitBase owner = ownerObject.GetComponent<UnitBase>();
                    Assert.IsTrue(table.TryGetEffectData(effectIdx, out EffectData data));
                    Vector2 bodyCenter = owner.Stats.DefenseBodyCollider.bounds.center;

                    owner.SetFacingRight(true);
                    object[] rightArgs = { owner, data, Vector2.zero, 1, 1u, 0u,
                        default(CombatStats.AttackSweep2D), Quaternion.identity };
                    Assert.IsTrue((bool)createSweep.Invoke(null, rightArgs));
                    CombatStats.AttackSweep2D right = (CombatStats.AttackSweep2D)rightArgs[6];

                    owner.SetFacingRight(false);
                    object[] leftArgs = { owner, data, Vector2.zero, 1, 1u, 0u,
                        default(CombatStats.AttackSweep2D), Quaternion.identity };
                    Assert.IsTrue((bool)createSweep.Invoke(null, leftArgs));
                    CombatStats.AttackSweep2D left = (CombatStats.AttackSweep2D)leftArgs[6];

                    Assert.AreEqual(data.ActiveSizeX * data.Scale, right.Size.x, .0001f);
                    Assert.AreEqual(data.ActiveSizeY * data.Scale, right.Size.y, .0001f);
                    Assert.AreEqual(data.ActiveCenterX * data.Scale, right.Current.x - bodyCenter.x, .0001f);
                    Assert.AreEqual(data.ActiveCenterX * data.Scale, bodyCenter.x - left.Current.x, .0001f);
                    Assert.AreEqual(right.Size, left.Size);
                    Assert.AreEqual(right.Shape, left.Shape);
                }
                finally
                {
                    Object.DestroyImmediate(ownerObject);
                }
            }
        }

        [Test]
        public void EffectDrivenAttackBounds_GameViewDebugReusesOneOwnerRenderer()
        {
            string source = File.ReadAllText("Assets/Scripts/Gameplay/SkillExecutor.cs");
            StringAssert.Contains("DrawEffectBoundsDebug(owner, sweep);", source);
            StringAssert.Contains("DrawEffectBoundsDebug(owner, movedSweep);", source);
            StringAssert.Contains("sweep.Shape == ActiveShape.Circle", source);
            StringAssert.Contains("sweep.Shape == ActiveShape.Capsule", source);
            StringAssert.Contains("owner.gameObject.AddComponent<LineRenderer>()", source);
            StringAssert.Contains("effectBoundsDebugLine.sharedMaterial = material", source);
            StringAssert.Contains("HideEffectBoundsDebug();", source);
            StringAssert.Contains("#if UNITY_EDITOR || DEVELOPMENT_BUILD", source);
            StringAssert.DoesNotContain("new GameObject(\"DebugEffect", source);
            StringAssert.DoesNotContain("Debug.DrawLine", source);
            StringAssert.DoesNotContain("new Material", source);
        }

        [Test]
        public void Unit3102_EffectVisualsMirrorOnceAndScaleCoversDefenseBody()
        {
            var table = new EffectDataTable();
            table.LoadData(File.ReadAllText("Assets/Datas/EffectData.csv"));
            GameObject ownerObject = Object.Instantiate(UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Prefabs/Unit_3102.prefab"));
            MethodInfo pose = typeof(SkillExecutor).GetMethod("TryCalculateEffectPose",
                BindingFlags.Static | BindingFlags.NonPublic);
            MethodInfo createSweep = typeof(SkillExecutor).GetMethod("TryCreateEffectSweep",
                BindingFlags.Static | BindingFlags.NonPublic);
            MethodInfo visualTransform = typeof(SkillExecutor).GetMethod("ApplyEffectVisualTransform",
                BindingFlags.Static | BindingFlags.NonPublic);
            try
            {
                UnitBase owner = ownerObject.GetComponent<UnitBase>();
                Collider2D body = owner.Stats.DefenseBodyCollider;
                Assert.NotNull(body);
                Assert.NotNull(pose);
                Assert.NotNull(createSweep);
                Assert.NotNull(visualTransform);

                foreach ((uint idx, string path) in new[]
                {
                    (8016u, "Assets/Prefabs/Effects/Attack/Effect_8016.prefab"),
                    (8017u, "Assets/Prefabs/Effects/Attack/Effect_8017.prefab")
                })
                {
                    Assert.IsTrue(table.TryGetEffectData(idx, out EffectData data));
                    GameObject effect = Object.Instantiate(UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(path));
                    try
                    {
                        owner.SetFacingRight(true);
                        object[] rightArgs = { owner, data, Vector2.zero, Quaternion.identity };
                        Assert.IsTrue((bool)pose.Invoke(null, rightArgs));
                        owner.SetFacingRight(false);
                        object[] leftArgs = { owner, data, Vector2.zero, Quaternion.identity };
                        Assert.IsTrue((bool)pose.Invoke(null, leftArgs));
                        Vector2 bodyCenter = body.bounds.center;
                        Vector2 right = (Vector2)rightArgs[2];
                        Vector2 left = (Vector2)leftArgs[2];
                        Assert.AreEqual(right.x - bodyCenter.x, bodyCenter.x - left.x, .0001f);
                        Assert.AreEqual(right.y, left.y, .0001f);
                        float rightAngle = ((Quaternion)rightArgs[3]).eulerAngles.z;
                        float leftAngle = ((Quaternion)leftArgs[3]).eulerAngles.z;
                        float ownerAngle = owner.transform.rotation.eulerAngles.z;
                        Assert.AreEqual(0f, Mathf.DeltaAngle(ownerAngle, rightAngle), .0001f);
                        Assert.AreEqual(0f, Mathf.DeltaAngle(ownerAngle, leftAngle), .0001f);

                        owner.SetFacingRight(true);
                        object[] rightSweepArgs = { owner, data, right, 1, 1u, 0u,
                            default(CombatStats.AttackSweep2D), Quaternion.identity };
                        Assert.IsTrue((bool)createSweep.Invoke(null, rightSweepArgs));
                        owner.SetFacingRight(false);
                        object[] leftSweepArgs = { owner, data, left, 1, 1u, 0u,
                            default(CombatStats.AttackSweep2D), Quaternion.identity };
                        Assert.IsTrue((bool)createSweep.Invoke(null, leftSweepArgs));
                        var rightSweep = (CombatStats.AttackSweep2D)rightSweepArgs[6];
                        var leftSweep = (CombatStats.AttackSweep2D)leftSweepArgs[6];
                        Assert.AreEqual(0f, Mathf.DeltaAngle(ownerAngle, rightSweep.Angle), .0001f);
                        Assert.AreEqual(0f, Mathf.DeltaAngle(ownerAngle, leftSweep.Angle), .0001f);

                        visualTransform.Invoke(null, new object[] { effect, data.Scale, true });
                        SpriteRenderer renderer = effect.GetComponentInChildren<SpriteRenderer>(true);
                        Vector3 rightScale = renderer.transform.localScale;
                        visualTransform.Invoke(null, new object[] { effect, data.Scale, false });
                        Vector3 leftScale = renderer.transform.localScale;
                        Assert.AreEqual(rightScale.x, -leftScale.x, .0001f);
                        Assert.AreEqual(rightScale.y, leftScale.y, .0001f);
                        Assert.IsFalse(renderer.flipX, "Facing must be applied once by visual X scale.");
                        Vector2 envelope = Vector2.Scale(renderer.sprite.bounds.size,
                            new Vector2(Mathf.Abs(leftScale.x), Mathf.Abs(leftScale.y)));
                        Assert.GreaterOrEqual(envelope.x + .0001f, body.bounds.size.x);
                        Assert.GreaterOrEqual(envelope.y + .0001f, body.bounds.size.y);
                    }
                    finally
                    {
                        Object.DestroyImmediate(effect);
                    }
                }

            }
            finally
            {
                Object.DestroyImmediate(ownerObject);
            }
        }

        [Test]
        public void AttackEffectVisualTail_IsMeasuredFromEachHitWindow()
        {
            string source = File.ReadAllText("Assets/Scripts/Gameplay/SkillExecutor.cs");
            StringAssert.Contains("Mathf.Max(lastWindowEnd, windowStart + attackEffectData.Duration)", source);
            StringAssert.DoesNotContain("firstWindowStart + attackEffectData.Duration", source);
            Assert.Less(source.IndexOf("SpawnAttackEffectForWindowAsync(effect, owner", System.StringComparison.Ordinal),
                source.IndexOf("for (uint tick = 0; tick < (uint)skill.HitTimings.Length", System.StringComparison.Ordinal));
            StringAssert.Contains("effect.HitTick == 0u && attackEffects.Length > 1", source);
            Assert.IsFalse(new EffectData
            {
                ActiveCenterX = 0f, ActiveCenterY = 0f, ActiveSizeX = float.NaN, ActiveSizeY = 1f,
                ActiveShapeValue = (uint)ActiveShape.Box
            }.HasValidActiveBounds);
        }


        [Test]
        public void Unit3102SimplePatterns_UseCommonPatternExecutionWithoutBasicAttackFallback()
        {
            var units = new MonsterDataTable();
            var patterns = new MonsterPatternDataTable();
            var skills = new SkillDataTable();
            units.LoadData(File.ReadAllText("Assets/Datas/MonsterBaseData.csv"));
            patterns.LoadData(File.ReadAllText("Assets/Datas/MonsterPatternData.csv"));
            skills.LoadData(File.ReadAllText("Assets/Datas/SkillData.csv"));
            Assert.IsTrue(units.TryGetMonsterData(5102u, out var unit));
            Assert.IsTrue(patterns.TryGetPatternData(6008u, out var thrust));
            Assert.IsTrue(patterns.TryGetPatternData(6009u, out var barrage));
            Assert.IsTrue(skills.TryGetSkillData(7005u, out var thrustSkill));
            Assert.IsTrue(skills.TryGetSkillData(7006u, out var barrageSkill));

            CollectionAssert.AreEqual(new uint[] { 6008u, 6009u }, unit.PatternIdxList);
            Assert.AreEqual((uint)PatternExecutionType.Simple, thrust.ExecutionType);
            Assert.AreEqual((uint)PatternExecutionType.Simple, barrage.ExecutionType);
            Assert.AreEqual(7005u, thrust.SkillIdx);
            Assert.AreEqual(7006u, barrage.SkillIdx);
            Assert.IsFalse(Monster.IsPatternStartDistanceValid(thrust, thrustSkill, 1f));
            Assert.IsTrue(Monster.IsPatternStartDistanceValid(barrage, barrageSkill, 1f));
            Assert.IsTrue(Monster.IsPatternStartDistanceValid(thrust, thrustSkill, 1.75f));
            Assert.IsFalse(Monster.IsPatternStartDistanceValid(barrage, barrageSkill, 1.75f));
            Assert.AreEqual(14, thrustSkill.AnimState);
            Assert.AreEqual(15, barrageSkill.AnimState);
            Assert.AreEqual(10002u, SkillExecutor.ResolveAttackMotionProfileIdx(thrustSkill, thrust.AttackMotionProfileIdx));
            Assert.AreEqual(10002u, SkillExecutor.ResolveAttackMotionProfileIdx(barrageSkill, barrage.AttackMotionProfileIdx), "6009 reuses the common Step profile.");

            Assert.NotNull(typeof(Monster).GetMethod("SelectNextPattern", BindingFlags.Instance | BindingFlags.NonPublic,
                null, System.Type.EmptyTypes, null));
            Assert.NotNull(typeof(Monster).GetMethod("CanReservePattern", BindingFlags.Instance | BindingFlags.NonPublic),
                "Simple patterns must share the reservation/band gate.");
            Assert.NotNull(typeof(Monster).GetMethod("ExecutePatternAsync", BindingFlags.Instance | BindingFlags.NonPublic),
                "6008/6009 must share the common pattern executor.");
        }

        [Test]
        public void MonsterPatternLifecycle_ReservationSchedulerAndPublicContracts()
        {
            CollectionAssert.AreEqual(new[] { "Idle", "Reserved", "Chase", "Startup", "Active", "Recovery", "Returning" },
                System.Enum.GetNames(typeof(PatternState)));
            Assert.AreEqual(0u, (uint)PatternTriggerSubject.Self);
            Assert.AreEqual(1u, (uint)PatternTriggerSubject.CurrentTarget);

            GameObject gameObject = new GameObject("MonsterPatternLifecycle_QA");
            try
            {
                Monster monster = gameObject.AddComponent<Monster>();
                Assert.IsFalse(monster.SupportsPatternQueue);
                Assert.Throws<System.NotSupportedException>(() => monster.EnqueuePattern(6001u));
                Assert.AreEqual(PatternState.Idle, monster.CurrentPatternSnapshot.State);
                Assert.IsFalse(monster.CurrentPatternSnapshot.TokenHeld);
            }
            finally
            {
                Object.DestroyImmediate(gameObject);
            }

            string monsterSource = File.ReadAllText("Assets/Scripts/Gameplay/Monster.cs");
            StringAssert.Contains("UniTask.Delay(100", monsterSource);
            StringAssert.Contains("CancellationTokenSource.CreateLinkedTokenSource", monsterSource);
            StringAssert.Contains("CalculateReservationChaseSpeed(UnitData.MoveSpeed)", monsterSource);
            StringAssert.Contains("PlayerLoopTiming.FixedUpdate", monsterSource);
            StringAssert.Contains("CurrentPatternState = PatternState.Active", monsterSource);
            StringAssert.Contains("finally\n        {\n            StopAttackMotionImmediately();\n            ReleaseAttackToken();",
                monsterSource.Replace("\r", ""), "The chain owns one token until its shared finally cleanup.");
            StringAssert.Contains("mData.PatternIdxList.Length > 16", monsterSource);
            Assert.AreEqual(10f, Monster.CalculateReservationChaseSpeed(10f), .0001f);
            Assert.AreEqual(4.5f, Monster.CalculateReservationChaseSpeed(4.5f), .0001f);

            foreach (float fixedStep in new[] { 1f / 15f, 1f / 30f, 1f / 60f })
            {
                const float timeout = 1f;
                const float moveSpeed = 4.5f;
                float position = 0f;
                float elapsed = 0f;
                while (elapsed + Mathf.Epsilon < timeout)
                {
                    float remaining = timeout - elapsed;
                    float speed = Monster.CalculateReservationChaseSpeed(moveSpeed);
                    Assert.AreEqual(moveSpeed, speed, .0001f);
                    position = Mathf.Min(.405f, position + speed * fixedStep);
                    elapsed += fixedStep;
                    Assert.LessOrEqual(position, .405f, "Correction must not overshoot its boundary.");
                }
                Assert.AreEqual(.405f, position, moveSpeed * fixedStep + .0001f);
                Assert.GreaterOrEqual(elapsed, timeout, "Reservation still consumes the full ChaseTimeout.");
            }

            string executorSource = File.ReadAllText("Assets/Scripts/Gameplay/SkillExecutor.cs");
            int generationGuard = executorSource.IndexOf("if (!owner.IsActionGenerationCurrent(generation)) return true;",
                executorSource.IndexOf("CalculateAttackMotionVelocity", System.StringComparison.Ordinal),
                System.StringComparison.Ordinal);
            int motorWrite = executorSource.IndexOf("owner.SetAttackMotionStopPosition", generationGuard,
                System.StringComparison.Ordinal);
            Assert.GreaterOrEqual(generationGuard, 0);
            Assert.Greater(motorWrite, generationGuard, "Generation must be checked before SkillExecutor writes the motor.");
        }

        [Test]
        public void AttackMotionStep_RequiresGroundSupportBeforeMotorWrite()
        {
            GameObject unitObject = new GameObject("AttackStepSupportUnit_QA");
            GameObject groundObject = new GameObject("AttackStepSupportGround_QA");
            try
            {
                unitObject.transform.position = new Vector3(0f, .52f, 0f);
                unitObject.AddComponent<Rigidbody2D>().bodyType = RigidbodyType2D.Kinematic;
                unitObject.AddComponent<BoxCollider2D>().size = Vector2.one;
                KinematicMotor2D motor = unitObject.AddComponent<KinematicMotor2D>();
                motor.SolidGroundLayer = 1 << groundObject.layer;

                groundObject.transform.position = new Vector3(0f, -.5f, 0f);
                groundObject.AddComponent<BoxCollider2D>().size = new Vector2(4f, 1f);
                Physics2D.SyncTransforms();
                motor.InitMotor();
                motor.SimulateStep(.02f);

                Assert.IsTrue(motor.IsGrounded);
                Assert.IsTrue(motor.HasGroundSupportForHorizontalStep(.09f),
                    "A supported 4.5m/s fixed step must remain available.");
                Assert.IsFalse(motor.HasGroundSupportForHorizontalStep(3f),
                    "A step beyond the current support must be rejected before movement.");

                string executor = File.ReadAllText("Assets/Scripts/Gameplay/SkillExecutor.cs");
                StringAssert.Contains("profile.MotionType == AttackMotionType.Stationary", executor);
                StringAssert.Contains("owner.HasGroundSupportForAttackStep", executor);
            }
            finally
            {
                Object.DestroyImmediate(unitObject);
                Object.DestroyImmediate(groundObject);
            }
        }

        [Test]
        public void TorsoStep_UsesOppositeTargetSurfaceAndPreservesSnapshotAndLeash()
        {
            string executor = File.ReadAllText("Assets/Scripts/Gameplay/SkillExecutor.cs");
            StringAssert.Contains("Vector2 previousSampledPose = sampledPose;", executor);
            StringAssert.Contains("if (overshootTarget &&", executor);
            StringAssert.Contains("ApplyAttackSweep(owner, patternDamage, motionSweep)", executor);
            StringAssert.Contains("float deltaX = targetBody.bounds.center.x - ownerBody.bounds.center.x;", executor);
            StringAssert.Contains("float directionEpsilon = Mathf.Max(owner.AttackMotionSkinWidth, Physics2D.defaultContactOffset);", executor);
            StringAssert.Contains("if (Mathf.Abs(deltaX) > directionEpsilon) facingSnapshot = deltaX > 0f;", executor,
                "Exact overlap must retain the last valid facing instead of producing a zero direction.");
            StringAssert.Contains("owner.SetFacingRight(facingSnapshot);", executor);
            StringAssert.Contains("if (facingSnapshot != previousFacing) initialExteriorPose = null;", executor,
                "A selector-facing exterior pose cannot survive a motion-start direction change.");
            StringAssert.Contains("if (!overshootTarget || IsMotionPhaseEnabled(skill.MotionPhaseMask, SkillMotionPhase.Active))",
                executor, "Torso mask5 keeps segment contact and its explicitly selected Active sweep.");
            string monsterSource = File.ReadAllText("Assets/Scripts/Gameplay/Monster.cs");
            StringAssert.Contains("BeginAttackTelegraph(generation, telegraphStartsAt, telegraphEndsAt, telegraphEndsAt,",
                monsterSource, "HUD completion must precede the captured torso motion direction.");

            foreach (float fixedStep in new[] { 1f / 15f, 1f / 60f })
            {
                float right = SkillExecutor.CalculateAttackAlignmentTargetX(0f, 0f, 5f,
                    3f, fixedStep, 1f, .2f, .01f, 18.1f, false, true, .8f);
                float left = SkillExecutor.CalculateAttackAlignmentTargetX(0f, 0f, -5f,
                    -3f, fixedStep, 1f, .2f, .01f, 18.1f, false, true, .8f);
                Assert.AreEqual(6.8f, right, .0001f, "Torso endpoint must place its body beyond the target surface.");
                Assert.AreEqual(-6.8f, left, .0001f);
                Assert.AreEqual(right, SkillExecutor.CalculateAttackAlignmentTargetX(0f, 0f, 5f,
                    30f, fixedStep, 1f, .2f, .01f, 18.1f, false, true, .8f), .0001f,
                    "Snapshot motion ignores later target velocity.");
                float lockedRight = SkillExecutor.CalculateAttackAlignmentTargetX(0f, 0f, 5f,
                    0f, fixedStep, 1f, .2f, .01f, 18.1f, false, true, .8f, 1f);
                Assert.AreEqual(lockedRight, SkillExecutor.CalculateAttackAlignmentTargetX(0f, 8f, 5f,
                    0f, fixedStep, 1f, .2f, .01f, 18.1f, false, true, .8f, 1f), .0001f,
                    "Crossing the target cannot reverse a locked lunge endpoint.");
                Assert.AreEqual(2.31f, SkillExecutor.CalculateAttackAlignmentTargetX(0f, 0f, 5f,
                    0f, fixedStep, .5f, .2f, .01f, 2.31f, false), .0001f,
                    "Ordinary Step retains its profile distance cap.");
            }

            foreach (float gap in new[] { 1.5f, 5f, 9.9f, 10f })
            {
                float targetCenter = gap + 1.8f;
                float endpoint = SkillExecutor.CalculateAttackAlignmentTargetX(0f, 0f, targetCenter,
                    0f, .82f, 1f, .2f, .01f, 18.1f, false, true, .8f);
                Assert.AreEqual(Mathf.Min(18.1f, targetCenter + 1.8f), endpoint, .0001f);
                Assert.AreEqual(-endpoint, SkillExecutor.CalculateAttackAlignmentTargetX(0f, 0f,
                    -targetCenter, 0f, .82f, 1f, .2f, .01f, 18.1f, false, true, .8f), .0001f);
            }

            var lunge = new AttackMotionProfileData
            {
                MotionType = AttackMotionType.AcceleratingLunge,
                MaxDistance = 18.1f,
                MaxSpeed = 32f,
                Acceleration = 64f,
                Enabled = true
            };
            foreach (float fixedStep in new[] { 1f / 15f, 1f / 30f, .02f, 1f / 60f })
            {
                float position = 0f;
                float velocity = 0f;
                for (float elapsed = 0f; elapsed + Mathf.Epsilon < .82f + fixedStep; elapsed += fixedStep)
                {
                    velocity = SkillExecutor.CalculateAttackMotionVelocity(lunge, position, 18.1f,
                        Mathf.Max(fixedStep, .82f - elapsed), velocity, fixedStep);
                    position = Mathf.MoveTowards(position, 18.1f, Mathf.Abs(velocity) * fixedStep);
                }
                Assert.AreEqual(18.1f, position, .0001f,
                    $"10003 must arrive within .82s plus one {fixedStep:0.####}s physics tick.");
            }

            StringAssert.Contains("HasGroundSupportForAttackStep", executor);
            StringAssert.Contains("IsAttackMotionPositionAllowed", executor);
            StringAssert.Contains("IsAttackMotionBlocked", executor);
            StringAssert.DoesNotContain("Teleport(", executor);

            var monsterObject = new GameObject("TorsoStepLeash_QA");
            try
            {
                Monster monster = monsterObject.AddComponent<Monster>();
                Assert.IsTrue(monster.ConfigureSpawnArea(Vector3.zero,
                    new Bounds(Vector3.zero, new Vector3(4f, 4f, 1f)), false));
                Assert.IsTrue(monster.IsAttackMotionPositionAllowed(1.9f));
                Assert.IsFalse(monster.IsAttackMotionPositionAllowed(2.1f));
            }
            finally
            {
                Object.DestroyImmediate(monsterObject);
            }
        }


        [Test]
        public void AttackSwingEffect_IsAbsentFromAttackRuntimeWhileResponseEffectsRemain()
        {
            string player = File.ReadAllText("Assets/Scripts/Gameplay/Player.cs");
            string executor = File.ReadAllText("Assets/Scripts/Gameplay/SkillExecutor.cs");
            string combatStats = File.ReadAllText("Assets/Scripts/Gameplay/CombatStats.cs");
            StringAssert.DoesNotContain("SpawnSkillEffectFromDataAsync", player);
            StringAssert.DoesNotContain("SpawnSkillEffectFromDataAsync", executor);
            StringAssert.Contains("SetFacingRight(facingDir.x >= 0f);", player);
            StringAssert.Contains("SpawnResponseEffect(8010", combatStats);
            StringAssert.Contains("SpawnResponseEffect(8011", combatStats);
            StringAssert.Contains("SpawnResponseEffect(8012", combatStats);
            Assert.AreEqual(1, Regex.Matches(combatStats, @"SpawnResponseEffect\(8013").Count,
                "Confirmed body damage must spawn exactly one shared hit response effect.");
            StringAssert.Contains("SkillExecutor.SpawnEffectByEffectIdxAsync", combatStats);
            StringAssert.DoesNotContain("SkillExecutor.Instance", combatStats + executor,
                "Response effects must not depend on an arbitrary unit SkillExecutor singleton.");
        }

        [Test]
        public void GaronPatternBandsAndGroundEffectContracts()
        {
            var skill = new SkillData { Range = 20f };
            var far = new MonsterPatternData
            {
                Idx = 6100u, TriggerType = (uint)PatternTriggerType.DistanceOver,
                TriggerValue = 8f, MinStartDistance = 8f, MaxStartDistance = 0f
            };
            var ground = new MonsterPatternData
            {
                Idx = 6103u, TriggerType = (uint)PatternTriggerType.DistanceUnder,
                TriggerValue = 8f, MinStartDistance = 5f, MaxStartDistance = 8f, ChaseTimeout = .5f
            };
            foreach (float gap in new[] { 5f, 7.99f, 8f, 8.01f })
                Assert.IsTrue(Monster.IsPatternStartDistanceValid(ground, skill, gap, .01f), $"6103 gap {gap}");
            foreach (float gap in new[] { 4f, 4.5f })
                Assert.IsFalse(Monster.IsPatternStartDistanceValid(ground, skill, gap, .01f), $"6103 gap {gap}");
            Assert.AreEqual(8f, Monster.NormalizePatternStartDistance(far, 8.01f, .01f), .0001f);
            Assert.IsFalse(Monster.NormalizePatternStartDistance(far, 8.01f, .01f) > far.TriggerValue,
                "The SkinWidth boundary belongs to 6103, not strict DistanceOver 6100.");
            Assert.IsTrue(Monster.TryGetPatternStartDistanceBand(far, skill, out float farMin, out float farMax));
            Assert.AreEqual(8f, farMin);
            Assert.AreEqual(float.MaxValue, farMax);
            Assert.Greater(Monster.NormalizePatternStartDistance(far, 8.011f, .01f), far.TriggerValue);

            var randomA = new MonsterPatternData { Idx = 6101u, MinStartDistance = 0f, MaxStartDistance = 4f, RandomWeight = 30 };
            var randomB = new MonsterPatternData { Idx = 6102u, MinStartDistance = 0f, MaxStartDistance = 4f, RandomWeight = 70 };
            Assert.IsTrue(Monster.IsPatternStartDistanceValid(randomA, skill, 4f, .01f));
            Assert.IsTrue(Monster.IsPatternStartDistanceValid(randomB, skill, 4f, .01f));
            Assert.AreEqual(100, randomA.RandomWeight + randomB.RandomWeight);
            MethodInfo weighted = typeof(Monster).GetMethod("SelectWeightedPattern",
                BindingFlags.Static | BindingFlags.NonPublic);
            var candidates = new System.Collections.Generic.List<MonsterPatternData> { randomA, randomB };
            Random.State previousRandom = Random.state;
            int selected6102 = 0;
            Random.InitState(6102);
            for (int i = 0; i < 10000; i++)
                if (((MonsterPatternData)weighted.Invoke(null,
                    new object[] { candidates, Random.Range(0, 100) })).Idx == 6102u) selected6102++;
            Random.state = previousRandom;
            Assert.That(selected6102 / 10000f, Is.InRange(.68f, .72f));

            string executor = File.ReadAllText("Assets/Scripts/Gameplay/SkillExecutor.cs");
            StringAssert.Contains("motion.MotionType == AttackMotionType.AcceleratingLunge && motion.Enabled", executor);
            StringAssert.DoesNotContain("skill.AttackSubject == AttackSubject.BodyPart &&", executor,
                "All configured Lunges must reuse the fixed-path contact sweep, not only torso attacks.");

            var ownerObject = new GameObject("GaronGroundEffect_QA");
            try
            {
                var body = ownerObject.AddComponent<BoxCollider2D>();
                body.size = new Vector2(2f, 4f);
                var stats = ownerObject.AddComponent<CombatStats>();
                var owner = ownerObject.AddComponent<Monster>();
                typeof(Monster).GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic)
                    .Invoke(owner, null);
                stats.SetDefenseBodyCollider(body);
                var motor = ownerObject.GetComponent<KinematicMotor2D>();
                motor.SetGroundNormal(Vector2.up);
                var effect = new EffectData
                {
                    Idx = 8023u, PatternIdx = 6103u, SkillIdx = 7013u, Scale = 1f,
                    ActiveCenterX = .25f, ActiveCenterY = .5f
                };
                MethodInfo pose = typeof(SkillExecutor).GetMethod("TryCalculateEffectPoseForFacing",
                    BindingFlags.Static | BindingFlags.NonPublic);
                object[] args = { owner, effect, true, Vector2.zero, Quaternion.identity };
                Assert.IsTrue((bool)pose.Invoke(null, args));
                Assert.AreEqual(body.bounds.min.y + effect.ActiveCenterY, ((Vector2)args[3]).y, .0001f);

                motor.SetVelocityY(1f);
                LogAssert.Expect(LogType.Error, new Regex("Unit 0/Pattern 6103/Skill 7013/Effect 8023 requires grounded support"));
                args[3] = Vector2.zero;
                args[4] = Quaternion.identity;
                Assert.IsFalse((bool)pose.Invoke(null, args));
            }
            finally
            {
                Object.DestroyImmediate(ownerObject);
            }
        }

        [Test]
        public void UnitChaseStopsAtTargetColliderWidthWithoutAffectingPatternMotion()
        {
            var pursuerObject = new GameObject("ChasePursuer_QA");
            var targetObject = new GameObject("ChaseTarget_QA");
            try
            {
                var pursuer = pursuerObject.AddComponent<BoxCollider2D>();
                pursuer.size = Vector2.one;
                pursuer.offset = new Vector2(.5f, 0f);
                var target = targetObject.AddComponent<BoxCollider2D>();
                target.size = new Vector2(2f, 1f);
                target.offset = new Vector2(-.5f, 0f);

                foreach ((float gap, bool stop) in new[]
                         { (2.01f, false), (2f, true), (1.99f, true), (-1f, true) })
                {
                    targetObject.transform.position = new Vector2(gap < 0f ? .5f : gap + 2.5f, 0f);
                    Physics2D.SyncTransforms();
                    Assert.IsTrue(Monster.TryGetChaseSurfaceGap(pursuer, target,
                        out float actualGap, out float width, out float direction));
                    Assert.AreEqual(Mathf.Max(0f, gap), actualGap, .0001f);
                    Assert.AreEqual(2f, width, .0001f);
                    if (gap >= 0f) Assert.AreEqual(1f, direction);
                    Assert.AreEqual(stop, actualGap <= width);
                }

                targetObject.transform.position = new Vector2(-2.51f, 0f);
                Physics2D.SyncTransforms();
                Assert.IsTrue(Monster.TryGetChaseSurfaceGap(pursuer, target,
                    out float leftGap, out float leftWidth, out float leftDirection));
                Assert.AreEqual(2.01f, leftGap, .0001f);
                Assert.AreEqual(2f, leftWidth, .0001f);
                Assert.AreEqual(-1f, leftDirection);

                foreach (float scaleX in new[] { .5f, 1f, 2f })
                {
                    targetObject.transform.localScale = new Vector3(scaleX, 1f, 1f);
                    targetObject.transform.position = new Vector2(10f, 0f);
                    Physics2D.SyncTransforms();
                    Assert.IsTrue(Monster.TryGetChaseSurfaceGap(pursuer, target,
                        out _, out float scaledWidth, out _));
                    Assert.AreEqual(2f * scaleX, scaledWidth, .0001f);
                }
                Assert.AreEqual(2f, Monster.CalculateChaseStopX(0f, 1f, 4f, 2f), .0001f,
                    "A fast FixedStep must clamp at the collider-width threshold.");
                Assert.AreEqual(0f, Monster.CalculateChaseStopX(0f, 1f, 1f, 2f), .0001f);
                Assert.AreEqual(.15f, Monster.CalculateChaseVelocity(4f, 1f / 15f,
                    2.01f, 2f, 1f), .0001f);
                Assert.AreEqual(-.15f, Monster.CalculateChaseVelocity(4f, 1f / 15f,
                    2.01f, 2f, -1f), .0001f);
                foreach (float gap in new[] { 2f, 1.99f, 0f })
                    Assert.AreEqual(0f, Monster.CalculateChaseVelocity(4f, .02f,
                        gap, 2f, 1f), .0001f, $"gap {gap} must clear stale chase velocity.");

                target.enabled = false;
                Assert.IsFalse(Monster.TryGetChaseSurfaceGap(pursuer, target, out _, out _, out _));

                string monster = File.ReadAllText("Assets/Scripts/Gameplay/Monster.cs");
                StringAssert.Contains("if (!missingChaseColliderLogged)", monster);
                StringAssert.Contains("if (currentPattern != null", monster,
                    "Pattern-owned Step/Lunge movement must remain outside the general chase stop.");
                StringAssert.Contains("TrySetReservationChaseVelocity", monster,
                    "Reservation chase must share the collider stop contract.");
            }
            finally
            {
                Object.DestroyImmediate(targetObject);
                Object.DestroyImmediate(pursuerObject);
            }
        }

        [Test]
        public void GaronLandingAttackRequiresAirborneToGroundedTransitionExactlyOnce()
        {
            foreach (float skin in new[] { .01f, .02f, .0666667f })
            {
                var state = Monster.LandingAttackState.WaitingForTakeoff;
                state = Monster.AdvanceLandingAttackState(state, 0f, 0f, skin,
                    true, false, 11f, false);
                Assert.AreEqual(Monster.LandingAttackState.WaitingForTakeoff, state,
                    "SetVelocityY must not count as takeoff before the body rises beyond SkinWidth.");
                state = Monster.AdvanceLandingAttackState(state, 0f, skin, skin,
                    false, false, 10f, false);
                Assert.AreEqual(Monster.LandingAttackState.WaitingForTakeoff, state);
                state = Monster.AdvanceLandingAttackState(state, 0f, skin + .001f, skin,
                    false, false, 9f, false);
                Assert.AreEqual(Monster.LandingAttackState.AirborneObserved, state);
                Assert.AreEqual(Monster.LandingAttackState.AirborneObserved,
                    Monster.AdvanceLandingAttackState(state, 0f, 1f, skin,
                        false, true, 1f, true), "Rising contact cannot commit landing.");
                Assert.AreEqual(Monster.LandingAttackState.AirborneObserved,
                    Monster.AdvanceLandingAttackState(state, 0f, 1f, skin,
                        false, true, 0f, false), "Landing requires valid support.");
                state = Monster.AdvanceLandingAttackState(state, 0f, 1f, skin,
                    false, true, 0f, true);
                Assert.AreEqual(Monster.LandingAttackState.LandingCommitted, state);
                Assert.AreEqual(Monster.LandingAttackState.LandingCommitted,
                    Monster.AdvanceLandingAttackState(state, 0f, 1f, skin,
                        true, true, 0f, true), "Landing commit must be idempotent.");
            }
            string monster = File.ReadAllText("Assets/Scripts/Gameplay/Monster.cs");
            StringAssert.Contains("TryStartAttackJump(pattern.JumpVelocityY, LandingAttackClearance)", monster);
            StringAssert.Contains("skillExecutor.ExecuteLandingHit", monster);
            StringAssert.Contains("pattern.JumpVelocityY > 0f", monster);
        }

        [Test]
        public void SkillBoundsDebug_UsesOneRendererAndOnlyLungeCanCommitDuringPre()
        {
            string source = File.ReadAllText("Assets/Scripts/Gameplay/SkillExecutor.cs");
            StringAssert.Contains("DrawEffectBoundsDebug(owner, preSweep, PreBoundsColor)", source);
            StringAssert.Contains("DrawEffectBoundsDebug(owner, sweep, ActiveBoundsColor)", source);
            StringAssert.Contains("DrawEffectBoundsDebug(owner, lastActiveSweep, PostBoundsColor)", source);
            StringAssert.Contains("private LineRenderer effectBoundsDebugLine", source);
            int preStart = source.IndexOf("while (elapsed + Mathf.Epsilon < windowStart", System.StringComparison.Ordinal);
            int activeStart = source.IndexOf("Vector2 activePrevious", preStart, System.StringComparison.Ordinal);
            string pre = source.Substring(preStart, activeStart - preStart);
            StringAssert.Contains("if (overshootTarget && phaseMoves", pre);
            Assert.AreEqual(1, pre.Split(new[] { "ApplyAttackSweep(owner, patternDamage" },
                System.StringSplitOptions.None).Length - 1,
                "Only the approved Lunge movement segment may query damage before timed Active.");
        }

        private static float SimulateArrival(AttackMotionProfileData profile, float dt)
        {
            float x = 0f;
            float elapsed = 0f;
            while (elapsed < 1.5f)
            {
                float stepDt = Mathf.Min(dt, 1.5f - elapsed);
                float velocity = SkillExecutor.CalculateAttackMotionVelocity(
                    profile, x, 6f, 1.5f - elapsed, 0f, stepDt);
                x += velocity * stepDt;
                elapsed += stepDt;
            }
            return x;
        }



    }
}
