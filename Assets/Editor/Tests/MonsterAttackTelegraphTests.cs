using NUnit.Framework;
using System.IO;
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
        [TestCase(0.045f, 0.045f, 0.045f)]
        [TestCase(0.05f, 0.0375f, 0.05f)]
        [TestCase(0f, 0.0625f, 0.0625f)]
        public void StepStartup_UsesPatternOrWindowOnce(
            float configuredPreDelay, float windowStart, float expectedStartup)
        {
            float lead = Monster.CalculateEffectivePreDelay(configuredPreDelay, windowStart);
            Assert.AreEqual(expectedStartup, lead + windowStart, 0.001f);
            Assert.AreEqual(Mathf.Max(configuredPreDelay, windowStart), lead + windowStart, 0.001f);
        }

        [TestCase(2f, 0f, 0f, false, 2f)]
        [TestCase(2f, .5f, 0f, true, 1.5f)]
        [TestCase(2f, 0f, .2f, true, 1.8f)]
        [TestCase(2f, .5f, .2f, true, 1.3f)]
        public void SkillTelegraph_AddsAttackMotionTimeAndPre(
            float attackStart, float preDuration, float attackMotionTime,
            bool expected, float expectedStart)
        {
            Assert.AreEqual(expected, Monster.TryCalculateSkillTelegraphWindow(attackStart, preDuration,
                attackMotionTime,
                out float displayStart, out float displayEnd));
            Assert.AreEqual(expectedStart, displayStart, .0001f);
            Assert.AreEqual(attackStart, displayEnd, .0001f, "Telegraph calculation cannot shift ATTACK.");
        }

        [TestCase(1f / 15f)]
        [TestCase(1f / 30f)]
        [TestCase(1f / 60f)]
        public void AttackMotionTime_DelaysEachChainStepExactlyOnce(float fixedStep)
        {
            float[] motion = { .18f, .15f, .25f };
            float baseline = 0f;
            float delayed = 0f;
            foreach (float value in motion)
            {
                baseline += Monster.CalculateSkillStartupSeconds(0f, .05f, 0f);
                delayed += Monster.CalculateSkillStartupSeconds(0f, .05f, value);
            }
            Assert.AreEqual(.58f, delayed - baseline, fixedStep);
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
        public void OverheadHud_FiltersOwner_AndAllowsConcurrentMonsterTelegraphs()
        {
            var firstHudObject = new GameObject("FirstAttackTelegraphHud_QA");
            var secondHudObject = new GameObject("SecondAttackTelegraphHud_QA");
            var firstImageObject = new GameObject("FirstAttackTelegraphFill_QA", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            var secondImageObject = new GameObject("SecondAttackTelegraphFill_QA", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            var firstObject = new GameObject("FirstMonster_QA");
            var secondObject = new GameObject("SecondMonster_QA");
            try
            {
                var first = firstObject.AddComponent<Monster>();
                var second = secondObject.AddComponent<Monster>();
                var firstHud = firstHudObject.AddComponent<MonsterOverheadHUD>();
                var secondHud = secondHudObject.AddComponent<MonsterOverheadHUD>();
                var firstImage = firstImageObject.GetComponent<Image>();
                var secondImage = secondImageObject.GetComponent<Image>();
                SetField(firstHud, "owner", first);
                SetField(firstHud, "attackTelegraphFill", firstImage);
                SetField(secondHud, "owner", second);
                SetField(secondHud, "attackTelegraphFill", secondImage);
                float now = Time.time;
                var firstAttack = new Monster.AttackTelegraph(first, first.ActionGeneration,
                    now - 0.75f, now + 0.75f, now + 2f);
                var secondAttack = new Monster.AttackTelegraph(second, second.ActionGeneration,
                    now - 0.5f, now + 1f, now + 2f);

                Invoke(firstHud, "OnAttackTelegraphStarted", secondAttack);
                Invoke(firstHud, "OnAttackTelegraphStarted", firstAttack);
                Invoke(secondHud, "OnAttackTelegraphStarted", firstAttack);
                Invoke(secondHud, "OnAttackTelegraphStarted", secondAttack);
                Invoke(firstHud, "Update");
                Invoke(secondHud, "Update");
                Assert.AreEqual(0.5f, firstImage.fillAmount, 0.02f);
                Assert.AreEqual(1f / 3f, secondImage.fillAmount, 0.02f);

                Invoke(firstHud, "OnAttackTelegraphEnded", first, first.ActionGeneration);
                Assert.IsFalse((bool)GetField(firstHud, "hasAttackTelegraph"));
                Assert.IsTrue((bool)GetField(secondHud, "hasAttackTelegraph"));
            }
            finally
            {
                Object.DestroyImmediate(firstObject);
                Object.DestroyImmediate(secondObject);
                Object.DestroyImmediate(firstImageObject);
                Object.DestroyImmediate(secondImageObject);
                Object.DestroyImmediate(firstHudObject);
                Object.DestroyImmediate(secondHudObject);
            }
        }

        [Test]
        public void ProductionHud_DoesNotCollectNormalMonsterTelegraphs()
        {
            var hudObject = new GameObject("ProductionHudTelegraph_QA");
            var monsterObject = new GameObject("NormalMonsterTelegraph_QA");
            try
            {
                var hud = hudObject.AddComponent<ProductionMainHUD>();
                var monster = monsterObject.AddComponent<Monster>();
                var telegraph = new Monster.AttackTelegraph(monster, monster.ActionGeneration,
                    Time.time, Time.time + 1.5f, Time.time + 1.7f);
                Invoke(hud, "OnAttackTelegraphStarted", telegraph);
                Assert.IsFalse((bool)GetField(hud, "hasBossAttackTelegraph"));
                string source = File.ReadAllText("Assets/Scripts/UI/ProductionMainHUD.cs");
                StringAssert.DoesNotContain("Dictionary<Monster", source);
                StringAssert.DoesNotContain("Monster.ActiveMonsters", source.Substring(
                    source.IndexOf("private void Update"), source.IndexOf("public void BindSceneState") - source.IndexOf("private void Update")));
            }
            finally
            {
                Object.DestroyImmediate(monsterObject);
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

        [Test]
        public void Unit3101To3106_OverheadTelegraphReferencesAreSerialized()
        {
            for (uint unitIdx = 3101; unitIdx <= 3106; unitIdx++)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/Prefabs/Unit_{unitIdx}.prefab");
                Assert.NotNull(prefab, $"Unit_{unitIdx}");
                var hud = prefab.GetComponentInChildren<MonsterOverheadHUD>(true);
                Assert.NotNull(hud, $"Unit_{unitIdx}");
                var serialized = new SerializedObject(hud);
                Assert.NotNull(serialized.FindProperty("attackTelegraphGroup").objectReferenceValue, $"Unit_{unitIdx}");
                var fill = serialized.FindProperty("attackTelegraphFill").objectReferenceValue as Image;
                Assert.NotNull(fill, $"Unit_{unitIdx}");
                Assert.AreEqual("Sprite_UI_SolidFill", fill.sprite.name, $"Unit_{unitIdx}");
                Assert.IsFalse(fill.raycastTarget, $"Unit_{unitIdx}");

                var instance = Object.Instantiate(prefab);
                try
                {
                    var instanceHud = instance.GetComponentInChildren<MonsterOverheadHUD>(true);
                    var instanceSerialized = new SerializedObject(instanceHud);
                    foreach (string fieldName in new[] { "hpFill", "postureFill" })
                    {
                        var bar = instanceSerialized.FindProperty(fieldName).objectReferenceValue as Image;
                        Assert.NotNull(bar, $"Unit_{unitIdx}/{fieldName}");
                        int imageCount = instanceHud.GetComponentsInChildren<Image>(true).Length;
                        Invoke(instanceHud, "EnsureFillBackground", bar);
                        Invoke(instanceHud, "EnsureFillBackground", bar);
                        Assert.AreEqual(imageCount, instanceHud.GetComponentsInChildren<Image>(true).Length,
                            $"Unit_{unitIdx}/{fieldName} repeated setup must not create objects.");
                        var background = bar.transform.parent.GetChild(bar.transform.GetSiblingIndex() - 1)
                            .GetComponent<Image>();
                        Assert.NotNull(background, $"Unit_{unitIdx}/{fieldName} background");
                        Assert.AreSame(bar.sprite, background.sprite);
                        Assert.AreSame(bar.material, background.material);
                        Assert.AreEqual(new Color(0f, 0f, 0f, .9f), background.color);
                        Assert.AreEqual(bar.rectTransform.rect.size, background.rectTransform.rect.size);
                    }
                }
                finally { Object.DestroyImmediate(instance); }
            }
        }

        [Test]
        public void OverheadTelegraph_ReusesFillSpriteForBlackBackgroundBehindFill()
        {
            var root = new GameObject("TelegraphRoot_QA", typeof(RectTransform), typeof(CanvasGroup));
            var fillObject = new GameObject("Fill_QA", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            fillObject.transform.SetParent(root.transform, false);
            var hudObject = new GameObject("Hud_QA");
            try
            {
                var hud = hudObject.AddComponent<MonsterOverheadHUD>();
                var fill = fillObject.GetComponent<Image>();
                fill.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
                SetField(hud, "attackTelegraphGroup", root.GetComponent<CanvasGroup>());
                SetField(hud, "attackTelegraphFill", fill);
                Invoke(hud, "EnsureTelegraphBackground");

                var background = root.GetComponent<Image>();
                Assert.NotNull(background);
                Assert.AreSame(fill.sprite, background.sprite);
                Assert.AreEqual(new Color(0f, 0f, 0f, .9f), background.color);
                Assert.IsFalse(background.raycastTarget);
                Assert.AreEqual(root.transform.childCount - 1, fill.transform.GetSiblingIndex());
            }
            finally
            {
                Object.DestroyImmediate(hudObject);
                Object.DestroyImmediate(root);
            }
        }

        private static void SetField(object target, string name, object value) =>
            target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);

        private static object GetField(object target, string name) =>
            target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(target);

        private static object Invoke(object target, string name, params object[] args) =>
            target.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(target, args);
    }
}
