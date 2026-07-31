using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Unity CLI / Batchmode 파이프라인 전용 더미 스테이지 씬 자동 생성 클래스.
/// </summary>
public static class DummyStagePipeline
{
    [MenuItem("Build/Generate Dummy Stage Scene")]
    public static void GenerateDummyStageScene()
    {
        Debug.Log("<color=yellow>[Unity-Pipeline] 더미 스테이지 씬 생성 및 배치 파이프라인 가동...</color>");

        var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

        GameObject builderObj = new GameObject("TilemapStageBuilder");
        var builder = builderObj.AddComponent<TilemapStageBuilder>();
        builder.BuildStageEditorSync();

        string scenePath = "Assets/Scenes/TestDummyStageScene.unity";
        bool saved = EditorSceneManager.SaveScene(scene, scenePath);

        if (saved)
        {
            Debug.Log($"<color=green>[Unity-Pipeline] 더미 스테이지 씬 ('{scenePath}') 자동 파이프라인 생성 완결!</color>");
        }
        else
        {
            Debug.LogError($"[Unity-Pipeline Error] 씬 저장 실패: {scenePath}");
        }
    }
}
