using Cysharp.Threading.Tasks;
using UnityEngine;

/// <summary>
/// MainScene – 코어 테스트 및 실제 플레이 환경 구축 스크립트.
/// UnitBase 상속 아키텍처 (Player, BossMonster) 및 신규 CSV 데이터 시스템으로 스폰합니다.
/// </summary>
public class MainScene : MonoBehaviour
{
    private async void Start()
    {
        // 0. 매니저 부트스트랩 안전장치 (InitScene을 거치지 않고 직접 실행 시 대비)
        await this.ensureManagersReadyAsync();

        // 1. HubScene -> MainScene 진입 시 2D Tilemap 기반 스테이지 구축 (0.5s 버퍼 시간 & 화면 페이드 인 연출)
        var builderObj = new GameObject("TilemapStageBuilder");
        var builder = builderObj.AddComponent<TilemapStageBuilder>();
        await builder.BuildTilemapStageAsync(this.GetCancellationTokenOnDestroy());

        // 2. 카메라 위치 및 앵글 설정 (더미 스테이지 룸 전체 30x18 크기가 선명하게 렌더링되도록 시점 조정)
        if (Camera.main != null)
        {
            Camera.main.orthographic = true;
            Camera.main.orthographicSize = 9f;
            Camera.main.transform.position = new Vector3(0f, 4.5f, -10f);
            Camera.main.transform.rotation = Quaternion.identity;
            Camera.main.backgroundColor = new Color(0.15f, 0.15f, 0.2f);
        }

        // 3. ----- Player (UnitBase 상속) 스폰 -----
        this.setupPlayerFromPrefab();

        // 4. ----- BossMonster (철위병 가론 - UnitBase/Monster 상속) 스폰 -----
        this.setupBossMonsterObject();

        // 5. ----- OnGUI HUD 및 테스트 플레이어 HUD 생성 -----
        var hudObj = new GameObject("CoreTestHUD");
        hudObj.AddComponent<CoreTestHUD>();
        hudObj.AddComponent<TestPlayerHUDUI>();

        Debug.Log("<color=cyan><b>[MainScene] HubScene -> MainScene 진입 기반 메트로배니아 더미 스테이지 & 플레이 환경 구축 완료!</b></color>");
    }

    private async UniTask ensureManagersReadyAsync()
    {
        // ResourceManager 보장
        if (FindObjectOfType<ResourceManager>() == null)
        {
            var go = new GameObject("ResourceManager");
            go.AddComponent<ResourceManager>();
        }

        // DataTableManager 보장
        if (FindObjectOfType<DataTableManager>() == null)
        {
            var go = new GameObject("DataTableManager");
            go.AddComponent<DataTableManager>();
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

    private void setupPlayerFromPrefab()
    {
        if (GameObject.FindWithTag("Player") != null) return;

        if (ResourceManager.Instance != null)
        {
            ResourceManager.Instance.LoadAssetAsync<GameObject>("Player", prefab =>
            {
                if (prefab != null)
                {
                    Instantiate(prefab, new Vector3(0f, 0f, 0f), Quaternion.identity);
                    Debug.Log("<color=green>[MainScene] 'Player.prefab' 스폰 완료!</color>");
                }
                else
                {
                    this.createPure2DPlayer();
                }
            });
        }
        else
        {
            this.createPure2DPlayer();
        }
    }

    private void createPure2DPlayer()
    {
        if (GameObject.FindWithTag("Player") != null) return;

        var playerObj = new GameObject("Player");
        playerObj.tag = "Player";
        playerObj.transform.position = new Vector3(0f, 0f, 0f);

        var visualObj = new GameObject("Visual");
        visualObj.transform.SetParent(playerObj.transform, false);
        visualObj.transform.localPosition = new Vector3(0f, 0.6f, 0f);

        var spriteRenderer = visualObj.AddComponent<SpriteRenderer>();
        spriteRenderer.sortingOrder = 10;
        visualObj.AddComponent<Animator>();

        playerObj.AddComponent<CombatStats>();
        playerObj.AddComponent<SkillExecutor>();
        playerObj.AddComponent<Player>(); // Player (UnitBase)

        Debug.Log("<color=cyan>[MainScene] Pure 2D Player (UnitBase 상속) 스폰 완료!</color>");
    }

    private void setupBossMonsterObject()
    {
        if (GameObject.FindObjectOfType<BossMonster>() != null) return;

        var bossObj = new GameObject("BossGaron");
        bossObj.transform.position = new Vector3(3.5f, 0f, 0f);

        var visualObj = new GameObject("Visual");
        visualObj.transform.SetParent(bossObj.transform, false);
        visualObj.transform.localPosition = new Vector3(0f, 0.75f, 0f);

        var spriteRenderer = visualObj.AddComponent<SpriteRenderer>();
        spriteRenderer.sortingOrder = 5;
        visualObj.AddComponent<Animator>();

        bossObj.AddComponent<CombatStats>();
        bossObj.AddComponent<SkillExecutor>();
        bossObj.AddComponent<BossMonster>(); // BossMonster (UnitBase -> Monster 상속)
    }
}
