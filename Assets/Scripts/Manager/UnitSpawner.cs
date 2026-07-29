using Cysharp.Threading.Tasks;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 룸 청크 내 SpawnPointMarker 정보를 읽어 플레이어, 일반 몬스터, 보스를 동적으로 스폰하는 유닛 매니저.
/// 동일한 몬스터 데이터라도 스폰 위치(SpawnType.Boss vs SpawnType.Monster)에 따라
/// 상단 대형 보스 HUD vs 오버레이 HP HUD 및 드롭 보상을 차등 연동합니다.
/// </summary>
public class UnitSpawner : MonoBehaviour
{
    private static UnitSpawner instance;
    public static UnitSpawner Instance
    {
        get
        {
            if (instance == null)
            {
                var go = new GameObject("UnitSpawner");
                instance = go.AddComponent<UnitSpawner>();
                DontDestroyOnLoad(go);
            }
            return instance;
        }
    }

    private void Awake()
    {
        if (instance == null)
        {
            instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else if (instance != this)
        {
            Destroy(gameObject);
        }
    }

    /// <summary>
    /// 스폰된 룸 청크 내부의 SpawnPointMarker 목록을 탐색하여 플레이어 및 몬스터/보스 동적 스폰 수행
    /// </summary>
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
            this.processSpawnMarker(marker);
        }
    }

    private void processSpawnMarker(SpawnPointMarker marker)
    {
        if (marker == null || !marker.EnableSpawn || marker.Type == SpawnType.None)
        {
            return; // 유닛 생성을 하지 않도록 설정된 스폰 마커이므로 완전 스킵!
        }

        switch (marker.Type)
        {
            case SpawnType.Player:
                this.spawnPlayerAt(marker.transform.position);
                break;

            case SpawnType.Monster:
                this.spawnMonsterUnit(marker, isBoss: false);
                break;

            case SpawnType.Boss:
                this.spawnMonsterUnit(marker, isBoss: true);
                break;
        }
    }

    private void spawnPlayerAt(Vector3 spawnPos)
    {
        Player player = FindObjectOfType<Player>();
        if (player != null)
        {
            player.transform.position = spawnPos;
            Debug.Log($"<color=cyan>[UnitSpawner] 기존 플레이어 위치를 마커 위치 ({spawnPos})로 텔레포트 이동완료!</color>");
        }
        else
        {
            // Player 프리팹 비동기 스폰
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

    private void spawnMonsterUnit(SpawnPointMarker marker, bool isBoss)
    {
        Vector3 spawnPos = marker.transform.position;
        string monsterId = string.IsNullOrEmpty(marker.MonsterId) ? "1001" : marker.MonsterId; // 기본 가론/몬스터 ID

        if (ResourceManager.Instance == null) return;

        // 보스/몬스터 공용 데이터 프리팹 로드 (가론 등)
        string prefabKey = isBoss ? "Garon" : "Garon"; // 데이터상 동일한 몬스터 프리팹 사용 가능

        ResourceManager.Instance.LoadAssetAsync<GameObject>(prefabKey, prefab =>
        {
            if (prefab == null)
            {
                // 세이프티 폴백
                GameObject fallback = new GameObject(isBoss ? "BossGaron" : "MonsterGaron");
                fallback.transform.position = spawnPos;
                var monsterComp = fallback.AddComponent<BossMonster>();
                this.configureMonsterUIAndRewards(monsterComp, isBoss);
                return;
            }

            GameObject mObj = Instantiate(prefab, spawnPos, Quaternion.identity);
            mObj.name = isBoss ? $"Boss_{monsterId}" : $"Monster_{monsterId}";

            var unitComp = mObj.GetComponent<UnitBase>();
            if (unitComp != null)
            {
                this.configureMonsterUIAndRewards(unitComp, isBoss);
            }

            Debug.Log($"<color={(isBoss ? "magenta" : "yellow")}>[UnitSpawner] {(isBoss ? "보스" : "일반 몬스터")} 유닛 스폰 완결 (ID: {monsterId}, Position: {spawnPos})</color>");
        });
    }

    private void configureMonsterUIAndRewards(UnitBase unit, bool isBoss)
    {
        if (unit == null) return;

        if (isBoss)
        {
            // 보스 스폰 위치: 상단 대형 보스 HP HUD 및 특별 보상 드롭 연동
            var testHud = FindObjectOfType<TestPlayerHUDUI>();
            if (testHud != null)
            {
                testHud.BindBossTarget(unit); // 화면 상단 대형 보스 체력바 UI 연결
            }
            Debug.Log($"<color=magenta>[UnitSpawner] '{unit.name}' -> 화면 상단 대형 보스 HUD 및 보스 특별 보상 바인딩 완료!</color>");
        }
        else
        {
            // 일반 몬스터 스폰 위치: 머리 위 오버레이 HP HUD 및 일반 드롭 보상 연동
            Debug.Log($"<color=yellow>[UnitSpawner] '{unit.name}' -> 머리 위 오버레이 HP HUD 및 일반 드롭 보상 바인딩 완료!</color>");
        }
    }
}
