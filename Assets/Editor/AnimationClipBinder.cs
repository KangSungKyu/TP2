#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>
/// .anim 애니메이션 클립 파일에 분할된 Sprite Sub-Assets 키프레임들을 자동 바인딩해주는 에디터 스크립트.
/// </summary>
public static class AnimationClipBinder
{
    [MenuItem("TP2/Bind All Animation Clips (모든 애니메이션 키프레임 채우기)")]
    public static void BindAllAnimationClips()
    {
        int boundCount = 0;

        // 1. 플레이어 애니메이션 클립 바인딩
        boundCount += bindCategoryClips("Assets/Anims/Player", "Assets/Textures/Characters/Player");

        // 2. 보스 애니메이션 클립 바인딩
        boundCount += bindCategoryClips("Assets/Anims/Monster", "Assets/Textures/Characters/Bosses/Garon");

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log($"<color=cyan><b>[AnimationClipBinder] 총 {boundCount}개의 애니메이션 클립에 키프레임 바인딩 완료!</b></color>");
    }

    private static int bindCategoryClips(string animFolderPath, string textureFolderPath)
    {
        if (!Directory.Exists(animFolderPath) || !Directory.Exists(textureFolderPath)) return 0;

        string[] animGuids = AssetDatabase.FindAssets("t:AnimationClip", new[] { animFolderPath });
        int count = 0;

        foreach (var guid in animGuids)
        {
            string animPath = AssetDatabase.GUIDToAssetPath(guid);
            AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(animPath);
            if (clip == null) continue;

            string clipName = clip.name; // 예: Player_Idle, Garon_Idle
            
            string texturePath = $"{textureFolderPath}/{clipName}.png";
            if (!File.Exists(texturePath))
            {
                string[] matchingTexGuids = AssetDatabase.FindAssets($"{clipName} t:Texture2D", new[] { textureFolderPath });
                if (matchingTexGuids.Length > 0)
                {
                    texturePath = AssetDatabase.GUIDToAssetPath(matchingTexGuids[0]);
                }
            }

            if (File.Exists(texturePath))
            {
                Object[] assets = AssetDatabase.LoadAllAssetsAtPath(texturePath);
                List<Sprite> sprites = assets.OfType<Sprite>().OrderBy(s => s.name).ToList();

                if (sprites.Count > 0)
                {
                    bindSpritesToClip(clip, sprites);
                    EditorUtility.SetDirty(clip);
                    count++;
                }
            }
        }

        return count;
    }

    private static void bindSpritesToClip(AnimationClip clip, List<Sprite> sprites)
    {
        clip.frameRate = 12; // 12 FPS

        // Root 하위 자식 Visual 객체 바인딩 및 Root 직접 바인딩 2가지 바인딩 동시 추가
        EditorCurveBinding rootBinding = new EditorCurveBinding
        {
            type = typeof(SpriteRenderer),
            path = "",
            propertyName = "m_Sprite"
        };

        EditorCurveBinding visualBinding = new EditorCurveBinding
        {
            type = typeof(SpriteRenderer),
            path = "Visual",
            propertyName = "m_Sprite"
        };

        ObjectReferenceKeyframe[] keyframes = new ObjectReferenceKeyframe[sprites.Count];
        float frameDuration = 1f / clip.frameRate;

        for (int i = 0; i < sprites.Count; i++)
        {
            keyframes[i] = new ObjectReferenceKeyframe
            {
                time = i * frameDuration,
                value = sprites[i]
            };
        }

        AnimationUtility.SetObjectReferenceCurve(clip, rootBinding, keyframes);
        AnimationUtility.SetObjectReferenceCurve(clip, visualBinding, keyframes);
        
        var settings = AnimationUtility.GetAnimationClipSettings(clip);
        settings.loopTime = true;
        AnimationUtility.SetAnimationClipSettings(clip, settings);
    }
}
#endif
