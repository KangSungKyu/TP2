#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using System.IO;
using System.Collections.Generic;

/// <summary>
/// 2D 사이드뷰 스테이지 배경, 지형 타일 및 구조물 5종 에셋 슬라이싱(Pivot Center 0.5, 0.5), 
/// PhysicsMaterial2D 세팅, Addressables Tilemaps / RoomPrefabs 라벨 등록 & 배포 파이프라인 유틸리티.
/// </summary>
public static class StageResourcePipeline
{
    [MenuItem("TP2/Build Stage Environment Resources (스테이지 타일 & 구조물 일괄 가공)")]
    public static void BuildStageEnvironmentResources()
    {
        Debug.Log("<color=cyan><b>[StageResourcePipeline] 2D 스테이지 타일 & 구조물 5종 슬라이싱 및 세팅 시작...</b></color>");

        string envTexDir = "Assets/Textures/Environment";
        if (!Directory.Exists(envTexDir))
        {
            Directory.CreateDirectory(envTexDir);
            AssetDatabase.Refresh();
        }

        // 1. 5종 타일 및 구조물 임포트 & 슬라이싱 명세 (Pivot Center 0.5, 0.5)
        var envSpecs = new List<(string file, int cellW, int cellH, int ppu)>()
        {
            ("Tile_Terrain_Ground.png", 32, 32, 32),
            ("Tile_Platform_OneWay.png", 32, 32, 32),
            ("Tile_Hazard_SpikesLava.png", 32, 32, 32),
            ("Tile_Background_Deco.png", 32, 32, 32),
            ("Sprite_Structures_Interactive.png", 64, 64, 64)
        };

        foreach (var spec in envSpecs)
        {
            string texPath = $"{envTexDir}/{spec.file}";
            if (File.Exists(texPath))
            {
                TextureImporter importer = AssetImporter.GetAtPath(texPath) as TextureImporter;
                if (importer != null)
                {
                    importer.textureType = TextureImporterType.Sprite;
                    importer.spriteImportMode = SpriteImportMode.Multiple;
                    importer.filterMode = FilterMode.Point;
                    importer.spritePixelsPerUnit = spec.ppu;

                    Texture2D tex = AssetDatabase.LoadAssetAtPath<Texture2D>(texPath);
                    if (tex != null)
                    {
                        int cols = tex.width / spec.cellW;
                        int rows = tex.height / spec.cellH;
                        int index = 0;

                        List<SpriteMetaData> metaList = new List<SpriteMetaData>();
                        string baseName = Path.GetFileNameWithoutExtension(spec.file);

                        for (int r = 0; r < rows; r++)
                        {
                            for (int c = 0; c < cols; c++)
                            {
                                SpriteMetaData meta = new SpriteMetaData();
                                meta.name = $"{baseName}_{index++}";
                                meta.rect = new Rect(c * spec.cellW, (rows - 1 - r) * spec.cellH, spec.cellW, spec.cellH);
                                meta.alignment = (int)SpriteAlignment.Center;
                                meta.pivot = new Vector2(0.5f, 0.5f);
                                metaList.Add(meta);
                            }
                        }

                        importer.spritesheet = metaList.ToArray();
                    }

                    importer.SaveAndReimport();
                    Debug.Log($"Sliced Stage Resource: {texPath}");
                }
            }
        }

        // 2. PhysicsMaterial2D 세팅 (Ground: Friction 0.4f, Wall: Friction 0.0f)
        string matDir = "Assets/Materials/Physics";
        if (!Directory.Exists(matDir))
        {
            Directory.CreateDirectory(matDir);
            AssetDatabase.Refresh();
        }

        PhysicsMaterial2D groundMat = AssetDatabase.LoadAssetAtPath<PhysicsMaterial2D>($"{matDir}/GroundPhysicsMaterial.physicsMaterial2D");
        if (groundMat == null)
        {
            groundMat = new PhysicsMaterial2D("GroundPhysicsMaterial") { friction = 0.4f, bounciness = 0.0f };
            AssetDatabase.CreateAsset(groundMat, $"{matDir}/GroundPhysicsMaterial.physicsMaterial2D");
        }

        PhysicsMaterial2D wallMat = AssetDatabase.LoadAssetAtPath<PhysicsMaterial2D>($"{matDir}/WallPhysicsMaterial.physicsMaterial2D");
        if (wallMat == null)
        {
            wallMat = new PhysicsMaterial2D("WallPhysicsMaterial") { friction = 0.0f, bounciness = 0.0f };
            AssetDatabase.CreateAsset(wallMat, $"{matDir}/WallPhysicsMaterial.physicsMaterial2D");
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        // 3. Addressables 등록 & 로컬 배포
        AddressablePipeline.BuildAndDeploy();

        Debug.Log("<color=green><b>[StageResourcePipeline] 2D 스테이지 타일 & 구조물 5종 슬라이싱, PhysicsMaterial2D & 배포 완결!</b></color>");
    }
}
#endif
