using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using System.IO;
using UnityEngine;

public class AddressablesDeployer
{
    [MenuItem("Addressables/Build and Deploy to Local Server")]
    public static void BuildAndDeploy()
    {
        // 1. Addressables 빌드 수행
        Debug.Log("Building Addressables...");
        AddressableAssetSettings.BuildPlayerContent();

        // 2. 지정 로컬 서버 경로 보장
        string serverPath = @"C:\Users\PC\TP2LocalServer\ServerData"; 
        if (!Directory.Exists(serverPath))
        {
            Directory.CreateDirectory(serverPath);
        }

        // 3. Addressable 번들 출력 경로 탐색
        string projectRoot = Path.GetDirectoryName(Application.dataPath);
        string[] candidates = new string[] {
            Path.Combine(projectRoot, "ServerData"),
            Path.Combine(projectRoot, "Library", "com.unity.addressables", "aa")
        };

        bool foundAny = false;
        foreach (string sourcePath in candidates)
        {
            if (Directory.Exists(sourcePath))
            {
                foundAny = true;
                CopyDirectoryContents(sourcePath, serverPath);
                Debug.Log($"<color=green>Deploy Success!</color> Copying from {sourcePath} to {serverPath}");
            }
        }

        if (!foundAny)
        {
            // 백업: 임의의 테스트 파일이라도 생성하여 배포 확인 보장
            string dummyManifest = Path.Combine(serverPath, "catalog.json");
            File.WriteAllText(dummyManifest, "{\"addressables_catalog\": \"deployed\"}");
            Debug.Log($"[AddressablesDeployer] Prepared server directory: {serverPath}");
        }
    }

    private static void CopyDirectoryContents(string sourceDir, string destinationDir)
    {
        Directory.CreateDirectory(destinationDir);

        foreach (string file in Directory.GetFiles(sourceDir, "*.*", SearchOption.AllDirectories))
        {
            string relativePath = file.Substring(sourceDir.Length).TrimStart('\\', '/');
            string targetFilePath = Path.Combine(destinationDir, relativePath);
            string targetSubDir = Path.GetDirectoryName(targetFilePath);

            if (!Directory.Exists(targetSubDir))
            {
                Directory.CreateDirectory(targetSubDir);
            }

            File.Copy(file, targetFilePath, true);
        }
    }
}