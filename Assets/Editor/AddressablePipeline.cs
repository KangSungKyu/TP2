#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEngine;

/// <summary>
/// Addressables 전수 자동 등록 및 로컬 배포 서버(C:\Users\PC\TP2LocalServer\ServerData) 빌드/동기화 통합 유틸리티.
/// </summary>
public static class AddressablePipeline
{
    [MenuItem("Addressables/Register All Addressables")]
    public static void RegisterAllAddressables()
    {
        var settings = AddressableAssetSettingsDefaultObject.GetSettings(true);
        if (settings == null) return;

        var animGroup = GetOrCreateGroup(settings, "Anims");
        var prefabGroup = GetOrCreateGroup(settings, "Prefabs");
        var dataGroup = GetOrCreateGroup(settings, "Datas");

        // 1. .controller -> Anims
        foreach (var file in Directory.GetFiles("Assets", "*.controller", SearchOption.AllDirectories))
        {
            RegisterFile(settings, animGroup, file, "Anims");
        }

        // 2. .prefab -> Prefabs
        foreach (var file in Directory.GetFiles("Assets", "*.prefab", SearchOption.AllDirectories))
        {
            RegisterFile(settings, prefabGroup, file, "Prefabs");
        }

        // 3. .csv -> Datas
        foreach (var file in Directory.GetFiles("Assets", "*.csv", SearchOption.AllDirectories))
        {
            RegisterFile(settings, dataGroup, file, "Datas");
        }

        AssetDatabase.SaveAssets();
    }

    [MenuItem("Addressables/Build and Deploy to Local Server")]
    public static void BuildAndDeploy()
    {
        RegisterAllAddressables();
        AddressableAssetSettings.BuildPlayerContent();

        string projectServerData = Path.Combine(Application.dataPath, "..", "ServerData");
        string localServerData = @"C:\Users\PC\TP2LocalServer\ServerData";

        if (Directory.Exists(projectServerData))
        {
            Directory.CreateDirectory(localServerData);
            CopyDirectory(projectServerData, localServerData);
            Debug.Log($"<color=green>[AddressablePipeline] Successfully deployed bundles to {localServerData}</color>");
        }
    }

    private static AddressableAssetGroup GetOrCreateGroup(AddressableAssetSettings settings, string groupName)
    {
        var group = settings.FindGroup(groupName);
        if (group == null)
        {
            group = settings.CreateGroup(groupName, false, false, true, null);
        }
        return group;
    }

    private static void RegisterFile(AddressableAssetSettings settings, AddressableAssetGroup group, string path, string label)
    {
        string assetPath = path.Replace("\\", "/");
        string guid = AssetDatabase.AssetPathToGUID(assetPath);
        if (string.IsNullOrEmpty(guid)) return;

        var entry = settings.CreateOrMoveEntry(guid, group);
        if (entry != null)
        {
            string key = Path.GetFileNameWithoutExtension(assetPath);
            entry.address = key;
            entry.SetLabel(label, true);
        }
    }

    private static void CopyDirectory(string sourceDir, string targetDir)
    {
        Directory.CreateDirectory(targetDir);
        foreach (string file in Directory.GetFiles(sourceDir, "*.*", SearchOption.AllDirectories))
        {
            string relPath = file.Substring(sourceDir.Length + 1);
            string destFile = Path.Combine(targetDir, relPath);
            Directory.CreateDirectory(Path.GetDirectoryName(destFile));
            File.Copy(file, destFile, true);
        }
    }
}
#endif
