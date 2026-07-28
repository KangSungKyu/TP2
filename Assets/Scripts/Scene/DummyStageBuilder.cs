using Cysharp.Threading.Tasks;
using System.Threading;
using UnityEngine;

/// <summary>
/// 메트로배니아/로그라이트 물리 점프, 발판, 가시 함정 및 오브젝트 테스트용 더미 스테이지 생성 클래스.
/// 모든 동적 오브젝트를 풀링으로 재활용 관리하며, 생성 완료 후 버퍼 시간(Buffer Time)과 페이드 인 연출을 거쳐 화면을 보여줍니다.
/// </summary>
public class DummyStageBuilder : MonoBehaviour
{
    [Header("Test Environment Settings")]
    public bool AutoBuildOnStart = false;
    public Vector2 RoomSize = new Vector2(30f, 18f);

    [Header("Buffer & Fade Settings")]
    public float BufferTimeSec = 0.5f; // 스테이지 구성 완료 후 렌더링 정돈 대기 버퍼 시간 (초)
    public float FadeDurationSec = 0.4f; // 화면 페이드 인 연출 시간 (초)

    private CanvasGroup fadeOverlayCanvasGroup;

    private void Start()
    {
        if (this.AutoBuildOnStart)
        {
            this.BuildDummyStageAsync(this.GetCancellationTokenOnDestroy()).Forget();
        }
    }

