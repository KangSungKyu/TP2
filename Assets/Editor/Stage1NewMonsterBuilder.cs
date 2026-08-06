#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

public static class Stage1NewMonsterBuilder
{
    [MenuItem("TP2/Build Stage 1 New Monsters")]
    public static void Build()
    {
        BuildMonster("ShieldSentinel", "Attack6003", "Attack6004");
        BuildMonster("OrbitalMarksman", "Attack6005", "Attack6006");
        AssetDatabase.SaveAssets();
        AddressablePipeline.RegisterAllAddressables();
    }

    private static void BuildMonster(string name, string attackA, string attackB)
    {
        string textureRoot = $"Assets/Textures/Characters/Monsters/{name}/{name}_";
        var clips = new Dictionary<string, AnimationClip>();
        foreach (string action in new[] { "Idle", "Move", "Hit", "Death", attackA, attackB })
            clips[action] = BuildClip(textureRoot + action + ".png", $"Assets/Anims/Monster/{name}_{action}.anim", action == "Idle" || action == "Move");

        string controllerPath = $"Assets/Anims/Monster/{name}AnimatorController.controller";
        var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);
        if (controller == null) controller = AnimatorController.CreateAnimatorControllerAtPath(controllerPath);
        controller.parameters = new[] { new AnimatorControllerParameter { name = "State", type = AnimatorControllerParameterType.Int } };
        var machine = controller.layers[0].stateMachine;
        foreach (var state in machine.states) machine.RemoveState(state.state);
        foreach (var transition in machine.anyStateTransitions) machine.RemoveAnyStateTransition(transition);

        AddState(machine, clips["Idle"], $"{name}_Idle", 1, true);
        AddState(machine, clips["Move"], $"{name}_Move", 2);
        AddState(machine, clips["Hit"], $"{name}_Hit", 5);
        AddState(machine, clips["Death"], $"{name}_Death", 8);
        AddState(machine, clips[attackA], $"{name}_{attackA}", 7);
        machine.AddState($"{name}_{attackB}").motion = clips[attackB];
        EditorUtility.SetDirty(controller);

        var root = new GameObject(name);
        var renderer = root.AddComponent<SpriteRenderer>();
        renderer.sortingOrder = 10;
        renderer.sprite = AssetDatabase.LoadAllAssetsAtPath(textureRoot + "Idle.png").OfType<Sprite>().OrderBy(sprite => sprite.name).First();
        var collider = root.AddComponent<BoxCollider2D>();
        collider.size = new Vector2(1.2f, 2f);
        collider.offset = new Vector2(0f, 1f);
        root.AddComponent<Rigidbody2D>().bodyType = RigidbodyType2D.Kinematic;
        root.AddComponent<KinematicMotor2D>();
        root.AddComponent<CombatStats>();
        root.AddComponent<Monster>();
        root.AddComponent<Animator>().runtimeAnimatorController = controller;
        PrefabUtility.SaveAsPrefabAsset(root, $"Assets/Prefabs/{name}.prefab");
        Object.DestroyImmediate(root);
    }

    private static AnimationClip BuildClip(string texturePath, string clipPath, bool loop)
    {
        var importer = (TextureImporter)AssetImporter.GetAtPath(texturePath);
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Multiple;
        importer.spritePixelsPerUnit = 64;
        importer.alphaIsTransparency = true;
        importer.filterMode = FilterMode.Point;
        importer.spritesheet = Enumerable.Range(0, 8).Select(frame => new SpriteMetaData
        {
            name = $"{System.IO.Path.GetFileNameWithoutExtension(texturePath)}_{frame}",
            rect = new Rect(frame * 128, 0, 128, 256),
            alignment = (int)SpriteAlignment.BottomCenter,
            pivot = new Vector2(0.5f, 0f)
        }).ToArray();
        importer.SaveAndReimport();

        var sprites = AssetDatabase.LoadAllAssetsAtPath(texturePath).OfType<Sprite>().OrderBy(sprite => sprite.name).ToArray();
        var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPath);
        if (clip == null) { clip = new AnimationClip(); AssetDatabase.CreateAsset(clip, clipPath); }
        clip.frameRate = 8;
        var settings = AnimationUtility.GetAnimationClipSettings(clip);
        settings.loopTime = loop;
        AnimationUtility.SetAnimationClipSettings(clip, settings);
        AnimationUtility.SetObjectReferenceCurve(clip, new EditorCurveBinding
        {
            type = typeof(SpriteRenderer), path = "", propertyName = "m_Sprite"
        }, sprites.Select((sprite, frame) => new ObjectReferenceKeyframe { time = frame / 8f, value = sprite }).ToArray());
        EditorUtility.SetDirty(clip);
        return clip;
    }

    private static void AddState(AnimatorStateMachine machine, AnimationClip clip, string name, int value, bool isDefault = false)
    {
        var state = machine.AddState(name);
        state.motion = clip;
        if (isDefault) machine.defaultState = state;
        var transition = machine.AddAnyStateTransition(state);
        transition.AddCondition(AnimatorConditionMode.Equals, value, "State");
        transition.hasExitTime = false;
        transition.duration = 0.1f;
        transition.canTransitionToSelf = false;
    }
}
#endif
