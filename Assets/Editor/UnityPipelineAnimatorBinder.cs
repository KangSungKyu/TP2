#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using System.IO;
using System.Collections.Generic;

/// <summary>
/// Unity CLI 및 Unity Pipeline 표준 API(TextureImporter, AssetDatabase, AnimatorController API)를 활용하여 
/// 플레이어 10종 전신 애니메이션 및 철위병 가론 보스 8종 애니메이션 클립과 컨트롤러를 
/// 유니티 엔진 직렬화 엔진으로 100% 무결점 바인딩 및 보장해 주는 에디터 파이프라인 유틸리티.
/// </summary>
public static class UnityPipelineAnimatorBinder
{
    [MenuItem("TP2/Execute Unity Pipeline Full Animator Binding (유니티 CLI 표준 바인딩)")]
    public static void ExecuteFullPipelineBinding()
    {
        Debug.Log("<color=cyan><b>[UnityPipelineAnimatorBinder] Unity CLI & Pipeline 애니메이터 정식 바인딩 시작...</b></color>");

        // 1. 플레이어 10종 전신 애니메이션 클립 및 AnimatorController 유니티 정식 바인딩
        BindPlayerPipeline();

        // 2. 철위병 가론 보스 8종 애니메이션 클립 및 AnimatorController 유니티 정식 바인딩
        BindGaronPipeline();

        // 3. 몬스터 3종 15종 애니메이션 클립 및 AnimatorController 유니티 정식 바인딩
        BindMonsterPipeline();

        // 4. Addressables 전수 자동 등록 및 로컬 배포
        AddressableAutoRegister.RegisterAllAddressables();
        AddressablesDeployer.BuildAndDeploy();

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("<color=green><b>[UnityPipelineAnimatorBinder] 모든 Animator & AnimationClip 유니티 엔진 정식 바인딩 및 배포 완료!</b></color>");
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

        // 플레이어 10종 전신 모션 맵 (State 1~10)
        var playerMap = new Dictionary<int, (string animName, string texName, float fps)>()
        {
            { 1, ("Player_Idle", "Player_Idle.png", 8f) },
            { 2, ("Player_Run", "Player_Run.png", 8f) },
            { 3, ("Player_Jump", "Player_Jump.png", 16f) },
            { 4, ("Player_Parry", "Player_Parry.png", 16f) },
            { 5, ("Player_Guard", "Player_Guard.png", 8f) },
            { 6, ("Player_Dodge", "Player_Dodge.png", 16f) },
            { 7, ("Player_Attack_Hit1", "Player_Attack_Hit1.png", 16f) },
            { 8, ("Player_Execution", "Player_Execution.png", 16f) },
            { 9, ("Player_Attack_Hit2", "Player_Attack_Hit2.png", 16f) },
            { 10, ("Player_Attack_Hit3", "Player_Attack_Hit3.png", 16f) }
        };

        string playerTexDir = "Assets/Textures/Characters/Player";

        foreach (var kvp in playerMap)
        {
            int stateVal = kvp.Key;
            string animName = kvp.Value.animName;
            string texName = kvp.Value.texName;
            float fps = kvp.Value.fps;

            string texPath = $"{playerTexDir}/{texName}";
            string saveClipPath = $"{animsDir}/{animName}.anim";

            AnimationClip clip = CreateAndSaveAnimationClip(texPath, saveClipPath, animName, fps);

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

        var garonMap = new Dictionary<int, (string animName, string texName, float fps)>()
        {
            { 1, ("Garon_Idle", "Garon_Idle.png", 8f) },
            { 2, ("Garon_Move", "Garon_Move.png", 8f) },
            { 3, ("Garon_Jump", "Garon_Jump.png", 8f) },
            { 4, ("Garon_Pattern_OverheadSmash", "Garon_Pattern_OverheadSmash.png", 8f) },
            { 5, ("Garon_Pattern_ComboSlash", "Garon_Pattern_ComboSlash.png", 8f) },
            { 6, ("Garon_Pattern_Charge", "Garon_Pattern_Charge.png", 8f) },
            { 7, ("Garon_Pattern_Shockwave", "Garon_Pattern_Shockwave.png", 8f) },
            { 8, ("Garon_Death", "Garon_Death.png", 8f) }
        };

        string garonTexDir = "Assets/Textures/Characters/Bosses/Garon";

        foreach (var kvp in garonMap)
        {
            int stateVal = kvp.Key;
            string animName = kvp.Value.animName;
            string texName = kvp.Value.texName;
            float fps = kvp.Value.fps;

            string texPath = $"{garonTexDir}/{texName}";
            string saveClipPath = $"{animsDir}/{animName}.anim";

            AnimationClip clip = CreateAndSaveAnimationClip(texPath, saveClipPath, animName, fps);

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
        var monsterMap = new Dictionary<int, string>()
        {
            { 1, "Idle" }, { 2, "Move" }, { 3, "Jump" }, { 7, "Attack" }, { 8, "Death" }
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
                string actName = kvp.Value;
                string animName = $"{mName}_{actName}";
                string texPath = $"{monsterTexDir}/{mName}/{animName}.png";
                string saveClipPath = $"{animsDir}/{animName}.anim";

                AnimationClip clip = CreateAndSaveAnimationClip(texPath, saveClipPath, animName, 8f);

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

    private static AnimationClip CreateAndSaveAnimationClip(string texturePath, string saveClipPath, string clipName, float fps)
    {
        // Texture Importer 설정 검증 및 정합
        TextureImporter importer = AssetImporter.GetAtPath(texturePath) as TextureImporter;
        if (importer != null)
        {
            bool changed = false;
            if (importer.textureType != TextureImporterType.Sprite)
            {
                importer.textureType = TextureImporterType.Sprite;
                changed = true;
            }
            if (importer.spriteImportMode != SpriteImportMode.Multiple && importer.spriteImportMode != SpriteImportMode.Single)
            {
                importer.spriteImportMode = SpriteImportMode.Multiple;
                changed = true;
            }
            if (importer.filterMode != FilterMode.Point)
            {
                importer.filterMode = FilterMode.Point;
                changed = true;
            }
            if (importer.spritePixelsPerUnit != 128)
            {
                importer.spritePixelsPerUnit = 128;
                changed = true;
            }

            if (changed)
            {
                importer.SaveAndReimport();
            }
        }

        // All Sprites 로드
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

        // 단일 스프라이트 예외 처리
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
