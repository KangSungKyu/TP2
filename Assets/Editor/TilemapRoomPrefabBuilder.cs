#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEngine.Tilemaps;
using System.IO;

/// <summary>
/// Unity 2D Tilemap 기반 룸 청크 프리팹 (Tilemap_Room_TestDummy.prefab) 제작 및 Addressables 배포 유틸리티.
/// Root Grid 하위 Tilemap_Background, Tilemap_Ground (CompositeCollider2D), Tilemap_Platforms (PlatformEffector2D) 레이어 구축.
/// </summary>
public static class TilemapRoomPrefabBuilder
{
    [MenuItem("TP2/Build Tilemap_Room_TestDummy Prefab (Tilemap 룸 청크 제작)")]
    public static void BuildTilemapRoomPrefab()
    {
        Debug.Log("<color=cyan><b>[TilemapRoomPrefabBuilder] Tilemap_Room_TestDummy.prefab 제작 시작...</b></color>");

        string roomsDir = "Assets/Prefabs/Rooms";
        if (!Directory.Exists(roomsDir))
        {
            Directory.CreateDirectory(roomsDir);
            AssetDatabase.Refresh();
        }

        string prefabPath = $"{roomsDir}/Tilemap_Room_TestDummy.prefab";

        // 1. Root Grid GameObject
        GameObject gridRoot = new GameObject("Tilemap_Room_TestDummy");
        var gridComp = gridRoot.AddComponent<Grid>();
        gridComp.cellSize = new Vector3(1, 1, 0);

        // 2. Tilemap_Background (No Collider)
        GameObject bgObj = new GameObject("Tilemap_Background");
        bgObj.transform.SetParent(gridRoot.transform);
        var bgTilemap = bgObj.AddComponent<Tilemap>();
        var bgRenderer = bgObj.AddComponent<TilemapRenderer>();
        bgRenderer.sortingOrder = -10;

        // 3. Tilemap_Ground (CompositeCollider2D + Rigidbody2D Static)
        GameObject groundObj = new GameObject("Tilemap_Ground");
        groundObj.transform.SetParent(gridRoot.transform);
        var groundTilemap = groundObj.AddComponent<Tilemap>();
        var groundRenderer = groundObj.AddComponent<TilemapRenderer>();
        groundRenderer.sortingOrder = 0;

        var groundCol = groundObj.AddComponent<TilemapCollider2D>();
        groundCol.usedByComposite = true;

        var rb2d = groundObj.AddComponent<Rigidbody2D>();
        rb2d.bodyType = RigidbodyType2D.Static;

        var compositeCol = groundObj.AddComponent<CompositeCollider2D>();
        compositeCol.geometryType = CompositeCollider2D.GeometryType.Polygons;

        PhysicsMaterial2D groundMat = AssetDatabase.LoadAssetAtPath<PhysicsMaterial2D>("Assets/Materials/Physics/GroundPhysicsMaterial.physicsMaterial2D");
        if (groundMat != null) compositeCol.sharedMaterial = groundMat;

        // 4. Tilemap_Platforms (PlatformEffector2D + OneWayPlatformPassThrough)
        GameObject platObj = new GameObject("Tilemap_Platforms");
        platObj.transform.SetParent(gridRoot.transform);
        var platTilemap = platObj.AddComponent<Tilemap>();
        var platRenderer = platObj.AddComponent<TilemapRenderer>();
        platRenderer.sortingOrder = 5;

        var platCol = platObj.AddComponent<TilemapCollider2D>();
        platCol.usedByEffector = true;

        var effector = platObj.AddComponent<PlatformEffector2D>();
        effector.surfaceArc = 180f;
        effector.useOneWay = true;

        System.Type passThroughType = System.Type.GetType("OneWayPlatformPassThrough, Assembly-CSharp");
        if (passThroughType != null)
        {
            platObj.AddComponent(passThroughType);
        }

        // Prefab 세이브
        PrefabUtility.SaveAsPrefabAsset(gridRoot, prefabPath);
        Object.DestroyImmediate(gridRoot);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log($"<color=green>Created Tilemap Room Chunk Prefab: {prefabPath}</color>");

        // Addressables 등록 및 배포
        AddressablePipeline.BuildAndDeploy();

        Debug.Log("<color=green><b>[TilemapRoomPrefabBuilder] Tilemap_Room_TestDummy.prefab 제작 및 Addressables 배포 완결!</b></color>");
    }
}
#endif
