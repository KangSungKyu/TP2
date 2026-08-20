using System.Threading.Tasks;
using System.Reflection;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Events;

namespace Tests.PlayMode
{
    public sealed class MonsterSpawnAreaLeashPlayModeTests
    {
        [Test]
        public async Task BoundsExit_ReturnsToOriginWithoutGroundEscape()
        {
            var owner = new GameObject("LeashPlayModeMonster", typeof(Rigidbody2D), typeof(CapsuleCollider2D),
                typeof(CombatStats), typeof(KinematicMotor2D), typeof(Monster));
            try
            {
                Monster monster = owner.GetComponent<Monster>();
                CombatStats stats = owner.GetComponent<CombatStats>();
                stats.OnDeath ??= new UnityEvent();
                stats.OnGroggyState ??= new UnityEvent();
                stats.OnGroggyEnded ??= new UnityEvent();
                var bounds = new Bounds(Vector3.zero, new Vector3(20f, 20f, 2f));
                Assert.IsTrue(monster.ConfigureSpawnArea(Vector3.zero, bounds, false));
                owner.transform.position = new Vector3(15f, 0f);
                typeof(Monster).GetMethod("FixedUpdate", BindingFlags.Instance | BindingFlags.NonPublic)!
                    .Invoke(monster, null);
                owner.transform.position = owner.GetComponent<Rigidbody2D>().position;
                await UniTask.Yield();

                Assert.AreEqual(Monster.LeashState.Idle, monster.CurrentLeashState);
                Assert.LessOrEqual(Vector2.Distance(owner.transform.position, monster.SpawnOrigin), .01f);
                Assert.AreEqual(monster.Stats.MaxHp, monster.Stats.CurrentHp, .001f);
            }
            finally { Object.Destroy(owner); }
        }
    }
}
