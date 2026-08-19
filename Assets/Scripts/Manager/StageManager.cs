using Cysharp.Threading.Tasks;
using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;

[Serializable]
public sealed class ChunkSlotData
{
    public byte SlotIdx;
    public uint ChunkResourceIdx;
    public byte ConnectionMask;
    public uint[] MonsterUnitIdxList;
    public bool Visited;
    public bool Cleared;
    public bool RewardClaimed;
}

public sealed class IntraRoomZoneState
{
    public bool Visited;
    public bool Cleared;
}

[Serializable]
public sealed class StageRunData
{
    public uint StageDataIdx;
    public uint Seed;
    public byte Rows;
    public byte Columns;
    public byte StartSlotIdx;
    public byte BossGateSlotIdx;
    public byte CurrentSlotIdx;
    public byte PreviousSlotIdx = byte.MaxValue;
    public bool CompletionLocked;
    public ChunkSlotData[] Slots;

    public bool TryVisit(byte slotIdx)
    {
        TryGetSlot(slotIdx, out ChunkSlotData slot);
        if (slot == null || slot.Visited) return false;
        slot.Visited = true;
        return true;
    }

    public bool TryClear(byte slotIdx)
    {
        TryGetSlot(slotIdx, out ChunkSlotData slot);
        if (slot == null || slot.Cleared) return false;
        slot.Cleared = true;
        return true;
    }

    public bool TryClaimReward(byte slotIdx)
    {
        TryGetSlot(slotIdx, out ChunkSlotData slot);
        if (slot == null || slot.RewardClaimed) return false;
        slot.RewardClaimed = true;
        return true;
    }

    public bool TryGetSlot(byte slotIdx, out ChunkSlotData result)
    {
        if (Slots == null) { result = null; return false; }
        foreach (ChunkSlotData slot in Slots)
        {
            if (slot != null && slot.SlotIdx == slotIdx) { result = slot; return true; }
        }
        result = null;
        return false;
    }
}

public static class Stage1RunGenerator
{
    private const byte Up = 1;
    private const byte Right = 2;
    private const byte Down = 4;
    private const byte Left = 8;

    public static StageRunData Generate(uint seed)
    {
        bool wide = (seed & 1u) == 0u;
        int rows = wide ? 3 : 4;
        int columns = wide ? 4 : 3;
        var active = new bool[rows * columns];

        for (int i = 0; i < active.Length; i++) active[i] = true;
        if (wide)
        {
            active[2 * columns + 2] = false;
            active[2 * columns + 3] = false;
        }
        else
        {
            active[2 * columns + 2] = false;
            active[3 * columns + 2] = false;
        }

        byte bossSlot = (byte)(wide ? columns + 3 : 3 * columns + 1);
        var slots = new List<ChunkSlotData>(10);
        for (int i = 0; i < active.Length; i++)
        {
            if (!active[i]) continue;
            int row = i / columns;
            int column = i % columns;
            byte mask = 0;
            if (row > 0 && active[i - columns]) mask |= Up;
            if (column + 1 < columns && active[i + 1]) mask |= Right;
            if (row + 1 < rows && active[i + columns]) mask |= Down;
            if (column > 0 && active[i - 1]) mask |= Left;

            slots.Add(new ChunkSlotData
            {
                SlotIdx = (byte)i,
                ChunkResourceIdx = i == 0 ? 1040u : 1041u,
                ConnectionMask = mask,
                MonsterUnitIdxList = CreateMonsterAssignment(seed, (byte)i, i == 0 || i == bossSlot),
                Visited = i == 0
            });
        }

        return new StageRunData
        {
            StageDataIdx = 9001,
            Seed = seed,
            Rows = (byte)rows,
            Columns = (byte)columns,
            StartSlotIdx = 0,
            BossGateSlotIdx = bossSlot,
            CurrentSlotIdx = 0,
            Slots = slots.ToArray()
        };
    }

