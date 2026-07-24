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

        // 2. 경로 설정
        string sourcePath = @"ServerData\Local"; // 유니티 프로젝트 루트
        string serverPath = @"C:\Users\PC\TP2LocalServer\ServerData"; // 로컬 서버 루트 경로

        // 3. 파일 동기화
        if (Directory.Exists(sourcePath))
        {
            if (!Directory.Exists(serverPath)) Directory.CreateDirectory(serverPath);

            // 파일 복사 (ServerData의 모든 내용을 서버 루트로 덮어쓰기)
            foreach (string dirPath in Directory.GetDirectories(sourcePath, "*", SearchOption.AllDirectories))
                Directory.CreateDirectory(dirPath.Replace(sourcePath, serverPath));

            foreach (string newPath in Directory.GetFiles(sourcePath, "*.*", SearchOption.AllDirectories))
                File.Copy(newPath, newPath.Replace(sourcePath, serverPath), true);

            Debug.Log($"<color=green>Deploy Success!</color> Files copied to {serverPath}");
        }
        else
        {
            Debug.LogError("ServerData folder not found! Build failed?");
        }
    }
}