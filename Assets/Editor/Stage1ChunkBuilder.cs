#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEngine.Tilemaps;
using System.IO;

/// <summary>
/// 1스테이지 초심자 전용 룸 청크 프리팹 3종 (Entry, Battle, Boss) 자동 제작 빌더.
/// 기본 조작법만으로 원활히 진행할 수 있는 평이하고 쾌적한 레벨 디자인을 반영합니다.
/// </summary>
public static class Stage1ChunkBuilder
{
    [MenuItem("TP2/Build Stage 1 Beginner Chunks (1스테이지 초심자 룸 청크 3종 제작)")]
    public static void BuildStage1Chunks()
    {
        Debug.Log("<color=cyan><b>[Stage1ChunkBuilder] 1스테이지 초심자용 룸 청크 3종 제작 시작...</b></color>");

        string roomsDir = "Assets/Prefabs/Rooms";
        if (!Directory.Exists(roomsDir)) Directory.CreateDirectory(roomsDir);

        string tilesDir = "Assets/Textures/Environment/Tiles";
        if (!Directory.Exists(tilesDir)) Directory.CreateDirectory(tilesDir);

        Tile taoGroundTile = AssetDatabase.LoadAssetAtPath<Tile>($"{tilesDir}/Tile_TaoShrine_Ground.asset");
        Tile groundTile = taoGroundTile != null ? taoGroundTile : AssetDatabase.LoadAssetAtPath<Tile>($"{tilesDir}/Tile_Ground.asset");
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

        Tile bgTile = AssetDatabase.LoadAssetAtPath<Tile>($"{tilesDir}/Tile_Background.asset");

        Material defaultSpriteMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/TilemapDefaultMaterial.mat");

        // ---------------------------------------------------------------------
        // 룸 1: Tilemap_Room_Stage1_Entry (입장 & 기초 조작 룸)
        // ---------------------------------------------------------------------
        BuildSingleChunk("Tilemap_Room_Stage1_Entry", roomsDir, groundTile, platTile, bgTile, defaultSpriteMat, (groundMap, platMap, root) =>
        {
            // Floor & Boundary Walls
            for (int x = -20; x <= 20; x++)
            {
                for (int y = -2; y <= 0; y++) groundMap.SetTile(new Vector3Int(x, y, 0), groundTile);
            }
            for (int y = 0; y <= 12; y++)
            {
                groundMap.SetTile(new Vector3Int(-20, y, 0), groundTile);
                groundMap.SetTile(new Vector3Int(20, y, 0), groundTile);
            }

            // Easy 1-Way Platforms (height 3, 6)
            for (int x = -10; x <= -4; x++) platMap.SetTile(new Vector3Int(x, 3, 0), platTile);
            for (int x = 4; x <= 10; x++) platMap.SetTile(new Vector3Int(x, 4, 0), platTile);

            // Player Spawn Point & Exit Portal Gate
            CreateSpawnMarker(root, "SpawnPoint_Player", new Vector3(-15, 1.5f, 0), SpawnType.Player);
            CreatePortalGate(root, new Vector3(15, 1.5f, 0), 1041);
        });

        // ---------------------------------------------------------------------
        // 룸 2: Tilemap_Room_Stage1_Battle (기초 전투 아레나 룸)
        // ---------------------------------------------------------------------
        BuildSingleChunk("Tilemap_Room_Stage1_Battle", roomsDir, groundTile, platTile, bgTile, defaultSpriteMat, (groundMap, platMap, root) =>
        {
            // Floor & Boundary Walls
            for (int x = -22; x <= 22; x++)
            {
                for (int y = -2; y <= 0; y++) groundMap.SetTile(new Vector3Int(x, y, 0), groundTile);
            }
            for (int y = 0; y <= 12; y++)
            {
                groundMap.SetTile(new Vector3Int(-22, y, 0), groundTile);
                groundMap.SetTile(new Vector3Int(22, y, 0), groundTile);
            }

            // Easy Stepped Battle Platforms
            for (int x = -8; x <= -2; x++) platMap.SetTile(new Vector3Int(x, 3, 0), platTile);
            for (int x = 2; x <= 8; x++) platMap.SetTile(new Vector3Int(x, 3, 0), platTile);

            // Spawners & Exit Portal Gate
            CreateSpawnMarker(root, "SpawnPoint_Player", new Vector3(-16, 1.5f, 0), SpawnType.Player);
            CreateSpawnMarker(root, "SpawnPoint_Monster_01", new Vector3(-5f, 1.5f, 0), SpawnType.Monster, "3101");
            CreateSpawnMarker(root, "SpawnPoint_Monster_02", new Vector3(3f, 1.5f, 0), SpawnType.Monster, "3102");
            CreateSpawnMarker(root, "SpawnPoint_Monster_03", new Vector3(10f, 1.5f, 0), SpawnType.Monster, "3103");
            CreatePortalGate(root, new Vector3(16, 1.5f, 0), 1042);
        });

        // ---------------------------------------------------------------------
        // 룸 3: Tilemap_Room_Stage1_Boss (1스테이지 보스 아레나 룸)
        // ---------------------------------------------------------------------
        BuildSingleChunk("Tilemap_Room_Stage1_Boss", roomsDir, groundTile, platTile, bgTile, defaultSpriteMat, (groundMap, platMap, root) =>
        {
            // Broad Boss Arena Floor
            for (int x = -25; x <= 25; x++)
            {
                for (int y = -2; y <= 0; y++) groundMap.SetTile(new Vector3Int(x, y, 0), groundTile);
            }
            for (int y = 0; y <= 15; y++)
            {
                groundMap.SetTile(new Vector3Int(-25, y, 0), groundTile);
                groundMap.SetTile(new Vector3Int(25, y, 0), groundTile);
            }

            // Evasion Platforms
            for (int x = -14; x <= -8; x++) platMap.SetTile(new Vector3Int(x, 4, 0), platTile);
            for (int x = 8; x <= 14; x++) platMap.SetTile(new Vector3Int(x, 4, 0), platTile);

            // Player & Boss Spawners & Exit Portal
            CreateSpawnMarker(root, "SpawnPoint_Player", new Vector3(-18, 1.5f, 0), SpawnType.Player);
            CreateSpawnMarker(root, "SpawnPoint_Boss", new Vector3(10, 1.5f, 0), SpawnType.Boss, "3201");
            CreatePortalGate(root, new Vector3(20, 1.5f, 0), 1040);
        });

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log("<color=green><b>[Stage1ChunkBuilder] 1스테이지 초심자 룸 청크 3종 (Entry, Battle, Boss) 제작 완결!</b></color>");

        AddressablePipeline.BuildAndDeploy();
    }

