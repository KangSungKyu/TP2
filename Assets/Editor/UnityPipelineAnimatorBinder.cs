#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using System.IO;
using System.Collections.Generic;

/// <summary>
/// Unity 6 (6000.4.8f1) 2D Animation & VFX Pipeline 표준 유틸리티.
/// 메인 프로그래머의 최신 State Int 매핑 사양 (보스 가론 패턴 10~13번 할당) 반영.
/// </summary>
public static class UnityPipelineAnimatorBinder
{
    [MenuItem("TP2/Execute Unity Pipeline Full Animator Binding (유니티 CLI 표준 바인딩)")]
    public static void ExecuteFullPipelineBinding()
    {
        Debug.Log("<color=cyan><b>[UnityPipelineAnimatorBinder] Unity 6 CLI & Pipeline 애니메이터 및 VFX 이펙트 바인딩 시작...</b></color>");

        // 1. 플레이어 10종 애니메이션 클립 & PlayerAnimatorController 바인딩 (128x256px)
        BindPlayerPipeline();

        // 2. 철위병 가론 보스 8종 애니메이션 클립 & GaronAnimatorController 바인딩 (패턴 10~13번 최신 사양)
        BindGaronPipeline();

        // 3. 일반 몬스터 3종 애니메이션 클립 & Controllers 바인딩 (64x64px)
        BindMonsterPipeline();

        // 4. 실제 게임용 VFX 이펙트 11종 클립 & 슬라이싱 바인딩
        BindEffectsPipeline();

        // 5. Addressables 전수 자동 등록 및 로컬 배포
        AddressablePipeline.RegisterAllAddressables();
        AddressablePipeline.BuildAndDeploy();

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("<color=green><b>[UnityPipelineAnimatorBinder] 모든 Animator, AnimationClip & VFX 이펙트 정식 바인딩 완료!</b></color>");
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

            AnimationClip clip = CreateAndSaveAnimationClip(texPath, saveClipPath, animName, fps, loop, 128, 256, 128, SpriteAlignment.BottomCenter, new Vector2(0.5f, 0.0f));

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

        // 메인 프로그래머 최신 사양 (공용 1~8번, 특수 패턴 10~13번 할당)
        var garonMap = new Dictionary<int, (string animName, string texName, float fps, bool loop)>()
        {
            { 1, ("Garon_Idle", "Garon_Idle.png", 8f, true) },
            { 2, ("Garon_Move", "Garon_Move.png", 10f, true) },
            { 3, ("Garon_Jump", "Garon_Jump.png", 10f, false) },
            { 8, ("Garon_Death", "Garon_Death.png", 8f, false) },
            { 10, ("Garon_Pattern_OverheadSmash", "Garon_Pattern_OverheadSmash.png", 16f, false) },
            { 11, ("Garon_Pattern_ComboSlash", "Garon_Pattern_ComboSlash.png", 16f, false) },
            { 12, ("Garon_Pattern_Charge", "Garon_Pattern_Charge.png", 16f, false) },
            { 13, ("Garon_Pattern_Shockwave", "Garon_Pattern_Shockwave.png", 16f, false) }
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

            AnimationClip clip = CreateAndSaveAnimationClip(texPath, saveClipPath, animName, fps, loop, 256, 512, 128, SpriteAlignment.BottomCenter, new Vector2(0.5f, 0.0f));

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

        var monsterSpecs = new Dictionary<string, (int frameW, int frameH, int ppu)>()
        {
            { "ShadowStalker", (128, 256, 64) },
            { "SpearSentry", (154, 307, 77) },
            { "WaveHeavy", (205, 410, 102) }
        };

        var monsterMap = new Dictionary<int, (string actName, bool loop)>()
        {
            { 1, ("Idle", true) }, { 2, ("Move", true) }, { 3, ("Jump", false) }, { 7, ("Attack", false) }, { 8, ("Death", false) }
        };

        foreach (var specKvp in monsterSpecs)
        {
            string mName = specKvp.Key;
            var spec = specKvp.Value;

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

                AnimationClip clip = CreateAndSaveAnimationClip(texPath, saveClipPath, animName, 8f, loop, spec.frameW, spec.frameH, spec.ppu, SpriteAlignment.BottomCenter, new Vector2(0.5f, 0.0f), "Visual");

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

    private static void BindEffectsPipeline()
    {
        string animsDir = "Assets/Anims/Effects";
        if (!AssetDatabase.IsValidFolder("Assets/Anims")) AssetDatabase.CreateFolder("Assets", "Anims");
        if (!AssetDatabase.IsValidFolder(animsDir)) AssetDatabase.CreateFolder("Assets/Anims", "Effects");

        var effectSpecs = new List<(string texPath, string clipName, int frameW, int frameH, float fps, bool loop)>()
        {
            ("Assets/Textures/Effects/Player/Player_Attack_Hit1_Effect.png", "Player_Attack_Hit1_Effect", 128, 128, 8f, false),
            ("Assets/Textures/Effects/Player/Player_Attack_Hit2_Effect.png", "Player_Attack_Hit2_Effect", 128, 128, 8f, false),
            ("Assets/Textures/Effects/Player/Player_Attack_Hit3_Effect.png", "Player_Attack_Hit3_Effect", 160, 160, 8f, false),
            ("Assets/Textures/Effects/Bosses/Garon/Garon_ComboSlash_Effect.png", "Garon_ComboSlash_Effect", 256, 256, 8f, false),
            ("Assets/Textures/Effects/Bosses/Garon/Garon_OverheadSmash_Effect.png", "Garon_OverheadSmash_Effect", 256, 128, 8f, false),
            ("Assets/Textures/Effects/Bosses/Garon/Garon_Shockwave_Effect.png", "Garon_Shockwave_Effect", 128, 128, 8f, false),
            ("Assets/Textures/Effects/Bosses/Garon/Garon_Charge_Effect.png", "Garon_Charge_Effect", 256, 256, 8f, false),
            ("Assets/Textures/Effects/Placeholder_Parry.png", "Placeholder_Parry", 128, 128, 8f, false),
            ("Assets/Textures/Effects/Placeholder_Guard.png", "Placeholder_Guard", 128, 128, 8f, false),
            ("Assets/Textures/Effects/Placeholder_Dodge.png", "Placeholder_Dodge", 128, 128, 8f, false),
            ("Assets/Textures/Effects/Placeholder_Hit.png", "Placeholder_Hit", 128, 128, 8f, false)
        };

        foreach (var eff in effectSpecs)
        {
            if (File.Exists(eff.texPath))
            {
                string saveClipPath = $"{animsDir}/{eff.clipName}.anim";
                CreateAndSaveAnimationClip(eff.texPath, saveClipPath, eff.clipName, eff.fps, eff.loop, eff.frameW, eff.frameH, 128, SpriteAlignment.Center, new Vector2(0.5f, 0.5f));
            }
        }
    }

    private static AnimationClip CreateAndSaveAnimationClip(string texturePath, string saveClipPath, string clipName, float fps, bool isLooping, int frameWidth, int frameHeight, int ppu, SpriteAlignment alignment, Vector2 pivot, string bindingPath = "")
    {
        TextureImporter importer = AssetImporter.GetAtPath(texturePath) as TextureImporter;
        if (importer != null)
        {
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Multiple;
            importer.filterMode = FilterMode.Point;
            importer.spritePixelsPerUnit = ppu;

            Texture2D tex = AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);
            if (tex != null)
            {
                int cols = Mathf.Max(1, tex.width / frameWidth);
                List<SpriteMetaData> metaList = new List<SpriteMetaData>();
                for (int c = 0; c < cols; c++)
                {
                    SpriteMetaData meta = new SpriteMetaData();
                    meta.name = $"{clipName}_{c}";
                    meta.rect = new Rect(c * frameWidth, 0, frameWidth, frameHeight);
                    meta.alignment = (int)alignment;
                    meta.pivot = pivot;
                    metaList.Add(meta);
                }
                importer.spritesheet = metaList.ToArray();
            }

            importer.SaveAndReimport();
            AssetDatabase.ImportAsset(texturePath, ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
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
        curveBinding.path = bindingPath;
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
