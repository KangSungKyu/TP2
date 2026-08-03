import os, re, csv

rooms_dir = r"c:\Users\PC\Projects\TP2\Assets\Prefabs\Rooms"
datas_dir = r"c:\Users\PC\Projects\TP2\Assets\Datas"
os.makedirs(rooms_dir, exist_ok=True)

# 1. Generate 3 Room Prefabs (Entry, Battle, Boss)
room_names = ["Tilemap_Room_Stage1_Entry", "Tilemap_Room_Stage1_Battle", "Tilemap_Room_Stage1_Boss"]

sample_tilemap_prefab = """%YAML 1.1
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
  - component: {{fileID: 15600000}}
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
--- !u!15600000 &15600000
Grid:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: 100000}}
  m_Enabled: 1
  m_CellSize: {{x: 1, y: 1, z: 0}}
  m_CellGap: {{x: 0, y: 0, z: 0}}
  m_CellLayout: 0
  m_CellSwizzle: 0
"""

meta_template = "fileFormatVersion: 2\nguid: {guid}\nPrefabImporter:\n  externalObjects: {{}}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n"

for rname in room_names:
    p_path = os.path.join(rooms_dir, f"{rname}.prefab")
    m_path = p_path + ".meta"
    
    with open(p_path, "w", encoding="utf-8") as f:
        f.write(sample_tilemap_prefab.format(name=rname))
        
    guid = f"r{hash(rname) & 0xffffffffffffffff:031x}"
    with open(m_path, "w", encoding="utf-8") as f:
        f.write(meta_template.format(guid=guid))
        
    print(f"Generated Room Prefab: {rname}.prefab (GUID: {guid})")

# 2. Update ResourceData.csv
res_path = os.path.join(datas_dir, "ResourceData.csv")
res_entries = [
    ("1040", "Tilemap_Room_Stage1_Entry"),
    ("1041", "Tilemap_Room_Stage1_Battle"),
    ("1042", "Tilemap_Room_Stage1_Boss")
]

with open(res_path, "r", encoding="utf-8") as f:
    res_lines = [line.strip() for line in f if line.strip()]

existing_idxs = set()
for line in res_lines[1:]:
    parts = line.split(",")
    if parts: existing_idxs.add(parts[0])

for idx, path_val in res_entries:
    if idx not in existing_idxs:
        res_lines.append(f"{idx},{path_val}")

with open(res_path, "w", encoding="utf-8") as f:
    f.write("\n".join(res_lines) + "\n")
print("Updated ResourceData.csv with Stage 1 room chunk idxs!")

# 3. Update StageData.csv
stage_path = os.path.join(datas_dir, "StageData.csv")
new_stage_header = "idx,nametextidx,chapter,themetype,startroomidx,bossroomidx,roomsequenceidxlist"
new_stage_data = "9001,2001,1,TaoShrine,1040,1042,1040_1041_1042"

with open(stage_path, "w", encoding="utf-8") as f:
    f.write(f"{new_stage_header}\n{new_stage_data}\n")
print("Updated StageData.csv with int idx specification!")
