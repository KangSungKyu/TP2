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
    public bool AutoTriggerOnTouch = true;

    private bool isTransitioning = false;

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
            if (TargetRoomResourceIdx == 0 || StageManager.Instance == null)
            {
                Debug.LogError($"[RoomDoorPortal] Invalid transition target idx: {TargetRoomResourceIdx}");
                return;
            }

            await StageManager.Instance.LoadNextRoomAsync(TargetRoomResourceIdx);
        }
        catch (System.Exception exception)
        {
            Debug.LogException(exception, this);
        }
        finally
        {
            isTransitioning = false;
        }
    }
}
