#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using System.IO;
using System.Collections.Generic;

/// <summary>
/// Unity 6 (6000.4.8f1) 2D Animation Pipeline 표준 유틸리티.
/// 유니티 C# TextureImporter.spritesheet API를 직접 사용하여 Sprite Editor 내 슬라이스 데이터를 100% 무결점으로 재정합 및 Reimport 해줍니다.
/// </summary>
public static class UnityPipelineAnimatorBinder
{
    [MenuItem("TP2/Execute Unity Pipeline Full Animator Binding (유니티 CLI 표준 바인딩)")]
    public static void ExecuteFullPipelineBinding()
    {
        Debug.Log("<color=cyan><b>[UnityPipelineAnimatorBinder] Unity 6 CLI & Pipeline 애니메이터 정식 바인딩 및 Sprite Slicing 시작...</b></color>");

        // 1. 플레이어 10종 애니메이션 클립 & PlayerAnimatorController 바인딩 (128x256px)
        BindPlayerPipeline();

        // 2. 철위병 가론 보스 8종 애니메이션 클립 & GaronAnimatorController 바인딩 (256x512px)
        BindGaronPipeline();

        // 3. 일반 몬스터 3종 애니메이션 클립 & Controllers 바인딩 (64x64px)
        BindMonsterPipeline();

        // 4. Addressables 전수 자동 등록 및 로컬 배포
        AddressablePipeline.RegisterAllAddressables();
        AddressablePipeline.BuildAndDeploy();

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("<color=green><b>[UnityPipelineAnimatorBinder] 모든 Sprite Slicing, Animator & AnimationClip 유니티 엔진 정식 바인딩 완료!</b></color>");
    }

