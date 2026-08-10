#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// 6x6 청크 모듈 Prefab 24종 자동 제작 및 Stage 1 룸 청크 11종 전면 재생성 빌더.
/// </summary>
public static class ModuleChunkBuilder
{
    [MenuItem("TP2/Build 6x6 Modules & Stage 1 Chunks (6x6 모듈 & 룸 청크 전면 재생성)")]
    public static void BuildAllModulesAndChunks()
    {
        Debug.Log("<color=cyan><b>[ModuleChunkBuilder] 6x6 모듈 Prefab 24종 및 룸 청크 11종 빌드 시작...</b></color>");

        string modulesDir = "Assets/Prefabs/Modules";
        if (!Directory.Exists(modulesDir)) Directory.CreateDirectory(modulesDir);

        string roomsDir = "Assets/Prefabs/Rooms";
        if (!Directory.Exists(roomsDir)) Directory.CreateDirectory(roomsDir);

        string tilesDir = "Assets/Textures/Environment/Tiles";
        if (!Directory.Exists(tilesDir)) Directory.CreateDirectory(tilesDir);

        Tile groundTile = AssetDatabase.LoadAssetAtPath<Tile>($"{tilesDir}/Tile_Ground.asset");
        if (groundTile == null)
        {
            Sprite groundSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Textures/Environment/Tile_Terrain_Ground.png");
            groundTile = ScriptableObject.CreateInstance<Tile>();
            groundTile.sprite = groundSprite;
            AssetDatabase.CreateAsset(groundTile, $"{tilesDir}/Tile_Ground.asset");
        }

        Tile platTile = AssetDatabase.LoadAssetAtPath<Tile>($"{tilesDir}/Tile_Platform.asset");
        if (platTile == null)
        {
            Sprite platSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Textures/Environment/Tile_Platform_OneWay.png");
            platTile = ScriptableObject.CreateInstance<Tile>();
            platTile.sprite = platSprite;
            AssetDatabase.CreateAsset(platTile, $"{tilesDir}/Tile_Platform.asset");
        }

        Material defaultSpriteMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/TilemapDefaultMaterial.mat");

        // 1. 24종 6x6 모듈 Prefab 빌드
        Build24ModulePrefabs(modulesDir, groundTile, platTile, defaultSpriteMat);

        // 2. Stage 1 룸 청크 11종 전면 빌드
        Build11RoomChunkPrefabs(roomsDir, groundTile, platTile, defaultSpriteMat);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log("<color=green><b>[ModuleChunkBuilder] 6x6 모듈 Prefab 및 Stage 1 룸 청크 11종 빌드 완결!</b></color>");

        AddressablePipeline.BuildAndDeploy();
    }

    private static void Build24ModulePrefabs(string modulesDir, Tile groundTile, Tile platTile, Material mat)
    {
        string[] moduleNames = new string[]
        {
            "Module_A1", "Module_A2", "Module_B1", "Module_B2", "Module_C1", "Module_C2",
            "Module_D1", "Module_D2", "Module_E1", "Module_E2", "Module_F1", "Module_F2",
            "Module_G1", "Module_G2", "Module_H1", "Module_H2", "Module_I1", "Module_I2",
            "Module_J1", "Module_J2", "Module_K1", "Module_K2", "Module_L1", "Module_L2"
        };

        foreach (var modName in moduleNames)
        {
            GameObject modRoot = new GameObject(modName);
            var grid = modRoot.AddComponent<Grid>();
            grid.cellSize = new Vector3(1, 1, 0);

            // Ground Tilemap
            GameObject groundObj = new GameObject("Tilemap_Ground");
            groundObj.transform.SetParent(modRoot.transform);
            var gMap = groundObj.AddComponent<Tilemap>();
            var gRend = groundObj.AddComponent<TilemapRenderer>();
            if (mat != null) gRend.sharedMaterial = mat;
            groundObj.AddComponent<TilemapCollider2D>();

            // Platform Tilemap
            GameObject platObj = new GameObject("Tilemap_Platforms");
            platObj.transform.SetParent(modRoot.transform);
            var pMap = platObj.AddComponent<Tilemap>();
            var pRend = platObj.AddComponent<TilemapRenderer>();
            if (mat != null) pRend.sharedMaterial = mat;

            // Fill 6x6 Base Grid
            for (int x = 0; x < 6; x++)
            {
                gMap.SetTile(new Vector3Int(x, 0, 0), groundTile);
            }

            // Hazard Traps Binding based on module type
            if (modName.Contains("A") || modName.Contains("E"))
            {
                GameObject spike = new GameObject("SpikeTrap");
                spike.transform.SetParent(modRoot.transform);
                spike.transform.localPosition = new Vector3(3f, 1f, 0f);
                spike.AddComponent<SpikeTrap>();
            }
            if (modName.Contains("C") || modName.Contains("E"))
            {
                GameObject saw = new GameObject("SawBladeTrap");
                saw.transform.SetParent(modRoot.transform);
                saw.transform.localPosition = new Vector3(3f, 3f, 0f);
                saw.AddComponent<SawBladeTrap>();
            }

            string prefabPath = $"{modulesDir}/{modName}.prefab";
            PrefabUtility.SaveAsPrefabAsset(modRoot, prefabPath);
            Object.DestroyImmediate(modRoot);
        }
        Debug.Log($"<color=green>[ModuleChunkBuilder] 6x6 모듈 Prefab 24종 저장 완결!</color>");
    }

