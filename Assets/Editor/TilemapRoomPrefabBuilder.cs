#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEngine.Tilemaps;
using System.IO;

/// <summary>
/// 60x30 대형 2D Tilemap 더미 스테이지 청크 프리팹(Tilemap_Room_TestDummy.prefab) 제작.
/// Tilemap 셀에 타일을 채우고 Sprites/Default 머티리얼을 바인딩하여 100% 가시성을 보장합니다.
/// </summary>
public static class TilemapRoomPrefabBuilder
{
    [MenuItem("TP2/Build Tilemap_Room_TestDummy Prefab (Tilemap 룸 청크 60x30 제작)")]
    public static void BuildTilemapRoomPrefab()
    {
        Debug.Log("<color=cyan><b>[TilemapRoomPrefabBuilder] 60x30 Tilemap_Room_TestDummy.prefab 제작 시작...</b></color>");

        string roomsDir = "Assets/Prefabs/Development";
        if (!Directory.Exists(roomsDir))
        {
            Directory.CreateDirectory(roomsDir);
        }

        string tilesDir = "Assets/Textures/Environment/Tiles";
        if (!Directory.Exists(tilesDir))
        {
            Directory.CreateDirectory(tilesDir);
        }

        // 1. Tile 에셋 생성 및 로드
        Sprite groundSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Textures/Environment/Tile_Terrain_Ground.png");
        Sprite platSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Textures/Environment/Tile_Platform_OneWay.png");
        Sprite bgSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Textures/Environment/Tile_Background_Deco.png");

        Tile groundTile = AssetDatabase.LoadAssetAtPath<Tile>($"{tilesDir}/Tile_Ground.asset");
        if (groundTile == null)
        {
            groundTile = ScriptableObject.CreateInstance<Tile>();
            groundTile.sprite = groundSprite;
            AssetDatabase.CreateAsset(groundTile, $"{tilesDir}/Tile_Ground.asset");
        }

        Tile platTile = AssetDatabase.LoadAssetAtPath<Tile>($"{tilesDir}/Tile_Platform.asset");
        if (platTile == null)
        {
            platTile = ScriptableObject.CreateInstance<Tile>();
            platTile.sprite = platSprite;
            AssetDatabase.CreateAsset(platTile, $"{tilesDir}/Tile_Platform.asset");
        }

        Tile redWallTile = AssetDatabase.LoadAssetAtPath<Tile>($"{tilesDir}/Tile_Wall_RedNoJump.asset");
        if (redWallTile == null)
        {
            redWallTile = ScriptableObject.CreateInstance<Tile>();
            redWallTile.sprite = groundSprite;
            redWallTile.color = new Color(0.9f, 0.2f, 0.2f, 1f);
            AssetDatabase.CreateAsset(redWallTile, $"{tilesDir}/Tile_Wall_RedNoJump.asset");
        }

        Tile iceWallTile = AssetDatabase.LoadAssetAtPath<Tile>($"{tilesDir}/Tile_Wall_IceSlide.asset");
        if (iceWallTile == null)
        {
            iceWallTile = ScriptableObject.CreateInstance<Tile>();
            iceWallTile.sprite = groundSprite;
            iceWallTile.color = new Color(0.2f, 0.85f, 1.0f, 1f);
            AssetDatabase.CreateAsset(iceWallTile, $"{tilesDir}/Tile_Wall_IceSlide.asset");
        }

        Material defaultSpriteMat = GetOrCreateTilemapMaterial();

        // 2. Root Grid GameObject
        GameObject gridRoot = new GameObject("Tilemap_Room_TestDummy");
        var gridComp = gridRoot.AddComponent<Grid>();
        gridComp.cellSize = new Vector3(1, 1, 0);

        // 3. Tilemap_Background (No Collider)
        GameObject bgObj = new GameObject("Tilemap_Background");
        bgObj.transform.SetParent(gridRoot.transform);
        var bgTilemap = bgObj.AddComponent<Tilemap>();
        var bgRenderer = bgObj.AddComponent<TilemapRenderer>();
        bgRenderer.sortingOrder = -10;
        bgRenderer.sharedMaterial = defaultSpriteMat;

        // 4. Tilemap_Ground (CompositeCollider2D + Rigidbody2D Static)
        GameObject groundObj = new GameObject("Tilemap_Ground");
        groundObj.transform.SetParent(gridRoot.transform);
        var groundTilemap = groundObj.AddComponent<Tilemap>();
        var groundRenderer = groundObj.AddComponent<TilemapRenderer>();
        groundRenderer.sortingOrder = 0;
        groundRenderer.sharedMaterial = defaultSpriteMat;

        var groundCol = groundObj.AddComponent<TilemapCollider2D>();
        groundCol.usedByComposite = true;

        var rb2d = groundObj.AddComponent<Rigidbody2D>();
        rb2d.bodyType = RigidbodyType2D.Static;

        var compositeCol = groundObj.AddComponent<CompositeCollider2D>();
        compositeCol.geometryType = CompositeCollider2D.GeometryType.Polygons;

        // 5. Tilemap_Platforms (PlatformEffector2D + OneWayPlatformPassThrough)
        GameObject platObj = new GameObject("Tilemap_Platforms");
        platObj.transform.SetParent(gridRoot.transform);
        var platTilemap = platObj.AddComponent<Tilemap>();
        var platRenderer = platObj.AddComponent<TilemapRenderer>();
        platRenderer.sortingOrder = 5;
        platRenderer.sharedMaterial = defaultSpriteMat;

        var platCol = platObj.AddComponent<TilemapCollider2D>();
        platCol.usedByEffector = true;

        var effector = platObj.AddComponent<PlatformEffector2D>();
        effector.surfaceArc = 180f;
        effector.useOneWay = true;

        platObj.AddComponent<OneWayPlatformPassThrough>();
        var platSurf = platObj.AddComponent<WallJumpSurface>();
        platSurf.CanWallJump = false;

        // ---------------------------------------------------------------------
        // 6. 타일맵 셀 배치 (60x30 대형 스테이지 타일 전개)
        // ---------------------------------------------------------------------
        // 6-1. 지면 타일 (Main Floor & Outer Walls)
        for (int x = -30; x <= 30; x++)
        {
            for (int y = -2; y <= 0; y++)
            {
                groundTilemap.SetTile(new Vector3Int(x, y, 0), groundTile);
            }
        }
        for (int y = 0; y <= 20; y++)
        {
            groundTilemap.SetTile(new Vector3Int(-30, y, 0), groundTile);
            groundTilemap.SetTile(new Vector3Int(30, y, 0), groundTile);
        }
        // Zone B 벽 타일 (Standard, Alternate, Red NoJump, Ice Slide)
        for (int y = 1; y <= 14; y++)
        {
            groundTilemap.SetTile(new Vector3Int(-8, y, 0), groundTile);
            groundTilemap.SetTile(new Vector3Int(-4, y, 0), groundTile);
            groundTilemap.SetTile(new Vector3Int(0, y, 0), groundTile);
            groundTilemap.SetTile(new Vector3Int(4, y, 0), redWallTile != null ? redWallTile : groundTile);
            groundTilemap.SetTile(new Vector3Int(8, y, 0), iceWallTile != null ? iceWallTile : groundTile);
        }

        // 6-2. 1-Way 발판 타일 (Zone A & Zone C)
        for (int x = -23; x <= -17; x++) platTilemap.SetTile(new Vector3Int(x, 4, 0), platTile);
        for (int x = -17; x <= -11; x++) platTilemap.SetTile(new Vector3Int(x, 7, 0), platTile);
        for (int x = -23; x <= -17; x++) platTilemap.SetTile(new Vector3Int(x, 10, 0), platTile);
        for (int x = 13; x <= 17; x++) platTilemap.SetTile(new Vector3Int(x, 4, 0), platTile);
        for (int x = 21; x <= 25; x++) platTilemap.SetTile(new Vector3Int(x, 5, 0), platTile);

        // ---------------------------------------------------------------------
        // 7. SpawnPointMarker 자식 마커 오브젝트 3종 세팅
        // ---------------------------------------------------------------------
        GameObject spPlayer = new GameObject("SpawnPoint_Player");
        spPlayer.transform.SetParent(gridRoot.transform);
        spPlayer.transform.localPosition = new Vector3(-25, 1.5f, 0);
        var mPlayer = spPlayer.AddComponent<SpawnPointMarker>();
        mPlayer.Type = SpawnType.Player;
        mPlayer.EnableSpawn = true;

        GameObject spMonster = new GameObject("SpawnPoint_Monster_01");
        spMonster.transform.SetParent(gridRoot.transform);
        spMonster.transform.localPosition = new Vector3(15, 1.5f, 0);
        var mMonster = spMonster.AddComponent<SpawnPointMarker>();
        mMonster.Type = SpawnType.Monster;
        mMonster.MonsterId = 3101;
        mMonster.EnableSpawn = true;

        GameObject spBoss = new GameObject("SpawnPoint_Boss");
        spBoss.transform.SetParent(gridRoot.transform);
        spBoss.transform.localPosition = new Vector3(23, 1.5f, 0);
        var mBoss = spBoss.AddComponent<SpawnPointMarker>();
        mBoss.Type = SpawnType.Boss;
        mBoss.MonsterId = 3201;
        mBoss.EnableSpawn = true;

        // Prefab 세이브
        string prefabPath = $"{roomsDir}/Tilemap_Room_TestDummy.prefab";
        PrefabUtility.SaveAsPrefabAsset(gridRoot, prefabPath);
        Object.DestroyImmediate(gridRoot);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log($"<color=green><b>[TilemapRoomPrefabBuilder] 60x30 타일 배치 완료! Prefab: {prefabPath}</b></color>");

        AddressablePipeline.BuildAndDeploy();
    }
    private static Material GetOrCreateTilemapMaterial()
    {
        string matDir = "Assets/Materials";
        if (!Directory.Exists(matDir)) Directory.CreateDirectory(matDir);

        string matPath = $"{matDir}/TilemapDefaultMaterial.mat";
        Material mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
        if (mat == null)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
            if (shader == null)
            {
                shader = Shader.Find("Sprites/Default");
            }
            mat = new Material(shader);
            AssetDatabase.CreateAsset(mat, matPath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
        }
        return mat;
    }
}
#endif

