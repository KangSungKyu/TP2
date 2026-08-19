using Cysharp.Threading.Tasks;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 룸/스테이지 관문 포탈 컴포넌트.
/// 플레이어가 포탈에 접촉하거나 상호작용 시 정수 TargetRoomResourceIdx 참조 기반으로 다음 룸 청크 비동기 전환을 유도합니다.
/// </summary>
public class RoomDoorPortal : MonoBehaviour
{
    [Header("Portal Settings")]
    [SerializeField] private uint doorIdx;
    [SerializeField] private uint sourceChunkResourceIdx;
    [SerializeField] private uint destinationDoorIdx;
    public uint TargetRoomResourceIdx = 1041; // Default: 1041 (Tilemap_Room_Stage1_Battle)
    public byte TargetSlotIdx = byte.MaxValue;
    public bool AutoTriggerOnTouch = false;
    public bool ShowPrototypeDestination = true;
    public uint DestinationChunkResourceIdx { get; private set; }
    public uint DoorIdx => doorIdx;
    public uint SourceChunkResourceIdx => sourceChunkResourceIdx;
    public uint DestinationDoorIdx => destinationDoorIdx;
    public byte OwnerSlotIdx { get; private set; } = byte.MaxValue;
    public uint RoomGeneration { get; private set; }

    private bool isTransitioning = false;
    private bool requiresTriggerExit;
    private readonly HashSet<Collider2D> playerCandidates = new HashSet<Collider2D>();
    private int lastInteractionFrame = -1;
    [SerializeField] private TextMeshPro destinationLabel;

    private void Start()
    {
        // ponytail: visualize portal door with cyan glowing indicator
        EnsureVisualOverlay();
        RefreshDestinationLabel();
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

    private void Update()
    {
        if (TryConsumeInteraction(WasInteractionPressedThisFrame())) TriggerRoomTransitionAsync().Forget();
    }

    public static bool WasInteractionPressedThisFrame()
    {
        Keyboard keyboard = Keyboard.current;
        return keyboard != null && (keyboard.wKey.wasPressedThisFrame || keyboard.upArrowKey.wasPressedThisFrame);
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (Player.Instance != null &&
            (collision.transform == Player.Instance.transform || collision.transform.IsChildOf(Player.Instance.transform)))
            playerCandidates.Add(collision);
    }

    private void OnTriggerExit2D(Collider2D collision)
    {
        playerCandidates.Remove(collision);
        if (playerCandidates.Count == 0) requiresTriggerExit = false;
    }

    private void OnDisable()
    {
        playerCandidates.Clear();
        isTransitioning = false;
        requiresTriggerExit = false;
    }

    private bool TryConsumeInteraction(bool pressed)
    {
        Player player = Player.Instance;
        if (!pressed || !isActiveAndEnabled || isTransitioning || requiresTriggerExit || playerCandidates.Count == 0 ||
            player == null || player.Motor == null || !player.Motor.IsGrounded ||
            lastInteractionFrame == Time.frameCount) return false;
        lastInteractionFrame = Time.frameCount;
        return true;
    }

    public async UniTaskVoid TriggerRoomTransitionAsync()
    {
        if (isTransitioning) return;
        isTransitioning = true;
        requiresTriggerExit = true;
        bool transitioned = false;

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
                    transitioned = await stageManager.LoadNextRoomAsync(1042);
                }
                else if (!(transitioned = await stageManager.LoadConnectedRoomAsync(TargetSlotIdx)))
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
            transitioned = await stageManager.LoadNextRoomAsync(TargetRoomResourceIdx);
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
            if (!transitioned) requiresTriggerExit = false;
        }
    }

    public void Configure(byte targetSlotIdx, byte ownerSlotIdx, uint roomGeneration,
        uint destinationChunkResourceIdx = 0)
    {
        TargetSlotIdx = targetSlotIdx;
        OwnerSlotIdx = ownerSlotIdx;
        RoomGeneration = roomGeneration;
        DestinationChunkResourceIdx = destinationChunkResourceIdx;
        RefreshDestinationLabel();
    }

    public void ConfigureDoor(uint idx, uint sourceChunkIdx, uint destinationChunkIdx, uint targetDoorIdx,
        byte targetSlotIdx, byte ownerSlotIdx, uint roomGeneration)
    {
        doorIdx = idx;
        sourceChunkResourceIdx = sourceChunkIdx;
        destinationDoorIdx = targetDoorIdx;
        Configure(targetSlotIdx, ownerSlotIdx, roomGeneration, destinationChunkIdx);
    }

    public void SetDestinationLabelVisible(bool visible)
    {
        ShowPrototypeDestination = visible;
        RefreshDestinationLabel();
    }

    public string GetDestinationLabelText() =>
        DestinationChunkResourceIdx == 0 ? string.Empty : $"Chunk {DestinationChunkResourceIdx}";

    private void RefreshDestinationLabel()
    {
        if (destinationLabel == null) return;
        destinationLabel.gameObject.SetActive(ShowPrototypeDestination && DestinationChunkResourceIdx != 0);
        if (destinationLabel.gameObject.activeSelf)
            destinationLabel.SetText("Chunk {0}", DestinationChunkResourceIdx);
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
