import os, glob, re

anims_effects_dir = r"c:\Users\PC\Projects\TP2\Assets\Anims\Effects"
effects_tex_dir = r"c:\Users\PC\Projects\TP2\Assets\Textures\Effects"
os.makedirs(anims_effects_dir, exist_ok=True)

effect_specs = [
    ("Placeholder_Parry", r"c:\Users\PC\Projects\TP2\Assets\Textures\Effects\Placeholder_Parry.png", 8, 128, 128),
    ("Placeholder_Guard", r"c:\Users\PC\Projects\TP2\Assets\Textures\Effects\Placeholder_Guard.png", 8, 128, 128),
    ("Placeholder_Dodge", r"c:\Users\PC\Projects\TP2\Assets\Textures\Effects\Placeholder_Dodge.png", 8, 128, 128),
    ("Placeholder_Hit", r"c:\Users\PC\Projects\TP2\Assets\Textures\Effects\Placeholder_Hit.png", 8, 128, 128),
    ("Player_Attack_Hit1_Effect", r"c:\Users\PC\Projects\TP2\Assets\Textures\Effects\Player\Player_Attack_Hit1_Effect.png", 8, 128, 128),
    ("Player_Attack_Hit2_Effect", r"c:\Users\PC\Projects\TP2\Assets\Textures\Effects\Player\Player_Attack_Hit2_Effect.png", 8, 128, 128),
    ("Player_Attack_Hit3_Effect", r"c:\Users\PC\Projects\TP2\Assets\Textures\Effects\Player\Player_Attack_Hit3_Effect.png", 8, 160, 160),
    ("Garon_ComboSlash_Effect", r"c:\Users\PC\Projects\TP2\Assets\Textures\Effects\Bosses\Garon\Garon_ComboSlash_Effect.png", 8, 256, 256),
    ("Garon_OverheadSmash_Effect", r"c:\Users\PC\Projects\TP2\Assets\Textures\Effects\Bosses\Garon\Garon_OverheadSmash_Effect.png", 8, 256, 128),
    ("Garon_Shockwave_Effect", r"c:\Users\PC\Projects\TP2\Assets\Textures\Effects\Bosses\Garon\Garon_Shockwave_Effect.png", 8, 128, 128),
    ("Garon_Charge_Effect", r"c:\Users\PC\Projects\TP2\Assets\Textures\Effects\Bosses\Garon\Garon_Charge_Effect.png", 8, 256, 256)
]

