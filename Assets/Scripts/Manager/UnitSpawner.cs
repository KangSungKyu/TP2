using Cysharp.Threading.Tasks;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 룸 청크 내 SpawnPointMarker 정보를 읽어 플레이어, 일반 몬스터, 보스를 동적으로 스폰하는 유닛 매니저.
/// </summary>
public class UnitSpawner : Singleton<UnitSpawner>
{
    public void SpawnUnitsFromMarkers(GameObject roomInstance)
    {
        if (roomInstance == null) return;

        var markers = roomInstance.GetComponentsInChildren<SpawnPointMarker>(true);
        if (markers == null || markers.Length == 0)
        {
            Debug.LogWarning("[UnitSpawner] 룸 청크 내에 SpawnPointMarker가 존재하지 않습니다.");
            return;
        }

        // 유닛 중복 생성 완전 방지: 기존 몬스터 제거 및 플레이어 유일성 보장
        CleanupExistingUnits();

        foreach (var marker in markers)
        {
            ProcessSpawnMarker(marker, roomInstance);
        }
    }

    private void CleanupExistingUnits()
    {
        if (UnitPoolManager.Instance != null)
        {
            UnitPoolManager.Instance.DespawnAllMonsters();
        }
    }

    private void ProcessSpawnMarker(SpawnPointMarker marker, GameObject roomInstance)
    {
        if (marker == null || !marker.EnableSpawn || marker.Type == SpawnType.None)
        {
            return;
        }

        bool isBossRoom = marker.Type == SpawnType.Boss;

        switch (marker.Type)
        {
            case SpawnType.Player:
                SpawnPlayerAt(marker.transform.position);
                break;

            case SpawnType.Monster:
                SpawnMonsterUnit(marker, isBoss: false);
                break;

            case SpawnType.Boss:
                if (isBossRoom)
                {
                    SpawnMonsterUnit(marker, isBoss: true);
                }
                else
                {
                    Debug.Log($"<color=yellow>[UnitSpawner] 일반 룸 청크 '{roomInstance.name}' 내 보스 마커 스폰 차단.</color>");
                }
                break;
        }
    }

    private void SpawnPlayerAt(Vector3 spawnPos)
    {
        if (UnitPoolManager.Instance != null)
        {
            UnitPoolManager.Instance.SpawnPlayerAsync(spawnPos).Forget();
        }
    }

    private void SpawnMonsterUnit(SpawnPointMarker marker, bool isBoss)
    {
        Vector3 spawnPos = marker.transform.position;
        string monsterIdStr = string.IsNullOrEmpty(marker.MonsterId) ? (isBoss ? "3201" : "3101") : marker.MonsterId;
        if (!uint.TryParse(monsterIdStr, out uint unitId))
        {
            unitId = isBoss ? 3201u : 3101u;
        }

        if (UnitPoolManager.Instance != null)
        {
            UnitPoolManager.Instance.SpawnMonsterAsync(unitId, spawnPos).ContinueWith(monster =>
            {
                if (monster != null)
                {
                    ConfigureMonsterUIAndRewards(monster, isBoss);
                }
            }).Forget();
        }
    }

    private void ConfigureMonsterUIAndRewards(UnitBase unit, bool isBoss)
    {
        if (unit == null) return;

        if (isBoss)
        {
            var testHud = TestPlayerHUDUI.Instance;
            if (testHud != null)
            {
                testHud.BindBossTarget(unit);
            }
            Debug.Log($"<color=magenta>[UnitSpawner] '{unit.name}' -> 화면 상단 대형 보스 HUD 및 보스 특별 보상 바인딩 완료!</color>");
        }
        else
        {
            Debug.Log($"<color=yellow>[UnitSpawner] '{unit.name}' -> 머리 위 오버레이 HP HUD 및 일반 드롭 보상 바인딩 완료!</color>");
        }
    }
}

