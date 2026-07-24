#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using System.IO;

/// <summary>
/// 아트 디자이너로부터 수신된 원본 텍스처 및 스프라이트시트를 바탕으로 
/// 애니메이션 클립(.anim), AnimatorController, 프리팹을 순차 가공하고 
/// Addressable 규격에 맞게 등록을 완료하는 에디터 가공 프로세서.
/// </summary>
public static class AnimationAssetProcessor
{
    [MenuItem("TP2/Process All Art Assets (아트 리소스 순차 가공 및 프리팹/Addressable 구축)")]
    public static void ProcessAllArtAssets()
    {
        Debug.Log("<color=yellow><b>[AnimationAssetProcessor] 아트 디자이너 리소스 순차 가공 시작...</b></color>");

        // 1. 보스 (철위병 가론) 애니메이션 클립 & AnimatorController 가공
        ProcessGaronAssets();

        // 2. 일반 몬스터 3종 애니메이션 클립 & AnimatorController 가공
        ProcessMonsterAssets();

        // 2-1. Animator State 파라미터 기반 Transition (조건) 완벽 복구
        AnimatorTransitionBuilder.RebuildAllAnimatorTransitions();

        // 3. Addressable 규격 맞춤 자동 등록
        AddressableAutoRegister.RegisterAllAddressables();

        // 4. Addressables 번들 빌드 및 로컬 서버 배포 (C:\Users\PC\TP2LocalServer\ServerData)
        AddressablesDeployer.BuildAndDeploy();

        Debug.Log("<color=cyan><b>[AnimationAssetProcessor] 모든 아트 에셋 순차 가공, Addressables 등록 및 로컬 서버 배포 완료!</b></color>");
    }

    private static void ProcessGaronAssets()
    {
        string animsMonsterFolder = "Assets/Anims/Monster";
        if (!AssetDatabase.IsValidFolder(animsMonsterFolder))
        {
            AssetDatabase.CreateFolder("Assets/Anims", "Monster");
        }

        string garonTextureFolder = "Assets/Textures/Characters/Bosses/Garon";

        // Garon AnimatorController 생성
        string controllerPath = $"{animsMonsterFolder}/GaronAnimatorController.controller";
        AnimatorController controller = AnimatorController.CreateAnimatorControllerAtPath(controllerPath);
        controller.AddParameter("State", AnimatorControllerParameterType.Int);

        // 기본 애니메이션 클립들 생성
        string[] animNames = new string[] {
            "Garon_Idle", "Garon_Move", "Garon_Jump", "Garon_Death",
            "Garon_Pattern_Charge", "Garon_Pattern_ComboSlash", 
            "Garon_Pattern_OverheadSmash", "Garon_Pattern_Shockwave"
        };

        var rootStateMachine = controller.layers[0].stateMachine;

        foreach (string animName in animNames)
        {
            string texPath = $"{garonTextureFolder}/{animName}.png";
            AnimationClip clip = CreateClipFromTexture(texPath, $"{animsMonsterFolder}/{animName}.anim");

            if (clip != null)
            {
                var state = rootStateMachine.AddState(animName);
                state.motion = clip;

                // State 변수에 대응되는 파라미터 트랜지션 설정 (예: Idle=1, Move=2 등)
                if (animName == "Garon_Idle")
                {
                    rootStateMachine.defaultState = state;
                }
            }
        }

        EditorUtility.SetDirty(controller);
        AssetDatabase.SaveAssets();
        Debug.Log($"[AnimationAssetProcessor] Garon AnimatorController 가공 완료: {controllerPath}");

        // Garon.prefab 가공/생성 (Root + Visual Separation)
        BuildUnitPrefab("Garon", "Assets/prefabs/Garon.prefab", "Assets/Textures/Characters/Bosses/Garon/Garon_Concept.png", controllerPath);
    }

