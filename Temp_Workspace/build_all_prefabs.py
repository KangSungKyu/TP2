import os, glob

prefabs_dir = r"c:\Users\PC\Projects\TP2\Assets\prefabs"
effects_prefabs_dir = r"c:\Users\PC\Projects\TP2\Assets\prefabs\Effects"
os.makedirs(prefabs_dir, exist_ok=True)
os.makedirs(effects_prefabs_dir, exist_ok=True)

effect_keys = [
    "Placeholder_Parry",
    "Placeholder_Guard",
    "Placeholder_Dodge",
    "Placeholder_Hit",
    "Player_Attack_Hit1_Effect",
    "Player_Attack_Hit2_Effect",
    "Player_Attack_Hit3_Effect",
    "Garon_ComboSlash_Effect",
    "Garon_OverheadSmash_Effect",
    "Garon_Shockwave_Effect",
    "Garon_Charge_Effect"
]

prefab_yaml_template = """%YAML 1.1
%TAG !u! tag:unity3d.com,2011:
--- !u!1 &100000
GameObject:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  serializedVersion: 6
  m_Component:
  - component: {{fileID: 400000}}
  - component: {{fileID: 21200000}}
  - component: {{fileID: 9500000}}
  m_Layer: 0
  m_Name: {key}
  m_TagString: Untagged
  m_Icon: {{fileID: 0}}
  m_NavMeshLayer: 0
  m_StaticEditorFlags: 0
  m_IsActive: 1
--- !u!4 &400000
Transform:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: 100000}}
  m_LocalRotation: {{x: 0, y: 0, z: 0, w: 1}}
  m_LocalPosition: {{x: 0, y: 0, z: 0}}
  m_LocalScale: {{x: 1, y: 1, z: 1}}
  m_ConstrainProportionsScale: 0
  m_Children: []
  m_Father: {{fileID: 0}}
  m_RootOrder: 0
  m_LocalEulerAnglesHint: {{x: 0, y: 0, z: 0}}
--- !u!212 &21200000
SpriteRenderer:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: 100000}}
  m_Enabled: 1
  m_CastShadows: 0
  m_ReceiveShadows: 0
  m_DynamicOccludee: 1
  m_StaticShadowCaster: 0
  m_MotionVectors: 1
  m_LightProbeUsage: 1
  m_ReflectionProbeUsage: 1
  m_RayTracingMode: 0
  m_RayTraceProcedural: 0
  m_RenderingLayerMask: 1
  m_RendererPriority: 0
  m_Materials:
  - {{fileID: 10754, guid: 0000000000000000f000000000000000, type: 0}}
  m_StaticBatchInfo:
    firstSubMesh: 0
    subMeshCount: 0
  m_StaticBatchRoot: {{fileID: 0}}
  m_ProbeAnchor: {{fileID: 0}}
  m_LightProbeVolumeOverride: {{fileID: 0}}
  m_ScaleInLightmap: 1
  m_ReceiveGI: 1
  m_PreserveUVs: 0
  m_IgnoreNormalsForChartDetection: 0
  m_ImportantGI: 0
  m_StitchLightmapSeams: 1
  m_SelectedEditorRenderState: 0
  m_MinimumChartSize: 4
  m_AutoUVMaxDistance: 0.5
  m_AutoUVMaxAngle: 89
  m_LightmapParameters: {{fileID: 0}}
  m_SortingLayerID: 0
  m_SortingLayer: 0
  m_SortingOrder: 10
  m_Sprite: {{fileID: 0}}
  m_Color: {{r: 1, g: 1, b: 1, a: 1}}
  m_FlipX: 0
  m_FlipY: 0
  m_DrawMode: 0
  m_Size: {{x: 1, y: 1}}
  m_AdaptiveModeThreshold: 0.5
  m_SpriteTileMode: 0
  m_WasSpriteAssigned: 0
  m_MaskInteraction: 0
  m_SpriteSortPoint: 0
--- !u!95 &9500000
Animator:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: 100000}}
  m_Enabled: 1
  m_Avatar: {{fileID: 0}}
  m_Controller: {{fileID: 0}}
  m_CullingMode: 0
  m_UpdateMode: 0
  m_ApplyRootMotion: 0
  m_LinearVelocityBlending: 0
  m_StabilizeFeet: 0
  m_WarningMessage: 
  m_HasTransformHierarchy: 1
  m_AllowConstantClipSamplingOptimization: 1
  m_KeepAnimatorStateOnDisable: 0
  m_WriteDefaultValuesOnDisable: 0
"""

for key in effect_keys:
    yaml_txt = prefab_yaml_template.format(key=key)
    
    # Save to Assets/prefabs/
    root_p = os.path.join(prefabs_dir, f"{key}.prefab")
    with open(root_p, "w", encoding="utf-8") as f:
        f.write(yaml_txt)
    if not os.path.exists(root_p + ".meta"):
        guid = f"c{hash(key + '_root') & 0xffffffffffffffff:031x}"
        meta = f"fileFormatVersion: 2\nguid: {guid}\nPrefabImporter:\n  externalObjects: {{}}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n"
        with open(root_p + ".meta", "w", encoding="utf-8") as f:
            f.write(meta)
            
    # Save to Assets/prefabs/Effects/
    sub_p = os.path.join(effects_prefabs_dir, f"{key}.prefab")
    with open(sub_p, "w", encoding="utf-8") as f:
        f.write(yaml_txt)
    if not os.path.exists(sub_p + ".meta"):
        guid = f"d{hash(key + '_sub') & 0xffffffffffffffff:031x}"
        meta = f"fileFormatVersion: 2\nguid: {guid}\nPrefabImporter:\n  externalObjects: {{}}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n"
        with open(sub_p + ".meta", "w", encoding="utf-8") as f:
            f.write(meta)

print("Successfully generated all 11 effect prefabs in Assets/prefabs/ and Assets/prefabs/Effects/")
