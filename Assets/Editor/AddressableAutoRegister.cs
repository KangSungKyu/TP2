#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using System.IO;
using UnityEngine;

/// <summary>
/// 프로젝트 내 사용 중인 모든 최상위 리소스(AnimatorController, Datas CSV, Prefabs)를 
/// 동적으로 전수 탐색하여 Addressable 규격(PascalCase Key, 지정 Label)에 따라 자동 등록하는 에디터 유틸리티.
/// </summary>
public static class AddressableAutoRegister
{
    [MenuItem("TP2/Register All Addressables (자동 전수 탐색 및 등록)")]
    public static void RegisterAllAddressables()
    {
        var settings = AddressableAssetSettingsDefaultObject.Settings;
        if (settings == null)
        {
            Debug.LogError("[AddressableAutoRegister] AddressableAssetSettings를 찾을 수 없습니다. Window > Asset Management > Addressables > Groups 메뉴에서 Settings를 생성해 주세요.");
            return;
        }

        var group = settings.DefaultGroup;
        int registeredCount = 0;

        // 1. Controller 에셋 전수 탐색 및 등록 (Label: Anims)
        string[] animControllers = Directory.GetFiles("Assets", "*.controller", SearchOption.AllDirectories);
        foreach (string controllerPath in animControllers)
        {
            string fileName = Path.GetFileNameWithoutExtension(controllerPath);
            RegisterAsset(settings, group, NormalizePath(controllerPath), fileName, "Anims");
            registeredCount++;
        }

        // 2. Datas CSV 에셋 전수 탐색 및 등록 (Label: Datas)
        string[] dataFiles = Directory.GetFiles("Assets", "*.csv", SearchOption.AllDirectories);
        foreach (string dataPath in dataFiles)
        {
            // Resources 폴더 내부 에셋은 Addressable 등록 대상에서 제외
            if (dataPath.Contains("Resources")) continue;

            string fileName = Path.GetFileNameWithoutExtension(dataPath);
            // PascalCase 보장
            string pascalName = char.ToUpper(fileName[0]) + fileName.Substring(1);
            RegisterAsset(settings, group, NormalizePath(dataPath), pascalName, "Datas");
            registeredCount++;
        }

        // 3. Prefabs 에셋 전수 탐색 및 등록 (Label: Prefabs)
        string[] prefabFiles = Directory.GetFiles("Assets", "*.prefab", SearchOption.AllDirectories);
        foreach (string prefabPath in prefabFiles)
        {
            // Plugins 등 외부 패키지 프리팹 제외
            if (prefabPath.Contains("Plugins") || prefabPath.Contains("Packages")) continue;

            string fileName = Path.GetFileNameWithoutExtension(prefabPath);
            string pascalName = char.ToUpper(fileName[0]) + fileName.Substring(1);
            RegisterAsset(settings, group, NormalizePath(prefabPath), pascalName, "Prefabs");
            registeredCount++;
        }

        settings.SetDirty(AddressableAssetSettings.ModificationEvent.EntryMoved, null, true);
        AssetDatabase.SaveAssets();
        Debug.Log($"<color=green><b>[TP2 Addressables] 총 {registeredCount}개 에셋의 Addressable PascalCase Key 및 Label 자동 전수 등록 완료!</b></color>");
    }

    private static void RegisterAsset(AddressableAssetSettings settings, AddressableAssetGroup group, string assetPath, string addressableName, string labelName)
    {
        string guid = AssetDatabase.AssetPathToGUID(assetPath);
        if (string.IsNullOrEmpty(guid))
        {
            Debug.LogWarning($"[AddressableAutoRegister] 에셋 GUID를 찾을 수 없습니다: {assetPath}");
            return;
        }

        var entry = settings.CreateOrMoveEntry(guid, group, false, false);
        if (entry != null)
        {
            entry.address = addressableName; // PascalCase 주소 바인딩

            if (!settings.GetLabels().Contains(labelName))
            {
                settings.AddLabel(labelName, true);
            }
            entry.SetLabel(labelName, true, true);

            Debug.Log($"[AddressableAutoRegister] 등록 완료: {assetPath} -> Key: '{addressableName}', Label: '{labelName}'");
        }
    }

    private static string NormalizePath(string path)
    {
        return path.Replace("\\", "/");
    }
}
#endif
