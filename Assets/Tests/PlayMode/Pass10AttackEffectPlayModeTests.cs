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