for key, tex_p, frames, fw, fh in effect_specs:
    anim_p = os.path.join(anims_effects_dir, f"{key}.anim")
    anim_meta = anim_p + ".meta"
    ctrl_p = os.path.join(anims_effects_dir, f"{key}_Controller.controller")
    ctrl_meta = ctrl_p + ".meta"
    
    tex_meta = tex_p + ".meta"
    tex_guid = ""
    sub_internal_ids = []
    if os.path.exists(tex_meta):
        with open(tex_meta, "r", encoding="utf-8") as f:
            txt = f.read()
        m = re.search(r"guid: ([a-f0-9]+)", txt)
        if m: tex_guid = m.group(1)
        sub_internal_ids = re.findall(r"internalID: (\d+)", txt)

    if not sub_internal_ids:
        sub_internal_ids = [21300000 + i * 2 for i in range(frames)]

    anim_guid = f"e{hash(key + '_anim') & 0xffffffffffffffff:031x}"
    ctrl_guid = f"f{hash(key + '_ctrl') & 0xffffffffffffffff:031x}"

    # 1. Build .anim YAML
    keyframes_curve = ""
    pptr_mappings = ""
    fps = 8.0
    for i, int_id in enumerate(sub_internal_ids):
        t = i / fps
        keyframes_curve += f"    - time: {t}\n      value: {{fileID: {int_id}, guid: {tex_guid}, type: 3}}\n"
        pptr_mappings += f"    - {{fileID: {int_id}, guid: {tex_guid}, type: 3}}\n"

    anim_yaml = f"""%YAML 1.1
%TAG !u! tag:unity3d.com,2011:
--- !u!74 &7400000
AnimationClip:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_Name: {key}
  serializedVersion: 7
  m_Legacy: 0
  m_Compressed: 0
  m_UseHighQualityCurve: 1
  m_RotationCurves: []
  m_CompressedRotationCurves: []
  m_EulerCurves: []
  m_PositionCurves: []
  m_ScaleCurves: []
  m_FloatCurves: []
  m_PPtrCurves:
  - serializedVersion: 2
    curve:
{keyframes_curve}    attribute: m_Sprite
    path: 
    classID: 212
    script: {{fileID: 0}}
    flags: 2
  m_SampleRate: {fps}
  m_WrapMode: 0
  m_Bounds:
    m_Center: {{x: 0, y: 0, z: 0}}
    m_Extent: {{x: 0, y: 0, z: 0}}
  m_ClipBindingConstant:
    genericBindings:
    - serializedVersion: 2
      path: 0
      attribute: 0
      script: {{fileID: 0}}
      classID: 212
      customType: 23
      isPPtrCurve: 1
      isIntCurve: 0
      isSerializeReferenceCurve: 0
    pptrCurveMapping:
{pptr_mappings}  m_AnimationClipSettings:
    serializedVersion: 2
    m_AdditiveReferencePoseClip: {{fileID: 0}}
    m_AdditiveReferencePoseTime: 0
    m_StartTime: 0
    m_StopTime: {len(sub_internal_ids) / fps}
    m_OrientationOffsetY: 0
    m_Level: 0
    m_CycleOffset: 0
    m_HasAdditiveReferencePose: 0
    m_LoopTime: 0
    m_LoopBlend: 0
    m_LoopBlendOrientation: 0
    m_LoopBlendPositionY: 0
    m_LoopBlendPositionXZ: 0
    m_KeepOriginalOrientation: 0
    m_KeepOriginalPositionY: 1
    m_KeepOriginalPositionXZ: 0
    m_HeightFromFeet: 0
    m_Mirror: 0
  m_EditorCurves: []
  m_EulerEditorCurves: []
  m_HasGenericRootTransform: 0
  m_HasMotionFloatCurves: 0
  m_Events: []
"""
    with open(anim_p, "w", encoding="utf-8") as f:
        f.write(anim_yaml)
    with open(anim_meta, "w", encoding="utf-8") as f:
        f.write(f"fileFormatVersion: 2\nguid: {anim_guid}\nNativeFormatImporter:\n  externalObjects: {{}}\n  mainObjectFileID: 7400000\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n")

    # 2. Build .controller YAML
    ctrl_yaml = f"""%YAML 1.1
%TAG !u! tag:unity3d.com,2011:
--- !u!91 &9100000
AnimatorController:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_Name: {key}_Controller
  serializedVersion: 5
  m_AnimatorParameters: []
  m_AnimatorLayers:
  - serializedVersion: 5
    m_Name: Base Layer
    m_StateMachine: {{fileID: 1107000000000000000}}
    m_Mask: {{fileID: 0}}
    m_Motions: []
    m_Behaviours: []
    m_BlendingMode: 0
    m_SyncedLayerIndex: -1
    m_DefaultWeight: 0
    m_IKPass: 0
    m_SyncedLayerAffectsTiming: 0
    m_Controller: {{fileID: 9100000}}
--- !u!1102 &1102000000000000000
AnimatorState:
  serializedVersion: 6
  m_ObjectHideFlags: 1
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_Name: Default
  m_Speed: 1
  m_CycleOffset: 0
  m_Transitions: []
  m_StateMachineBehaviours: []
  m_Position: {{x: 50, y: 50, z: 0}}
  m_IKOnFeet: 0
  m_WriteDefaultValues: 1
  m_Mirror: 0
  m_SpeedParameterActive: 0
  m_MirrorParameterActive: 0
  m_CycleOffsetParameterActive: 0
  m_TimeParameterActive: 0
  m_Motion: {{fileID: 7400000, guid: {anim_guid}, type: 2}}
  m_Tag: 
  m_SpeedParameter: 
  m_MirrorParameter: 
  m_CycleOffsetParameter: 
  m_TimeParameter: 
--- !u!1107 &1107000000000000000
AnimatorStateMachine:
  serializedVersion: 6
  m_ObjectHideFlags: 1
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_Name: Base Layer
  m_ChildStates:
  - serializedVersion: 1
    m_State: {{fileID: 1102000000000000000}}
    m_Position: {{x: 300, y: 120, z: 0}}
  m_ChildStateMachines: []
  m_AnyStateTransitions: []
  m_EntryTransitions: []
  m_StateMachineTransitions: {{}}
  m_StateMachineBehaviours: []
  m_AnyStatePosition: {{x: 50, y: 20, z: 0}}
  m_EntryPosition: {{x: 50, y: 120, z: 0}}
  m_ExitPosition: {{x: 800, y: 120, z: 0}}
  m_ParentStateMachinePosition: {{x: 800, y: 20, z: 0}}
  m_DefaultState: {{fileID: 1102000000000000000}}
"""
    with open(ctrl_p, "w", encoding="utf-8") as f:
        f.write(ctrl_yaml)
    with open(ctrl_meta, "w", encoding="utf-8") as f:
        f.write(f"fileFormatVersion: 2\nguid: {ctrl_guid}\nNativeFormatImporter:\n  externalObjects: {{}}\n  mainObjectFileID: 9100000\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n")

    # 3. Update Prefabs YAML to reference this ctrl_guid
    for p_dir in [r"c:\Users\PC\Projects\TP2\Assets\prefabs", r"c:\Users\PC\Projects\TP2\Assets\prefabs\Effects"]:
        pf_path = os.path.join(p_dir, f"{key}.prefab")
        if os.path.exists(pf_path):
            with open(pf_path, "r", encoding="utf-8") as f:
                pf_txt = f.read()
            pf_txt = re.sub(r"m_Controller: \{fileID: 0\}", f"m_Controller: {{fileID: 9100000, guid: {ctrl_guid}, type: 2}}", pf_txt)
            with open(pf_path, "w", encoding="utf-8") as f:
                f.write(pf_txt)

    print(f"Generated Effect Controller & Anim: {key}")

print("All 11 Effect Controllers, Anims, and Prefab bindings written to disk successfully!")
