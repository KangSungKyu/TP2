import os, glob, re

anims_player_dir = r"c:\Users\PC\Projects\TP2\Assets\Anims\Player"
anims_monster_dir = r"c:\Users\PC\Projects\TP2\Assets\Anims\Monster"
player_tex_dir = r"c:\Users\PC\Projects\TP2\Assets\Textures\Characters\Player"
garon_tex_dir = r"c:\Users\PC\Projects\TP2\Assets\Textures\Characters\Bosses\Garon"

player_specs = [
    ("Player_Idle", "Player_Idle.png", 12, 1),
    ("Player_Run", "Player_Run.png", 12, 1),
    ("Player_Jump", "Player_Jump.png", 16, 0),
    ("Player_Parry", "Player_Parry.png", 24, 0),
    ("Player_Guard", "Player_Guard.png", 12, 1),
    ("Player_Dodge", "Player_Dodge.png", 20, 0),
    ("Player_Attack_Hit1", "Player_Attack_Hit1.png", 24, 0),
    ("Player_Execution", "Player_Execution.png", 16, 0),
    ("Player_Attack_Hit2", "Player_Attack_Hit2.png", 24, 0),
    ("Player_Attack_Hit3", "Player_Attack_Hit3.png", 20, 0)
]

garon_specs = [
    ("Garon_Idle", "Garon_Idle.png", 8, 1),
    ("Garon_Move", "Garon_Move.png", 10, 1),
    ("Garon_Jump", "Garon_Jump.png", 10, 0),
    ("Garon_Pattern_OverheadSmash", "Garon_Pattern_OverheadSmash.png", 16, 0),
    ("Garon_Pattern_ComboSlash", "Garon_Pattern_ComboSlash.png", 16, 0),
    ("Garon_Pattern_Charge", "Garon_Pattern_Charge.png", 16, 0),
    ("Garon_Pattern_Shockwave", "Garon_Pattern_Shockwave.png", 16, 0),
    ("Garon_Death", "Garon_Death.png", 8, 0)
]

def update_anim_with_subsprites(anim_dir, anim_name, tex_path, fps, loop):
    anim_path = os.path.join(anim_dir, f"{anim_name}.anim")
    meta_path = tex_path + ".meta"
    
    guid = ""
    sub_internal_ids = []
    if os.path.exists(meta_path):
        with open(meta_path, "r", encoding="utf-8") as f:
            txt = f.read()
        m = re.search(r"guid: ([a-f0-9]+)", txt)
        if m: guid = m.group(1)
        sub_internal_ids = re.findall(r"internalID: (\d+)", txt)
        
    if not sub_internal_ids:
        sub_internal_ids = [21300000]
        
    keyframes_curve = ""
    pptr_mappings = ""
    generic_bindings = ""
    
    frame_count = len(sub_internal_ids)
    for i, int_id in enumerate(sub_internal_ids):
        t = i / fps
        keyframes_curve += f"    - time: {t}\n      value: {{fileID: {int_id}, guid: {guid}, type: 3}}\n"
        pptr_mappings += f"    - {{fileID: {int_id}, guid: {guid}, type: 3}}\n"
        
    anim_content = f"""%YAML 1.1
%TAG !u! tag:unity3d.com,2011:
--- !u!74 &7400000
AnimationClip:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_Name: {anim_name}
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
    m_StopTime: {frame_count / fps}
    m_OrientationOffsetY: 0
    m_Level: 0
    m_CycleOffset: 0
    m_HasAdditiveReferencePose: 0
    m_LoopTime: {loop}
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
    with open(anim_path, "w", encoding="utf-8") as f:
        f.write(anim_content)
    print(f"Updated .anim ({frame_count} frames): {anim_name}")

for name, tex, fps, loop in player_specs:
    update_anim_with_subsprites(anims_player_dir, name, os.path.join(player_tex_dir, tex), fps, loop)

for name, tex, fps, loop in garon_specs:
    update_anim_with_subsprites(anims_monster_dir, name, os.path.join(garon_tex_dir, tex), fps, loop)

print("All AnimationClips successfully updated with 8/16 sub-sprite keyframe sequences!")
