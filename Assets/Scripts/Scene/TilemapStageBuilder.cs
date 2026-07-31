using Cysharp.Threading.Tasks;
using System.Threading;
using UnityEngine;

/// <summary>
/// Unity 2D Tilemap 기반 메트로배니아/로그라이트 스테이지 생성 & 관리 클래스.
/// ResourceManager를 통해 'Tilemap_Room_TestDummy' 청크 프리팹을 비동기 스폰하여
/// CompositeCollider2D 일체형 지형 및 1-Way 발판 Tilemap 시스템을 깨끗하게 전개합니다.
/// </summary>
public class TilemapStageBuilder : MonoBehaviour
{
    [Header("Tilemap Room Chunk Settings")]
    public string TilemapAddressableKey = "Tilemap_Room_TestDummy";
    public Vector2 RoomSize = new Vector2(30f, 18f);

    [Header("Buffer & Fade Settings")]
    public float BufferTimeSec = 0.5f;
    public float FadeDurationSec = 0.4f;

    private CanvasGroup fadeOverlayCanvasGroup;

    private void Start()
    {
        if (Application.isPlaying)
        {
            BuildTilemapStageAsync(this.GetCancellationTokenOnDestroy()).Forget();
        }
    }

    [ContextMenu("Build Stage In Editor")]
    public void BuildStageEditorSync()
    {
        GameObject existingStage = GameObject.Find("TilemapStage_Root");
        if (existingStage != null)
        {
            if (Application.isPlaying) Destroy(existingStage);
            else DestroyImmediate(existingStage);
        }

        GameObject rootObj = new GameObject("TilemapStage_Root");
        GameObject chunkPrefab = null;

#if UNITY_EDITOR
        chunkPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Rooms/Tilemap_Room_TestDummy.prefab");
#endif

        if (chunkPrefab != null)
        {
            GameObject spawnedChunk = Instantiate(chunkPrefab, rootObj.transform);
            spawnedChunk.name = "Tilemap_Room_Chunk_Instance";

            var spawner = UnitSpawner.Instance != null ? UnitSpawner.Instance : FindObjectOfType<UnitSpawner>();
            if (spawner != null)
            {
                spawner.SpawnUnitsFromMarkers(spawnedChunk);
            }
            Debug.Log($"<color=green>[TilemapStageBuilder] 에디터 동기 로드 완료: '{TilemapAddressableKey}'</color>");
        }
        else
        {
            buildExpandedDummyStage(rootObj.transform);
            var spawner = UnitSpawner.Instance != null ? UnitSpawner.Instance : FindObjectOfType<UnitSpawner>();
            if (spawner != null)
            {
                spawner.SpawnUnitsFromMarkers(rootObj);
            }
            Debug.Log("<color=green>[TilemapStageBuilder] 에디터 동기 60x30 대형 더미 스테이지 생성 완료!</color>");
        }
    }

    public async UniTask BuildTilemapStageAsync(CancellationToken cancellationToken = default)
    {
        setupFadeOverlay();
        if (fadeOverlayCanvasGroup != null)
        {
            fadeOverlayCanvasGroup.alpha = 1f;
        }

        GameObject existingStage = GameObject.Find("TilemapStage_Root");
        if (existingStage != null)
        {
            if (Application.isPlaying) Destroy(existingStage);
            else DestroyImmediate(existingStage);
            if (Application.isPlaying) await UniTask.Yield(PlayerLoopTiming.Update, cancellationToken);
        }

        GameObject rootObj = new GameObject("TilemapStage_Root");
        GameObject chunkPrefab = null;

#if UNITY_EDITOR
        chunkPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Rooms/Tilemap_Room_TestDummy.prefab");
#endif

        if (chunkPrefab == null && ResourceManager.Instance != null)
        {
            try
            {
                var tcs = new UniTaskCompletionSource<GameObject>();
                ResourceManager.Instance.LoadAssetAsync<GameObject>(this.TilemapAddressableKey, prefab =>
                {
                    tcs.TrySetResult(prefab);
                });
                chunkPrefab = await tcs.Task;
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"[TilemapStageBuilder] Addressable Key '{this.TilemapAddressableKey}' 로드 예외 (폴백 전환): {ex.Message}");
            }
        }

