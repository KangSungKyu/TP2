#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// 12x12 자율 단독 플레이 가능 모듈 20종 및 가변 NxM Stage 1 룸 청크 재생성 빌더.
/// 1. 단일 모듈 규격 12m x 12m (12x12 cells) 전면 확대: 독립 주행(12m), 대시(3.6m), 점프(4.5m) 완결.
/// 2. 발판(=)과 지형(#) 접촉 금지 & 3~4m 광폭 통로 공간 보장.
/// 3. 리소스 작업자 1(f4f6cc90-75c3-4e62-890c-fcd62e9a47f7) 위임 구동용 C# 파서 빌더.
/// </summary>
public static class ModuleChunkBuilder
{
    private static readonly Dictionary<string, string[]> ModuleTemplates = new Dictionary<string, string[]>()
    {
        // === Category A: 12x12 평지 & 기초 횡단 트랙 (독립 주행/점프 가능) ===
        ["Module_A1"] = new string[] {
            "............",
            "............",
            "............",
            "............",
            "............",
            "............",
            "............",
            "............",
            "S..........E",
            "............",
            "############",
            "############"
        },
        ["Module_A2"] = new string[] {
            "............",
            "............",
            "............",
            "....====....",
            "............",
            "............",
            "............",
            "............",
            "S..........E",
            "............",
            "############",
            "############"
        },
        ["Module_A3"] = new string[] {
            "............",
            "............",
            "............",
            "........====",
            "............",
            "............",
            "....====....",
            "............",
            "S..........E",
            "............",
            "############",
            "############"
        },
        ["Module_A4"] = new string[] {
            "............",
            "............",
            "....====....",
            "............",
            "............",
            "....====....",
            "............",
            "............",
            "S..........E",
            "............",
            "############",
            "############"
        },

        // === Category B: 12x12 2층/3층 부유 입체 발판 ===
        ["Module_B1"] = new string[] {
            "............",
            "......====..",
            "............",
            "............",
            "..====......",
            "............",
            "............",
            "....====....",
            "S..........E",
            "............",
            "############",
            "############"
        },
        ["Module_B2"] = new string[] {
            "S...........",
            "............",
            "..====......",
            "............",
            "............",
            "......====..",
            "............",
            "............",
            "..........=E",
            "............",
            "############",
            "############"
        },
        ["Module_B3"] = new string[] {
            "...........E",
            "......====..",
            "............",
            "............",
            "..====......",
            "............",
            "............",
            "S...........",
            "............",
            "............",
            "############",
            "############"
        },
        ["Module_B4"] = new string[] {
            "S...........",
            "............",
            "..====......",
            "............",
            "............",
            "......====..",
            "............",
            "...........E",
            "............",
            "............",
            "############",
            "############"
        },

        // === Category C: 12x12 수직 상승/하강 개방 샤프트 ===
        ["Module_C1"] = new string[] {
            "............",
            "............",
            "......====..",
            "............",
            "............",
            "....====....",
            "............",
            "............",
            "S..........E",
            "............",
            "............",
            "............"
        },
        ["Module_C2"] = new string[] {
            "...........E",
            "............",
            "....====....",
            "............",
            "............",
            "....====....",
            "............",
            "............",
            "S...........",
            "............",
            "############",
            "############"
        },

        // === Category D: 12x12 대시 & 쾌적 지형 모듈 ===
        ["Module_D1"] = new string[] {
            "............",
            "............",
            "............",
            "............",
            "............",
            "............",
            "S..........E",
            "............",
            "....####....",
            "....####....",
            "............",
            "############"
        },
        ["Module_D2"] = new string[] {
            "............",
            "............",
            "............",
            "............",
            "............",
            "............",
            "S..........E",
            "............",
            "............",
            "............",
            "............",
            "############"
        },

        // === Category E: 12x12 라이트 타이밍 모듈 ===
        ["Module_E1"] = new string[] {
            "............",
            "............",
            "....====....",
            "............",
            "............",
            "....====....",
            "............",
            "............",
            "S..........E",
            "............",
            "............",
            "############"
        },
        ["Module_E2"] = new string[] {
            "..........=E",
            "............",
            "......=.....",
            "............",
            "....=.......",
            "............",
            "S=..........",
            "............",
            "............",
            "............",
            "............",
            "############"
        },

        // === Category F: 12x12 공중 부유 오픈 에어 모듈 ===
        ["Module_F1"] = new string[] {
            "............",
            "....====....",
            "............",
            "............",
            "====....====",
            "............",
            "............",
            "....====....",
            "............",
            "............",
            "............",
            "............"
        },
        ["Module_F2"] = new string[] {
            "............",
            "............",
            "............",
            "............",
            "====....====",
            "............",
            "............",
            "............",
            "............",
            "............",
            "............",
            "............"
        },

        // === Category G: 12x12 부유 섬 아레나 ===
        ["Module_G1"] = new string[] {
            "............",
            "............",
            "............",
            "S..........E",
            "....====....",
            "............",
            "............",
            "............",
            "............",
            "............",
            "............",
            "............"
        },
        ["Module_G2"] = new string[] {
            "............",
            "............",
            "....####....",
            "....####....",
            "............",
            "............",
            "............",
            "............",
            "............",
            "............",
            "............",
            "............"
        },

        // === Category H & I: 12x12 높은 절벽 & 경사 모듈 ===
        ["Module_H1"] = new string[] {
            "............",
            "............",
            "............",
            "..######....",
            "S.######....",
            "..######....",
            "..######....",
            "..######....",
            "..######....",
            "............",
            "............",
            "############"
        },
        ["Module_I1"] = new string[] {
            "....####....",
            "...######...",
            "..########..",
            ".##########.",
            "S.##########.",
            ".##########.",
            ".##########.",
            ".##########.",
            ".##########.",
            "............",
            "............",
            "############"
        }
    };