    public static StageRunData Generate(uint seed, StageLayoutData layout,
        IReadOnlyList<ChunkResourceData> chunks, IReadOnlyList<MonsterEncounterData> encounters)
    {
        StageRunData run = Generate(seed);
        if (layout == null || layout.StageDataIdx != 9001 ||
            layout.MinActiveChunks > run.Slots.Length || layout.MaxActiveChunks < run.Slots.Length)
            return run;

        var resourceUseCounts = new Dictionary<uint, int>();
        for (int i = 0; i < run.Slots.Length; i++)
        {
            ChunkSlotData slot = run.Slots[i];
            if (slot.SlotIdx == run.StartSlotIdx || slot.SlotIdx == run.BossGateSlotIdx) continue;
            bool acceptsEncounter = slot.ChunkResourceIdx == 1041u;

            if (chunks != null && chunks.Count > 0)
            {
                var validChunks = new List<ChunkResourceData>();
                foreach (ChunkResourceData chunk in chunks)
                {
                    resourceUseCounts.TryGetValue(chunk.ResourceIdx, out int used);
                    if (chunk.MaxUsePerRun > 0 && used < chunk.MaxUsePerRun &&
                        (chunk.SupportedConnectionMask & slot.ConnectionMask) == slot.ConnectionMask)
                        validChunks.Add(chunk);
                }
                if (validChunks.Count > 0)
                {
                    ChunkResourceData selected = validChunks[(int)((seed + slot.SlotIdx) % (uint)validChunks.Count)];
                    slot.ChunkResourceIdx = selected.ResourceIdx;
                    acceptsEncounter = selected.ChunkType == 1 || selected.ChunkType == 2;
                    resourceUseCounts.TryGetValue(selected.ResourceIdx, out int used);
                    resourceUseCounts[selected.ResourceIdx] = used + 1;
                }
            }

            if (acceptsEncounter && encounters != null && encounters.Count > 0)
            {
                MonsterEncounterData encounter = encounters[(int)((seed + slot.SlotIdx) % (uint)encounters.Count)];
                slot.MonsterUnitIdxList = encounter.UnitIdxList ?? Array.Empty<uint>();
            }
            else
            {
                slot.MonsterUnitIdxList = Array.Empty<uint>();
            }
        }
        return run;
    }

    public static bool Validate(StageRunData run)
    {
        if (run == null || run.Slots == null || run.Slots.Length < 9 || run.Slots.Length > 11) return false;
        if (!((run.Rows == 3 && run.Columns == 4) || (run.Rows == 4 && run.Columns == 3))) return false;
        List<byte> path = FindPath(run, run.StartSlotIdx, run.BossGateSlotIdx);
        if (path == null || path.Count - 1 < 3 || path.Count - 1 > 4) return false;

        int edges = 0;
        int branches = 0;
        foreach (ChunkSlotData slot in run.Slots)
        {
            int degree = CountBits(slot.ConnectionMask);
            edges += degree;
            if (degree >= 3) branches++;
        }

        return branches >= 3 && edges / 2 - run.Slots.Length + 1 >= 1;
    }

