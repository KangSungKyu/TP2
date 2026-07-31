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

        foreach (var marker in markers)
        {
            ProcessSpawnMarker(marker);
        }
    }

    private void ProcessSpawnMarker(SpawnPointMarker marker)
    {
        if (marker == null || !marker.EnableSpawn || marker.Type == SpawnType.None)
        {
            return;
        }

        switch (marker.Type)
        {
            case SpawnType.Player:
                SpawnPlayerAt(marker.transform.position);
                break;

            case SpawnType.Monster:
                SpawnMonsterUnit(marker, isBoss: false);
                break;

            case SpawnType.Boss:
                SpawnMonsterUnit(marker, isBoss: true);
                break;
        }
    }

    private void SpawnPlayerAt(Vector3 spawnPos)
    {
        Player player = FindObjectOfType<Player>();
        if (player != null)
        {
            var motor = player.GetComponent<KinematicMotor2D>();
            if (motor != null)
            {
                motor.Teleport(spawnPos);
            }
            else
            {
                player.transform.position = spawnPos;
            }
            Debug.Log($"<color=cyan>[UnitSpawner] 기존 플레이어 위치를 마커 위치 ({spawnPos})로 텔레포트 이동완료!</color>");
        }
        else
        {
            if (ResourceManager.Instance != null)
            {
                ResourceManager.Instance.LoadAssetAsync<GameObject>("Player", prefab =>
                {
                    if (prefab != null)
                    {
                        GameObject pObj = Instantiate(prefab, spawnPos, Quaternion.identity);
                        pObj.name = "Player";
                        Debug.Log($"<color=cyan>[UnitSpawner] 플레이어 프리팹 스폰 완결 ({spawnPos})</color>");
                    }
                });
            }
        }
    }

    private void SpawnMonsterUnit(SpawnPointMarker marker, bool isBoss)
    {
        Vector3 spawnPos = marker.transform.position;
        string monsterId = string.IsNullOrEmpty(marker.MonsterId) ? "1001" : marker.MonsterId;

        if (ResourceManager.Instance == null) return;

        string prefabKey = "Garon";

        ResourceManager.Instance.LoadAssetAsync<GameObject>(prefabKey, prefab =>
        {
            if (prefab == null)
            {
                GameObject fallback = new GameObject(isBoss ? "BossGaron" : "MonsterGaron");
                fallback.transform.position = spawnPos;
                var monsterComp = fallback.AddComponent<BossMonster>();
                ConfigureMonsterUIAndRewards(monsterComp, isBoss);
                return;
            }

            GameObject mObj = Instantiate(prefab, spawnPos, Quaternion.identity);
            mObj.name = isBoss ? $"Boss_{monsterId}" : $"Monster_{monsterId}";

            var unitComp = mObj.GetComponent<UnitBase>();
            if (unitComp != null)
            {
                ConfigureMonsterUIAndRewards(unitComp, isBoss);
            }

            Debug.Log($"<color={(isBoss ? "magenta" : "yellow")}>[UnitSpawner] {(isBoss ? "보스" : "일반 몬스터")} 유닛 스폰 완결 (ID: {monsterId}, Position: {spawnPos})</color>");
        });
    }

    private void ConfigureMonsterUIAndRewards(UnitBase unit, bool isBoss)
    {
        if (unit == null) return;

        if (isBoss)
        {
            var testHud = FindObjectOfType<TestPlayerHUDUI>();
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

