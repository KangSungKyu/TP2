#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using System.IO;
using System.Collections.Generic;

/// <summary>
/// 11종 이펙트 및 몬스터 3종에 대한 .anim 클립 생성, .controller 파이프라인 바인딩,
/// .prefab 컴포넌트 조립 및 Addressables 배포를 원스톱으로 총괄하는 파이프라인 유틸리티.
/// </summary>
public static class EffectPrefabBuilder
{
    [MenuItem("TP2/Build All Effect Prefabs (VFX 이펙트 프리팹 및 몬스터 컨트롤러 일괄 패키징)")]
    public static void BuildAllEffectPrefabs()
    {
        Debug.Log("<color=cyan><b>[EffectPrefabBuilder] 11종 이펙트 .anim/.controller/.prefab 일괄 패키징 시작...</b></color>");

        string rootPrefabsDir = "Assets/prefabs";
        string subEffectsDir = "Assets/prefabs/Effects";
        string animsMonsterDir = "Assets/Anims/Monster";
        string animsEffectsDir = "Assets/Anims/Effects";

        if (!Directory.Exists(rootPrefabsDir)) Directory.CreateDirectory(rootPrefabsDir);
        if (!Directory.Exists(subEffectsDir)) Directory.CreateDirectory(subEffectsDir);
        if (!Directory.Exists(animsMonsterDir)) Directory.CreateDirectory(animsMonsterDir);
        if (!Directory.Exists(animsEffectsDir)) Directory.CreateDirectory(animsEffectsDir);

        AssetDatabase.Refresh();

        // 1. 몬스터 3종 AnimatorController (SpearSentry, ShadowStalker, WaveHeavy) 정식 생성
        string[] monsters = new string[] { "SpearSentry", "ShadowStalker", "WaveHeavy" };
        var monsterMap = new Dictionary<int, string>()
        {
            { 1, "Idle" }, { 2, "Move" }, { 3, "Jump" }, { 7, "Attack" }, { 8, "Death" }
        };

        foreach (string mName in monsters)
        {
            string controllerPath = $"{animsMonsterDir}/{mName}AnimatorController.controller";
            AnimatorController controller = AnimatorController.CreateAnimatorControllerAtPath(controllerPath);
            controller.AddParameter("State", AnimatorControllerParameterType.Int);
            var stateMachine = controller.layers[0].stateMachine;

            foreach (var kvp in monsterMap)
            {
                int stateVal = kvp.Key;
                string actName = kvp.Value;
                string animName = $"{mName}_{actName}";
                string animClipPath = $"{animsMonsterDir}/{animName}.anim";

                AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(animClipPath);
                var state = stateMachine.AddState(animName);
                if (clip != null)
                {
                    state.motion = clip;
                }

                if (stateVal == 1)
                {
                    stateMachine.defaultState = state;
                }

                var trans = stateMachine.AddAnyStateTransition(state);
                trans.AddCondition(AnimatorConditionMode.Equals, stateVal, "State");
                trans.hasExitTime = false;
                trans.duration = 0.1f;
                trans.canTransitionToSelf = false;
            }

            EditorUtility.SetDirty(controller);
        }

        // 2. 11종 VFX 이펙트 텍스처 -> .anim -> .controller -> .prefab 일괄 생성 및 바인딩
        var effectSpecs = new List<(string key, string texPath, int frameW, int frameH, float fps, bool loop)>()
        {
            ("Placeholder_Parry", "Assets/Textures/Effects/Placeholder_Parry.png", 128, 128, 8f, false),
            ("Placeholder_Guard", "Assets/Textures/Effects/Placeholder_Guard.png", 128, 128, 8f, false),
            ("Placeholder_Dodge", "Assets/Textures/Effects/Placeholder_Dodge.png", 128, 128, 8f, false),
            ("Placeholder_Hit", "Assets/Textures/Effects/Placeholder_Hit.png", 128, 128, 8f, false),
            ("Player_Attack_Hit1_Effect", "Assets/Textures/Effects/Player/Player_Attack_Hit1_Effect.png", 128, 128, 8f, false),
            ("Player_Attack_Hit2_Effect", "Assets/Textures/Effects/Player/Player_Attack_Hit2_Effect.png", 128, 128, 8f, false),
            ("Player_Attack_Hit3_Effect", "Assets/Textures/Effects/Player/Player_Attack_Hit3_Effect.png", 160, 160, 8f, false),
            ("Garon_ComboSlash_Effect", "Assets/Textures/Effects/Bosses/Garon/Garon_ComboSlash_Effect.png", 256, 256, 8f, false),
            ("Garon_OverheadSmash_Effect", "Assets/Textures/Effects/Bosses/Garon/Garon_OverheadSmash_Effect.png", 256, 128, 8f, false),
            ("Garon_Shockwave_Effect", "Assets/Textures/Effects/Bosses/Garon/Garon_Shockwave_Effect.png", 128, 128, 8f, false),
            ("Garon_Charge_Effect", "Assets/Textures/Effects/Bosses/Garon/Garon_Charge_Effect.png", 256, 256, 8f, false)
        };

        foreach (var eff in effectSpecs)
        {
            string key = eff.key;
            string saveClipPath = $"{animsEffectsDir}/{key}.anim";

            // 2-1. .anim 애니메이션 클립 생성/확인
            AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(saveClipPath);
            if (clip == null && File.Exists(eff.texPath))
            {
                clip = createAndSaveAnimationClip(eff.texPath, saveClipPath, key, eff.fps, eff.loop, eff.frameW, eff.frameH);
            }

            // 2-2. .controller 생성 및 Default State 설정
            string controllerPath = $"{animsEffectsDir}/{key}_Controller.controller";
            AnimatorController controller = AnimatorController.CreateAnimatorControllerAtPath(controllerPath);
            if (clip != null)
            {
                var defaultState = controller.layers[0].stateMachine.AddState("Default");
                defaultState.motion = clip;
                controller.layers[0].stateMachine.defaultState = defaultState;
            }
            EditorUtility.SetDirty(controller);

            // 2-3. GameObject 구성 및 Animator.runtimeAnimatorController 1:1 바인딩
            GameObject go = new GameObject(key);
            var sr = go.AddComponent<SpriteRenderer>();
            var animator = go.AddComponent<Animator>();
            animator.runtimeAnimatorController = controller;

            // 2-4. 루트 및 서브 디렉토리에 .prefab 저장
            string rootPrefabPath = $"{rootPrefabsDir}/{key}.prefab";
            string subPrefabPath = $"{subEffectsDir}/{key}.prefab";

            PrefabUtility.SaveAsPrefabAsset(go, rootPrefabPath);
            PrefabUtility.SaveAsPrefabAsset(go, subPrefabPath);

            Object.DestroyImmediate(go);
            Debug.Log($"<color=green>[EffectPrefabBuilder] 이펙트 패키징 완료: {key} (Controller: {controllerPath})</color>");
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        // 3. Addressables 전수 자동 등록 및 로컬 서버 동기화
        AddressablePipeline.BuildAndDeploy();

        Debug.Log("<color=cyan><b>[EffectPrefabBuilder] 11종 이펙트 .anim/.controller/.prefab 일괄 조립 및 로컬 배포 완성!</b></color>");
    }

    private static AnimationClip createAndSaveAnimationClip(string texturePath, string saveClipPath, string clipName, float fps, bool isLooping, int frameWidth, int frameHeight)
    {
        TextureImporter importer = AssetImporter.GetAtPath(texturePath) as TextureImporter;
        if (importer != null)
        {
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Multiple;
            importer.filterMode = FilterMode.Point;
            importer.spritePixelsPerUnit = 128;

            Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);
            if (texture != null)
            {
                int columns = Mathf.Max(1, texture.width / frameWidth);
                int rows = Mathf.Max(1, texture.height / frameHeight);
                List<SpriteMetaData> metaList = new List<SpriteMetaData>();

                int spriteIndex = 0;
                for (int r = rows - 1; r >= 0; r--)
                {
                    for (int c = 0; c < columns; c++)
                    {
                        SpriteMetaData meta = new SpriteMetaData();
                        meta.rect = new Rect(c * frameWidth, r * frameHeight, frameWidth, frameHeight);
                        meta.name = $"{clipName}_{spriteIndex++}";
                        meta.alignment = (int)SpriteAlignment.Center;
                        meta.pivot = new Vector2(0.5f, 0.5f);
                        metaList.Add(meta);
                    }
                }

                importer.spritesheet = metaList.ToArray();
                EditorUtility.SetDirty(importer);
                importer.SaveAndReimport();
            }
        }

        Object[] assets = AssetDatabase.LoadAllAssetsAtPath(texturePath);
        List<Sprite> sprites = new List<Sprite>();
        foreach (var asset in assets)
        {
            if (asset is Sprite s) sprites.Add(s);
        }

        if (sprites.Count == 0) return null;

        AnimationClip clip = new AnimationClip();
        clip.name = clipName;
        clip.frameRate = fps;

        EditorCurveBinding binding = new EditorCurveBinding();
        binding.type = typeof(SpriteRenderer);
        binding.path = "";
        binding.propertyName = "m_Sprite";

        ObjectReferenceKeyframe[] keys = new ObjectReferenceKeyframe[sprites.Count];
        for (int i = 0; i < sprites.Count; i++)
        {
            keys[i] = new ObjectReferenceKeyframe();
            keys[i].time = i / fps;
            keys[i].value = sprites[i];
        }

        AnimationUtility.SetObjectReferenceCurve(clip, binding, keys);

        if (isLooping)
        {
            AnimationClipSettings settings = AnimationUtility.GetAnimationClipSettings(clip);
            settings.loopTime = true;
            AnimationUtility.SetAnimationClipSettings(clip, settings);
        }

        AssetDatabase.CreateAsset(clip, saveClipPath);
        return clip;
    }
}
#endif
