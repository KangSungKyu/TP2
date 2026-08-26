using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace QA.Tests
{
    public sealed class MonsterSpawnAreaLeashTests
    {
        [Test]
        public void DevelopmentLeash_IsMarkerCenteredFortyMetersClippedToRoom()
        {
            MethodInfo resolve = typeof(UnitSpawner).GetMethod("ResolveDevelopmentMovementBounds",
                BindingFlags.Static | BindingFlags.NonPublic);
            Assert.NotNull(resolve);

            var room = new Bounds(new Vector3(10f, 0f, 0f), new Vector3(60f, 30f, 2f));
            Bounds clipped = (Bounds)resolve.Invoke(null, new object[] { Vector3.zero, room });
            Assert.AreEqual(new Vector3(0f, 0f, 0f), clipped.center);
            Assert.AreEqual(new Vector3(40f, 30f, 2f), clipped.size);

            Bounds fallback = (Bounds)resolve.Invoke(null, new object[] { new Vector3(100f, 0f), room });
            Assert.AreEqual(room, fallback, "A room that does not contain the origin remains authoritative.");
        }

        [TestCase(false, 100f)]
        [TestCase(true, 75f)]
        public void BoundsExit_CancelsGenerationAndRestoresAccordingToArenaPolicy(bool bossArena, float expectedHp)
        {
            GameObject monsterObject = null;
            try
            {
                monsterObject = CreateUnitObject("LeashMonster");
                Monster monster = monsterObject.AddComponent<Monster>();
                typeof(Monster).GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(monster, null);
                KinematicMotor2D motor = monsterObject.GetComponent<KinematicMotor2D>();
                Assert.NotNull(monster);
                Assert.NotNull(motor);
                Assert.NotNull(monster.Stats);
                motor.InitMotor();
                monster.Stats.InitStats();
                var bounds = new Bounds(Vector3.zero, new Vector3(20f, 20f, 2f));
                Assert.IsTrue(monster.ConfigureSpawnArea(Vector3.zero, bounds, bossArena));
                Assert.IsFalse(monster.ConfigureSpawnArea(new Vector3(30f, 0f), bounds, bossArena));
                monster.Stats.TakeDamage(25f);
                uint generation = monster.ActionGeneration;

                monsterObject.transform.position = new Vector3(15f, 0f);
                Physics2D.SyncTransforms();
                InvokeFixedUpdate(monster);
                monsterObject.transform.position = monsterObject.GetComponent<Rigidbody2D>().position;

                Assert.AreEqual(Vector3.zero, monsterObject.transform.position);
                Assert.AreEqual(Monster.LeashState.Idle, monster.CurrentLeashState);
                Assert.Greater(monster.ActionGeneration, generation);
                Assert.AreEqual(expectedHp, monster.Stats.CurrentHp, .001f);
            }
            finally
            {
                if (monsterObject != null) Object.DestroyImmediate(monsterObject);
            }
        }

        private static GameObject CreateUnitObject(string name)
        {
            var result = new GameObject(name, typeof(Rigidbody2D), typeof(CapsuleCollider2D),
                typeof(CombatStats), typeof(KinematicMotor2D));
            result.layer = LayerMask.NameToLayer("Enemy");
            return result;
        }

        private static void InvokeFixedUpdate(Monster monster) =>
            typeof(Monster).GetMethod("FixedUpdate", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(monster, null);
    }
}
