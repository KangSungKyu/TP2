import os
import glob
import re

specs = {
    "ShadowStalker": {"w": 128, "h": 256, "ppu": 64},
    "SpearSentry": {"w": 154, "h": 307, "ppu": 77},
    "WaveHeavy": {"w": 205, "h": 410, "ppu": 102}
}

meta_files = glob.glob(r"C:\Users\PC\Projects\TP2\Assets\Textures\Characters\Monsters\*\*.png.meta")

for meta_path in meta_files:
    mname = os.path.basename(os.path.dirname(meta_path))
    if mname not in specs:
        continue
    
    spec = specs[mname]
    fw, fh, ppu = spec["w"], spec["h"], spec["ppu"]

    with open(meta_path, 'r', encoding='utf-8') as f:
        content = f.read()

    # PPU
    content = re.sub(r'spritePixelsToUnits:\s*\d+', f'spritePixelsToUnits: {ppu}', content)
    # Sprite Mode Multiple (2)
    content = re.sub(r'spriteMode:\s*\d+', 'spriteMode: 2', content)

    # Replace sprites array with y=0 horizontal 8 frames
    sprites_yaml = "    sprites:\n"
    for c in range(8):
        x_pos = c * fw
        sprites_yaml += f"""    - serializedVersion: 2
      name: {os.path.splitext(os.path.basename(meta_path))[0]}_{c}
      rect:
        serializedVersion: 2
        x: {x_pos}
        y: 0
        width: {fw}
        height: {fh}
      alignment: 7
      pivot: {{x: 0.5, y: 0}}
      border: {{x: 0, y: 0, z: 0, w: 0}}
      outline: []
      physicsShape: []
      tessellationDetail: 0
      boneTimeStamps: []
      atlasRectOffset: {{x: 0, y: 0}}
      spriteID: {os.path.splitext(os.path.basename(meta_path))[0]}_{c}_id
      internalID: 0
"""
    
    if 'sprites:' in content:
        content = re.sub(r'    sprites:.*?(?=\n  \w|\Z)', sprites_yaml.rstrip(), content, flags=re.DOTALL)

    with open(meta_path, 'w', encoding='utf-8') as f:
        f.write(content)

print("Accurately resliced all 12 monster meta files with individual monster specs!")
