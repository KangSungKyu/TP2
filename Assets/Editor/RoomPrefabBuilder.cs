#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using System.IO;

/// <summary>
/// 대문자 P 표준 경로(Assets/Prefabs/Rooms/Room_TestDummy.prefab)에 룸 청크 프리팹을 제작하고 
/// Addressables "Room_TestDummy" Key로 빌드 및 배포하는 유틸리티 (Global_Light2D 제거 정돈 버전).
/// </summary>
public static class RoomPrefabBuilder
{
    [MenuItem("TP2/Build Room_TestDummy Chunk Prefab (Room_TestDummy 청크 프리팹 제작)")]
    public static void BuildRoomTestDummyPrefab()
    {
        Debug.Log("<color=cyan><b>[RoomPrefabBuilder] Assets/Prefabs/Rooms/Room_TestDummy.prefab (Global_Light2D 제거) 제작 시작...</b></color>");

        string roomsDir = "Assets/Prefabs/Rooms";
        if (!Directory.Exists(roomsDir))
        {
            Directory.CreateDirectory(roomsDir);
            AssetDatabase.Refresh();
        }

        string prefabPath = $"{roomsDir}/Room_TestDummy.prefab";

        GameObject roomRoot = new GameObject("Room_TestDummy");

        PhysicsMaterial2D groundMat = AssetDatabase.LoadAssetAtPath<PhysicsMaterial2D>("Assets/Materials/Physics/GroundPhysicsMaterial.physicsMaterial2D");
        PhysicsMaterial2D wallMat = AssetDatabase.LoadAssetAtPath<PhysicsMaterial2D>("Assets/Materials/Physics/WallPhysicsMaterial.physicsMaterial2D");

        // Ground_Base
        GameObject groundObj = new GameObject("Ground_Base");
        groundObj.transform.SetParent(roomRoot.transform);
        groundObj.transform.localPosition = new Vector3(0, -0.5f, 0);
        var gSr = groundObj.AddComponent<SpriteRenderer>();
        gSr.sprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Textures/Environment/Tile_Terrain_Ground.png");
        gSr.drawMode = SpriteDrawMode.Tiled;
        gSr.size = new Vector2(30, 1);
        var gCol = groundObj.AddComponent<BoxCollider2D>();
        gCol.size = new Vector2(30, 1);
        if (groundMat != null) gCol.sharedMaterial = groundMat;

        // Walls
        GameObject wLeft = new GameObject("Wall_Left");
        wLeft.transform.SetParent(roomRoot.transform);
        wLeft.transform.localPosition = new Vector3(-15, 9, 0);
        var wlCol = wLeft.AddComponent<BoxCollider2D>();
        wlCol.size = new Vector2(1, 18);
        if (wallMat != null) wlCol.sharedMaterial = wallMat;

        GameObject wRight = new GameObject("Wall_Right");
        wRight.transform.SetParent(roomRoot.transform);
        wRight.transform.localPosition = new Vector3(15, 9, 0);
        var wrCol = wRight.AddComponent<BoxCollider2D>();
        wrCol.size = new Vector2(1, 18);
        if (wallMat != null) wrCol.sharedMaterial = wallMat;

        // Platforms
        Vector3[] platPositions = new Vector3[] { new Vector3(-5, 2.5f, 0), new Vector3(0, 5.0f, 0), new Vector3(5, 7.5f, 0) };
        string[] platNames = new string[] { "Platform_Low", "Platform_Mid", "Platform_High" };
        Sprite platSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Textures/Environment/Tile_Platform_OneWay.png");

        for (int i = 0; i < platPositions.Length; i++)
        {
            GameObject pObj = new GameObject(platNames[i]);
            pObj.transform.SetParent(roomRoot.transform);
            pObj.transform.localPosition = platPositions[i];

            var pSr = pObj.AddComponent<SpriteRenderer>();
            if (platSprite != null) pSr.sprite = platSprite;
            pSr.drawMode = SpriteDrawMode.Tiled;
            pSr.size = new Vector2(4, 0.4f);

            var pCol = pObj.AddComponent<BoxCollider2D>();
            pCol.size = new Vector2(4, 0.4f);
            pCol.usedByEffector = true;

            var effector = pObj.AddComponent<PlatformEffector2D>();
            effector.surfaceArc = 180f;
            effector.useOneWay = true;

            System.Type passThroughType = System.Type.GetType("OneWayPlatformPassThrough, Assembly-CSharp");
            if (passThroughType != null)
            {
                pObj.AddComponent(passThroughType);
            }
        }

        // Hazard
        GameObject hazardObj = new GameObject("Hazard_Spikes");
        hazardObj.transform.SetParent(roomRoot.transform);
        hazardObj.transform.localPosition = new Vector3(10, 0.2f, 0);
        var hSr = hazardObj.AddComponent<SpriteRenderer>();
        hSr.sprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Textures/Environment/Tile_Hazard_SpikesLava.png");
        hSr.drawMode = SpriteDrawMode.Tiled;
        hSr.size = new Vector2(5, 0.4f);
        var hCol = hazardObj.AddComponent<BoxCollider2D>();
        hCol.size = new Vector2(5, 0.4f);
        hCol.isTrigger = true;

        // Door & Chest
        Sprite structSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Textures/Environment/Sprite_Structures_Interactive.png");

        GameObject doorObj = new GameObject("Door_Exit");
        doorObj.transform.SetParent(roomRoot.transform);
        doorObj.transform.localPosition = new Vector3(12, 1.2f, 0);
        var dSr = doorObj.AddComponent<SpriteRenderer>();
        if (structSprite != null) dSr.sprite = structSprite;

        GameObject chestObj = new GameObject("Chest_Treasure");
        chestObj.transform.SetParent(roomRoot.transform);
        chestObj.transform.localPosition = new Vector3(5, 8.1f, 0);
        var cSr = chestObj.AddComponent<SpriteRenderer>();
        if (structSprite != null) cSr.sprite = structSprite;

        PrefabUtility.SaveAsPrefabAsset(roomRoot, prefabPath);
        Object.DestroyImmediate(roomRoot);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        AddressablePipeline.BuildAndDeploy();
        Debug.Log("<color=green><b>[RoomPrefabBuilder] Global_Light2D가 완전히 제거된 Room_TestDummy.prefab 배포 완결!</b></color>");
    }
}
#endif
