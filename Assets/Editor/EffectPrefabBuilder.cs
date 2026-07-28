#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using System.IO;
using System.Collections.Generic;

/// <summary>
/// 대문자 P 표준 경로(Assets/Prefabs/ 및 Assets/Prefabs/Effects/)에 11종 이펙트 프리팹을 빌드하고 Addressables 전수 배포하는 유틸리티.
/// </summary>
public static class EffectPrefabBuilder
{
    [MenuItem("TP2/Build All Effect Prefabs (VFX 이펙트 프리팹 일괄 패키징)")]
    public static void BuildAllEffectPrefabs()
    {
        Debug.Log("<color=cyan><b>[EffectPrefabBuilder] 대문자 Prefabs 11종 이펙트 프리팹 패키징 시작...</b></color>");

        string rootPrefabsDir = "Assets/Prefabs";
        string subEffectsDir = "Assets/Prefabs/Effects";
        string animsMonsterDir = "Assets/Anims/Monster";
        string animsEffectsDir = "Assets/Anims/Effects";

        if (!Directory.Exists(rootPrefabsDir)) Directory.CreateDirectory(rootPrefabsDir);
        if (!Directory.Exists(subEffectsDir)) Directory.CreateDirectory(subEffectsDir);
        if (!Directory.Exists(animsMonsterDir)) Directory.CreateDirectory(animsMonsterDir);
        if (!Directory.Exists(animsEffectsDir)) Directory.CreateDirectory(animsEffectsDir);

        AssetDatabase.Refresh();

        string[] effectKeys = new string[]
        {
            "Placeholder_Parry",
            "Placeholder_Guard",
            "Placeholder_Dodge",
            "Placeholder_Hit",
            "Player_Attack_Hit1_Effect",
            "Player_Attack_Hit2_Effect",
            "Player_Attack_Hit3_Effect",
            "Garon_ComboSlash_Effect",
            "Garon_OverheadSmash_Effect",
            "Garon_Shockwave_Effect",
            "Garon_Charge_Effect"
        };

        foreach (string key in effectKeys)
        {
            string animClipPath = $"{animsEffectsDir}/{key}.anim";

            GameObject go = new GameObject(key);
            var sr = go.AddComponent<SpriteRenderer>();
            var animator = go.AddComponent<Animator>();

            string tempControllerPath = $"{animsEffectsDir}/{key}_Controller.controller";
            RuntimeAnimatorController controller = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(tempControllerPath);

            animator.runtimeAnimatorController = controller;

            string rootPrefabPath = $"{rootPrefabsDir}/{key}.prefab";
            string subPrefabPath = $"{subEffectsDir}/{key}.prefab";

            PrefabUtility.SaveAsPrefabAsset(go, rootPrefabPath);
            PrefabUtility.SaveAsPrefabAsset(go, subPrefabPath);

            Object.DestroyImmediate(go);
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        AddressablePipeline.BuildAndDeploy();
        Debug.Log("<color=green><b>[EffectPrefabBuilder] 대문자 Assets/Prefabs/ 11종 이펙트 배포 완결!</b></color>");
    }
}
#endif
