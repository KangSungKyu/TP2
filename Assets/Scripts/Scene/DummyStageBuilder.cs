using Cysharp.Threading.Tasks;
using System.Threading;
using UnityEngine;

/// <summary>
/// 메트로배니아/로그라이트 더미 스테이지 생성 클래스.
/// Room Chunk Prefab (Room_TestDummy) 및 다채로운 벽점프 테스트 지형을 동적으로 셋업합니다.
/// </summary>
public class DummyStageBuilder : MonoBehaviour
{
    [Header("Room Chunk Settings")]
    public string RoomChunkAddressableKey = "Room_TestDummy";
    public Vector2 RoomSize = new Vector2(30f, 18f);

    [Header("Buffer & Fade Settings")]
    public float BufferTimeSec = 0.5f;
    public float FadeDurationSec = 0.4f;

    private CanvasGroup fadeOverlayCanvasGroup;

    private void Start()
    {
        BuildDummyStageAsync(this.GetCancellationTokenOnDestroy()).Forget();
    }

    public async UniTask BuildDummyStageAsync(CancellationToken cancellationToken = default)
    {
        setupFadeOverlay();
        if (fadeOverlayCanvasGroup != null)
        {
            fadeOverlayCanvasGroup.alpha = 1f;
        }

        GameObject existingStage = GameObject.Find("DummyTestStage");
        if (existingStage != null)
        {
            Destroy(existingStage);
            await UniTask.Yield(PlayerLoopTiming.Update, cancellationToken);
        }

        GameObject rootObj = new GameObject("DummyTestStage");
        GameObject chunkPrefab = null;

#if UNITY_EDITOR
        chunkPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Rooms/Room_TestDummy.prefab");
#endif

        if (chunkPrefab == null && ResourceManager.Instance != null)
        {
            try
            {
                var tcs = new UniTaskCompletionSource<GameObject>();
                ResourceManager.Instance.LoadAssetAsync<GameObject>(RoomChunkAddressableKey, prefab =>
                {
                    tcs.TrySetResult(prefab);
                });

                chunkPrefab = await tcs.Task;
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"[DummyStageBuilder] Addressables Key '{RoomChunkAddressableKey}' 미등록 감지: {ex.Message}");
            }
        }

        bool loadedFromPrefab = false;
        if (chunkPrefab != null)
        {
            GameObject spawnedChunk = Instantiate(chunkPrefab, rootObj.transform);
            spawnedChunk.name = "Room_Chunk_Instance";
            loadedFromPrefab = true;

            fixChunkSpritesIfNeeded(spawnedChunk);
            Debug.Log($"<color=green>[DummyStageBuilder] '{RoomChunkAddressableKey}' 청크 프리팹 로드 & 스폰 완결!</color>");
        }

        if (!loadedFromPrefab)
        {
            buildFallbackRoomChunk(rootObj.transform);
        }

        if (BufferTimeSec > 0f)
        {
            await UniTask.Delay(System.TimeSpan.FromSeconds(BufferTimeSec), cancellationToken: cancellationToken);
        }

        await fadeInScreenAsync(cancellationToken);

