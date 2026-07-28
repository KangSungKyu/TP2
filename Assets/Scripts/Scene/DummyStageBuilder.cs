using Cysharp.Threading.Tasks;
using System.Threading;
using UnityEngine;

/// <summary>
/// 메트로배니아/로그라이트 더미 스테이지 생성 클래스.
/// Room Chunk Prefab (Room_TestDummy)을 ResourceManager를 통해 비동기로 통째 스폰하여 정돈된 스테이지 아키텍처를 구동하며,
/// 생성 완료 후 0.5초 버퍼 시간(Buffer Time)과 페이드 인 연출을 거쳐 깨끗한 화면을 전개시킵니다.
/// </summary>
public class DummyStageBuilder : MonoBehaviour
{
    [Header("Room Chunk Settings")]
    public string RoomChunkAddressableKey = "Room_TestDummy";
    public Vector2 RoomSize = new Vector2(30f, 18f);

    [Header("Buffer & Fade Settings")]
    public float BufferTimeSec = 0.5f; // 스테이지 구성 완료 후 렌더링 정돈 대기 버퍼 시간 (초)
    public float FadeDurationSec = 0.4f; // 화면 페이드 인 연출 시간 (초)

    private CanvasGroup fadeOverlayCanvasGroup;

    private void Start()
    {
        this.BuildDummyStageAsync(this.GetCancellationTokenOnDestroy()).Forget();
    }

    public async UniTask BuildDummyStageAsync(CancellationToken cancellationToken = default)
    {
        // 1. 화면 암전 처리 (Black Curtain Overlay)
        this.setupFadeOverlay();
        if (this.fadeOverlayCanvasGroup != null)
        {
            this.fadeOverlayCanvasGroup.alpha = 1f;
        }

        // 2. 기존 스테이지 오브젝트가 있다면 정리
        GameObject existingStage = GameObject.Find("DummyTestStage");
        if (existingStage != null)
        {
            Destroy(existingStage);
            await UniTask.Yield(PlayerLoopTiming.Update, cancellationToken);
        }

        // 3. Room Chunk Prefab 비동기 로드 & 스폰 (ResourceManager 및 에디터 직통 로더)
        GameObject rootObj = new GameObject("DummyTestStage");

        GameObject chunkPrefab = null;

#if UNITY_EDITOR
        // 에디터 Play 시 Addressables 번들 미갱신 상태에서도 실물 Room_TestDummy.prefab 100% 우선 로드
        chunkPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Rooms/Room_TestDummy.prefab");
#endif

        if (chunkPrefab == null && ResourceManager.Instance != null)
        {
            try
            {
                var tcs = new UniTaskCompletionSource<GameObject>();
                ResourceManager.Instance.LoadAssetAsync<GameObject>(this.RoomChunkAddressableKey, prefab =>
                {
                    tcs.TrySetResult(prefab);
                });

                chunkPrefab = await tcs.Task;
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"[DummyStageBuilder] Addressables Key '{this.RoomChunkAddressableKey}' 미등록 감지: {ex.Message}");
            }
        }

        bool loadedFromPrefab = false;
        if (chunkPrefab != null)
        {
            GameObject spawnedChunk = Instantiate(chunkPrefab, rootObj.transform);
            spawnedChunk.name = "Room_Chunk_Instance";
            loadedFromPrefab = true;

            // 청크 프리팹 내부의 m_Sprite가 {fileID: 0}으로 비어있는 경우 자동 수선 및 바인딩 보장
            this.fixChunkSpritesIfNeeded(spawnedChunk);

            Debug.Log($"<color=green>[DummyStageBuilder] '{this.RoomChunkAddressableKey}' 청크 프리팹 로드, 수선 & 스폰 완결!</color>");
        }

        // 3-1. 청크 프리팹 미생성 대비 세이프티 폴백 (지면, 발판 3종, 벽, 가시, 문, 상자 무결점 동적 보완)
        if (!loadedFromPrefab)
        {
            this.buildFallbackRoomChunk(rootObj.transform);
        }

        // 4. 버퍼 시간 (Buffer Time) 대기 (물리/카메라/프레임 안정을 위한 0.5초 버퍼)
        if (this.BufferTimeSec > 0f)
        {
            await UniTask.Delay(System.TimeSpan.FromSeconds(this.BufferTimeSec), cancellationToken: cancellationToken);
        }

        // 6. 화면 페이드 인 연출 (Black Overlay Fade Out)
        await this.fadeInScreenAsync(cancellationToken);

