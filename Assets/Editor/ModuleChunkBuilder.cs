#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
    public static int LastNormalizeCount { get; private set; }
    public static int LastRejectCount { get; private set; }

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
            "...........E",
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
            "....####....",
            "....####....",
            "S..........E",
            "............",
            "............",
            "............",
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
            "........===E",
            "............",
            "......===...",
            "............",
            "....===.....",
            "............",
            "S===........",
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
            "............",
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
            "............",
            "............",
            "............",
            "############"
        },

        // === Authoritative Stage 1 expansion patterns ===
        ["Module_J1_Connector"] = new string[] {
            ".....===....",
            "............",
            "..===.......",
            "............",
            ".......===..",
            "............",
            "....===.....",
            "............",
            "S..........E",
            "............",
            "############",
            "############"
        },
        ["Module_K1_ReturnShaft"] = new string[] {
            ".......===..",
            "............",
            "...===......",
            "............",
            ".......===..",
            "............",
            "...===......",
            "............",
            "S..........E",
            "............",
            "############",
            "############"
        },
        ["Module_L1_CombatPocket"] = new string[] {
            "............",
            "............",
            "............",
            "............",
            "............",
            "............",
            "....===.....",
            "............",
            "S..........E",
            "............",
            "############",
            "############"
        },
        ["Module_M1_LandmarkConnector"] = new string[] {
            "............", "....===.....", "............", "............",
            "........===.", "............", ".===........", "............",
            "S..........E", "............", "############", "############"
        },
        ["Module_M2_LandmarkConnector"] = new string[] {
            "............", "........===.", "............", "...====.....",
            "............", "............", ".===........", "............",
            "S..........E", "............", "############", "############"
        },
        ["Module_N1_VerticalReturnLoop"] = new string[] {
            "...===......", "............", "........===.", "............",
            "....===.....", "............", ".===........", "............",
            "S..........E", "............", "############", "############"
        },
        ["Module_N2_VerticalReturnLoop"] = new string[] {
            ".......===..", "............", "..===.......", "............",
            "......===...", "............", ".........===", "............",
            "S..........E", "............", "############", "############"
        },
        ["Module_O1_SplitLevelCombatPocket"] = new string[] {
            "............", "............", "............", ".===.....===",
            "............", "............", "....====....", "............",
            "S..........E", "............", "############", "############"
        },
        ["Module_O2_SplitLevelCombatPocket"] = new string[] {
            "............", "............", "....====....", "............",
            "............", "===......===", "............", "............",
            "S..........E", "............", "############", "############"
        }
    };

    static ModuleChunkBuilder()
    {
        AddPromotedLegacyTemplates();
    }

    private static void AddPromotedLegacyTemplates()
    {
        string[] names = {
            "Module_C3", "Module_C4", "Module_D3", "Module_D4", "Module_E3", "Module_E4",
            "Module_F3", "Module_F4", "Module_G4", "Module_H2", "Module_H3", "Module_H4",
            "Module_I2", "Module_I3", "Module_I4", "Module_J1", "Module_J2", "Module_J3",
            "Module_J4", "Module_K1", "Module_K2", "Module_L1", "Module_L2"
        };

        for (int i = 0; i < names.Length; i++)
        {
            char[][] rows = new char[12][];
            for (int row = 0; row < rows.Length; row++) rows[row] = "............".ToCharArray();

            rows[8] = "S..........E".ToCharArray();
            rows[10] = "############".ToCharArray();
            rows[11] = "############".ToCharArray();
            SetPlatformRun(rows[7], i % 8, 3 + i / 8);

            int role = i % 3;
            int variant = i / 3;
            if (role == 0) // Connector: low and mid-height choices over the full-width ground route.
            {
                SetPlatformRun(rows[6], 1 + variant % 4, 3 + variant % 2);
                SetPlatformRun(rows[3], 7 - variant % 3, 3);
            }
            else if (role == 1) // Return Shaft: three reachable alternating landings.
            {
                SetPlatformRun(rows[5], 5 + variant % 2, 3);
                SetPlatformRun(rows[3], 8 - variant % 3, 3);
            }
            else // Combat Pocket: clear center with elevated edge cover.
            {
                SetPlatformRun(rows[6], variant % 2 == 0 ? 1 : 8, 3);
                SetPlatformRun(rows[4], 4 + variant % 3, 3 + variant % 2);
            }

            ModuleTemplates.Add(names[i], System.Array.ConvertAll(rows, row => new string(row)));
        }
    }

    private static void SetPlatformRun(char[] row, int startX, int length)
    {
        for (int x = startX; x < startX + length && x < row.Length; x++) row[x] = '=';
    }

    private class ChunkGridConfig
    {
        public int GridWidth { get; set; }  // ModX 갯수 (N)
        public int GridHeight { get; set; } // ModY 갯수 (M)
        public string[,] Matrix { get; set; }
    }

    private enum ModuleRole { Connector, ReturnShaft, CombatPocket }

    private static bool IsValidModuleTemplate(string[] template)
    {
        if (template == null || template.Length != 12) return false;
        for (int y = 0; y < template.Length; y++)
        {
            if (template[y] == null || template[y].Length < 12) return false;
            for (int x = 0; x < 12; x++)
                if (".#=SE^v<>O".IndexOf(template[y][x]) < 0) return false;
        }
        return true;
    }

    private static ModuleRole GetModuleRole(string[] template)
    {
        foreach (var pair in ModuleTemplates)
        {
            if (!ReferenceEquals(pair.Value, template)) continue;
            if (pair.Key.Contains("LandmarkConnector")) return ModuleRole.Connector;
            if (pair.Key.Contains("VerticalReturnLoop")) return ModuleRole.ReturnShaft;
            if (pair.Key.Contains("SplitLevelCombatPocket")) return ModuleRole.CombatPocket;
            break;
        }
        uint hash = 2166136261u;
        foreach (string row in template)
            foreach (char cell in row)
                hash = (hash ^ cell) * 16777619u;
        return (ModuleRole)(hash % 3u);
    }

    private static string[] SelectModuleTemplate(string[] requested, uint seed, int cellOrdinal,
        IList<string[]> candidates = null)
    {
        if (requested == null) return ModuleTemplates["Module_A1"];
        ModuleRole role = GetModuleRole(requested);
        var pool = new List<string[]>();
        foreach (string[] candidate in candidates ?? new List<string[]>(ModuleTemplates.Values))
            if (candidate != null && GetModuleRole(candidate) == role) pool.Add(candidate);

        if (pool.Count == 0) return requested;
        int start = (int)((seed + (uint)cellOrdinal) % (uint)pool.Count);
        for (int offset = 0; offset < pool.Count; offset++)
        {
            string[] candidate = pool[(start + offset) % pool.Count];
            if (IsValidModuleTemplate(candidate)) return candidate;
        }
        return requested;
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

    [MenuItem("TP2/Rebuild Stage 1 Room Chunks")]
    public static void RebuildStage1RoomChunks() => RebuildStage1RoomChunks(0u);

    public static void RebuildStage1RoomChunks(uint selectionSeed)
    {
        const string tilesDir = "Assets/Textures/Environment/Tiles";
        BuildVariableRoomChunkPrefabs(
            "Assets/Prefabs/Rooms",
            AssetDatabase.LoadAssetAtPath<Tile>($"{tilesDir}/Tile_Ground.asset"),
            AssetDatabase.LoadAssetAtPath<Tile>($"{tilesDir}/Tile_Platform.asset"),
            AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/TilemapDefaultMaterial.mat"),
            AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Textures/Environment/Sprite_SpikeTrap.png"),
            AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Textures/Environment/Sprite_SawBladeTrap.png"),
            selectionSeed: selectionSeed);
        AssetDatabase.SaveAssets();
    }

    public static void RebuildStage1ModulesAndRooms()
    {
        const string tilesDir = "Assets/Textures/Environment/Tiles";
        Build12x12ModulePrefabs(
            "Assets/Prefabs/Modules",
            AssetDatabase.LoadAssetAtPath<Tile>($"{tilesDir}/Tile_Ground.asset"),
            AssetDatabase.LoadAssetAtPath<Tile>($"{tilesDir}/Tile_Platform.asset"),
            AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/TilemapDefaultMaterial.mat"),
            AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Textures/Environment/Sprite_SpikeTrap.png"),
            AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Textures/Environment/Sprite_SawBladeTrap.png"));
        RebuildStage1RoomChunks();
    }

    internal const string Candidate1uSourcePath =
        "Assets/Screenshots/StageChunkV10BlobGraph/StageChunkV10BlobGraph_XLarge_Comparison_MinModuleBlob.png";
    internal const string Candidate1uPrefabPath =
        "Assets/Prefabs/Development/Tilemap_Room_Candidate1u_ImageReconstructed.prefab";
    internal const string Candidate1uCombatReservedPrefabPath =
        "Assets/Prefabs/Development/Tilemap_Room_Candidate1u_CombatReserved.prefab";
    internal const string GoldenTrialPrefabPath =
        "Assets/Prefabs/Development/Tilemap_Room_GoldenDerived_Trial01.prefab";
    internal const string EmptyFirstTrial02PrefabPath =
        "Assets/Prefabs/Development/Tilemap_Room_EmptyFirst_Trial02.prefab";
    internal const string EmptyFirstAngularTrial03PrefabPath =
        "Assets/Prefabs/Development/Tilemap_Room_EmptyFirstAngular_Trial03.prefab";
    internal const string EmptyFirstAngularTrial04PrefabPath =
        "Assets/Prefabs/Development/Tilemap_Room_EmptyFirstAngular_Trial04.prefab";

    [MenuItem("TP2/Development/Rebuild Candidate 1u From Image")]
    public static void RebuildCandidate1uFromImage()
    {
        const int width = 84;
        const int height = 60;
        const int roiX = 60;
        const int roiY = 60;
        const int pixelsPerCell = 10;
        byte[] png = File.ReadAllBytes(Candidate1uSourcePath);
        var source = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        if (!source.LoadImage(png) || source.width < 900 || source.height < 660)
        {
            Object.DestroyImmediate(source);
            throw new InvalidDataException("Candidate 1u source must contain the 84x60 ROI at (60,60)-(899,659).");
        }

        var solid = new bool[width, height];
        int extractedSolidCount = 0;
        for (int y = 0; y < height; y++)
        for (int x = 0; x < width; x++)
        {
            Color32 color = source.GetPixel(roiX + x * pixelsPerCell + 5,
                source.height - 1 - (roiY + y * pixelsPerCell + 5));
            solid[x, height - 1 - y] = color.r == 94 && color.g == 123 && color.b == 105;
            if (solid[x, height - 1 - y]) extractedSolidCount++;
        }
        Object.DestroyImmediate(source);
        int silhouetteDiff = CountCandidate1uSilhouetteDiff(png, solid, width, height, roiX, roiY, pixelsPerCell);
        if (silhouetteDiff != 0) throw new InvalidDataException($"Candidate 1u silhouette diff={silhouetteDiff}.");

        NormalizeCandidate1u(solid, width, height);
        Directory.CreateDirectory("Assets/Prefabs/Development");
        Tile groundTile = AssetDatabase.LoadAssetAtPath<Tile>("Assets/Textures/Environment/Tiles/Tile_Ground.asset");
        Tile platformTile = AssetDatabase.LoadAssetAtPath<Tile>("Assets/Textures/Environment/Tiles/Tile_Platform.asset");
        if (groundTile == null || platformTile == null)
            throw new InvalidDataException("Candidate 1u requires the existing Ground and OneWay Tile assets.");

        GameObject root = new GameObject("Tilemap_Room_Candidate1u_ImageReconstructed", typeof(Grid));
        try
        {
            Tilemap ground = CreateCandidate1uTilemap(root.transform, "Tilemap_Ground", groundTile, false);
            Tilemap platforms = CreateCandidate1uTilemap(root.transform, "Tilemap_Platforms", platformTile, true);
            for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
                if (solid[x, y]) ground.SetTile(new Vector3Int(x, y, 0), groundTile);

            for (int x = 3; x <= 4; x++)
            for (int y = 15; y <= 18; y++)
                ground.SetTile(new Vector3Int(x, y, 0), null);
            for (int x = 81; x <= 83; x++)
            for (int y = 25; y <= 27; y++)
                ground.SetTile(new Vector3Int(x, y, 0), null);

            for (int x = 44; x <= 46; x++)
            for (int y = 2; y <= 3; y++) ground.SetTile(new Vector3Int(x, y, 0), groundTile);
            for (int x = 44; x <= 46; x++)
            for (int y = 4; y <= 7; y++) ground.SetTile(new Vector3Int(x, y, 0), null);
            for (int x = 47; x <= 49; x++)
            for (int y = 2; y <= 5; y++) ground.SetTile(new Vector3Int(x, y, 0), groundTile);
            for (int x = 47; x <= 49; x++)
            for (int y = 6; y <= 9; y++) ground.SetTile(new Vector3Int(x, y, 0), null);
            for (int x = 50; x <= 52; x++)
            for (int y = 2; y <= 7; y++) ground.SetTile(new Vector3Int(x, y, 0), groundTile);
            for (int x = 50; x <= 52; x++)
            for (int y = 8; y <= 11; y++) ground.SetTile(new Vector3Int(x, y, 0), null);
            for (int x = 53; x <= 55; x++)
            for (int y = 2; y <= 9; y++) ground.SetTile(new Vector3Int(x, y, 0), groundTile);
            for (int x = 53; x <= 55; x++)
            for (int y = 10; y <= 13; y++) ground.SetTile(new Vector3Int(x, y, 0), null);
            for (int x = 56; x <= 58; x++)
            for (int y = 2; y <= 11; y++) ground.SetTile(new Vector3Int(x, y, 0), groundTile);
            for (int x = 56; x <= 58; x++)
            for (int y = 12; y <= 15; y++) ground.SetTile(new Vector3Int(x, y, 0), null);
            for (int x = 59; x <= 61; x++)
            for (int y = 2; y <= 13; y++) ground.SetTile(new Vector3Int(x, y, 0), groundTile);
            for (int x = 59; x <= 61; x++)
            for (int y = 14; y <= 17; y++) ground.SetTile(new Vector3Int(x, y, 0), null);
            for (int x = 62; x <= 64; x++)
            for (int y = 2; y <= 15; y++) ground.SetTile(new Vector3Int(x, y, 0), groundTile);
            for (int x = 62; x <= 64; x++)
            for (int y = 16; y <= 19; y++) ground.SetTile(new Vector3Int(x, y, 0), null);
            for (int x = 65; x <= 67; x++)
            for (int y = 2; y <= 17; y++) ground.SetTile(new Vector3Int(x, y, 0), groundTile);
            for (int x = 65; x <= 67; x++)
            for (int y = 18; y <= 21; y++) ground.SetTile(new Vector3Int(x, y, 0), null);
            for (int x = 68; x <= 70; x++)
            for (int y = 2; y <= 19; y++) ground.SetTile(new Vector3Int(x, y, 0), groundTile);
            for (int x = 68; x <= 70; x++)
            for (int y = 20; y <= 23; y++) ground.SetTile(new Vector3Int(x, y, 0), null);

            for (int x = 8; x <= 10; x++) ground.SetTile(new Vector3Int(x, 38, 0), groundTile);
            for (int x = 8; x <= 10; x++)
            for (int y = 39; y <= 42; y++) ground.SetTile(new Vector3Int(x, y, 0), null);
            for (int x = 8; x <= 10; x++) ground.SetTile(new Vector3Int(x, 32, 0), groundTile);
            for (int x = 8; x <= 10; x++)
            for (int y = 33; y <= 36; y++) ground.SetTile(new Vector3Int(x, y, 0), null);
            for (int x = 8; x <= 10; x++) ground.SetTile(new Vector3Int(x, 26, 0), groundTile);
            for (int x = 8; x <= 10; x++)
            for (int y = 27; y <= 30; y++) ground.SetTile(new Vector3Int(x, y, 0), null);
            for (int x = 5; x <= 10; x++) ground.SetTile(new Vector3Int(x, 20, 0), groundTile);
            for (int x = 5; x <= 10; x++)
            for (int y = 21; y <= 24; y++) ground.SetTile(new Vector3Int(x, y, 0), null);

            for (int x = 47; x <= 49; x++)
            for (int y = 47; y <= 50; y++) ground.SetTile(new Vector3Int(x, y, 0), null);
            for (int x = 71; x <= 82; x++)
            for (int y = 2; y <= 18; y++) ground.SetTile(new Vector3Int(x, y, 0), groundTile);
            for (int x = 58; x <= 79; x++)
            for (int y = 53; y <= 56; y++) ground.SetTile(new Vector3Int(x, y, 0), groundTile);
            ground.SetTile(new Vector3Int(57, 53, 0), groundTile);
            ground.SetTile(new Vector3Int(57, 54, 0), groundTile);

            for (int x = 61; x <= 63; x++)
            for (int y = 46; y <= 49; y++) ground.SetTile(new Vector3Int(x, y, 0), null);
            for (int x = 65; x <= 68; x++)
            for (int y = 46; y <= 48; y++) ground.SetTile(new Vector3Int(x, y, 0), null);
            for (int x = 45; x <= 47; x++)
            for (int y = 37; y <= 39; y++) ground.SetTile(new Vector3Int(x, y, 0), null);
            for (int x = 40; x <= 42; x++)
            for (int y = 26; y <= 28; y++) ground.SetTile(new Vector3Int(x, y, 0), null);
            for (int x = 34; x <= 37; x++)
            for (int y = 23; y <= 27; y++) ground.SetTile(new Vector3Int(x, y, 0), null);
            for (int x = 44; x <= 47; x++)
            for (int y = 18; y <= 19; y++) ground.SetTile(new Vector3Int(x, y, 0), null);
            for (int x = 59; x <= 62; x++) ground.SetTile(new Vector3Int(x, 22, 0), null);
            for (int x = 64; x <= 66; x++)
            for (int y = 22; y <= 24; y++) ground.SetTile(new Vector3Int(x, y, 0), null);
            for (int x = 41; x <= 45; x++)
            for (int y = 8; y <= 14; y++) ground.SetTile(new Vector3Int(x, y, 0), null);
            ground.SetTile(new Vector3Int(5, 17, 0), null);

            for (int x = 80; x <= 82; x++)
            for (int y = 20; y <= 34; y++) ground.SetTile(new Vector3Int(x, y, 0), groundTile);
            for (int x = 0; x <= 4; x++)
            for (int y = 13; y <= 34; y++) ground.SetTile(new Vector3Int(x, y, 0), groundTile);
            for (int x = 40; x <= 43; x++)
            for (int y = 57; y <= 59; y++) ground.SetTile(new Vector3Int(x, y, 0), groundTile);
            for (int x = 40; x <= 43; x++)
            for (int y = 0; y <= 2; y++) ground.SetTile(new Vector3Int(x, y, 0), groundTile);

            for (int x = 29; x <= 44; x++)
            for (int y = 51; y <= 56; y++) ground.SetTile(new Vector3Int(x, y, 0), null);
            ground.SetTile(new Vector3Int(5, 45, 0), null);
            ground.SetTile(new Vector3Int(7, 45, 0), null);
            for (int x = 49; x <= 59; x++)
            for (int y = 30; y <= 39; y++) ground.SetTile(new Vector3Int(x, y, 0), null);
            for (int x = 43; x <= 44; x++)
            for (int y = 34; y <= 36; y++) ground.SetTile(new Vector3Int(x, y, 0), groundTile);
            for (int x = 5; x <= 10; x++)
            for (int y = 26; y <= 27; y++) ground.SetTile(new Vector3Int(x, y, 0), null);
            for (int x = 14; x <= 15; x++)
            for (int y = 25; y <= 26; y++) ground.SetTile(new Vector3Int(x, y, 0), null);
            for (int x = 28; x <= 30; x++)
            for (int y = 16; y <= 20; y++) ground.SetTile(new Vector3Int(x, y, 0), groundTile);
            for (int y = 18; y <= 20; y++) ground.SetTile(new Vector3Int(27, y, 0), groundTile);
            ground.SetTile(new Vector3Int(71, 19, 0), groundTile);
            ground.SetTile(new Vector3Int(71, 20, 0), groundTile);
            ground.SetTile(new Vector3Int(70, 33, 0), null);
            for (int y = 14; y <= 15; y++) ground.SetTile(new Vector3Int(8, y, 0), null);
            for (int x = 7; x <= 10; x++) ground.SetTile(new Vector3Int(x, 13, 0), null);
            for (int x = 12; x <= 13; x++) ground.SetTile(new Vector3Int(x, 13, 0), null);

            AddCandidate1uPlatforms(ground, platforms, platformTile);
            for (int x = 48; x <= 50; x++) platforms.SetTile(new Vector3Int(x, 39, 0), platformTile);
            for (int x = 55; x <= 57; x++) platforms.SetTile(new Vector3Int(x, 37, 0), null);
            for (int x = 62; x <= 64; x++) platforms.SetTile(new Vector3Int(x, 35, 0), null);
            for (int x = 65; x <= 67; x++) platforms.SetTile(new Vector3Int(x, 33, 0), null);
            for (int x = 54; x <= 56; x++) platforms.SetTile(new Vector3Int(x, 37, 0), platformTile);
            for (int x = 60; x <= 62; x++) platforms.SetTile(new Vector3Int(x, 35, 0), platformTile);
            for (int x = 66; x <= 68; x++) platforms.SetTile(new Vector3Int(x, 33, 0), platformTile);
            Vector2Int[] portalCells = AddCandidate1uSockets(root.transform, ground, platforms, groundTile);

            for (int y = 0; y < height; y++)
            {
                ground.SetTile(new Vector3Int(0, y, 0), groundTile);
                ground.SetTile(new Vector3Int(width - 1, y, 0), groundTile);
            }
            for (int x = 0; x < width; x++)
            {
                ground.SetTile(new Vector3Int(x, 0, 0), groundTile);
                ground.SetTile(new Vector3Int(x, height - 1, 0), groundTile);
            }
            for (int x = 1; x <= 4; x++)
            for (int y = 15; y <= 18; y++) ground.SetTile(new Vector3Int(x, y, 0), groundTile);
            for (int x = 80; x <= 82; x++)
            for (int y = 21; y <= 24; y++) ground.SetTile(new Vector3Int(x, y, 0), groundTile);
            for (int x = 41; x <= 43; x++)
            for (int y = 51; y <= 54; y++) ground.SetTile(new Vector3Int(x, y, 0), groundTile);

            Vector2Int[] spawns = AddCandidate1uSpawns(root.transform, ground, platforms, portalCells);

            var boundsObject = new GameObject("CameraBounds", typeof(BoxCollider2D));
            boundsObject.transform.SetParent(root.transform, false);
            boundsObject.transform.localPosition = new Vector3(width * 0.5f, height * 0.5f, 0f);
            BoxCollider2D bounds = boundsObject.GetComponent<BoxCollider2D>();
            bounds.size = new Vector2(width, height);
            bounds.isTrigger = true;

            PrefabUtility.SaveAsPrefabAsset(root, Candidate1uPrefabPath);
            Debug.Log($"[ModuleChunkBuilder] Candidate1u image-reconstructed: silhouetteDiff=0, sourceSolid={extractedSolidCount}, normalizedSolid={CountCandidate1uSolid(solid)}, oneWay=7, spawn={spawns.Length}, portal={portalCells.Length}, output={Candidate1uPrefabPath}");
        }
        finally
        {
            Object.DestroyImmediate(root);
        }
        AssetDatabase.SaveAssets();
    }

    [MenuItem("TP2/Development/Build Candidate 1u Combat Reserved")]
    public static void BuildCandidate1uCombatReserved()
    {
        GameObject root = PrefabUtility.LoadPrefabContents(Candidate1uPrefabPath);
        try
        {
            Tilemap ground = root.GetComponentsInChildren<Tilemap>(true)
                .SingleOrDefault(tilemap => tilemap.name == "Tilemap_Ground");
            Tile groundTile = AssetDatabase.LoadAssetAtPath<Tile>(
                "Assets/Textures/Environment/Tiles/Tile_Ground.asset");
            SpawnPointMarker[] spawns = root.GetComponentsInChildren<SpawnPointMarker>(true)
                .Where(marker => marker.Type == SpawnType.Monster)
                .OrderBy(marker => marker.transform.position.x)
                .ToArray();
            if (ground == null || groundTile == null || spawns.Length != 4)
                throw new InvalidDataException("Candidate 1u source requires Ground and exactly four monster spawn markers.");

            int[] centers = { 12, 32, 52, 72 };
            for (int i = 0; i < centers.Length; i++)
            {
                int center = centers[i];
                for (int x = center - 2; x <= center + 1; x++)
                    ground.SetTile(new Vector3Int(x, 49, 0), groundTile);
                for (int x = center - 3; x <= center + 2; x++)
                for (int y = 50; y <= 53; y++)
                    ground.SetTile(new Vector3Int(x, y, 0), null);
                spawns[i].transform.position = root.transform.TransformPoint(new Vector3(center, 50.51f, 0f));
            }
            ground.SetTile(new Vector3Int(54, 53, 0), null);
            for (int x = 69; x <= 74; x++) ground.SetTile(new Vector3Int(x, 53, 0), null);

            for (int x = 63; x <= 65; x++) ground.SetTile(new Vector3Int(x, 16, 0), groundTile);
            ground.SetTile(new Vector3Int(68, 20, 0), null);

            root.name = "Tilemap_Room_Candidate1u_CombatReserved";
            if (PrefabUtility.SaveAsPrefabAsset(root, Candidate1uCombatReservedPrefabPath) == null)
                throw new InvalidDataException("Candidate 1u combat-reserved prefab save failed.");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
        AssetDatabase.SaveAssets();
    }

    [MenuItem("TP2/Development/Build Golden Modules And Trial 01")]
    public static void BuildGoldenModulesAndTrial01()
    {
        const string moduleDirectory = "Assets/Prefabs/Development/GoldenModules";
        Directory.CreateDirectory(moduleDirectory);
        GameObject source = PrefabUtility.LoadPrefabContents(Candidate1uPrefabPath);
        try
        {
            Tilemap sourceGround = source.GetComponentsInChildren<Tilemap>(true)
                .Single(tilemap => tilemap.name == "Tilemap_Ground");
            Tilemap sourcePlatforms = source.GetComponentsInChildren<Tilemap>(true)
                .Single(tilemap => tilemap.name == "Tilemap_Platforms");
            Tile groundTile = AssetDatabase.LoadAssetAtPath<Tile>("Assets/Textures/Environment/Tiles/Tile_Ground.asset");
            Tile platformTile = AssetDatabase.LoadAssetAtPath<Tile>("Assets/Textures/Environment/Tiles/Tile_Platform.asset");
            if (groundTile == null || platformTile == null)
                throw new InvalidDataException("Golden extraction requires the existing Ground and OneWay tiles.");

            RectInt[] regions =
            {
                new RectInt(36, 0, 10, 8),
                new RectInt(66, 0, 18, 24),
                new RectInt(0, 13, 12, 20),
                new RectInt(8, 43, 10, 10),
                new RectInt(46, 32, 14, 12)
            };
            for (int i = 0; i < regions.Length; i++)
                SaveGoldenModule(sourceGround, sourcePlatforms, groundTile, platformTile,
                    regions[i], i + 1, $"{moduleDirectory}/Module_Golden_{i + 1}u.prefab");

            BuildGoldenTrial(source, groundTile);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(source);
        }
        AssetDatabase.SaveAssets();
    }

    private static void SaveGoldenModule(Tilemap sourceGround, Tilemap sourcePlatforms, Tile groundTile,
        Tile platformTile, RectInt region, int phase, string outputPath)
    {
        GameObject root = new GameObject($"Module_Golden_{phase}u", typeof(Grid));
        try
        {
            Tilemap ground = CreateCandidate1uTilemap(root.transform, "Tilemap_Ground", groundTile, false);
            Tilemap platforms = CreateCandidate1uTilemap(root.transform, "Tilemap_Platforms", platformTile, true);
            for (int y = 0; y < region.height; y++)
            for (int x = 0; x < region.width; x++)
            {
                int offset = ((y + phase) & 1) == 0 ? 2 : -2;
                int sourceX = (x - offset + region.width) % region.width;
                int sourceY = (y - 6 + region.height) % region.height;
                if (sourceGround.HasTile(new Vector3Int(region.x + sourceX, region.y + sourceY, 0)))
                    ground.SetTile(new Vector3Int(x, y, 0), groundTile);
                if (sourcePlatforms.HasTile(new Vector3Int(region.x + x, region.y + y, 0)))
                    platforms.SetTile(new Vector3Int(x, y, 0), platformTile);
            }
            EnsureGoldenModuleDifference(sourceGround, ground, groundTile, region);
            var boundsObject = new GameObject("ModuleBounds", typeof(BoxCollider2D));
            boundsObject.transform.SetParent(root.transform, false);
            boundsObject.transform.localPosition = new Vector3(region.width * .5f, region.height * .5f);
            BoxCollider2D bounds = boundsObject.GetComponent<BoxCollider2D>();
            bounds.size = new Vector2(region.width, region.height);
            bounds.isTrigger = true;
            if (PrefabUtility.SaveAsPrefabAsset(root, outputPath) == null)
                throw new InvalidDataException($"Golden module save failed: {outputPath}");
        }
        finally
        {
            Object.DestroyImmediate(root);
        }
    }

    private static void EnsureGoldenModuleDifference(Tilemap source, Tilemap module, Tile groundTile, RectInt region)
    {
        int area = region.width * region.height;
        int targetXor = Mathf.CeilToInt(area * .15f);
        int maxOccupancyDelta = Mathf.FloorToInt(area * .05f);
        int sourceSolid = 0, moduleSolid = 0, xor = 0;
        for (int y = 0; y < region.height; y++)
        for (int x = 0; x < region.width; x++)
        {
            bool a = source.HasTile(new Vector3Int(region.x + x, region.y + y, 0));
            bool b = module.HasTile(new Vector3Int(x, y, 0));
            if (a) sourceSolid++;
            if (b) moduleSolid++;
            if (a != b) xor++;
        }

        while (xor < targetXor)
        {
            bool changed = false;
            for (int y = 0; y < region.height && !changed; y++)
            for (int x = 0; x < region.width && !changed; x++)
            {
                var cell = new Vector3Int(x, y, 0);
                bool original = source.HasTile(new Vector3Int(region.x + x, region.y + y, 0));
                bool current = module.HasTile(cell);
                if (original != current) continue;
                int nextSolid = moduleSolid + (current ? -1 : 1);
                if (Mathf.Abs(nextSolid - sourceSolid) > maxOccupancyDelta) continue;
                bool touchesDestination = false;
                Vector3Int[] neighbors = { Vector3Int.left, Vector3Int.right, Vector3Int.up, Vector3Int.down };
                foreach (Vector3Int neighbor in neighbors)
                    if (module.HasTile(cell + neighbor) != current) touchesDestination = true;
                if (!touchesDestination) continue;
                module.SetTile(cell, current ? null : groundTile);
                moduleSolid = nextSolid;
                xor++;
                changed = true;
            }
            if (!changed)
                throw new InvalidDataException($"Golden module {region.size} cannot reach the 15% XOR contract.");
        }
    }

    private static void BuildGoldenTrial(GameObject source, Tile groundTile)
    {
        Tilemap ground = source.GetComponentsInChildren<Tilemap>(true)
            .Single(tilemap => tilemap.name == "Tilemap_Ground");
        var baseline = new bool[84, 60];
        for (int y = 0; y < 60; y++)
        for (int x = 0; x < 84; x++)
            baseline[x, y] = ground.HasTile(new Vector3Int(x, y, 0));
        Tilemap platforms = source.GetComponentsInChildren<Tilemap>(true)
            .Single(tilemap => tilemap.name == "Tilemap_Platforms");
        void ClearOneWayClearance()
        {
            foreach (Vector3Int cell in platforms.cellBounds.allPositionsWithin)
            {
                if (!platforms.HasTile(cell)) continue;
                for (int x = cell.x - 2; x <= cell.x + 2; x++)
                for (int y = cell.y - 2; y <= cell.y + 4; y++)
                    ground.SetTile(new Vector3Int(x, y, 0), null);
            }
        }
        ClearOneWayClearance();

        Vector3[] spawnPositions =
        {
            new Vector3(12f, 48.51f), new Vector3(32f, 36.51f),
            new Vector3(52f, 48.51f), new Vector3(72f, 36.51f)
        };
        SpawnPointMarker[] spawns = source.GetComponentsInChildren<SpawnPointMarker>(true)
            .Where(marker => marker.Type == SpawnType.Monster).OrderBy(marker => marker.transform.position.x).ToArray();
        if (spawns.Length != spawnPositions.Length)
            throw new InvalidDataException("Golden trial requires exactly four monster spawn markers.");
        for (int i = 0; i < spawns.Length; i++)
        {
            spawns[i].transform.position = source.transform.TransformPoint(spawnPositions[i]);
            int centerX = Mathf.RoundToInt(spawnPositions[i].x);
            int floorY = Mathf.FloorToInt(spawnPositions[i].y) - 1;
            int floorStartX = i == 3 ? centerX - 1 : centerX - 2;
            for (int x = floorStartX; x < floorStartX + 4; x++)
                ground.SetTile(new Vector3Int(x, floorY, 0), groundTile);
            for (int x = centerX - 3; x <= centerX + 2; x++)
            for (int y = floorY + 1; y <= floorY + 4; y++)
                ground.SetTile(new Vector3Int(x, y, 0), null);
            for (int y = floorY + 1; y < 59; y++)
            {
                ground.SetTile(new Vector3Int(centerX, y, 0), null);
                ground.SetTile(new Vector3Int(centerX + 1, y, 0), null);
            }
        }

        Vector3[] portalPositions =
        {
            new Vector3(8.5f, 8f), new Vector3(28.5f, 8f),
            new Vector3(55.5f, 8f), new Vector3(75.5f, 8f)
        };
        ChunkSocketDirection[] directions =
        {
            ChunkSocketDirection.West, ChunkSocketDirection.East,
            ChunkSocketDirection.South, ChunkSocketDirection.North
        };
        ChunkSocketMarker[] sockets = source.GetComponentsInChildren<ChunkSocketMarker>(true);
        if (sockets.Length != directions.Length)
            throw new InvalidDataException("Golden trial requires exactly four socket markers.");
        for (int i = 0; i < directions.Length; i++)
        {
            ChunkSocketMarker socket = sockets.Single(marker => marker.Direction == directions[i]);
            Vector3 portal = portalPositions[i];
            socket.transform.position = source.transform.TransformPoint(portal);
            socket.EntryMarker.position = source.transform.TransformPoint(new Vector3(portal.x, portal.y - .49f));
            int floorY = Mathf.FloorToInt(portal.y) - 2;
            int centerX = Mathf.FloorToInt(portal.x);
            for (int x = centerX - 1; x <= centerX + 1; x++)
                ground.SetTile(new Vector3Int(x, floorY, 0), groundTile);
            for (int x = centerX - 2; x <= centerX + 2; x++)
            for (int y = floorY + 1; y <= floorY + 4; y++)
                ground.SetTile(new Vector3Int(x, y, 0), null);
            for (int y = floorY + 1; y < 59; y++)
            {
                ground.SetTile(new Vector3Int(centerX, y, 0), null);
                ground.SetTile(new Vector3Int(centerX + 1, y, 0), null);
            }
        }

        var normalized = new bool[84, 60];
        for (int y = 0; y < 60; y++)
        for (int x = 0; x < 84; x++)
            normalized[x, y] = ground.HasTile(new Vector3Int(x, y, 0));
        NormalizeCandidate1u(normalized, 84, 60);
        for (int y = 0; y < 60; y++)
        for (int x = 0; x < 84; x++)
            ground.SetTile(new Vector3Int(x, y, 0), normalized[x, y] ? groundTile : null);
        ClearOneWayClearance();
        for (int y = 1; y < 59; y++)
        {
            ground.SetTile(new Vector3Int(2, y, 0), null);
            ground.SetTile(new Vector3Int(3, y, 0), null);
        }

        var protectedCells = new bool[84, 60];
        for (int y = 1; y < 59; y++)
        {
            protectedCells[2, y] = true;
            protectedCells[3, y] = true;
        }
        foreach (Vector3 spawn in spawnPositions)
        {
            int cx = Mathf.RoundToInt(spawn.x), fy = Mathf.FloorToInt(spawn.y) - 1;
            for (int x = cx - 3; x <= cx + 2; x++)
            for (int y = fy; y <= fy + 4; y++) protectedCells[x, y] = true;
            for (int y = fy; y <= 58; y++)
            {
                protectedCells[cx, y] = true;
                protectedCells[cx + 1, y] = true;
            }
        }
        foreach (Vector3 portal in portalPositions)
        {
            int cx = Mathf.FloorToInt(portal.x), fy = Mathf.FloorToInt(portal.y) - 2;
            for (int x = cx - 2; x <= cx + 2; x++)
            for (int y = fy; y <= fy + 4; y++) protectedCells[x, y] = true;
            for (int y = fy; y <= 58; y++)
            {
                protectedCells[cx, y] = true;
                protectedCells[cx + 1, y] = true;
            }
        }
        foreach (Vector3Int platform in platforms.cellBounds.allPositionsWithin)
        {
            if (!platforms.HasTile(platform)) continue;
            for (int x = platform.x - 2; x <= platform.x + 2; x++)
            for (int y = platform.y - 2; y <= platform.y + 4; y++)
                if (x >= 0 && x < 84 && y >= 0 && y < 60) protectedCells[x, y] = true;
        }

        int baselineSolid = 0, trialSolid = 0, trialXor = 0;
        for (int y = 0; y < 60; y++)
        for (int x = 0; x < 84; x++)
        {
            bool current = ground.HasTile(new Vector3Int(x, y, 0));
            if (baseline[x, y]) baselineSolid++;
            if (current) trialSolid++;
            if (baseline[x, y] != current) trialXor++;
        }
        int targetTrialXor = Mathf.CeilToInt(84 * 60 * .15f);
        int maxTrialOccupancyDelta = Mathf.FloorToInt(84 * 60 * .05f);
        while (trialXor < targetTrialXor)
        {
            bool preferFill = trialSolid - baselineSolid <= -maxTrialOccupancyDelta || (trialXor & 1) == 0;
            bool changed = false;
            for (int pass = 0; pass < 2 && !changed; pass++)
            {
                bool fill = pass == 0 ? preferFill : !preferFill;
                int nextDelta = trialSolid + (fill ? 1 : -1) - baselineSolid;
                int currentDelta = trialSolid - baselineSolid;
                if (Mathf.Abs(nextDelta) > maxTrialOccupancyDelta &&
                    Mathf.Abs(nextDelta) >= Mathf.Abs(currentDelta)) continue;
                for (int y = 1; y < 59 && !changed; y++)
                for (int x = 1; x < 83 && !changed; x++)
                {
                    var cell = new Vector3Int(x, y, 0);
                    bool current = ground.HasTile(cell);
                    if (protectedCells[x, y] || current == fill || baseline[x, y] != current) continue;
                    int same = 0, opposite = 0;
                    Vector3Int[] neighbors = { Vector3Int.left, Vector3Int.right, Vector3Int.up, Vector3Int.down };
                    foreach (Vector3Int neighbor in neighbors)
                    {
                        if (ground.HasTile(cell + neighbor) == current) same++;
                        else opposite++;
                    }
                    bool horizontalBridge = !ground.HasTile(cell + Vector3Int.left) &&
                                            !ground.HasTile(cell + Vector3Int.right);
                    bool verticalBridge = !ground.HasTile(cell + Vector3Int.up) &&
                                          !ground.HasTile(cell + Vector3Int.down);
                    bool safeFill = same == 1 || (same == 2 && !horizontalBridge && !verticalBridge);
                    if ((fill && !safeFill) || opposite == 0) continue;
                    ground.SetTile(cell, fill ? groundTile : null);
                    trialSolid += fill ? 1 : -1;
                    trialXor++;
                    changed = true;
                }
            }
            if (!changed)
                throw new InvalidDataException("Golden trial cannot satisfy XOR and occupancy contracts without touching protected cells.");
        }

        source.name = "Tilemap_Room_GoldenDerived_Trial01";
        if (PrefabUtility.SaveAsPrefabAsset(source, GoldenTrialPrefabPath) == null)
            throw new InvalidDataException("Golden trial prefab save failed.");
    }

    [MenuItem("TP2/Development/Build Empty First Trial 02")]
    public static void BuildEmptyFirstTrial02()
    {
        const int width = 84, height = 60, shell = 3;
        var random = new System.Random(20260818);
        var solid = new bool[width, height];
        var protectedEmpty = new bool[width, height];
        for (int y = 0; y < height; y++)
        for (int x = 0; x < width; x++) solid[x, y] = true;

        Vector2Int[] sizes =
        {
            new Vector2Int(6, 4), new Vector2Int(6, 4), new Vector2Int(6, 4),
            new Vector2Int(6, 4), new Vector2Int(12, 8), new Vector2Int(8, 16)
        };
        Vector2Int[] anchors =
        {
            new Vector2Int(10, 10), new Vector2Int(34, 10), new Vector2Int(58, 10),
            new Vector2Int(10, 38), new Vector2Int(30, 36), new Vector2Int(62, 32)
        };
        var rooms = new RectInt[sizes.Length];
        var centers = new Vector2Int[sizes.Length];
        void Carve(int fromX, int toX, int fromY, int toY)
        {
            for (int y = Mathf.Max(shell, fromY); y <= Mathf.Min(height - shell - 1, toY); y++)
            for (int x = Mathf.Max(shell, fromX); x <= Mathf.Min(width - shell - 1, toX); x++)
            {
                solid[x, y] = false;
                protectedEmpty[x, y] = true;
            }
        }
        for (int i = 0; i < rooms.Length; i++)
        {
            Vector2Int jitter = new Vector2Int(random.Next(-2, 3), random.Next(-2, 3));
            Vector2Int origin = anchors[i] + jitter;
            rooms[i] = new RectInt(origin, sizes[i]);
            centers[i] = new Vector2Int(origin.x + sizes[i].x / 2, origin.y + sizes[i].y / 2);
            Carve(origin.x, origin.x + sizes[i].x - 1, origin.y, origin.y + sizes[i].y - 1);
        }

        var edges = new List<(int a, int b, int cost)>();
        for (int a = 0; a < centers.Length; a++)
        for (int b = a + 1; b < centers.Length; b++)
            edges.Add((a, b, Mathf.Abs(centers[a].x - centers[b].x) + Mathf.Abs(centers[a].y - centers[b].y)));
        edges.Sort((left, right) => left.cost != right.cost ? left.cost.CompareTo(right.cost) :
            left.a != right.a ? left.a.CompareTo(right.a) : left.b.CompareTo(right.b));
        int[] parent = { 0, 1, 2, 3, 4, 5 };
        int Find(int node)
        {
            while (parent[node] != node) node = parent[node];
            return node;
        }
        var selected = new List<(int a, int b)>();
        foreach ((int a, int b, int cost) edge in edges)
        {
            int rootA = Find(edge.a), rootB = Find(edge.b);
            if (rootA == rootB) continue;
            parent[rootB] = rootA;
            selected.Add((edge.a, edge.b));
            if (selected.Count == centers.Length - 1) break;
        }
        foreach ((int a, int b, int cost) edge in edges)
            if (!selected.Contains((edge.a, edge.b)))
            {
                selected.Add((edge.a, edge.b));
                break;
            }

        var oneWayRuns = new List<(int x, int y)>();
        foreach ((int a, int b) edge in selected)
        {
            Vector2Int from = centers[edge.a], to = centers[edge.b];
            Carve(Mathf.Min(from.x, to.x), Mathf.Max(from.x, to.x), from.y - 1, from.y + 2);
            Carve(to.x - 2, to.x + 2, Mathf.Min(from.y, to.y), Mathf.Max(from.y, to.y));
            int lowY = Mathf.Min(from.y, to.y) + 2, highY = Mathf.Max(from.y, to.y) - 2;
            for (int y = lowY; y <= highY; y += 2)
                oneWayRuns.Add((to.x - 1 + ((y / 2) & 1), y));
        }

        int CountEmptyRegions()
        {
            var visited = new bool[width, height];
            int count = 0;
            int[] dx = { -1, 1, 0, 0 }, dy = { 0, 0, -1, 1 };
            for (int sy = shell; sy < height - shell; sy++)
            for (int sx = shell; sx < width - shell; sx++)
            {
                if (solid[sx, sy] || visited[sx, sy]) continue;
                count++;
                var queue = new Queue<Vector2Int>();
                queue.Enqueue(new Vector2Int(sx, sy));
                visited[sx, sy] = true;
                while (queue.Count > 0)
                {
                    Vector2Int cell = queue.Dequeue();
                    for (int i = 0; i < 4; i++)
                    {
                        int x = cell.x + dx[i], y = cell.y + dy[i];
                        if (x < shell || x >= width - shell || y < shell || y >= height - shell ||
                            solid[x, y] || visited[x, y]) continue;
                        visited[x, y] = true;
                        queue.Enqueue(new Vector2Int(x, y));
                    }
                }
            }
            return count;
        }
        if (selected.Count != 6 || CountEmptyRegions() != 1)
            throw new InvalidDataException("Empty-first Trial02 graph generation failed.");

        var visitedSolid = new bool[width, height];
        for (int sy = shell; sy < height - shell; sy++)
        for (int sx = shell; sx < width - shell; sx++)
        {
            if (!solid[sx, sy] || visitedSolid[sx, sy]) continue;
            var component = new List<Vector2Int>();
            var queue = new Queue<Vector2Int>();
            queue.Enqueue(new Vector2Int(sx, sy));
            visitedSolid[sx, sy] = true;
            while (queue.Count > 0)
            {
                Vector2Int cell = queue.Dequeue();
                component.Add(cell);
                Vector2Int[] neighbors = { Vector2Int.left, Vector2Int.right, Vector2Int.up, Vector2Int.down };
                foreach (Vector2Int neighbor in neighbors)
                {
                    Vector2Int next = cell + neighbor;
                    if (next.x < shell || next.x >= width - shell || next.y < shell || next.y >= height - shell ||
                        !solid[next.x, next.y] || visitedSolid[next.x, next.y]) continue;
                    visitedSolid[next.x, next.y] = true;
                    queue.Enqueue(next);
                }
            }
            if (component.Count <= 2 && !component.Exists(cell => protectedEmpty[cell.x, cell.y]))
                foreach (Vector2Int cell in component) solid[cell.x, cell.y] = false;
        }

        Tile groundTile = AssetDatabase.LoadAssetAtPath<Tile>("Assets/Textures/Environment/Tiles/Tile_Ground.asset");
        Tile platformTile = AssetDatabase.LoadAssetAtPath<Tile>("Assets/Textures/Environment/Tiles/Tile_Platform.asset");
        if (groundTile == null || platformTile == null)
            throw new InvalidDataException("Empty-first Trial02 requires existing Ground and OneWay tiles.");
        GameObject root = new GameObject("Tilemap_Room_EmptyFirst_Trial02", typeof(Grid));
        try
        {
            Tilemap ground = CreateCandidate1uTilemap(root.transform, "Tilemap_Ground", groundTile, false);
            Tilemap platforms = CreateCandidate1uTilemap(root.transform, "Tilemap_Platforms", platformTile, true);
            for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
                if (solid[x, y]) ground.SetTile(new Vector3Int(x, y, 0), groundTile);
            foreach ((int x, int y) run in oneWayRuns)
                for (int x = run.x; x < run.x + 3; x++)
                {
                    platforms.SetTile(new Vector3Int(x, run.y, 0), platformTile);
                    for (int y = run.y; y <= run.y + 4; y++)
                        ground.SetTile(new Vector3Int(x, y, 0), null);
                }

            ChunkSocketDirection[] directions =
            {
                ChunkSocketDirection.West, ChunkSocketDirection.East,
                ChunkSocketDirection.South, ChunkSocketDirection.North
            };
            for (int i = 0; i < 4; i++)
            {
                RectInt room = rooms[i];
                float portalX = room.x + 1.5f, surfaceY = room.y;
                AddSocket(root.transform, directions[i], new Vector3(portalX, surfaceY + 1f));
                AddGroundedSpawnMarker(root.transform, ground, platforms, $"SpawnZone_{i + 1}",
                    room.x + room.width - 2f, room.y + room.height - 1f, SpawnType.Monster);
            }
            var boundsObject = new GameObject("CameraBounds", typeof(BoxCollider2D));
            boundsObject.transform.SetParent(root.transform, false);
            boundsObject.transform.localPosition = new Vector3(width * .5f, height * .5f);
            BoxCollider2D bounds = boundsObject.GetComponent<BoxCollider2D>();
            bounds.size = new Vector2(width, height);
            bounds.isTrigger = true;
            if (PrefabUtility.SaveAsPrefabAsset(root, EmptyFirstTrial02PrefabPath) == null)
                throw new InvalidDataException("Empty-first Trial02 prefab save failed.");
        }
        finally
        {
            Object.DestroyImmediate(root);
        }
        AssetDatabase.SaveAssets();
    }

    [MenuItem("TP2/Development/Build Empty First Angular Trial 03")]
    public static void BuildEmptyFirstAngularTrial03()
    {
        const int width = 84, height = 60, shell = 3;
        var solid = new bool[width, height];
        for (int y = 0; y < height; y++)
        for (int x = 0; x < width; x++) solid[x, y] = true;
        void Carve(RectInt area)
        {
            for (int y = Mathf.Max(shell, area.yMin); y < Mathf.Min(height - shell, area.yMax); y++)
            for (int x = Mathf.Max(shell, area.xMin); x < Mathf.Min(width - shell, area.xMax); x++)
                solid[x, y] = false;
        }

        RectInt[] combatRooms =
        {
            new RectInt(6, 8, 24, 12), new RectInt(54, 8, 24, 12), new RectInt(30, 40, 24, 12)
        };
        foreach (RectInt room in combatRooms) Carve(room);
        Carve(new RectInt(24, 26, 36, 8));
        Carve(new RectInt(24, 20, 8, 20));
        Vector2Int[] centers =
        {
            new Vector2Int(18, 14), new Vector2Int(66, 14),
            new Vector2Int(42, 46), new Vector2Int(42, 30)
        };

        var edges = new List<(int a, int b, int cost)>();
        for (int a = 0; a < centers.Length; a++)
        for (int b = a + 1; b < centers.Length; b++)
            edges.Add((a, b, Mathf.Abs(centers[a].x - centers[b].x) + Mathf.Abs(centers[a].y - centers[b].y)));
        edges.Sort((left, right) => left.cost != right.cost ? left.cost.CompareTo(right.cost) :
            left.a != right.a ? left.a.CompareTo(right.a) : left.b.CompareTo(right.b));
        int[] parent = { 0, 1, 2, 3 };
        int Find(int node)
        {
            while (parent[node] != node) node = parent[node];
            return node;
        }
        var selected = new List<(int a, int b)>();
        foreach ((int a, int b, int cost) edge in edges)
        {
            int rootA = Find(edge.a), rootB = Find(edge.b);
            if (rootA == rootB) continue;
            parent[rootB] = rootA;
            selected.Add((edge.a, edge.b));
            if (selected.Count == 3) break;
        }
        foreach ((int a, int b, int cost) edge in edges)
            if (!selected.Contains((edge.a, edge.b)))
            {
                selected.Add((edge.a, edge.b));
                break;
            }

        GameObject playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Unit_3001.prefab");
        Player player = playerPrefab != null ? playerPrefab.GetComponent<Player>() : null;
        KinematicMotor2D motor = playerPrefab != null ? playerPrefab.GetComponent<KinematicMotor2D>() : null;
        SerializedProperty jumpForceProperty = player != null
            ? new SerializedObject(player).FindProperty("jumpForce")
            : null;
        TextAsset unitCsv = AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/Datas/UnitBaseData.csv");
        var unitTable = new UnitBaseDataTable();
        if (unitCsv != null) unitTable.LoadData(unitCsv.text);
        if (!unitTable.TryGetUnitData(3001u, out UnitBaseData playerData) || playerData.MoveSpeed <= 0f ||
            player == null || motor == null || jumpForceProperty == null ||
            jumpForceProperty.floatValue <= 0f || motor.Gravity <= 0f || motor.FallGravityMultiplier <= 0f)
            throw new InvalidDataException("Trial03 requires valid Unit_3001 movement values.");
        float jumpVelocity = jumpForceProperty.floatValue;
        float jumpHeight = jumpVelocity * jumpVelocity / (2f * motor.Gravity);
        float maxRise = jumpHeight - motor.SkinWidth * 2f;
        float maxHorizontal = playerData.MoveSpeed * (jumpVelocity / motor.Gravity +
            Mathf.Sqrt(2f * jumpHeight / (motor.Gravity * motor.FallGravityMultiplier))) - motor.SkinWidth * 2f;
        int platformRise = Mathf.FloorToInt(maxRise);
        if (platformRise < 1 || maxHorizontal < 1f)
            throw new InvalidDataException("Unit_3001 jump envelope cannot place Trial03 platforms.");

        var verticalCorridors = new List<(int x, int minY, int maxY)>();
        foreach ((int a, int b) edge in selected)
        {
            Vector2Int from = centers[edge.a], to = centers[edge.b];
            int combatIndex = edge.a < 3 ? edge.a : edge.b < 3 ? edge.b : -1;
            int verticalX = combatIndex >= 0 ? combatRooms[combatIndex].xMin : to.x;
            Carve(new RectInt(Mathf.Min(from.x, verticalX), from.y - 2,
                Mathf.Abs(from.x - verticalX) + 1, 5));
            Carve(new RectInt(Mathf.Min(verticalX, to.x), to.y - 2,
                Mathf.Abs(verticalX - to.x) + 1, 5));
            Carve(new RectInt(verticalX - 2, Mathf.Min(from.y, to.y), 5,
                Mathf.Abs(from.y - to.y) + 1));
            verticalCorridors.Add((verticalX, Mathf.Min(from.y, to.y), Mathf.Max(from.y, to.y)));
        }

        var oneWayRuns = new List<(int x, int y)>();
        foreach ((int x, int minY, int maxY) corridor in verticalCorridors)
        {
            int predecessorSurfaceY = -1;
            for (int y = corridor.minY; y >= shell; y--)
                if (solid[corridor.x, y])
                {
                    predecessorSurfaceY = y + 1;
                    break;
                }
            if (predecessorSurfaceY < 0)
                throw new InvalidDataException("Trial03 vertical corridor has no Ground predecessor.");

            for (int surfaceY = predecessorSurfaceY + platformRise;
                 surfaceY < corridor.maxY;
                 surfaceY += platformRise)
            {
                var run = (corridor.x - 1, surfaceY - 1);
                if (!oneWayRuns.Contains(run)) oneWayRuns.Add(run);
            }
        }

        int EmptyRegions()
        {
            var visited = new bool[width, height];
            int count = 0;
            for (int sy = shell; sy < height - shell; sy++)
            for (int sx = shell; sx < width - shell; sx++)
            {
                if (solid[sx, sy] || visited[sx, sy]) continue;
                count++;
                var queue = new Queue<Vector2Int>();
                queue.Enqueue(new Vector2Int(sx, sy));
                visited[sx, sy] = true;
                while (queue.Count > 0)
                {
                    Vector2Int cell = queue.Dequeue();
                    foreach (Vector2Int direction in new[] { Vector2Int.left, Vector2Int.right, Vector2Int.up, Vector2Int.down })
                    {
                        Vector2Int next = cell + direction;
                        if (next.x < shell || next.x >= width - shell || next.y < shell || next.y >= height - shell ||
                            solid[next.x, next.y] || visited[next.x, next.y]) continue;
                        visited[next.x, next.y] = true;
                        queue.Enqueue(next);
                    }
                }
            }
            return count;
        }
        if (selected.Count != 4 || EmptyRegions() != 1)
            throw new InvalidDataException("Empty-first Angular Trial03 graph generation failed.");

        Tile groundTile = AssetDatabase.LoadAssetAtPath<Tile>("Assets/Textures/Environment/Tiles/Tile_Ground.asset");
        Tile platformTile = AssetDatabase.LoadAssetAtPath<Tile>("Assets/Textures/Environment/Tiles/Tile_Platform.asset");
        if (groundTile == null || platformTile == null)
            throw new InvalidDataException("Empty-first Angular Trial03 requires existing Ground and OneWay tiles.");
        GameObject root = new GameObject("Tilemap_Room_EmptyFirstAngular_Trial03", typeof(Grid));
        try
        {
            Tilemap ground = CreateCandidate1uTilemap(root.transform, "Tilemap_Ground", groundTile, false);
            Tilemap platforms = CreateCandidate1uTilemap(root.transform, "Tilemap_Platforms", platformTile, true);
            for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
                if (solid[x, y]) ground.SetTile(new Vector3Int(x, y, 0), groundTile);
            foreach ((int x, int y) run in oneWayRuns)
                for (int x = run.x; x < run.x + 3; x++)
                {
                    platforms.SetTile(new Vector3Int(x, run.y, 0), platformTile);
                    for (int y = run.y; y <= run.y + 4; y++) ground.SetTile(new Vector3Int(x, y, 0), null);
                }

            RectInt[] markerRooms =
            {
                combatRooms[0], combatRooms[1], combatRooms[2], new RectInt(24, 26, 36, 8)
            };
            ChunkSocketDirection[] directions =
            {
                ChunkSocketDirection.West, ChunkSocketDirection.East,
                ChunkSocketDirection.South, ChunkSocketDirection.North
            };
            for (int i = 0; i < markerRooms.Length; i++)
            {
                RectInt room = markerRooms[i];
                AddSocket(root.transform, directions[i], new Vector3(room.x + 2.5f, room.y + 1f));
                AddGroundedSpawnMarker(root.transform, ground, platforms, $"SpawnZone_{i + 1}",
                    room.xMax - 3f, room.yMax - 1f, SpawnType.Monster);
            }
            var boundsObject = new GameObject("CameraBounds", typeof(BoxCollider2D));
            boundsObject.transform.SetParent(root.transform, false);
            boundsObject.transform.localPosition = new Vector3(width * .5f, height * .5f);
            BoxCollider2D bounds = boundsObject.GetComponent<BoxCollider2D>();
            bounds.size = new Vector2(width, height);
            bounds.isTrigger = true;
            if (PrefabUtility.SaveAsPrefabAsset(root, EmptyFirstAngularTrial03PrefabPath) == null)
                throw new InvalidDataException("Empty-first Angular Trial03 prefab save failed.");
        }
        finally
        {
            Object.DestroyImmediate(root);
        }
        AssetDatabase.SaveAssets();
    }

    [MenuItem("TP2/Development/Build Empty First Angular Reachable Trial 04")]
    public static void BuildEmptyFirstAngularTrial04()
    {
        if (AssetDatabase.LoadAssetAtPath<GameObject>(EmptyFirstAngularTrial04PrefabPath) != null)
        {
            ProjectTrial04SpawnMarkers();
            return;
        }
        const int width = 84, height = 60, shell = 3;
        GameObject source = AssetDatabase.LoadAssetAtPath<GameObject>(EmptyFirstAngularTrial03PrefabPath);
        GameObject playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Unit_3001.prefab");
        if (source == null || playerPrefab == null)
            throw new InvalidDataException("Trial04 requires Trial03 and Unit_3001 prefabs.");

        Player player = playerPrefab.GetComponent<Player>();
        KinematicMotor2D motor = playerPrefab.GetComponent<KinematicMotor2D>();
        SerializedProperty jumpProperty = player != null ? new SerializedObject(player).FindProperty("jumpForce") : null;
        var unitTable = new UnitBaseDataTable();
        TextAsset unitCsv = AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/Datas/UnitBaseData.csv");
        if (unitCsv != null) unitTable.LoadData(unitCsv.text);
        if (player == null || motor == null || jumpProperty == null ||
            !unitTable.TryGetUnitData(3001u, out UnitBaseData playerData) || playerData.MoveSpeed <= 0f ||
            jumpProperty.floatValue <= 0f || motor.Gravity <= 0f || motor.FallGravityMultiplier <= 0f)
            throw new InvalidDataException("Trial04 requires valid Unit_3001 movement values.");
        float jumpHeight = jumpProperty.floatValue * jumpProperty.floatValue / (2f * motor.Gravity);
        float maxRise = jumpHeight - motor.SkinWidth * 2f;
        float maxHorizontal = playerData.MoveSpeed * (jumpProperty.floatValue / motor.Gravity +
            Mathf.Sqrt(2f * jumpHeight / (motor.Gravity * motor.FallGravityMultiplier))) - motor.SkinWidth * 2f;
        int platformRise = Mathf.FloorToInt(maxRise);

        GameObject root = Object.Instantiate(source);
        root.name = "Tilemap_Room_EmptyFirstAngular_Trial04";
        try
        {
            Tilemap ground = root.GetComponentsInChildren<Tilemap>(true).Single(x => x.name == "Tilemap_Ground");
            Tilemap platforms = root.GetComponentsInChildren<Tilemap>(true).Single(x => x.name == "Tilemap_Platforms");
            Tile groundTile = AssetDatabase.LoadAssetAtPath<Tile>("Assets/Textures/Environment/Tiles/Tile_Ground.asset");
            Tile platformTile = AssetDatabase.LoadAssetAtPath<Tile>("Assets/Textures/Environment/Tiles/Tile_Platform.asset");
            if (groundTile == null || platformTile == null || platformRise < 1 || maxHorizontal < 1f)
                throw new InvalidDataException("Trial04 tiles or jump envelope are invalid.");

            var solid = new bool[width, height];
            for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
                solid[x, y] = x < shell || x >= width - shell || y < shell || y >= height - shell ||
                    ground.HasTile(new Vector3Int(x, y, 0));
            void Carve(RectInt area)
            {
                for (int y = Mathf.Max(shell, area.yMin); y < Mathf.Min(height - shell, area.yMax); y++)
                for (int x = Mathf.Max(shell, area.xMin); x < Mathf.Min(width - shell, area.xMax); x++) solid[x, y] = false;
            }

            RectInt[] rooms =
            {
                new RectInt(3, 5, 30, 16), new RectInt(51, 5, 30, 16),
                new RectInt(27, 41, 30, 16), new RectInt(24, 26, 36, 8),
                new RectInt(24, 20, 8, 20)
            };
            foreach (RectInt room in rooms)
            {
                Carve(room);
                for (int x = room.xMin; x < room.xMax; x++) solid[x, room.yMin - 1] = true;
            }
            Vector2Int[] centers = rooms.Select(room => new Vector2Int(
                Mathf.RoundToInt(room.center.x), Mathf.RoundToInt(room.center.y))).ToArray();
            var edges = new List<(int a, int b, int cost)>();
            for (int a = 0; a < centers.Length; a++)
            for (int b = a + 1; b < centers.Length; b++)
                edges.Add((a, b, Mathf.Abs(centers[a].x - centers[b].x) + Mathf.Abs(centers[a].y - centers[b].y)));
            edges.Sort((a, b) => a.cost != b.cost ? a.cost.CompareTo(b.cost) : a.a != b.a ? a.a.CompareTo(b.a) : a.b.CompareTo(b.b));
            int[] parent = { 0, 1, 2, 3, 4 };
            int Find(int node) { while (parent[node] != node) node = parent[node]; return node; }
            var selected = new List<(int a, int b)>();
            foreach ((int a, int b, int cost) edge in edges)
            {
                int rootA = Find(edge.a), rootB = Find(edge.b);
                if (rootA == rootB) continue;
                parent[rootB] = rootA;
                selected.Add((edge.a, edge.b));
                if (selected.Count == 4) break;
            }
            foreach ((int a, int b, int cost) edge in edges)
                if (!selected.Contains((edge.a, edge.b))) { selected.Add((edge.a, edge.b)); break; }

            var verticalCorridors = new List<(int x, int minY, int maxY)>();
            foreach ((int a, int b) edge in selected)
            {
                Vector2Int from = centers[edge.a], to = centers[edge.b];
                int combatIndex = edge.a < 3 ? edge.a : edge.b < 3 ? edge.b : -1;
                int verticalX = combatIndex >= 0 ? rooms[combatIndex].xMin : to.x;
                Carve(new RectInt(Mathf.Min(from.x, verticalX), from.y - 2, Mathf.Abs(from.x - verticalX) + 1, 5));
                Carve(new RectInt(Mathf.Min(verticalX, to.x), to.y - 2, Mathf.Abs(verticalX - to.x) + 1, 5));
                Carve(new RectInt(verticalX - 2, Mathf.Min(from.y, to.y), 5, Mathf.Abs(from.y - to.y) + 1));
                verticalCorridors.Add((verticalX, Mathf.Min(from.y, to.y), Mathf.Max(from.y, to.y)));
            }

            WallJumpSurface wallSurface = ground.GetComponent<WallJumpSurface>() ?? ground.gameObject.AddComponent<WallJumpSurface>();
            wallSurface.CanWallJump = true;
            const float wallJumpRise = 2.58f;
            int wallPredecessors = 0;
            foreach ((int x, int minY, int maxY) corridor in verticalCorridors)
            {
                for (int y = corridor.minY; y < corridor.maxY; y += Mathf.FloorToInt(wallJumpRise))
                    if (solid[corridor.x - 3, y] || solid[corridor.x + 3, y]) wallPredecessors++;
                    else throw new InvalidDataException("Trial04 vertical corridor has no continuous Wall-jump predecessor.");
            }
            if (wallPredecessors < 2)
                throw new InvalidDataException("Trial04 requires at least two Step or Wall predecessors.");

            ground.ClearAllTiles();
            platforms.ClearAllTiles();
            for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
                if (solid[x, y]) ground.SetTile(new Vector3Int(x, y, 0), groundTile);
            foreach (SpawnPointMarker marker in root.GetComponentsInChildren<SpawnPointMarker>(true))
            {
                int centerX = Mathf.RoundToInt(marker.transform.localPosition.x);
                bool moved = false;
                for (int y = Mathf.Min(height - shell - 1, Mathf.FloorToInt(marker.transform.localPosition.y)); y >= shell; y--)
                {
                    bool supported = true;
                    for (int x = centerX - 1; x <= centerX + 1; x++)
                        supported &= ground.HasTile(new Vector3Int(x, y, 0));
                    for (int x = centerX - 1; supported && x <= centerX + 1; x++)
                    for (int clearY = y + 1; clearY <= y + 4; clearY++)
                        supported &= !ground.HasTile(new Vector3Int(x, clearY, 0));
                    if (!supported) continue;
                    marker.transform.localPosition = new Vector3(marker.transform.localPosition.x, y + 1.51f, 0f);
                    moved = true;
                    break;
                }
                if (!moved) throw new InvalidDataException("Trial04 SpawnZone has no continuous Ground support.");
            }

            if (PrefabUtility.SaveAsPrefabAsset(root, EmptyFirstAngularTrial04PrefabPath) == null)
                throw new InvalidDataException("Empty-first Angular Trial04 prefab save failed.");
        }
        finally
        {
            Object.DestroyImmediate(root);
        }
        AssetDatabase.SaveAssets();
    }

    private static void ProjectTrial04SpawnMarkers()
    {
        GameObject playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Unit_3001.prefab");
        CapsuleCollider2D playerCollider = playerPrefab != null ? playerPrefab.GetComponent<CapsuleCollider2D>() : null;
        KinematicMotor2D motor = playerPrefab != null ? playerPrefab.GetComponent<KinematicMotor2D>() : null;
        if (playerCollider == null || motor == null)
            throw new InvalidDataException("Trial04 Spawn projection requires Unit_3001 collider and motor.");
        float scaleY = Mathf.Abs(playerCollider.transform.lossyScale.y);
        float colliderBottomFromPivot = (playerCollider.offset.y - playerCollider.size.y * .5f) * scaleY;

        GameObject root = PrefabUtility.LoadPrefabContents(EmptyFirstAngularTrial04PrefabPath);
        try
        {
            Tilemap ground = root.GetComponentsInChildren<Tilemap>(true).Single(x => x.name == "Tilemap_Ground");
            Tilemap platforms = root.GetComponentsInChildren<Tilemap>(true).Single(x => x.name == "Tilemap_Platforms");
            SpawnPointMarker[] markers = root.GetComponentsInChildren<SpawnPointMarker>(true);
            RectInt[] rooms =
            {
                new RectInt(3, 5, 30, 16), new RectInt(51, 5, 30, 16),
                new RectInt(27, 41, 30, 16), new RectInt(24, 26, 36, 8)
            };
            if (markers.Length != rooms.Length)
                throw new InvalidDataException("Trial04 requires exactly four SpawnZone markers.");

            var unused = new List<SpawnPointMarker>(markers);
            foreach (RectInt room in rooms)
            {
                SpawnPointMarker marker = unused.OrderBy(candidate =>
                    Vector2.SqrMagnitude((Vector2)candidate.transform.localPosition - room.center)).First();
                unused.Remove(marker);
                Vector3 best = default;
                float bestDistance = float.MaxValue;
                for (int y = room.yMin - 1; y < room.yMax; y++)
                for (int startX = room.xMin + 1; startX + 4 < room.xMax; startX++)
                {
                    bool valid = true;
                    for (int x = startX; x < startX + 4; x++)
                    {
                        valid &= ground.HasTile(new Vector3Int(x, y, 0)) &&
                            !platforms.HasTile(new Vector3Int(x, y, 0));
                        for (int clearY = y + 1; clearY <= y + 4; clearY++)
                            valid &= !ground.HasTile(new Vector3Int(x, clearY, 0)) &&
                                !platforms.HasTile(new Vector3Int(x, clearY, 0));
                    }
                    for (int clearY = y + 1; valid && clearY <= y + 2; clearY++)
                        valid &= !ground.HasTile(new Vector3Int(startX - 1, clearY, 0)) &&
                            !ground.HasTile(new Vector3Int(startX + 4, clearY, 0));
                    if (!valid) continue;
                    Vector3 surface = ground.CellToWorld(new Vector3Int(startX + 2, y + 1, 0));
                    float distance = Mathf.Abs(surface.x - room.center.x) + Mathf.Abs(surface.y - room.center.y);
                    if (distance >= bestDistance) continue;
                    bestDistance = distance;
                    best = new Vector3(surface.x, surface.y + motor.SkinWidth - colliderBottomFromPivot, 0f);
                }
                if (bestDistance == float.MaxValue)
                    throw new InvalidDataException("Trial04 Combat room has no valid Spawn surface.");
                marker.transform.position = best;
            }
            PrefabUtility.SaveAsPrefabAsset(root, EmptyFirstAngularTrial04PrefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
        AssetDatabase.SaveAssets();
    }

    private static Tilemap CreateCandidate1uTilemap(Transform parent, string name, Tile tile, bool oneWay)
    {
        var child = new GameObject(name, typeof(Tilemap), typeof(TilemapRenderer), typeof(TilemapCollider2D));
        child.transform.SetParent(parent, false);
        TilemapRenderer renderer = child.GetComponent<TilemapRenderer>();
        renderer.sortingLayerName = "Tilemap";
        renderer.sortingOrder = oneWay ? 5 : 0;
        if (oneWay)
        {
            int layer = LayerMask.NameToLayer("OneWayPlatform");
            if (layer >= 0) child.layer = layer;
            TilemapCollider2D collider = child.GetComponent<TilemapCollider2D>();
            collider.usedByEffector = true;
            PlatformEffector2D effector = child.AddComponent<PlatformEffector2D>();
            effector.useOneWay = true;
            effector.surfaceArc = 180f;
            child.AddComponent<OneWayPlatformPassThrough>();
        }
        else
        {
            Rigidbody2D body = child.AddComponent<Rigidbody2D>();
            body.bodyType = RigidbodyType2D.Static;
            CompositeCollider2D composite = child.AddComponent<CompositeCollider2D>();
            child.GetComponent<TilemapCollider2D>().compositeOperation = Collider2D.CompositeOperation.Merge;
        }
        return child.GetComponent<Tilemap>();
    }

    private static int CountCandidate1uSilhouetteDiff(byte[] png, bool[,] solid, int width, int height,
        int roiX, int roiY, int pixelsPerCell)
    {
        var source = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        source.LoadImage(png);
        int diff = 0;
        for (int y = 0; y < height; y++)
        for (int x = 0; x < width; x++)
        {
            Color32 color = source.GetPixel(roiX + x * pixelsPerCell + 5,
                source.height - 1 - (roiY + y * pixelsPerCell + 5));
            bool sourceSolid = color.r == 94 && color.g == 123 && color.b == 105;
            if (sourceSolid != solid[x, height - 1 - y]) diff++;
        }
        Object.DestroyImmediate(source);
        return diff;
    }

    private static void NormalizeCandidate1u(bool[,] solid, int width, int height)
    {
        var visited = new bool[width, height];
        int[] dx = { -1, 1, 0, 0 };
        int[] dy = { 0, 0, -1, 1 };
        for (int sy = 1; sy < height - 1; sy++)
        for (int sx = 1; sx < width - 1; sx++)
        {
            if (!solid[sx, sy] || visited[sx, sy]) continue;
            var cells = new List<Vector2Int>();
            var queue = new Queue<Vector2Int>();
            queue.Enqueue(new Vector2Int(sx, sy));
            visited[sx, sy] = true;
            bool touchesBoundary = false;
            while (queue.Count > 0)
            {
                Vector2Int cell = queue.Dequeue();
                cells.Add(cell);
                touchesBoundary |= cell.x <= 1 || cell.x >= width - 2 || cell.y <= 1;
                for (int i = 0; i < 4; i++)
                {
                    int nx = cell.x + dx[i], ny = cell.y + dy[i];
                    if (nx < 0 || nx >= width || ny < 0 || ny >= height || visited[nx, ny] || !solid[nx, ny]) continue;
                    visited[nx, ny] = true;
                    queue.Enqueue(new Vector2Int(nx, ny));
                }
            }
            if (!touchesBoundary && cells.Count <= 2)
                foreach (Vector2Int cell in cells) solid[cell.x, cell.y] = false;
        }
        var fill = new List<Vector2Int>();
        for (int y = 1; y < height - 1; y++)
        for (int x = 1; x < width - 1; x++)
            if (!solid[x, y] && solid[x - 1, y] && solid[x + 1, y] && solid[x, y - 1] && solid[x, y + 1])
                fill.Add(new Vector2Int(x, y));
        foreach (Vector2Int cell in fill) solid[cell.x, cell.y] = true;
    }

    private static void AddCandidate1uPlatforms(Tilemap ground, Tilemap platforms, Tile tile)
    {
        Vector2Int[] starts = { new Vector2Int(46, 10), new Vector2Int(50, 13), new Vector2Int(57, 18) };
        int[] lengths = { 3, 4, 6 };
        for (int i = 0; i < starts.Length; i++)
        for (int x = starts[i].x; x < starts[i].x + lengths[i]; x++)
        {
            platforms.SetTile(new Vector3Int(x, starts[i].y, 0), tile);
            for (int y = starts[i].y; y <= starts[i].y + 4; y++) ground.SetTile(new Vector3Int(x, y, 0), null);
        }
    }

    private static Vector2Int[] AddCandidate1uSockets(Transform root, Tilemap ground, Tilemap platforms, Tile groundTile)
    {
        for (int x = 40; x <= 43; x++)
        {
            ground.SetTile(new Vector3Int(x, 2, 0), groundTile);
            ground.SetTile(new Vector3Int(x, 3, 0), groundTile);
            for (int y = 4; y <= 7; y++)
            {
                ground.SetTile(new Vector3Int(x, y, 0), null);
                platforms.SetTile(new Vector3Int(x, y, 0), null);
            }
        }
        for (int x = 44; x <= 46; x++)
        {
            ground.SetTile(new Vector3Int(x, 3, 0), groundTile);
            ground.SetTile(new Vector3Int(x, 4, 0), groundTile);
            for (int y = 5; y <= 8; y++)
            {
                ground.SetTile(new Vector3Int(x, y, 0), null);
                platforms.SetTile(new Vector3Int(x, y, 0), null);
            }
        }
        for (int x = 68; x <= 70; x++)
        {
            ground.SetTile(new Vector3Int(x, 19, 0), groundTile);
            ground.SetTile(new Vector3Int(x, 20, 0), groundTile);
            for (int y = 21; y <= 24; y++)
            {
                ground.SetTile(new Vector3Int(x, y, 0), null);
                platforms.SetTile(new Vector3Int(x, y, 0), null);
            }
        }
        for (int x = 65; x <= 67; x++)
        {
            ground.SetTile(new Vector3Int(x, 17, 0), groundTile);
            ground.SetTile(new Vector3Int(x, 18, 0), groundTile);
            for (int y = 19; y <= 22; y++)
            {
                ground.SetTile(new Vector3Int(x, y, 0), null);
                platforms.SetTile(new Vector3Int(x, y, 0), null);
            }
        }

        Vector2Int[] cells = {
            new Vector2Int(40, 4), new Vector2Int(70, 21),
            new Vector2Int(44, 5), new Vector2Int(65, 19)
        };
        ChunkSocketDirection[] directions = { ChunkSocketDirection.West, ChunkSocketDirection.East, ChunkSocketDirection.South, ChunkSocketDirection.North };
        Vector3[] positions = {
            new Vector3(40.5f, 5f, 0f), new Vector3(70.5f, 22f, 0f),
            new Vector3(44.5f, 6f, 0f), new Vector3(65.5f, 20f, 0f)
        };
        for (int i = 0; i < cells.Length; i++) AddSocket(root, directions[i], positions[i]);
        return cells;
    }

    private static Vector2Int[] AddCandidate1uSpawns(Transform root, Tilemap ground, Tilemap platforms, Vector2Int[] portals)
    {
        Vector2Int[] preferred = { new Vector2Int(12, 50), new Vector2Int(32, 50), new Vector2Int(52, 50), new Vector2Int(72, 50) };
        var result = new List<Vector2Int>();
        foreach (Vector2Int origin in preferred)
        {
            Vector2Int found = new Vector2Int(-1, -1);
            for (int radius = 0; radius < 60 && found.x < 0; radius++)
            for (int y = Mathf.Min(55, origin.y + radius); y >= Mathf.Max(1, origin.y - radius) && found.x < 0; y--)
            for (int x = Mathf.Max(1, origin.x - radius); x <= Mathf.Min(82, origin.x + radius); x++)
            {
                if (Mathf.Abs(x - origin.x) + Mathf.Abs(y - origin.y) != radius ||
                    !ground.HasTile(new Vector3Int(x - 1, y - 1, 0)) ||
                    !ground.HasTile(new Vector3Int(x, y - 1, 0)) ||
                    !ground.HasTile(new Vector3Int(x + 1, y - 1, 0))) continue;
                bool clear = true;
                for (int cx = x - 1; cx <= x + 2 && clear; cx++)
                for (int cy = y; cy <= y + 2; cy++)
                    if (ground.HasTile(new Vector3Int(cx, cy, 0)) || platforms.HasTile(new Vector3Int(cx, cy, 0))) clear = false;
                foreach (Vector2Int portal in portals) clear &= Vector2.Distance(new Vector2(x, y), portal) >= 7f;
                foreach (Vector2Int spawn in result) clear &= Vector2.Distance(new Vector2(x, y), spawn) >= 15f;
                if (clear) found = new Vector2Int(x, y);
            }
            if (found.x < 0) throw new InvalidDataException($"Candidate 1u has no valid SpawnZone near {origin}.");
            result.Add(found);
            AddGroundedSpawnMarker(root, ground, platforms, $"SpawnZone_{result.Count}", found.x, found.y, SpawnType.Monster);
        }
        return result.ToArray();
    }

    private static int CountCandidate1uSolid(bool[,] solid)
    {
        int count = 0;
        foreach (bool cell in solid) if (cell) count++;
        return count;
    }

    public static void RebuildStage1ExpansionModules()
    {
        const string tilesDir = "Assets/Textures/Environment/Tiles";
        Build12x12ModulePrefabs(
            "Assets/Prefabs/Modules",
            AssetDatabase.LoadAssetAtPath<Tile>($"{tilesDir}/Tile_Ground.asset"),
            AssetDatabase.LoadAssetAtPath<Tile>($"{tilesDir}/Tile_Platform.asset"),
            AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/TilemapDefaultMaterial.mat"),
            AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Textures/Environment/Sprite_SpikeTrap.png"),
            AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Textures/Environment/Sprite_SawBladeTrap.png"),
            new HashSet<string> {
                "Module_M1_LandmarkConnector", "Module_M2_LandmarkConnector",
                "Module_N1_VerticalReturnLoop", "Module_N2_VerticalReturnLoop",
                "Module_O1_SplitLevelCombatPocket", "Module_O2_SplitLevelCombatPocket"
            });
        AssetDatabase.SaveAssets();
    }

    public static void RebuildStage1LargeAuthoringRooms()
    {
        const string tilesDir = "Assets/Textures/Environment/Tiles";
        const string outputDir = "Assets/Prefabs/Rooms";
        if (!Directory.Exists(outputDir)) Directory.CreateDirectory(outputDir);
        string[] names = {
            "Room_11072", "Room_11073", "Room_11074", "Room_11075",
            "Room_11076", "Room_11077", "Room_11078", "Room_11079"
        };
        foreach (string name in names)
            BuildVariableRoomChunkPrefabs(
                outputDir,
                AssetDatabase.LoadAssetAtPath<Tile>($"{tilesDir}/Tile_Ground.asset"),
                AssetDatabase.LoadAssetAtPath<Tile>($"{tilesDir}/Tile_Platform.asset"),
                AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/TilemapDefaultMaterial.mat"),
                AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Textures/Environment/Sprite_SpikeTrap.png"),
                AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Textures/Environment/Sprite_SawBladeTrap.png"),
                name);
        AssetDatabase.SaveAssets();
    }

    public static void RebuildStage1RoomChunk(string chunkName)
    {
        const string tilesDir = "Assets/Textures/Environment/Tiles";
        BuildVariableRoomChunkPrefabs(
            "Assets/Prefabs/Rooms",
            AssetDatabase.LoadAssetAtPath<Tile>($"{tilesDir}/Tile_Ground.asset"),
            AssetDatabase.LoadAssetAtPath<Tile>($"{tilesDir}/Tile_Platform.asset"),
            AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/TilemapDefaultMaterial.mat"),
            AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Textures/Environment/Sprite_SpikeTrap.png"),
            AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Textures/Environment/Sprite_SawBladeTrap.png"),
            chunkName);
        AssetDatabase.SaveAssets();
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

    private static void Build12x12ModulePrefabs(string modulesDir, Tile groundTile, Tile platTile, Material mat, Sprite spikeSprite, Sprite sawSprite,
        ISet<string> onlyModules = null)
    {
        foreach (var kvp in ModuleTemplates)
        {
            string modName = kvp.Key;
            if (onlyModules != null && !onlyModules.Contains(modName)) continue;
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

    private static void BuildVariableRoomChunkPrefabs(string roomsDir, Tile groundTile, Tile platTile, Material mat, Sprite spikeSprite, Sprite sawSprite, string onlyChunkName = null, uint selectionSeed = 0)
    {
        LastNormalizeCount = 0;
        LastRejectCount = 0;
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
                    { "Module_K1_ReturnShaft", "Module_C2" },
                    { "Module_B3", "Module_C1" },
                    { "Module_A1", "Module_C1" }
                }
            },
            ["Room_11051"] = new ChunkGridConfig { // Descent Drop (2x3 12x12 Modules = 24m x 36m)
                GridWidth = 2, GridHeight = 3,
                Matrix = new string[,] {
                    { "Module_C2", "Module_K1_ReturnShaft" },
                    { "Module_F2", "Module_C1" },
                    { "Module_A1", "Module_C1" }
                }
            },
            ["Room_11052"] = new ChunkGridConfig { // Corridor East-West (4x2 12x12 Modules = 48m x 24m)
                GridWidth = 4, GridHeight = 2,
                Matrix = new string[,] {
                    { "Module_D2", "Module_J1_Connector", "Module_D1", "Module_C1" },
                    { "Module_A1", "Module_A2", "Module_B1", "Module_C1" }
                }
            },
            ["Room_11053"] = new ChunkGridConfig { // Elite Arena (4x2 12x12 Modules = 48m x 24m)
                GridWidth = 4, GridHeight = 2,
                Matrix = new string[,] {
                    { "Module_L1_CombatPocket", "Module_G2", "Module_E2", "Module_C1" },
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
            },
            ["Room_11072"] = LargeRoom(
                new[] { "Module_M1_LandmarkConnector", "Module_B2", "Module_O1_SplitLevelCombatPocket", "Module_D1", "Module_N1_VerticalReturnLoop", "Module_A2" },
                new[] { "Module_E1", "Module_J2", "Module_B1", "Module_L1_CombatPocket", "Module_F3", "Module_C1" },
                new[] { "Module_A3", "Module_G4", "Module_M2_LandmarkConnector", "Module_E4", "Module_O2_SplitLevelCombatPocket", "Module_D2" },
                new[] { "Module_K2", "Module_F1", "Module_A4", "Module_N2_VerticalReturnLoop", "Module_H2", "Module_C2" }),
            ["Room_11073"] = LargeRoom(
                new[] { "Module_O2_SplitLevelCombatPocket", "Module_D2", "Module_M1_LandmarkConnector", "Module_B4", "Module_N2_VerticalReturnLoop", "Module_A1" },
                new[] { "Module_F4", "Module_J3", "Module_E2", "Module_L2", "Module_B1", "Module_C2" },
                new[] { "Module_A4", "Module_G1", "Module_O1_SplitLevelCombatPocket", "Module_D3", "Module_M2_LandmarkConnector", "Module_E1" },
                new[] { "Module_K1", "Module_F2", "Module_A2", "Module_N1_VerticalReturnLoop", "Module_H3", "Module_C1" }),
            ["Room_11074"] = LargeRoom(
                new[] { "Module_N1_VerticalReturnLoop", "Module_B3", "Module_O2_SplitLevelCombatPocket", "Module_D4", "Module_M2_LandmarkConnector", "Module_A3" },
                new[] { "Module_E3", "Module_J4", "Module_B2", "Module_L1_CombatPocket", "Module_F1", "Module_C2" },
                new[] { "Module_A1", "Module_G2", "Module_M1_LandmarkConnector", "Module_E2", "Module_O1_SplitLevelCombatPocket", "Module_D1" },
                new[] { "Module_K2", "Module_F4", "Module_A4", "Module_N2_VerticalReturnLoop", "Module_H4", "Module_C1" }),
            ["Room_11075"] = LargeRoom(
                new[] { "Module_M2_LandmarkConnector", "Module_B4", "Module_N2_VerticalReturnLoop", "Module_D2", "Module_O1_SplitLevelCombatPocket", "Module_A2" },
                new[] { "Module_F3", "Module_J1", "Module_E4", "Module_L2", "Module_B3", "Module_C1" },
                new[] { "Module_A3", "Module_G4", "Module_O2_SplitLevelCombatPocket", "Module_D4", "Module_M1_LandmarkConnector", "Module_E1" },
                new[] { "Module_K1", "Module_F2", "Module_A1", "Module_N1_VerticalReturnLoop", "Module_H2", "Module_C2" }),
            ["Room_11076"] = LargeRoom(
                new[] { "Module_A1", "Module_M1_LandmarkConnector", "Module_A2", "Module_J2", "Module_A3", "Module_C1" },
                new[] { "Module_E1", "Module_F1", "Module_A4", "Module_M2_LandmarkConnector", "Module_E3", "Module_C2" },
                new[] { "Module_A2", "Module_G1", "Module_E2", "Module_J3", "Module_A1", "Module_D2" },
                new[] { "Module_K1", "Module_F2", "Module_A3", "Module_N1_VerticalReturnLoop", "Module_H1", "Module_C1" }),
            ["Room_11077"] = LargeRoom(
                new[] { "Module_M1_LandmarkConnector", "Module_D1", "Module_M2_LandmarkConnector", "Module_B1", "Module_J4", "Module_A1" },
                new[] { "Module_E2", "Module_F3", "Module_A4", "Module_L1_CombatPocket", "Module_G1", "Module_C2" },
                new[] { "Module_A3", "Module_J1", "Module_O1_SplitLevelCombatPocket", "Module_D3", "Module_N2_VerticalReturnLoop", "Module_E1" },
                new[] { "Module_K2", "Module_F1", "Module_A2", "Module_M2_LandmarkConnector", "Module_H3", "Module_C1" }),
            ["Room_11078"] = LargeRoom(
                new[] { "Module_N1_VerticalReturnLoop", "Module_B2", "Module_K2", "Module_D1", "Module_N2_VerticalReturnLoop", "Module_A1" },
                new[] { "Module_E3", "Module_J2", "Module_B4", "Module_L2", "Module_F1", "Module_C2" },
                new[] { "Module_A4", "Module_G2", "Module_M1_LandmarkConnector", "Module_E1", "Module_O2_SplitLevelCombatPocket", "Module_D2" },
                new[] { "Module_K1_ReturnShaft", "Module_F4", "Module_A2", "Module_N1_VerticalReturnLoop", "Module_H4", "Module_C1" }),
            ["Room_11079"] = LargeRoom(
                new[] { "Module_O1_SplitLevelCombatPocket", "Module_B3", "Module_M1_LandmarkConnector", "Module_D4", "Module_O2_SplitLevelCombatPocket", "Module_A3" },
                new[] { "Module_E4", "Module_J3", "Module_B1", "Module_L1_CombatPocket", "Module_F2", "Module_C1" },
                new[] { "Module_A1", "Module_G4", "Module_N2_VerticalReturnLoop", "Module_E2", "Module_M2_LandmarkConnector", "Module_D1" },
                new[] { "Module_K2", "Module_F3", "Module_A4", "Module_N1_VerticalReturnLoop", "Module_H2", "Module_C2" })
        };

        foreach (var kvp in chunkConfigs)
        {
            string chunkName = kvp.Key;
            if (onlyChunkName != null && chunkName != onlyChunkName) continue;
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
                    layout = SelectModuleTemplate(layout, selectionSeed, modY * nX + modX);

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
                for (int x = -halfW; x < halfW; x++)
                {
                    gMap.SetTile(new Vector3Int(x, -1, 0), groundTile);
                    gMap.SetTile(new Vector3Int(x, worldHeight, 0), groundTile);
                }
                // Left/Right Boundary Walls (West/East Passages)
                for (int y = 0; y <= worldHeight; y++)
                {
                    gMap.SetTile(new Vector3Int(-halfW, y, 0), groundTile);
                    gMap.SetTile(new Vector3Int(halfW - 1, y, 0), groundTile);
                }
            }

            if (RequiresP0TraversalCorridor(chunkName))
                EnsureP0TraversalCorridor(gMap, pMap, groundTile, worldWidth,
                    chunkName == "Room_11056" ? 6 : 3);
            // Camera Bounds
            GameObject cameraBounds = new GameObject("CameraBounds");
            cameraBounds.transform.SetParent(gridRoot.transform);
            cameraBounds.transform.localPosition = new Vector3(-0.5f, worldHeight * 0.5f, 0f);
            var box = cameraBounds.AddComponent<BoxCollider2D>();
            box.size = new Vector2(worldWidth, worldHeight);
            box.isTrigger = true;

            // Dynamic Sockets (West, East, South, North) - surface=1m 기준 center=2m, EntryMarker=1.51m(surface+0.51m)
            float[] socketXs = { -worldWidth * 0.375f, -worldWidth * 0.125f, worldWidth * 0.125f, worldWidth * 0.375f };
            socketXs[1] += 1f;
            socketXs[2] -= 1f;
            if (chunkName == "Room_11053") socketXs[3] = 10f;
            int[] socketSurfaceCells = GetSocketSurfaceCells(chunkName);
            ChunkSocketDirection[] socketDirections =
            {
                ChunkSocketDirection.West, ChunkSocketDirection.North,
                ChunkSocketDirection.South, ChunkSocketDirection.East
            };
            for (int i = 0; i < socketDirections.Length; i++)
                AddReachableSocket(gridRoot.transform, gMap, pMap, groundTile, platTile,
                    socketDirections[i], socketXs[i], socketSurfaceCells[i]);
            if (chunkName == "Room_11053")
            {
                for (int step = 0; step < 3; step++)
                    for (int x = 11 + step * 3; x <= 13 + step * 3; x++)
                        pMap.SetTile(new Vector3Int(x, 1 + step, 0), platTile);
                for (int x = 15; x <= 20; x++)
                    pMap.SetTile(new Vector3Int(x, 6, 0), platTile);
            }
            if (chunkName == "Prefab_1042")
                for (int x = -7; x <= -5; x++) gMap.SetTile(new Vector3Int(x, -1, 0), groundTile);
            if (chunkName == "Room_11053")
            {
                for (int x = -6; x <= -4; x++) gMap.SetTile(new Vector3Int(x, -1, 0), groundTile);
                for (int x = 4; x <= 6; x++) gMap.SetTile(new Vector3Int(x, -1, 0), groundTile);
            }
            if (chunkName == "Prefab_1040")
                for (int x = 10; x <= 12; x++)
                    pMap.SetTile(new Vector3Int(x, 2, 0), null);
            if (chunkName == "Prefab_1040" || chunkName == "Prefab_1041" || chunkName == "Room_11052")
                for (int x = Mathf.RoundToInt(socketXs[0]) - 1; x <= Mathf.RoundToInt(socketXs[3]) + 1; x++)
                    gMap.SetTile(new Vector3Int(x, -1, 0), groundTile);
            if (chunkName == "Prefab_1041")
            {
                pMap.SetTile(new Vector3Int(-9, 4, 0), platTile);
                pMap.SetTile(new Vector3Int(16, 6, 0), null);
                pMap.SetTile(new Vector3Int(-14, 15, 0), null);
            }
            if (chunkName == "Room_11063")
                for (int x = -6; x <= -4; x++) gMap.SetTile(new Vector3Int(x, -1, 0), groundTile);
            if (chunkName == "Room_11050")
            {
                for (int x = -3; x <= -1; x++) gMap.SetTile(new Vector3Int(x, -1, 0), groundTile);
            }
            if (chunkName == "Prefab_1042") pMap.SetTile(new Vector3Int(-30, 19, 0), null);
            if (chunkName == "Room_11051") pMap.SetTile(new Vector3Int(-12, 19, 0), null);
            if (chunkName == "Room_11053") pMap.SetTile(new Vector3Int(6, 2, 0), null);
            if (chunkName == "Room_11057") pMap.SetTile(new Vector3Int(-18, 7, 0), null);
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
                AddCombatSpawnZones(gridRoot.transform, gMap, pMap, platTile, worldWidth, worldHeight);
            if (IsLargeCombatRoom(chunkName))
                AddCombatSpawnZones(gridRoot.transform, gMap, pMap, platTile, worldWidth, worldHeight);
            ApplyStage1ShortRunFixes(chunkName, gMap, pMap, groundTile, platTile);

            string prefabPath = $"{roomsDir}/{chunkName}.prefab";
            if (!ValidateGeneratedRoom(gridRoot, gMap, pMap, worldWidth, worldHeight, out string rejectReason))
            {
                LastRejectCount++;
                Debug.LogError($"[ModuleChunkBuilder] Rejected {chunkName}; preserved previous prefab: {rejectReason}");
                Object.DestroyImmediate(gridRoot);
                continue;
            }
            PrefabUtility.SaveAsPrefabAsset(gridRoot, prefabPath);
            Object.DestroyImmediate(gridRoot);
        }
        Debug.Log($"<color=green>[ModuleChunkBuilder] 12x12 가변 NxM 룸 청크 11종 재빌드 완료!</color>");
    }

    private static ChunkGridConfig LargeRoom(params string[][] rows)
    {
        var matrix = new string[4, 6];
        for (int y = 0; y < 4; y++)
            for (int x = 0; x < 6; x++) matrix[y, x] = rows[y][x];
        return new ChunkGridConfig { GridWidth = 6, GridHeight = 4, Matrix = matrix };
    }

    private static void ApplyStage1ShortRunFixes(string chunkName, Tilemap ground, Tilemap platforms,
        Tile groundTile, Tile platformTile)
    {
        void SetRun(int y, int fromX, int toX)
        {
            for (int x = fromX; x <= toX; x++)
                platforms.SetTile(new Vector3Int(x, y, 0), platformTile);
        }

        void ClearRun(int y, int fromX, int toX)
        {
            for (int x = fromX; x <= toX; x++)
                platforms.SetTile(new Vector3Int(x, y, 0), null);
        }

        void SetSolidRun(int y, int fromX, int toX)
        {
            ClearRun(y, fromX, toX);
            for (int x = fromX; x <= toX; x++)
                ground.SetTile(new Vector3Int(x, y, 0), groundTile);
        }

        switch (chunkName)
        {
            case "Room_11050": platforms.SetTile(new Vector3Int(4, 1, 0), null); break;
            case "Room_11051": platforms.SetTile(new Vector3Int(0, 2, 0), null); break;
            case "Room_11052": SetRun(5, -9, -7); break;
            case "Room_11053":
                platforms.SetTile(new Vector3Int(3, 1, 0), null);
                platforms.SetTile(new Vector3Int(7, 2, 0), null);
                platforms.SetTile(new Vector3Int(8, 2, 0), null);
                SetRun(5, 0, 2);
                SetRun(7, 3, 5);
                SetRun(9, 5, 7);
                SetRun(11, 9, 11);
                break;
            case "Room_11056":
                SetSolidRun(1, -9, -3);
                SetSolidRun(1, 0, 2);
                ClearRun(1, 10, 12);
                SetSolidRun(2, -8, -6);
                SetSolidRun(2, -2, 0);
                break;
            case "Room_11063":
                SetRun(5, -12, -10);
                SetRun(7, -9, -7);
                SetRun(9, -7, -5);
                SetRun(11, -3, -1);
                break;
        }
    }

    private static void AddReachableSocket(Transform parent, Tilemap ground, Tilemap platforms,
        Tile groundTile, Tile platformTile, ChunkSocketDirection direction, float desiredX, int surfaceCellY)
    {
        int centerX = Mathf.Clamp(Mathf.RoundToInt(desiredX), ground.cellBounds.xMin + 2, ground.cellBounds.xMax - 3);

        for (int x = centerX - 1; x <= centerX + 1; x++)
        {
            ground.SetTile(new Vector3Int(x, surfaceCellY, 0), groundTile);
            ground.SetTile(new Vector3Int(x, surfaceCellY - 1, 0), groundTile);
            for (int y = surfaceCellY - 2; y >= 0 && !ground.HasTile(new Vector3Int(x, y, 0)); y--)
            {
                ground.SetTile(new Vector3Int(x, y, 0), groundTile);
                platforms.SetTile(new Vector3Int(x, y, 0), null);
            }
            platforms.SetTile(new Vector3Int(x, surfaceCellY, 0), null);
            platforms.SetTile(new Vector3Int(x, surfaceCellY - 1, 0), null);
            ground.SetTile(new Vector3Int(x, surfaceCellY + 1, 0), null);
            ground.SetTile(new Vector3Int(x, surfaceCellY + 2, 0), null);
            ground.SetTile(new Vector3Int(x, surfaceCellY + 3, 0), null);
            platforms.SetTile(new Vector3Int(x, surfaceCellY + 1, 0), null);
            platforms.SetTile(new Vector3Int(x, surfaceCellY + 2, 0), null);
            platforms.SetTile(new Vector3Int(x, surfaceCellY + 3, 0), null);
        }

        int towardCenter = centerX < 0 ? 1 : -1;
        for (int approachSide = -1; approachSide <= 1; approachSide += 2)
        {
            for (int level = surfaceCellY - 1, step = 1; level > 0; level--, step++)
            {
                int stepCenterX = centerX + approachSide * step * 3;
                for (int x = stepCenterX - 1; x <= stepCenterX + 1; x++)
                {
                    Vector3Int support = new Vector3Int(x, level, 0);
                    if (ground.HasTile(support) || ground.HasTile(support + Vector3Int.down)) continue;
                    platforms.SetTile(support, platformTile);
                    ground.SetTile(support + Vector3Int.up, null);
                    ground.SetTile(support + Vector3Int.up * 2, null);
                    ground.SetTile(support + Vector3Int.up * 3, null);
                    platforms.SetTile(support + Vector3Int.up, null);
                    platforms.SetTile(support + Vector3Int.up * 2, null);
                    platforms.SetTile(support + Vector3Int.up * 3, null);
                }
            }
        }
        RemoveShortAdjacentOneWayRun(platforms, surfaceCellY, centerX - 2, -1);
        RemoveShortAdjacentOneWayRun(platforms, surfaceCellY, centerX + 2, 1);

        float socketX = direction == ChunkSocketDirection.North
            ? centerX - 0.5f
            : centerX + 0.5f + towardCenter;
        AddSocket(parent, direction, new Vector3(socketX, surfaceCellY + 2f, 0f));
    }

    private static void RemoveShortAdjacentOneWayRun(Tilemap platforms, int y, int startX, int direction)
    {
        var cells = new List<Vector3Int>();
        for (int x = startX; platforms.HasTile(new Vector3Int(x, y, 0)); x += direction)
            cells.Add(new Vector3Int(x, y, 0));
        if (cells.Count >= 3) return;
        foreach (Vector3Int cell in cells) platforms.SetTile(cell, null);
    }

    private static int[] GetSocketSurfaceCells(string chunkName)
    {
        switch (chunkName)
        {
            case "Prefab_1040": return new[] { 0, 2, 1, 3 }; // G2
            case "Room_11061": return new[] { 1, 0, 1, 0 }; // G0
            case "Prefab_1041": return new[] { 0, 2, 1, 3 }; // G2
            case "Room_11052": return new[] { 0, 2, 1, 2 }; // G1
            case "Room_11063": return new[] { 1, 2, 0, 2 }; // G1
            case "Room_11050": return new[] { 0, 2, 1, 3 }; // G2
            case "Room_11056": return new[] { 1, 3, 0, 2 }; // G2
            case "Room_11051":
            case "Room_11057": return new[] { 0, 3, 1, 2 }; // G2
            default: return new[] { 0, 2, 1, 3 }; // G2: Prefab_1042, Room_11053
        }
    }

    private static bool RequiresP0TraversalCorridor(string chunkName)
    {
        if (IsLargeRuntimeRoom(chunkName)) return true;
        return chunkName == "Prefab_1041" || chunkName == "Prefab_1042" ||
            chunkName == "Room_11050" || chunkName == "Room_11051" ||
            chunkName == "Room_11052" || chunkName == "Room_11053" ||
            chunkName == "Room_11056" || chunkName == "Room_11057";
    }

    private static bool IsLargeRuntimeRoom(string name) =>
        name == "Room_11072" || name == "Room_11073" || name == "Room_11074" || name == "Room_11075" ||
        name == "Room_11076" || name == "Room_11077" || name == "Room_11078" || name == "Room_11079";

    private static bool IsLargeCombatRoom(string name) =>
        name == "Room_11072" || name == "Room_11073" || name == "Room_11074" || name == "Room_11075" ||
        name == "Room_11079";

    private static void EnsureP0TraversalCorridor(Tilemap ground, Tilemap platforms, Tile groundTile, int worldWidth, int clearHeight)
    {
        int left = Mathf.RoundToInt(-worldWidth * 0.375f) - 1;
        int right = Mathf.RoundToInt(worldWidth * 0.375f) + 1;
        for (int x = left; x <= right; x++)
        {
            ground.SetTile(new Vector3Int(x, -1, 0), groundTile);
            ground.SetTile(new Vector3Int(x, 0, 0), groundTile);
            for (int y = 1; y <= clearHeight; y++)
            {
                ground.SetTile(new Vector3Int(x, y, 0), null);
                platforms.SetTile(new Vector3Int(x, y, 0), null);
            }
        }
    }

    private static bool ValidateGeneratedRoom(GameObject room, Tilemap ground, Tilemap platforms,
        int worldWidth, int worldHeight, out string reason)
    {
        ChunkSocketMarker[] sockets = room.GetComponentsInChildren<ChunkSocketMarker>(true);
        if (sockets.Length != 4 || System.Array.Exists(sockets, socket => socket.EntryMarker == null))
        {
            reason = "socket/EntryMarker contract";
            return false;
        }

        foreach (Vector3Int cell in platforms.cellBounds.allPositionsWithin)
            if (platforms.HasTile(cell) && ground.HasTile(cell))
            {
                reason = $"Ground/OneWay overlap {cell}";
                return false;
            }

        foreach (ChunkSocketMarker socket in sockets)
        {
            int surfaceY = Mathf.RoundToInt(socket.transform.localPosition.y - 2f);
            int towardCenter = socket.transform.localPosition.x < 0f ? 1 : -1;
            int centerX = socket.Direction == ChunkSocketDirection.North
                ? Mathf.RoundToInt(socket.transform.localPosition.x + 0.5f)
                : Mathf.RoundToInt(socket.transform.localPosition.x - 0.5f - towardCenter);
            for (int x = centerX - 1; x <= centerX + 1; x++)
            {
                Vector3Int landing = new Vector3Int(x, surfaceY, 0);
                if (!ground.HasTile(landing) && !platforms.HasTile(landing))
                {
                    reason = $"{socket.Direction} landing gap {landing}";
                    return false;
                }
                for (int y = surfaceY + 1; y <= surfaceY + 3; y++)
                    if (ground.HasTile(new Vector3Int(x, y, 0)) || platforms.HasTile(new Vector3Int(x, y, 0)))
                    {
                        reason = $"{socket.Direction} 3x3 obstruction ({x},{y}) ground={ground.HasTile(new Vector3Int(x, y, 0))} oneWay={platforms.HasTile(new Vector3Int(x, y, 0))}";
                        return false;
                    }
            }
        }

        BoxCollider2D cameraBounds = room.transform.Find("CameraBounds")?.GetComponent<BoxCollider2D>();
        if (cameraBounds == null || cameraBounds.size != new Vector2(worldWidth, worldHeight))
        {
            reason = "CameraBounds contract";
            return false;
        }
        reason = null;
        return true;
    }

    private static void AddCombatSpawnZones(Transform parent, Tilemap ground, Tilemap platforms,
        Tile platformTile, int worldWidth, int worldHeight)
    {
        Vector3[] positions = worldWidth < 30
            ? new[] { new Vector3(-8f, 16f), new Vector3(8f, 16f), new Vector3(0f, 30f) }
            : new[] { new Vector3(-18f, 16f), new Vector3(0f, 16f), new Vector3(18f, 16f) };
        for (int i = 0; i < positions.Length; i++)
        {
            float surfaceY = Mathf.Min(positions[i].y, worldHeight - 4f);
            int centerX = Mathf.RoundToInt(positions[i].x);
            int supportY = Mathf.RoundToInt(surfaceY) - 1;
            for (int x = centerX - 1; x <= centerX + 1; x++)
            {
                ground.SetTile(new Vector3Int(x, supportY, 0), null);
                platforms.SetTile(new Vector3Int(x, supportY, 0), platformTile);
                ground.SetTile(new Vector3Int(x, supportY + 1, 0), null);
                ground.SetTile(new Vector3Int(x, supportY + 2, 0), null);
                platforms.SetTile(new Vector3Int(x, supportY + 1, 0), null);
                platforms.SetTile(new Vector3Int(x, supportY + 2, 0), null);
            }
            AddGroundedSpawnMarker(parent, ground, platforms, $"SpawnZone_{i + 1}",
                positions[i].x, surfaceY, SpawnType.Monster);
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
