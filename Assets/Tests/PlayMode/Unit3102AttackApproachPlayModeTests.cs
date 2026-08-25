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

    }
}
