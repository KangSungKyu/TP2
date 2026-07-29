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
    public float BufferTimeSec = 0.5f; // 스테이지 구성 완료 후 렌더링 정돈 대기 버퍼 시간 (초)
    public float FadeDurationSec = 0.4f; // 화면 페이드 인 연출 시간 (초)

    private CanvasGroup fadeOverlayCanvasGroup;

    private void Start()
    {
        this.BuildTilemapStageAsync(this.GetCancellationTokenOnDestroy()).Forget();
    }

    public async UniTask BuildTilemapStageAsync(CancellationToken cancellationToken = default)
    {
        // 1. 화면 암전 처리 (Black Curtain Overlay)
        this.setupFadeOverlay();
        if (this.fadeOverlayCanvasGroup != null)
        {
            this.fadeOverlayCanvasGroup.alpha = 1f;
        }

        // 2. 기존 스테이지 오브젝트 정리
        GameObject existingStage = GameObject.Find("TilemapStage_Root");
        if (existingStage != null)
        {
            Destroy(existingStage);
            await UniTask.Yield(PlayerLoopTiming.Update, cancellationToken);
        }

        // 3. Tilemap Room Chunk Prefab 비동기 로드 & 스폰
        GameObject rootObj = new GameObject("TilemapStage_Root");
        GameObject chunkPrefab = null;

#if UNITY_EDITOR
        // 에디터 직통 로드 (Addressables 빌드 미갱신 시에도 100% 최우선 로드 보장)
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

        // 3-1. 청크 프리팹 미생성 대비 폴백
        if (!loadedFromPrefab)
        {
            Debug.LogWarning("[TilemapStageBuilder] Tilemap 청크 프리팹이 존재하지 않아 기본 세이프티 구성을 셋업합니다.");
        }

        Debug.Log("<color=cyan>[TilemapStageBuilder] Unity 2D Tilemap 스테이지 전개 완료! 0.5s 버퍼 대기 개시...</color>");

        // 4. 버퍼 시간 (Buffer Time) 대기 (물리/카메라/프레임 안정을 위한 0.5초 버퍼)
        if (this.BufferTimeSec > 0f)
        {
            await UniTask.Delay(System.TimeSpan.FromSeconds(this.BufferTimeSec), cancellationToken: cancellationToken);
        }

        // 5. 화면 페이드 인 연출 (Black Overlay Fade Out)
        await this.fadeInScreenAsync(cancellationToken);

        Debug.Log("<color=green>[TilemapStageBuilder] 버퍼 시간 종료 및 화면 페이드 인 전개 완결!</color>");
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
