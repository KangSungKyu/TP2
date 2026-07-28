#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Unity 6 C# API (PrefabUtility) 기반 Room_TestDummy.prefab 정밀 생성기.
/// YAML 파싱 에러(Type Mismatch)를 100% 원천 차단하고 무결점 공식 유니티 프리팹을 정식 제작합니다.
/// </summary>
public static class RoomChunkPrefabGenerator
{
    [MenuItem("TP2/Generate Room_TestDummy Prefab (무결점 룸 청크 프리팹 재생성)")]
    public static void GenerateRoomChunkPrefab()
    {
        string targetDir = "Assets/Prefabs/Rooms";
        if (!Directory.Exists(targetDir))
        {
            Directory.CreateDirectory(targetDir);
        }

        string prefabPath = Path.Combine(targetDir, "Room_TestDummy.prefab");

        // 1. Root GameObject
        GameObject rootObj = new GameObject("Room_TestDummy");

        // 2. Ground Base
        GameObject groundObj = createChildObject("Ground_Base", rootObj.transform, new Vector3(0f, -0.5f, 0f));
        var groundCol = groundObj.AddComponent<BoxCollider2D>();
        groundCol.size = new Vector2(30f, 1f);
        groundCol.sharedMaterial = createPhysicsMaterial("Mat_Ground", 0.4f);
        var groundRend = groundObj.AddComponent<SpriteRenderer>();
        bindSprite(groundRend, "Assets/Textures/Environment/Tile_Terrain_Ground.png", new Color(0.25f, 0.28f, 0.32f, 1f));

        // 3. Walls (Friction 0)
        GameObject leftWall = createChildObject("Wall_Left", rootObj.transform, new Vector3(-15f, 9f, 0f));
        var leftCol = leftWall.AddComponent<BoxCollider2D>();
        leftCol.size = new Vector2(1f, 18f);
        leftCol.sharedMaterial = createPhysicsMaterial("Mat_Wall", 0f);

        GameObject rightWall = createChildObject("Wall_Right", rootObj.transform, new Vector3(15f, 9f, 0f));
        var rightCol = rightWall.AddComponent<BoxCollider2D>();
        rightCol.size = new Vector2(1f, 18f);
        rightCol.sharedMaterial = leftCol.sharedMaterial;

        // 4. Step Platforms (Low, Mid, High)
        createPlatform(rootObj.transform, "Platform_Low", new Vector3(-5f, 2.5f, 0f), new Vector2(4f, 0.4f));
        createPlatform(rootObj.transform, "Platform_Mid", new Vector3(0f, 5f, 0f), new Vector2(4f, 0.4f));
        createPlatform(rootObj.transform, "Platform_High", new Vector3(5f, 7.5f, 0f), new Vector2(4f, 0.4f));

        // 5. Hazard Spikes
        GameObject hazardObj = createChildObject("Hazard_Spikes", rootObj.transform, new Vector3(10f, 0.2f, 0f));
        var hazardCol = hazardObj.AddComponent<BoxCollider2D>();
        hazardCol.size = new Vector2(5f, 0.4f);
        hazardCol.isTrigger = true;
        var hazardRend = hazardObj.AddComponent<SpriteRenderer>();
        bindSprite(hazardRend, "Assets/Textures/Environment/Tile_Hazard_SpikesLava.png", new Color(0.9f, 0.2f, 0.2f, 0.8f));

        // 6. Interactive Structures (Door & Chest)
        GameObject doorObj = createChildObject("Door_Exit", rootObj.transform, new Vector3(12f, 1.2f, 0f));
        var doorRend = doorObj.AddComponent<SpriteRenderer>();
        bindSprite(doorRend, "Assets/Textures/Environment/Sprite_Structures_Interactive.png", new Color(0.6f, 0.3f, 0.8f, 1f));

        GameObject chestObj = createChildObject("Chest_Treasure", rootObj.transform, new Vector3(5f, 8.1f, 0f));
        var chestRend = chestObj.AddComponent<SpriteRenderer>();
        bindSprite(chestRend, "Assets/Textures/Environment/Sprite_Structures_Interactive.png", new Color(0.9f, 0.8f, 0.2f, 1f));

        // 7. Unity PrefabUtility C# API로 무결점 실물 프리팹 에셋 저장
        PrefabUtility.SaveAsPrefabAsset(rootObj, prefabPath);
        Object.DestroyImmediate(rootObj);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        // 9. Addressables 파이프라인 자동 구동 (Room_TestDummy Key 자동 체크 및 등록)
        AddressablePipeline.RegisterAllAddressables();

        Debug.Log($"<color=green><b>[RoomChunkPrefabGenerator] '{prefabPath}' 무결점 룸 청크 프리팹 C# API 재생성 & Addressables 자동 체크 완결!</b></color>");
    }

    private static GameObject createChildObject(string name, Transform parent, Vector3 pos)
    {
        GameObject obj = new GameObject(name);
        obj.transform.SetParent(parent);
        obj.transform.position = pos;
        return obj;
    }

    private static PhysicsMaterial2D createPhysicsMaterial(string name, float friction)
    {
        PhysicsMaterial2D mat = new PhysicsMaterial2D(name);
        mat.friction = friction;
        mat.bounciness = 0f;
        return mat;
    }

    private static void createPlatform(Transform parent, string name, Vector3 pos, Vector2 size)
    {
        GameObject platObj = createChildObject(name, parent, pos);

        var col = platObj.AddComponent<BoxCollider2D>();
        col.size = size;

        var effector = platObj.AddComponent<PlatformEffector2D>();
        effector.surfaceArc = 180f;
        effector.useOneWay = true;
        col.usedByEffector = true;

        platObj.AddComponent<OneWayPlatformPassThrough>();

        var rend = platObj.AddComponent<SpriteRenderer>();
        bindSprite(rend, "Assets/Textures/Environment/Tile_Platform_OneWay.png", new Color(0.1f, 0.7f, 0.85f, 0.9f));
    }

    private static void bindSprite(SpriteRenderer renderer, string texturePath, Color fallbackColor)
    {
        if (renderer == null) return;

        Sprite loadedSprite = null;
        var assets = AssetDatabase.LoadAllAssetsAtPath(texturePath);
        if (assets != null && assets.Length > 0)
        {
            foreach (var a in assets)
            {
                if (a is Sprite sp)
                {
                    loadedSprite = sp;
                    break;
                }
            }
        }

        if (loadedSprite == null)
        {
            loadedSprite = AssetDatabase.LoadAssetAtPath<Sprite>(texturePath);
        }

        if (loadedSprite != null)
        {
            renderer.sprite = loadedSprite;
            renderer.color = Color.white;
        }
        else
        {
            renderer.sprite = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), 16f);
            renderer.color = fallbackColor;
        }
    }
}
#endif
