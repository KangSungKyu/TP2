#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

/// <summary>
/// 유니티 CLI 배치모드 execution method 통합 진입점.
/// </summary>
public static class BatchFullProcessor
{
    public static void ExecuteFullPipeline()
    {
        Debug.Log("<color=yellow><b>[BatchFullProcessor] Full Unity Pipeline Executing...</b></color>");
        UnityPipelineAnimatorBinder.ExecuteFullPipelineBinding();
        EffectPrefabBuilder.BuildAllEffectPrefabs();
        StageResourcePipeline.BuildStageEnvironmentResources();
        RoomPrefabBuilder.BuildRoomTestDummyPrefab();
        TilemapRoomPrefabBuilder.BuildTilemapRoomPrefab();
        AddressablePipeline.BuildAndDeploy();
        Debug.Log("<color=green><b>[BatchFullProcessor] Full Unity Pipeline Complete!</b></color>");
    }
}
#endif