    private static void Build11RoomChunkPrefabs(string roomsDir, Tile groundTile, Tile platTile, Material mat)
    {
        string[] chunkNames = new string[]
        {
            "Prefab_1040", "Prefab_1041", "Prefab_1042",
            "Room_11050", "Room_11051", "Room_11052", "Room_11053",
            "Room_11056", "Room_11057", "Room_11061", "Room_11063"
        };

        foreach (var chunkName in chunkNames)
        {
            GameObject gridRoot = new GameObject(chunkName);
            gridRoot.AddComponent<Grid>().cellSize = new Vector3(1, 1, 0);

            // Ground
            GameObject groundObj = new GameObject("Tilemap_Ground");
            groundObj.transform.SetParent(gridRoot.transform);
            var gMap = groundObj.AddComponent<Tilemap>();
            var gR = groundObj.AddComponent<TilemapRenderer>();
            if (mat != null) gR.sharedMaterial = mat;
            groundObj.AddComponent<TilemapCollider2D>();

            // Floor
            int width = chunkName == "Prefab_1042" ? 30 : 20;
            for (int x = -width; x <= width; x++)
            {
                for (int y = -2; y <= 0; y++) gMap.SetTile(new Vector3Int(x, y, 0), groundTile);
            }
            for (int y = 0; y <= 12; y++)
            {
                gMap.SetTile(new Vector3Int(-width, y, 0), groundTile);
                gMap.SetTile(new Vector3Int(width, y, 0), groundTile);
            }

            // Spawners & Portals
            if (chunkName == "Prefab_1040")
            {
                CreateSpawnMarker(gridRoot, "SpawnPoint_Player", new Vector3(-15, 1.5f, 0), SpawnType.Player);
                CreatePortalGate(gridRoot, new Vector3(15, 1.5f, 0), 1041);
            }
            else if (chunkName == "Prefab_1041")
            {
                CreateSpawnMarker(gridRoot, "SpawnPoint_Player", new Vector3(-16, 1.5f, 0), SpawnType.Player);
                CreateSpawnMarker(gridRoot, "SpawnPoint_Monster_01", new Vector3(-5f, 1.5f, 0), SpawnType.Monster, 3101);
                CreateSpawnMarker(gridRoot, "SpawnPoint_Monster_02", new Vector3(3f, 1.5f, 0), SpawnType.Monster, 3102);
                CreatePortalGate(gridRoot, new Vector3(16, 1.5f, 0), 1042);
            }
            else if (chunkName == "Prefab_1042")
            {
                CreateSpawnMarker(gridRoot, "SpawnPoint_Player", new Vector3(-20, 1.5f, 0), SpawnType.Player);
                CreateSpawnMarker(gridRoot, "SpawnPoint_Boss", new Vector3(15, 1.5f, 0), SpawnType.Boss, 3201);
                CreatePortalGate(gridRoot, new Vector3(25, 1.5f, 0), 1040);
            }
            else
            {
                CreateSpawnMarker(gridRoot, "SpawnPoint_Player", new Vector3(-12, 1.5f, 0), SpawnType.Player);
                CreateSpawnMarker(gridRoot, "SpawnPoint_Monster", new Vector3(0, 1.5f, 0), SpawnType.Monster, 3101);
                CreatePortalGate(gridRoot, new Vector3(12, 1.5f, 0), 1041);
            }

            string prefabPath = $"{roomsDir}/{chunkName}.prefab";
            PrefabUtility.SaveAsPrefabAsset(gridRoot, prefabPath);
            Object.DestroyImmediate(gridRoot);
        }
        Debug.Log($"<color=green>[ModuleChunkBuilder] Stage 1 룸 청크 11종 저장 완결!</color>");
    }

    private static void CreateSpawnMarker(GameObject parent, string name, Vector3 pos, SpawnType type, uint monsterId = 0)
    {
        GameObject markerObj = new GameObject(name);
        markerObj.transform.SetParent(parent.transform);
        markerObj.transform.localPosition = pos;
        var marker = markerObj.AddComponent<SpawnPointMarker>();
        marker.Type = type;
        marker.MonsterId = monsterId;
        marker.EnableSpawn = true;
    }

    private static void CreatePortalGate(GameObject parent, Vector3 pos, uint targetResourceIdx)
    {
        GameObject portalObj = new GameObject("Portal_Gate");
        portalObj.transform.SetParent(parent.transform);
        portalObj.transform.localPosition = pos;
        var boxCol = portalObj.AddComponent<BoxCollider2D>();
        boxCol.size = new Vector2(2f, 3f);
        boxCol.isTrigger = true;

        var portalComp = portalObj.AddComponent<RoomDoorPortal>();
        portalComp.TargetRoomResourceIdx = targetResourceIdx;
        portalComp.AutoTriggerOnTouch = true;
    }
}
#endif
