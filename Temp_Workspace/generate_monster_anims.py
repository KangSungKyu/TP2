import os, glob, re

monster_tex_dir = r"c:\Users\PC\Projects\TP2\Assets\Textures\Characters\Monsters"
anims_monster_dir = r"c:\Users\PC\Projects\TP2\Assets\Anims\Monster"

monsters = ["SpearSentry", "ShadowStalker", "WaveHeavy"]
actions = ["Idle", "Move", "Jump", "Attack", "Death"]

print("=== GENERATING MONSTER ANIMATION CLIPS AND METAS ===")

for mname in monsters:
    for act in actions:
        anim_name = f"{mname}_{act}"
        anim_path = os.path.join(anims_monster_dir, f"{anim_name}.anim")
        meta_path = anim_path + ".meta"
        
        # Texture sheet path
        tex_path = os.path.join(monster_tex_dir, mname, f"{anim_name}.png")
        tex_meta = tex_path + ".meta"
        
        guid = ""
        if os.path.exists(tex_meta):
            with open(tex_meta, "r", encoding="utf-8") as f:
                txt = f.read()
            m = re.search(r"guid: ([a-f0-9]+)", txt)
            if m:
                guid = m.group(1)

        if not os.path.exists(anim_path):
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
    - time: 0
      value: {{fileID: 21300000, guid: {guid}, type: 3}}
    attribute: m_Sprite
    path: 
    classID: 212
    script: {{fileID: 0}}
    flags: 2
  m_SampleRate: 8
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
    - {{fileID: 21300000, guid: {guid}, type: 3}}
  m_AnimationClipSettings:
    serializedVersion: 2
    m_AdditiveReferencePoseClip: {{fileID: 0}}
    m_AdditiveReferencePoseTime: 0
    m_StartTime: 0
    m_StopTime: 1
    m_OrientationOffsetY: 0
    m_Level: 0
    m_CycleOffset: 0
    m_HasAdditiveReferencePose: 0
    m_LoopTime: 1
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
            print(f"Created Anim Clip: {anim_path}")
            
        if not os.path.exists(meta_path):
            anim_guid = f"{hash(anim_name) & 0xffffffffffffffff:032x}"
            meta_content = f"""fileFormatVersion: 2
guid: {anim_guid}
NativeFormatImporter:
  externalObjects: {{}}
  mainObjectFileID: 7400000
  userData: 
  assetBundleName: 
  assetBundleVariant: 
"""
            with open(meta_path, "w", encoding="utf-8") as f:
                f.write(meta_content)
            print(f"Created Anim Meta: {meta_path}")

# Update Monster Controllers to bind these clips
from build_monster_yaml import build_monster_controller_yaml
monster_map = {1: "Idle", 2: "Move", 3: "Jump", 7: "Attack", 8: "Death"}
for mname in monsters:
    build_monster_controller_yaml(mname, monster_map)
print("Updated Monster Controllers YAML bindings!")
