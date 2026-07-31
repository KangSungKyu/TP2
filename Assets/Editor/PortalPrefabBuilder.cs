#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using System.IO;

/// <summary>
/// 스테이지 관문 포탈 (Portal/Door) 구조물 프리팹 패키징 및 Addressables 배포 유틸리티.
/// Assets/Prefabs/Structures/Portal.prefab 및 Door.prefab 제작 및 Addressable Key ("Portal", "Door") 등록.
/// </summary>
public static class PortalPrefabBuilder
{
    [MenuItem("TP2/Build Portal & Door Prefabs (관문 포탈 프리팹 제작)")]
    public static void BuildPortalPrefabs()
    {
        Debug.Log("<color=cyan><b>[PortalPrefabBuilder] 관문 포탈 & 문 프리팹 패키징 시작...</b></color>");

        string structDir = "Assets/Prefabs/Structures";
        if (!Directory.Exists(structDir))
        {
            Directory.CreateDirectory(structDir);
            AssetDatabase.Refresh();
        }

        Sprite structSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Textures/Environment/Sprite_Structures_Interactive.png");

        // 1. Portal.prefab
        string portalPath = $"{structDir}/Portal.prefab";
        GameObject portalObj = new GameObject("Portal");
        var pSr = portalObj.AddComponent<SpriteRenderer>();
        if (structSprite != null) pSr.sprite = structSprite;
        var pCol = portalObj.AddComponent<BoxCollider2D>();
        pCol.isTrigger = true;
        pCol.size = new Vector2(1, 2);

        PrefabUtility.SaveAsPrefabAsset(portalObj, portalPath);
        Object.DestroyImmediate(portalObj);

        // 2. Door.prefab
        string doorPath = $"{structDir}/Door.prefab";
        GameObject doorObj = new GameObject("Door");
        var dSr = doorObj.AddComponent<SpriteRenderer>();
        if (structSprite != null) dSr.sprite = structSprite;
        var dCol = doorObj.AddComponent<BoxCollider2D>();
        dCol.isTrigger = true;
        dCol.size = new Vector2(1, 2);

        PrefabUtility.SaveAsPrefabAsset(doorObj, doorPath);
        Object.DestroyImmediate(doorObj);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log($"<color=green>Created Portal & Door Prefabs in {structDir}</color>");

        // Addressables 등록 및 배포
        AddressablePipeline.BuildAndDeploy();

        Debug.Log("<color=green><b>[PortalPrefabBuilder] Portal & Door 프리팹 제작 및 Addressables 배포 완결!</b></color>");
    }
}
#endif
