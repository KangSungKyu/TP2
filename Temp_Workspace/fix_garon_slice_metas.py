import os, glob, re
from PIL import Image

garon_tex_dir = r"c:\Users\PC\Projects\TP2\Assets\Textures\Characters\Bosses\Garon"
print("=== REPAIRING GARON SPRITE SLICE METAS (256x512 CELL SIZE) ===")

garon_pngs = glob.glob(os.path.join(garon_tex_dir, "*.png"))

for png_path in garon_pngs:
    meta_path = png_path + ".meta"
    if not os.path.exists(meta_path):
        continue
        
    im = Image.open(png_path)
    w, h = im.size
    cell_w, cell_h = 256, 512
    cols = w // cell_w
    
    anim_name = os.path.basename(png_path).replace(".png", "")
    print(f"Processing: {anim_name} ({w}x{h} px) -> {cols} frames (Cell: {cell_w}x{cell_h})")
    
    # Generate exact YAML for m_Sprites
    sprites_yaml = "  m_Sprites:\n"
    for i in range(cols):
        sub_name = f"{anim_name}_{i}"
        sub_guid = f"b{hash(sub_name) & 0xffffffffffffffff:031x}"
        x_pos = i * cell_w
        sprites_yaml += f"""  - serializedVersion: 2
    name: {sub_name}
    rect:
      serializedVersion: 2
      x: {x_pos}
      y: 0
      width: {cell_w}
      height: {cell_h}
    alignment: 7
    pivot: {{x: 0.5, y: 0}}
    border: {{x: 0, y: 0, z: 0, w: 0}}
    outline: []
    physicsShape: []
    tessellationDetail: 0
    glyph: 0
    isModifiable: 1
    nameFileIdTable: {{}}
    internalID: {21300000 + i * 2}
"""

    with open(meta_path, "r", encoding="utf-8") as f:
        meta_txt = f.read()
        
    # Replace spriteSheet section or inject
    # Ensure spriteMode: 2 (Multiple), spritePixelsToUnits: 128, filterMode: 0 (Point)
    meta_txt = re.sub(r"spriteMode: \d+", "spriteMode: 2", meta_txt)
    meta_txt = re.sub(r"spritePixelsToUnits: \d+", "spritePixelsToUnits: 128", meta_txt)
    meta_txt = re.sub(r"filterMode: -?\d+", "filterMode: 0", meta_txt)
    
    # Replace m_SpriteSheet in YAML
    if "spriteSheet:" in meta_txt:
        meta_txt = re.sub(r"  spriteSheet:\s+serializedVersion: 2.*?(?=  outline:|\n\n|\Z)", f"  spriteSheet:\n    serializedVersion: 2\n{sprites_yaml}", meta_txt, flags=re.DOTALL)
    else:
        meta_txt += f"\n  spriteSheet:\n    serializedVersion: 2\n{sprites_yaml}"
        
    with open(meta_path, "w", encoding="utf-8") as f:
        f.write(meta_txt)
        
    print(f" -> Successfully repaired meta slice for {anim_name}!")

print("\nAll Garon sprite sheet metas have been perfectly repaired!")
