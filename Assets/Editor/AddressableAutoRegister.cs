#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEngine;

/// <summary>
/// 유니티 에디터에서 클릭 한 번으로 모든 리소스의 Addressable 체크박스, Name, Label을 자동 등록해 주는 에디터 유틸리티.
/// </summary>
public static class AddressableAutoRegister
{
    [MenuItem("TP2/Register All Addressables (자동 등록)")]
    public static void RegisterAllAddressables()
    {
        var settings = AddressableAssetSettingsDefaultObject.Settings;
        if (settings == null)
        {
            Debug.LogError("[AddressableAutoRegister] AddressableAssetSettings를 찾을 수 없습니다. Window > Asset Management > Addressables > Groups 메뉴에서 Settings를 생성해 주세요.");
            return;
        }

        var group = settings.DefaultGroup;

        // 1. AnimatorController 등록 (PascalCase Key, Label: Anims)
        RegisterAsset(settings, group, "Assets/Anims/Player/PlayerAnimatorController.controller", "PlayerAnimatorController", "Anims");
        RegisterAsset(settings, group, "Assets/Anims/Monster/GaronAnimatorController.controller", "GaronAnimatorController", "Anims");
        RegisterAsset(settings, group, "Assets/Anims/Monster/SpearSentryAnimatorController.controller", "SpearSentryAnimatorController", "Anims");
        RegisterAsset(settings, group, "Assets/Anims/Monster/ShadowStalkerAnimatorController.controller", "ShadowStalkerAnimatorController", "Anims");
        RegisterAsset(settings, group, "Assets/Anims/Monster/WaveHeavyAnimatorController.controller", "WaveHeavyAnimatorController", "Anims");

        // 2. Datas 등록 (PascalCase Key, Label: Datas)
        RegisterAsset(settings, group, "Assets/datas/SkillData.csv", "SkillData", "Datas");
        RegisterAsset(settings, group, "Assets/datas/ResourceData.csv", "ResourceData", "Datas");
        RegisterAsset(settings, group, "Assets/datas/TextData.csv", "TextData", "Datas");
        RegisterAsset(settings, group, "Assets/datas/UnitBaseData.csv", "UnitBaseData", "Datas");
        RegisterAsset(settings, group, "Assets/datas/MonsterBaseData.csv", "MonsterBaseData", "Datas");
        RegisterAsset(settings, group, "Assets/datas/MonsterPatternData.csv", "MonsterPatternData", "Datas");
        RegisterAsset(settings, group, "Assets/datas/BossPatternData.csv", "BossPatternData", "Datas");

        // 3. Prefabs 등록 (PascalCase Key, Label: Prefabs)
        RegisterAsset(settings, group, "Assets/prefabs/Particle.prefab", "Particle", "Prefabs");
        RegisterAsset(settings, group, "Assets/prefabs/Player.prefab", "Player", "Prefabs");
        RegisterAsset(settings, group, "Assets/prefabs/Garon.prefab", "Garon", "Prefabs");
        RegisterAsset(settings, group, "Assets/prefabs/SpearSentry.prefab", "SpearSentry", "Prefabs");
        RegisterAsset(settings, group, "Assets/prefabs/ShadowStalker.prefab", "ShadowStalker", "Prefabs");
        RegisterAsset(settings, group, "Assets/prefabs/WaveHeavy.prefab", "WaveHeavy", "Prefabs");

        settings.SetDirty(AddressableAssetSettings.ModificationEvent.EntryMoved, null, true);
        AssetDatabase.SaveAssets();
        Debug.Log("<color=green><b>[TP2 Addressables] 모든 리소스의 Addressable 체크박스, Name, Label 자동 등록 완료!</b></color>");
    }

    private static void RegisterAsset(AddressableAssetSettings settings, AddressableAssetGroup group, string assetPath, string addressableName, string labelName)
    {
        string guid = AssetDatabase.AssetPathToGUID(assetPath);
        if (string.IsNullOrEmpty(guid))
        {
            Debug.LogWarning($"[AddressableAutoRegister] 파일을 찾을 수 없습니다: {assetPath}");
            return;
        }

        // Entry 생성 또는 가져오기
        var entry = settings.CreateOrMoveEntry(guid, group, false, false);
        if (entry != null)
        {
            entry.address = addressableName; // PascalCase 주소 지정

            // 라벨 추가
            if (!settings.GetLabels().Contains(labelName))
            {
                settings.AddLabel(labelName, true);
            }
            entry.SetLabel(labelName, true, true);

            Debug.Log($"[AddressableAutoRegister] 성공적으로 등록됨: {assetPath} -> Address: '{addressableName}', Label: '{labelName}'");
        }
    }
}
#endif
