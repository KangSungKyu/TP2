#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using System.IO;

/// <summary>
/// 1스테이지 일반 몬스터 3종 (SpearSentry, ShadowStalker, WaveHeavy) 프리팹 패키징 및 Addressables 등록 빌더.
/// </summary>
public static class MonsterPrefabBuilder
{
    public struct MonsterSpec
    {
        public string name;
        public uint unitId;
        public Vector2 colliderSize;
        public Vector2 colliderOffset;
        public int frameWidth;
        public int frameHeight;
        public int ppu;
    }

    [MenuItem("TP2/Build Stage 1 Regular Monster Prefabs (1스테이지 일반 몬스터 3종 프리팹 빌드)")]
    public static void BuildMonsterPrefabs()
    {
        Debug.Log("<color=cyan><b>[MonsterPrefabBuilder] 1스테이지 일반 몬스터 3종 프리팹 빌드 시작...</b></color>");

        // 1. 애니메이터 바인딩 우선 실행
        UnityPipelineAnimatorBinder.ExecuteFullPipelineBinding();

        string prefabsDir = "Assets/Prefabs";
        string animsDir = "Assets/Anims/Monster";
        if (!Directory.Exists(prefabsDir)) Directory.CreateDirectory(prefabsDir);

        MonsterSpec[] monsterSpecs = new MonsterSpec[]
        {
            new MonsterSpec { name = "SpearSentry", unitId = 3101, colliderSize = new Vector2(1.5f, 3f), colliderOffset = new Vector2(0f, 1.5f), frameWidth = 154, frameHeight = 307, ppu = 77 },
            new MonsterSpec { name = "ShadowStalker", unitId = 3102, colliderSize = new Vector2(1.0f, 1.6f), colliderOffset = new Vector2(0f, 0.8f), frameWidth = 128, frameHeight = 256, ppu = 64 },
            new MonsterSpec { name = "WaveHeavy", unitId = 3103, colliderSize = new Vector2(1.6f, 2.0f), colliderOffset = new Vector2(0f, 1.0f), frameWidth = 205, frameHeight = 410, ppu = 102 }
        };

        foreach (var spec in monsterSpecs)
        {
            string unitName = $"Unit_{spec.unitId}";
            GameObject monsterObj = new GameObject(unitName);
            GameObject visual = new GameObject("Visual");
            visual.transform.SetParent(monsterObj.transform, false);

            var sr = visual.AddComponent<SpriteRenderer>();
            sr.sortingOrder = 10;
            string idleTexPath = $"Assets/Textures/Characters/Monsters/{spec.name}/{spec.name}_Idle.png";
            Sprite defaultSprite = AssetDatabase.LoadAssetAtPath<Sprite>(idleTexPath);
            if (defaultSprite == null)
            {
                var sprites = AssetDatabase.LoadAllAssetsAtPath(idleTexPath);
                foreach (var s in sprites)
                {
                    if (s is Sprite sp) { defaultSprite = sp; break; }
                }
            }
            if (defaultSprite != null) sr.sprite = defaultSprite;

            var boxCol = monsterObj.AddComponent<BoxCollider2D>();
            boxCol.size = spec.colliderSize;
            boxCol.offset = spec.colliderOffset;

            var rb2d = monsterObj.AddComponent<Rigidbody2D>();
            rb2d.bodyType = RigidbodyType2D.Kinematic;

            monsterObj.AddComponent<KinematicMotor2D>();
            monsterObj.AddComponent<CombatStats>();
            monsterObj.AddComponent<Monster>();

            var animator = visual.AddComponent<Animator>();
            string controllerPath = $"{animsDir}/{spec.name}AnimatorController.controller";
            RuntimeAnimatorController controller = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(controllerPath);
            if (controller != null)
            {
                animator.runtimeAnimatorController = controller;
            }

            string prefabPath = $"{prefabsDir}/{unitName}.prefab";
            PrefabUtility.SaveAsPrefabAsset(monsterObj, prefabPath);
            Object.DestroyImmediate(monsterObj);

            Debug.Log($"<color=green>[MonsterPrefabBuilder] 몬스터 프리팹 생성 완료: {prefabPath}</color>");
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        AddressablePipeline.BuildAndDeploy();
        Debug.Log("<color=green><b>[MonsterPrefabBuilder] 1스테이지 일반 몬스터 3종 프리팹 및 Addressables 배포 완결!</b></color>");
    }
}
#endif