    private static void BindPlayerPipeline()
    {
        string animsDir = "Assets/Anims/Player";
        if (!AssetDatabase.IsValidFolder(animsDir))
        {
            AssetDatabase.CreateFolder("Assets/Anims", "Player");
        }

        string controllerPath = $"{animsDir}/PlayerAnimatorController.controller";
        AnimatorController controller = AnimatorController.CreateAnimatorControllerAtPath(controllerPath);
        EnsureIntParameter(controller, "State");
        var stateMachine = controller.layers[0].stateMachine;

        var playerMap = new Dictionary<int, (string animName, string texName, float fps, bool loop)>()
        {
            { 1, ("Player_Idle", "Player_Idle.png", 12f, true) },
            { 2, ("Player_Run", "Player_Run.png", 12f, true) },
            { 3, ("Player_Jump", "Player_Jump.png", 16f, false) },
            { 4, ("Player_Parry", "Player_Parry.png", 24f, false) },
            { 5, ("Player_Guard", "Player_Guard.png", 12f, true) },
            { 6, ("Player_Dodge", "Player_Dodge.png", 20f, false) },
            { 7, ("Player_Attack_Hit1", "Player_Attack_Hit1.png", 24f, false) },
            { 8, ("Player_Execution", "Player_Execution.png", 16f, false) },
            { 9, ("Player_Attack_Hit2", "Player_Attack_Hit2.png", 24f, false) },
            { 10, ("Player_Attack_Hit3", "Player_Attack_Hit3.png", 20f, false) }
        };

        string playerTexDir = "Assets/Textures/Characters/Player";

        foreach (var kvp in playerMap)
        {
            int stateVal = kvp.Key;
            string animName = kvp.Value.animName;
            string texName = kvp.Value.texName;
            float fps = kvp.Value.fps;
            bool loop = kvp.Value.loop;

            string texPath = $"{playerTexDir}/{texName}";
            string saveClipPath = $"{animsDir}/{animName}.anim";

            // Player Frame Size: 128 x 256 px
            AnimationClip clip = CreateAndSaveAnimationClip(texPath, saveClipPath, animName, fps, loop, 128, 256);

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

    private static void BindGaronPipeline()
    {
        string animsDir = "Assets/Anims/Monster";
        if (!AssetDatabase.IsValidFolder(animsDir))
        {
            AssetDatabase.CreateFolder("Assets/Anims", "Monster");
        }

        string controllerPath = $"{animsDir}/GaronAnimatorController.controller";
        AnimatorController controller = AnimatorController.CreateAnimatorControllerAtPath(controllerPath);
        EnsureIntParameter(controller, "State");
        var stateMachine = controller.layers[0].stateMachine;

        var garonMap = new Dictionary<int, (string animName, string texName, float fps, bool loop)>()
        {
            { 1, ("Garon_Idle", "Garon_Idle.png", 8f, true) },
            { 2, ("Garon_Move", "Garon_Move.png", 10f, true) },
            { 3, ("Garon_Jump", "Garon_Jump.png", 10f, false) },
            { 4, ("Garon_Pattern_OverheadSmash", "Garon_Pattern_OverheadSmash.png", 16f, false) },
            { 5, ("Garon_Pattern_ComboSlash", "Garon_Pattern_ComboSlash.png", 16f, false) },
            { 6, ("Garon_Pattern_Charge", "Garon_Pattern_Charge.png", 16f, false) },
            { 7, ("Garon_Pattern_Shockwave", "Garon_Pattern_Shockwave.png", 16f, false) },
            { 8, ("Garon_Death", "Garon_Death.png", 8f, false) }
        };

        string garonTexDir = "Assets/Textures/Characters/Bosses/Garon";

        foreach (var kvp in garonMap)
        {
            int stateVal = kvp.Key;
            string animName = kvp.Value.animName;
            string texName = kvp.Value.texName;
            float fps = kvp.Value.fps;
            bool loop = kvp.Value.loop;

            string texPath = $"{garonTexDir}/{texName}";
            string saveClipPath = $"{animsDir}/{animName}.anim";

            // Boss Garon Frame Size: 256 x 512 px
            AnimationClip clip = CreateAndSaveAnimationClip(texPath, saveClipPath, animName, fps, loop, 256, 512);

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

    private static void BindMonsterPipeline()
    {
        string animsDir = "Assets/Anims/Monster";
        string monsterTexDir = "Assets/Textures/Characters/Monsters";
        string[] monsters = new string[] { "SpearSentry", "ShadowStalker", "WaveHeavy" };
        var monsterMap = new Dictionary<int, (string actName, bool loop)>()
        {
            { 1, ("Idle", true) }, { 2, ("Move", true) }, { 3, ("Jump", false) }, { 7, ("Attack", false) }, { 8, ("Death", false) }
        };

        foreach (string mName in monsters)
        {
            string controllerPath = $"{animsDir}/{mName}AnimatorController.controller";
            AnimatorController controller = AnimatorController.CreateAnimatorControllerAtPath(controllerPath);
            EnsureIntParameter(controller, "State");
            var stateMachine = controller.layers[0].stateMachine;

            foreach (var kvp in monsterMap)
            {
                int stateVal = kvp.Key;
                string actName = kvp.Value.actName;
                bool loop = kvp.Value.loop;
                string animName = $"{mName}_{actName}";
                string texPath = $"{monsterTexDir}/{mName}/{animName}.png";
                string saveClipPath = $"{animsDir}/{animName}.anim";

                // Monster Frame Size: 64 x 64 px
                AnimationClip clip = CreateAndSaveAnimationClip(texPath, saveClipPath, animName, 8f, loop, 64, 64);

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
    }

    private static AnimationClip CreateAndSaveAnimationClip(string texturePath, string saveClipPath, string clipName, float fps, bool isLooping, int frameWidth, int frameHeight)
    {
        TextureImporter importer = AssetImporter.GetAtPath(texturePath) as TextureImporter;
        if (importer != null)
        {
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Multiple;
            importer.filterMode = FilterMode.Point;
            importer.spritePixelsPerUnit = 128;

            // Texture 크기 정보 로드
            Texture2D tex = AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);
            if (tex != null)
            {
                int cols = tex.width / frameWidth;
                if (cols > 0)
                {
                    List<SpriteMetaData> metaList = new List<SpriteMetaData>();
                    for (int i = 0; i < cols; i++)
                    {
                        SpriteMetaData meta = new SpriteMetaData();
                        meta.name = $"{clipName}_{i}";
                        meta.rect = new Rect(i * frameWidth, 0, frameWidth, frameHeight);
                        meta.alignment = (int)SpriteAlignment.BottomCenter;
                        meta.pivot = new Vector2(0.5f, 0.0f);
                        metaList.Add(meta);
                    }
                    importer.spritesheet = metaList.ToArray();
                }
            }

            importer.SaveAndReimport();
        }

        Object[] assets = AssetDatabase.LoadAllAssetsAtPath(texturePath);
        List<Sprite> sprites = new List<Sprite>();
        if (assets != null)
        {
            foreach (var obj in assets)
            {
                if (obj is Sprite s)
                {
                    sprites.Add(s);
                }
            }
        }

        if (sprites.Count == 0)
        {
            Sprite single = AssetDatabase.LoadAssetAtPath<Sprite>(texturePath);
            if (single != null) sprites.Add(single);
        }

        if (sprites.Count == 0)
        {
            Debug.LogWarning($"[UnityPipelineAnimatorBinder] 스프라이트를 로드할 수 없습니다: {texturePath}");
            return null;
        }

        AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(saveClipPath);
        if (clip == null)
        {
            clip = new AnimationClip();
            AssetDatabase.CreateAsset(clip, saveClipPath);
        }

        clip.frameRate = fps;

        AnimationClipSettings clipSettings = AnimationUtility.GetAnimationClipSettings(clip);
        clipSettings.loopTime = isLooping;
        AnimationUtility.SetAnimationClipSettings(clip, clipSettings);

        EditorCurveBinding curveBinding = new EditorCurveBinding();
        curveBinding.type = typeof(SpriteRenderer);
        curveBinding.path = "";
        curveBinding.propertyName = "m_Sprite";

        ObjectReferenceKeyframe[] keyframes = new ObjectReferenceKeyframe[sprites.Count];
        for (int i = 0; i < sprites.Count; i++)
        {
            keyframes[i] = new ObjectReferenceKeyframe();
            keyframes[i].time = i / fps;
            keyframes[i].value = sprites[i];
        }

        AnimationUtility.SetObjectReferenceCurve(clip, curveBinding, keyframes);
        EditorUtility.SetDirty(clip);
        return clip;
    }

    private static void EnsureIntParameter(AnimatorController controller, string paramName)
    {
        foreach (var param in controller.parameters)
        {
            if (param.name == paramName && param.type == AnimatorControllerParameterType.Int)
            {
                return;
            }
        }
        controller.AddParameter(paramName, AnimatorControllerParameterType.Int);
    }
}
#endif
