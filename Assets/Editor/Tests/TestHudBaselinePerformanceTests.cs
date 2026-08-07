using System;
using System.Collections;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Tests.PlayMode
{
    public class TestHudBaselinePerformanceTests
    {
        private const int WarmupFrames = 30;
        private const int SampleFrames = 300;
        private static readonly Action<CombatStats, float> SetHp = CreateSetter("CurrentHp");
        private static readonly Action<CombatStats, float> SetMp = CreateSetter("CurrentMp");
        private static readonly Action<CombatStats, float> SetPosture = CreateSetter("CurrentPosture");

        [UnityTest]
        public IEnumerator ProductionHUD_Phase1BaselineThresholds_ThreeScenarios_300FramesEach()
        {
            yield return new EnterPlayMode();
            Screen.SetResolution(996, 560, false);
            var cameraObject = new GameObject("Main Camera") { tag = "MainCamera" };
            var camera = cameraObject.AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = 9f;
            cameraObject.transform.position = new Vector3(0f, 4.5f, -10f);

            TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Fonts/BMJUA_UI.asset");
            Material material = AssetDatabase.LoadAssetAtPath<Material>("Assets/Fonts/BMJUA_UI_Shared.mat");
            var canvasObject = new GameObject("MainHUDRoot");
            canvasObject.AddComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            var hud = canvasObject.AddComponent<ProductionMainHUD>();
            ConfigureHud(hud, canvasObject.transform, font, material);

            var playerObject = CreateUnit<Player>("Player", new Vector3(-4f, 0f));
            var playerStats = playerObject.GetComponent<CombatStats>();
            var monsters = new GameObject[4];
            for (int i = 0; i < monsters.Length; i++)
                monsters[i] = CreateUnit<Monster>($"Monster_{i + 1}", new Vector3(-3f + i * 2f, 1f));
            var bossObject = CreateUnit<BossMonster>("Garon", new Vector3(4f, 1f));

            try
            {
                SetActive(monsters, false);
                bossObject.SetActive(false);
                hud.BindSceneState();
                yield return Measure("Player_HP_Posture_MP_Changing", 62.01, 62.01, 62.01, () =>
                {
                    float value = 1f + Time.frameCount % 99;
                    SetHp(playerStats, value);
                    SetMp(playerStats, value * 0.5f);
                    SetPosture(playerStats, value);
                    playerStats.OnHpChanged.Invoke(value / playerStats.MaxHp);
                    playerStats.OnMpChanged.Invoke(value * 0.5f / playerStats.MaxMp);
                    playerStats.OnPostureChanged.Invoke(value / playerStats.MaxPosture);
                });

                SetActive(monsters, true);
                yield return Measure("Four_Normal_Monsters", 72.27, 72.27, 72.27, null);

                bossObject.SetActive(true);
                yield return Measure("Garon_Prompt_Warning_Maximum", 62.00, 62.00, 62.00, null);

                string source = File.ReadAllText("Assets/Scripts/UI/ProductionMainHUD.cs");
                Assert.IsFalse(System.Text.RegularExpressions.Regex.IsMatch(source, @"\b(Update|LateUpdate|OnGUI)\s*\("),
                    "Production HUD must not allocate per-frame strings.");
                StringAssert.Contains("stageProgressText.SetText(\"{0}  {1}/{2}\"", source);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(cameraObject);
                UnityEngine.Object.DestroyImmediate(canvasObject);
                UnityEngine.Object.DestroyImmediate(playerObject);
                foreach (var monster in monsters) UnityEngine.Object.DestroyImmediate(monster);
                UnityEngine.Object.DestroyImmediate(bossObject);
            }
            yield return new ExitPlayMode();
        }

        private static IEnumerator Measure(string scenario, double baselineDraw, double baselineBatches,
            double baselineSetPass, Action perFrame)
        {
            for (int i = 0; i < WarmupFrames; i++) yield return null;
            using var canvasBuild = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Canvas.BuildBatch");
            using var gcAlloc = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC Allocated In Frame");
            var stats = new[] { new Stats(), new Stats(), new Stats(), new Stats(), new Stats() };
            for (int i = 0; i < SampleFrames; i++)
            {
                perFrame?.Invoke();
                yield return null;
                stats[0].Add(UnityStats.drawCalls);
                stats[1].Add(UnityStats.drawCalls);
                stats[2].Add(UnityStats.setPassCalls);
                stats[3].Add(canvasBuild.LastValue);
                stats[4].Add(gcAlloc.LastValue);
            }

            TestContext.WriteLine($"PRODUCTION|{scenario}|Resolution={Screen.width}x{Screen.height}|Frames={SampleFrames}|" +
                $"DrawCalls={stats[0]}|Batches={stats[1]}|SetPass={stats[2]}|" +
                $"Canvas.BuildBatch(ns)={stats[3]}|GC.Alloc(bytes)={stats[4]}");
            Assert.LessOrEqual(stats[0].Average, baselineDraw + 5d);
            Assert.LessOrEqual(stats[1].Average, baselineBatches + 5d);
            Assert.LessOrEqual(stats[2].Average, baselineSetPass + 2d);
        }

        private static void ConfigureHud(ProductionMainHUD hud, Transform parent, TMP_FontAsset font, Material material)
        {
            SetField(hud, "playerHpFill", CreateImage("PlayerHp", parent));
            SetField(hud, "playerPostureFill", CreateImage("PlayerPosture", parent));
            SetField(hud, "playerMpFill", CreateImage("PlayerMp", parent));
            SetField(hud, "monsterGroup", CreateGroup("MonsterGroup", parent));
            SetField(hud, "monsterHpFill", CreateImage("MonsterHp", parent));
            SetField(hud, "monsterPostureFill", CreateImage("MonsterPosture", parent));
            SetField(hud, "bossGroup", CreateGroup("BossGroup", parent));
            SetField(hud, "bossHpFill", CreateImage("BossHp", parent));
            SetField(hud, "bossPostureFill", CreateImage("BossPosture", parent));
            SetField(hud, "bossNameText", CreateText("BossName", parent, font, material));
            SetField(hud, "stageProgressText", CreateText("StageProgress", parent, font, material));

            var alertObject = new GameObject("AlertMessage");
            alertObject.transform.SetParent(parent, false);
            var alertGroup = alertObject.AddComponent<CanvasGroup>();
            var alertText = CreateText("AlertText", alertObject.transform, font, material);
            var alert = alertObject.AddComponent<AlertMessage>();
            SetField(alert, "messageText", alertText);
            SetField(alert, "canvasGroup", alertGroup);
            SetField(hud, "alertMessage", alert);
        }

        private static Image CreateImage(string name, Transform parent)
        {
            var obj = new GameObject(name);
            obj.transform.SetParent(parent, false);
            return obj.AddComponent<Image>();
        }

        private static CanvasGroup CreateGroup(string name, Transform parent)
        {
            var obj = new GameObject(name);
            obj.transform.SetParent(parent, false);
            return obj.AddComponent<CanvasGroup>();
        }

        private static TextMeshProUGUI CreateText(string name, Transform parent, TMP_FontAsset font, Material material)
        {
            var obj = new GameObject(name);
            obj.transform.SetParent(parent, false);
            var text = obj.AddComponent<TextMeshProUGUI>();
            text.font = font;
            text.fontSharedMaterial = material;
            return text;
        }

        private static GameObject CreateUnit<T>(string name, Vector3 position) where T : UnitBase
        {
            var unit = new GameObject(name);
            unit.transform.position = position;
            unit.AddComponent<Rigidbody2D>().bodyType = RigidbodyType2D.Kinematic;
            unit.AddComponent<CapsuleCollider2D>().isTrigger = true;
            var stats = unit.AddComponent<CombatStats>();
            stats.OnHpChanged = new UnityEvent<float>();
            stats.OnMpChanged = new UnityEvent<float>();
            stats.OnPostureChanged = new UnityEvent<float>();
            stats.OnParrySuccess = new UnityEvent();
            stats.OnGroggyState = new UnityEvent();
            stats.OnGroggyEnded = new UnityEvent();
            stats.OnDeath = new UnityEvent();
            stats.OnHpZero = new UnityEvent();
            stats.InitStats();
            unit.AddComponent<T>();
            return unit;
        }

        private static void SetActive(GameObject[] objects, bool active)
        {
            foreach (var obj in objects) obj.SetActive(active);
        }

        private static void SetField(object target, string name, object value) => target.GetType()
            .GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);

        private static Action<CombatStats, float> CreateSetter(string propertyName) =>
            (Action<CombatStats, float>)typeof(CombatStats).GetProperty(propertyName)
                .GetSetMethod(true).CreateDelegate(typeof(Action<CombatStats, float>));

        private struct Stats
        {
            private long min;
            private long max;
            private long sum;
            private int count;
            public double Average => count == 0 ? 0d : sum / (double)count;
            public void Add(long value)
            {
                if (count == 0 || value < min) min = value;
                if (count == 0 || value > max) max = value;
                sum += value;
                count++;
            }
            public override string ToString() => count == 0 ? "N/A" : $"avg={Average:F2},min={min},max={max}";
        }
    }
}