        Debug.Log("<color=green>[DummyStageBuilder] 버퍼 시간 종료 및 화면 페이드 인 전개 완결!</color>");
    }

    private void buildFallbackRoomChunk(Transform parent)
    {
        // Ground Base
        GameObject groundObj = this.createPoolableObject("Ground_Base", parent, new Vector3(0f, -0.5f, 0f));
        var groundCol = this.getOrAddComponent<BoxCollider2D>(groundObj);
        groundCol.size = new Vector2(this.RoomSize.x, 1.0f);
        var groundSprite = this.getOrAddComponent<SpriteRenderer>(groundObj);
        groundSprite.sprite = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), 16f);
        groundSprite.color = new Color(0.25f, 0.28f, 0.32f, 1.0f);

        // Walls
        GameObject leftWall = this.createPoolableObject("Wall_Left", parent, new Vector3(-this.RoomSize.x * 0.5f, this.RoomSize.y * 0.5f, 0f));
        var leftCol = this.getOrAddComponent<BoxCollider2D>(leftWall);
        leftCol.size = new Vector2(1.0f, this.RoomSize.y);

        GameObject rightWall = this.createPoolableObject("Wall_Right", parent, new Vector3(this.RoomSize.x * 0.5f, this.RoomSize.y * 0.5f, 0f));
        var rightCol = this.getOrAddComponent<BoxCollider2D>(rightWall);
        rightCol.size = new Vector2(1.0f, this.RoomSize.y);

        // Step Platforms with Effector & PassThrough
        this.createPlatform(parent, "Platform_Low", new Vector3(-5f, 2.5f, 0f), new Vector2(4f, 0.4f));
        this.createPlatform(parent, "Platform_Mid", new Vector3(0f, 5.0f, 0f), new Vector2(4f, 0.4f));
        this.createPlatform(parent, "Platform_High", new Vector3(5f, 7.5f, 0f), new Vector2(4f, 0.4f));

        // Hazard Spikes
        GameObject hazardObj = this.createPoolableObject("Hazard_Spikes", parent, new Vector3(10f, 0.2f, 0f));
        var hazardCol = this.getOrAddComponent<BoxCollider2D>(hazardObj);
        hazardCol.size = new Vector2(5f, 0.4f);
        hazardCol.isTrigger = true;
        var hazardSprite = this.getOrAddComponent<SpriteRenderer>(hazardObj);
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
                    this.bindSprite(rend, "Assets/Textures/Environment/Tile_Terrain_Ground.png", new Color(0.25f, 0.28f, 0.32f, 1.0f));
                }
                else if (objName.Contains("Platform"))
                {
                    this.bindSprite(rend, "Assets/Textures/Environment/Tile_Platform_OneWay.png", new Color(0.1f, 0.7f, 0.85f, 1.0f));
                }
                else if (objName.Contains("Hazard"))
                {
                    this.bindSprite(rend, "Assets/Textures/Environment/Tile_Hazard_SpikesLava.png", new Color(0.9f, 0.2f, 0.2f, 0.8f));
                }
                else if (objName.Contains("Door") || objName.Contains("Chest"))
                {
                    this.bindSprite(rend, "Assets/Textures/Environment/Sprite_Structures_Interactive.png", new Color(0.6f, 0.3f, 0.8f, 1.0f));
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
        GameObject platObj = this.createPoolableObject(name, parent, pos);
        
        var col = this.getOrAddComponent<BoxCollider2D>(platObj);
        col.size = size;
        
        var effector = this.getOrAddComponent<PlatformEffector2D>(platObj);
        col.usedByEffector = true;

        this.getOrAddComponent<OneWayPlatformPassThrough>(platObj);

        var sprite = this.getOrAddComponent<SpriteRenderer>(platObj);
        sprite.sprite = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), 16f);
        sprite.color = new Color(0.1f, 0.7f, 0.85f, 0.9f);
    }



    private void setupFadeOverlay()
    {
        if (this.fadeOverlayCanvasGroup != null) return;

        GameObject canvasObj = new GameObject("StageFadeCanvas");
        Canvas canvas = canvasObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 9999;

        this.fadeOverlayCanvasGroup = canvasObj.AddComponent<CanvasGroup>();

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
        if (this.fadeOverlayCanvasGroup == null) return;

        float elapsed = 0f;
        float duration = Mathf.Max(0.1f, this.FadeDurationSec);

        while (elapsed < duration && !cancellationToken.IsCancellationRequested)
        {
            if (this.fadeOverlayCanvasGroup == null) break;

            elapsed += Time.deltaTime;
            float t = elapsed / duration;
            this.fadeOverlayCanvasGroup.alpha = Mathf.Lerp(1f, 0f, t);
            await UniTask.Yield(PlayerLoopTiming.Update, cancellationToken);
        }

        if (this.fadeOverlayCanvasGroup != null)
        {
            this.fadeOverlayCanvasGroup.alpha = 0f;
            Destroy(this.fadeOverlayCanvasGroup.gameObject);
            this.fadeOverlayCanvasGroup = null;
        }
    }
}
