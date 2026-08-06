// File: AttackHandlerTests.cs
using System.Threading.Tasks;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Gameplay.Combat;
using Cysharp.Threading.Tasks;

namespace Tests.PlayMode
{
    public class AttackHandlerTests
    {
        private GameObject _attacker;
        private GameObject _target;
        private Health _targetHealth;
        private AttackHandler _handler;
        private AttackPower _attackPower;

        [SetUp]
        public void SetUp()
        {
            // Create target with Health and Collider
            _target = new GameObject("Target");
            _target.layer = LayerMask.NameToLayer("Default");
            var collider = _target.AddComponent<SphereCollider>();
            collider.isTrigger = false;
            _targetHealth = _target.AddComponent<Health>();
            _targetHealth.MaxHealth = 100f;

            // Create attacker with AttackPower, AttackHandler and a dummy Health (not used)
            _attacker = new GameObject("Attacker");
            _attacker.transform.position = Vector3.zero;
            _attacker.layer = LayerMask.NameToLayer("Default");
            _attackPower = _attacker.AddComponent<AttackPower>();
            _attackPower.BaseDamage = 10f; // expect 10 damage
            _attackPower.DamagePercent = 0f;
            _attackPower.DamageFlat = 0f;
            _target.transform.position = Vector3.forward * 1.5f;

            _handler = _attacker.AddComponent<AttackHandler>();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_attacker);
            Object.DestroyImmediate(_target);
        }

        [Test]
        public async Task MeleeAttackAppliesDamage()
        {
            AttackData data = _attackPower.GetAttackData();
            data.TargetMask = 1 << LayerMask.NameToLayer("Default");
            Physics.SyncTransforms();
            var method = typeof(AttackHandler).GetMethod(
                "PerformMeleeAttackAsync", BindingFlags.Instance | BindingFlags.NonPublic);
            await ((UniTask)method.Invoke(_handler, new object[] { data })).AsTask();

            // Verify target health reduced by 10
            Assert.AreEqual(90f, _targetHealth.CurrentHealth, 0.01f);
        }
    }
}
