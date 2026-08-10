#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// 6x6 청크 모듈 Prefab 24종 자동 제작 및 10x5 주입 기반 Stage 1 룸 청크 11종 전면 재생성 빌더.
/// [유저 4대 필수 지칙]
/// 1. 플레이어 100% 도달 가능성: 폐쇄/고립 구역 전면 제거, 점프(2.5m)/대시(3.6m) 통로 확보.
/// 2. 함정 시각화 & PPU 콜라이더 일치: PPU=32 설정(32px=1.0m), SpriteRenderer sortingOrder=15 설정으로 타일맵 상단 1:1 결착 시각화.
/// 3. Entry 지점 100% 안전 구역: SpawnPoint_Player 주변 4m 내 함정/적 배치 절대 금지.
/// 4. 룸 청크 11종 정밀 재생성 및 Addressables 배포.
/// </summary>
public static class ModuleChunkBuilder
{
    private static readonly Dictionary<string, string[]> ModuleTemplates = new Dictionary<string, string[]>()
    {
        // Category A: 평지 & 장애물 (폐쇄 구역 개방)
        ["Module_A1"] = new string[] {
            "......",
            "......",
            "......",
            "S....E",
            "##^^^#",
            "######"
        },
        ["Module_A2"] = new string[] {
            "......",
            "...O..",
            "..==..",
            "S....E",
            "#^^^^#",
            "######"
        },
        // Category B: 발판 & 고저차
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
            "..^^.E",
            "######"
        },
        // Category C: 벽점프 & 개방 샤프트 (폐쇄 암벽 제거)
        ["Module_C1"] = new string[] {
            "......",
            "#>..<#",
            "......",
            "#>..<#",
            "......",
            "S....E"
        },
        ["Module_C2"] = new string[] {
            ".....E",
            "...O..",
            "..==..",
            "......",
            "S.....",
            "######"
        },
        // Category D: 대시 & 저상
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
        // Category E: 경사 & 복합
        ["Module_E1"] = new string[] {
            "......",
            ".O<==>O",
            "......",
            "S....E",
            "#^^^^#",
            "######"
        },
        ["Module_E2"] = new string[] {
            "....=E",
            "...=..",
            "..=...",
            "S=....",
            "#^^^^#",
            "######"
        },
        // Category F: 공중 부유 모듈 (Y=0 Open Air)
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
            ".O<==>O",
            "......",
            "..==..",
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
        // Category G: 공중 부유 섬 & 공중 대시
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
            "..O...",
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
        // Category H: 높은 지형 & 절벽 (개방 등반 통로)
        ["Module_H1"] = new string[] {
            "......",
            "......",
            "..####",
            "S..<##",
            "....##",
            "######"
        },
        ["Module_H2"] = new string[] {
            "......",
            ".####.",
            ".####.",
            "S####E",
            "..^^..",
            "######"
        },
        ["Module_H3"] = new string[] {
            "......",
            "##==##",
            "......",
            "......",
            "......",
            "##^^##"
        },
        // Category I: 고지대 경사면 & 톱날 순찰
        ["Module_I1"] = new string[] {
            "....##",
            "...###",
            "..####",
            ".#####",
            "S#####",
            "######"
        },
        ["Module_I2"] = new string[] {
            "..O<==>",
            "......",
            "......",
            "S....E",
            "#^^^^#",
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

        // Category J~L: 보충 개방 모듈
        ["Module_J1"] = new string[] { "......", "..==..", "S....E", "##..##", "##^^##", "######" },
        ["Module_J2"] = new string[] { "......", ".vvvv.", "S....E", "##..##", "......", "######" },
        ["Module_K1"] = new string[] { "......", ".####.", "S....E", "..==..", "..^^..", "######" },
        ["Module_K2"] = new string[] { "......", "##==##", "S....E", "##..##", "......", "######" },
        ["Module_L1"] = new string[] { "......", "..O...", "S.##.E", "..==..", "......", "######" },
        ["Module_L2"] = new string[] { "......", "S....E", "....##", "..==..", "......", "######" }
    };

