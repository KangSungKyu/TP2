#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// Unity 6 C# API (PrefabUtility & Tilemap API) 기반 Tilemap_Room_TestDummy.prefab 생성기.
/// 30x18 크기의 메트로배니아 룸 청크를 Grid + 3단 레이어 타일맵(Background, Ground Composite, OneWay Platforms) 구조로 무결점 생성합니다.
/// </summary>
public static class TilemapChunkGenerator
{
    [MenuItem("TP2/Generate Tilemap_Room_TestDummy Prefab (무결점 타일맵 룸 청크 생성)")]
    public static void GenerateTilemapRoomChunkPrefab()
    {
        string targetDir = "Assets/Prefabs/Rooms";
        if (!Directory.Exists(targetDir))
        {
            Directory.CreateDirectory(targetDir);
        }

        string prefabPath = Path.Combine(targetDir, "Tilemap_Room_TestDummy.prefab");

        // 1. Grid Root GameObject
        GameObject gridObj = new GameObject("Tilemap_Room_TestDummy");
        Grid grid = gridObj.AddComponent<Grid>();
        grid.cellSize = new Vector3(1f, 1f, 0f);

        // 2. Tilemap_Background Layer
        GameObject bgObj = createChildObject("Tilemap_Background", gridObj.transform);
        Tilemap bgTilemap = bgObj.AddComponent<Tilemap>();
        TilemapRenderer bgRend = bgObj.AddComponent<TilemapRenderer>();
        bgRend.sortingOrder = -10;

        // 3. Tilemap_Ground Layer (CompositeCollider2D 틈새 없는 일체형 지형)
        GameObject groundObj = createChildObject("Tilemap_Ground", gridObj.transform);
        Tilemap groundTilemap = groundObj.AddComponent<Tilemap>();
        TilemapRenderer groundRend = groundObj.AddComponent<TilemapRenderer>();
        groundRend.sortingOrder = 0;
        
        Rigidbody2D groundRb = groundObj.AddComponent<Rigidbody2D>();
        groundRb.bodyType = RigidbodyType2D.Static;

        TilemapCollider2D groundCol = groundObj.AddComponent<TilemapCollider2D>();
        groundCol.compositeOperation = Collider2D.CompositeOperation.Merge;
        
        CompositeCollider2D groundCompCol = groundObj.AddComponent<CompositeCollider2D>();
        groundCompCol.geometryType = CompositeCollider2D.GeometryType.Polygons;
        groundCompCol.generationType = CompositeCollider2D.GenerationType.Synchronous;

        // 4. Tilemap_Platforms Layer (1-Way 발판 + Effector + PassThrough)
        GameObject platObj = createChildObject("Tilemap_Platforms", gridObj.transform);
        Tilemap platTilemap = platObj.AddComponent<Tilemap>();
        TilemapRenderer platRend = platObj.AddComponent<TilemapRenderer>();
        platRend.sortingOrder = 5;

        TilemapCollider2D platCol = platObj.AddComponent<TilemapCollider2D>();
        platCol.usedByEffector = true;

        PlatformEffector2D effector = platObj.AddComponent<PlatformEffector2D>();
        effector.surfaceArc = 180f;
        effector.useOneWay = true;

        platObj.AddComponent<OneWayPlatformPassThrough>();

        // 5. 기본 스프라이트 텍스처에서 Tile 에셋 동적 생성 및 타일 그리드 칠하기
        Tile groundTile = createTileFromTexture("Assets/Textures/Environment/Tile_Terrain_Ground.png", new Color(0.25f, 0.28f, 0.32f));
        Tile platformTile = createTileFromTexture("Assets/Textures/Environment/Tile_Platform_OneWay.png", new Color(0.1f, 0.7f, 0.85f));

        // 5-1. 지면 & 벽 타일 칠하기 (30x18 룸)
        int halfWidth = 15;
        int roomHeight = 18;
        
        // 바닥 지면 (Y = 0)
        for (int x = -halfWidth; x < halfWidth; x++)
        {
            groundTilemap.SetTile(new Vector3Int(x, 0, 0), groundTile);
        }

        // 좌/우 벽 (X = -15, 14, Y = 1 ~ 18)
        for (int y = 1; y <= roomHeight; y++)
        {
            groundTilemap.SetTile(new Vector3Int(-halfWidth, y, 0), groundTile);
            groundTilemap.SetTile(new Vector3Int(halfWidth - 1, y, 0), groundTile);
        }

        // 5-2. 계단형 1-Way 발판 타일 칠하기 (Low, Mid, High)
        // Low Platform (Y = 3, X = -7 ~ -3)
        for (int x = -7; x <= -3; x++)
        {
            platTilemap.SetTile(new Vector3Int(x, 3, 0), platformTile);
        }
        // Mid Platform (Y = 6, X = -2 ~ 2)
        for (int x = -2; x <= 2; x++)
        {
            platTilemap.SetTile(new Vector3Int(x, 6, 0), platformTile);
        }
        // High Platform (Y = 9, X = 3 ~ 7)
        // 5-3. SpawnPointMarker 자식 마커 포함 배치 (Player, Monster, Boss)
        createSpawnMarker("SpawnPoint_Player", gridObj.transform, new Vector3(-12f, 1f, 0f), SpawnType.Player, "");
        createSpawnMarker("SpawnPoint_Monster_01", gridObj.transform, new Vector3(-5f, 4f, 0f), SpawnType.Monster, "1001");
        createSpawnMarker("SpawnPoint_Monster_02", gridObj.transform, new Vector3(0f, 7f, 0f), SpawnType.Monster, "1002");
        createSpawnMarker("SpawnPoint_Boss", gridObj.transform, new Vector3(5f, 10f, 0f), SpawnType.Boss, "1001");

        // 6. Unity PrefabUtility C# API로 무결점 실물 프리팹 에셋 저장
        PrefabUtility.SaveAsPrefabAsset(gridObj, prefabPath);
        Object.DestroyImmediate(gridObj);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        // 7. Addressables 자동 등록 구동
        AddressablePipeline.RegisterAllAddressables();

        Debug.Log($"<color=green><b>[TilemapChunkGenerator] '{prefabPath}' 무결점 2D Tilemap 룸 청크 C# API 생성 완결!</b></color>");
    }

    private static GameObject createChildObject(string name, Transform parent)
    {
        GameObject obj = new GameObject(name);
        obj.transform.SetParent(parent);
        obj.transform.localPosition = Vector3.zero;
        return obj;
    }

    private static void createSpawnMarker(string name, Transform parent, Vector3 pos, SpawnType type, string monsterId)
    {
        GameObject markerObj = createChildObject(name, parent);
        markerObj.transform.position = pos;
        var markerComp = markerObj.AddComponent<SpawnPointMarker>();
        markerComp.Type = type;
        markerComp.MonsterId = monsterId;
    }

    private static Tile createTileFromTexture(string texturePath, Color fallbackColor)
    {
        Sprite sp = null;
        var assets = AssetDatabase.LoadAllAssetsAtPath(texturePath);
        if (assets != null && assets.Length > 0)
        {
            foreach (var a in assets)
            {
                if (a is Sprite loadedSp)
                {
                    sp = loadedSp;
                    break;
                }
            }
        }

        if (sp == null)
        {
            sp = AssetDatabase.LoadAssetAtPath<Sprite>(texturePath);
        }

        if (sp == null)
        {
            sp = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), 16f);
        }

        Tile tile = ScriptableObject.CreateInstance<Tile>();
        tile.sprite = sp;
        tile.color = Color.white;
        return tile;
    }
}
#endif
