using Cysharp.Threading.Tasks;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;

/// <summary>
/// StageData.csv(Type 9) 및 ResourceData.csv(Type 1) 정수 idx 참조 기반 동적 룸 로더 및 스테이지 총괄 매니저.
/// </summary>
public class StageManager : Singleton<StageManager>
{
    [Header("Current Stage Configuration")]
    public uint CurrentStageIdx = 9001; // 1Stage (TaoShrine)
    public int CurrentRoomSequenceIndex { get; private set; } = 0;

    public StageBaseData CurrentStageData
    {
        get
        {
            var db = DataTableManager.Instance != null ? DataTableManager.Instance.GetDB<StageDataTable>(DataTableType.StageData) : null;
            if (db != null && db.TryGetStageData(CurrentStageIdx, out var data))
            {
                return data;
            }
            return null;
        }
    }

    public uint CurrentRoomResourceIdx
    {
        get
        {
            var stage = CurrentStageData;
            if (stage != null && stage.RoomSequenceIdxList != null && stage.RoomSequenceIdxList.Length > 0)
            {
                int clampIdx = Mathf.Clamp(CurrentRoomSequenceIndex, 0, stage.RoomSequenceIdxList.Length - 1);
                return stage.RoomSequenceIdxList[clampIdx];
            }
            return 1040; // Fallback: 1040 (Tilemap_Room_Stage1_Entry)
        }
    }

    public string CurrentRoomAddressableKey => ResolveAddressableKey(CurrentRoomResourceIdx);

    public string ResolveAddressableKey(uint resourceIdx)
    {
        var resDb = DataTableManager.Instance != null ? DataTableManager.Instance.GetDB<ResourceDataTable>(DataTableType.Resource) : null;
        if (resDb != null)
        {
            string path = resDb.GetResourcePath(resourceIdx);
            if (!string.IsNullOrEmpty(path)) return path;
        }

        // Fallback for direct string key resolution
        switch (resourceIdx)
        {
            case 1040: return "Tilemap_Room_Stage1_Entry";
            case 1041: return "Tilemap_Room_Stage1_Battle";
            case 1042: return "Tilemap_Room_Stage1_Boss";
            default: return "Tilemap_Room_Stage1_Entry";
        }
    }

    public async UniTask LoadNextRoomAsync(uint roomResourceIdx = 0, CancellationToken cancellationToken = default)
    {
        string targetAddressKey = string.Empty;

        if (roomResourceIdx > 0)
        {
            targetAddressKey = ResolveAddressableKey(roomResourceIdx);
            var stage = CurrentStageData;
            if (stage != null && stage.RoomSequenceIdxList != null)
            {
                for (int i = 0; i < stage.RoomSequenceIdxList.Length; i++)
                {
                    if (stage.RoomSequenceIdxList[i] == roomResourceIdx)
                    {
                        CurrentRoomSequenceIndex = i;
                        break;
                    }
                }
            }
        }
        else
        {
            targetAddressKey = CurrentRoomAddressableKey;
        }

        Debug.Log($"<color=cyan>[StageManager] 정수 idx 참조 동적 룸 전환: TargetKey='{targetAddressKey}' (ResourceIdx: {roomResourceIdx}, Stage: {CurrentStageIdx})</color>");

        var builder = TilemapStageBuilder.Instance;
        if (builder == null)
        {
            var builderObj = new GameObject("TilemapStageBuilder");
            builder = builderObj.AddComponent<TilemapStageBuilder>();
        }

        builder.TilemapAddressableKey = targetAddressKey;
        await builder.BuildTilemapStageAsync(cancellationToken);
    }

    // 하위 호환성 메서드 (string 기반)
    public async UniTask LoadNextRoomAsync(string roomKey, CancellationToken cancellationToken = default)
    {
        uint resIdx = 1040;
        if (roomKey == "Tilemap_Room_Stage1_Battle") resIdx = 1041;
        else if (roomKey == "Tilemap_Room_Stage1_Boss") resIdx = 1042;
        await LoadNextRoomAsync(resIdx, cancellationToken);
    }

    public GameObject CurrentRoomInstance { get; set; }
    public List<GameObject> ActiveChunkInstances { get; } = new List<GameObject>();

    public void RegisterRoomInstance(GameObject roomObj)
    {
        if (roomObj == null) return;
        CurrentRoomInstance = roomObj;
        if (!ActiveChunkInstances.Contains(roomObj))
        {
            ActiveChunkInstances.Add(roomObj);
        }
    }

    public void CleanupActiveChunksAndEffects()
    {
        for (int i = ActiveChunkInstances.Count - 1; i >= 0; i--)
        {
            var chunk = ActiveChunkInstances[i];
            if (chunk != null)
            {
                if (Application.isPlaying) Destroy(chunk);
                else DestroyImmediate(chunk);
            }
        }
        ActiveChunkInstances.Clear();

        if (CurrentRoomInstance != null)
        {
            if (Application.isPlaying) Destroy(CurrentRoomInstance);
            else DestroyImmediate(CurrentRoomInstance);
            CurrentRoomInstance = null;
        }

        // 활성 몬스터 유닛 풀 회수 및 이펙트 풀 전면 정리
        if (UnitPoolManager.Instance != null)
        {
            UnitPoolManager.Instance.DespawnAllMonsters();
        }

        if (EffectPoolManager.Instance != null)
        {
            EffectPoolManager.Instance.ClearAllActiveEffects();
        }

        var particles = UnityEngine.Object.FindObjectsByType<ParticleSystem>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (var ps in particles)
        {
            if (ps != null && ps.gameObject != null)
            {
                if (Application.isPlaying) Destroy(ps.gameObject);
                else DestroyImmediate(ps.gameObject);
            }
        }

        var trailRenderers = UnityEngine.Object.FindObjectsByType<TrailRenderer>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (var tr in trailRenderers)
        {
            if (tr != null && tr.gameObject != null)
            {
                if (Application.isPlaying) Destroy(tr.gameObject);
                else DestroyImmediate(tr.gameObject);
            }
        }
    }
}
