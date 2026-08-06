using System;
using Cysharp.Threading.Tasks;
using UnityEngine;

public class HubScene : MonoBehaviour
{
    private const uint Stage1Idx = 9001;
    private bool transitionInProgress;

    private void Awake()
    {
        StageManager.Instance?.ResetRunForHub();
        if (Player.Instance != null && UnitPoolManager.Instance != null)
            UnitPoolManager.Instance.DespawnUnit(Player.Instance);
    }

    public async void EnterStage(int stageIndex)
    {
        if (transitionInProgress) return;
        transitionInProgress = true;
        uint stageIdx = stageIndex > 0 ? (uint)stageIndex : Stage1Idx;

        try
        {
            if (StageManager.Instance == null || GameSceneManager.Instance == null)
                throw new InvalidOperationException("Required scene manager is unavailable.");

            StageManager.Instance.CurrentStageIdx = stageIdx;
            await GameSceneManager.Instance.TransitionTo(GameSceneManager.SceneName.Main);
        }
        catch (Exception exception)
        {
            transitionInProgress = false;
            Debug.LogError($"[HubScene] Stage {stageIdx} transition failed: {exception.Message}");
        }
    }
}