        bool loadedFromPrefab = false;
        if (chunkPrefab != null)
        {
            GameObject spawnedChunk = Instantiate(chunkPrefab, rootObj.transform);
            spawnedChunk.name = "Tilemap_Room_Chunk_Instance";
            loadedFromPrefab = true;

            // SpawnPointMarker를 탐색하여 플레이어, 일반 몬스터, 보스 동적 스폰 파이프라인 구동
            if (UnitSpawner.Instance != null)
            {
                UnitSpawner.Instance.SpawnUnitsFromMarkers(spawnedChunk);
            }

            Debug.Log($"<color=green>[TilemapStageBuilder] '{this.TilemapAddressableKey}' 2D Tilemap 청크 로드, 스폰 & 유닛 마커 배치 완결!</color>");
        }

        // 3-1. 청크 프리팹 미생성 시 60x30 대형 4개 구역 더미 스테이지 자동 전개
        if (!loadedFromPrefab)
        {
            Debug.LogWarning("[TilemapStageBuilder] 대형 60x30 멀티 존 더미 스테이지를 동적 생성합니다.");
            buildExpandedDummyStage(rootObj.transform);

            if (UnitSpawner.Instance != null)
            {
                UnitSpawner.Instance.SpawnUnitsFromMarkers(rootObj);
            }
        }

        SetupMetroidvaniaCamera();
        Debug.Log("<color=cyan>[TilemapStageBuilder] 메트로배니아 2D 카메라 바인딩 & 대형 테스트 스테이지 전개 완료! 0.5s 버퍼 대기 개시...</color>");

        if (BufferTimeSec > 0f)
        {
            await UniTask.Delay(System.TimeSpan.FromSeconds(BufferTimeSec), cancellationToken: cancellationToken);
        }

