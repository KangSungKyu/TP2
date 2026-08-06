#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// TP2 프로젝트 InitScene.unity 및 MainScene.unity에 핵심 매니저 노드 및 플레이어 노드를
/// 씬상에 정적으로 사전 배치하고 자동 저장하는 에디터 툴 자동화 클래스.
/// </summary>
public static class SceneSetupAutomation
{
    [MenuItem("TP2/Setup All Manager Scenes Statically")]
    public static void SetupAllScenesStatically()
    {
        string[] scenePaths = new string[]
        {
            "Assets/Scenes/InitScene.unity",
            "Assets/Scenes/MainScene.unity"
        };

        foreach (string scenePath in scenePaths)
        {
            var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            if (!scene.IsValid())
            {
                Debug.LogWarning($"[SceneSetupAutomation] 씬을 로드할 수 없습니다: {scenePath}");
                continue;
            }

            EnsureManagerInScene<ResourceManager>("ResourceManager");
            EnsureManagerInScene<DataTableManager>("DataTableManager");
            EnsureManagerInScene<StageManager>("StageManager");
            EnsureManagerInScene<UnitSpawner>("UnitSpawner");
            EnsureManagerInScene<UnitPoolManager>("UnitPoolManager");
            EnsureManagerInScene<EffectPoolManager>("EffectPoolManager");
            EnsureManagerInScene<SimplePoolManager>("SimplePoolManager");

            if (scenePath.Contains("MainScene"))
            {
                EnsurePlayerInMainScene();
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log($"<color=green>[SceneSetupAutomation] 씬 정적 배치 & 저장 완결: {scenePath}</color>");
        }
    }

    private static void EnsureManagerInScene<T>(string name) where T : MonoBehaviour
    {
        var existing = Object.FindFirstObjectByType<T>();
        if (existing == null)
        {
            GameObject go = new GameObject(name);
            go.AddComponent<T>();
            Debug.Log($"[SceneSetupAutomation] '{name}' 정적 매니저 노드 씬에 배치 완료.");
        }
    }

    private static void EnsurePlayerInMainScene()
    {
        var existingPlayer = Object.FindFirstObjectByType<Player>();
        if (existingPlayer == null)
        {
            GameObject playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Unit_3001.prefab");
            if (playerPrefab != null)
            {
                GameObject playerObj = (GameObject)PrefabUtility.InstantiatePrefab(playerPrefab);
                playerObj.name = "Player";
                playerObj.transform.position = Vector3.zero;
                Debug.Log("[SceneSetupAutomation] MainScene 내 'Player' 프리팹 정적 배치 완료.");
            }
            else
            {
                GameObject playerObj = new GameObject("Player");
                playerObj.AddComponent<Player>();
                Debug.LogWarning("[SceneSetupAutomation] Player 프리팹 미발견 ➔ 기본 Player 정적 노드 생성.");
            }
        }
    }
}
#endif