        Debug.Log("<color=green>[DummyStageBuilder] 버퍼 시간 종료 및 화면 페이드 인 전개 완결!</color>");
    }

    private void buildFallbackRoomChunk(Transform parent)
    {
        // Ground Base
        GameObject groundObj = createPoolableObject("Ground_Base", parent, new Vector3(0f, -0.5f, 0f));
        var groundCol = getOrAddComponent<BoxCollider2D>(groundObj);
        groundCol.size = new Vector2(RoomSize.x, 1.0f);
        var groundSprite = getOrAddComponent<SpriteRenderer>(groundObj);
        groundSprite.sprite = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), 16f);
        groundSprite.color = new Color(0.25f, 0.28f, 0.32f, 1.0f);

        // 1. Left Wall (표준 벽점프: 동일 벽 연속 점프 허용)
        GameObject leftWall = createPoolableObject("Wall_Left_Standard", parent, new Vector3(-RoomSize.x * 0.5f, RoomSize.y * 0.5f, 0f));
        var leftCol = getOrAddComponent<BoxCollider2D>(leftWall);
        leftCol.size = new Vector2(1.0f, RoomSize.y);
        var leftSurf = getOrAddComponent<WallJumpSurface>(leftWall);
        leftSurf.CanWallJump = true;
        leftSurf.AllowSameWall = true;

        // 2. Right Wall (교차 벽점프 전용: 동일 벽 연속 점프 불가)
        GameObject rightWall = createPoolableObject("Wall_Right_AlternateOnly", parent, new Vector3(RoomSize.x * 0.5f, RoomSize.y * 0.5f, 0f));
        var rightCol = getOrAddComponent<BoxCollider2D>(rightWall);
        rightCol.size = new Vector2(1.0f, RoomSize.y);
        var rightSurf = getOrAddComponent<WallJumpSurface>(rightWall);
        rightSurf.CanWallJump = true;
        rightSurf.AllowSameWall = false;

        // 3. Center Red Wall (벽점프 금지 구역)
        GameObject noJumpWall = createPoolableObject("Wall_Center_NoJump", parent, new Vector3(-7f, 5.0f, 0f));
        var noJumpCol = getOrAddComponent<BoxCollider2D>(noJumpWall);
        noJumpCol.size = new Vector2(1.0f, 8.0f);
        var noJumpSurf = getOrAddComponent<WallJumpSurface>(noJumpWall);
        noJumpSurf.CanWallJump = false;
        var noJumpSprite = getOrAddComponent<SpriteRenderer>(noJumpWall);
        noJumpSprite.sprite = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), 16f);
        noJumpSprite.color = new Color(0.85f, 0.2f, 0.2f, 0.9f); // 빨간색: 벽점프 불가

        // 4. Center Cyan Wall (얼음 미끄럼 벽: 빠른 슬라이딩 배율 2.5x)
        GameObject iceWall = createPoolableObject("Wall_Center_IceSlide", parent, new Vector3(7f, 5.0f, 0f));
        var iceCol = getOrAddComponent<BoxCollider2D>(iceWall);
        iceCol.size = new Vector2(1.0f, 8.0f);
        var iceSurf = getOrAddComponent<WallJumpSurface>(iceWall);
        iceSurf.CanWallJump = true;
        iceSurf.SlideSpeedMultiplier = 2.5f;
        var iceSprite = getOrAddComponent<SpriteRenderer>(iceWall);
        iceSprite.sprite = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), 16f);
        iceSprite.color = new Color(0.2f, 0.85f, 1.0f, 0.9f); // 하늘색: 빠른 슬라이딩

        // Step Platforms
        createPlatform(parent, "Platform_Low", new Vector3(-3f, 2.5f, 0f), new Vector2(4f, 0.4f));
        createPlatform(parent, "Platform_Mid", new Vector3(0f, 5.0f, 0f), new Vector2(4f, 0.4f));
        createPlatform(parent, "Platform_High", new Vector3(3f, 7.5f, 0f), new Vector2(4f, 0.4f));

        // Hazard Spikes
        GameObject hazardObj = createPoolableObject("Hazard_Spikes", parent, new Vector3(11f, 0.2f, 0f));
        var hazardCol = getOrAddComponent<BoxCollider2D>(hazardObj);
        hazardCol.size = new Vector2(4f, 0.4f);
        hazardCol.isTrigger = true;
        var hazardSprite = getOrAddComponent<SpriteRenderer>(hazardObj);
        hazardSprite.sprite = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), 16f);
        hazardSprite.color = new Color(0.9f, 0.2f, 0.2f, 0.8f);
    }

    private void fixChunkSpritesIfNeeded(GameObject chunkInstance)
    {
        if (chunkInstance == null) return;

        var renderers = chunkInstance.GetComponentsInChildren<SpriteRenderer>(true);
        foreach (var rend in renderers)
        {
            if (rend.sprite == null)
            {
                string objName = rend.gameObject.name;
                if (objName.Contains("Ground"))
                {
                    bindSprite(rend, "Assets/Textures/Environment/Tile_Terrain_Ground.png", new Color(0.25f, 0.28f, 0.32f, 1.0f));
                }
                else if (objName.Contains("Platform"))
                {
                    bindSprite(rend, "Assets/Textures/Environment/Tile_Platform_OneWay.png", new Color(0.1f, 0.7f, 0.85f, 1.0f));
                }
                else if (objName.Contains("Hazard"))
                {
                    bindSprite(rend, "Assets/Textures/Environment/Tile_Hazard_SpikesLava.png", new Color(0.9f, 0.2f, 0.2f, 0.8f));
                }
                else if (objName.Contains("Door") || objName.Contains("Chest"))
                {
                    bindSprite(rend, "Assets/Textures/Environment/Sprite_Structures_Interactive.png", new Color(0.6f, 0.3f, 0.8f, 1.0f));
                }
            }
        }
    }

    private void bindSprite(SpriteRenderer renderer, string texturePath, Color fallbackColor)
    {
        if (renderer == null) return;

        Sprite loadedSprite = null;
#if UNITY_EDITOR
        var assets = UnityEditor.AssetDatabase.LoadAllAssetsAtPath(texturePath);
        if (assets != null && assets.Length > 0)
        {
            foreach (var a in assets)
            {
                if (a is Sprite sp)
                {
                    loadedSprite = sp;
                    break;
                }
            }
        }

        if (loadedSprite == null)
        {
            loadedSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(texturePath);
        }
#endif
        if (loadedSprite != null)
        {
            renderer.sprite = loadedSprite;
            renderer.color = Color.white;
        }
        else
        {
            renderer.color = fallbackColor;
        }
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

    private void createPlatform(Transform parent, string name, Vector3 pos, Vector2 size)
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

        // 발판 옆면 벽점프 방지 세이프티
        var surf = getOrAddComponent<WallJumpSurface>(platObj);
        surf.CanWallJump = false;

        var sprite = getOrAddComponent<SpriteRenderer>(platObj);
        sprite.sprite = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), 16f);
        sprite.color = new Color(0.1f, 0.7f, 0.85f, 0.9f);
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

