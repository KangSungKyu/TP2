using UnityEngine;
using Cysharp.Threading.Tasks;
using System.Threading;

/// <summary>
/// InitScene – 게임 실행 시 가장 먼저 로드되는 Boot 씬.
/// ResourceManager, DataTableManager 등의 부트 프로세스를 완료한 후 MainScene으로 전환합니다.
/// </summary>
public class InitScene : MonoBehaviour
{
    [SerializeField] private GameSceneManager.SceneName nextScene = GameSceneManager.SceneName.Hub;

    private async void Start()
    {
        Debug.Log("<color=cyan><b>[InitScene] 게임 부팅 프로세스 시작...</b></color>");

        // 1. 필수 매니저 GameObject 생성 보장
        this.ensureManagerExists<GameSceneManager>("GameSceneManager");
        this.ensureManagerExists<ResourceManager>("ResourceManager");
        this.ensureManagerExists<DataTableManager>("DataTableManager");

        // 2. ResourceManager 초기화 (Addressables 및 카탈로그 수신)
        if (ResourceManager.Instance != null)
        {
            await ResourceManager.Instance.InitAsync(null, this.GetCancellationTokenOnDestroy());
            Debug.Log("[InitScene] ResourceManager 초기화 완료.");
        }

        // 3. DataTableManager CSV 데이터가 완전히 로드/캐싱 완료될 때까지 안전 대기
        if (DataTableManager.Instance != null)
        {
            await DataTableManager.Instance.EnsureDataLoadedAsync();
            Debug.Log("[InitScene] DataTableManager 모든 CSV 데이터 로드 완료.");
        }

        Debug.Log($"<color=green><b>[InitScene] 부팅 프로세스 완료! {nextScene} 씬으로 전환합니다.</b></color>");

        // 4. 다음 씬(MainScene)으로 전환
        await GameSceneManager.Instance.TransitionTo(nextScene);
    }

    private void ensureManagerExists<T>(string gameObjectName) where T : MonoBehaviour
    {
        if (UnityEngine.Object.FindFirstObjectByType<T>() == null)
        {
            Debug.LogWarning($"[InitScene] '{typeof(T).Name}' 매니저가 씬 상에 사전 배치되어 있지 않습니다.");
        }
    }
}