    private static void BuildSingleChunk(string roomName, string roomsDir, Tile groundTile, Tile platTile, Tile bgTile, Material mat, System.Action<Tilemap, Tilemap, GameObject> populateAction)
    {
        GameObject gridRoot = new GameObject(roomName);
        var gridComp = gridRoot.AddComponent<Grid>();
        gridComp.cellSize = new Vector3(1, 1, 0);

        // Tilemap_Background
        GameObject bgObj = new GameObject("Tilemap_Background");
        bgObj.transform.SetParent(gridRoot.transform);
        var bgTilemap = bgObj.AddComponent<Tilemap>();
        var bgRenderer = bgObj.AddComponent<TilemapRenderer>();
        bgRenderer.sortingOrder = -10;
        if (mat != null) bgRenderer.sharedMaterial = mat;

        // Tilemap_Ground
        GameObject groundObj = new GameObject("Tilemap_Ground");
        groundObj.transform.SetParent(gridRoot.transform);
        var groundTilemap = groundObj.AddComponent<Tilemap>();
        var groundRenderer = groundObj.AddComponent<TilemapRenderer>();
        groundRenderer.sortingOrder = 0;
        if (mat != null) groundRenderer.sharedMaterial = mat;

        var groundCol = groundObj.AddComponent<TilemapCollider2D>();
        groundCol.usedByComposite = true;
        var rb2d = groundObj.AddComponent<Rigidbody2D>();
        rb2d.bodyType = RigidbodyType2D.Static;
        var compositeCol = groundObj.AddComponent<CompositeCollider2D>();
        compositeCol.geometryType = CompositeCollider2D.GeometryType.Polygons;

        // Tilemap_Platforms
        GameObject platObj = new GameObject("Tilemap_Platforms");
        platObj.transform.SetParent(gridRoot.transform);
        int oneWayLayer = LayerMask.NameToLayer("OneWayPlatform");
        if (oneWayLayer >= 0) platObj.layer = oneWayLayer;
        var platTilemap = platObj.AddComponent<Tilemap>();
        var platRenderer = platObj.AddComponent<TilemapRenderer>();
        platRenderer.sortingOrder = 5;
        if (mat != null) platRenderer.sharedMaterial = mat;

        var platCol = platObj.AddComponent<TilemapCollider2D>();
        platCol.usedByEffector = true;
        var effector = platObj.AddComponent<PlatformEffector2D>();
        effector.surfaceArc = 180f;
        effector.useOneWay = true;
        platObj.AddComponent<OneWayPlatformPassThrough>();
        var platSurf = platObj.AddComponent<WallJumpSurface>();
        platSurf.CanWallJump = false;

        // Cell Population
        populateAction?.Invoke(groundTilemap, platTilemap, gridRoot);

        // Save Prefab
        string prefabPath = $"{roomsDir}/{roomName}.prefab";
        PrefabUtility.SaveAsPrefabAsset(gridRoot, prefabPath);
        Object.DestroyImmediate(gridRoot);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"<color=green>[Stage1ChunkBuilder] 실물 룸 청크 프리팹 저장 완결: {prefabPath}</color>");
    }

    private static void CreateSpawnMarker(GameObject parent, string name, Vector3 pos, SpawnType type, string monsterId = "")
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
        GameObject portalPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Structures/Portal_Gate.prefab");
        GameObject portalObj = null;
        if (portalPrefab != null)
        {
            portalObj = Object.Instantiate(portalPrefab, parent.transform);
            portalObj.name = "Portal_Gate";
        }
        else
        {
            portalObj = new GameObject("Portal_Gate");
            portalObj.transform.SetParent(parent.transform);
            var boxCol = portalObj.AddComponent<BoxCollider2D>();
            boxCol.size = new Vector2(2f, 3f);
            boxCol.isTrigger = true;
        }

        portalObj.transform.localPosition = pos;
        var portalComp = portalObj.GetComponent<RoomDoorPortal>();
        if (portalComp == null) portalComp = portalObj.AddComponent<RoomDoorPortal>();
        portalComp.TargetRoomResourceIdx = targetResourceIdx;
        portalComp.AutoTriggerOnTouch = true;
    }
}
#endif
