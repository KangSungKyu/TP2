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
        public void Pass10AttackEffects_MapByUintIdentityAndDriveExactTickBounds()
        {
            (uint unit, uint pattern, uint skill, uint effect)[] mappings =
            {
                (3001, 0, 7001, 8014), (3101, 6001, 7001, 8015),
                (3102, 6008, 7005, 8016), (3102, 6009, 7006, 8017),
                (3103, 6001, 7001, 8018), (3103, 6010, 7007, 8019),
                (3104, 6003, 7001, 8020), (3104, 6004, 7001, 8021),
                (3105, 6005, 7002, 8022), (3201, 6103, 7013, 8023)
            };
            foreach (var mapping in mappings)
            {
                Assert.IsTrue(SkillExecutor.TryResolvePass10AttackEffectIdx(
                    mapping.unit, mapping.pattern, mapping.skill, out uint effect));
                Assert.AreEqual(mapping.effect, effect);
            }
            Assert.IsFalse(SkillExecutor.TryResolvePass10AttackEffectIdx(3106, 6007, 7003, out _));
            (uint unit, uint pattern, uint skill, uint tick, uint effect)[] reworkV2 =
            {
                (3105, 6006, 7002, 0, 8024), (3201, 6100, 7012, 0, 8025),
                (3201, 6102, 7010, 0, 8026), (3001, 0, 7003, 0, 8027),
                (3001, 0, 7003, 1, 8028), (3201, 6101, 7011, 0, 8029),
                (3201, 6101, 7011, 1, 8030)
            };
            foreach (var mapping in reworkV2)
            {
                Assert.IsTrue(SkillExecutor.TryResolvePass10AttackEffectIdx(
                    mapping.unit, mapping.pattern, mapping.skill, mapping.tick, out uint effect));
                Assert.AreEqual(mapping.effect, effect);
            }
            Assert.IsFalse(SkillExecutor.TryResolvePass10AttackEffectIdx(3001, 0, 7003, 2, out _));
            Assert.IsFalse(SkillExecutor.TryResolvePass10AttackEffectIdx(3201, 6101, 7011, 2, out _));

            GameObject ownerObject = new GameObject("Pass10Owner");
            GameObject attachObject = new GameObject("Pass10Attach");
            try
            {
                attachObject.transform.SetParent(ownerObject.transform, false);
                UnitBase owner = ownerObject.AddComponent<UnitBase>();
                BoxCollider2D collider = attachObject.AddComponent<BoxCollider2D>();
                collider.isTrigger = true;
                collider.offset = new Vector2(.2f, .3f);
                collider.size = new Vector2(1f, .5f);
                UnitAttackHitbox2D hitbox = attachObject.AddComponent<UnitAttackHitbox2D>();
                typeof(UnitAttackHitbox2D).GetField("weaponAttackCollider", BindingFlags.Instance | BindingFlags.NonPublic)
                    ?.SetValue(hitbox, collider);
                typeof(UnitAttackHitbox2D).GetField("attachRoot", BindingFlags.Instance | BindingFlags.NonPublic)
                    ?.SetValue(hitbox, attachObject.transform);
                typeof(UnitBase).GetField("attackHitbox", BindingFlags.Instance | BindingFlags.NonPublic)
                    ?.SetValue(owner, hitbox);
                hitbox.Bind(owner);

                Vector2 originalOffset = collider.offset;
                Vector2 originalSize = collider.size;
                Assert.IsTrue(owner.TryOpenAttackHitbox(1, owner.ActionGeneration, 0,
                    AttackSubject.Weapon, BodyPartRole.None, new Vector2(.16f, -.11f),
                    new Vector2(.56f, .82f), out var rightSweep));
                Assert.AreEqual(.56f, rightSweep.HalfExtents.x * 2f, .001f);
                Assert.AreEqual(.82f, rightSweep.HalfExtents.y * 2f, .001f);
                Assert.AreEqual(.16f, rightSweep.Current.x, .001f);
                owner.CloseAttackHitbox();
                Assert.AreEqual(originalOffset, collider.offset);
                Assert.AreEqual(originalSize, collider.size);

                owner.SetFacingRight(false);
                Assert.IsTrue(owner.TryOpenAttackHitbox(2, owner.ActionGeneration, 0,
                    AttackSubject.Weapon, BodyPartRole.None, new Vector2(.16f, -.11f),
                    new Vector2(.56f, .82f), out var leftSweep));
                Assert.AreEqual(-.16f, leftSweep.Current.x, .001f);
            }
            finally
            {
                Object.DestroyImmediate(ownerObject);
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
        public void Test_CommonAttackHitbox_WindowFacingAndCancellation()
        {
            GameObject ownerObject = new GameObject("AttackHitboxOwner_QA");
            GameObject hitboxObject = new GameObject("WeaponHitbox_QA");
            hitboxObject.transform.SetParent(ownerObject.transform, false);
            GameObject sweepObject = new GameObject("MeleeSweepVisual_QA");
            sweepObject.transform.SetParent(hitboxObject.transform, false);
            try
            {
                UnitBase owner = ownerObject.AddComponent<UnitBase>();
                BoxCollider2D collider = hitboxObject.AddComponent<BoxCollider2D>();
                collider.isTrigger = true;
                collider.size = new Vector2(2f, 1f);
                SpriteRenderer debugSprite = hitboxObject.AddComponent<SpriteRenderer>();
                SpriteRenderer sweepSprite = sweepObject.AddComponent<SpriteRenderer>();
                sweepSprite.sprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(
                    UnityEditor.AssetDatabase.GUIDToAssetPath("577e4bcb39da808489091d00c71ddfe4"));
                UnitAttackHitbox2D hitbox = hitboxObject.AddComponent<UnitAttackHitbox2D>();
                typeof(UnitAttackHitbox2D).GetField("weaponAttackCollider", BindingFlags.Instance | BindingFlags.NonPublic)
                    ?.SetValue(hitbox, collider);
                typeof(UnitAttackHitbox2D).GetField("attachRoot", BindingFlags.Instance | BindingFlags.NonPublic)
                    ?.SetValue(hitbox, hitboxObject.transform);
                typeof(UnitAttackHitbox2D).GetField("debugHitboxSprite", BindingFlags.Instance | BindingFlags.NonPublic)
                    ?.SetValue(hitbox, debugSprite);
                typeof(UnitAttackHitbox2D).GetField("debugSweepSprite", BindingFlags.Instance | BindingFlags.NonPublic)
                    ?.SetValue(hitbox, sweepSprite);
                typeof(UnitBase).GetField("attackHitbox", BindingFlags.Instance | BindingFlags.NonPublic)
                    ?.SetValue(owner, hitbox);
                hitbox.Bind(owner);

                owner.SetFacingRight(false);
                Assert.Less(hitboxObject.transform.localScale.x, 0f);
                Bounds originalColliderBounds = collider.bounds;
                Assert.IsTrue(owner.TryOpenAttackHitbox(11, owner.ActionGeneration, 0, out var sweep));
                Assert.IsTrue(hitbox.IsWindowActive);
                Assert.IsTrue(hitbox.IsDebugVisualizationActive);
                Assert.IsTrue(debugSprite.enabled);
                Assert.AreEqual(hitbox.ActiveColor, debugSprite.color, "Active hit window must render active color.");
                Assert.AreEqual(new Vector2(1.5f, 0.75f), sweep.HalfExtents);
                Assert.AreEqual(-0.5f, sweep.Current.x, .001f);
                Assert.IsTrue(sweepSprite.enabled, "Active melee window must display the authoritative sweep bounds.");
                Assert.AreEqual(sweep.Current.x, sweepSprite.bounds.center.x, .001f);
                Assert.AreEqual(sweep.Current.y, sweepSprite.bounds.center.y, .001f);
                Assert.AreEqual(sweep.HalfExtents.x * 2f, sweepSprite.bounds.size.x, .001f);
                Assert.AreEqual(sweep.HalfExtents.y * 2f, sweepSprite.bounds.size.y, .001f);
                Assert.AreEqual(1f, sweep.Current.x + sweep.HalfExtents.x, .001f,
                    "Left-facing expansion must preserve the original rear edge.");
                Assert.AreEqual(originalColliderBounds.center, collider.bounds.center,
                    "Sweep expansion must not move the serialized collider anchor.");

                hitbox.SetTelegraphed(true);
                owner.CloseAttackHitbox();
                Assert.IsFalse(hitbox.IsWindowActive);
                Assert.IsTrue(hitbox.IsDebugVisualizationActive, "Telegraphed state must keep visualization active.");
                Assert.IsTrue(debugSprite.enabled);
                Assert.IsFalse(sweepSprite.enabled, "The authoritative sweep is visible only during an active window.");
                Assert.AreEqual(hitbox.InactiveColor, debugSprite.color, "Telegraphed/inactive window must render inactive color.");

                hitbox.SetTelegraphed(false);
                Assert.IsFalse(hitbox.IsDebugVisualizationActive);
                Assert.IsFalse(debugSprite.enabled);

                UnitAttackHitbox2D.DebugVisualizationEnabled = false;
                owner.CloseAttackHitbox();
                Assert.IsTrue(owner.TryOpenAttackHitbox(12, owner.ActionGeneration, 0, out _));
                Assert.IsFalse(hitbox.IsDebugVisualizationActive, "Global OFF must suppress the active debug state.");
                Assert.IsFalse(debugSprite.enabled, "Global OFF must keep the sprite renderer disabled when the window reopens.");
                Assert.IsFalse(sweepSprite.enabled, "Global OFF must suppress the sweep renderer without changing collision.");
                Assert.IsTrue(hitbox.IsWindowActive);

                uint generation = owner.ActionGeneration;
                owner.CancelAttackHitbox();
                Assert.IsFalse(hitbox.IsWindowActive, "Cancel must close the attack window.");
                Assert.IsFalse(hitbox.IsDebugVisualizationActive, "Cancel must clear the debug state.");
                Assert.IsFalse(debugSprite.enabled, "Cancel must disable the sprite renderer.");
                Assert.IsFalse(sweepSprite.enabled, "Cancel must disable the sweep renderer.");
                Assert.IsFalse(owner.IsActionGenerationCurrent(generation));

                UnitAttackHitbox2D.DebugVisualizationEnabled = true;
                owner.SetFacingRight(true);
                Assert.IsTrue(owner.TryOpenAttackHitbox(13, owner.ActionGeneration, 0, out var rightSweep));
                Assert.IsTrue(debugSprite.enabled);
                Assert.IsTrue(sweepSprite.enabled);
                Assert.AreEqual(.5f, rightSweep.Current.x, .001f);
                Assert.AreEqual(rightSweep.Current.x, sweepSprite.bounds.center.x, .001f);
                Assert.AreEqual(rightSweep.HalfExtents.x * 2f, sweepSprite.bounds.size.x, .001f);
                typeof(UnitAttackHitbox2D).GetMethod("OnDisable", BindingFlags.Instance | BindingFlags.NonPublic)
                    ?.Invoke(hitbox, null);
                Assert.IsFalse(debugSprite.enabled, "Disable must not leave a pooled sprite renderer enabled.");
                Assert.IsFalse(sweepSprite.enabled, "Disable must not leave a pooled sweep renderer enabled.");
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
        public void Test_HitboxLifecycle_YellowToRedToYellowToOff_Transition()
        {
            GameObject ownerObject = new GameObject("HitboxLifecycle_Owner_QA");
            GameObject hitboxObject = new GameObject("HitboxLifecycle_Hitbox_QA");
            hitboxObject.transform.SetParent(ownerObject.transform, false);
            try
            {
                UnitBase owner = ownerObject.AddComponent<UnitBase>();
                BoxCollider2D collider = hitboxObject.AddComponent<BoxCollider2D>();
                collider.isTrigger = true;
                SpriteRenderer debugSprite = hitboxObject.AddComponent<SpriteRenderer>();
                UnitAttackHitbox2D hitbox = hitboxObject.AddComponent<UnitAttackHitbox2D>();
                typeof(UnitAttackHitbox2D).GetField("weaponAttackCollider", BindingFlags.Instance | BindingFlags.NonPublic)?.SetValue(hitbox, collider);
                typeof(UnitAttackHitbox2D).GetField("attachRoot", BindingFlags.Instance | BindingFlags.NonPublic)?.SetValue(hitbox, hitboxObject.transform);
                typeof(UnitAttackHitbox2D).GetField("debugHitboxSprite", BindingFlags.Instance | BindingFlags.NonPublic)?.SetValue(hitbox, debugSprite);
                typeof(UnitBase).GetField("attackHitbox", BindingFlags.Instance | BindingFlags.NonPublic)?.SetValue(owner, hitbox);
                hitbox.Bind(owner);

                // 1. 선딜 (Pre-delay): 🟡 Yellow, Collider disabled
                owner.SetTelegraphedAttackHitbox(true);
                Assert.IsTrue(hitbox.IsDebugVisualizationActive);
                Assert.IsFalse(hitbox.IsWindowActive);
                Assert.IsFalse(collider.enabled, "Telegraphed state must keep attack collider disabled to prevent pre-damage.");
                Assert.AreEqual(hitbox.InactiveColor, debugSprite.color, "Pre-delay must display yellow inactive color.");

                // 2. 타격 순간 (Active Window): 🔴 Red, Collider enabled
                Assert.IsTrue(owner.TryOpenAttackHitbox(101, owner.ActionGeneration, 0, out _));
                Assert.IsTrue(hitbox.IsWindowActive);
                Assert.IsTrue(collider.enabled, "Active hit window must enable attack collider.");
                Assert.AreEqual(hitbox.ActiveColor, debugSprite.color, "Active hit window must display red active color.");

                // 3. 후딜 (Post-delay): 🟡 Yellow, Collider disabled
                owner.CloseAttackHitbox();
                owner.SetTelegraphedAttackHitbox(true);
                Assert.IsTrue(hitbox.IsDebugVisualizationActive);
                Assert.IsFalse(hitbox.IsWindowActive);
                Assert.IsFalse(collider.enabled, "Post-delay must keep attack collider disabled.");
                Assert.AreEqual(hitbox.InactiveColor, debugSprite.color, "Post-delay must display yellow inactive color.");

                // 4. 모션 완전 종료 (OFF): Visual disabled, Collider disabled
                owner.CloseAttackHitbox();
                owner.SetTelegraphedAttackHitbox(false);
                Assert.IsFalse(hitbox.IsDebugVisualizationActive);
                Assert.IsFalse(debugSprite.enabled, "Complete motion end must turn off debug visualization.");
                Assert.IsFalse(collider.enabled);
            }
            finally
            {
                Object.DestroyImmediate(ownerObject);
            }
        }

        [Test]
        public void Test_TelegraphTimer_SyncsWithWindowStart_AndPreRendersYellowHitbox()
        {
            float configuredPreDelay = 0.5f;
            float firstHitTiming = 0.20f;
            float hitWindowPre = 0.15f;
            float windowStart = Mathf.Max(0f, firstHitTiming - hitWindowPre);

            float effectivePreDelay = Monster.CalculateEffectivePreDelay(configuredPreDelay, windowStart);
            Assert.AreEqual(1.45f, effectivePreDelay, 0.001f, "EffectivePreDelay must compensate for windowStart.");
            Assert.AreEqual(0.05f, windowStart, 0.001f, "windowStart must be HitTiming - HitWindowPre.");
        }

        [Test]
        public void Test_DoubledAttackTiming_RecoveryAndMultiHitGapContracts()
        {
            Assert.AreEqual(1.6f, SkillExecutor.CalculateAttackRecoverySeconds(2f, .4f, .5f), .001f,
                "A doubled clip must not be truncated by the old combo/recovery duration.");
            Assert.AreEqual(.5f, SkillExecutor.CalculateAttackRecoverySeconds(.4f, .4f, .5f), .001f,
                "Existing longer recovery remains authoritative.");

            float firstStart = .20f - .06f;
            float firstEnd = .20f + .06f;
            float secondStart = .50f - .06f;
            float secondEnd = .50f + .06f;
            Assert.Greater(secondStart, firstEnd, "Doubled 7003 timings must retain a collider-OFF gap.");
            Assert.AreEqual(.12f, firstEnd - firstStart, .001f);
            Assert.AreEqual(.12f, secondEnd - secondStart, .001f);
            foreach (float fixedStep in new[] { 1f / 15f, 1f / 60f })
            {
                float elapsed = 0f;
                while (elapsed + Mathf.Epsilon < firstStart) elapsed += fixedStep;
                Assert.That(elapsed, Is.InRange(firstStart, firstStart + fixedStep));
                while (elapsed + Mathf.Epsilon < firstEnd) elapsed += fixedStep;
                Assert.That(elapsed, Is.InRange(firstEnd, firstEnd + fixedStep));
                while (elapsed + Mathf.Epsilon < secondStart) elapsed += fixedStep;
                Assert.That(elapsed, Is.InRange(secondStart, secondStart + fixedStep));
            }

            string player = File.ReadAllText("Assets/Scripts/Gameplay/Player.cs");
            string monster = File.ReadAllText("Assets/Scripts/Gameplay/Monster.cs");
            StringAssert.Contains("windowElapsed < recoveryWindow", player);
            StringAssert.Contains("GetAttackRecoverySeconds(animator, animationStartedAt, pattern.PostDelay)", monster);
            StringAssert.Contains("if (windowStart > 0f)", monster);
            StringAssert.Contains("SetTelegraphedAttackHitbox(false);", monster);
        }

        [Test]
        public void ShadowStalker_AttackAttachCurve_IsContinuousAndPreservesActivePose()
        {
            var clip = UnityEditor.AssetDatabase.LoadAssetAtPath<AnimationClip>(
                "Assets/Anims/Monster/ShadowStalker_Attack.anim");
            Assert.NotNull(clip);
            Assert.AreEqual(2f, clip.length, 1f / clip.frameRate);
            Assert.AreEqual(8f, clip.frameRate);

            var bindings = UnityEditor.AnimationUtility.GetCurveBindings(clip);
            AnimationCurve x = UnityEditor.AnimationUtility.GetEditorCurve(clip,
                System.Array.Find(bindings, b => b.path.Contains("AttackAttach") && b.propertyName == "m_LocalPosition.x"));
            AnimationCurve y = UnityEditor.AnimationUtility.GetEditorCurve(clip,
                System.Array.Find(bindings, b => b.path.Contains("AttackAttach") && b.propertyName == "m_LocalPosition.y"));
            AnimationCurve z = UnityEditor.AnimationUtility.GetEditorCurve(clip,
                System.Array.Find(bindings, b => b.path.Contains("AttackAttach") && b.propertyName == "localEulerAnglesRaw.z"));
            Assert.NotNull(x); Assert.NotNull(y); Assert.NotNull(z);
            Assert.AreEqual(6, x.length); Assert.AreEqual(6, y.length); Assert.AreEqual(6, z.length);
            Assert.AreEqual(.7f, x.Evaluate(0f), .001f);
            Assert.AreEqual(1f, y.Evaluate(0f), .001f);
            Assert.AreEqual(0f, z.Evaluate(0f), .001f);
            Assert.AreEqual(.49056f, x.Evaluate(.24f), .001f);
            Assert.AreEqual(1.3614f, y.Evaluate(.24f), .001f);
            Assert.AreEqual(43.8f, z.Evaluate(.24f), .001f);
            Assert.AreEqual(.7f, x.Evaluate(2f), .001f);
            Assert.AreEqual(1f, y.Evaluate(2f), .001f);
            Assert.AreEqual(0f, z.Evaluate(2f), .001f);

            Vector2 previous = new Vector2(x.Evaluate(0f), y.Evaluate(0f));
            for (int frame = 1; frame <= 16; frame++)
            {
                Vector2 current = new Vector2(x.Evaluate(frame / 8f), y.Evaluate(frame / 8f));
                Assert.IsTrue(float.IsFinite(current.x) && float.IsFinite(current.y));
                Assert.Less(Vector2.Distance(previous, current), .604f);
                Assert.That(current.x, Is.InRange(.49056f, .9f));
                Assert.That(current.y, Is.InRange(.78f, 1.3614f));
                previous = current;
            }

            var spriteBinding = UnityEditor.AnimationUtility.GetObjectReferenceCurveBindings(clip);
            Assert.AreEqual(1, spriteBinding.Length);
            var spriteKeys = UnityEditor.AnimationUtility.GetObjectReferenceCurve(clip, spriteBinding[0]);
            Assert.AreEqual(8, spriteKeys.Length);
            Assert.IsFalse(System.Array.Exists(spriteKeys, key => key.value == null));
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
                typeof(UnitAttackHitbox2D).GetField("weaponAttackCollider", BindingFlags.Instance | BindingFlags.NonPublic)?.SetValue(hitbox, attackCollider);
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
                Assert.AreEqual(new Vector3(1f, 1f, 0f), guardRoot.localPosition);
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
                Assert.AreEqual(-1f, guardRoot.localPosition.x, .001f);
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
        public void Test_MonsterPatternStartDistanceBand_SelectionFallbackAndBoundaries()
        {
            var patterns = new MonsterPatternDataTable();
            var skills = new SkillDataTable();
            patterns.LoadData(File.ReadAllText("Assets/Datas/MonsterPatternData.csv"));
            skills.LoadData(File.ReadAllText("Assets/Datas/SkillData.csv"));

            Assert.IsTrue(patterns.TryGetPatternData(6005, out MonsterPatternData far));
            Assert.IsTrue(patterns.TryGetPatternData(6006, out MonsterPatternData near));
            Assert.IsTrue(skills.TryGetSkillData(7002, out SkillData projectileSkill));
            Assert.AreEqual(8f, far.MinStartDistance);
            Assert.AreEqual(10f, far.MaxStartDistance);
            Assert.IsFalse(Monster.IsPatternStartDistanceValid(far, projectileSkill, 7.999f));
            Assert.IsTrue(Monster.IsPatternStartDistanceValid(far, projectileSkill, 8f));
            Assert.IsTrue(Monster.IsPatternStartDistanceValid(far, projectileSkill, 10f));
            Assert.IsFalse(Monster.IsPatternStartDistanceValid(far, projectileSkill, 10.001f));
            Assert.IsTrue(Monster.IsPatternStartDistanceValid(near, projectileSkill, 0f));
            Assert.IsTrue(Monster.IsPatternStartDistanceValid(near, projectileSkill, 8f));
            Assert.IsFalse(Monster.IsPatternStartDistanceValid(near, projectileSkill, 8.001f));

            Assert.IsTrue(patterns.TryGetPatternData(6001, out MonsterPatternData melee));
            Assert.IsTrue(skills.TryGetSkillData(7001, out SkillData meleeSkill));
            Assert.IsTrue(Monster.TryGetPatternStartDistanceBand(melee, meleeSkill, out float min, out float max));
            Assert.AreEqual(0f, min);
            Assert.AreEqual(meleeSkill.Range, max);

            foreach (float step in new[] { 1f / 15f, 1f / 60f })
            {
                float boundary = 8f + 0f * step;
                Assert.IsTrue(Monster.IsPatternStartDistanceValid(far, projectileSkill, boundary));
                Assert.IsTrue(Monster.IsPatternStartDistanceValid(near, projectileSkill, boundary));
            }

            string monster = File.ReadAllText("Assets/Scripts/Gameplay/Monster.cs");
            StringAssert.Contains("CanReservePattern(pattern, skillTable)", monster);
            StringAssert.Contains("GetAttackSurfaceGap()", monster);
            StringAssert.Contains("IsInsideStartBand", monster);
            StringAssert.Contains("SetAttackMotionVelocityX(0f);", monster);
            StringAssert.DoesNotContain("pattern.TriggerValue > 0f ? pattern.TriggerValue", monster);
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
        public void PhaseA1x1_PlaytestUsesEntryAndSingleMonster3103Override()
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
            StringAssert.Contains("DevelopmentMonsterUnitIdx = 3103u", main);
            Assert.AreEqual(1, System.Array.FindAll(room.GetComponentsInChildren<SpawnPointMarker>(true),
                marker => marker.MonsterId == 3102u).Length);
            Assert.AreEqual(0, System.Array.FindAll(room.GetComponentsInChildren<SpawnPointMarker>(true),
                marker => marker.MonsterId == 3103u).Length,
                "The Development prefab remains unchanged; the runtime override owns the test unit.");
            StringAssert.Contains("ConfigureDevelopmentPlaytestMarkers", builder);
            StringAssert.Contains("markers[1].MonsterId = DevelopmentMonsterUnitIdx", builder);
            Assert.Less(spawner.IndexOf("if (zones.Count == 1 && zones[0].MonsterId != 0u)"),
                spawner.IndexOf("uint[] encounter = GetCurrentEncounter(zones)"));
            StringAssert.Contains("playtestMarker.EnableSpawn = false", spawner);
            Assert.NotNull(UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Unit_3103.prefab")
                .GetComponent<SkillExecutor>(), "Unit_3103 must keep its prefab-bound SkillExecutor.");
        }

        [Test]
        public void AttackMotionProfile_StrictFallbackPriorityAndFixedStepContracts()
        {
            Assert.AreEqual(DataTableType.AttackMotionProfile, Util.GetDataTableType(10001));
            var persisted = new AttackMotionProfileDataTable();
            persisted.LoadData(File.ReadAllText("Assets/Datas/AttackMotionProfileData.csv"));
            Assert.AreEqual(3, persisted.GetDataCount());
            Assert.IsTrue(persisted.TryGetValid(10001, out var persistedStationary));
            Assert.AreEqual(AttackMotionType.Stationary, persistedStationary.MotionType);
            Assert.IsTrue(persisted.TryGetValid(10002, out var persistedStep));
            Assert.AreEqual(AttackMotionType.Step, persistedStep.MotionType);
            Assert.IsTrue(persistedStep.Enabled);
            Assert.IsFalse(persisted.TryGetValid(10003, out _));

            var skills = new SkillDataTable();
            skills.LoadData(File.ReadAllText("Assets/Datas/SkillData.csv"));
            foreach (uint idx in new uint[] { 7001, 7002, 7003, 7004, 7010, 7011, 7012, 7013 })
            {
                Assert.IsTrue(skills.TryGetSkillData(idx, out var skillData));
                Assert.AreEqual(10001u, skillData.AttackMotionProfileIdx);
            }

            var patterns = new MonsterPatternDataTable();
            patterns.LoadData(File.ReadAllText("Assets/Datas/MonsterPatternData.csv"));
            foreach (uint idx in new uint[] { 6001, 6002, 6003, 6004, 6005, 6006, 6007, 6100, 6101, 6102, 6103 })
            {
                Assert.IsTrue(patterns.TryGetPatternData(idx, out var pattern));
                Assert.AreEqual(0u, pattern.AttackMotionProfileIdx);
            }

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
        public void Unit3102Prototype_ThrustAndBarrageContracts()
        {
            Assert.AreEqual(DataTableType.MonsterPattern, Util.GetDataTableType(6008));
            Assert.AreEqual(DataTableType.MonsterPattern, Util.GetDataTableType(6009));
            Assert.AreEqual(DataTableType.Skill, Util.GetDataTableType(7005));
            Assert.AreEqual(DataTableType.Skill, Util.GetDataTableType(7006));
            var profiles = new AttackMotionProfileDataTable();
            profiles.LoadData(File.ReadAllText("Assets/Datas/AttackMotionProfileData.csv"));
            Assert.IsTrue(profiles.TryGetValid(10002, out var thrust));
            Assert.AreEqual(AttackMotionType.Step, thrust.MotionType);
            Assert.AreEqual(AttackTargetPolicy.SnapshotAtStartup, thrust.TargetPolicy);

            GameObject attackerObject = new GameObject("BarrageAttacker_QA");
            GameObject hitboxObject = new GameObject("BarrageHitbox_QA");
            GameObject targetObject = new GameObject("BarrageTarget_QA");
            hitboxObject.transform.SetParent(attackerObject.transform, false);
            try
            {
                UnitBase attacker = attackerObject.AddComponent<UnitBase>();
                BoxCollider2D attackCollider = hitboxObject.AddComponent<BoxCollider2D>();
                attackCollider.isTrigger = true;
                UnitAttackHitbox2D hitbox = hitboxObject.AddComponent<UnitAttackHitbox2D>();
                typeof(UnitAttackHitbox2D).GetField("weaponAttackCollider", BindingFlags.Instance | BindingFlags.NonPublic)
                    ?.SetValue(hitbox, attackCollider);
                typeof(UnitAttackHitbox2D).GetField("attachRoot", BindingFlags.Instance | BindingFlags.NonPublic)
                    ?.SetValue(hitbox, hitboxObject.transform);
                typeof(UnitBase).GetField("attackHitbox", BindingFlags.Instance | BindingFlags.NonPublic)
                    ?.SetValue(attacker, hitbox);
                hitbox.Bind(attacker);

                CombatStats target = targetObject.AddComponent<CombatStats>();
                target.MaxHp = 100;
                target.InitStats();
                for (int attack = 0; attack < 2; attack++)
                {
                    for (int tick = 0; tick < 2; tick++)
                    {
                        Assert.IsTrue(attacker.TryOpenAttackHitbox(7006, attacker.ActionGeneration, (uint)tick, out var sweep));
                        target.TakeDamage(10, attackSweep: sweep);
                        target.TakeDamage(10, attackSweep: sweep);
                        attacker.CloseAttackHitbox();
                        Assert.IsFalse(attackCollider.enabled, "Barrage collider must be off between hit windows.");
                    }
                }
                Assert.AreEqual(60, target.CurrentHp,
                    "Each Barrage window hits once, and a second attack generation can hit again.");

                string monster = File.ReadAllText("Assets/Scripts/Gameplay/Monster.cs");
                int animationStart = monster.IndexOf("TryPlaySkillAnimation", System.StringComparison.Ordinal);
                int executionStart = monster.IndexOf("ExecuteSkillHitsAsync", animationStart, System.StringComparison.Ordinal);
                Assert.GreaterOrEqual(animationStart, 0);
                Assert.Greater(executionStart, animationStart,
                    "Animation and SkillExecutor must start in the same attack startup path.");
                string executor = File.ReadAllText("Assets/Scripts/Gameplay/SkillExecutor.cs");
                int motionStart = executor.IndexOf("SetAttackMotionStopPosition", System.StringComparison.Ordinal);
                int fixedYield = executor.IndexOf("PlayerLoopTiming.FixedUpdate", motionStart, System.StringComparison.Ordinal);
                Assert.GreaterOrEqual(motionStart, 0);
                Assert.Greater(fixedYield, motionStart, "Thrust motion must be armed before the first startup fixed tick.");
            }
            finally
            {
                Object.DestroyImmediate(attackerObject);
                Object.DestroyImmediate(targetObject);
            }
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
            StringAssert.Contains("CalculateReservationChaseSpeed(correction, remaining", monsterSource);
            StringAssert.Contains("PlayerLoopTiming.FixedUpdate", monsterSource);
            StringAssert.Contains("CurrentPatternState = PatternState.Active", monsterSource);
            StringAssert.Contains("ReleaseAttackToken();\n        CurrentPatternState = PatternState.Recovery", monsterSource.Replace("\r", ""));
            StringAssert.Contains("mData.PatternIdxList.Length > 16", monsterSource);
            Assert.AreEqual(1.25f, Monster.CalculateReservationChaseSpeed(1f, 1f, 10f, .02f), .0001f);
            Assert.AreEqual(4.5f, Monster.CalculateReservationChaseSpeed(10f, 1f, 4.5f, .02f), .0001f);

            foreach (float fixedStep in new[] { 1f / 15f, 1f / 60f })
            {
                const float timeout = 1f;
                const float moveSpeed = 4.5f;
                float position = 0f;
                float elapsed = 0f;
                while (elapsed + Mathf.Epsilon < timeout)
                {
                    float remaining = timeout - elapsed;
                    float speed = Monster.CalculateReservationChaseSpeed(0.405f - position,
                        remaining, moveSpeed, fixedStep);
                    Assert.LessOrEqual(speed, moveSpeed);
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
                StringAssert.Contains("motion.MotionType == AttackMotionType.Step", executor);
                StringAssert.Contains("owner.HasGroundSupportForAttackStep", executor);
            }
            finally
            {
                Object.DestroyImmediate(unitObject);
                Object.DestroyImmediate(groundObject);
            }
        }

        [Test]
        public void AttackMotion_ActiveTimeReachAlignment_LeftRightTrackingAndStationary()
        {
            GameObject ownerObject = new GameObject("ReachAlignmentOwner_QA");
            GameObject attachObject = new GameObject("ReachAlignmentAttach_QA");
            GameObject hitboxObject = new GameObject("ReachAlignmentHitbox_QA");
            attachObject.transform.SetParent(ownerObject.transform, false);
            hitboxObject.transform.SetParent(attachObject.transform, false);
            hitboxObject.transform.localPosition = Vector3.right;
            try
            {
                UnitBase owner = ownerObject.AddComponent<UnitBase>();
                BoxCollider2D collider = hitboxObject.AddComponent<BoxCollider2D>();
                collider.isTrigger = true;
                collider.size = new Vector2(2f, 1f);
                UnitAttackHitbox2D hitbox = hitboxObject.AddComponent<UnitAttackHitbox2D>();
                typeof(UnitAttackHitbox2D).GetField("weaponAttackCollider", BindingFlags.Instance | BindingFlags.NonPublic)?.SetValue(hitbox, collider);
                typeof(UnitAttackHitbox2D).GetField("attachRoot", BindingFlags.Instance | BindingFlags.NonPublic)?.SetValue(hitbox, attachObject.transform);
                typeof(UnitBase).GetField("attackHitbox", BindingFlags.Instance | BindingFlags.NonPublic)?.SetValue(owner, hitbox);
                hitbox.Bind(owner);

                owner.SetFacingRight(true);
                Assert.IsTrue(owner.TryGetAttackForwardReach(true, out float rightReach));
                owner.SetFacingRight(false);
                Assert.IsTrue(owner.TryGetAttackForwardReach(false, out float leftReach));
                Assert.AreEqual(3f, rightReach, .001f);
                Assert.AreEqual(3f, leftReach, .001f);
                owner.SetFacingRight(true);
                Assert.IsTrue(owner.TryGetAttackSweepCenterOffset(true, out float rightCenterOffset));
                owner.SetFacingRight(false);
                Assert.IsTrue(owner.TryGetAttackSweepCenterOffset(false, out float leftCenterOffset));
                Assert.AreEqual(1.5f, rightCenterOffset, .001f);
                Assert.AreEqual(-1.5f, leftCenterOffset, .001f);

                const float skin = .01f;
                float snapshot = SkillExecutor.CalculateAttackAlignmentTargetX(0f, 0f, 10f, 2f, .5f, 1f, rightCenterOffset, skin, 20f, false);
                float tracking = SkillExecutor.CalculateAttackAlignmentTargetX(0f, 0f, 10f, 2f, .5f, 1f, rightCenterOffset, skin, 20f, true);
                float left = SkillExecutor.CalculateAttackAlignmentTargetX(0f, 0f, -10f, -2f, .5f, 1f, leftCenterOffset, skin, 20f, false);
                Assert.AreEqual(7.49f, snapshot, .001f);
                Assert.AreEqual(8.49f, tracking, .001f);
                Assert.AreEqual(-7.49f, left, .001f);
                Assert.AreEqual(skin, 9f - (snapshot + rightCenterOffset), .001f,
                    "Step root must align the sweep center to the target body near surface without crossing it.");

                var stationary = new AttackMotionProfileData { MotionType = AttackMotionType.Stationary, Enabled = true };
                Assert.AreEqual(0f, SkillExecutor.CalculateAttackMotionVelocity(stationary, 0f, tracking, .5f, 8f, .02f));
                foreach (float fixedStep in new[] { 1f / 15f, 1f / 60f })
                {
                    var step = new AttackMotionProfileData { MotionType = AttackMotionType.Step, MaxSpeed = 100f, MaxDistance = 20f, Enabled = true };
                    float x = 0f;
                    float remaining = .5f;
                    while (remaining > 0f)
                    {
                        float dt = Mathf.Min(fixedStep, remaining);
                        x += SkillExecutor.CalculateAttackMotionVelocity(step, x, tracking, remaining, 0f, dt) * dt;
                        remaining -= dt;
                    }
                    Assert.LessOrEqual(Mathf.Abs(tracking - x), skin + step.MaxSpeed * fixedStep);
                }

                var doubledStep = new AttackMotionProfileData
                    { MotionType = AttackMotionType.Step, MaxSpeed = 9f, MaxDistance = .81f, Enabled = true };
                var skillTable = new SkillDataTable();
                skillTable.LoadData(File.ReadAllText("Assets/Datas/SkillData.csv"));
                Assert.IsTrue(skillTable.TryGetSkillData(7005u, out SkillData thrust));
                Assert.IsTrue(skillTable.TryGetSkillData(7006u, out SkillData barrage));
                float thrustWindowStart = Mathf.Max(0f, thrust.HitTimings[0] - thrust.HitWindowPre);
                float barrageWindowStart = Mathf.Max(0f, barrage.HitTimings[0] - barrage.HitWindowPre);
                Assert.LessOrEqual(Mathf.Min(doubledStep.MaxDistance, doubledStep.MaxSpeed * barrageWindowStart), .63f,
                    "6009 startup timing cannot consume more than 0.63m after the 2x profile migration.");
                Assert.LessOrEqual(Mathf.Min(doubledStep.MaxDistance, doubledStep.MaxSpeed * thrustWindowStart), .81f,
                    "6008 total attack motion remains capped by the profile max distance.");
            }
            finally
            {
                Object.DestroyImmediate(ownerObject);
            }
        }

        [Test]
        public void AttackSwingEffect_IsAbsentFromAttackRuntimeWhileResponseEffectsRemain()
        {
            string player = File.ReadAllText("Assets/Scripts/Gameplay/Player.cs");
            string executor = File.ReadAllText("Assets/Scripts/Gameplay/SkillExecutor.cs");
            StringAssert.DoesNotContain("SpawnSkillEffectFromDataAsync", player);
            StringAssert.DoesNotContain("SpawnSkillEffectFromDataAsync", executor);
            StringAssert.Contains("SetFacingRight(facingDir.x >= 0f);", player);
            StringAssert.Contains("SpawnResponseEffect(8010", File.ReadAllText("Assets/Scripts/Gameplay/CombatStats.cs"));
            StringAssert.Contains("SpawnResponseEffect(8011", File.ReadAllText("Assets/Scripts/Gameplay/CombatStats.cs"));
            StringAssert.Contains("SpawnResponseEffect(8012", File.ReadAllText("Assets/Scripts/Gameplay/CombatStats.cs"));
            StringAssert.Contains("SpawnResponseEffect(8013", File.ReadAllText("Assets/Scripts/Gameplay/CombatStats.cs"));
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
                        .GetField("weaponAttackCollider", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(hitbox);
                    SpriteRenderer debugSprite = (SpriteRenderer)typeof(UnitAttackHitbox2D)
                        .GetField("debugHitboxSprite", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(hitbox);
                    SpriteRenderer sweepSprite = (SpriteRenderer)typeof(UnitAttackHitbox2D)
                        .GetField("debugSweepSprite", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(hitbox);

                    hitbox.Bind(unit);
                    Assert.IsTrue(hitbox.gameObject.activeSelf, $"Unit_{unitIdx} AttackAttach after Bind");
                    Assert.IsFalse(collider.enabled, $"Unit_{unitIdx} collider after Bind");
                    Assert.IsTrue(hitbox.TryOpen(1, unit.ActionGeneration, 0, out _));
                    Assert.IsTrue(hitbox.gameObject.activeSelf, $"Unit_{unitIdx} AttackAttach during window");
                    Assert.IsTrue(collider.enabled, $"Unit_{unitIdx} collider during window");
                    Assert.AreEqual(collider.bounds.size.x, debugSprite.bounds.size.x, .001f, $"Unit_{unitIdx} debug width");
                    Assert.AreEqual(collider.bounds.size.y, debugSprite.bounds.size.y, .001f, $"Unit_{unitIdx} debug height");
                    if (unitIdx == 3102u)
                    {
                        Assert.NotNull(sweepSprite, "Unit_3102 must serialize the separate effective-sweep renderer.");
                        Assert.IsNull(sweepSprite.GetComponent<Collider2D>());
                        Assert.IsNull(sweepSprite.GetComponent<Rigidbody2D>());
                        Assert.AreEqual(collider.bounds.size.x * 1.5f, sweepSprite.bounds.size.x, .001f);
                        Assert.AreEqual(collider.bounds.size.y * 1.5f, sweepSprite.bounds.size.y, .001f);
                    }
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

        [Test]
        public void AttackSubject_SelectsExactlyOneSerializedColliderAndPreservesWeaponVisual()
        {
            GameObject ownerObject = new GameObject("AttackSubjectOwner");
            GameObject attachObject = new GameObject("AttackAttach");
            GameObject weaponObject = new GameObject("WeaponCollider");
            GameObject torsoObject = new GameObject("TorsoCollider");
            GameObject visualObject = new GameObject("WeaponVisual");
            try
            {
                attachObject.transform.SetParent(ownerObject.transform, false);
                weaponObject.transform.SetParent(attachObject.transform, false);
                torsoObject.transform.SetParent(attachObject.transform, false);
                visualObject.transform.SetParent(attachObject.transform, false);
                BoxCollider2D weapon = weaponObject.AddComponent<BoxCollider2D>();
                BoxCollider2D torso = torsoObject.AddComponent<BoxCollider2D>();
                weapon.isTrigger = true;
                torso.isTrigger = true;
                weapon.size = new Vector2(1f, .5f);
                torso.size = new Vector2(2f, 1f);
                UnitBase owner = ownerObject.AddComponent<UnitBase>();
                UnitAttackHitbox2D hitbox = attachObject.AddComponent<UnitAttackHitbox2D>();
                typeof(UnitAttackHitbox2D).GetField("weaponAttackCollider", BindingFlags.Instance | BindingFlags.NonPublic)?.SetValue(hitbox, weapon);
                typeof(UnitAttackHitbox2D).GetField("torsoAttackCollider", BindingFlags.Instance | BindingFlags.NonPublic)?.SetValue(hitbox, torso);
                typeof(UnitAttackHitbox2D).GetField("attachRoot", BindingFlags.Instance | BindingFlags.NonPublic)?.SetValue(hitbox, attachObject.transform);
                typeof(UnitAttackHitbox2D).GetField("weaponVisual", BindingFlags.Instance | BindingFlags.NonPublic)?.SetValue(hitbox, visualObject.transform);
                Vector3 visualPosition = new Vector3(.2f, .3f, 0f);
                Quaternion visualRotation = Quaternion.Euler(0f, 0f, 17f);
                Vector3 visualScale = new Vector3(.8f, .9f, 1f);
                visualObject.transform.localPosition = visualPosition;
                visualObject.transform.localRotation = visualRotation;
                visualObject.transform.localScale = visualScale;

                hitbox.Bind(owner);
                Assert.IsTrue(hitbox.TryOpen(1, owner.ActionGeneration, 0,
                    AttackSubject.Weapon, BodyPartRole.None, out CombatStats.AttackSweep2D weaponSweep));
                Assert.IsTrue(weapon.enabled);
                Assert.IsFalse(torso.enabled);
                Assert.AreEqual(weapon.bounds.size.x * 1.5f, weaponSweep.HalfExtents.x * 2f, .001f);
                Vector3 animatedPosition = new Vector3(.7f, 1.1f, 0f);
                Quaternion animatedRotation = Quaternion.Euler(0f, 0f, 42f);
                weaponObject.transform.localPosition = animatedPosition;
                weaponObject.transform.localRotation = animatedRotation;
                typeof(UnitAttackHitbox2D).GetMethod("LateUpdate", BindingFlags.Instance | BindingFlags.NonPublic)
                    ?.Invoke(hitbox, null);
                Assert.AreEqual(animatedPosition, visualObject.transform.localPosition);
                Assert.Less(Quaternion.Angle(animatedRotation, visualObject.transform.localRotation), .001f);
                Assert.AreEqual(visualScale, visualObject.transform.localScale);
                hitbox.Close();
                Assert.IsFalse(weapon.enabled);
                Assert.IsFalse(torso.enabled);
                Assert.AreEqual(visualPosition, visualObject.transform.localPosition);
                Assert.Less(Quaternion.Angle(visualRotation, visualObject.transform.localRotation), .001f);
                Assert.AreEqual(visualScale, visualObject.transform.localScale);

                hitbox.SetFacingRight(false);
                Assert.IsTrue(hitbox.TryOpen(2, owner.ActionGeneration, 0,
                    AttackSubject.BodyPart, BodyPartRole.Torso, out CombatStats.AttackSweep2D torsoSweep));
                Assert.IsFalse(weapon.enabled);
                Assert.IsTrue(torso.enabled);
                Assert.AreEqual(torso.bounds.size.x * 1.5f, torsoSweep.HalfExtents.x * 2f, .001f);
                Assert.Less(torsoSweep.Current.x, owner.transform.position.x);
                torsoObject.transform.localPosition = new Vector3(-.5f, 1.4f, 0f);
                typeof(UnitAttackHitbox2D).GetMethod("LateUpdate", BindingFlags.Instance | BindingFlags.NonPublic)
                    ?.Invoke(hitbox, null);
                Assert.AreEqual(visualPosition, visualObject.transform.localPosition);
                Assert.Less(Quaternion.Angle(visualRotation, visualObject.transform.localRotation), .001f);
                hitbox.Close();

                hitbox.SetTelegraphed(true, AttackSubject.Weapon, BodyPartRole.None, false);
                weaponObject.transform.localPosition = new Vector3(1.2f, 1.5f, 0f);
                typeof(UnitAttackHitbox2D).GetMethod("LateUpdate", BindingFlags.Instance | BindingFlags.NonPublic)
                    ?.Invoke(hitbox, null);
                Assert.AreEqual(visualPosition, visualObject.transform.localPosition);
                Assert.Less(Quaternion.Angle(visualRotation, visualObject.transform.localRotation), .001f);
                Assert.AreEqual(visualScale, visualObject.transform.localScale);
                hitbox.SetTelegraphed(false);
            }
            finally
            {
                Object.DestroyImmediate(ownerObject);
            }
        }

        [Test]
        public void Unit3103TorsoRam_ActiveDataPrefabAndCombatContract()
        {
            var monsters = new MonsterDataTable();
            var patterns = new MonsterPatternDataTable();
            var skills = new SkillDataTable();
            monsters.LoadData(File.ReadAllText("Assets/Datas/MonsterBaseData.csv"));
            patterns.LoadData(File.ReadAllText("Assets/Datas/MonsterPatternData.csv"));
            skills.LoadData(File.ReadAllText("Assets/Datas/SkillData.csv"));
            Assert.IsTrue(monsters.TryGetMonsterData(5103u, out MonsterBaseData monsterData));
            CollectionAssert.Contains(monsterData.PatternIdxList, 6010u);
            Assert.IsTrue(patterns.TryGetPatternData(6010u, out MonsterPatternData pattern));
            Assert.IsTrue(skills.TryGetSkillData(7007u, out SkillData skill));
            Assert.AreEqual(7007u, pattern.SkillIdx);
            Assert.AreEqual(14, skill.AnimState);
            Assert.AreEqual(10002u, SkillExecutor.ResolveAttackMotionProfileIdx(skill, pattern.AttackMotionProfileIdx));
            Assert.AreEqual(AttackSubject.BodyPart, skill.AttackSubject);
            Assert.AreEqual(BodyPartRole.Torso, skill.BodyPartRole);
            Assert.AreEqual(0u, pattern.ProjectileResourceIdx);

            uint[] visualUnits = { 3001u, 3101u, 3102u, 3103u, 3104u, 3105u, 3201u };
            foreach (uint unitIdx in visualUnits)
            {
                GameObject prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/Prefabs/Unit_{unitIdx}.prefab");
                Assert.NotNull(prefab, $"Unit_{unitIdx} prefab");
                UnitAttackHitbox2D prefabHitbox = prefab.GetComponentInChildren<UnitAttackHitbox2D>(true);
                Assert.NotNull(prefabHitbox, $"Unit_{unitIdx} UnitAttackHitbox2D");
                Assert.NotNull(typeof(UnitAttackHitbox2D).GetField("weaponVisual", BindingFlags.Instance | BindingFlags.NonPublic)
                    ?.GetValue(prefabHitbox), $"Unit_{unitIdx} WeaponVisual serialized reference");
            }

            GameObject instance = Object.Instantiate(UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Prefabs/Unit_3103.prefab"));
            GameObject normalTarget = null;
            GameObject guardTarget = null;
            GameObject parryTarget = null;
            try
            {
                UnitBase owner = instance.GetComponent<UnitBase>();
                CombatStats ownerStats = instance.GetComponent<CombatStats>();
                typeof(CombatStats).GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic)
                    ?.Invoke(ownerStats, null);
                typeof(UnitBase).GetField("stats", BindingFlags.Instance | BindingFlags.NonPublic)
                    ?.SetValue(owner, ownerStats);
                UnitAttackHitbox2D hitbox = instance.GetComponentInChildren<UnitAttackHitbox2D>(true);
                Collider2D weapon = (Collider2D)typeof(UnitAttackHitbox2D)
                    .GetField("weaponAttackCollider", BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(hitbox);
                Collider2D torso = (Collider2D)typeof(UnitAttackHitbox2D)
                    .GetField("torsoAttackCollider", BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(hitbox);
                Transform weaponVisual = (Transform)typeof(UnitAttackHitbox2D)
                    .GetField("weaponVisual", BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(hitbox);
                Assert.NotNull(weapon);
                Assert.NotNull(torso);
                Assert.IsTrue(torso.isTrigger);
                Assert.IsFalse(torso.enabled);
                Vector3 visualPosition = weaponVisual.localPosition;
                Quaternion visualRotation = weaponVisual.localRotation;
                Vector3 visualScale = weaponVisual.localScale;

                hitbox.Bind(owner);
                owner.SetFacingRight(true);
                Assert.IsTrue(hitbox.TryOpen(7007, owner.ActionGeneration, 0,
                    AttackSubject.BodyPart, BodyPartRole.Torso, out CombatStats.AttackSweep2D sweep));
                Assert.IsFalse(weapon.enabled);
                Assert.IsTrue(torso.enabled);
                Assert.AreEqual(2.4f, sweep.HalfExtents.x * 2f, .001f);
                Assert.AreEqual(2.4f, sweep.HalfExtents.y * 2f, .001f);

                CombatStats CreateTarget(string name, bool guard, bool parry, out GameObject targetObject)
                {
                    targetObject = new GameObject(name);
                    targetObject.transform.position = new Vector3(2f, 0f, 0f);
                    BoxCollider2D body = targetObject.AddComponent<BoxCollider2D>();
                    body.size = new Vector2(.8f, 1.6f);
                    body.offset = new Vector2(0f, 1.6f);
                    CombatStats targetStats = targetObject.AddComponent<CombatStats>();
                    typeof(CombatStats).GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic)
                        ?.Invoke(targetStats, null);
                    targetStats.SetDefenseBodyCollider(body);
                    targetStats.SetFacingRight(false);
                    targetStats.SetGuarding(guard);
                    targetStats.SetParrying(parry);
                    return targetStats;
                }

                CombatStats normal = CreateTarget("TorsoNormal", false, false, out normalTarget);
                normal.TakeDamage(pattern.Damage, attacker: owner.Stats, attackSweep: sweep);
                Assert.AreEqual(85f, normal.CurrentHp, .001f);

                CombatStats guard = CreateTarget("TorsoGuard", true, false, out guardTarget);
                guard.TakeDamage(pattern.Damage, attacker: owner.Stats, attackSweep: sweep);
                Assert.AreEqual(100f, guard.CurrentHp, .001f);
                Assert.AreEqual(7.5f, guard.CurrentPosture, .001f);

                CombatStats parry = CreateTarget("TorsoParry", false, true, out parryTarget);
                parry.TakeDamage(pattern.Damage, attacker: owner.Stats, attackSweep: sweep);
                Assert.AreEqual(100f, parry.CurrentHp, .001f);
                Assert.AreEqual(0f, parry.CurrentPosture, .001f);
                Assert.AreEqual(40f, owner.Stats.CurrentPosture, .001f);

                owner.CancelAttackHitbox();
                Assert.IsFalse(weapon.enabled);
                Assert.IsFalse(torso.enabled);
                Assert.AreEqual(visualPosition, weaponVisual.localPosition);
                Assert.AreEqual(visualRotation, weaponVisual.localRotation);
                Assert.AreEqual(visualScale, weaponVisual.localScale);
            }
            finally
            {
                Object.DestroyImmediate(normalTarget);
                Object.DestroyImmediate(guardTarget);
                Object.DestroyImmediate(parryTarget);
                Object.DestroyImmediate(instance);
            }
        }
    }
}
