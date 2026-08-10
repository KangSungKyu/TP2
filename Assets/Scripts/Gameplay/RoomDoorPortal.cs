using Cysharp.Threading.Tasks;
using UnityEngine;

/// <summary>
/// 룸/스테이지 관문 포탈 컴포넌트.
/// 플레이어가 포탈에 접촉하거나 상호작용 시 정수 TargetRoomResourceIdx 참조 기반으로 다음 룸 청크 비동기 전환을 유도합니다.
/// </summary>
public class RoomDoorPortal : MonoBehaviour
{
    [Header("Portal Settings")]
    public uint TargetRoomResourceIdx = 1041; // Default: 1041 (Tilemap_Room_Stage1_Battle)
    public byte TargetSlotIdx = byte.MaxValue;
    public bool AutoTriggerOnTouch = true;
    public byte OwnerSlotIdx { get; private set; } = byte.MaxValue;
    public uint RoomGeneration { get; private set; }

    private bool isTransitioning = false;

    private void Start()
    {
        // ponytail: visualize portal door with cyan glowing indicator
        EnsureVisualOverlay();
    }

    private void EnsureVisualOverlay()
    {
        var sr = GetComponent<SpriteRenderer>();
        if (sr == null)
        {
            sr = gameObject.AddComponent<SpriteRenderer>();
            Texture2D tex = Texture2D.whiteTexture;
            sr.sprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f);
            sr.color = new Color(0f, 0.9f, 1f, 0.65f);
            transform.localScale = new Vector3(1.2f, 2.5f, 1f);
        }

        var col = GetComponent<Collider2D>();
        if (col == null)
        {
            var box = gameObject.AddComponent<BoxCollider2D>();
            box.isTrigger = true;
            box.size = new Vector2(1.5f, 2.8f);
        }
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = new Color(0f, 0.9f, 1f, 0.8f);
        Gizmos.DrawWireCube(transform.position, new Vector3(1.5f, 2.8f, 0f));
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (!AutoTriggerOnTouch || isTransitioning) return;

        if (Player.Instance != null &&
            (collision.transform == Player.Instance.transform || collision.transform.IsChildOf(Player.Instance.transform)))
        {
            TriggerRoomTransitionAsync().Forget();
        }
    }

    public async UniTaskVoid TriggerRoomTransitionAsync()
    {
        if (isTransitioning) return;
        isTransitioning = true;

        try
        {
            if (StageManager.Instance == null)
            {
                Debug.LogError("[RoomDoorPortal] StageManager is unavailable.");
                return;
            }

            StageManager stageManager = StageManager.Instance;
            if (stageManager.CurrentRun != null)
            {
                if (!TryAcquireTransition(stageManager)) return;
                if (stageManager.CurrentRun.CurrentSlotIdx == stageManager.CurrentRun.BossGateSlotIdx &&
                    TargetRoomResourceIdx == 1042)
                {
                    await stageManager.LoadNextRoomAsync(1042);
                }
                else if (!await stageManager.LoadConnectedRoomAsync(TargetSlotIdx))
                {
                    stageManager.CancelPortalTransition(OwnerSlotIdx, RoomGeneration);
                }
                return;
            }

            if (TargetRoomResourceIdx == 0)
            {
                Debug.LogError("[RoomDoorPortal] Invalid fallback room idx 0.");
                return;
            }
            await stageManager.LoadNextRoomAsync(TargetRoomResourceIdx);
        }
        catch (System.Exception exception)
        {
            if (StageManager.Instance != null)
                StageManager.Instance.CancelPortalTransition(OwnerSlotIdx, RoomGeneration);
            Debug.LogException(exception, this);
        }
        finally
        {
            isTransitioning = false;
        }
    }

    public void Configure(byte targetSlotIdx, byte ownerSlotIdx, uint roomGeneration)
    {
        TargetSlotIdx = targetSlotIdx;
        OwnerSlotIdx = ownerSlotIdx;
        RoomGeneration = roomGeneration;
    }

    public bool TryAcquireTransition(StageManager stageManager)
    {
        if (stageManager == null || !stageManager.IsCurrentPortal(OwnerSlotIdx, RoomGeneration))
        {
            gameObject.SetActive(false);
            return false;
        }
        return stageManager.TryBeginPortalTransition(OwnerSlotIdx, RoomGeneration);
    }
}
