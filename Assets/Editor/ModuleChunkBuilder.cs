#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// 6x6 청크 모듈 Prefab 24종 자동 제작 및 10x5 주입 기반 Stage 1 룸 청크 11종 전면 재생성 빌더.
/// 6x6 그리드(Y=0~5, X=0~5) 레이아웃 파서:
/// '#' : 지면 타일 (Solid Ground)
/// '=' : 1-Way 발판 (One-Way Platform)
/// '^' : 바닥 가시 함정 (Up Spike)
/// 'v' : 천장 가시 함정 (Down Spike)
/// '<' : 좌측 벽 가시 함정 (Left Spike)
/// '>' : 우측 벽 가시 함정 (Right Spike)
/// 'O' : 둥근 톱날 함정 (Saw Blade Trap)
/// 'S' / 'E' : 진입 / 진출 Marker
/// '.' : 통과 가능 공간 (Open Air)
/// </summary>
public static class ModuleChunkBuilder
{
    private static readonly Dictionary<string, string[]> ModuleTemplates = new Dictionary<string, string[]>()
    {
        // Category A: 평지 & 장애물
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
        // Category C: 벽점프 & 샤프트
        ["Module_C1"] = new string[] {
            "#....#",
            "#>..<#",
            "#....#",
            "#>..<#",
            "#....#",
            "##S.##"
        },
        ["Module_C2"] = new string[] {
            "#....E",
            "#..O.#",
            "#=...",
            "#....#",
            "S....#",
            "######"
        },
        // Category D: 대시 & 저상
        ["Module_D1"] = new string[] {
            "######",
            "#vvvv#",
            "S....E",
            "######",
            "######",
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
        // Category F: 공중 부유 모듈 (Floating Air - Y=0 Open)
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
            "vvvvvv",
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
        // Category H: 높은 지형 & 절벽
        ["Module_H1"] = new string[] {
            "######",
            "######",
            "######",
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
            "##..##",
            "##==##",
            "##..##",
            "##..##",
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
            "######",
            "######",
            "S....E",
            "#^^^^#",
            "######"
        },
        ["Module_I3"] = new string[] {
            "######",
            "S.....",
            "####..",
            "......",
            ".....E",
            "######"
        },

        // Duplicate fallbacks to ensure full 24 keys
        ["Module_J1"] = new string[] { "......", "..==..", "S....E", "##..##", "##^^##", "######" },
        ["Module_J2"] = new string[] { "######", "#vvvv#", "S....E", "##..##", "......", "######" },
        ["Module_K1"] = new string[] { "......", ".####.", "S....E", "..==..", "..^^..", "######" },
        ["Module_K2"] = new string[] { "##..##", "##==##", "S....E", "##..##", "......", "######" },
        ["Module_L1"] = new string[] { "......", "..O...", "S.##.E", "..==..", "......", "######" },
        ["Module_L2"] = new string[] { "######", "S....E", "....##", "..==..", "......", "######" }
    };

    [MenuItem("TP2/Build 6x6 Modules & Stage 1 Chunks (6x6 모듈 & 룸 청크 전면 재생성)")]
    public static void BuildAllModulesAndChunks()
    {
        Debug.Log("<color=cyan><b>[ModuleChunkBuilder] 6x6 모듈 Prefab 24종 및 10x5 주입 청크 11종 빌드 시작...</b></color>");

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

        Material mat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/TilemapDefaultMaterial.mat");

        // 1. 24종 6x6 모듈 Prefab 제작
        Build24ModulePrefabs(modulesDir, groundTile, platTile, mat);

        // 2. 10x5 모듈 주입 기반 Stage 1 룸 청크 11종 전면 빌드
        Build11RoomChunkPrefabs(roomsDir, groundTile, platTile, mat);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log("<color=green><b>[ModuleChunkBuilder] 6x6 모듈 24종 및 10x5 주입 룸 청크 11종 정밀 빌드 완결!</b></color>");

        AddressablePipeline.BuildAndDeploy();
    }

    private static void Build24ModulePrefabs(string modulesDir, Tile groundTile, Tile platTile, Material mat)
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
            if (mat != null) gRend.sharedMaterial = mat;
            groundObj.AddComponent<TilemapCollider2D>();

            // Platform Tilemap
            GameObject platObj = new GameObject("Tilemap_Platforms");
            platObj.transform.SetParent(modRoot.transform);
            var pMap = platObj.AddComponent<Tilemap>();
            var pRend = platObj.AddComponent<TilemapRenderer>();
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
                            CreateSpikeTrap(modRoot.transform, new Vector3(x + 0.5f, y + 0.5f, 0f), 0f);
                            break;
                        case 'v':
                            CreateSpikeTrap(modRoot.transform, new Vector3(x + 0.5f, y + 0.5f, 0f), 180f);
                            break;
                        case '<':
                            CreateSpikeTrap(modRoot.transform, new Vector3(x + 0.5f, y + 0.5f, 0f), -90f);
                            break;
                        case '>':
                            CreateSpikeTrap(modRoot.transform, new Vector3(x + 0.5f, y + 0.5f, 0f), 90f);
                            break;
                        case 'O':
                            CreateSawBladeTrap(modRoot.transform, new Vector3(x + 0.5f, y + 0.5f, 0f));
                            break;
                    }
                }
            }

            string prefabPath = $"{modulesDir}/{modName}.prefab";
            PrefabUtility.SaveAsPrefabAsset(modRoot, prefabPath);
            Object.DestroyImmediate(modRoot);
        }
        Debug.Log($"<color=green>[ModuleChunkBuilder] 6x6 모듈 Prefab 24종 ASCII 정밀 배치 빌드 완결!</color>");
    }

    private static void CreateSpikeTrap(Transform parent, Vector3 pos, float angleDeg)
    {
        GameObject spike = new GameObject("SpikeTrap");
        spike.transform.SetParent(parent);
        spike.transform.localPosition = pos;
        spike.transform.localRotation = Quaternion.Euler(0f, 0f, angleDeg);
        var col = spike.AddComponent<BoxCollider2D>();
        col.isTrigger = true;
        col.size = new Vector2(0.8f, 0.8f);
        spike.AddComponent<SpikeTrap>();
    }

    private static void CreateSawBladeTrap(Transform parent, Vector3 pos)
    {
        GameObject saw = new GameObject("SawBladeTrap");
        saw.transform.SetParent(parent);
        saw.transform.localPosition = pos;
        var col = saw.AddComponent<CircleCollider2D>();
        col.isTrigger = true;
        col.radius = 0.5f;
        saw.AddComponent<SawBladeTrap>();
    }

    private static void Build11RoomChunkPrefabs(string roomsDir, Tile groundTile, Tile platTile, Material mat)
    {
        string[] chunkNames = new string[]
        {
            "Prefab_1040", "Prefab_1041", "Prefab_1042",
            "Room_11050", "Room_11051", "Room_11052", "Room_11053",
            "Room_11056", "Room_11057", "Room_11061", "Room_11063"
        };

        // 10x5 모듈 그리드 주입 조합
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
            if (mat != null) gR.sharedMaterial = mat;
            groundObj.AddComponent<TilemapCollider2D>();

            // Platform Tilemap
            GameObject platObj = new GameObject("Tilemap_Platforms");
            platObj.transform.SetParent(gridRoot.transform);
            var pMap = platObj.AddComponent<Tilemap>();
            var pR = platObj.AddComponent<TilemapRenderer>();
            if (mat != null) pR.sharedMaterial = mat;

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
                            char ch = cellX < line.Length ? line[x: cellX] : '.';
                            Vector3Int tilePos = new Vector3Int(offsetX + cellX, offsetY + cellY, 0);

                            switch (ch)
                            {
                                case '#':
                                    gMap.SetTile(tilePos, groundTile);
                                    break;
                                case '=':
                                    pMap.SetTile(tilePos, platTile);
                                    break;
                                case '^':
                                    CreateSpikeTrap(gridRoot.transform, new Vector3(offsetX + cellX + 0.5f, offsetY + cellY + 0.5f, 0f), 0f);
                                    break;
                                case 'v':
                                    CreateSpikeTrap(gridRoot.transform, new Vector3(offsetX + cellX + 0.5f, offsetY + cellY + 0.5f, 0f), 180f);
                                    break;
                                case '<':
                                    CreateSpikeTrap(gridRoot.transform, new Vector3(offsetX + cellX + 0.5f, offsetY + cellY + 0.5f, 0f), -90f);
                                    break;
                                case '>':
                                    CreateSpikeTrap(gridRoot.transform, new Vector3(offsetX + cellX + 0.5f, offsetY + cellY + 0.5f, 0f), 90f);
                                    break;
                                case 'O':
                                    CreateSawBladeTrap(gridRoot.transform, new Vector3(offsetX + cellX + 0.5f, offsetY + cellY + 0.5f, 0f));
                                    break;
                            }
                        }
                    }
                }
            }

            // Boundary Walls
            for (int x = -30; x <= 30; x++)
            {
                gMap.SetTile(new Vector3Int(x, -1, 0), groundTile);
                gMap.SetTile(new Vector3Int(x, 30, 0), groundTile);
            }
            for (int y = 0; y <= 30; y++)
            {
                gMap.SetTile(new Vector3Int(-30, y, 0), groundTile);
                gMap.SetTile(new Vector3Int(30, y, 0), groundTile);
            }

            // Player Spawn Marker & Portals
            GameObject spawnMarker = new GameObject("SpawnPoint_Player");
            spawnMarker.transform.SetParent(gridRoot.transform);
            spawnMarker.transform.localPosition = new Vector3(-25f, 2f, 0f);
            var marker = spawnMarker.AddComponent<SpawnPointMarker>();
            marker.Type = SpawnType.Player;

            string prefabPath = $"{roomsDir}/{chunkName}.prefab";
            PrefabUtility.SaveAsPrefabAsset(gridRoot, prefabPath);
            Object.DestroyImmediate(gridRoot);
        }
        Debug.Log($"<color=green>[ModuleChunkBuilder] Stage 1 룸 청크 11종 10x5 모듈 주입 재빌드 완료!</color>");
    }
}
#endif
