#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEngine;
using System.IO;

/// <summary>
/// Addressables 자동 등록 & 로컬 배포 파이프라인 (대문자 P 표준 경로 Assets/Prefabs 반영).
/// </summary>
public static class AddressablePipeline
{
    [MenuItem("TP2/Register All Addressables (Addressables 전수 등록)")]
    public static void RegisterAllAddressables()
    {
        var settings = AddressableAssetSettingsDefaultObject.Settings;
        if (settings == null)
        {
            Debug.LogError("[AddressablePipeline] AddressableAssetSettings를 찾을 수 없습니다.");
            return;
        }

        var prefabsGroup = settings.FindGroup("Prefabs") ?? settings.CreateGroup("Prefabs", false, false, true, null);
        var animsGroup = settings.FindGroup("Anims") ?? settings.CreateGroup("Anims", false, false, true, null);
        var datasGroup = settings.FindGroup("Datas") ?? settings.CreateGroup("Datas", false, false, true, null);

        // 1. Prefabs 하위 전체 (.prefab) 서치 (대문자 P 경로 Assets/Prefabs 및 서브 폴더 Rooms 등 포함)
        string prefabsDir = "Assets/Prefabs";
        if (Directory.Exists(prefabsDir))
        {
            string[] prefabFiles = Directory.GetFiles(prefabsDir, "*.prefab", SearchOption.AllDirectories);
            foreach (string file in prefabFiles)
            {
                string assetPath = file.Replace('\\', '/');
                string guid = AssetDatabase.AssetPathToGUID(assetPath);
                if (!string.IsNullOrEmpty(guid))
                {
                    string addressKey = Path.GetFileNameWithoutExtension(assetPath);
                    var entry = settings.CreateOrMoveEntry(guid, prefabsGroup);
                    entry.address = addressKey;
                    Debug.Log($"Addressable Registered [Prefabs]: {addressKey} -> {assetPath}");
                }
            }
        }

        // 2. Anims 하위 전체 (.controller, .anim)
        string animsDir = "Assets/Anims";
        if (Directory.Exists(animsDir))
        {
            string[] animFiles = Directory.GetFiles(animsDir, "*.*", SearchOption.AllDirectories);
            foreach (string file in animFiles)
            {
                if (file.EndsWith(".controller") || file.EndsWith(".anim"))
                {
                    string assetPath = file.Replace('\\', '/');
                    string guid = AssetDatabase.AssetPathToGUID(assetPath);
                    if (!string.IsNullOrEmpty(guid))
                    {
                        string addressKey = Path.GetFileNameWithoutExtension(assetPath);
                        var entry = settings.CreateOrMoveEntry(guid, animsGroup);
                        entry.address = addressKey;
                    }
                }
            }
        }

        // 3. Datas (.csv)
        string datasDir = "Assets/Datas";
        if (Directory.Exists(datasDir))
        {
            settings.AddLabel("Datas");
            string[] csvFiles = Directory.GetFiles(datasDir, "*.csv", SearchOption.AllDirectories);
            foreach (string file in csvFiles)
            {
                string assetPath = file.Replace('\\', '/');
                string guid = AssetDatabase.AssetPathToGUID(assetPath);
                if (!string.IsNullOrEmpty(guid))
                {
                    string addressKey = Path.GetFileNameWithoutExtension(assetPath);
                    var entry = settings.CreateOrMoveEntry(guid, datasGroup);
                    entry.address = addressKey;
                    entry.SetLabel("Datas", true);
                }
            }
        }

        settings.SetDirty(AddressableAssetSettings.ModificationEvent.EntryMoved, null, true);
        AssetDatabase.SaveAssets();
        Debug.Log("<color=green><b>[AddressablePipeline] Addressables 대문자 Prefabs 전수 등록 완결!</b></color>");
    }

    [MenuItem("TP2/Build & Deploy Addressables (Addressables 빌드 및 배포)")]
    public static void BuildAndDeploy()
    {
        RegisterAllAddressables();

        var settings = AddressableAssetSettingsDefaultObject.GetSettings(true);
        if (settings != null)
        {
            string activeProfileId = settings.activeProfileId;
            if (string.IsNullOrEmpty(activeProfileId))
            {
                activeProfileId = settings.profileSettings.GetProfileId("Default");
                if (string.IsNullOrEmpty(activeProfileId))
                {
                    var profileNames = settings.profileSettings.GetAllProfileNames();
                    if (profileNames != null && profileNames.Count > 0)
                    {
                        activeProfileId = settings.profileSettings.GetProfileId(profileNames[0]);
                    }
                }
                if (!string.IsNullOrEmpty(activeProfileId))
                {
                    settings.activeProfileId = activeProfileId;
                }
            }

            foreach (var group in settings.groups)
            {
                if (group == null) continue;
                foreach (var schema in group.Schemas)
                {
                    if (schema is UnityEditor.AddressableAssets.Settings.GroupSchemas.BundledAssetGroupSchema bundledSchema)
                    {
                        if (bundledSchema.BuildPath != null && string.IsNullOrEmpty(bundledSchema.BuildPath.Id))
                        {
                            bundledSchema.BuildPath.SetVariableByName(settings, AddressableAssetSettings.kLocalBuildPath);
                        }
                        if (bundledSchema.LoadPath != null && string.IsNullOrEmpty(bundledSchema.LoadPath.Id))
                        {
                            bundledSchema.LoadPath.SetVariableByName(settings, AddressableAssetSettings.kLocalLoadPath);
                        }
                    }
                }
            }

            settings.SetDirty(AddressableAssetSettings.ModificationEvent.GroupSchemaModified, null, true);
            AssetDatabase.SaveAssets();
        }

        AddressableAssetSettings.BuildPlayerContent();
        Debug.Log("<color=green><b>[AddressablePipeline] Addressable Player Content Build Complete!</b></color>");
    }
}
#endif
