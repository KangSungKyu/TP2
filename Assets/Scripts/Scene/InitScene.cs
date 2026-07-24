using UnityEngine;
using Cysharp.Threading.Tasks;
using System.Threading;

/// <summary>
/// InitScene – 게임 실행 시 가장 먼저 로드되는 Boot 씬.
/// ResourceManager, DataTableManager 등의 부트 프로세스를 완료한 후 MainScene으로 전환합니다.
/// </summary>
public class InitScene : MonoBehaviour
{
    [SerializeField] private GameSceneManager.SceneName nextScene = GameSceneManager.SceneName.Main;

    private async void Start()
    {
        Debug.Log("<color=cyan><b>[InitScene] 게임 부팅 프로세스 시작...</b></color>");

        // 1. ResourceManager 초기화 (Addressables 및 카탈로그 수신)
        if (ResourceManager.Instance != null)
        {
            await ResourceManager.Instance.InitAsync(null, this.GetCancellationTokenOnDestroy());
            Debug.Log("[InitScene] ResourceManager 초기화 완료.");
        }

        // 2. DataTableManager 초기화 및 프리로드 웜업
        _ = DataTableManager.Instance;
        Debug.Log("[InitScene] DataTableManager 웜업 시작.");

        // 부팅 연출/프리로드 대기
        await UniTask.Delay(System.TimeSpan.FromSeconds(0.5f), cancellationToken: this.GetCancellationTokenOnDestroy());

        Debug.Log($"<color=green><b>[InitScene] 부팅 프로세스 완료! {nextScene} 씬으로 전환합니다.</b></color>");

        // 3. 다음 씬(MainScene)으로 전환
        await GameSceneManager.Instance.TransitionTo(nextScene);
    }
}
