#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEngine.Tilemaps;
using System.IO;

/// <summary>
/// Unity 2D Tilemap 타일셋 & 타일 팰리트(TilePalette) 자동 구축 및 60x30 더미 스테이지 타일 바인딩 파이프라인.
/// </summary>
public static class TilemapAssetPipeline
{
    [MenuItem("TP2/Build Complete Tilemap Assets & Palette (타일셋 및 타일 팰리트 전면 구축)")]
    public static void BuildTilemapAssetsAndPalette()
    {
        Debug.Log("<color=cyan><b>[TilemapAssetPipeline] 유니티 2D 타일셋 & PPU 32 & 타일 팰리트 전면 구축 개시...</b></color>");

        // 1. PPU (Pixels Per Unit) 32 및 Sprite Import 설정 교정
        FixTextureImporterPPU("Assets/Textures/Environment/Tile_Terrain_Ground.png", 32);
        FixTextureImporterPPU("Assets/Textures/Environment/Tile_Platform_OneWay.png", 32);
        FixTextureImporterPPU("Assets/Textures/Environment/Tile_Background_Deco.png", 32);
        FixTextureImporterPPU("Assets/Textures/Environment/Tile_Terrain_SpecialWalls.png", 32);
        FixTextureImporterPPU("Assets/Textures/Environment/Tile_Stage_Large60x30.png", 32);
        FixTextureImporterPPU("Assets/Textures/Environment/Tile_Chapter1_TaoShrine.png", 32);
        FixTextureImporterPPU("Assets/Textures/Environment/Tile_Chapter2_CyberRuins.png", 32);

        AssetDatabase.Refresh();

        // 2. Tiles 디렉토리 및 Tile 에셋 생성
        string tilesDir = "Assets/Textures/Environment/Tiles";
        if (!Directory.Exists(tilesDir)) Directory.CreateDirectory(tilesDir);

        Sprite groundSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Textures/Environment/Tile_Terrain_Ground.png");
        Sprite platSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Textures/Environment/Tile_Platform_OneWay.png");
        Sprite bgSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Textures/Environment/Tile_Background_Deco.png");
        Sprite specSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Textures/Environment/Tile_Terrain_SpecialWalls.png");
        Sprite taoSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Textures/Environment/Tile_Chapter1_TaoShrine.png");
        Sprite cyberSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Textures/Environment/Tile_Chapter2_CyberRuins.png");

        Tile groundTile = CreateOrUpdateTileAsset($"{tilesDir}/Tile_Ground.asset", groundSprite, Color.white);
        Tile platTile = CreateOrUpdateTileAsset($"{tilesDir}/Tile_Platform.asset", platSprite, Color.white);
        Tile bgTile = CreateOrUpdateTileAsset($"{tilesDir}/Tile_Background.asset", bgSprite, new Color(1f, 1f, 1f, 0.5f));

        // 챕터별 타일셋 에셋 생성
        Tile taoGroundTile = CreateOrUpdateTileAsset($"{tilesDir}/Tile_TaoShrine_Ground.asset", taoSprite != null ? taoSprite : groundSprite, Color.white);
        Tile cyberGroundTile = CreateOrUpdateTileAsset($"{tilesDir}/Tile_CyberRuins_Ground.asset", cyberSprite != null ? cyberSprite : groundSprite, Color.white);

        // 특수 지형 타일 (빨간색 벽점프 금지, 하늘색 얼음 슬라이딩)
        Tile redWallTile = CreateOrUpdateTileAsset($"{tilesDir}/Tile_Wall_RedNoJump.asset", specSprite != null ? specSprite : groundSprite, new Color(0.9f, 0.2f, 0.2f, 1f));
        Tile iceWallTile = CreateOrUpdateTileAsset($"{tilesDir}/Tile_Wall_IceSlide.asset", specSprite != null ? specSprite : groundSprite, new Color(0.2f, 0.85f, 1.0f, 1f));

        // 3. TilePalette 에셋 생성 (유니티 2D Tile Palette 창 전용)
        string paletteDir = "Assets/Tilemap/Palettes";
        if (!Directory.Exists(paletteDir)) Directory.CreateDirectory(paletteDir);

        string palettePath = $"{paletteDir}/MainTilePalette.prefab";
        GameObject paletteObj = new GameObject("MainTilePalette");
        var gridComp = paletteObj.AddComponent<Grid>();
        gridComp.cellSize = new Vector3(1, 1, 0);

        GameObject layerObj = new GameObject("Layer1");
        layerObj.transform.SetParent(paletteObj.transform);
        var tilemapComp = layerObj.AddComponent<Tilemap>();
        var rend = layerObj.AddComponent<TilemapRenderer>();
        rend.sharedMaterial = GetOrCreateTilemapMaterial();

        tilemapComp.SetTile(new Vector3Int(0, 0, 0), groundTile);
        tilemapComp.SetTile(new Vector3Int(1, 0, 0), platTile);
        tilemapComp.SetTile(new Vector3Int(2, 0, 0), bgTile);
        tilemapComp.SetTile(new Vector3Int(3, 0, 0), redWallTile);
        tilemapComp.SetTile(new Vector3Int(4, 0, 0), iceWallTile);
        tilemapComp.SetTile(new Vector3Int(5, 0, 0), taoGroundTile);
        tilemapComp.SetTile(new Vector3Int(6, 0, 0), cyberGroundTile);

        PrefabUtility.SaveAsPrefabAsset(paletteObj, palettePath);
        Object.DestroyImmediate(paletteObj);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log($"<color=green><b>[TilemapAssetPipeline] 타일 에셋 & 타일 팰리트 구축 완결: {palettePath}</b></color>");

        // 4. 프리팹 갱신 실행
        TilemapRoomPrefabBuilder.BuildTilemapRoomPrefab();
    }

    private static void FixTextureImporterPPU(string path, int ppu)
    {
        TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer != null)
        {
            bool modified = false;
            if (importer.spritePixelsPerUnit != ppu)
            {
                importer.spritePixelsPerUnit = ppu;
                modified = true;
            }
            if (importer.textureType != TextureImporterType.Sprite)
            {
                importer.textureType = TextureImporterType.Sprite;
                modified = true;
            }
            if (importer.filterMode != FilterMode.Point)
            {
                importer.filterMode = FilterMode.Point;
                modified = true;
            }

            if (modified)
            {
                importer.SaveAndReimport();
                Debug.Log($"[TilemapAssetPipeline] PPU {ppu} & Point Filter 설정 완결: {path}");
            }
        }
    }

    private static Material GetOrCreateTilemapMaterial()
    {
        string matPath = "Assets/Materials/TilemapDefaultMaterial.mat";
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
        }
        return mat;
    }

    private static Tile CreateOrUpdateTileAsset(string path, Sprite sprite, Color color)
    {
        Tile tile = AssetDatabase.LoadAssetAtPath<Tile>(path);
        if (tile == null)
        {
            tile = ScriptableObject.CreateInstance<Tile>();
            tile.sprite = sprite;
            tile.color = color;
            AssetDatabase.CreateAsset(tile, path);
        }
        else
        {
            tile.sprite = sprite;
            tile.color = color;
            EditorUtility.SetDirty(tile);
        }
        return tile;
    }
}
#endif
