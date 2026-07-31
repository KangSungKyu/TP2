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
    print(f"[Special Tiles Generator] Saved: {output_path} ({cols}x{rows} grid, tile: {tile_w}x{tile_h}px)")

def main():
    print("=== Generating Special Wall Tiles & 60x30 Large Stage Resources (Independent) ===")
    
    # 1. Special Walls Tiles (NoJump Red Wall, Ice Wall) - 32x32px, 4x2 grid
    special_wall_tiles = [
        {"name": "NoJump_Top", "bg": (65, 15, 20, 230), "border": (255, 40, 50, 255), "text": (255, 180, 180, 255)},
        {"name": "NoJump_Mid", "bg": (60, 10, 15, 230), "border": (240, 30, 40, 255), "text": (255, 160, 160, 255)},
        {"name": "NoJump_Bot", "bg": (55, 10, 15, 230), "border": (220, 20, 30, 255), "text": (255, 140, 140, 255)},
        {"name": "NoJump_Warn", "bg": (70, 20, 10, 230), "border": (255, 100, 0, 255), "text": (255, 200, 100, 255)},
        {"name": "Ice_Top", "bg": (15, 45, 65, 230), "border": (0, 220, 255, 255), "text": (180, 245, 255, 255)},
        {"name": "Ice_Mid", "bg": (10, 40, 60, 230), "border": (0, 200, 240, 255), "text": (160, 235, 255, 255)},
        {"name": "Ice_Bot", "bg": (10, 35, 55, 230), "border": (0, 180, 220, 255), "text": (140, 225, 255, 255)},
        {"name": "Ice_Slick", "bg": (20, 55, 75, 230), "border": (100, 240, 255, 255), "text": (200, 250, 255, 255)}
    ]
    create_tile_grid_sheet(special_wall_tiles, 32, 32, 4, 2, os.path.join(ENV_DIR, "Tile_Terrain_SpecialWalls.png"))

    # 2. 60x30 Large Stage 3-Zone Tileset - 32x32px, 6x2 grid
    large_stage_tiles = [
        {"name": "Z1_Move_Gnd", "bg": (20, 45, 30, 230), "border": (0, 220, 120, 255), "text": (180, 255, 200, 255)},
        {"name": "Z1_Move_Plat", "bg": (15, 40, 55, 230), "border": (0, 200, 240, 255), "text": (160, 235, 255, 255)},
        {"name": "Z2_Wall_Normal", "bg": (40, 40, 50, 230), "border": (160, 160, 200, 255), "text": (220, 220, 255, 255)},
        {"name": "Z2_Wall_Red", "bg": (65, 15, 20, 230), "border": (255, 40, 50, 255), "text": (255, 180, 180, 255)},
        {"name": "Z3_Arena_Floor", "bg": (50, 20, 25, 230), "border": (255, 80, 90, 255), "text": (255, 180, 190, 255)},
        {"name": "Z3_Arena_Gate", "bg": (55, 35, 15, 230), "border": (255, 180, 40, 255), "text": (255, 220, 120, 255)},
        {"name": "Z1_Move_BG", "bg": (15, 25, 20, 230), "border": (40, 120, 70, 255), "text": (120, 200, 150, 255)},
        {"name": "Z1_Move_Deco", "bg": (25, 35, 45, 230), "border": (80, 140, 180, 255), "text": (150, 200, 240, 255)},
        {"name": "Z2_Wall_Ice", "bg": (15, 45, 65, 230), "border": (0, 220, 255, 255), "text": (180, 245, 255, 255)},
        {"name": "Z2_Wall_Trap", "bg": (55, 15, 45, 230), "border": (240, 60, 220, 255), "text": (255, 160, 240, 255)},
        {"name": "Z3_Arena_Pillar", "bg": (45, 15, 20, 230), "border": (220, 60, 70, 255), "text": (255, 160, 170, 255)},
        {"name": "Z3_Arena_BG", "bg": (35, 10, 15, 230), "border": (180, 40, 50, 255), "text": (240, 120, 130, 255)}
    ]
    create_tile_grid_sheet(large_stage_tiles, 32, 32, 6, 2, os.path.join(ENV_DIR, "Tile_Stage_Large60x30.png"))

    print("\n=== All Special Wall Tiles & 60x30 Large Stage Resources Successfully Built ===")

if __name__ == "__main__":
    main()
