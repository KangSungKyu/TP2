#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

/// <summary>
/// Addressable 'Particle' 키에 해당하는 기본 Particle.prefab을 생성해 주는 에디터 유틸리티.
/// </summary>
public static class ParticlePrefabBuilder
{
    [MenuItem("TP2/Build Particle Prefab (Particle.prefab 생성)")]
    public static void BuildParticlePrefab()
    {
        string prefabPath = "Assets/prefabs/Particle.prefab";

        if (!AssetDatabase.IsValidFolder("Assets/prefabs"))
        {
            AssetDatabase.CreateFolder("Assets", "prefabs");
        }

        GameObject particleObj = new GameObject("Particle");
        ParticleSystem ps = particleObj.AddComponent<ParticleSystem>();
        var main = ps.main;
        main.duration = 1.0f;
        main.startSize = 0.5f;

        bool success;
        PrefabUtility.SaveAsPrefabAsset(particleObj, prefabPath, out success);
        Object.DestroyImmediate(particleObj);

        if (success)
        {
            Debug.Log($"<color=cyan><b>[ParticlePrefabBuilder] 'Assets/prefabs/Particle.prefab' 생성 완료!</b></color>");
        }
    }
}
#endif
