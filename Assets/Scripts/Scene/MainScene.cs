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

        // 1. 카메라 위치 및 앵글 설정
        if (Camera.main != null)
        {
            Camera.main.transform.position = new Vector3(1.5f, 1.5f, -8f);
            Camera.main.transform.rotation = Quaternion.identity;
            Camera.main.backgroundColor = new Color(0.15f, 0.15f, 0.2f);
        }

        // 2. ----- Player (UnitBase 상속) 스폰 -----
        this.setupPlayerFromPrefab();

        // 3. ----- BossMonster (철위병 가론 - UnitBase/Monster 상속) 스폰 -----
        this.setupBossMonsterObject();

        // 4. ----- OnGUI HUD 생성 -----
        var hudObj = new GameObject("CoreTestHUD");
        hudObj.AddComponent<CoreTestHUD>();

        Debug.Log("<color=cyan><b>[MainScene] UnitBase 아키텍처 & CSV 데이터 기반 2D 환경 구축 완료!</b></color>");
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