        await fadeInScreenAsync(cancellationToken);
        Debug.Log("<color=green>[TilemapStageBuilder] 버퍼 시간 종료 및 화면 페이드 인 전개 완결!</color>");
    }

    private void SetupMetroidvaniaCamera()
    {
        Camera mainCam = Camera.main;
        if (mainCam == null)
        {
            var camObj = new GameObject("Main Camera");
            mainCam = camObj.AddComponent<Camera>();
            camObj.tag = "MainCamera";
        }

        var metroCam = mainCam.GetComponent<MetroidvaniaCamera2D>();
        if (metroCam == null)
        {
            metroCam = mainCam.gameObject.AddComponent<MetroidvaniaCamera2D>();
        }

        metroCam.SetBounds(new Vector2(-29f, -1f), new Vector2(29f, 17f));
        var player = FindObjectOfType<Player>();
        if (player != null)
        {
            metroCam.Target = player.transform;
        }
    }

    private void buildExpandedDummyStage(Transform parent)
    {
        // ---------------------------------------------------------------------
        // 1. Base Ground (60m x 1.5m)
        // ---------------------------------------------------------------------
        GameObject groundObj = createPoolableObject("Ground_Base_Main", parent, new Vector3(0f, -0.75f, 0f));
        var groundCol = getOrAddComponent<BoxCollider2D>(groundObj);
        groundCol.size = new Vector2(60f, 1.5f);
        var groundSprite = getOrAddComponent<SpriteRenderer>(groundObj);
        groundSprite.sprite = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), 16f);
        groundSprite.color = new Color(0.2f, 0.23f, 0.28f, 1.0f);

        // 外壁 (Left / Right Outer Boundaries)
        GameObject leftBoundary = createPoolableObject("Boundary_Wall_Left", parent, new Vector3(-30.5f, 15f, 0f));
        var bLeftCol = getOrAddComponent<BoxCollider2D>(leftBoundary);
        bLeftCol.size = new Vector2(1.0f, 32.0f);

        GameObject rightBoundary = createPoolableObject("Boundary_Wall_Right", parent, new Vector3(30.5f, 15f, 0f));
        var bRightCol = getOrAddComponent<BoxCollider2D>(rightBoundary);
        bRightCol.size = new Vector2(1.0f, 32.0f);

        // ---------------------------------------------------------------------
        // Zone A: Player Spawn & Movement Zone (X: -28 ~ -10)
        // ---------------------------------------------------------------------
        createSpawnMarker(parent, "Marker_PlayerSpawn", new Vector3(-25f, 1.5f, 0f), SpawnType.Player);

        // 1-Way 하향 점프 발판 3단
        createOneWayPlatform(parent, "Platform_Low", new Vector3(-22f, 3.5f, 0f), new Vector2(5f, 0.4f));
        createOneWayPlatform(parent, "Platform_Mid", new Vector3(-17f, 6.5f, 0f), new Vector2(5f, 0.4f));
        createOneWayPlatform(parent, "Platform_High", new Vector3(-22f, 9.5f, 0f), new Vector2(5f, 0.4f));

        // ---------------------------------------------------------------------
        // Zone B: Wall Jump & Wall Slide Multi-Zone (X: -10 ~ +10)
        // ---------------------------------------------------------------------
        // B-1. 수직 클라이밍 통로 (Wall Climb Shaft: 폭 4m, 높이 16m)
        GameObject shaftLeft = createWallObject("Wall_Shaft_Left", parent, new Vector3(-8f, 8f, 0f), new Vector2(1f, 16f), true, true, 1.0f, new Color(0.3f, 0.6f, 0.9f));
        GameObject shaftRight = createWallObject("Wall_Shaft_Right", parent, new Vector3(-4f, 8f, 0f), new Vector2(1f, 16f), true, true, 1.0f, new Color(0.3f, 0.6f, 0.9f));

        // B-2. 교차 전용 벽 (AllowSameWall = false)
        createWallObject("Wall_AlternateOnly", parent, new Vector3(0f, 6f, 0f), new Vector2(1f, 12f), true, false, 1.0f, new Color(0.8f, 0.7f, 0.2f));

        // B-3. 벽점프 금지 벽 (CanWallJump = false)
        createWallObject("Wall_NoJump_Red", parent, new Vector3(4f, 6f, 0f), new Vector2(1f, 12f), false, false, 1.0f, new Color(0.85f, 0.2f, 0.2f));

        // B-4. 얼음 슬라이딩 벽 (SlideSpeedMultiplier = 2.5x)
        createWallObject("Wall_IceSlide_Cyan", parent, new Vector3(8f, 6f, 0f), new Vector2(1f, 12f), true, true, 2.5f, new Color(0.2f, 0.85f, 1.0f));

        // ---------------------------------------------------------------------
        // Zone C: Combat & Monster Test Arena (X: +10 ~ +28)
        // ---------------------------------------------------------------------
        createSpawnMarker(parent, "Marker_MonsterGaron", new Vector3(15f, 1.5f, 0f), SpawnType.Monster, "1001");
        createSpawnMarker(parent, "Marker_BossGaron", new Vector3(23f, 1.5f, 0f), SpawnType.Boss, "3201");

        // 전투용 높낮이 보조 발판
        createOneWayPlatform(parent, "Platform_Combat_1", new Vector3(15f, 4.0f, 0f), new Vector2(4f, 0.4f));
        createOneWayPlatform(parent, "Platform_Combat_2", new Vector3(23f, 5.0f, 0f), new Vector2(4f, 0.4f));

        // Hazard Spikes (X: 18 ~ 20)
        GameObject hazardObj = createPoolableObject("Hazard_Spikes", parent, new Vector3(19f, 0.2f, 0f));
        var hazardCol = getOrAddComponent<BoxCollider2D>(hazardObj);
        hazardCol.size = new Vector2(3f, 0.4f);
        hazardCol.isTrigger = true;
        var hazardSprite = getOrAddComponent<SpriteRenderer>(hazardObj);
        hazardSprite.sprite = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), 16f);
        hazardSprite.color = new Color(0.9f, 0.2f, 0.2f, 0.8f);
    }

    private GameObject createWallObject(string name, Transform parent, Vector3 pos, Vector2 size, bool canWallJump, bool allowSameWall, float slideMult, Color color)
    {
        GameObject wallObj = createPoolableObject(name, parent, pos);
        var col = getOrAddComponent<BoxCollider2D>(wallObj);
        col.size = size;

        var surf = getOrAddComponent<WallJumpSurface>(wallObj);
        surf.CanWallJump = canWallJump;
        surf.AllowSameWall = allowSameWall;
        surf.SlideSpeedMultiplier = slideMult;

        var sprite = getOrAddComponent<SpriteRenderer>(wallObj);
        sprite.sprite = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), 16f);
        sprite.color = color;

        return wallObj;
    }

    private void createOneWayPlatform(Transform parent, string name, Vector3 pos, Vector2 size)
    {
        GameObject platObj = createPoolableObject(name, parent, pos);

        int oneWayLayer = LayerMask.NameToLayer("OneWayPlatform");
        if (oneWayLayer >= 0)
        {
            platObj.layer = oneWayLayer;
        }

        var col = getOrAddComponent<BoxCollider2D>(platObj);
        col.size = size;

        var effector = getOrAddComponent<PlatformEffector2D>(platObj);
        col.usedByEffector = true;

        getOrAddComponent<OneWayPlatformPassThrough>(platObj);

        var surf = getOrAddComponent<WallJumpSurface>(platObj);
        surf.CanWallJump = false;

        var sprite = getOrAddComponent<SpriteRenderer>(platObj);
        sprite.sprite = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), 16f);
        sprite.color = new Color(0.1f, 0.7f, 0.85f, 0.9f);
    }

    private void createSpawnMarker(Transform parent, string name, Vector3 pos, SpawnType type, string monsterId = "")
    {
        GameObject markerObj = createPoolableObject(name, parent, pos);
        var marker = getOrAddComponent<SpawnPointMarker>(markerObj);
        marker.Type = type;
        marker.MonsterId = monsterId;
        marker.EnableSpawn = true;
    }

    private GameObject createPoolableObject(string name, Transform parent, Vector3 pos)
    {
        GameObject obj = new GameObject(name);
        obj.transform.SetParent(parent);
        obj.transform.position = pos;
        return obj;
    }

    private T getOrAddComponent<T>(GameObject target) where T : Component
    {
        T comp = target.GetComponent<T>();
        if (comp == null)
        {
            comp = target.AddComponent<T>();
        }
        return comp;
    }

    private void setupFadeOverlay()
    {
        if (fadeOverlayCanvasGroup != null) return;

        GameObject canvasObj = new GameObject("StageFadeCanvas");
        Canvas canvas = canvasObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 9999;

        fadeOverlayCanvasGroup = canvasObj.AddComponent<CanvasGroup>();

        GameObject panel = new GameObject("BlackOverlay");
        panel.transform.SetParent(canvasObj.transform, false);
        UnityEngine.UI.Image img = panel.AddComponent<UnityEngine.UI.Image>();
        img.color = Color.black;
        RectTransform rect = panel.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.sizeDelta = Vector2.zero;
    }

    private async UniTask fadeInScreenAsync(CancellationToken cancellationToken)
    {
        if (fadeOverlayCanvasGroup == null) return;

        float elapsed = 0f;
        float duration = Mathf.Max(0.1f, FadeDurationSec);

        while (elapsed < duration && !cancellationToken.IsCancellationRequested)
        {
            if (fadeOverlayCanvasGroup == null) break;

            elapsed += Time.deltaTime;
            float t = elapsed / duration;
            fadeOverlayCanvasGroup.alpha = Mathf.Lerp(1f, 0f, t);
            await UniTask.Yield(PlayerLoopTiming.Update, cancellationToken);
        }

        if (fadeOverlayCanvasGroup != null)
        {
            fadeOverlayCanvasGroup.alpha = 0f;
            Destroy(fadeOverlayCanvasGroup.gameObject);
            fadeOverlayCanvasGroup = null;
        }
    }
}