    private static void ProcessMonsterAssets()
    {
        string animsMonsterFolder = "Assets/Anims/Monster";
        string monstersTextureFolder = "Assets/Textures/Characters/Monsters";

        string[] monsterNames = new string[] { "SpearSentry", "ShadowStalker", "WaveHeavy" };

        foreach (string mName in monsterNames)
        {
            string controllerPath = $"{animsMonsterFolder}/{mName}AnimatorController.controller";
            AnimatorController controller = AnimatorController.CreateAnimatorControllerAtPath(controllerPath);
            controller.AddParameter("State", AnimatorControllerParameterType.Int);

            string[] animTypes = new string[] { "Idle", "Move", "Jump", "Attack", "Death" };
            var rootStateMachine = controller.layers[0].stateMachine;

            foreach (string aType in animTypes)
            {
                string animName = $"{mName}_{aType}";
                string texPath = $"{monstersTextureFolder}/{mName}/{animName}.png";
                AnimationClip clip = CreateClipFromTexture(texPath, $"{animsMonsterFolder}/{animName}.anim");

                if (clip != null)
                {
                    var state = rootStateMachine.AddState(animName);
                    state.motion = clip;
                    if (aType == "Idle")
                    {
                        rootStateMachine.defaultState = state;
                    }
                }
            }

            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssets();
            Debug.Log($"[AnimationAssetProcessor] {mName} AnimatorController 가공 완료: {controllerPath}");

            // Prefab 가공/생성
            BuildUnitPrefab(mName, $"Assets/prefabs/{mName}.prefab", $"{monstersTextureFolder}/{mName}/{mName}_Idle.png", controllerPath);
        }
    }

    private static AnimationClip CreateClipFromTexture(string texturePath, string saveClipPath)
    {
        Object[] assets = AssetDatabase.LoadAllAssetsAtPath(texturePath);
        if (assets == null || assets.Length == 0)
        {
            // 백업: 단일 스프라이트 로드 시도
            Sprite singleSprite = AssetDatabase.LoadAssetAtPath<Sprite>(texturePath);
            if (singleSprite == null) return null;
            assets = new Object[] { singleSprite };
        }

        // Sprite 객체들 필터링
        System.Collections.Generic.List<Sprite> sprites = new System.Collections.Generic.List<Sprite>();
        foreach (var obj in assets)
        {
            if (obj is Sprite s)
            {
                sprites.Add(s);
            }
        }

        if (sprites.Count == 0) return null;

        AnimationClip clip = new AnimationClip();
        clip.frameRate = 12;

        EditorCurveBinding curveBinding = new EditorCurveBinding();
        curveBinding.type = typeof(SpriteRenderer);
        curveBinding.path = "";
        curveBinding.propertyName = "m_Sprite";

        ObjectReferenceKeyframe[] keyframes = new ObjectReferenceKeyframe[sprites.Count];
        for (int i = 0; i < sprites.Count; i++)
        {
            keyframes[i] = new ObjectReferenceKeyframe();
            keyframes[i].time = i / 12f;
            keyframes[i].value = sprites[i];
        }

        AnimationUtility.SetObjectReferenceCurve(clip, curveBinding, keyframes);
        AssetDatabase.CreateAsset(clip, saveClipPath);
        return clip;
    }

    private static void BuildUnitPrefab(string unitName, string prefabPath, string sampleSpritePath, string controllerPath)
    {
        // 1. Root GameObject (Y=0 발밑 피벗 중심점)
        GameObject root = new GameObject(unitName);
        root.transform.position = Vector3.zero;

        // 2. Visual Child GameObject (Visual 분리)
        GameObject visualObj = new GameObject("Visual");
        visualObj.transform.SetParent(root.transform, false);
        visualObj.transform.localPosition = new Vector3(0f, 0.6f, 0f);

        SpriteRenderer sr = visualObj.AddComponent<SpriteRenderer>();
        sr.sortingOrder = 10;
        Sprite sp = AssetDatabase.LoadAssetAtPath<Sprite>(sampleSpritePath);
        if (sp != null) sr.sprite = sp;

        Animator anim = visualObj.AddComponent<Animator>();
        RuntimeAnimatorController ctrl = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(controllerPath);
        if (ctrl != null) anim.runtimeAnimatorController = ctrl;

        // 3. Prefab 저장
        if (!AssetDatabase.IsValidFolder("Assets/prefabs"))
        {
            AssetDatabase.CreateFolder("Assets", "prefabs");
        }

        bool success;
        PrefabUtility.SaveAsPrefabAsset(root, prefabPath, out success);
        Object.DestroyImmediate(root);

        if (success)
        {
            Debug.Log($"[AnimationAssetProcessor] '{unitName}' 프리팹 가공 완료: {prefabPath}");
        }
    }
}
#endif