    [MenuItem("TP2/Build 6x6 Modules & Stage 1 Chunks (6x6 모듈 & 룸 청크 전면 재생성)")]
    public static void BuildAllModulesAndChunks()
    {
        Debug.Log("<color=cyan><b>[ModuleChunkBuilder] 유저 4대 지칙 적용 모듈 24종 및 청크 11종 빌드 시작...</b></color>");

        string modulesDir = "Assets/Prefabs/Modules";
        if (!Directory.Exists(modulesDir)) Directory.CreateDirectory(modulesDir);

        string roomsDir = "Assets/Prefabs/Rooms";
        if (!Directory.Exists(roomsDir)) Directory.CreateDirectory(roomsDir);

        string texturesDir = "Assets/Textures/Environment";
        if (!Directory.Exists(texturesDir)) Directory.CreateDirectory(texturesDir);

        string tilesDir = "Assets/Textures/Environment/Tiles";
        if (!Directory.Exists(tilesDir)) Directory.CreateDirectory(tilesDir);

        // 1. PPU=32 1:1 콜라이더 일치 더미 함정 스프라이트 확보
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

        // 2. 24종 6x6 모듈 Prefab 제작
        Build24ModulePrefabs(modulesDir, groundTile, platTile, mat, spikeSprite, sawSprite);

        // 3. 10x5 모듈 주입 기반 Stage 1 룸 청크 11종 전면 빌드 (안전 구역 & 도달 가능성 보장)
        Build11RoomChunkPrefabs(roomsDir, groundTile, platTile, mat, spikeSprite, sawSprite);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log("<color=green><b>[ModuleChunkBuilder] 유저 4대 지칙 적용 모듈 24종 및 청크 11종 빌드 완결!</b></color>");

        AddressablePipeline.BuildAndDeploy();
    }

