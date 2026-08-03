using UnityEngine;
using Cysharp.Threading.Tasks;

/// <summary>
/// HubScene – UI/OnGUI 중심 허브 화면.
/// 1스테이지(9001 - TaoShrine) 등 스테이지 선택 입장 및 MainScene 전환을 담당합니다.
/// </summary>
public class HubScene : MonoBehaviour
{
    public async void EnterStage(int stageIndex)
    {
        uint stageIdx = stageIndex > 0 ? (uint)stageIndex : 9001;
        Debug.Log($"<color=cyan>[HubScene] 스테이지 {stageIdx} (1스테이지 도교 신전) 입장 버튼 클릭! MainScene으로 전환...</color>");

        if (StageManager.Instance != null)
        {
            StageManager.Instance.CurrentStageIdx = stageIdx;
        }

        if (GameSceneManager.Instance != null)
        {
            await GameSceneManager.Instance.TransitionTo(GameSceneManager.SceneName.Main);
        }
    }

    private void OnGUI()
    {
        GUIStyle btnStyle = new GUIStyle(GUI.skin.button)
        {
            fontSize = 18,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter
        };

        float btnWidth = 360f;
        float btnHeight = 60f;
        float centerX = (Screen.width - btnWidth) * 0.5f;
        float centerY = (Screen.height - btnHeight) * 0.5f;

        GUI.Box(new Rect(centerX - 20, centerY - 80, btnWidth + 40, btnHeight + 140), "=== TP2 Stage Hub Scene ===");

        if (GUI.Button(new Rect(centerX, centerY, btnWidth, btnHeight), "⛩️ 1스테이지 도교 신전 입장 (Stage 9001)", btnStyle))
        {
            EnterStage(9001);
        }
    }
}
