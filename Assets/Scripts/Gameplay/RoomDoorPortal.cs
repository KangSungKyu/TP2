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
    public string TargetRoomKey = "Tilemap_Room_Stage1_Battle";
    public bool AutoTriggerOnTouch = true;

    private bool isTransitioning = false;

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (!AutoTriggerOnTouch || isTransitioning) return;

        if (collision.CompareTag("Player") || collision.GetComponent<Player>() != null || collision.GetComponentInParent<Player>() != null || collision.name.Contains("Player"))
        {
            TriggerRoomTransitionAsync().Forget();
        }
    }

    public async UniTaskVoid TriggerRoomTransitionAsync()
    {
        if (isTransitioning) return;
        isTransitioning = true;

        Debug.Log($"<color=cyan>[RoomDoorPortal] 포탈 관문 작동! 다음 룸 (ResourceIdx: {TargetRoomResourceIdx}, Key: '{TargetRoomKey}') 비동기 전환 개시...</color>");

        if (StageManager.Instance != null)
        {
            if (TargetRoomResourceIdx > 0)
            {
                await StageManager.Instance.LoadNextRoomAsync(TargetRoomResourceIdx);
            }
            else
            {
                await StageManager.Instance.LoadNextRoomAsync(TargetRoomKey);
            }
        }
        else
        {
            var builder = Object.FindObjectOfType<TilemapStageBuilder>();
            if (builder != null)
            {
                string addressKey = TargetRoomKey;
                if (TargetRoomResourceIdx > 0 && StageManager.Instance != null)
                {
                    addressKey = StageManager.Instance.ResolveAddressableKey(TargetRoomResourceIdx);
                }
                builder.TilemapAddressableKey = addressKey;
                await builder.BuildTilemapStageAsync();
            }
        }

        isTransitioning = false;
    }
}