    public async UniTask BuildDummyStageAsync(CancellationToken cancellationToken = default)
    {
        // 1. 화면 암전 처리 (Black Curtain Overlay)
        this.setupFadeOverlay();
        if (this.fadeOverlayCanvasGroup != null)
        {
            this.fadeOverlayCanvasGroup.alpha = 1f;
        }

        // 2. 기존 스테이지 오브젝트가 있다면 풀링/정리
        GameObject existingStage = GameObject.Find("DummyTestStage");
        if (existingStage != null)
        {
            Destroy(existingStage);
            await UniTask.Yield(PlayerLoopTiming.Update, cancellationToken);
        }

        // 3. 풀링 기반 동적 스테이지 룸 오브젝트 조립
        GameObject rootObj = new GameObject("DummyTestStage");

        // 3-1. 바닥 지형 (Ground Base)
        GameObject groundObj = this.createPoolableObject("Ground_Base", rootObj.transform, new Vector3(0f, -0.5f, 0f));
        var groundCol = this.getOrAddComponent<BoxCollider2D>(groundObj);
        groundCol.size = new Vector2(this.RoomSize.x, 1.0f);
        var groundSprite = this.getOrAddComponent<SpriteRenderer>(groundObj);
        this.bindSprite(groundSprite, "Assets/Textures/Environment/Tile_Terrain_Ground.png", new Color(0.25f, 0.28f, 0.32f, 1.0f));

        // 3-2. 좌/우 벽 (Left & Right Walls - Friction 0)
        GameObject leftWall = this.createPoolableObject("Wall_Left", rootObj.transform, new Vector3(-this.RoomSize.x * 0.5f, this.RoomSize.y * 0.5f, 0f));
        var leftCol = this.getOrAddComponent<BoxCollider2D>(leftWall);
        leftCol.size = new Vector2(1.0f, this.RoomSize.y);

        GameObject rightWall = this.createPoolableObject("Wall_Right", rootObj.transform, new Vector3(this.RoomSize.x * 0.5f, this.RoomSize.y * 0.5f, 0f));
        var rightCol = this.getOrAddComponent<BoxCollider2D>(rightWall);
        rightCol.size = new Vector2(1.0f, this.RoomSize.y);

        // 3-3. 계단형 발판 (OneWay Step Platforms: Low, Mid, High)
        this.createPlatform(rootObj.transform, "Platform_Low", new Vector3(-5f, 2.5f, 0f), new Vector2(4f, 0.4f));
        this.createPlatform(rootObj.transform, "Platform_Mid", new Vector3(0f, 5.0f, 0f), new Vector2(4f, 0.4f));
        this.createPlatform(rootObj.transform, "Platform_High", new Vector3(5f, 7.5f, 0f), new Vector2(4f, 0.4f));

        // 3-4. 가시/함정 구역 (Hazard Spike Zone)
        GameObject hazardObj = this.createPoolableObject("Hazard_Spikes", rootObj.transform, new Vector3(10f, 0.2f, 0f));
        var hazardCol = this.getOrAddComponent<BoxCollider2D>(hazardObj);
        hazardCol.size = new Vector2(5f, 0.4f);
        hazardCol.isTrigger = true;
        var hazardSprite = this.getOrAddComponent<SpriteRenderer>(hazardObj);
        this.bindSprite(hazardSprite, "Assets/Textures/Environment/Tile_Hazard_SpikesLava.png", new Color(0.9f, 0.2f, 0.2f, 0.8f));

        // 3-5. 구조물 (Door & Chest 더미)
        GameObject doorObj = this.createPoolableObject("Door_Exit", rootObj.transform, new Vector3(12f, 1.2f, 0f));
        var doorSprite = this.getOrAddComponent<SpriteRenderer>(doorObj);
        this.bindSprite(doorSprite, "Assets/Textures/Environment/Sprite_Structures_Interactive.png", new Color(0.6f, 0.3f, 0.8f, 1.0f));

        GameObject chestObj = this.createPoolableObject("Chest_Treasure", rootObj.transform, new Vector3(5f, 8.1f, 0f));
        var chestSprite = this.getOrAddComponent<SpriteRenderer>(chestObj);
        this.bindSprite(chestSprite, "Assets/Textures/Environment/Sprite_Structures_Interactive.png", new Color(0.9f, 0.8f, 0.2f, 1.0f));

        Debug.Log("<color=cyan>[DummyStageBuilder] 풀링 및 텍스처 바인딩 기반 스테이지 동적 조립 완료! 버퍼 대기 개시...</color>");

        // 4. 버퍼 시간 (Buffer Time) 대기 (물리/카메라/프레임 안정을 위한 0.5초 버퍼)
        if (this.BufferTimeSec > 0f)
        {
            await UniTask.Delay(System.TimeSpan.FromSeconds(this.BufferTimeSec), cancellationToken: cancellationToken);
        }

        // 5. 화면 페이드 인 연출 (Black Overlay Fade Out)
        await this.fadeInScreenAsync(cancellationToken);

        Debug.Log("<color=green>[DummyStageBuilder] 버퍼 시간 종료 및 화면 페이드 인 전개 완결!</color>");
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
            renderer.color = Color.white; // 원본 아트 텍스처 보존
        }
        else
        {
            renderer.color = fallbackColor;
        }
    }

    private void createPlatform(Transform parent, string name, Vector3 pos, Vector2 size)
    {
        GameObject platObj = this.createPoolableObject(name, parent, pos);
        
        var col = this.getOrAddComponent<BoxCollider2D>(platObj);
        col.size = size;
        
        var effector = this.getOrAddComponent<PlatformEffector2D>(platObj);
        col.usedByEffector = true;

        var sprite = this.getOrAddComponent<SpriteRenderer>(platObj);
        this.bindSprite(sprite, "Assets/Textures/Environment/Tile_Platform_OneWay.png", new Color(0.1f, 0.7f, 0.85f, 0.9f));
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
            elapsed += Time.deltaTime;
            float t = elapsed / duration;
            this.fadeOverlayCanvasGroup.alpha = Mathf.Lerp(1f, 0f, t);
            await UniTask.Yield(PlayerLoopTiming.Update, cancellationToken);
        }

        this.fadeOverlayCanvasGroup.alpha = 0f;
        Destroy(this.fadeOverlayCanvasGroup.gameObject);
    }
}
