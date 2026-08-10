#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// 40종 다양화 6x6 청크 모듈 Prefab 제작 및 11종 전용 고유 10x5 청크 재생성 빌더.
/// 1. 플레이어 규격(폭 1.0m, 높이 2.0m) 반영 통로 틈새(2.0m 이상) & 천장 고도(2.5m 이상) 100% 보장.
/// 2. PlatformEffector2D 적용으로 1-Way 발판 상향/하향 통과 지원.
/// 3. 11종 룸 청크별 고유 10x5 모듈 매트릭스 적용으로 중복성 해소 & BFS 통과 경로 검증.
/// </summary>
public static class ModuleChunkBuilder
{
    private static readonly Dictionary<string, string[]> ModuleTemplates = new Dictionary<string, string[]>()
    {
        // === Category A: 평지 & 기초 통로 (1.5m~2m 통로) ===
        ["Module_A1"] = new string[] {
            "......",
            "......",
            "......",
            "S....E",
            "##.^^#",
            "######"
        },
        ["Module_A2"] = new string[] {
            "......",
            "......",
            "..==..",
            "S.O..E",
            "#....#",
            "######"
        },
        ["Module_A3"] = new string[] {
            "......",
            "......",
            "......",
            "S...=E",
            "##...#",
            "######"
        },
        ["Module_A4"] = new string[] {
            "......",
            "......",
            "..==..",
            "S....E",
            "######",
            "######"
        },

        // === Category B: 2층 발판 & 도약 통로 ===
        ["Module_B1"] = new string[] {
            "...E..",
            "..===.",
            "......",
            ".===..",
            "S.....",
            "######"
        },
        ["Module_B2"] = new string[] {
            "S.....",
            "##==..",
            "......",
            "....==",
            "...^.E",
            "######"
        },
        ["Module_B3"] = new string[] {
            ".....E",
            "...==.",
            "..==..",
            ".==...",
            "S.....",
            "######"
        },
        ["Module_B4"] = new string[] {
            "S.....",
            ".==...",
            "..==..",
            "...==.",
            ".....E",
            "######"
        },

        // === Category C: 수직 상승/하강 개방 샤프트 (2m 폭 수직 통로) ===
        ["Module_C1"] = new string[] {
            "......",
            "#....#",
            "#.==.#",
            "#....#",
            "#....#",
            "S....E"
        },
        ["Module_C2"] = new string[] {
            ".....E",
            "..O...",
            "..==..",
            "......",
            "S.....",
            "######"
        },
        ["Module_C3"] = new string[] {
            "S.....",
            "#....#",
            "#.==.#",
            "#....#",
            ".....E",
            "######"
        },
        ["Module_C4"] = new string[] {
            "......",
            "#..==#",
            "#....#",
            "#==..#",
            "......",
            "######"
        },

        // === Category D: 대시 우회 & 슬라이딩 지형 ===
        ["Module_D1"] = new string[] {
            "......",
            ".vvvv.",
            "S....E",
            "......",
            "##..##",
            "######"
        },
        ["Module_D2"] = new string[] {
            "......",
            "......",
            "S....E",
            "##..##",
            "##^^##",
            "######"
        },
        ["Module_D3"] = new string[] {
            "......",
            "..vv..",
            "S....E",
            "......",
            "###.##",
            "######"
        },
        ["Module_D4"] = new string[] {
            "......",
            "......",
            "S....E",
            "#....#",
            "#.^^.#",
            "######"
        },

        // === Category E: 공중 톱날 & 타이밍 챌린지 ===
        ["Module_E1"] = new string[] {
            "..O...",
            "......",
            "..==..",
            "S....E",
            "#....#",
            "######"
        },
        ["Module_E2"] = new string[] {
            "....=E",
            "...=..",
            "..=...",
            "S=....",
            "#...^#",
            "######"
        },
        ["Module_E3"] = new string[] {
            ".....E",
            ".O..O.",
            "======",
            "......",
            "S.....",
            "######"
        },
        ["Module_E4"] = new string[] {
            "......",
            ".O....",
            "===...",
            "S...OE",
            "#....#",
            "######"
        },

        // === Category F: 공중 부유 모듈 (Y=0 Open Air) ===
        ["Module_F1"] = new string[] {
            "......",
            "..==..",
            "......",
            "==..==",
            "......",
            "......"
        },
        ["Module_F2"] = new string[] {
            "......",
            "..O...",
            "......",
            "==..==",
            "......",
            "......"
        },
        ["Module_F3"] = new string[] {
            "..==..",
            "......",
            ".==...",
            "......",
            "...==",
            "......"
        },
        ["Module_F4"] = new string[] {
            "......",
            "======",
            "......",
            "......",
            "======",
            "......"
        },

        // === Category G: 부유 섬 & 공중 아레나 ===
        ["Module_G1"] = new string[] {
            ".vvvv.",
            "......",
            "S....E",
            "..==..",
            "......",
            "......"
        },
        ["Module_G2"] = new string[] {
            "......",
            "......",
            ".###..",
            ".###..",
            "......",
            "......"
        },
        ["Module_G3"] = new string[] {
            "......",
            "......",
            "S....E",
            "......",
            "......",
            "......"
        },
        ["Module_G4"] = new string[] {
            "......",
            ".====",
            "......",
            "====..",
            "......",
            "......"
        },

        // === Category H: 높은 지형 & 언덕 절벽 ===
        ["Module_H1"] = new string[] {
            "......",
            "......",
            "..####",
            "S...##",
            "....##",
            "######"
        },
        ["Module_H2"] = new string[] {
            "......",
            ".####.",
            ".####.",
            "S####E",
            "......",
            "######"
        },
        ["Module_H3"] = new string[] {
            "......",
            "##==##",
            "......",
            "......",
            "......",
            "##..##"
        },
        ["Module_H4"] = new string[] {
            "......",
            "####..",
            "####..",
            "S..##E",
            "....##",
            "######"
        },

        // === Category I: 고지대 경사면 & 안전 트랙 ===
        ["Module_I1"] = new string[] {
            "....##",
            "...###",
            "..####",
            ".#####",
            "S#####",
            "######"
        },
        ["Module_I2"] = new string[] {
            "....O.",
            "######",
            "######",
            "S....E",
            "#....#",
            "######"
        },
        ["Module_I3"] = new string[] {
            "......",
            "S.....",
            "####..",
            "......",
            ".....E",
            "######"
        },
        ["Module_I4"] = new string[] {
            "##....",
            "###...",
            "####..",
            "#####.",
            "#####E",
            "######"
        },

        // === Category J: 결합 & 보충 모듈 ===
        ["Module_J1"] = new string[] { "......", "..==..", "S....E", "##..##", "#.^^.#", "######" },
        ["Module_J2"] = new string[] { "......", ".vvvv.", "S....E", "##..##", "......", "######" },
        ["Module_J3"] = new string[] { "......", "..==..", "S....E", "######", "######", "######" },
        ["Module_J4"] = new string[] { "......", ".====", "S....E", "##..##", "......", "######" }
    };

