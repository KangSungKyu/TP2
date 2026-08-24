using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;

namespace Tests.PlayMode
{
    public sealed class Pass10TestUnit : UnitBase { }

    public sealed class Pass10AttackEffectPlayModeTests
    {
        [Test]
        public async Task Effect8014_AddressablePoolBoundsDamageAndCleanupContract()
        {
            DataTableManager previousData = DataTableManager.Instance;
            ResourceManager previousResource = ResourceManager.Instance;
            EffectPoolManager previousPool = EffectPoolManager.Instance;
            SkillExecutor previousExecutor = SkillExecutor.Instance;
            GameObject dataObject = null, resourceObject = null, poolObject = null;
            GameObject ownerObject = null, targetObject = null;
            try
            {
                dataObject = new GameObject("Pass10Data_PlayMode");
                dataObject.SetActive(false);
                DataTableManager data = dataObject.AddComponent<DataTableManager>();
                SetSingleton(data);
                var tables = (Dictionary<DataTableType, IDataLoad>)typeof(DataTableManager)
                    .GetField("dataList", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(data);
                var resources = new ResourceDataTable();
                var effects = new EffectDataTable();
                var skills = new SkillDataTable();
                resources.LoadData(File.ReadAllText("Assets/Datas/ResourceData.csv"));
                effects.LoadData(File.ReadAllText("Assets/Datas/EffectData.csv"));
                skills.LoadData(File.ReadAllText("Assets/Datas/SkillData.csv"));
                tables[DataTableType.Resource] = resources;
                tables[DataTableType.EffectData] = effects;
                tables[DataTableType.Skill] = skills;
                typeof(DataTableManager).GetField("isLoaded", BindingFlags.Instance | BindingFlags.NonPublic)
                    .SetValue(data, true);

                resourceObject = new GameObject("Pass10Resource_PlayMode");
                resourceObject.SetActive(false);
                SetSingleton(resourceObject.AddComponent<ResourceManager>());
                poolObject = new GameObject("Pass10EffectPool_PlayMode");
                poolObject.SetActive(false);
                SetSingleton(poolObject.AddComponent<EffectPoolManager>());
                poolObject.SetActive(true);

                ownerObject = new GameObject("Pass10Player_PlayMode");
                ownerObject.SetActive(false);
                CombatStats ownerStats = ownerObject.AddComponent<CombatStats>();
                Pass10TestUnit owner = ownerObject.AddComponent<Pass10TestUnit>();
                if (ownerObject.GetComponent<KinematicMotor2D>() is { } ownerMotor) ownerMotor.enabled = false;
                SkillExecutor executor = ownerObject.AddComponent<SkillExecutor>();
                GameObject attach = new GameObject("AttackAttach");
                attach.transform.SetParent(ownerObject.transform, false);
                BoxCollider2D attackCollider = attach.AddComponent<BoxCollider2D>();
                attackCollider.isTrigger = true;
                attackCollider.enabled = false;
                attackCollider.size = Vector2.one;
                UnitAttackHitbox2D hitbox = attach.AddComponent<UnitAttackHitbox2D>();
                SetField(hitbox, "weaponAttackCollider", attackCollider);
                SetField(hitbox, "attachRoot", attach.transform);
                SetField(owner, "stats", ownerStats);
                SetField(owner, "skillExecutor", executor);
                SetField(owner, "attackHitbox", hitbox);
                SetBackingField(owner, "UnitIdx", 3001u);
                SetBackingField(owner, "UnitData", new UnitBaseData { Faction = 1u });
                ownerObject.SetActive(true);
                hitbox.Bind(owner);

                targetObject = new GameObject("Pass10Monster_PlayMode");
                targetObject.transform.position = new Vector3(.16f, -.11f);
                BoxCollider2D targetBody = targetObject.AddComponent<BoxCollider2D>();
                targetBody.size = new Vector2(.2f, .2f);
                CombatStats targetStats = targetObject.AddComponent<CombatStats>();
                targetStats.MaxHp = 100f;
                targetStats.InitStats();
                targetStats.SetDefenseBodyCollider(targetBody);
                Pass10TestUnit target = targetObject.AddComponent<Pass10TestUnit>();
                if (targetObject.GetComponent<KinematicMotor2D>() is { } targetMotor) targetMotor.enabled = false;
                SetField(target, "stats", targetStats);
                SetBackingField(target, "UnitIdx", 3101u);
                SetBackingField(target, "UnitData", new UnitBaseData { Faction = 2u });
                Physics2D.SyncTransforms();

                Assert.IsTrue(SkillExecutor.TryResolvePass10AttackEffectIdx(3001, 0, 7001, out uint effectIdx));
                Assert.AreEqual(8014u, effectIdx);
                Assert.IsTrue(effects.TryGetEffectData(effectIdx, out EffectData effectData));

                Vector2 originalOffset = attackCollider.offset;
                Vector2 originalSize = attackCollider.size;
                owner.SetFacingRight(false);
                Assert.IsTrue(owner.TryOpenAttackHitbox(1, owner.ActionGeneration, 0,
                    AttackSubject.Weapon, BodyPartRole.None,
                    new Vector2(effectData.ActiveCenterX, effectData.ActiveCenterY),
                    new Vector2(effectData.ActiveSizeX, effectData.ActiveSizeY), out var leftSweep));
                Assert.AreEqual(-effectData.ActiveCenterX, leftSweep.Current.x, .001f);
                Assert.AreEqual(effectData.ActiveSizeX, leftSweep.HalfExtents.x * 2f, .001f);
                Assert.AreEqual(effectData.ActiveSizeY, leftSweep.HalfExtents.y * 2f, .001f);
                owner.CloseAttackHitbox();
                Assert.AreEqual(originalOffset, attackCollider.offset);
                Assert.AreEqual(originalSize, attackCollider.size);

                GameObject prewarmed = await executor.SpawnEffectByEffectIdxAsync(8014u, Vector3.zero);
                Assert.NotNull(prewarmed, "Effect_8014 must instantiate through ResourceManager/EffectPoolManager.");
                Animator pooledAnimator = prewarmed.GetComponent<Animator>();
                if (pooledAnimator != null && pooledAnimator.runtimeAnimatorController != null)
                {
                    pooledAnimator.Play(0, 0, .75f);
                    pooledAnimator.Update(0f);
                }
                EffectPoolManager.Instance.DespawnEffect(prewarmed);

                owner.SetFacingRight(true);
                var firstTask = executor.ExecuteSkillHitsAsync(7001, owner, target, 10f).AsTask();
                GameObject first = null;
                bool normalizedResetObserved = false;
                while (!firstTask.IsCompleted)
                {
                    first ??= GetActiveEffect(8014u);
                    if (first != null)
                    {
                        if (!normalizedResetObserved && pooledAnimator != null &&
                            pooledAnimator.runtimeAnimatorController != null)
                        {
                            Assert.Less(pooledAnimator.GetCurrentAnimatorStateInfo(0).normalizedTime, .1f,
                                "Pooled attack effect animation must restart at normalized time zero.");
                            normalizedResetObserved = true;
                        }
                        SpriteRenderer renderer = first.GetComponentInChildren<SpriteRenderer>(true);
                        if (renderer != null) renderer.enabled = false;
                    }
                    await UniTask.Yield(PlayerLoopTiming.Update);
                }
                Assert.IsFalse(firstTask.IsFaulted, firstTask.Exception?.ToString());
                Assert.NotNull(first, "Effect_8014 must spawn through ResourceManager/EffectPoolManager.");
                Assert.AreSame(prewarmed, first);
                Assert.IsTrue(pooledAnimator == null || pooledAnimator.runtimeAnimatorController == null ||
                    normalizedResetObserved);
                Assert.AreEqual(90f, targetStats.CurrentHp, .001f,
                    "Visual renderer state must not own damage authority.");
                Assert.IsFalse(first.activeSelf, "Window close must return the effect to its pool.");

                targetStats.InitStats();
                var secondTask = executor.ExecuteSkillHitsAsync(7001, owner, target, 10f).AsTask();
                GameObject second = null;
                while (!secondTask.IsCompleted)
                {
                    second ??= GetActiveEffect(8014u);
                    await UniTask.Yield(PlayerLoopTiming.Update);
                }
                Assert.IsFalse(secondTask.IsFaulted, secondTask.Exception?.ToString());
                Assert.AreSame(first, second, "Spawn-return-spawn must reuse the same GameObject identity.");
                Assert.AreEqual(90f, targetStats.CurrentHp, .001f);

                targetStats.InitStats();
                using (var cancel = new CancellationTokenSource())
                {
                    var cancelTask = executor.ExecuteSkillHitsAsync(7001, owner, target, 10f,
                        cancel.Token).AsTask();
                    GameObject active = null;
                    while (!cancelTask.IsCompleted && active == null)
                    {
                        active = GetActiveEffect(8014u);
                        await UniTask.Yield(PlayerLoopTiming.Update);
                    }
                    cancel.Cancel();
                    while (!cancelTask.IsCompleted) await UniTask.Yield(PlayerLoopTiming.Update);
                    Assert.IsTrue(cancelTask.IsCanceled || cancelTask.IsFaulted,
                        "Cancellation must terminate the active execution.");
                    await UniTask.Yield(PlayerLoopTiming.Update);
                    Assert.IsTrue(active == null || !active.activeSelf,
                        "Cancellation must not leave an active pooled effect.");
                    Assert.IsFalse(attackCollider.enabled);
                }
            }
            finally
            {
                if (ownerObject != null) Object.Destroy(ownerObject);
                if (targetObject != null) Object.Destroy(targetObject);
                if (poolObject != null) Object.Destroy(poolObject);
                if (resourceObject != null) Object.Destroy(resourceObject);
                if (dataObject != null) Object.Destroy(dataObject);
                await UniTask.Yield(PlayerLoopTiming.PostLateUpdate);
                SetSingleton(previousData);
                SetSingleton(previousResource);
                SetSingleton(previousPool);
                typeof(SkillExecutor).GetProperty("Instance", BindingFlags.Static | BindingFlags.Public)
                    .GetSetMethod(true)?.Invoke(null, new object[] { previousExecutor });
            }
        }

        private static GameObject GetActiveEffect(uint effectIdx)
        {
            var effects = DataTableManager.Instance.GetDB<EffectDataTable>(DataTableType.EffectData);
            var resources = DataTableManager.Instance.GetDB<ResourceDataTable>(DataTableType.Resource);
            if (effects == null || resources == null ||
                !effects.TryGetEffectData(effectIdx, out EffectData effectData) ||
                !resources.TryGetResource(effectData.PrefabIdx, out ResourceData resource)) return null;

            var active = (HashSet<GameObject>)typeof(EffectPoolManager)
                .GetField("activeEffects", BindingFlags.Instance | BindingFlags.NonPublic)
                .GetValue(EffectPoolManager.Instance);
            foreach (GameObject effect in active)
                if (effect != null && effect.activeSelf && effect.name == resource.Path) return effect;
            return null;
        }

        private static void SetField(object target, string name, object value)
        {
            for (System.Type type = target.GetType(); type != null; type = type.BaseType)
            {
                FieldInfo field = type.GetField(name,
                    BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
                if (field == null) continue;
                field.SetValue(target, value);
                return;
            }
            Assert.Fail($"Missing fixture field {target.GetType().Name}.{name}");
        }

        private static void SetBackingField(object target, string property, object value) => typeof(UnitBase)
            .GetField($"<{property}>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic)
            ?.SetValue(target, value);

        private static void SetSingleton<T>(T component) where T : MonoBehaviour =>
            typeof(Singleton<T>).GetField("<Instance>k__BackingField", BindingFlags.Static | BindingFlags.NonPublic)
                ?.SetValue(null, component);
    }
}
