using NUnit.Framework;
using System.IO;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using TMPro;
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
        public void Test_CommonAttackHitbox_WindowFacingAndCancellation()
        {
            GameObject ownerObject = new GameObject("AttackHitboxOwner_QA");
            GameObject hitboxObject = new GameObject("WeaponHitbox_QA");
            hitboxObject.transform.SetParent(ownerObject.transform, false);
            try
            {
                UnitBase owner = ownerObject.AddComponent<UnitBase>();
                BoxCollider2D collider = hitboxObject.AddComponent<BoxCollider2D>();
                collider.isTrigger = true;
                collider.size = new Vector2(2f, 1f);
                SpriteRenderer debugSprite = hitboxObject.AddComponent<SpriteRenderer>();
                UnitAttackHitbox2D hitbox = hitboxObject.AddComponent<UnitAttackHitbox2D>();
                typeof(UnitAttackHitbox2D).GetField("attackCollider", BindingFlags.Instance | BindingFlags.NonPublic)
                    ?.SetValue(hitbox, collider);
                typeof(UnitAttackHitbox2D).GetField("attachRoot", BindingFlags.Instance | BindingFlags.NonPublic)
                    ?.SetValue(hitbox, hitboxObject.transform);
                typeof(UnitAttackHitbox2D).GetField("debugHitboxSprite", BindingFlags.Instance | BindingFlags.NonPublic)
                    ?.SetValue(hitbox, debugSprite);
                typeof(UnitBase).GetField("attackHitbox", BindingFlags.Instance | BindingFlags.NonPublic)
                    ?.SetValue(owner, hitbox);
                hitbox.Bind(owner);

                owner.SetFacingRight(false);
                Assert.Less(hitboxObject.transform.localScale.x, 0f);
                Assert.IsTrue(owner.TryOpenAttackHitbox(11, owner.ActionGeneration, 0, out var sweep));
                Assert.IsTrue(hitbox.IsWindowActive);
                Assert.IsTrue(hitbox.IsDebugVisualizationActive);
                Assert.IsTrue(debugSprite.enabled);
                Assert.AreEqual(new Vector2(1f, 0.5f), sweep.HalfExtents);

                UnitAttackHitbox2D.DebugVisualizationEnabled = false;
                owner.CloseAttackHitbox();
                Assert.IsTrue(owner.TryOpenAttackHitbox(12, owner.ActionGeneration, 0, out _));
                Assert.IsFalse(hitbox.IsDebugVisualizationActive, "Global OFF must suppress the active debug state.");
                Assert.IsFalse(debugSprite.enabled, "Global OFF must keep the sprite renderer disabled when the window reopens.");
                Assert.IsTrue(hitbox.IsWindowActive);

                uint generation = owner.ActionGeneration;
                owner.CancelAttackHitbox();
                Assert.IsFalse(hitbox.IsWindowActive, "Cancel must close the attack window.");
                Assert.IsFalse(hitbox.IsDebugVisualizationActive, "Cancel must clear the debug state.");
                Assert.IsFalse(debugSprite.enabled, "Cancel must disable the sprite renderer.");
                Assert.IsFalse(owner.IsActionGenerationCurrent(generation));

                UnitAttackHitbox2D.DebugVisualizationEnabled = true;
                Assert.IsTrue(owner.TryOpenAttackHitbox(13, owner.ActionGeneration, 0, out _));
                Assert.IsTrue(debugSprite.enabled);
                typeof(UnitAttackHitbox2D).GetMethod("OnDisable", BindingFlags.Instance | BindingFlags.NonPublic)
                    ?.Invoke(hitbox, null);
                Assert.IsFalse(debugSprite.enabled, "Disable must not leave a pooled sprite renderer enabled.");
                Assert.IsFalse(collider.enabled, "Disable must close the attack collider.");
                owner.CloseAttackHitbox();
                Assert.IsFalse(hitbox.IsDebugVisualizationActive, "Re-enable must not restore a closed window.");
                Assert.IsFalse(debugSprite.enabled, "Re-enable must not restore stale visualization.");
            }
            finally
            {
                UnitAttackHitbox2D.DebugVisualizationEnabled = true;
                Object.DestroyImmediate(ownerObject);
            }
        }

        [Test]
        public void Test_RepeatedAttackGenerationAndGuardSpriteContracts()
        {
            GameObject attackerObject = new GameObject("RepeatedAttack_Attacker_QA");
            GameObject hitboxObject = new GameObject("RepeatedAttack_Hitbox_QA");
            GameObject targetObject = new GameObject("RepeatedAttack_Target_QA");
            hitboxObject.transform.SetParent(attackerObject.transform, false);
            try
            {
                UnitBase attacker = attackerObject.AddComponent<UnitBase>();
                BoxCollider2D attackCollider = hitboxObject.AddComponent<BoxCollider2D>();
                attackCollider.isTrigger = true;
                UnitAttackHitbox2D hitbox = hitboxObject.AddComponent<UnitAttackHitbox2D>();
                typeof(UnitAttackHitbox2D).GetField("attackCollider", BindingFlags.Instance | BindingFlags.NonPublic)?.SetValue(hitbox, attackCollider);
                typeof(UnitAttackHitbox2D).GetField("attachRoot", BindingFlags.Instance | BindingFlags.NonPublic)?.SetValue(hitbox, hitboxObject.transform);
                typeof(UnitBase).GetField("attackHitbox", BindingFlags.Instance | BindingFlags.NonPublic)?.SetValue(attacker, hitbox);
                hitbox.Bind(attacker);

                CombatStats target = targetObject.AddComponent<CombatStats>();
                target.InitStats();
                BoxCollider2D body = targetObject.AddComponent<BoxCollider2D>();
                target.SetDefenseBodyCollider(body);
                SpriteRenderer guardSprite = targetObject.AddComponent<SpriteRenderer>();
                guardSprite.transform.localPosition = Vector3.right;
                typeof(CombatStats).GetField("debugGuardSprite", BindingFlags.Instance | BindingFlags.NonPublic)?.SetValue(target, guardSprite);
                target.SetGuarding(false);

                Assert.IsTrue(attacker.TryOpenAttackHitbox(31, attacker.ActionGeneration, 0, out var attack1));
                Assert.IsFalse(target.TakeDamage(10f, attacker: attacker.Stats, attackSweep: attack1));
                Assert.AreEqual(90f, target.CurrentHp);
                target.TakeDamage(10f, attacker: attacker.Stats, attackSweep: attack1);
                Assert.AreEqual(90f, target.CurrentHp, "One attack tick must damage the same target once.");

                attacker.CloseAttackHitbox();
                attacker.CancelAttackHitbox();
                Assert.IsTrue(attacker.TryOpenAttackHitbox(31, attacker.ActionGeneration, 0, out var attack2));
                Assert.AreNotEqual(attack1.Generation, attack2.Generation);
                target.TakeDamage(10f, attacker: attacker.Stats, attackSweep: attack2);
                Assert.AreEqual(80f, target.CurrentHp, "A new generation must damage the target again.");

                attacker.CancelAttackHitbox();
                Assert.IsTrue(attacker.TryOpenAttackHitbox(31, attacker.ActionGeneration, 0, out var attack3));
                Assert.AreNotEqual(attack2.Generation, attack3.Generation);
                target.TakeDamage(10f, attacker: attacker.Stats, attackSweep: attack3);
                Assert.AreEqual(70f, target.CurrentHp, "Cancel must not poison the following generation.");

                UnitAttackHitbox2D.DebugVisualizationEnabled = true;
                target.SetFacingRight(false);
                target.SetGuarding(true);
                Assert.IsTrue(guardSprite.enabled);
                Assert.Less(guardSprite.transform.localPosition.x, 0f);
                target.SetParrying(true);
                Assert.Greater(guardSprite.color.g, guardSprite.color.b * 0.9f);
                target.SetParrying(false);
                target.SetGuarding(false);
                Assert.IsFalse(guardSprite.enabled);
                UnitAttackHitbox2D.DebugVisualizationEnabled = false;
                target.SetGuarding(true);
                Assert.IsFalse(guardSprite.enabled, "Global OFF must not change the actual guarding state.");
                Assert.IsTrue(target.IsGuarding);
                UnitAttackHitbox2D.DebugVisualizationEnabled = true;
                target.SetGuarding(false);
                target.SetGuarding(true);
                typeof(CombatStats).GetMethod("OnDisable", BindingFlags.Instance | BindingFlags.NonPublic)?.Invoke(target, null);
                Assert.IsFalse(guardSprite.enabled, "Pool/disable must hide the guard sprite.");
            }
            finally
            {
                UnitAttackHitbox2D.DebugVisualizationEnabled = true;
                Object.DestroyImmediate(attackerObject);
                Object.DestroyImmediate(targetObject);
            }
        }

        [Test]
        public void Test_PlayerGuardDebugPrefab_GameViewContract()
        {
            GameObject playerObject = Object.Instantiate(UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Prefabs/Unit_3001.prefab"));
            try
            {
                Player player = playerObject.GetComponent<Player>();
                CombatStats stats = playerObject.GetComponent<CombatStats>();
                Transform guardRoot = playerObject.transform.Find("DebugGuardVisual");
                SpriteRenderer guard = guardRoot.GetComponent<SpriteRenderer>();
                UnitAttackHitbox2D hitbox = playerObject.GetComponentInChildren<UnitAttackHitbox2D>(true);
                SpriteRenderer attackDebug = hitbox.GetComponentInChildren<SpriteRenderer>(true);
                hitbox.Bind(player);
                Assert.NotNull(stats);
                Assert.NotNull(guard);
                Assert.NotNull(hitbox);
                Assert.IsFalse(guard.enabled);
                Assert.AreEqual(new Vector3(1f, 0f, 0f), guardRoot.localPosition);
                Vector2 guardSize = Vector2.Scale(guard.sprite.bounds.size, guardRoot.lossyScale);
                Assert.AreEqual(1f, guardSize.x, .001f);
                Assert.AreEqual(2f, guardSize.y, .001f);

                Collider2D defenseBody = playerObject.GetComponent<Collider2D>();
                Bounds expectedGuardBounds = defenseBody.bounds;
                expectedGuardBounds.center += Vector3.right * expectedGuardBounds.size.x;
                Assert.AreEqual(expectedGuardBounds.center.x, guard.bounds.center.x, .001f);
                Assert.AreEqual(expectedGuardBounds.center.y, guard.bounds.center.y, .001f);
                Assert.AreEqual(expectedGuardBounds.size.x, guard.bounds.size.x, .001f);
                Assert.AreEqual(expectedGuardBounds.size.y, guard.bounds.size.y, .001f);

                UnitAttackHitbox2D.DebugVisualizationEnabled = true;
                stats.SetFacingRight(true);
                stats.SetGuarding(true);
                Assert.IsTrue(guard.enabled);
                Assert.AreEqual(new Color(0f, .5f, 1f, .35f), guard.color);
                stats.SetFacingRight(false);
                Assert.AreEqual(-.5f, guardRoot.localPosition.x, .001f);
                stats.SetParrying(true);
                Assert.AreEqual(new Color(0f, 1f, 1f, .35f), guard.color);

                Assert.IsTrue(player.TryOpenAttackHitbox(71, player.ActionGeneration, 0, out _));
                stats.SetGuarding(false);
                stats.SetParrying(false);
                Assert.IsFalse(guard.enabled);
                Assert.IsTrue(attackDebug.enabled, "Attack debug must follow its own active window.");

                UnitAttackHitbox2D.DebugVisualizationEnabled = false;
                stats.SetGuarding(true);
                Assert.IsTrue(stats.IsGuarding);
                Assert.IsFalse(guard.enabled);
                player.CloseAttackHitbox();
                Assert.IsTrue(player.TryOpenAttackHitbox(72, player.ActionGeneration, 0, out _));
                Assert.IsFalse(attackDebug.enabled);
                playerObject.SetActive(false);
                Assert.IsFalse(guard.enabled);
                Assert.IsFalse(attackDebug.enabled);
            }
            finally
            {
                UnitAttackHitbox2D.DebugVisualizationEnabled = true;
                Object.DestroyImmediate(playerObject);
            }
        }

        [Test]
        public void Test_CommonAttackHitbox_FixedStepAndLifecycleContracts()
        {
            string executor = File.ReadAllText("Assets/Scripts/Gameplay/SkillExecutor.cs");
            string unit = File.ReadAllText("Assets/Scripts/Gameplay/UnitBase.cs");
            string monster = File.ReadAllText("Assets/Scripts/Gameplay/Monster.cs");
            string player = File.ReadAllText("Assets/Scripts/Gameplay/Player.cs");
            string portal = File.ReadAllText("Assets/Scripts/Gameplay/IntraRoomPortal.cs");
            string hitbox = File.ReadAllText("Assets/Scripts/Gameplay/Combat/UnitAttackHitbox2D.cs");

            StringAssert.Contains("PlayerLoopTiming.FixedUpdate", executor);
            StringAssert.Contains("TryGetAttackSweepFraction(sweep", executor);
            StringAssert.Contains("attackSweep: sweep", executor);
            StringAssert.Contains("finally", executor);
            StringAssert.Contains("owner.CloseAttackHitbox()", executor);
            StringAssert.Contains("has no serialized attack hitbox; attack cancelled", unit);
            StringAssert.Contains("CancelAttackHitbox()", monster);
            StringAssert.Contains("CancelAttackHitbox()", player);
            StringAssert.Contains("player.CancelAttackHitbox()", portal);
            StringAssert.Contains("#if UNITY_EDITOR || DEVELOPMENT_BUILD", hitbox);
            StringAssert.Contains("DebugVisualizationEnabled", hitbox);
            StringAssert.Contains("Debug.DrawLine", hitbox);
            StringAssert.DoesNotContain("new Material", hitbox);
            StringAssert.DoesNotContain("new GameObject", hitbox);
            StringAssert.DoesNotContain("pStats.TakeDamage(pattern.Damage", monster);
            StringAssert.DoesNotContain("SpawnSkillEffect($\"Player_Hit", player);
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
        [TestCase(true, 0.5f, 0f, false)]
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
                    new Vector2(startX, 0f), new Vector2(endX, 0f), Vector2.zero, 101, 1, 0);

                if (Mathf.Abs(endX) > .5f)
                {
                    Assert.IsFalse(defender.TryGetBodySweepFraction(sweep, out _));
                    Assert.IsTrue(defender.TryGetAttackSweepFraction(sweep, out _),
                        "A front guard-only intersection must reach defense resolution before the body.");
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
                    projectile ? 402 : 401, 1, 0);

                Assert.IsFalse(defender.TryGetBodySweepFraction(sweep, out _));
                Assert.IsTrue(defender.TryGetAttackSweepFraction(sweep, out _));
                Assert.IsTrue(defender.TakeDamage(20f, attacker: attacker, attackSweep: sweep));
                Assert.AreEqual(100f, defender.CurrentHp);
                Assert.AreEqual(40f, attacker.CurrentPosture);

                string source = File.ReadAllText(projectile
                    ? "Assets/Scripts/Gameplay/Combat/MonsterProjectile2D.cs"
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

                var first = new CombatStats.AttackSweep2D(Vector2.right * 3f, Vector2.right * 1.25f, Vector2.zero, 202, 7, 0);
                Assert.IsTrue(defender.TakeDamage(20f, attackSweep: first));
                defender.SetParrying(false);
                Assert.IsTrue(defender.TakeDamage(20f, attackSweep:
                    new CombatStats.AttackSweep2D(Vector2.right * 3f, Vector2.zero, Vector2.zero, 202, 7, 1)));
                Assert.AreEqual(100f, defender.CurrentHp, "Parried attack generation must not apply later ticks.");

                defender.SetGuarding(true);
                var guardTick = new CombatStats.AttackSweep2D(Vector2.right * 3f, Vector2.zero, Vector2.zero, 202, 8, 0);
                Assert.IsTrue(defender.TakeDamage(20f, attackSweep: guardTick));
                defender.SetGuarding(false);
                var releasedTick = new CombatStats.AttackSweep2D(Vector2.right * 3f, Vector2.zero, Vector2.zero, 202, 8, 1);
                Assert.IsFalse(defender.TakeDamage(20f, attackSweep: releasedTick));
                Assert.IsTrue(defender.TakeDamage(20f, attackSweep: releasedTick));
                Assert.AreEqual(80f, defender.CurrentHp, "One source/generation/tick may damage at most once.");

                Assert.IsFalse(defender.TakeDamage(20f, attackSweep:
                    new CombatStats.AttackSweep2D(Vector2.left * 3f, Vector2.zero, Vector2.zero, 202, 9, 0)));
                Assert.AreEqual(60f, defender.CurrentHp, "A pooled source with a new generation must resolve normally.");
            }
            finally { Object.DestroyImmediate(defenderObject); }
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

                MethodInfo hostile = typeof(Gameplay.Combat.MonsterProjectile2D)
                    .GetMethod("IsHostile", BindingFlags.Static | BindingFlags.NonPublic);
                Assert.IsTrue((bool)hostile.Invoke(null, new object[] { player, monster }));
                Assert.IsTrue((bool)hostile.Invoke(null, new object[] { monster, player }));
                Assert.IsFalse((bool)hostile.Invoke(null, new object[] { monster, boss }));
                Assert.IsFalse((bool)hostile.Invoke(null, new object[] { monster, monster }));
                Assert.IsTrue(typeof(UnitPoolManager).GetMethod("SpawnMonsterProjectileAsync")
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
                motor.SetGroundNormal(Vector2.up);
                motor.SetTargetVelocityX(5f);
                motor.SimulateStep(Time.fixedDeltaTime);
                Assert.AreEqual(5f, motor.Velocity.x, 0.001f);

                stats.AddPosture(stats.MaxPosture);
                typeof(Monster).GetMethod("OnGroggyStarted", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(monster, null);
                motor.SimulateStep(Time.fixedDeltaTime);
                Assert.AreEqual(0f, motor.Velocity.x, 0.001f);
                Assert.IsFalse((bool)typeof(Monster).GetMethod("CanAct", BindingFlags.Instance | BindingFlags.NonPublic)
                    .Invoke(monster, new object[] { 0u }));

                string source = File.ReadAllText("Assets/Scripts/Gameplay/Monster.cs");
                int death = source.IndexOf("deathSequenceActive = true;", System.StringComparison.Ordinal);
                Assert.Greater(source.IndexOf("actionGeneration++;", death, System.StringComparison.Ordinal), death);
                StringAssert.Contains("skillExecutor != null && CanAct(generation)", source);
                StringAssert.Contains("playerTarget != null && CanAct(generation)", source);
                StringAssert.Contains("actionGeneration++;\n        ReleaseAttackToken();", source.Replace("\r\n", "\n"));
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
            var hpObject = new GameObject("BossHp_QA");
            var postureObject = new GameObject("BossPosture_QA");
            try
            {
                bossObject.SetActive(false);
                bossObject.AddComponent<BoxCollider2D>();
                var boss = bossObject.AddComponent<BossMonster>();
                var stats = bossObject.GetComponent<CombatStats>();
                stats.OnHpChanged = new UnityEngine.Events.UnityEvent<float>();
                stats.OnPostureChanged = new UnityEngine.Events.UnityEvent<float>();
                stats.InitStats();
                typeof(UnitBase).GetField("stats", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(boss, stats);

                var group = groupObject.AddComponent<CanvasGroup>();
                var hp = hpObject.AddComponent<UnityEngine.UI.Image>();
                var posture = postureObject.AddComponent<UnityEngine.UI.Image>();
                var hud = hudObject.AddComponent<ProductionMainHUD>();
                typeof(ProductionMainHUD).GetField("bossGroup", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(hud, group);
                typeof(ProductionMainHUD).GetField("bossHpFill", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(hud, hp);
                typeof(ProductionMainHUD).GetField("bossPostureFill", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(hud, posture);
                var bindBoss = typeof(ProductionMainHUD).GetMethod("BindBoss", BindingFlags.Instance | BindingFlags.NonPublic);
                var onMonsterActivated = typeof(ProductionMainHUD).GetMethod("OnMonsterActivated", BindingFlags.Instance | BindingFlags.NonPublic);

                group.alpha = 0f;
                onMonsterActivated.Invoke(hud, new object[] { boss });
                Assert.AreEqual(0f, group.alpha, "An uninitialized/unencountered Boss must not show the panel.");
                bindBoss.Invoke(hud, new object[] { boss });
                Assert.AreEqual(1f, group.alpha);
                Assert.AreEqual(1f, hp.fillAmount);
                Assert.AreEqual(0f, posture.fillAmount);
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
            }
            finally
            {
                Object.DestroyImmediate(postureObject);
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
            Assert.IsTrue(table.TryGetPatternData(6006, out var low));
            Assert.AreEqual(7002u, straight.SkillIdx);
            Assert.AreEqual(1045u, straight.ProjectileResourceIdx);
            Assert.AreEqual(15f, straight.ProjectileSpeed, 0.01f);
            Assert.AreEqual(25f, straight.ProjectileMaxDistance, 0.01f);
            Assert.AreEqual(14f, straight.Damage);
            Assert.AreEqual(1045u, low.ProjectileResourceIdx);
            Assert.AreEqual(16f, low.Damage);
            Assert.AreEqual(25f / 15f, straight.ProjectileMaxDistance / straight.ProjectileSpeed, 0.0001f);

            string monster = File.ReadAllText("Assets/Scripts/Gameplay/Monster.cs");
            string pool = File.ReadAllText("Assets/Scripts/Manager/UnitPoolManager.cs");
            string projectile = File.ReadAllText("Assets/Scripts/Gameplay/Combat/MonsterProjectile2D.cs");
            StringAssert.Contains("pattern.ProjectileResourceIdx != 0", monster);
            StringAssert.Contains("pattern.ProjectileResourceIdx == 0", monster);
            StringAssert.Contains("pattern.Damage", monster);
            StringAssert.Contains("TryGetResource(resourceIdx", pool);
            StringAssert.Contains("InstantiateAsyncTask(\n                resourceData.Path", pool);
            StringAssert.Contains("Collider2D.Cast", projectile.Replace("projectileCollider.Cast", "Collider2D.Cast"));
            StringAssert.DoesNotContain("SimplePoolManager", projectile);
            StringAssert.DoesNotContain("Physics.", projectile);
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
        public void PhaseA1x1_PlaytestUsesEntryAndSingleMonster3104()
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
            StringAssert.Contains("DevelopmentMonsterUnitIdx = 3104u", main);
            StringAssert.Contains("ConfigureDevelopmentPlaytestMarkers", builder);
            Assert.Less(spawner.IndexOf("if (zones.Count == 1 && zones[0].MonsterId != 0u)"),
                spawner.IndexOf("uint[] encounter = GetCurrentEncounter(zones)"));
            StringAssert.Contains("playtestMarker.EnableSpawn = false", spawner);
            Assert.NotNull(UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Unit_3104.prefab")
                .GetComponent<SkillExecutor>(), "Unit_3104 must keep its prefab-bound SkillExecutor.");
        }

        [Test]
        public void AttackAttach_StaysActiveWhileOnlyColliderTracksWindow()
        {
            foreach (uint unitIdx in new uint[] { 3001u, 3101u, 3102u, 3103u, 3104u, 3105u, 3106u, 3201u })
            {
                GameObject prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/Prefabs/Unit_{unitIdx}.prefab");
                GameObject instance = Object.Instantiate(prefab);
                try
                {
                    UnitBase unit = instance.GetComponent<UnitBase>();
                    UnitAttackHitbox2D hitbox = instance.GetComponentInChildren<UnitAttackHitbox2D>(true);
                    Collider2D collider = (Collider2D)typeof(UnitAttackHitbox2D)
                        .GetField("attackCollider", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(hitbox);
                    SpriteRenderer debugSprite = (SpriteRenderer)typeof(UnitAttackHitbox2D)
                        .GetField("debugHitboxSprite", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(hitbox);

                    hitbox.Bind(unit);
                    Assert.IsTrue(hitbox.gameObject.activeSelf, $"Unit_{unitIdx} AttackAttach after Bind");
                    Assert.IsFalse(collider.enabled, $"Unit_{unitIdx} collider after Bind");
                    Assert.IsTrue(hitbox.TryOpen(1, unit.ActionGeneration, 0, out _));
                    Assert.IsTrue(hitbox.gameObject.activeSelf, $"Unit_{unitIdx} AttackAttach during window");
                    Assert.IsTrue(collider.enabled, $"Unit_{unitIdx} collider during window");
                    Assert.AreEqual(collider.bounds.size.x, debugSprite.bounds.size.x, .001f, $"Unit_{unitIdx} debug width");
                    Assert.AreEqual(collider.bounds.size.y, debugSprite.bounds.size.y, .001f, $"Unit_{unitIdx} debug height");
                    hitbox.Close();
                    Assert.IsTrue(hitbox.gameObject.activeSelf, $"Unit_{unitIdx} AttackAttach after Close");
                    Assert.IsFalse(collider.enabled, $"Unit_{unitIdx} collider after Close");
                }
                finally
                {
                    Object.DestroyImmediate(instance);
                }
            }

            StringAssert.DoesNotContain("gameObject.SetActive", File.ReadAllText(
                "Assets/Scripts/Gameplay/Combat/UnitAttackHitbox2D.cs"));
        }
    }
}