    public static List<byte> FindPath(StageRunData run, byte start, byte target)
    {
        if (run == null || run.Slots == null) return null;
        var slotMap = new Dictionary<byte, ChunkSlotData>();
        foreach (ChunkSlotData slot in run.Slots) slotMap[slot.SlotIdx] = slot;
        if (!slotMap.ContainsKey(start) || !slotMap.ContainsKey(target)) return null;

        var queue = new Queue<byte>();
        var previous = new Dictionary<byte, byte>();
        queue.Enqueue(start);
        previous[start] = start;

        while (queue.Count > 0)
        {
            byte current = queue.Dequeue();
            if (current == target) break;
            ChunkSlotData slot = slotMap[current];
            int[] offsets = { -run.Columns, 1, run.Columns, -1 };
            byte[] flags = { Up, Right, Down, Left };
            for (int i = 0; i < flags.Length; i++)
            {
                if ((slot.ConnectionMask & flags[i]) == 0) continue;
                int candidate = current + offsets[i];
                if (candidate < 0 || candidate > byte.MaxValue) continue;
                byte next = (byte)candidate;
                if (!slotMap.ContainsKey(next) || previous.ContainsKey(next)) continue;
                previous[next] = current;
                queue.Enqueue(next);
            }
        }

        if (!previous.ContainsKey(target)) return null;
        var path = new List<byte>();
        for (byte cursor = target; ; cursor = previous[cursor])
        {
            path.Add(cursor);
            if (cursor == start) break;
        }
        path.Reverse();
        return path;
    }

    private static int CountBits(byte value)
    {
        int count = 0;
        while (value != 0)
        {
            count += value & 1;
            value >>= 1;
        }
        return count;
    }

    private static uint[] CreateMonsterAssignment(uint seed, byte slotIdx, bool safeSlot)
    {
        if (safeSlot) return Array.Empty<uint>();
        uint first = 3101u + ((seed + slotIdx) % 5u);
        if (((seed ^ slotIdx) & 1u) == 0u) return new[] { first };
        uint second = 3101u + ((seed + slotIdx + 2u) % 5u);
        return new[] { first, second };
    }
}

/// <summary>
/// StageData.csv(Type 9) 및 ResourceData.csv(Type 1) 정수 idx 참조 기반 동적 룸 로더 및 스테이지 총괄 매니저.
/// </summary>
public class StageManager : Singleton<StageManager>
{
    public event Action<uint, int, int> ProgressChanged;
    [Header("Current Stage Configuration")]
    public uint CurrentStageIdx = 9001; // 1Stage (TaoShrine)
    public int CurrentRoomSequenceIndex { get; private set; } = 0;
    public StageRunData CurrentRun { get; private set; }
    public uint RoomGeneration { get; private set; }
    public uint CurrentZoneIdx { get; private set; }
    public uint ZoneGeneration { get; private set; }
    private readonly Dictionary<byte, Dictionary<uint, IntraRoomZoneState>> zoneStates =
        new Dictionary<byte, Dictionary<uint, IntraRoomZoneState>>();
    private bool isLoadingRoom;
    private bool completionTransitionInProgress;
    private bool portalTransitionLocked;

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

    public async UniTask EnsureStageLoadedAsync(uint stageIdx, CancellationToken cancellationToken = default)
    {
        if (stageIdx != 9001)
        {
            await ReturnToHubAsync(cancellationToken);
            return;
        }

        if (DataTableManager.Instance != null)
        {
            await DataTableManager.Instance.EnsureDataLoadedAsync();
        }

        var stageTable = DataTableManager.Instance != null
            ? DataTableManager.Instance.GetDB<StageDataTable>(DataTableType.StageData)
            : null;
        if (stageTable == null || !stageTable.TryGetStageData(stageIdx, out _))
        {
            Debug.LogError($"[StageManager] StageData idx {stageIdx} validation failed.");
            await ReturnToHubAsync(cancellationToken);
            return;
        }

        CurrentStageIdx = stageIdx;
        uint seed = unchecked((uint)DateTime.UtcNow.Ticks);
        var layoutTable = DataTableManager.Instance.GetDB<StageLayoutDataTable>(DataTableType.StageLayout);
        var chunkTable = DataTableManager.Instance.GetDB<ChunkResourceDataTable>(DataTableType.ChunkResource);
        var encounterTable = DataTableManager.Instance.GetDB<MonsterEncounterDataTable>(DataTableType.MonsterEncounter);
        StageLayoutData layout = null;
        layoutTable?.TryGetByStage(stageIdx, out layout);
        CurrentRun = Stage1RunGenerator.Generate(seed, layout,
            chunkTable?.GetForStage(stageIdx), encounterTable?.GetForStage(stageIdx));
        zoneStates.Clear();
        CurrentZoneIdx = 0;
        ZoneGeneration = 0;
        if (!Stage1RunGenerator.Validate(CurrentRun))
        {
            Debug.LogError("[StageManager] Stage 1 safe graph validation failed.");
            CurrentRun = null;
            await LoadNextRoomAsync(1041, cancellationToken);
            return;
        }

        PublishProgress();

        await LoadNextRoomAsync(1040, cancellationToken);
    }

