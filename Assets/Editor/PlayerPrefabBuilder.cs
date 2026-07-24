#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

/// <summary>
/// Root 객체(지면에 닿는 발밑 피벗/중심점 Y=0)와 
/// Visual 하위 객체(Pure 2D SpriteRenderer + Animator) 구조를 준수하여 
/// 3D 캡슐 없이 요청된 2D 애니메이션이 즉각 출력되도록 표준 Player.prefab을 생성하는 에디터 스크립트.
/// </summary>
public static class PlayerPrefabBuilder
{
    [MenuItem("TP2/Build Player Prefab (Player.prefab 완벽 생성)")]
    public static void BuildPlayerPrefab()
    {
        // 1. [Root GameObject] - 지면에 닿는 발밑 피벗 중심점 (Y=0 레벨)
        GameObject playerRoot = new GameObject("Player");
        playerRoot.tag = "Player";
        playerRoot.layer = 0;
        playerRoot.transform.position = Vector3.zero;

        // Root에 로직 / 전투 컴포넌트 부착
        CombatStats stats = playerRoot.AddComponent<CombatStats>();
        stats.MaxHp = 100f;
        stats.MaxMp = 50f;
        stats.MaxPosture = 100f;

        SkillExecutor skillExecutor = playerRoot.AddComponent<SkillExecutor>();
        Player playerComp = playerRoot.AddComponent<Player>();
        playerComp.Speed = 5f;
        playerComp.DodgeDashSpeed = 12f;

        // 2. [Visual 하위 GameObject] - Pure 2D 렌더링 전담 객체 (SpriteRenderer + Animator)
        GameObject visualObj = new GameObject("Visual");
        visualObj.transform.SetParent(playerRoot.transform, false);
        visualObj.transform.localPosition = new Vector3(0f, 0.6f, 0f); // 지면 피벗 맞춤 로컬 오프셋

        // SpriteRenderer 추가 및 스프라이트 바인딩
        SpriteRenderer spriteRenderer = visualObj.AddComponent<SpriteRenderer>();
        spriteRenderer.sortingOrder = 10;
        
        string spritePath = "Assets/Textures/Characters/Player/Player_Concept.png";
        Sprite playerSprite = AssetDatabase.LoadAssetAtPath<Sprite>(spritePath);
        if (playerSprite == null)
        {
            playerSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Textures/Characters/Player/Player_Idle.png");
        }

        if (playerSprite != null)
        {
            spriteRenderer.sprite = playerSprite;
            Debug.Log($"[PlayerPrefabBuilder] 하위 'Visual' SpriteRenderer에 '{playerSprite.name}' 바인딩 완료.");
        }

        // Animator 추가 및 PlayerAnimatorController 바인딩
        Animator animator = visualObj.AddComponent<Animator>();
        string controllerPath = "Assets/Anims/Player/PlayerAnimatorController.controller";
        RuntimeAnimatorController controller = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(controllerPath);
        if (controller != null)
        {
            animator.runtimeAnimatorController = controller;
            Debug.Log($"[PlayerPrefabBuilder] 하위 'Visual' Animator에 '{controller.name}' 바인딩 완료.");
        }

        // 3. Assets/prefabs/Player.prefab 으로 저장
        string prefabPath = "Assets/prefabs/Player.prefab";

        if (!AssetDatabase.IsValidFolder("Assets/prefabs"))
        {
            AssetDatabase.CreateFolder("Assets", "prefabs");
        }

        bool success;
        PrefabUtility.SaveAsPrefabAsset(playerRoot, prefabPath, out success);

        // 임시 씬 오브젝트 파괴
        Object.DestroyImmediate(playerRoot);

        if (success)
        {
            Debug.Log($"<color=cyan><b>[PlayerPrefabBuilder] Pure 2D (Root 지면 피벗 + Visual Sprite/Animator) 'Assets/prefabs/Player.prefab' 에셋 생성 완료!</b></color>");
            
            // Addressables에도 자동 등록
            AddressableAutoRegister.RegisterAllAddressables();
        }
        else
        {
            Debug.LogError($"[PlayerPrefabBuilder] 프리팹 저장에 실패했습니다: {prefabPath}");
        }
    }
}
#endif
