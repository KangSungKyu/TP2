import os
from PIL import Image, ImageDraw, ImageFont

ENV_DIR = r"c:\Users\PC\Projects\TP2\Assets\Textures\Environment"
os.makedirs(ENV_DIR, exist_ok=True)

try:
    font_small = ImageFont.truetype("arial.ttf", 9)
    font_med = ImageFont.truetype("arial.ttf", 11)
    font_large = ImageFont.truetype("arial.ttf", 13)
except Exception:
    font_small = ImageFont.load_default()
    font_med = ImageFont.load_default()
    font_large = ImageFont.load_default()

def create_tile_grid_sheet(tiles, tile_w, tile_h, cols, rows, output_path):
    sheet_w = tile_w * cols
    sheet_h = tile_h * rows
    sheet = Image.new("RGBA", (sheet_w, sheet_h), (0, 0, 0, 0))
    
    for idx, tile in enumerate(tiles):
        col = idx % cols
        row = idx // cols
        x = col * tile_w
        y = row * tile_h
        
        tile_img = Image.new("RGBA", (tile_w, tile_h), tile["bg"])
        draw = ImageDraw.Draw(tile_img)
        
        # Border
        draw.rectangle([1, 1, tile_w - 2, tile_h - 2], outline=tile["border"], width=2)
        
        # Center Label Text
        text = tile["name"]
        bbox = draw.textbbox((0, 0), text, font=font_small)
        tw = bbox[2] - bbox[0]
        th = bbox[3] - bbox[1]
        tx = (tile_w - tw) // 2
        ty = (tile_h - th) // 2
        
        draw.rectangle([tx - 2, ty - 1, tx + tw + 2, ty + th + 1], fill=(10, 10, 15, 220))
        draw.text((tx, ty), text, fill=tile["text"], font=font_small)
        
        sheet.paste(tile_img, (x, y))
        
    sheet.save(output_path, "PNG")
    print(f"[Chapter Tiles Generator] Saved: {output_path} ({cols}x{rows} grid, tile: {tile_w}x{tile_h}px)")

def create_portal_sheet(output_path):
    width, height = 64, 64
    frame_count = 8
    sheet_width = width * frame_count
    sheet = Image.new("RGBA", (sheet_width, height), (0, 0, 0, 0))
    
    bg_color = (25, 15, 45, 230)
    border_color = (180, 80, 255, 255)
    portal_cyan = (0, 240, 255, 255)
    
    for i in range(frame_count):
        frame_num = i + 1
        frame_img = Image.new("RGBA", (width, height), (0, 0, 0, 0))
        draw = ImageDraw.Draw(frame_img)
        
        # Portal Arch Frame
        draw.rectangle([4, 4, width - 5, height - 5], fill=bg_color, outline=border_color, width=3)
        
        # Inner Swirling Portal Vortex
        margin = 12 - (i % 4) * 2
        draw.ellipse([margin, margin, width - margin, height - margin], outline=portal_cyan, width=3)
        
        # Label: Portal_F1~F8
        text = f"Portal_F{frame_num}"
        bbox = draw.textbbox((0, 0), text, font=font_small)
        tw = bbox[2] - bbox[0]
        th = bbox[3] - bbox[1]
        tx = (width - tw) // 2
        ty = (height - th) // 2
        
        draw.rectangle([tx - 3, ty - 1, tx + tw + 3, ty + th + 1], fill=(15, 10, 25, 230))
        draw.text((tx, ty), text, fill=(255, 230, 100, 255), font=font_small)
        
        sheet.paste(frame_img, (i * width, 0))
        
    sheet.save(output_path, "PNG")
    print(f"[Portal Sheet Generator] Saved: {output_path} (8 frames, {width}x{height}px)")

def main():
    print("=== Generating Chapter Theme Tilesets & Portal Gate Resources (Independent) ===")
    
    # 1. Chapter 1 Tao-Punk Neon Shrine Tileset (32x32px, 4x2 grid)
    ch1_tiles = [
        {"name": "Shrine_Gnd", "bg": (15, 45, 45, 230), "border": (0, 255, 220, 255), "text": (180, 255, 240, 255)},
        {"name": "Shrine_Wall", "bg": (20, 35, 40, 230), "border": (0, 200, 180, 255), "text": (150, 235, 220, 255)},
        {"name": "Shrine_Pillar", "bg": (25, 40, 45, 230), "border": (0, 220, 200, 255), "text": (170, 245, 230, 255)},
        {"name": "Shrine_Talisman", "bg": (50, 35, 15, 230), "border": (255, 180, 40, 255), "text": (255, 220, 120, 255)},
        {"name": "Shrine_Plat", "bg": (10, 40, 50, 230), "border": (0, 220, 240, 255), "text": (160, 235, 255, 255)},
        {"name": "Shrine_Lantern", "bg": (60, 25, 15, 230), "border": (255, 80, 40, 255), "text": (255, 160, 120, 255)},
        {"name": "Shrine_BG", "bg": (10, 25, 30, 230), "border": (40, 120, 140, 255), "text": (120, 180, 200, 255)},
        {"name": "Shrine_Door", "bg": (35, 30, 20, 230), "border": (200, 150, 60, 255), "text": (230, 190, 100, 255)}
    ]
    create_tile_grid_sheet(ch1_tiles, 32, 32, 4, 2, os.path.join(ENV_DIR, "Tile_Chapter1_TaoShrine.png"))

    # 2. Chapter 2 Cyber Ruins Tileset (32x32px, 4x2 grid)
    ch2_tiles = [
        {"name": "Ruins_Gnd", "bg": (45, 25, 35, 230), "border": (220, 80, 160, 255), "text": (255, 180, 220, 255)},
        {"name": "Ruins_Wall", "bg": (35, 20, 30, 230), "border": (180, 60, 130, 255), "text": (235, 150, 190, 255)},
        {"name": "Ruins_Pipe", "bg": (30, 30, 40, 230), "border": (120, 140, 180, 255), "text": (180, 200, 240, 255)},
        {"name": "Ruins_Wire", "bg": (40, 35, 15, 230), "border": (220, 180, 40, 255), "text": (240, 210, 100, 255)},
        {"name": "Ruins_Plat", "bg": (40, 20, 45, 230), "border": (200, 70, 220, 255), "text": (245, 160, 255, 255)},
        {"name": "Ruins_Steam", "bg": (20, 40, 50, 230), "border": (60, 200, 220, 255), "text": (150, 230, 250, 255)},
        {"name": "Ruins_BG", "bg": (20, 15, 25, 230), "border": (100, 50, 90, 255), "text": (180, 120, 160, 255)},
        {"name": "Ruins_Gate", "bg": (50, 20, 30, 230), "border": (240, 70, 100, 255), "text": (255, 160, 180, 255)}
    ]
    create_tile_grid_sheet(ch2_tiles, 32, 32, 4, 2, os.path.join(ENV_DIR, "Tile_Chapter2_CyberRuins.png"))

    # 3. Room Transition Portal Gate Sprite Sheet (64x64px, 8 frames)
    create_portal_sheet(os.path.join(ENV_DIR, "Sprite_Portal_Gate.png"))

    print("\n=== All Chapter Theme Tilesets & Portal Gate Resources Successfully Built ===")

if __name__ == "__main__":
    main()
