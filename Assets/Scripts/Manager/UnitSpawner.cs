using Cysharp.Threading.Tasks;
using System;
using System.Collections.Generic;
using UnityEngine;

public class UnitSpawner : Singleton<UnitSpawner>
{
    private const int MinimumCombatZones = 3;
    private const int MaximumActiveMonsters = 4;
    private const float MinimumZoneDistance = 15f;
    private const float MinimumEntryDistance = 14f;
    private const float MinimumPortalClearance = 7f;

    public void SpawnUnitsFromMarkers(GameObject roomInstance)
    {
        if (roomInstance == null) return;
        Bounds? movementBounds = ResolveMovementBounds(roomInstance);
        SpawnPointMarker[] markers = roomInstance.GetComponentsInChildren<SpawnPointMarker>(true);
        if (markers == null || markers.Length == 0)
        {
            uint[] missingMarkerEncounter = GetCurrentEncounter(Array.Empty<SpawnPointMarker>());
            if (missingMarkerEncounter.Length == 0) return;

            uint resourceIdx = 0;
            string resourcePath = string.Empty;
            StageRunData currentRun = StageManager.Instance?.CurrentRun;
            if (currentRun != null && currentRun.TryGetSlot(currentRun.CurrentSlotIdx, out ChunkSlotData slot) && slot != null)
                resourceIdx = slot.ChunkResourceIdx;
            ResourceDataTable resources = DataTableManager.Instance != null
                ? DataTableManager.Instance.GetDB<ResourceDataTable>(DataTableType.Resource)
                : null;
            if (resources != null) resourcePath = resources.GetResourcePath(resourceIdx);
            Debug.LogError($"[UnitSpawner] Combat chunk has no SpawnPointMarker. " +
                $"ChunkResourceIdx={resourceIdx}, Path='{resourcePath}', Root='{roomInstance.name}', " +
                $"Active={roomInstance.activeInHierarchy}.");
            return;
        }

        CleanupExistingUnits();
        Array.Sort(markers, CompareMarkers);
        SpawnPointMarker playerMarker = null;
        SpawnPointMarker bossMarker = null;
        var zones = new List<SpawnPointMarker>(MaximumActiveMonsters);
        foreach (SpawnPointMarker marker in markers)
        {
            if (marker == null || !marker.EnableSpawn) continue;
            if (marker.Type == SpawnType.Player && playerMarker == null) playerMarker = marker;
            else if (marker.Type == SpawnType.Boss && bossMarker == null) bossMarker = marker;
            else if (marker.Type == SpawnType.Monster) zones.Add(marker);
        }

        if (playerMarker != null) SpawnPlayerAt(playerMarker.transform.position);
        if (bossMarker != null)
        {
            SpawnMonsterUnit(bossMarker.MonsterId, bossMarker.transform.position, true, movementBounds);
            return;
        }

        uint[] encounter = GetCurrentEncounter(zones);
        if (encounter.Length == 0) return;
        if (!ValidateSpawnZones(roomInstance, zones, playerMarker != null ? playerMarker.transform : null, out string error))
        {
            Debug.LogError($"[UnitSpawner] Invalid combat SpawnZone layout: {error}");
            SpawnFallbackOnce(zones, encounter, movementBounds);
            return;
        }

        int start = GetDeterministicStart(zones.Count);
        StageRunData run = StageManager.Instance?.CurrentRun;
        uint[] allocation = BuildEncounterAllocation(encounter, zones.Count,
            run != null ? run.Seed : 0u, run != null ? run.CurrentSlotIdx : (byte)0);
        for (int i = 0; i < allocation.Length; i++)
        {
            SpawnPointMarker zone = zones[(start + i) % zones.Count];
            SpawnMonsterUnit(allocation[i], zone.transform.position, false, movementBounds);
        }
    }

    public static uint[] BuildEncounterAllocation(IReadOnlyList<uint> encounter, int zoneCount, uint seed, byte slotIdx)
    {
        if (encounter == null || zoneCount <= 0) return Array.Empty<uint>();
        int limit = Mathf.Min(MaximumActiveMonsters, zoneCount);
        var allocation = new List<uint>(limit);
        bool highThreatAdded = false;
        int offset = encounter.Count > 0 ? (int)((seed + slotIdx) % (uint)encounter.Count) : 0;
        for (int i = 0; i < encounter.Count && allocation.Count < limit; i++)
        {
            uint unitIdx = encounter[(offset + i) % encounter.Count];
            bool highThreat = unitIdx == 3103u || unitIdx == 3106u;
            if (highThreat && highThreatAdded) continue;
            highThreatAdded |= highThreat;
            allocation.Add(unitIdx);
        }
        return allocation.ToArray();
    }

