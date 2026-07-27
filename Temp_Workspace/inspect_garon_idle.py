import os, re
from PIL import Image

garon_idle_png = r"c:\Users\PC\Projects\TP2\Assets\Textures\Characters\Bosses\Garon\Garon_Idle.png"
garon_idle_meta = garon_idle_png + ".meta"

print("=== INSPECTING GARON_IDLE TEXTURE & META ===")

if os.path.exists(garon_idle_png):
    im = Image.open(garon_idle_png)
    print(f"PNG File Size: {im.size[0]} x {im.size[1]} px")

if os.path.exists(garon_idle_meta):
    with open(garon_idle_meta, "r", encoding="utf-8") as f:
        meta_txt = f.read()
    
    # Check spriteMode, PPU, filterMode
    mode = re.search(r"spriteMode: (\d+)", meta_txt)
    ppu = re.search(r"spritePixelsToUnits: (\d+)", meta_txt)
    filter_mode = re.search(r"filterMode: (-?\d+)", meta_txt)
    
    print(f"spriteMode: {mode.group(1) if mode else 'N/A'}")
    print(f"spritePixelsToUnits: {ppu.group(1) if ppu else 'N/A'}")
    print(f"filterMode: {filter_mode.group(1) if filter_mode else 'N/A'}")
    
    # Extract sliced rects
    rects = re.findall(r"rect:\s+serializedVersion: 2\n\s+x: ([\d.]+)\n\s+y: ([\d.]+)\n\s+width: ([\d.]+)\n\s+height: ([\d.]+)", meta_txt)
    print(f"\nSliced Rects Count: {len(rects)}")
    for i, r in enumerate(rects):
        print(f" - SubSprite [{i}]: x={r[0]}, y={r[1]}, w={r[2]}, h={r[3]}")
