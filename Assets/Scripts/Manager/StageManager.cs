using Cysharp.Threading.Tasks;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;

/// <summary>
/// 1스테이지 초심자용 룸 청크 시퀀스 & 룸 전환 총괄 매니저.
/// </summary>
public class StageManager : Singleton<StageManager>
{
    [Header("Stage 1 Room Sequence")]
    public List<string> Stage1RoomSequence = new List<string>
    {
        "Tilemap_Room_Stage1_Entry",
        "Tilemap_Room_Stage1_Battle",
        "Tilemap_Room_Stage1_Boss"
    };

    public int CurrentRoomIndex { get; private set; } = 0;
    public string CurrentRoomKey => (CurrentRoomIndex >= 0 && CurrentRoomIndex < Stage1RoomSequence.Count) 
        ? Stage1RoomSequence[CurrentRoomIndex] 
        : "Tilemap_Room_Stage1_Entry";

    public async UniTask LoadNextRoomAsync(string roomKey = null, CancellationToken cancellationToken = default)
    {
        if (!string.IsNullOrEmpty(roomKey))
        {
            int foundIdx = Stage1RoomSequence.IndexOf(roomKey);
            if (foundIdx >= 0) CurrentRoomIndex = foundIdx;
        }

        string targetKey = !string.IsNullOrEmpty(roomKey) ? roomKey : CurrentRoomKey;
        Debug.Log($"<color=cyan>[StageManager] 1스테이지 초심자 룸 전환: '{targetKey}' (룸 번호: {CurrentRoomIndex + 1}/{Stage1RoomSequence.Count})</color>");

        var builder = FindObjectOfType<TilemapStageBuilder>();
        if (builder != null)
        {
            builder.TilemapAddressableKey = targetKey;
            await builder.BuildTilemapStageAsync(cancellationToken);
        }
        else
        {
            var builderObj = new GameObject("TilemapStageBuilder");
            builder = builderObj.AddComponent<TilemapStageBuilder>();
            builder.TilemapAddressableKey = targetKey;
            await builder.BuildTilemapStageAsync(cancellationToken);
        }
    }
}