    public static bool ValidateSpawnZones(GameObject roomInstance, IReadOnlyList<SpawnPointMarker> zones,
        Transform entry, out string error)
    {
        error = string.Empty;
        if (zones == null || zones.Count < MinimumCombatZones)
        {
            error = $"requires at least {MinimumCombatZones} zones, found {zones?.Count ?? 0}";
            return false;
        }

        for (int i = 0; i < zones.Count; i++)
        {
            if (zones[i] == null) { error = "contains a null zone"; return false; }
            Vector3 position = zones[i].transform.position;
            if (entry != null && Vector2.Distance(position, entry.position) < MinimumEntryDistance)
            {
                error = $"zone {i} is closer than {MinimumEntryDistance}m to entry";
                return false;
            }
            for (int j = i + 1; j < zones.Count; j++)
            {
                if (zones[j] == null || Vector2.Distance(position, zones[j].transform.position) < MinimumZoneDistance)
                {
                    error = $"zones {i}/{j} are closer than {MinimumZoneDistance}m";
                    return false;
                }
            }
        }

        if (roomInstance == null) return true;
        ChunkSocketMarker[] sockets = roomInstance.GetComponentsInChildren<ChunkSocketMarker>(true);
        foreach (ChunkSocketMarker socket in sockets)
        {
            if (socket == null) continue;
            foreach (SpawnPointMarker zone in zones)
            {
                if (Vector2.Distance(zone.transform.position, socket.transform.position) < MinimumPortalClearance)
                {
                    error = $"zone is inside {MinimumPortalClearance}m portal clearance";
                    return false;
                }
            }
        }
        return true;
    }

    private static int CompareMarkers(SpawnPointMarker left, SpawnPointMarker right)
    {
        if (left == null) return right == null ? 0 : 1;
        if (right == null) return -1;
        int x = left.transform.localPosition.x.CompareTo(right.transform.localPosition.x);
        return x != 0 ? x : left.transform.localPosition.y.CompareTo(right.transform.localPosition.y);
    }

    private static uint[] GetCurrentEncounter(IReadOnlyList<SpawnPointMarker> zones)
    {
        StageManager stage = StageManager.Instance;
        if (stage?.CurrentRun == null ||
            !stage.CurrentRun.TryGetSlot(stage.CurrentRun.CurrentSlotIdx, out ChunkSlotData slot))
        {
            var fallback = new List<uint>(zones?.Count ?? 0);
            if (zones != null)
                foreach (SpawnPointMarker zone in zones)
                    if (zone != null && zone.MonsterId != 0) fallback.Add(zone.MonsterId);
            return fallback.ToArray();
        }
        return slot.MonsterUnitIdxList ?? Array.Empty<uint>();
    }

    private static int GetDeterministicStart(int zoneCount)
    {
        StageRunData run = StageManager.Instance?.CurrentRun;
        return run == null || zoneCount == 0 ? 0 : (int)((run.Seed + run.CurrentSlotIdx) % (uint)zoneCount);
    }

    private static void SpawnFallbackOnce(IReadOnlyList<SpawnPointMarker> zones, IReadOnlyList<uint> encounter,
        Bounds? movementBounds)
    {
        if (zones == null || zones.Count == 0 || zones[0] == null || encounter == null || encounter.Count == 0) return;
        UnitSpawner instance = Instance;
        if (instance != null) instance.SpawnMonsterUnit(encounter[0], zones[0].transform.position, false, movementBounds);
    }

    private static void CleanupExistingUnits()
    {
        if (UnitPoolManager.Instance != null) UnitPoolManager.Instance.DespawnAllMonsters();
    }

    private static void SpawnPlayerAt(Vector3 position)
    {
        if (UnitPoolManager.Instance != null) UnitPoolManager.Instance.SpawnPlayerAsync(position).Forget();
    }

    private void SpawnMonsterUnit(uint unitIdx, Vector3 position, bool isBoss, Bounds? movementBounds = null)
    {
        if (unitIdx == 0)
        {
            Debug.LogError("[UnitSpawner] Spawn marker has no validated uint unit idx.");
            return;
        }
        if (UnitPoolManager.Instance == null) return;
        UnitPoolManager.Instance.SpawnMonsterAsync(unitIdx, position).ContinueWith(monster =>
        {
            if (monster != null)
            {
                if (movementBounds.HasValue) monster.SetHorizontalMovementBounds(movementBounds.Value);
                ConfigureMonsterUIAndRewards(monster, isBoss);
            }
        }).Forget();
    }

    private static Bounds? ResolveMovementBounds(GameObject roomInstance)
    {
        BoxCollider2D selected = null;
        foreach (BoxCollider2D candidate in roomInstance.GetComponentsInChildren<BoxCollider2D>(true))
        {
            if (!candidate.isTrigger) continue;
            if (selected == null || candidate.bounds.size.sqrMagnitude > selected.bounds.size.sqrMagnitude)
                selected = candidate;
        }
        return selected != null ? selected.bounds : (Bounds?)null;
    }

    private static void ConfigureMonsterUIAndRewards(UnitBase unit, bool isBoss)
    {
        if (unit == null) return;
        Debug.Log(isBoss
            ? $"<color=magenta>[UnitSpawner] Boss idx {unit.UnitIdx} spawned.</color>"
            : $"<color=yellow>[UnitSpawner] Monster idx {unit.UnitIdx} spawned.</color>");
    }
}