    public async UniTask<GameObject> LoadRoomChunkAsync(uint roomResourceIdx, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var resourceTable = DataTableManager.Instance != null
            ? DataTableManager.Instance.GetDB<ResourceDataTable>(DataTableType.Resource)
            : null;
        if (resourceTable == null || !resourceTable.TryGetResource(roomResourceIdx, out ResourceData resource) ||
            string.IsNullOrWhiteSpace(resource.Path) || ResourceManager.Instance == null)
        {
            Debug.LogError($"[StageManager] Room ResourceData idx {roomResourceIdx} is invalid.");
            return null;
        }

        try
        {
            return await ResourceManager.Instance.InstantiateAsyncTask(resource.Path);
        }
        catch (Exception exception)
        {
            Debug.LogException(exception, this);
            return null;
        }
    }

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
            case 1040: return "Prefab_1040";
            case 1041: return "Prefab_1041";
            case 1042: return "Prefab_1042";
            default: return "Prefab_1040";
        }
    }

    public async UniTask<bool> LoadNextRoomAsync(uint roomResourceIdx = 0, CancellationToken cancellationToken = default)
    {
        if (isLoadingRoom) return false;
        isLoadingRoom = true;

        try
        {
        string targetAddressKey = string.Empty;

        if (CurrentRun != null && roomResourceIdx == 1042 && CurrentRun.CurrentSlotIdx != CurrentRun.BossGateSlotIdx)
        {
            Debug.LogWarning("[StageManager] Boss room transition rejected before reaching the BossGate slot.");
            return false;
        }

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

        RoomGeneration++;
        CurrentZoneIdx = 0;
        portalTransitionLocked = true;
        var builder = TilemapStageBuilder.Instance;
        if (builder == null)
        {
            var builderObj = new GameObject("TilemapStageBuilder");
            builder = builderObj.AddComponent<TilemapStageBuilder>();
        }

        builder.TilemapAddressableKey = targetAddressKey;
        return await builder.BuildTilemapStageAsync(cancellationToken);
        }
        finally
        {
            isLoadingRoom = false;
            portalTransitionLocked = false;
        }
    }

    public bool IsCurrentPortal(byte ownerSlotIdx, uint roomGeneration) =>
        CurrentRun != null && ownerSlotIdx == CurrentRun.CurrentSlotIdx && roomGeneration == RoomGeneration;

    public bool TryEnterIntraRoomZone(uint chunkResourceIdx, uint sourceZoneIdx, uint destinationZoneIdx,
        uint portalPairIdx)
    {
        if (CurrentRun == null || portalPairIdx == 0 || sourceZoneIdx == destinationZoneIdx ||
            !CurrentRun.TryGetSlot(CurrentRun.CurrentSlotIdx, out ChunkSlotData slot) ||
            slot.ChunkResourceIdx != chunkResourceIdx || CurrentZoneIdx != sourceZoneIdx)
            return false;

        CurrentZoneIdx = destinationZoneIdx;
        ZoneGeneration++;
        GetZoneState(CurrentRun.CurrentSlotIdx, destinationZoneIdx).Visited = true;
        return true;
    }

    public IntraRoomZoneState GetZoneState(byte slotIdx, uint zoneIdx)
    {
        if (!zoneStates.TryGetValue(slotIdx, out Dictionary<uint, IntraRoomZoneState> states))
        {
            states = new Dictionary<uint, IntraRoomZoneState>();
            zoneStates.Add(slotIdx, states);
        }
        if (!states.TryGetValue(zoneIdx, out IntraRoomZoneState state))
        {
            state = new IntraRoomZoneState();
            states.Add(zoneIdx, state);
        }
        return state;
    }

    public bool TryClearIntraRoomZone(uint zoneIdx)
    {
        if (CurrentRun == null || CurrentZoneIdx != zoneIdx) return false;
        IntraRoomZoneState state = GetZoneState(CurrentRun.CurrentSlotIdx, zoneIdx);
        if (state.Cleared) return false;
        state.Cleared = true;
        return true;
    }

    public bool TryBeginPortalTransition(byte ownerSlotIdx, uint roomGeneration)
    {
        if (portalTransitionLocked || !IsCurrentPortal(ownerSlotIdx, roomGeneration)) return false;
        portalTransitionLocked = true;
        return true;
    }

    public void CancelPortalTransition(byte ownerSlotIdx, uint roomGeneration)
    {
        if (IsCurrentPortal(ownerSlotIdx, roomGeneration)) portalTransitionLocked = false;
    }

    public async UniTask<bool> LoadConnectedRoomAsync(byte targetSlotIdx = byte.MaxValue,
        CancellationToken cancellationToken = default)
    {
        byte previousSlotIdx = CurrentRun != null ? CurrentRun.CurrentSlotIdx : byte.MaxValue;
        byte previousPreviousSlotIdx = CurrentRun != null ? CurrentRun.PreviousSlotIdx : byte.MaxValue;
        bool targetWasVisited = CurrentRun != null && CurrentRun.TryGetSlot(targetSlotIdx, out ChunkSlotData previousTarget) && previousTarget.Visited;
        if (!TryMoveToConnectedSlot(targetSlotIdx, out uint roomResourceIdx)) return false;
        bool loaded = await LoadNextRoomAsync(roomResourceIdx, cancellationToken);
        if (loaded) return true;

        if (CurrentRun != null && CurrentRun.TryGetSlot(targetSlotIdx, out ChunkSlotData target))
            target.Visited = targetWasVisited;
        if (CurrentRun != null)
        {
            CurrentRun.CurrentSlotIdx = previousSlotIdx;
            CurrentRun.PreviousSlotIdx = previousPreviousSlotIdx;
            PublishProgress();
        }
        return false;
    }

    public bool TryMoveToConnectedSlot(byte targetSlotIdx, out uint roomResourceIdx)
    {
        roomResourceIdx = 0;
        if (CurrentRun == null || !CurrentRun.TryGetSlot(CurrentRun.CurrentSlotIdx, out ChunkSlotData current))
            return false;

        List<byte> connected = GetConnectedSlotIndices(CurrentRun, current);
        if (targetSlotIdx == byte.MaxValue || !connected.Contains(targetSlotIdx))
        {
            Debug.LogWarning($"[StageManager] Invalid portal graph target {targetSlotIdx} from slot {current.SlotIdx}.");
            return false;
        }

        byte destination = targetSlotIdx;
        if (!CurrentRun.TryGetSlot(destination, out ChunkSlotData target)) return false;
        byte previous = CurrentRun.CurrentSlotIdx;
        CurrentRun.PreviousSlotIdx = previous;
        CurrentRun.CurrentSlotIdx = destination;
        CurrentRun.TryVisit(destination);
        PublishProgress();
        roomResourceIdx = target.ChunkResourceIdx != 0 ? target.ChunkResourceIdx : 1041u;
        return true;
    }

    public void PublishProgress()
    {
        if (CurrentRun?.Slots == null) return;
        int visited = 0;
        foreach (ChunkSlotData slot in CurrentRun.Slots)
            if (slot != null && slot.Visited) visited++;
        ProgressChanged?.Invoke(CurrentStageIdx, visited, CurrentRun.Slots.Length);
    }

    public bool TryGetConnectedSlot(ChunkSocketDirection direction, out byte slotIdx)
    {
        slotIdx = byte.MaxValue;
        if (CurrentRun == null || !CurrentRun.TryGetSlot(CurrentRun.CurrentSlotIdx, out ChunkSlotData current))
            return false;

        int row = current.SlotIdx / CurrentRun.Columns;
        int column = current.SlotIdx % CurrentRun.Columns;
        int[] rowOffsets = { -1, 0, 1, 0 };
        int[] columnOffsets = { 0, 1, 0, -1 };
        byte[] flags = { 1, 2, 4, 8 };
        byte[] opposite = { 4, 8, 1, 2 };
        int index = (int)direction;
        if (index < 0 || index >= flags.Length || (current.ConnectionMask & flags[index]) == 0) return false;

        int nextRow = row + rowOffsets[index];
        int nextColumn = column + columnOffsets[index];
        if (nextRow < 0 || nextRow >= CurrentRun.Rows || nextColumn < 0 || nextColumn >= CurrentRun.Columns)
            return false;

        byte candidate = (byte)(nextRow * CurrentRun.Columns + nextColumn);
        if (!CurrentRun.TryGetSlot(candidate, out ChunkSlotData next) ||
            (next.ConnectionMask & opposite[index]) == 0) return false;

        slotIdx = candidate;
        return true;
    }

    public async UniTask CompleteStage1Async(CancellationToken cancellationToken = default)
    {
        if (CurrentRun == null || CurrentRun.CompletionLocked || completionTransitionInProgress) return;
        completionTransitionInProgress = true;
        try
        {
            if (await ReturnToHubAsync(cancellationToken)) CurrentRun.TryLockCompletion();
        }
        finally
        {
            completionTransitionInProgress = false;
        }
    }

    private static List<byte> GetConnectedSlotIndices(StageRunData run, ChunkSlotData current)
    {
        var result = new List<byte>(4);
        int row = current.SlotIdx / run.Columns;
        int column = current.SlotIdx % run.Columns;
        int[] rowOffsets = { -1, 0, 1, 0 };
        int[] columnOffsets = { 0, 1, 0, -1 };
        byte[] flags = { 1, 2, 4, 8 };
        byte[] opposite = { 4, 8, 1, 2 };
        for (int i = 0; i < flags.Length; i++)
        {
            if ((current.ConnectionMask & flags[i]) == 0) continue;
            int nextRow = row + rowOffsets[i];
            int nextColumn = column + columnOffsets[i];
            if (nextRow < 0 || nextRow >= run.Rows || nextColumn < 0 || nextColumn >= run.Columns) continue;
            byte nextIdx = (byte)(nextRow * run.Columns + nextColumn);
            if (run.TryGetSlot(nextIdx, out ChunkSlotData next) && (next.ConnectionMask & opposite[i]) != 0)
                result.Add(nextIdx);
        }
        return result;
    }

    public static async UniTask<bool> ReturnToHubAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (GameSceneManager.Instance == null)
        {
            Debug.LogError("[StageManager] GameSceneManager unavailable; Hub fallback could not start.");
            return false;
        }
        try
        {
            await GameSceneManager.Instance.TransitionTo(GameSceneManager.SceneName.Hub);
            return true;
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            return false;
        }
    }

    // 하위 호환성 메서드 (string 기반)
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

    public void ResetRunForHub()
    {
        CleanupActiveChunksAndEffects();
        RoomGeneration++;
        CurrentZoneIdx = 0;
        ZoneGeneration++;
        zoneStates.Clear();
        CurrentRun = null;
        CurrentRoomSequenceIndex = 0;
        isLoadingRoom = false;
        completionTransitionInProgress = false;
        portalTransitionLocked = false;
    }
}
