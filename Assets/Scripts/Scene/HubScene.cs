using UnityEngine;
using Cysharp.Threading.Tasks;

/// <summary>
/// HubScene – UI 중심 화면. 현재는 기본 UI만 표시하고, 스테이지 입장을 담당합니다.
/// HubUIManager 가 UI 패널을 관리하고, 사용자가 스테이지를 선택하면 MainScene 으로 전환합니다.
/// </summary>
public class HubScene : MonoBehaviour
{
    private void Awake()
    {
        // 현재 HubUIManager 가 구현되지 않았으므로, 필요 시 추가하거나 여기서 초기화 코드를 작성하세요.
        // Debug.Log("HubScene Awake"); // optional placeholder
    }

    /// <summary>
    /// Called by UI button to start a stage.
    /// stageIndex corresponds to the CSV data index.
    /// </summary>
    public async void EnterStage(int stageIndex)
    {
        // 예시: 씬 이름 규칙 "stage_[index]"
        string targetSceneName = $"stage_{stageIndex}";
        // GameSceneManager 로 전환 (LoadingScene 을 자동 경유)
        await GameSceneManager.Instance.TransitionTo(GameSceneManager.SceneName.Main);
        // 실제 스테이지 이름 매핑은 GameSceneManager 혹은 Addressables 설정에 맡김.
    }
}
