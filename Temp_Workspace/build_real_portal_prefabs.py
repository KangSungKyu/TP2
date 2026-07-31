import os, re

struct_dir = r"c:\Users\PC\Projects\TP2\Assets\Prefabs\Structures"
os.makedirs(struct_dir, exist_ok=True)

portal_p = os.path.join(struct_dir, "Portal.prefab")
door_p = os.path.join(struct_dir, "Door.prefab")
gate_p = os.path.join(struct_dir, "Portal_Gate.prefab")

# Get Sprite_Structures_Interactive ref
meta_path = r"c:\Users\PC\Projects\TP2\Assets\Textures\Environment\Sprite_Structures_Interactive.png.meta"
guid, file_id = "", "21300000"
if os.path.exists(meta_path):
    with open(meta_path, "r", encoding="utf-8") as f:
        txt = f.read()
    m_guid = re.search(r"guid: ([a-f0-9]+)", txt)
    if m_guid: guid = m_guid.group(1)
    m_id = re.search(r"internalID: (\d+)", txt)
    if m_id: file_id = m_id.group(1)

# Get Sprite_Portal_Gate ref
gate_meta_path = r"c:\Users\PC\Projects\TP2\Assets\Textures\Environment\Sprite_Portal_Gate.png.meta"
g_guid, g_file_id = guid, file_id
if os.path.exists(gate_meta_path):
    with open(gate_meta_path, "r", encoding="utf-8") as f:
        txt = f.read()
    m_guid = re.search(r"guid: ([a-f0-9]+)", txt)
    if m_guid: g_guid = m_guid.group(1)
    m_id = re.search(r"internalID: (\d+)", txt)
    if m_id: g_file_id = m_id.group(1)

portal_yaml_template = """%YAML 1.1
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
  - component: {{fileID: 6100000}}
  m_Layer: 0
  m_Name: {name}
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
  m_SortingOrder: 0
  m_Sprite: {{fileID: {fid}, guid: {gid}, type: 3}}
  m_Color: {{r: 1, g: 1, b: 1, a: 1}}
  m_FlipX: 0
  m_FlipY: 0
  m_DrawMode: 0
  m_Size: {{x: 1, y: 2}}
  m_AdaptiveModeThreshold: 0.5
  m_SpriteTileMode: 0
  m_WasSpriteAssigned: 1
  m_MaskInteraction: 0
  m_SpriteSortPoint: 0
--- !u!61 &6100000
BoxCollider2D:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: 100000}}
  m_Enabled: 1
  m_Density: 1
  m_Material: {{fileID: 0}}
  m_IncludeLayers:
    serializedVersion: 2
    m_Bits: 0
  m_ExcludeLayers:
    serializedVersion: 2
    m_Bits: 0
  m_LayerOverridePriority: 0
  m_IsTrigger: 1
  m_UsedByEffector: 0
  m_UsedByComposite: 0
  m_Offset: {{x: 0, y: 0}}
  m_SpriteTilingProperty:
    border: {{x: 0, y: 0, z: 0, w: 0}}
    pivot: {{x: 0.5, y: 0.5}}
    oldSize: {{x: 1, y: 2}}
    newSize: {{x: 1, y: 2}}
    adaptiveTilingThreshold: 0.5
    drawMode: 0
    adaptiveMode: 0
  m_AutoTiling: 0
  m_Size: {{x: 1, y: 2}}
  m_EdgeRadius: 0
"""

with open(portal_p, "w", encoding="utf-8") as f: f.write(portal_yaml_template.format(name="Portal", fid=file_id, gid=guid))
with open(door_p, "w", encoding="utf-8") as f: f.write(portal_yaml_template.format(name="Door", fid=file_id, gid=guid))
with open(gate_p, "w", encoding="utf-8") as f: f.write(portal_yaml_template.format(name="Portal_Gate", fid=g_file_id, gid=g_guid))

p_guid = f"p{hash('Portal_Prefab') & 0xffffffffffffffff:031x}"
d_guid = f"d{hash('Door_Prefab') & 0xffffffffffffffff:031x}"
g_guid_meta = f"g{hash('Portal_Gate_Prefab') & 0xffffffffffffffff:031x}"

meta_temp = "fileFormatVersion: 2\nguid: {guid}\nPrefabImporter:\n  externalObjects: {{}}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n"
with open(portal_p + ".meta", "w", encoding="utf-8") as f: f.write(meta_temp.format(guid=p_guid))
with open(door_p + ".meta", "w", encoding="utf-8") as f: f.write(meta_temp.format(guid=d_guid))
with open(gate_p + ".meta", "w", encoding="utf-8") as f: f.write(meta_temp.format(guid=g_guid_meta))

print("Successfully generated Portal.prefab, Door.prefab, and Portal_Gate.prefab!")