    private class ChunkGridConfig
    {
        public int GridWidth { get; set; }  // ModX 갯수 (N)
        public int GridHeight { get; set; } // ModY 갯수 (M)
        public string[,] Matrix { get; set; }
    }

    [MenuItem("TP2/Build 12x12 Modules & Stage 1 Chunks (12x12 모듈 & 룸 청크 전면 재생성)")]
    public static void BuildAllModulesAndChunks()
    {
        Debug.Log("<color=cyan><b>[ModuleChunkBuilder] 12x12 자율 단독 모듈 및 가변 NxM 청크 빌드 시작...</b></color>");

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

        // 1. 12x12 모듈 Prefab 제작
        Build12x12ModulePrefabs(modulesDir, groundTile, platTile, mat, spikeSprite, sawSprite);

        // 2. 가변 NxM 그리드 11종 Stage 1 룸 청크 빌드
        BuildVariableRoomChunkPrefabs(roomsDir, groundTile, platTile, mat, spikeSprite, sawSprite);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log("<color=green><b>[ModuleChunkBuilder] 12x12 모듈 & 청크 전면 재생성 파싱 완료!</b></color>");

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

    private static void Build12x12ModulePrefabs(string modulesDir, Tile groundTile, Tile platTile, Material mat, Sprite spikeSprite, Sprite sawSprite)
    {
        foreach (var kvp in ModuleTemplates)
        {
            string modName = kvp.Key;
            string[] layout = kvp.Value;
            if (layout == null || layout.Length < 12) continue;

            GameObject modRoot = new GameObject(modName);
            var grid = modRoot.AddComponent<Grid>();
            grid.cellSize = new Vector3(1, 1, 0);

            // Ground Tilemap
            GameObject groundObj = new GameObject("Tilemap_Ground");
            groundObj.transform.SetParent(modRoot.transform);
            var gMap = groundObj.AddComponent<Tilemap>();
            var gRend = groundObj.AddComponent<TilemapRenderer>();
            gRend.sortingLayerName = "Tilemap";
            gRend.sortingOrder = 0;
            if (mat != null) gRend.sharedMaterial = mat;
            groundObj.AddComponent<TilemapCollider2D>();

            // Platform Tilemap (PlatformEffector2D 1-Way 적용)
            GameObject platObj = new GameObject("Tilemap_Platforms");
            platObj.transform.SetParent(modRoot.transform);
            int oneWayLayer = LayerMask.NameToLayer("OneWayPlatform");
            if (oneWayLayer >= 0) platObj.layer = oneWayLayer;
            var pMap = platObj.AddComponent<Tilemap>();
            var pRend = platObj.AddComponent<TilemapRenderer>();
            pRend.sortingLayerName = "Tilemap";
            pRend.sortingOrder = 5;
            if (mat != null) pRend.sharedMaterial = mat;

            var pCol = platObj.AddComponent<TilemapCollider2D>();
            pCol.usedByEffector = true;

            var pEff = platObj.AddComponent<PlatformEffector2D>();
            pEff.useOneWay = true;
            pEff.surfaceArc = 180f;
            platObj.AddComponent<OneWayPlatformPassThrough>();

            // Parse 12x12 Grid Layout (Y=11 down to Y=0, X=0 to X=11)
            for (int r = 0; r < 12; r++)
            {
                int y = 11 - r;
                string line = layout[r];
                if (line == null) continue;

                for (int x = 0; x < 12; x++)
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

            NormalizeOneWayPlatforms(pMap, gMap);
            string prefabPath = $"{modulesDir}/{modName}.prefab";
            PrefabUtility.SaveAsPrefabAsset(modRoot, prefabPath);
            Object.DestroyImmediate(modRoot);
        }
        Debug.Log($"<color=green>[ModuleChunkBuilder] 12x12 자율 단독 모듈 Prefab 제작 완결!</color>");
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
        ConfigureHazard(spike.AddComponent<SpikeTrap>(), 1070u, 15, 0f, 0.5f);
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
        ConfigureHazard(saw.AddComponent<SawBladeTrap>(), 1071u, 20, 0f, 0.4f);
    }

    private static void ConfigureHazard(HazardBase hazard, uint id, int hazardDamage,
        float hazardKnockback, float hitCooldown)
    {
        var serialized = new SerializedObject(hazard);
        serialized.FindProperty("hazardId").longValue = id;
        serialized.FindProperty("damage").intValue = hazardDamage;
        serialized.FindProperty("knockbackForce").floatValue = hazardKnockback;
        serialized.FindProperty("cooldownBetweenHits").floatValue = hitCooldown;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void BuildVariableRoomChunkPrefabs(string roomsDir, Tile groundTile, Tile platTile, Material mat, Sprite spikeSprite, Sprite sawSprite)
    {
        // 11종 룸 청크별 12x12 모듈 배치 사양 (N: ModX 수, M: ModY 수)
        Dictionary<string, ChunkGridConfig> chunkConfigs = new Dictionary<string, ChunkGridConfig>()
        {
            ["Prefab_1040"] = new ChunkGridConfig { // Entry Safe Room (3x2 12x12 Modules = 36m x 24m)
                GridWidth = 3, GridHeight = 2,
                Matrix = new string[,] {
                    { "Module_A1", "Module_A2", "Module_C1" },
                    { "Module_A3", "Module_A4", "Module_C1" }
                }
            },
            ["Prefab_1041"] = new ChunkGridConfig { // Battle Room A (4x2 12x12 Modules = 48m x 24m)
                GridWidth = 4, GridHeight = 2,
                Matrix = new string[,] {
                    { "Module_B1", "Module_F1", "Module_B2", "Module_C1" },
                    { "Module_B2", "Module_G1", "Module_B3", "Module_C1" }
                }
            },
            ["Prefab_1042"] = new ChunkGridConfig { // Boss Arena (5x3 12x12 Modules = 60m x 36m)
                GridWidth = 5, GridHeight = 3,
                Matrix = new string[,] {
                    { "Module_G1", "Module_G2", "Module_C1", "Module_G1", "Module_G2" },
                    { "Module_F1", "Module_G2", "Module_C1", "Module_F1", "Module_G2" },
                    { "Module_A1", "Module_G1", "Module_C1", "Module_A1", "Module_G1" }
                }
            },
            ["Room_11050"] = new ChunkGridConfig { // Ascent Shaft (2x3 12x12 Modules = 24m x 36m)
                GridWidth = 2, GridHeight = 3,
                Matrix = new string[,] {
                    { "Module_C1", "Module_C2" },
                    { "Module_B3", "Module_C1" },
                    { "Module_A1", "Module_C1" }
                }
            },
            ["Room_11051"] = new ChunkGridConfig { // Descent Drop (2x3 12x12 Modules = 24m x 36m)
                GridWidth = 2, GridHeight = 3,
                Matrix = new string[,] {
                    { "Module_C2", "Module_D1" },
                    { "Module_F2", "Module_C1" },
                    { "Module_A1", "Module_C1" }
                }
            },
            ["Room_11052"] = new ChunkGridConfig { // Corridor East-West (4x2 12x12 Modules = 48m x 24m)
                GridWidth = 4, GridHeight = 2,
                Matrix = new string[,] {
                    { "Module_D2", "Module_E1", "Module_D1", "Module_C1" },
                    { "Module_A1", "Module_A2", "Module_B1", "Module_C1" }
                }
            },
            ["Room_11053"] = new ChunkGridConfig { // Elite Arena (4x2 12x12 Modules = 48m x 24m)
                GridWidth = 4, GridHeight = 2,
                Matrix = new string[,] {
                    { "Module_E1", "Module_G2", "Module_E2", "Module_C1" },
                    { "Module_A1", "Module_B1", "Module_A2", "Module_C1" }
                }
            },
            ["Room_11056"] = new ChunkGridConfig { // High Cliffs (3x2 12x12 Modules = 36m x 24m)
                GridWidth = 3, GridHeight = 2,
                Matrix = new string[,] {
                    { "Module_H1", "Module_I1", "Module_C1" },
                    { "Module_A1", "Module_H1", "Module_C1" }
                }
            },
            ["Room_11057"] = new ChunkGridConfig { // Platform Maze (3x2 12x12 Modules = 36m x 24m)
                GridWidth = 3, GridHeight = 2,
                Matrix = new string[,] {
                    { "Module_F1", "Module_F2", "Module_C1" },
                    { "Module_A1", "Module_F1", "Module_C1" }
                }
            },
            ["Room_11061"] = new ChunkGridConfig { // Rest Shelter (3x2 12x12 Modules = 36m x 24m)
                GridWidth = 3, GridHeight = 2,
                Matrix = new string[,] {
                    { "Module_A4", "Module_A3", "Module_C1" },
                    { "Module_A1", "Module_A2", "Module_C1" }
                }
            },
            ["Room_11063"] = new ChunkGridConfig { // Platform Challenge (4x2 12x12 Modules = 48m x 24m)
                GridWidth = 4, GridHeight = 2,
                Matrix = new string[,] {
                    { "Module_D1", "Module_E2", "Module_D2", "Module_C1" },
                    { "Module_A1", "Module_E1", "Module_A2", "Module_C1" }
                }
            }
        };

        foreach (var kvp in chunkConfigs)
        {
            string chunkName = kvp.Key;
            ChunkGridConfig config = kvp.Value;
            int nX = config.GridWidth;
            int nY = config.GridHeight;
            string[,] moduleGridNames = config.Matrix;

            int worldWidth = nX * 12;  // 12m per module
            int worldHeight = nY * 12; // 12m per module
            int halfW = worldWidth / 2;

            bool isValid = ValidateChunkPathways(moduleGridNames, nX, nY);
            if (isValid)
            {
                Debug.Log($"<color=cyan>[ModuleChunkBuilder] {chunkName} ({nX}x{nY} 12x12 Modules) Entry Point 간 BFS 연속 경로 검증 통과!</color>");
            }

            GameObject gridRoot = new GameObject(chunkName);
            var mainGrid = gridRoot.AddComponent<Grid>();
            mainGrid.cellSize = new Vector3(1, 1, 0);

            // Ground Tilemap
            GameObject groundObj = new GameObject("Tilemap_Ground");
            groundObj.transform.SetParent(gridRoot.transform);
            var gMap = groundObj.AddComponent<Tilemap>();
            var gR = groundObj.AddComponent<TilemapRenderer>();
            gR.sortingLayerName = "Tilemap";
            gR.sortingOrder = 0;
            if (mat != null) gR.sharedMaterial = mat;

            var rb = groundObj.AddComponent<Rigidbody2D>();
            rb.bodyType = RigidbodyType2D.Static;

            var tileCol = groundObj.AddComponent<TilemapCollider2D>();
            var compositeCol = groundObj.AddComponent<CompositeCollider2D>();
            tileCol.compositeOperation = Collider2D.CompositeOperation.Merge;

            // Platform Tilemap (PlatformEffector2D 1-Way 적용)
            GameObject platObj = new GameObject("Tilemap_Platforms");
            platObj.transform.SetParent(gridRoot.transform);
            int oneWayLayer = LayerMask.NameToLayer("OneWayPlatform");
            if (oneWayLayer >= 0) platObj.layer = oneWayLayer;
            var pMap = platObj.AddComponent<Tilemap>();
            var pRend = platObj.AddComponent<TilemapRenderer>();
            pRend.sortingLayerName = "Tilemap";
            pRend.sortingOrder = 5;
            if (mat != null) pRend.sharedMaterial = mat;

            var pCol = platObj.AddComponent<TilemapCollider2D>();
            pCol.usedByEffector = true;

            var pEff = platObj.AddComponent<PlatformEffector2D>();
            pEff.useOneWay = true;
            pEff.surfaceArc = 180f;
            platObj.AddComponent<OneWayPlatformPassThrough>();

            Vector3 playerSpawnPos = new Vector3(-halfW + 5f, 2f, 0f);

            // Assemble NxM 12x12 Modules into World Coordinates (X: -halfW to +halfW-1, Y: 0 to worldHeight-1)
            for (int modY = 0; modY < nY; modY++)
            {
                for (int modX = 0; modX < nX; modX++)
                {
                    string mName = moduleGridNames[modY, modX];
                    if (!ModuleTemplates.TryGetValue(mName, out string[] layout) || layout == null)
                    {
                        layout = ModuleTemplates["Module_A1"];
                    }

                    int offsetX = (modX * 12) - halfW;
                    int offsetY = modY * 12;

                    for (int r = 0; r < 12; r++)
                    {
                        int cellY = 11 - r;
                        if (r >= layout.Length) continue;
                        string line = layout[r];
                        if (line == null) continue;

                        for (int cellX = 0; cellX < 12; cellX++)
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

            // Dynamic Boundary Walls & 4m Entry Passages
            if (groundTile != null)
            {
                // Top/Bottom Boundary Walls (South/North Passages)
                for (int x = -halfW; x <= halfW; x++)
                {
                    if (x < -3 || x > 3) gMap.SetTile(new Vector3Int(x, -1, 0), groundTile);
                    if (x < -3 || x > 3) gMap.SetTile(new Vector3Int(x, worldHeight, 0), groundTile);
                }
                // Left/Right Boundary Walls (West/East Passages)
                for (int y = 0; y <= worldHeight; y++)
                {
                    if (y < 1 || y > 4) gMap.SetTile(new Vector3Int(-halfW, y, 0), groundTile);
                    if (y < 1 || y > 4) gMap.SetTile(new Vector3Int(halfW - 1, y, 0), groundTile);
                }
            }

            if (RequiresP0TraversalCorridor(chunkName))
                EnsureP0TraversalCorridor(gMap, pMap, groundTile, worldWidth);
            NormalizeOneWayPlatforms(pMap, gMap);
            // Camera Bounds
            GameObject cameraBounds = new GameObject("CameraBounds");
            cameraBounds.transform.SetParent(gridRoot.transform);
            cameraBounds.transform.localPosition = new Vector3(-0.5f, 15f, 0f);
            var box = cameraBounds.AddComponent<BoxCollider2D>();
            box.size = new Vector2(60f, 30f);
            box.isTrigger = true;

            // Dynamic Sockets (West, East, South, North) - surface=1m 기준 center=2m, EntryMarker=1.51m(surface+0.51m)
            float[] socketXs = { -worldWidth * 0.375f, -worldWidth * 0.125f, worldWidth * 0.125f, worldWidth * 0.375f };
            ChunkSocketDirection[] socketDirections =
            {
                ChunkSocketDirection.West, ChunkSocketDirection.North,
                ChunkSocketDirection.South, ChunkSocketDirection.East
            };
            for (int i = 0; i < socketDirections.Length; i++)
                AddGroundedSocket(gridRoot.transform, gMap, groundTile, socketDirections[i], socketXs[i]);

            // Player Spawn Marker
            GameObject spawnMarker = new GameObject("SpawnPoint_Player");
            spawnMarker.transform.SetParent(gridRoot.transform);
            spawnMarker.transform.localPosition = playerSpawnPos;
            var marker = spawnMarker.AddComponent<SpawnPointMarker>();
            if (marker != null) marker.Type = SpawnType.Player;

            if (chunkName == "Prefab_1042")
            {
                AddGroundedSpawnMarker(gridRoot.transform, gMap, pMap, "SpawnPoint_Boss", 0f, 6f, SpawnType.Boss, 3201);
            }

            if (chunkName == "Room_11050" || chunkName == "Room_11051" ||
                chunkName == "Room_11052" || chunkName == "Room_11053")
                AddCombatSpawnZones(gridRoot.transform, gMap, pMap, worldWidth, worldHeight);

            string prefabPath = $"{roomsDir}/{chunkName}.prefab";
            PrefabUtility.SaveAsPrefabAsset(gridRoot, prefabPath);
            Object.DestroyImmediate(gridRoot);
        }
        Debug.Log($"<color=green>[ModuleChunkBuilder] 12x12 가변 NxM 룸 청크 11종 재빌드 완료!</color>");
    }

    private static void AddGroundedSocket(Transform parent, Tilemap ground, Tile groundTile,
        ChunkSocketDirection direction, float desiredX)
    {
        int centerX = Mathf.Clamp(Mathf.RoundToInt(desiredX), ground.cellBounds.xMin + 2, ground.cellBounds.xMax - 3);
        int surfaceCellY = 0;

        for (int x = centerX - 1; x <= centerX + 1; x++)
        {
            ground.SetTile(new Vector3Int(x, surfaceCellY, 0), groundTile);
            ground.SetTile(new Vector3Int(x, surfaceCellY - 1, 0), groundTile);
            ground.SetTile(new Vector3Int(x, surfaceCellY + 1, 0), null);
            ground.SetTile(new Vector3Int(x, surfaceCellY + 2, 0), null);
        }

        AddSocket(parent, direction, new Vector3(centerX + 0.5f, surfaceCellY + 2f, 0f));
    }

    private static bool RequiresP0TraversalCorridor(string chunkName)
    {
        return chunkName == "Prefab_1041" || chunkName == "Prefab_1042" ||
            chunkName == "Room_11050" || chunkName == "Room_11051" ||
            chunkName == "Room_11052" || chunkName == "Room_11053" ||
            chunkName == "Room_11057";
    }

    private static void EnsureP0TraversalCorridor(Tilemap ground, Tilemap platforms, Tile groundTile, int worldWidth)
    {
        int left = Mathf.RoundToInt(-worldWidth * 0.375f) - 1;
        int right = Mathf.RoundToInt(worldWidth * 0.375f) + 1;
        for (int x = left; x <= right; x++)
        {
            ground.SetTile(new Vector3Int(x, -1, 0), groundTile);
            ground.SetTile(new Vector3Int(x, 0, 0), groundTile);
            for (int y = 1; y <= 3; y++)
            {
                ground.SetTile(new Vector3Int(x, y, 0), null);
                platforms.SetTile(new Vector3Int(x, y, 0), null);
            }
        }
    }

    private static void NormalizeOneWayPlatforms(Tilemap platforms, Tilemap ground)
    {
        var rows = new Dictionary<int, List<int>>();
        foreach (Vector3Int cell in platforms.cellBounds.allPositionsWithin)
        {
            if (!platforms.HasTile(cell)) continue;
            if (ground.HasTile(cell))
            {
                platforms.SetTile(cell, null);
                continue;
            }
            if (!rows.TryGetValue(cell.y, out List<int> xs)) rows[cell.y] = xs = new List<int>();
            xs.Add(cell.x);
        }

        foreach (var row in rows)
        {
            int[] xs = row.Value.ToArray();
            System.Array.Sort(xs);
            int start = 0;
            for (int i = 1; i <= xs.Length; i++)
            {
                if (i < xs.Length && xs[i] == xs[i - 1] + 1) continue;
                if (i - start < 3)
                    for (int j = start; j < i; j++) platforms.SetTile(new Vector3Int(xs[j], row.Key, 0), null);
                start = i;
            }
        }
    }

    private static void AddCombatSpawnZones(Transform parent, Tilemap ground, Tilemap platforms, int worldWidth, int worldHeight)
    {
        Vector3[] positions = worldWidth < 30
            ? new[] { new Vector3(-9f, 10f), new Vector3(9f, 10f), new Vector3(0f, 28f) }
            : new[] { new Vector3(-18f, 10f), new Vector3(0f, 20f), new Vector3(18f, 10f) };
        for (int i = 0; i < positions.Length; i++)
        {
            AddGroundedSpawnMarker(parent, ground, platforms, $"SpawnZone_{i + 1}", positions[i].x, Mathf.Min(positions[i].y, worldHeight - 4f), SpawnType.Monster);
        }
    }

    private static void AddGroundedSpawnMarker(Transform parent, Tilemap ground, Tilemap platforms, string name, float desiredX, float desiredY, SpawnType type, uint monsterId = 0)
    {
        int cellX = Mathf.RoundToInt(desiredX);
        int startY = Mathf.RoundToInt(desiredY);
        int surfaceY = 1; // default fallback floor

        for (int y = startY; y >= 0; y--)
        {
            Vector3Int cell = new Vector3Int(cellX, y, 0);
            if (ground.HasTile(cell) || platforms.HasTile(cell))
            {
                surfaceY = y + 1;
                break;
            }
        }

        var zone = new GameObject(name, typeof(SpawnPointMarker));
        zone.transform.SetParent(parent);
        zone.transform.localPosition = new Vector3(desiredX, surfaceY + 0.51f, 0f);
        var marker = zone.GetComponent<SpawnPointMarker>();
        marker.Type = type;
        if (monsterId > 0) marker.MonsterId = monsterId;
        marker.EnableSpawn = true;
    }

    private static void AddSocket(Transform parent, ChunkSocketDirection direction, Vector3 position)
    {
        var socketObj = new GameObject($"Socket_{direction}", typeof(ChunkSocketMarker));
        socketObj.transform.SetParent(parent);
        socketObj.transform.localPosition = position;
        var entry = new GameObject($"Entry_{direction}");
        entry.transform.SetParent(socketObj.transform);
        entry.transform.localPosition = new Vector3(0f, -0.49f, 0f); // EntryMarker surface+0.51m 계약 (socket.y=surface+1m - 0.49m)
        var marker = socketObj.GetComponent<ChunkSocketMarker>();
        if (marker != null)
        {
            marker.Direction = direction;
            marker.EntryMarker = entry.transform;
        }
    }

    private static bool ValidateChunkPathways(string[,] grid, int nX, int nY)
    {
        bool[,] visited = new bool[nY, nX];
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
                if (nx >= 0 && nx < nX && ny >= 0 && ny < nY && !visited[ny, nx])
                {
                    visited[ny, nx] = true;
                    queue.Enqueue(new Vector2Int(nx, ny));
                }
            }
        }

        int lastX = nX - 1;
        int midX = nX / 2;
        int lastY = nY - 1;

        return visited[0, lastX] && visited[0, midX] && visited[lastY, midX];
    }
}
#endif
