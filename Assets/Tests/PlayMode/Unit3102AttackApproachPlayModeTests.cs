using System.Reflection;
using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace Tests.PlayMode
{
    public sealed class Unit3102AttackApproachPlayModeTests
    {
        [Test]
        public void Unit3102_RegisteredSimplePatterns_SelectByDistanceAndCooldown()
        {
            GameObject managerObject = new GameObject("PatternDataManager_QA");
            managerObject.SetActive(false);
            GameObject monsterObject = null;
            GameObject playerObject = null;
            FieldInfo singleton = typeof(Singleton<DataTableManager>).GetField(
                "<Instance>k__BackingField", BindingFlags.Static | BindingFlags.NonPublic);
            try
            {
                if (Player.Instance != null) Object.DestroyImmediate(Player.Instance.gameObject);
                DataTableManager manager = managerObject.AddComponent<DataTableManager>();
                singleton.SetValue(null, manager);
                var dataList = (System.Collections.Generic.Dictionary<DataTableType, IDataLoad>)typeof(DataTableManager)
                    .GetField("dataList", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(manager);
                var skills = new SkillDataTable();
                skills.LoadData(File.ReadAllText("Assets/Datas/SkillData.csv"));
                dataList[DataTableType.Skill] = skills;

                var units = new MonsterDataTable();
                var patterns = new MonsterPatternDataTable();
                var unitBases = new UnitBaseDataTable();
                units.LoadData(File.ReadAllText("Assets/Datas/MonsterBaseData.csv"));
                patterns.LoadData(File.ReadAllText("Assets/Datas/MonsterPatternData.csv"));
                unitBases.LoadData(File.ReadAllText("Assets/Datas/UnitBaseData.csv"));
                Assert.IsTrue(units.TryGetMonsterData(3102u, out var unitData));
                Assert.IsTrue(unitBases.TryGetUnitData(3102u, out var monsterUnitData));
                Assert.IsTrue(unitBases.TryGetUnitData(3001u, out var playerUnitData));

                monsterObject = Object.Instantiate(UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(
                    "Assets/Prefabs/Unit_3102.prefab"));
                playerObject = Object.Instantiate(UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(
                    "Assets/Prefabs/Unit_3001.prefab"));
                Monster monster = monsterObject.GetComponent<Monster>();
                Player player = playerObject.GetComponent<Player>();
                CapsuleCollider2D monsterBody = monsterObject.GetComponent<CapsuleCollider2D>() ??
                    monsterObject.AddComponent<CapsuleCollider2D>();
                CapsuleCollider2D playerBody = playerObject.GetComponent<CapsuleCollider2D>() ??
                    playerObject.AddComponent<CapsuleCollider2D>();
                monsterBody.size = new Vector2(monsterUnitData.HitboxRadius * 2f, monsterUnitData.HitboxRadius * 4f);
                playerBody.size = new Vector2(playerUnitData.HitboxRadius * 2f, playerUnitData.HitboxRadius * 4f);
                monster.Stats.SetDefenseBodyCollider(monsterBody);
                player.Stats.SetDefenseBodyCollider(playerBody);
                Assert.NotNull(monster.Stats.DefenseBodyCollider);
                Assert.NotNull(player.Stats.DefenseBodyCollider);
                monster.Patterns.Clear();
                foreach (uint idx in unitData.PatternIdxList)
                {
                    Assert.IsTrue(patterns.TryGetPatternData(idx, out var pattern));
                    monster.Patterns.Add(pattern);
                }
                CollectionAssert.AreEqual(new uint[] { 6008u, 6009u }, unitData.PatternIdxList);
                typeof(Monster).GetField("playerTarget", BindingFlags.Instance | BindingFlags.NonPublic)
                    .SetValue(monster, playerObject.transform);
                MethodInfo select = typeof(Monster).GetMethod("SelectNextPattern",
                    BindingFlags.Instance | BindingFlags.NonPublic, null, System.Type.EmptyTypes, null);
                var cooldowns = (System.Collections.Generic.Dictionary<uint, float>)typeof(Monster)
                    .GetField("patternCooldowns", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(monster);

                MonsterPatternData SelectAt(float distance)
                {
                    monsterObject.transform.position = Vector3.zero;
                    playerObject.transform.position = Vector3.right * distance;
                    Physics2D.SyncTransforms();
                    return (MonsterPatternData)select.Invoke(monster, null);
                }

                MonsterPatternData SelectAtSurfaceGap(float gap)
                {
                    float widths = monster.Stats.DefenseBodyCollider.bounds.extents.x +
                        player.Stats.DefenseBodyCollider.bounds.extents.x;
                    return SelectAt(widths + gap);
                }

                Assert.IsNull(SelectAt(3f));
                MonsterPatternData thrust = SelectAt(2.5f);
                MonsterPatternData barrage = SelectAtSurfaceGap(1f);
                Assert.AreEqual(6008u, thrust.Idx);
                Assert.AreEqual(6009u, barrage.Idx);
                foreach (float centerDistance in new[] { 2f, 1.81f, 1.5f, 1.2f, .8f })
                    Assert.AreEqual(6009u, SelectAt(centerDistance).Idx,
                        $"An in-band 6009 must win before 6008 retreat reservation at center distance {centerDistance}.");
                Assert.IsTrue(skills.TryGetSkillData(thrust.SkillIdx, out var thrustSkill));
                Assert.IsTrue(skills.TryGetSkillData(barrage.SkillIdx, out var barrageSkill));
                Assert.AreEqual(7005u, thrustSkill.Idx);
                Assert.AreEqual(14, thrustSkill.AnimState);
                Assert.AreEqual(10002u, SkillExecutor.ResolveAttackMotionProfileIdx(thrustSkill, thrust.AttackMotionProfileIdx));
                Assert.AreEqual(7006u, barrageSkill.Idx);
                Assert.AreEqual(15, barrageSkill.AnimState);
                Assert.AreEqual(10001u, SkillExecutor.ResolveAttackMotionProfileIdx(barrageSkill, barrage.AttackMotionProfileIdx));

                cooldowns[6008u] = Time.time + 1f;
                Assert.IsNull(SelectAt(2.5f));
                cooldowns[6008u] = Time.time - 1f;
                Assert.AreEqual(6008u, SelectAt(2.5f).Idx);
                cooldowns[6009u] = Time.time + 1f;
                Assert.IsNull(SelectAtSurfaceGap(1f));
                cooldowns[6009u] = Time.time - 1f;
                Assert.AreEqual(6009u, SelectAtSurfaceGap(1f).Idx);

                string[] clipNames = System.Array.ConvertAll(
                    monsterObject.GetComponentInChildren<Animator>(true).runtimeAnimatorController.animationClips,
                    clip => clip.name);
                CollectionAssert.Contains(clipNames, "ShadowStalker_Thrust_PrototypeDummy");
                CollectionAssert.Contains(clipNames, "ShadowStalker_Barrage_PrototypeDummy");
                string source = File.ReadAllText("Assets/Scripts/Gameplay/Monster.cs");
                StringAssert.DoesNotContain("Util.CreateDataIdx(DataTableType.Skill, 1)", source);
                StringAssert.DoesNotContain("SetAnimState(7)", source);
            }
            finally
            {
                singleton?.SetValue(null, null);
                if (monsterObject != null) Object.DestroyImmediate(monsterObject);
                if (playerObject != null) Object.DestroyImmediate(playerObject);
                Object.DestroyImmediate(managerObject);
            }
        }

        [Test]
        public void Unit3102_ChaseStationaryAndPrototypeStep_DoNotCrossTarget()
        {
            GameObject ground = new GameObject("Unit3102Ground_QA", typeof(BoxCollider2D));
            GameObject monsterObject = null;
            GameObject playerObject = null;
            try
            {
                ground.layer = 0;
                ground.transform.position = new Vector3(0f, -.5f);
                ground.GetComponent<BoxCollider2D>().size = new Vector2(30f, 1f);

                GameObject monsterPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Unit_3102.prefab");
                GameObject playerPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Unit_3001.prefab");
                Assert.NotNull(monsterPrefab);
                Assert.NotNull(playerPrefab);
                monsterObject = Object.Instantiate(monsterPrefab);
                playerObject = Object.Instantiate(playerPrefab);
                Monster monster = monsterObject.GetComponent<Monster>();
                Player player = playerObject.GetComponent<Player>();
                KinematicMotor2D motor = monsterObject.GetComponent<KinematicMotor2D>();
                Assert.NotNull(monster);
                Assert.NotNull(player);
                Assert.NotNull(monsterObject.GetComponent<SkillExecutor>());
                Assert.NotNull(motor);
                Assert.NotNull(monster.AttackHitbox);
                Assert.NotNull(player.Stats);
                player.Stats.SetDefenseBodyCollider(playerObject.GetComponent<CapsuleCollider2D>());

                MethodInfo acquire = typeof(Monster).GetMethod("TryAcquireAttackToken", BindingFlags.Instance | BindingFlags.NonPublic);
                MethodInfo release = typeof(Monster).GetMethod("ReleaseAttackToken", BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.NotNull(acquire);
                Assert.NotNull(release);

                foreach (float dt in new[] { 1f / 15f, 1f / 60f })
                foreach (float side in new[] { -1f, 1f })
                {
                    Vector2 playerPosition = new Vector2(0f, .01f);
                    Vector2 monsterPosition = new Vector2(side * 5f, .01f);
                    playerObject.transform.position = playerPosition;
                    playerObject.GetComponent<KinematicMotor2D>().Teleport(playerPosition);
                    monsterObject.transform.position = monsterPosition;
                    motor.Teleport(monsterPosition);
                    Physics2D.SyncTransforms();
                    bool facingRight = player.transform.position.x >= monster.transform.position.x;
                    monster.SetFacingRight(facingRight);
                    bool hasReach = monster.TryGetAttackForwardReach(facingRight, out float measuredReach);
                    Collider2D attackCollider = (Collider2D)typeof(UnitAttackHitbox2D)
                        .GetField("attackCollider", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(monster.AttackHitbox);
                    bool wasEnabled = attackCollider.enabled;
                    attackCollider.enabled = true;
                    Physics2D.SyncTransforms();
                    Bounds attackBounds = attackCollider.bounds;
                    attackCollider.enabled = wasEnabled;
                    Assert.IsTrue(hasReach,
                        $"facingRight={facingRight}, reach={measuredReach}, ownerX={monster.transform.position.x}, " +
                        $"bounds=({attackBounds.min.x},{attackBounds.max.x}), attachScale={monster.AttackHitbox.transform.localScale.x}");
                    Assert.IsTrue(monster.TryGetAttackApproachStopX(player, out float stopX));
                    float direction = Mathf.Sign(stopX - monster.transform.position.x);
                    Rigidbody2D monsterBody = monsterObject.GetComponent<Rigidbody2D>();
                    motor.SetHorizontalStopPosition(stopX);
                    motor.SetTargetVelocityX(direction * 4.5f);

                    for (int step = 0; step < 120; step++)
                    {
                        motor.SetGroundNormal(Vector2.up);
                        motor.SimulateStep(dt);
                        Assert.GreaterOrEqual((stopX - monsterBody.position.x) * direction, -.001f,
                            "Unit_3102 must not cross the target-facing stop point.");
                    }
                    Assert.AreEqual(stopX, monsterBody.position.x, motor.SkinWidth + 4.5f * dt);

                    for (int attack = 0; attack < 2; attack++)
                    {
                        motor.SetTargetVelocityX(direction * 4.5f);
                        motor.SetGroundNormal(Vector2.up);
                        motor.SimulateStep(dt);
                        Assert.AreNotEqual(0f, motor.Velocity.x);
                        Assert.IsTrue((bool)acquire.Invoke(monster, new object[] { true }));
                        Assert.AreEqual(0f, motor.Velocity.x, .0001f);
                        float attackStartX = monsterBody.position.x;
                        for (float elapsed = 0f; elapsed < Monster.AttackTelegraphLeadSeconds; elapsed += dt)
                        {
                            motor.SetGroundNormal(Vector2.up);
                            motor.SimulateStep(dt);
                        }
                        Assert.AreEqual(attackStartX, monsterBody.position.x, .001f,
                            "Stationary startup must not retain chase velocity.");
                        release.Invoke(monster, null);
                    }

                    const float thrustDistance = .405f;
                    const float thrustSpeed = 4.5f;
                    const float startupSeconds = .09f;
                    float thrustStopX = monsterBody.position.x + direction * thrustDistance;
                    var thrust = new AttackMotionProfileData
                    {
                        MotionType = AttackMotionType.Step,
                        TargetPolicy = AttackTargetPolicy.SnapshotAtStartup,
                        MaxDistance = thrustDistance,
                        MaxSpeed = thrustSpeed,
                        Acceleration = 0f,
                        Enabled = true
                    };
                    float remaining = startupSeconds;
                    while (remaining > 0f)
                    {
                        float stepDt = Mathf.Min(dt, remaining);
                        motor.SetHorizontalStopPosition(thrustStopX);
                        motor.SetTargetVelocityX(SkillExecutor.CalculateAttackMotionVelocity(
                            thrust, monsterBody.position.x, thrustStopX, remaining, motor.Velocity.x, stepDt));
                        motor.SetGroundNormal(Vector2.up);
                        motor.SimulateStep(stepDt);
                        Assert.GreaterOrEqual((thrustStopX - monsterBody.position.x) * direction, -.001f,
                            "Prototype thrust must not cross its snapshot stop point.");
                        remaining -= stepDt;
                    }
                    Assert.AreEqual(thrustStopX, monsterBody.position.x, motor.SkinWidth + thrustSpeed * dt);
                    player.Stats.MaxHp = 100f;
                    player.Stats.InitStats();
                    float hpBefore = player.Stats.CurrentHp;
                    monster.SetFacingRight(player.transform.position.x >= monster.transform.position.x);
                    Physics2D.SyncTransforms();
                    Assert.IsTrue(monster.TryOpenAttackHitbox(7005, monster.ActionGeneration, 0, out var thrustSweep));
                    Assert.IsTrue(player.Stats.TryGetAttackSweepFraction(thrustSweep, out _),
                        "Unit_3102 Step must place the Thrust sweep across the Player defense body.");
                    player.Stats.TakeDamage(15f, attacker: monster.Stats, attackOrigin: thrustSweep.Previous,
                        attackSweep: thrustSweep);
                    Assert.Less(player.Stats.CurrentHp, hpBefore, "Unit_3102 Thrust must apply one hostile hit.");
                    monster.CloseAttackHitbox();
                    motor.StopHorizontalImmediately();
                    float clipEndX = monsterBody.position.x;
                    for (float elapsed = startupSeconds; elapsed < 2f; elapsed += dt)
                    {
                        motor.SetGroundNormal(Vector2.up);
                        motor.SimulateStep(dt);
                    }
                    Assert.AreEqual(clipEndX, monsterBody.position.x, .001f,
                        "Thrust recovery must not retain motion after the active-time stop.");
                }
            }
            finally
            {
                if (monsterObject != null) Object.DestroyImmediate(monsterObject);
                if (playerObject != null) Object.DestroyImmediate(playerObject);
                Object.DestroyImmediate(ground);
            }
        }
    }
}