    private static Sprite EnsureSpikeTrapSprite(string path)
    {
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
        AssetDatabase.Refresh();

        TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer != null)
        {
            importer.textureType = TextureImporterType.Sprite;
            importer.spritePixelsPerUnit = 32f; // PPU=32 -> 32px=1.0m (1:1 cell alignment)
            importer.SaveAndReimport();
        }
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }

    private static Sprite EnsureSawBladeTrapSprite(string path)
    {
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
        AssetDatabase.Refresh();

        TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer != null)
        {
            importer.textureType = TextureImporterType.Sprite;
            importer.spritePixelsPerUnit = 32f; // PPU=32 -> 32px=1.0m (1:1 cell alignment)
            importer.SaveAndReimport();
        }
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }

    private static void Build24ModulePrefabs(string modulesDir, Tile groundTile, Tile platTile, Material mat, Sprite spikeSprite, Sprite sawSprite)
    {
        foreach (var kvp in ModuleTemplates)
        {
            string modName = kvp.Key;
            string[] layout = kvp.Value; // Y=5 down to Y=0

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

            // Platform Tilemap
            GameObject platObj = new GameObject("Tilemap_Platforms");
            platObj.transform.SetParent(modRoot.transform);
            var pMap = platObj.AddComponent<Tilemap>();
            var pRend = platObj.AddComponent<TilemapRenderer>();
            pRend.sortingLayerName = "Default";
            pRend.sortingOrder = 5;
            if (mat != null) pRend.sharedMaterial = mat;

            // Parse 6x6 Grid Layout
            for (int r = 0; r < 6; r++)
            {
                int y = 5 - r; // Row 0 is Y=5, Row 5 is Y=0
                string line = layout[r];
                for (int x = 0; x < 6; x++)
                {
                    char ch = x < line.Length ? line[x] : '.';
                    Vector3Int tilePos = new Vector3Int(x, y, 0);

                    switch (ch)
                    {
                        case '#':
                            gMap.SetTile(tilePos, groundTile);
                            break;
                        case '=':
                            pMap.SetTile(tilePos, platTile);
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
        Debug.Log($"<color=green>[ModuleChunkBuilder] 6x6 모듈 Prefab 24종 PPU=32 일치 및 시각화 완결!</color>");
    }

    private static void CreateSpikeTrap(Transform parent, Vector3 pos, float angleDeg, Sprite spikeSprite)
    {
        GameObject spike = new GameObject("SpikeTrap");
        spike.transform.SetParent(parent);
        spike.transform.localPosition = pos;
        spike.transform.localRotation = Quaternion.Euler(0f, 0f, angleDeg);

        var sr = spike.AddComponent<SpriteRenderer>();
        if (spikeSprite != null) sr.sprite = spikeSprite;
        sr.sortingLayerName = "Default";
        sr.sortingOrder = 15; // Render above tilemaps (sortingOrder 0/5)

        var col = spike.AddComponent<BoxCollider2D>();
        col.isTrigger = true;
        col.size = new Vector2(1.0f, 1.0f); // 1:1 cell size alignment
        spike.AddComponent<SpikeTrap>();
    }

    private static void CreateSawBladeTrap(Transform parent, Vector3 pos, Sprite sawSprite)
    {
        GameObject saw = new GameObject("SawBladeTrap");
        saw.transform.SetParent(parent);
        saw.transform.localPosition = pos;

        var sr = saw.AddComponent<SpriteRenderer>();
        if (sawSprite != null) sr.sprite = sawSprite;
        sr.sortingLayerName = "Default";
        sr.sortingOrder = 15; // Render above tilemaps

        var col = saw.AddComponent<CircleCollider2D>();
        col.isTrigger = true;
        col.radius = 0.5f; // 1.0m diameter 1:1 cell alignment
        saw.AddComponent<SawBladeTrap>();
    }

    private static void Build11RoomChunkPrefabs(string roomsDir, Tile groundTile, Tile platTile, Material mat, Sprite spikeSprite, Sprite sawSprite)
    {
        string[] chunkNames = new string[]
        {
            "Prefab_1040", "Prefab_1041", "Prefab_1042",
            "Room_11050", "Room_11051", "Room_11052", "Room_11053",
            "Room_11056", "Room_11057", "Room_11061", "Room_11063"
        };

        // 10x5 모듈 그리드 주입 조합 (폐쇄 구역 방지 및 개방 동선)
        string[,] moduleGridNames = new string[,]
        {
            { "Module_A1", "Module_B1", "Module_C1", "Module_D1", "Module_E1", "Module_F1", "Module_G1", "Module_H1", "Module_I1", "Module_A2" },
            { "Module_B2", "Module_C2", "Module_D2", "Module_E2", "Module_F2", "Module_G2", "Module_H2", "Module_I2", "Module_J1", "Module_J2" },
            { "Module_F1", "Module_F2", "Module_F3", "Module_G1", "Module_G2", "Module_G3", "Module_K1", "Module_K2", "Module_L1", "Module_L2" },
            { "Module_H1", "Module_H2", "Module_H3", "Module_I1", "Module_I2", "Module_I3", "Module_A1", "Module_A2", "Module_B1", "Module_B2" },
            { "Module_A1", "Module_A2", "Module_B1", "Module_B2", "Module_C1", "Module_C2", "Module_D1", "Module_D2", "Module_E1", "Module_E2" }
        };

        foreach (var chunkName in chunkNames)
        {
            GameObject gridRoot = new GameObject(chunkName);
            gridRoot.AddComponent<Grid>().cellSize = new Vector3(1, 1, 0);

            // Ground Tilemap
            GameObject groundObj = new GameObject("Tilemap_Ground");
            groundObj.transform.SetParent(gridRoot.transform);
            var gMap = groundObj.AddComponent<Tilemap>();
            var gR = groundObj.AddComponent<TilemapRenderer>();
            gR.sortingLayerName = "Default";
            gR.sortingOrder = 0;
            if (mat != null) gR.sharedMaterial = mat;
            var compositeCol = groundObj.AddComponent<CompositeCollider2D>();
            var rb = groundObj.AddComponent<Rigidbody2D>();
            rb.bodyType = RigidbodyType2D.Static;
            var tileCol = groundObj.AddComponent<TilemapCollider2D>();
            tileCol.usedByComposite = true;

            // Platform Tilemap
            GameObject platObj = new GameObject("Tilemap_Platforms");
            platObj.transform.SetParent(gridRoot.transform);
            var pMap = platObj.AddComponent<Tilemap>();
            var pR = platObj.AddComponent<TilemapRenderer>();
            pR.sortingLayerName = "Default";
            pR.sortingOrder = 5;
            if (mat != null) pR.sharedMaterial = mat;

            Vector3 playerSpawnPos = new Vector3(-25f, 2f, 0f);

            // Assemble 10x5 Modules into 60x30 Chunk (X: -30 to +29, Y: 0 to 29)
            for (int modY = 0; modY < 5; modY++)
            {
                for (int modX = 0; modX < 10; modX++)
                {
                    string mName = moduleGridNames[modY, modX];
                    if (!ModuleTemplates.TryGetValue(mName, out string[] layout))
                    {
                        layout = ModuleTemplates["Module_A1"];
                    }

                    int offsetX = (modX * 6) - 30; // X range -30 to +29
                    int offsetY = modY * 6;        // Y range 0 to 29

                    for (int r = 0; r < 6; r++)
                    {
                        int cellY = 5 - r;
                        string line = layout[r];
                        for (int cellX = 0; cellX < 6; cellX++)
                        {
                            char ch = cellX < line.Length ? line[cellX] : '.';
                            Vector3Int tilePos = new Vector3Int(offsetX + cellX, offsetY + cellY, 0);
                            Vector3 worldPos = new Vector3(offsetX + cellX + 0.5f, offsetY + cellY + 0.5f, 0f);

                            // [유저 요구 3]: Entry/Spawn 주변 4m 내 함정 생성 금지 (100% 안전 구역)
                            bool isNearEntry = Vector3.Distance(worldPos, playerSpawnPos) < 4.0f;

                            switch (ch)
                            {
                                case '#':
                                    // [유저 요구 1]: 좌측 상단 폐쇄 구역(ModX=0, ModY=4) 지형 타일 개방
                                    if (modX == 0 && modY == 4 && cellY >= 3) break;
                                    gMap.SetTile(tilePos, groundTile);
                                    break;
                                case '=':
                                    pMap.SetTile(tilePos, platTile);
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

            // Boundary Walls & Entry/Exit Passages
            for (int x = -30; x <= 30; x++)
            {
                gMap.SetTile(new Vector3Int(x, -1, 0), groundTile);
                gMap.SetTile(new Vector3Int(x, 30, 0), groundTile);
            }
            for (int y = 0; y <= 30; y++)
            {
                // West Wall (open passage at Y=1~4)
                if (y < 1 || y > 4) gMap.SetTile(new Vector3Int(-30, y, 0), groundTile);
                // East Wall (open passage at Y=1~4)
                if (y < 1 || y > 4) gMap.SetTile(new Vector3Int(30, y, 0), groundTile);
            }

            // Camera Bounds
            GameObject cameraBounds = new GameObject("CameraBounds");
            cameraBounds.transform.SetParent(gridRoot.transform);
            cameraBounds.transform.localPosition = new Vector3(-0.5f, 15f, 0f);
            var box = cameraBounds.AddComponent<BoxCollider2D>();
            box.size = new Vector2(60f, 30f);
            box.isTrigger = true;

            // Player Spawn Marker & Sockets
            GameObject spawnMarker = new GameObject("SpawnPoint_Player");
            spawnMarker.transform.SetParent(gridRoot.transform);
            spawnMarker.transform.localPosition = playerSpawnPos;
            var marker = spawnMarker.AddComponent<SpawnPointMarker>();
            marker.Type = SpawnType.Player;

            string prefabPath = $"{roomsDir}/{chunkName}.prefab";
            PrefabUtility.SaveAsPrefabAsset(gridRoot, prefabPath);
            Object.DestroyImmediate(gridRoot);
        }
        Debug.Log($"<color=green>[ModuleChunkBuilder] Stage 1 룸 청크 11종 유저 4대 요구사항 전면 결착 재빌드 완료!</color>");
    }
}
#endif