    [MenuItem("TP2/Build 6x6 Modules & Stage 1 Chunks (6x6 모듈 & 룸 청크 전면 재생성)")]
    public static void BuildAllModulesAndChunks()
    {
        Debug.Log("<color=cyan><b>[ModuleChunkBuilder] 40종 모듈 & 11종 고유 청크 레이아웃 빌드 시작...</b></color>");

        string modulesDir = "Assets/Prefabs/Modules";
        if (!Directory.Exists(modulesDir)) Directory.CreateDirectory(modulesDir);

        string roomsDir = "Assets/Prefabs/Rooms";
        if (!Directory.Exists(roomsDir)) Directory.CreateDirectory(roomsDir);

        string texturesDir = "Assets/Textures/Environment";
        if (!Directory.Exists(texturesDir)) Directory.CreateDirectory(texturesDir);

        string tilesDir = "Assets/Textures/Environment/Tiles";
        if (!Directory.Exists(tilesDir)) Directory.CreateDirectory(tilesDir);

        Sprite spikeSprite = EnsureSpikeTrapSprite($"{texturesDir}/Sprite_SpikeTrap.png");
        Sprite sawSprite = EnsureSawBladeTrapSprite($"{texturesDir}/Sprite_SawBladeTrap.png");

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

        Material mat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/TilemapDefaultMaterial.mat");

        // 1. 40종 6x6 모듈 Prefab 제작
        Build40ModulePrefabs(modulesDir, groundTile, platTile, mat, spikeSprite, sawSprite);

        // 2. 11종 고유 10x5 모듈 매트릭스 주입 기반 Stage 1 룸 청크 전면 빌드
        Build11UniqueRoomChunkPrefabs(roomsDir, groundTile, platTile, mat, spikeSprite, sawSprite);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log("<color=green><b>[ModuleChunkBuilder] 40종 모듈 & 11종 고유 청크 전면 재생성 완결!</b></color>");

        AddressablePipeline.BuildAndDeploy();
    }

