import os, glob, re

env_dir = r"c:\Users\PC\Projects\TP2\Assets\Textures\Environment"
mat_dir = r"c:\Users\PC\Projects\TP2\Assets\Materials\Physics"
os.makedirs(env_dir, exist_ok=True)
os.makedirs(mat_dir, exist_ok=True)

specs = [
    ("Tile_Terrain_Ground.png", 32, 32, 4, 3, 32),
    ("Tile_Platform_OneWay.png", 32, 32, 3, 1, 32),
    ("Tile_Hazard_SpikesLava.png", 32, 32, 3, 1, 32),
    ("Tile_Background_Deco.png", 32, 32, 4, 1, 32),
    ("Sprite_Structures_Interactive.png", 64, 64, 5, 1, 64)
]

for file_name, cw, ch, cols, rows, ppu in specs:
    png_p = os.path.join(env_dir, file_name)
    meta_p = png_p + ".meta"
    base_name = file_name.replace(".png", "")
    
    sprites_yaml = "  m_Sprites:\n"
    idx = 0
    for r in range(rows):
        for c in range(cols):
            sub_name = f"{base_name}_{idx}"
            x_pos = c * cw
            y_pos = (rows - 1 - r) * ch
            sprites_yaml += f"""  - serializedVersion: 2
    name: {sub_name}
    rect:
      serializedVersion: 2
      x: {x_pos}
      y: {y_pos}
      width: {cw}
      height: {ch}
    alignment: 0
    pivot: {{x: 0.5, y: 0.5}}
    border: {{x: 0, y: 0, z: 0, w: 0}}
    outline: []
    physicsShape: []
    tessellationDetail: 0
    glyph: 0
    isModifiable: 1
    nameFileIdTable: {{}}
    internalID: {21300000 + idx * 2}
"""
            idx += 1

    with open(meta_p, "r", encoding="utf-8") as f:
        txt = f.read()

    txt = re.sub(r"spriteMode: \d+", "spriteMode: 2", txt)
    txt = re.sub(r"spritePixelsToUnits: \d+", f"spritePixelsToUnits: {ppu}", txt)
    txt = re.sub(r"filterMode: -?\d+", "filterMode: 0", txt)

    if "spriteSheet:" in txt:
        txt = re.sub(r"  spriteSheet:\s+serializedVersion: 2.*?(?=  outline:|\n\n|\Z)", f"  spriteSheet:\n    serializedVersion: 2\n{sprites_yaml}", txt, flags=re.DOTALL)
    else:
        txt += f"\n  spriteSheet:\n    serializedVersion: 2\n{sprites_yaml}"

    with open(meta_p, "w", encoding="utf-8") as f:
        f.write(txt)

    print(f"Repaired Environment Meta ({idx} slices): {base_name}")

# Build PhysicsMaterial2D Assets
ground_mat_yaml = """%YAML 1.1
%TAG !u! tag:unity3d.com,2011:
--- !u!62 &6200000
PhysicsMaterial2D:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  m_Name: GroundPhysicsMaterial
  serializedVersion: 2
  friction: 0.4
  bounciness: 0
"""

wall_mat_yaml = """%YAML 1.1
%TAG !u! tag:unity3d.com,2011:
--- !u!62 &6200000
PhysicsMaterial2D:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  m_Name: WallPhysicsMaterial
  serializedVersion: 2
  friction: 0
  bounciness: 0
"""

g_mat_p = os.path.join(mat_dir, "GroundPhysicsMaterial.physicsMaterial2D")
w_mat_p = os.path.join(mat_dir, "WallPhysicsMaterial.physicsMaterial2D")

with open(g_mat_p, "w", encoding="utf-8") as f: f.write(ground_mat_yaml)
with open(w_mat_p, "w", encoding="utf-8") as f: f.write(wall_mat_yaml)

g_guid = f"g{hash('GroundPhysicsMaterial') & 0xffffffffffffffff:031x}"
w_guid = f"h{hash('WallPhysicsMaterial') & 0xffffffffffffffff:031x}"

with open(g_mat_p + ".meta", "w", encoding="utf-8") as f:
    f.write(f"fileFormatVersion: 2\nguid: {g_guid}\nNativeFormatImporter:\n  externalObjects: {{}}\n  mainObjectFileID: 6200000\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n")

with open(w_mat_p + ".meta", "w", encoding="utf-8") as f:
    f.write(f"fileFormatVersion: 2\nguid: {w_guid}\nNativeFormatImporter:\n  externalObjects: {{}}\n  mainObjectFileID: 6200000\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n")

print("Created Ground & Wall PhysicsMaterial2D assets and metas!")
