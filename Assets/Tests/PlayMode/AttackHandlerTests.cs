// File: AttackHandlerTests.cs
using System.Threading.Tasks;
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
            var attackPower = _attacker.AddComponent<AttackPower>();
            attackPower.BaseDamage = 10f; // expect 10 damage
            attackPower.DamagePercent = 0f;
            attackPower.DamageFlat = 0f;
            // ensure target mask includes Default layer
            // AttackPower.GetAttackData uses hardcoded "Enemy","Player" mask, override for test by reflection
            // Instead we directly modify the returned AttackData in AttackHandler via a subclass (not needed here)
            // Place target within range (2 units) and within hit radius (0.5) => target at (1,0,0)
            _target.transform.position = new Vector3(1f, 0f, 0f);

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
            await _handler.PerformAttackAsync().AsTask();

            // Verify target health reduced by 10
            Assert.AreEqual(90f, _targetHealth.CurrentHealth, 0.01f);
        }
    }
}