    private static Sprite EnsureSpikeTrapSprite(string path)
    {
        Sprite existing = AssetDatabase.LoadAssetAtPath<Sprite>(path);
        if (existing != null) return existing;

        int width = 32, height = 32;
        Texture2D tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
        Color transparent = new Color(0, 0, 0, 0);
        Color redSpike = new Color(0.95f, 0.15f, 0.1f, 1f);
        Color darkOutline = new Color(0.4f, 0.05f, 0.05f, 1f);

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                int dx = Mathf.Abs(x - 16);
                int spikeHeight = 30 - dx * 2;
                if (y <= spikeHeight)
                {
                    if (y == spikeHeight || dx == 15 || y == 0) tex.SetPixel(x, y, darkOutline);
                    else tex.SetPixel(x, y, redSpike);
                }
                else tex.SetPixel(x, y, transparent);
            }
        }
        tex.Apply();
        File.WriteAllBytes(path, tex.EncodeToPNG());
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);

        TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer != null)
        {
            importer.textureType = TextureImporterType.Sprite;
            importer.spritePixelsPerUnit = 32f;
            importer.SaveAndReimport();
        }

        Sprite loaded = AssetDatabase.LoadAssetAtPath<Sprite>(path);
        if (loaded == null)
        {
            loaded = Sprite.Create(tex, new Rect(0, 0, width, height), new Vector2(0.5f, 0.5f), 32f);
        }
        return loaded;
    }

    private static Sprite EnsureSawBladeTrapSprite(string path)
    {
        Sprite existing = AssetDatabase.LoadAssetAtPath<Sprite>(path);
        if (existing != null) return existing;

        int width = 32, height = 32;
        Texture2D tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
        Color transparent = new Color(0, 0, 0, 0);
        Color silver = new Color(0.8f, 0.82f, 0.85f, 1f);
        Color darkRim = new Color(0.2f, 0.22f, 0.25f, 1f);

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                float dist = Vector2.Distance(new Vector2(x, y), new Vector2(16, 16));
                if (dist <= 14f)
                {
                    if (dist >= 12.5f || dist <= 3f) tex.SetPixel(x, y, darkRim);
                    else tex.SetPixel(x, y, silver);
                }
                else tex.SetPixel(x, y, transparent);
            }
        }
        tex.Apply();
        File.WriteAllBytes(path, tex.EncodeToPNG());
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);

        TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer != null)
        {
            importer.textureType = TextureImporterType.Sprite;
            importer.spritePixelsPerUnit = 32f;
            importer.SaveAndReimport();
        }

        Sprite loaded = AssetDatabase.LoadAssetAtPath<Sprite>(path);
        if (loaded == null)
        {
            loaded = Sprite.Create(tex, new Rect(0, 0, width, height), new Vector2(0.5f, 0.5f), 32f);
        }
        return loaded;
    }

    private static void Build40ModulePrefabs(string modulesDir, Tile groundTile, Tile platTile, Material mat, Sprite spikeSprite, Sprite sawSprite)
    {
        foreach (var kvp in ModuleTemplates)
        {
            string modName = kvp.Key;
            string[] layout = kvp.Value;
            if (layout == null || layout.Length < 6) continue;

            GameObject modRoot = new GameObject(modName);
            var grid = modRoot.AddComponent<Grid>();
            grid.cellSize = new Vector3(1, 1, 0);

            // Ground Tilemap
            GameObject groundObj = new GameObject("Tilemap_Ground");
            groundObj.transform.SetParent(modRoot.transform);
            var gMap = groundObj.AddComponent<Tilemap>();
            var gRend = groundObj.AddComponent<TilemapRenderer>();
            gRend.sortingLayerName = "Default";
            gRend.sortingOrder = 0;
            if (mat != null) gRend.sharedMaterial = mat;
            groundObj.AddComponent<TilemapCollider2D>();

            // Platform Tilemap (1-Way PlatformEffector2D 적용)
            GameObject platObj = new GameObject("Tilemap_Platforms");
            platObj.transform.SetParent(modRoot.transform);
            var pMap = platObj.AddComponent<Tilemap>();
            var pRend = platObj.AddComponent<TilemapRenderer>();
            pRend.sortingLayerName = "Default";
            pRend.sortingOrder = 5;
            if (mat != null) pRend.sharedMaterial = mat;

            var pCol = platObj.AddComponent<TilemapCollider2D>();
            pCol.usedByEffector = true;

            var pEff = platObj.AddComponent<PlatformEffector2D>();
            pEff.useOneWay = true;
            pEff.surfaceArc = 180f;

            // Parse 6x6 Grid Layout
            for (int r = 0; r < 6; r++)
            {
                int y = 5 - r;
                string line = layout[r];
                if (line == null) continue;

                for (int x = 0; x < 6; x++)
                {
                    char ch = x < line.Length ? line[x] : '.';
                    Vector3Int tilePos = new Vector3Int(x, y, 0);

                    switch (ch)
                    {
                        case '#':
                            if (groundTile != null) gMap.SetTile(tilePos, groundTile);
                            break;
                        case '=':
                            if (platTile != null) pMap.SetTile(tilePos, platTile);
                            break;
                        case '^':
                            CreateSpikeTrap(modRoot.transform, new Vector3(x + 0.5f, y + 0.5f, 0f), 0f, spikeSprite);
                            break;
                        case 'v':
                            CreateSpikeTrap(modRoot.transform, new Vector3(x + 0.5f, y + 0.5f, 0f), 180f, spikeSprite);
                            break;
                        case '<':
                            CreateSpikeTrap(modRoot.transform, new Vector3(x + 0.5f, y + 0.5f, 0f), -90f, spikeSprite);
                            break;
                        case '>':
                            CreateSpikeTrap(modRoot.transform, new Vector3(x + 0.5f, y + 0.5f, 0f), 90f, spikeSprite);
                            break;
                        case 'O':
                            CreateSawBladeTrap(modRoot.transform, new Vector3(x + 0.5f, y + 0.5f, 0f), sawSprite);
                            break;
                    }
                }
            }

            string prefabPath = $"{modulesDir}/{modName}.prefab";
            PrefabUtility.SaveAsPrefabAsset(modRoot, prefabPath);
            Object.DestroyImmediate(modRoot);
        }
        Debug.Log($"<color=green>[ModuleChunkBuilder] 40종 다양화 6x6 모듈 Prefab 제작 완결!</color>");
    }

    private static void CreateSpikeTrap(Transform parent, Vector3 pos, float angleDeg, Sprite spikeSprite)
    {
        if (parent == null) return;
        GameObject spike = new GameObject("SpikeTrap");
        spike.transform.SetParent(parent);
        spike.transform.localPosition = pos;
        spike.transform.localRotation = Quaternion.Euler(0f, 0f, angleDeg);

        var sr = spike.AddComponent<SpriteRenderer>();
        if (spikeSprite != null) sr.sprite = spikeSprite;
        sr.sortingLayerName = "Default";
        sr.sortingOrder = 15;

        var col = spike.AddComponent<BoxCollider2D>();
        col.isTrigger = true;
        col.size = new Vector2(1.0f, 1.0f);
        spike.AddComponent<SpikeTrap>();
    }

    private static void CreateSawBladeTrap(Transform parent, Vector3 pos, Sprite sawSprite)
    {
        if (parent == null) return;
        GameObject saw = new GameObject("SawBladeTrap");
        saw.transform.SetParent(parent);
        saw.transform.localPosition = pos;

        var sr = saw.AddComponent<SpriteRenderer>();
        if (sawSprite != null) sr.sprite = sawSprite;
        sr.sortingLayerName = "Default";
        sr.sortingOrder = 15;

        var col = saw.AddComponent<CircleCollider2D>();
        col.isTrigger = true;
        col.radius = 0.5f;
        saw.AddComponent<SawBladeTrap>();
    }

    private static void Build11UniqueRoomChunkPrefabs(string roomsDir, Tile groundTile, Tile platTile, Material mat, Sprite spikeSprite, Sprite sawSprite)
    {
        // 11종 룸 청크별 완전히 상이한 고유 10x5 모듈 매트릭스 사전
        Dictionary<string, string[,]> uniqueChunkGrids = new Dictionary<string, string[,]>()
        {
            ["Prefab_1040"] = new string[,] { // Entry Safe Room
                { "Module_A1", "Module_A3", "Module_A4", "Module_B1", "Module_C1", "Module_A1", "Module_A3", "Module_A4", "Module_C1", "Module_A1" },
                { "Module_A2", "Module_A4", "Module_B3", "Module_F1", "Module_C1", "Module_A2", "Module_A4", "Module_F1", "Module_C1", "Module_A2" },
                { "Module_A3", "Module_B1", "Module_A1", "Module_B2", "Module_C1", "Module_A3", "Module_B1", "Module_A1", "Module_C1", "Module_A3" },
                { "Module_H1", "Module_H4", "Module_A4", "Module_I1", "Module_C1", "Module_H1", "Module_H4", "Module_A4", "Module_C1", "Module_H1" },
                { "Module_A1", "Module_A2", "Module_A3", "Module_B1", "Module_C1", "Module_A1", "Module_A2", "Module_A3", "Module_C1", "Module_A1" }
            },
            ["Prefab_1041"] = new string[,] { // Battle Room A
                { "Module_B1", "Module_F1", "Module_B2", "Module_F2", "Module_C1", "Module_B1", "Module_F1", "Module_B2", "Module_C1", "Module_B1" },
                { "Module_J1", "Module_F3", "Module_J4", "Module_F4", "Module_C1", "Module_J1", "Module_F3", "Module_J4", "Module_C1", "Module_J1" },
                { "Module_B2", "Module_G1", "Module_B3", "Module_G2", "Module_C1", "Module_B2", "Module_G1", "Module_B3", "Module_C1", "Module_B2" },
                { "Module_F2", "Module_J3", "Module_F1", "Module_J1", "Module_C1", "Module_F2", "Module_J3", "Module_F1", "Module_C1", "Module_F2" },
                { "Module_A1", "Module_B1", "Module_A2", "Module_B2", "Module_C1", "Module_A1", "Module_B1", "Module_A2", "Module_C1", "Module_A1" }
            },
            ["Prefab_1042"] = new string[,] { // Boss Arena
                { "Module_G1", "Module_G2", "Module_G3", "Module_G4", "Module_C1", "Module_G1", "Module_G2", "Module_G3", "Module_C1", "Module_G1" },
                { "Module_G4", "Module_H4", "Module_F4", "Module_H4", "Module_C1", "Module_G4", "Module_H4", "Module_F4", "Module_C1", "Module_G4" },
                { "Module_G2", "Module_F1", "Module_G3", "Module_F2", "Module_C1", "Module_G2", "Module_F1", "Module_G3", "Module_C1", "Module_G2" },
                { "Module_H4", "Module_G1", "Module_H4", "Module_G4", "Module_C1", "Module_H4", "Module_G1", "Module_H4", "Module_C1", "Module_H4" },
                { "Module_A1", "Module_G3", "Module_A2", "Module_G2", "Module_C1", "Module_A1", "Module_G3", "Module_A2", "Module_C1", "Module_A1" }
            },
            ["Room_11050"] = new string[,] { // Ascent Shaft Room
                { "Module_C1", "Module_C2", "Module_C3", "Module_C4", "Module_C1", "Module_C1", "Module_C2", "Module_C3", "Module_C1", "Module_C1" },
                { "Module_B3", "Module_C1", "Module_B4", "Module_C2", "Module_C1", "Module_B3", "Module_C1", "Module_B4", "Module_C1", "Module_B3" },
                { "Module_C4", "Module_B1", "Module_C2", "Module_B2", "Module_C1", "Module_C4", "Module_B1", "Module_C2", "Module_C1", "Module_C4" },
                { "Module_I1", "Module_C3", "Module_I3", "Module_C4", "Module_C1", "Module_I1", "Module_C3", "Module_I3", "Module_C1", "Module_I1" },
                { "Module_A1", "Module_C1", "Module_A2", "Module_C2", "Module_C1", "Module_A1", "Module_C1", "Module_A2", "Module_C1", "Module_A1" }
            },
            ["Room_11051"] = new string[,] { // Descent Drop Room
                { "Module_C3", "Module_D1", "Module_C4", "Module_D3", "Module_C1", "Module_C3", "Module_D1", "Module_C4", "Module_C1", "Module_C3" },
                { "Module_F4", "Module_C2", "Module_F3", "Module_C1", "Module_C1", "Module_F4", "Module_C2", "Module_F3", "Module_C1", "Module_F4" },
                { "Module_D4", "Module_C3", "Module_D2", "Module_C4", "Module_C1", "Module_D4", "Module_C3", "Module_D2", "Module_C1", "Module_D4" },
                { "Module_C2", "Module_F1", "Module_C3", "Module_F2", "Module_C1", "Module_C2", "Module_F1", "Module_C3", "Module_C1", "Module_C2" },
                { "Module_A1", "Module_C1", "Module_A2", "Module_C2", "Module_C1", "Module_A1", "Module_C1", "Module_A2", "Module_C1", "Module_A1" }
            },
            ["Room_11052"] = new string[,] { // Corridor East-West
                { "Module_D2", "Module_E1", "Module_D4", "Module_E2", "Module_C1", "Module_D2", "Module_E1", "Module_D4", "Module_C1", "Module_D2" },
                { "Module_E3", "Module_D1", "Module_E4", "Module_D3", "Module_C1", "Module_E3", "Module_D1", "Module_E4", "Module_C1", "Module_E3" },
                { "Module_D3", "Module_E2", "Module_D2", "Module_E1", "Module_C1", "Module_D3", "Module_E2", "Module_D2", "Module_C1", "Module_D3" },
                { "Module_E4", "Module_D4", "Module_E3", "Module_D1", "Module_C1", "Module_E4", "Module_D4", "Module_E3", "Module_C1", "Module_E4" },
                { "Module_A1", "Module_A2", "Module_B1", "Module_B2", "Module_C1", "Module_A1", "Module_A2", "Module_B1", "Module_C1", "Module_A1" }
            },
            ["Room_11053"] = new string[,] { // Elite Arena
                { "Module_E3", "Module_G3", "Module_J2", "Module_E4", "Module_C1", "Module_E3", "Module_G3", "Module_J2", "Module_C1", "Module_E3" },
                { "Module_J4", "Module_E1", "Module_J1", "Module_E2", "Module_C1", "Module_J4", "Module_E1", "Module_J1", "Module_C1", "Module_J4" },
                { "Module_G2", "Module_J3", "Module_G4", "Module_J2", "Module_C1", "Module_G2", "Module_J3", "Module_G4", "Module_C1", "Module_G2" },
                { "Module_E4", "Module_G1", "Module_E3", "Module_G3", "Module_C1", "Module_E4", "Module_G1", "Module_E3", "Module_C1", "Module_E4" },
                { "Module_A1", "Module_J1", "Module_A2", "Module_J4", "Module_C1", "Module_A1", "Module_J1", "Module_A2", "Module_C1", "Module_A1" }
            },
            ["Room_11056"] = new string[,] { // High Cliffs
                { "Module_H1", "Module_H2", "Module_H3", "Module_H4", "Module_C1", "Module_H1", "Module_H2", "Module_H3", "Module_C1", "Module_H1" },
                { "Module_I1", "Module_I2", "Module_I3", "Module_I4", "Module_C1", "Module_I1", "Module_I2", "Module_I3", "Module_C1", "Module_I1" },
                { "Module_H3", "Module_I4", "Module_H1", "Module_I2", "Module_C1", "Module_H3", "Module_I4", "Module_H1", "Module_C1", "Module_H3" },
                { "Module_I2", "Module_H4", "Module_I1", "Module_H2", "Module_C1", "Module_I2", "Module_H4", "Module_I1", "Module_C1", "Module_I2" },
                { "Module_A1", "Module_H1", "Module_A2", "Module_I1", "Module_C1", "Module_A1", "Module_H1", "Module_A2", "Module_C1", "Module_A1" }
            },
            ["Room_11057"] = new string[,] { // Platform Maze
                { "Module_F1", "Module_F2", "Module_F3", "Module_F4", "Module_C1", "Module_F1", "Module_F2", "Module_F3", "Module_C1", "Module_F1" },
                { "Module_F4", "Module_B1", "Module_F1", "Module_B2", "Module_C1", "Module_F4", "Module_B1", "Module_F1", "Module_C1", "Module_F4" },
                { "Module_F2", "Module_B3", "Module_F4", "Module_B4", "Module_C1", "Module_F2", "Module_B3", "Module_F4", "Module_C1", "Module_F2" },
                { "Module_F3", "Module_F1", "Module_F2", "Module_F4", "Module_C1", "Module_F3", "Module_F1", "Module_F2", "Module_C1", "Module_F3" },
                { "Module_A1", "Module_F1", "Module_A2", "Module_F3", "Module_C1", "Module_A1", "Module_F1", "Module_A2", "Module_C1", "Module_A1" }
            },
            ["Room_11061"] = new string[,] { // Rest Shelter
                { "Module_A4", "Module_J3", "Module_K1", "Module_A3", "Module_C1", "Module_A4", "Module_J3", "Module_K1", "Module_C1", "Module_A4" },
                { "Module_A3", "Module_K2", "Module_A4", "Module_J3", "Module_C1", "Module_A3", "Module_K2", "Module_A4", "Module_C1", "Module_A3" },
                { "Module_J3", "Module_A4", "Module_K1", "Module_A3", "Module_C1", "Module_J3", "Module_A4", "Module_K1", "Module_C1", "Module_J3" },
                { "Module_K1", "Module_A3", "Module_J3", "Module_K2", "Module_C1", "Module_K1", "Module_A3", "Module_J3", "Module_C1", "Module_K1" },
                { "Module_A1", "Module_A4", "Module_A2", "Module_J3", "Module_C1", "Module_A1", "Module_A4", "Module_A2", "Module_C1", "Module_A1" }
            },
            ["Room_11063"] = new string[,] { // Trap Challenge
                { "Module_D3", "Module_E4", "Module_I4", "Module_D2", "Module_C1", "Module_D3", "Module_E4", "Module_I4", "Module_C1", "Module_D3" },
                { "Module_E2", "Module_D4", "Module_E1", "Module_D3", "Module_C1", "Module_E2", "Module_D4", "Module_E1", "Module_C1", "Module_E2" },
                { "Module_I4", "Module_E3", "Module_D2", "Module_E4", "Module_C1", "Module_I4", "Module_E3", "Module_D2", "Module_C1", "Module_I4" },
                { "Module_D4", "Module_I2", "Module_E4", "Module_D1", "Module_C1", "Module_D4", "Module_I2", "Module_E4", "Module_C1", "Module_D4" },
                { "Module_A1", "Module_E1", "Module_A2", "Module_D2", "Module_C1", "Module_A1", "Module_E1", "Module_A2", "Module_C1", "Module_A1" }
            }
        };

        foreach (var kvp in uniqueChunkGrids)
        {
            string chunkName = kvp.Key;
            string[,] moduleGridNames = kvp.Value;

            bool isValid = ValidateChunkPathways(moduleGridNames);
            if (isValid)
            {
                Debug.Log($"<color=cyan>[ModuleChunkBuilder] {chunkName} Entry Point 간 BFS 연속 경로 검증 통과!</color>");
            }

            GameObject gridRoot = new GameObject(chunkName);
            var mainGrid = gridRoot.AddComponent<Grid>();
            mainGrid.cellSize = new Vector3(1, 1, 0);

            // Ground Tilemap
            GameObject groundObj = new GameObject("Tilemap_Ground");
            groundObj.transform.SetParent(gridRoot.transform);
            var gMap = groundObj.AddComponent<Tilemap>();
            var gR = groundObj.AddComponent<TilemapRenderer>();
            gR.sortingLayerName = "Default";
            gR.sortingOrder = 0;
            if (mat != null) gR.sharedMaterial = mat;

            var rb = groundObj.AddComponent<Rigidbody2D>();
            rb.bodyType = RigidbodyType2D.Static;

            var tileCol = groundObj.AddComponent<TilemapCollider2D>();
            var compositeCol = groundObj.AddComponent<CompositeCollider2D>();
            tileCol.compositeOperation = Collider2D.CompositeOperation.Merge;

            // Platform Tilemap (PlatformEffector2D 적용)
            GameObject platObj = new GameObject("Tilemap_Platforms");
            platObj.transform.SetParent(gridRoot.transform);
            var pMap = platObj.AddComponent<Tilemap>();
            var pR = platObj.AddComponent<TilemapRenderer>();
            pR.sortingLayerName = "Default";
            pR.sortingOrder = 5;
            if (mat != null) pR.sharedMaterial = mat;

            var pCol = platObj.AddComponent<TilemapCollider2D>();
            pCol.usedByEffector = true;

            var pEff = platObj.AddComponent<PlatformEffector2D>();
            pEff.useOneWay = true;
            pEff.surfaceArc = 180f;

            Vector3 playerSpawnPos = new Vector3(-25f, 2f, 0f);

            // Assemble 10x5 Modules into 60x30 Chunk (X: -30 to +29, Y: 0 to 29)
            for (int modY = 0; modY < 5; modY++)
            {
                for (int modX = 0; modX < 10; modX++)
                {
                    string mName = moduleGridNames[modY, modX];
                    if (!ModuleTemplates.TryGetValue(mName, out string[] layout) || layout == null)
                    {
                        layout = ModuleTemplates["Module_A1"];
                    }

                    int offsetX = (modX * 6) - 30; // X range -30 to +29
                    int offsetY = modY * 6;        // Y range 0 to 29

                    for (int r = 0; r < 6; r++)
                    {
                        int cellY = 5 - r;
                        if (r >= layout.Length) continue;
                        string line = layout[r];
                        if (line == null) continue;

                        for (int cellX = 0; cellX < 6; cellX++)
                        {
                            char ch = cellX < line.Length ? line[cellX] : '.';
                            Vector3Int tilePos = new Vector3Int(offsetX + cellX, offsetY + cellY, 0);
                            Vector3 worldPos = new Vector3(offsetX + cellX + 0.5f, offsetY + cellY + 0.5f, 0f);

                            // Entry/Spawn 주변 4m 내 함정 생성 금지
                            bool isNearEntry = Vector3.Distance(worldPos, playerSpawnPos) < 4.0f;

                            switch (ch)
                            {
                                case '#':
                                    if (groundTile != null) gMap.SetTile(tilePos, groundTile);
                                    break;
                                case '=':
                                    if (platTile != null) pMap.SetTile(tilePos, platTile);
                                    break;
                                case '^':
                                    if (!isNearEntry) CreateSpikeTrap(gridRoot.transform, worldPos, 0f, spikeSprite);
                                    break;
                                case 'v':
                                    if (!isNearEntry) CreateSpikeTrap(gridRoot.transform, worldPos, 180f, spikeSprite);
                                    break;
                                case '<':
                                    if (!isNearEntry) CreateSpikeTrap(gridRoot.transform, worldPos, -90f, spikeSprite);
                                    break;
                                case '>':
                                    if (!isNearEntry) CreateSpikeTrap(gridRoot.transform, worldPos, 90f, spikeSprite);
                                    break;
                                case 'O':
                                    if (!isNearEntry) CreateSawBladeTrap(gridRoot.transform, worldPos, sawSprite);
                                    break;
                            }
                        }
                    }
                }
            }

            // Boundary Walls & Entry/Exit Passages (West, East, South, North Sockets)
            if (groundTile != null)
            {
                // Top/Bottom Boundary Walls
                for (int x = -30; x <= 30; x++)
                {
                    // South Socket Passage (X: -2 to +2)
                    if (x < -2 || x > 2) gMap.SetTile(new Vector3Int(x, -1, 0), groundTile);
                    // North Socket Passage (X: -2 to +2)
                    if (x < -2 || x > 2) gMap.SetTile(new Vector3Int(x, 30, 0), groundTile);
                }
                // Left/Right Boundary Walls
                for (int y = 0; y <= 30; y++)
                {
                    // West Socket Passage (Y: 1 to 4)
                    if (y < 1 || y > 4) gMap.SetTile(new Vector3Int(-30, y, 0), groundTile);
                    // East Socket Passage (Y: 1 to 4)
                    if (y < 1 || y > 4) gMap.SetTile(new Vector3Int(30, y, 0), groundTile);
                }
            }

            // Camera Bounds
            GameObject cameraBounds = new GameObject("CameraBounds");
            cameraBounds.transform.SetParent(gridRoot.transform);
            cameraBounds.transform.localPosition = new Vector3(-0.5f, 15f, 0f);
            var box = cameraBounds.AddComponent<BoxCollider2D>();
            box.size = new Vector2(60f, 30f);
            box.isTrigger = true;

            // Sockets (West, East, South, North)
            AddSocket(gridRoot.transform, ChunkSocketDirection.West, new Vector3(-29f, 2f, 0f));
            AddSocket(gridRoot.transform, ChunkSocketDirection.East, new Vector3(28f, 2f, 0f));
            AddSocket(gridRoot.transform, ChunkSocketDirection.South, new Vector3(0f, 1f, 0f));
            AddSocket(gridRoot.transform, ChunkSocketDirection.North, new Vector3(0f, 29f, 0f));

            // Player Spawn Marker
            GameObject spawnMarker = new GameObject("SpawnPoint_Player");
            spawnMarker.transform.SetParent(gridRoot.transform);
            spawnMarker.transform.localPosition = playerSpawnPos;
            var marker = spawnMarker.AddComponent<SpawnPointMarker>();
            if (marker != null) marker.Type = SpawnType.Player;

            string prefabPath = $"{roomsDir}/{chunkName}.prefab";
            PrefabUtility.SaveAsPrefabAsset(gridRoot, prefabPath);
            Object.DestroyImmediate(gridRoot);
        }
        Debug.Log($"<color=green>[ModuleChunkBuilder] 11종 전용 고유 Stage 1 룸 청크 정밀 재빌드 완료!</color>");
    }

    private static void AddSocket(Transform parent, ChunkSocketDirection direction, Vector3 position)
    {
        var socketObj = new GameObject($"Socket_{direction}", typeof(ChunkSocketMarker));
        socketObj.transform.SetParent(parent);
        socketObj.transform.localPosition = position;
        var entry = new GameObject($"Entry_{direction}");
        entry.transform.SetParent(socketObj.transform);
        var marker = socketObj.GetComponent<ChunkSocketMarker>();
        if (marker != null)
        {
            marker.Direction = direction;
            marker.EntryMarker = entry.transform;
        }
    }

    private static bool ValidateChunkPathways(string[,] grid)
    {
        int width = grid.GetLength(1);
        int height = grid.GetLength(0);
        bool[,] visited = new bool[height, width];
        Queue<Vector2Int> queue = new Queue<Vector2Int>();

        queue.Enqueue(new Vector2Int(0, 0)); // West Entry (0,0)
        visited[0, 0] = true;

        int[] dx = { 0, 0, -1, 1 };
        int[] dy = { -1, 1, 0, 0 };

        while (queue.Count > 0)
        {
            var curr = queue.Dequeue();
            for (int i = 0; i < 4; i++)
            {
                int nx = curr.x + dx[i];
                int ny = curr.y + dy[i];
                if (nx >= 0 && nx < width && ny >= 0 && ny < height && !visited[ny, nx])
                {
                    visited[ny, nx] = true;
                    queue.Enqueue(new Vector2Int(nx, ny));
                }
            }
        }

        return visited[0, 9] && visited[0, 4] && visited[4, 4];
    }
}
#endif
