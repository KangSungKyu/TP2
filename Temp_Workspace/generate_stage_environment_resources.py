import os
from PIL import Image, ImageDraw, ImageFont

ENV_DIR = r"c:\Users\PC\Projects\TP2\Assets\Textures\Environment"
os.makedirs(ENV_DIR, exist_ok=True)

try:
    font_small = ImageFont.truetype("arial.ttf", 9)
    font_med = ImageFont.truetype("arial.ttf", 11)
except Exception:
    font_small = ImageFont.load_default()
    font_med = ImageFont.load_default()

def create_tile_grid_sheet(tiles, tile_w, tile_h, cols, rows, output_path):
    sheet_w = tile_w * cols
    sheet_h = tile_h * rows
    sheet = Image.new("RGBA", (sheet_w, sheet_h), (0, 0, 0, 0))
    draw_sheet = ImageDraw.Draw(sheet)
    
    for idx, tile in enumerate(tiles):
        col = idx % cols
        row = idx // cols
        x = col * tile_w
        y = row * tile_h
        
        tile_img = Image.new("RGBA", (tile_w, tile_h), tile["bg"])
        draw = ImageDraw.Draw(tile_img)
        
        # Outer Border
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
    print(f"[Stage Env Generator] Saved: {output_path} ({cols}x{rows} grid, tile: {tile_w}x{tile_h}px)")

def main():
    print("=== Generating 2D Metroidvania Stage Environment Placeholder Resources (Independent) ===")
    
    # 1. Terrain RuleTile (Ground, Wall, Slope) - 32x32px per tile, 4x3 grid
    terrain_tiles = [
        {"name": "G_TopLeft", "bg": (30, 60, 30, 230), "border": (0, 255, 120, 255), "text": (180, 255, 200, 255)},
        {"name": "G_TopMid", "bg": (30, 60, 30, 230), "border": (0, 255, 120, 255), "text": (180, 255, 200, 255)},
        {"name": "G_TopRight", "bg": (30, 60, 30, 230), "border": (0, 255, 120, 255), "text": (180, 255, 200, 255)},
        {"name": "Wall_L", "bg": (45, 45, 55, 230), "border": (150, 150, 200, 255), "text": (220, 220, 255, 255)},
        {"name": "G_MidLeft", "bg": (25, 50, 25, 230), "border": (0, 200, 100, 255), "text": (150, 240, 180, 255)},
        {"name": "G_Center", "bg": (20, 40, 20, 230), "border": (0, 180, 80, 255), "text": (130, 220, 160, 255)},
        {"name": "G_MidRight", "bg": (25, 50, 25, 230), "border": (0, 200, 100, 255), "text": (150, 240, 180, 255)},
        {"name": "Wall_R", "bg": (45, 45, 55, 230), "border": (150, 150, 200, 255), "text": (220, 220, 255, 255)},
        {"name": "Slope_L", "bg": (60, 50, 25, 230), "border": (255, 200, 50, 255), "text": (255, 230, 120, 255)},
        {"name": "Slope_R", "bg": (60, 50, 25, 230), "border": (255, 200, 50, 255), "text": (255, 230, 120, 255)},
        {"name": "Ceil_Mid", "bg": (35, 35, 45, 230), "border": (120, 120, 160, 255), "text": (200, 200, 240, 255)},
        {"name": "InnerFill", "bg": (15, 25, 15, 230), "border": (0, 120, 50, 255), "text": (100, 180, 120, 255)}
    ]
    create_tile_grid_sheet(terrain_tiles, 32, 32, 4, 3, os.path.join(ENV_DIR, "Tile_Terrain_Ground.png"))

    # 2. OneWay Platform Tiles (Jump-through) - 32x32px, 3x1 grid
    platform_tiles = [
        {"name": "Plat_L", "bg": (20, 50, 70, 230), "border": (0, 220, 255, 255), "text": (150, 240, 255, 255)},
        {"name": "Plat_Mid", "bg": (20, 50, 70, 230), "border": (0, 220, 255, 255), "text": (150, 240, 255, 255)},
        {"name": "Plat_R", "bg": (20, 50, 70, 230), "border": (0, 220, 255, 255), "text": (150, 240, 255, 255)}
    ]
    create_tile_grid_sheet(platform_tiles, 32, 32, 3, 1, os.path.join(ENV_DIR, "Tile_Platform_OneWay.png"))

    # 3. Hazard Tiles (Spikes, Lava, Traps) - 32x32px, 3x1 grid
    hazard_tiles = [
        {"name": "Spikes", "bg": (60, 15, 20, 230), "border": (255, 50, 60, 255), "text": (255, 150, 150, 255)},
        {"name": "Lava", "bg": (70, 25, 10, 230), "border": (255, 120, 20, 255), "text": (255, 190, 100, 255)},
        {"name": "Trap_Blade", "bg": (50, 20, 40, 230), "border": (220, 80, 200, 255), "text": (255, 150, 240, 255)}
    ]
    create_tile_grid_sheet(hazard_tiles, 32, 32, 3, 1, os.path.join(ENV_DIR, "Tile_Hazard_SpikesLava.png"))

    # 4. Background / Decorative Tiles - 32x32px, 4x1 grid
    bg_tiles = [
        {"name": "BG_Wall", "bg": (20, 20, 30, 230), "border": (80, 80, 120, 255), "text": (160, 160, 200, 255)},
        {"name": "BG_Pillar", "bg": (25, 25, 35, 230), "border": (100, 100, 140, 255), "text": (180, 180, 220, 255)},
        {"name": "FG_Vines", "bg": (15, 35, 20, 230), "border": (50, 180, 80, 255), "text": (140, 230, 160, 255)},
        {"name": "FG_Torch", "bg": (50, 35, 15, 230), "border": (255, 160, 40, 255), "text": (255, 210, 120, 255)}
    ]
    create_tile_grid_sheet(bg_tiles, 32, 32, 4, 1, os.path.join(ENV_DIR, "Tile_Background_Deco.png"))

    # 5. Interactive Structures (Door Closed/Open, Chest Closed/Open, Breakable) - 64x64px, 5x1 grid
    structure_tiles = [
        {"name": "Door_Closed", "bg": (45, 30, 20, 230), "border": (220, 140, 60, 255), "text": (255, 200, 120, 255)},
        {"name": "Door_Open", "bg": (25, 20, 15, 230), "border": (180, 110, 40, 255), "text": (220, 160, 80, 255)},
        {"name": "Chest_Closed", "bg": (55, 45, 15, 230), "border": (255, 215, 0, 255), "text": (255, 235, 120, 255)},
        {"name": "Chest_Open", "bg": (35, 30, 10, 230), "border": (200, 170, 0, 255), "text": (230, 200, 80, 255)},
        {"name": "Breakable", "bg": (40, 40, 40, 230), "border": (180, 180, 180, 255), "text": (230, 230, 230, 255)}
    ]
    create_tile_grid_sheet(structure_tiles, 64, 64, 5, 1, os.path.join(ENV_DIR, "Sprite_Structures_Interactive.png"))

    print("\n=== All 2D Metroidvania Stage Environment Placeholder Resources Built ===")

if __name__ == "__main__":
    main()
