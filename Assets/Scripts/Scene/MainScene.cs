using Cysharp.Threading.Tasks;
using UnityEngine;

/// <summary>
/// MainScene – 코어 1스테이지 메트로배니아 플레이 및 자동 룸 전개 스크립트.
/// HubScene에서 입장 시 StageManager를 통해 1스테이지(9001) 첫 룸 청크(Tilemap_Room_Stage1_Entry)를 자동 빌드하고 플레이 환경을 구축합니다.
/// </summary>
public class MainScene : MonoBehaviour
{
    [SerializeField] private AlertMessage alertMessage;
    [SerializeField] private uint warningTextIdx;
    [SerializeField] private uint warningEnglishTextIdx;

    private async void Start()
    {
        // 0. 매니저 부트스트랩 안전장치 (InitScene/HubScene을 거치지 않고 직접 실행 시 대비)
        await this.ensureManagersReadyAsync();

        // 1. 1스테이지(9001) 첫 룸 청크(Tilemap_Room_Stage1_Entry) 자동 비동기 전개 및 빌드
        if (StageManager.Instance != null)
        {
            Debug.Log("<color=cyan><b>[MainScene] StageManager를 통해 1스테이지(9001) 룸 청크 자동 로딩 시작...</b></color>");
            await StageManager.Instance.EnsureStageLoadedAsync(9001, this.GetCancellationTokenOnDestroy());
        }

        // 2. 카메라 위치 및 앵글 설정 (메트로배니아 카메라 셋업 완료 전 기본 시점 설정)
        if (Camera.main != null && Camera.main.GetComponent<MetroidvaniaCamera2D>() == null)
        {
            Camera.main.orthographic = true;
            Camera.main.orthographicSize = 9f;
            Camera.main.transform.position = new Vector3(0f, 4.5f, -10f);
            Camera.main.transform.rotation = Quaternion.identity;
            Camera.main.backgroundColor = new Color(0.15f, 0.15f, 0.2f);
        }

        // 3. ----- OnGUI HUD, 테스트 플레이어 HUD 및 최적화 몬스터 오버레이 HUD 생성 -----
        var hudObj = new GameObject("CoreTestHUD");
        hudObj.AddComponent<CoreTestHUD>();
        hudObj.AddComponent<TestPlayerHUDUI>();
        hudObj.AddComponent<MonsterOverheadHUD>();

        if (warningTextIdx != 0)
            alertMessage?.Show(warningTextIdx, warningEnglishTextIdx, 3f);

        Debug.Log("<color=green><b>[MainScene] 1스테이지 도교 신전(9001) 룸 청크 빌드 및 플레이 환경 구축 완결!</b></color>");
    }

    private async UniTask ensureManagersReadyAsync()
    {
        if (ResourceManager.Instance == null && UnityEngine.Object.FindFirstObjectByType<ResourceManager>() == null)
        {
            Debug.LogWarning("[MainScene] 'ResourceManager' 매니저가 씬 상에 사전 배치되어 있지 않습니다.");
        }

        if (DataTableManager.Instance == null && UnityEngine.Object.FindFirstObjectByType<DataTableManager>() == null)
        {
            Debug.LogWarning("[MainScene] 'DataTableManager' 매니저가 씬 상에 사전 배치되어 있지 않습니다.");
        }

        if (StageManager.Instance == null && UnityEngine.Object.FindFirstObjectByType<StageManager>() == null)
        {
            Debug.LogWarning("[MainScene] 'StageManager' 매니저가 씬 상에 사전 배치되어 있지 않습니다.");
        }

        // ResourceManager 초기화 대기
        if (ResourceManager.Instance != null)
        {
            await ResourceManager.Instance.InitAsync(null, this.GetCancellationTokenOnDestroy());
        }

        // DataTableManager CSV 로드 완료 대기
        if (DataTableManager.Instance != null)
        {
            await DataTableManager.Instance.EnsureDataLoadedAsync();
            Debug.Log("<color=green>[MainScene] DataTableManager CSV 데이터 로드 완료 확인.</color>");
        }
    }
}
