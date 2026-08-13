using NUnit.Framework;
using System.Reflection;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace QA.Tests
{
    public sealed class MonsterAttackTelegraphTests
    {
        [TestCase(0.25f, 0f, 1.5f)]
        [TestCase(0.25f, 0.10f, 1.5f)]
        [TestCase(2f, 0.10f, 2.10f)]
        public void EffectiveDelay_GuaranteesLeadWithoutOverwritingPattern(
            float configuredPreDelay, float firstHitTiming, float expectedImpactOffset)
        {
            float effective = Monster.CalculateEffectivePreDelay(configuredPreDelay, firstHitTiming);
            Assert.AreEqual(expectedImpactOffset, effective + firstHitTiming, 0.001f);
            Assert.GreaterOrEqual(effective + firstHitTiming, Monster.AttackTelegraphLeadSeconds);
            Assert.GreaterOrEqual(effective, configuredPreDelay);
        }

        [Test]
        public void Fill_IsMonotonicAtFifteenFps_AndActiveWindowStaysFull()
        {
            float previous = 0f;
            const float step = 1f / 15f;
            for (float now = 0f; now <= 1.6f; now += step)
            {
                float fill = ProductionMainHUD.CalculateAttackTelegraphFill(now, 0f, 1.5f);
                Assert.GreaterOrEqual(fill, previous);
                previous = fill;
            }
            Assert.AreEqual(1f, ProductionMainHUD.CalculateAttackTelegraphFill(1.6f, 0f, 1.5f));
        }

        [Test]
        public void Hud_SelectsEarliestImpact_AndGenerationEndRevealsNext()
        {
            var hudObject = new GameObject("AttackTelegraphHud_QA");
            var imageObject = new GameObject("AttackTelegraphFill_QA", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            var firstObject = new GameObject("FirstMonster_QA");
            var secondObject = new GameObject("SecondMonster_QA");
            try
            {
                var hud = hudObject.AddComponent<ProductionMainHUD>();
                var image = imageObject.GetComponent<Image>();
                SetField(hud, "attackTelegraphFill", image);
                var first = firstObject.AddComponent<Monster>();
                var second = secondObject.AddComponent<Monster>();
                float now = Time.time;
                var early = new Monster.AttackTelegraph(first, first.ActionGeneration,
                    now - 0.75f, now + 0.75f, now + 2f);
                var late = new Monster.AttackTelegraph(second, second.ActionGeneration,
                    now - 0.5f, now + 1f, now + 2f);

                Invoke(hud, "OnAttackTelegraphStarted", late);
                Invoke(hud, "OnAttackTelegraphStarted", early);
                Invoke(hud, "Update");
                Assert.AreEqual(0.5f, image.fillAmount, 0.02f, "Earliest impact must own the shared HUD.");

                Invoke(hud, "OnAttackTelegraphEnded", first, first.ActionGeneration);
                Invoke(hud, "Update");
                Assert.AreEqual(1f / 3f, image.fillAmount, 0.02f,
                    "Ending one generation must reveal the next candidate without stale ownership.");
            }
            finally
            {
                Object.DestroyImmediate(firstObject);
                Object.DestroyImmediate(secondObject);
                Object.DestroyImmediate(imageObject);
                Object.DestroyImmediate(hudObject);
            }
        }

        [Test]
        public void GroggyAndDisable_InvalidateEachMatchingGenerationOnce()
        {
            var monsterObject = new GameObject("TelegraphGroggyMonster_QA");
            int ended = 0;
            void OnEnded(Monster _, uint __) => ended++;
            Monster.AttackTelegraphEnded += OnEnded;
            try
            {
                var monster = monsterObject.AddComponent<Monster>();
                uint generation = monster.ActionGeneration;
                SetField(monster, "telegraphGeneration", generation);
                SetField(monster, "telegraphActive", true);
                Invoke(monster, "OnGroggyStarted");
                Invoke(monster, "OnGroggyStarted");
                Assert.AreEqual(1, ended);
                Assert.IsFalse(monster.IsActionGenerationCurrent(generation));

                uint nextGeneration = monster.ActionGeneration;
                SetField(monster, "telegraphGeneration", nextGeneration);
                SetField(monster, "telegraphActive", true);
                Invoke(monster, "OnDisable");
                Assert.AreEqual(2, ended, "Disable/chunk unload must cancel its current generation once.");
            }
            finally
            {
                Monster.AttackTelegraphEnded -= OnEnded;
                Object.DestroyImmediate(monsterObject);
            }
        }

        [Test]
        public void MainScene_TelegraphReferencesAndFillContractAreSerialized()
        {
            var scene = EditorSceneManager.OpenScene("Assets/Scenes/MainScene.unity", OpenSceneMode.Additive);
            try
            {
                var hud = scene.GetRootGameObjects()
                    .SelectMany(root => root.GetComponentsInChildren<ProductionMainHUD>(true)).Single();
                var serialized = new SerializedObject(hud);
                Assert.NotNull(serialized.FindProperty("attackTelegraphGroup").objectReferenceValue);
                var fill = serialized.FindProperty("attackTelegraphFill").objectReferenceValue as Image;
                Assert.NotNull(fill);
                Assert.AreEqual(Image.Type.Filled, fill.type);
                Assert.AreEqual(Image.FillMethod.Horizontal, fill.fillMethod);
                Assert.AreEqual(0, fill.fillOrigin);
                Assert.IsFalse(fill.raycastTarget);
                Assert.NotNull(fill.sprite);
                Assert.AreEqual("Sprite_UI_SolidFill", fill.sprite.name);
            }
            finally { EditorSceneManager.CloseScene(scene, true); }
        }

        private static void SetField(object target, string name, object value) =>
            target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);

        private static object Invoke(object target, string name, params object[] args) =>
            target.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(target, args);
    }
}
