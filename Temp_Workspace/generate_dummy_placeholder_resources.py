import os
from PIL import Image, ImageDraw, ImageFont

BASE_DIR = r"c:\Users\PC\Projects\TP2\Assets\Textures\Characters"

PLAYER_DIR = os.path.join(BASE_DIR, "Player")
GARON_DIR = os.path.join(BASE_DIR, "Bosses", "Garon")

os.makedirs(PLAYER_DIR, exist_ok=True)
os.makedirs(GARON_DIR, exist_ok=True)

# Font loading (default PIL font if truetype unavailable)
try:
    font = ImageFont.truetype("arial.ttf", 11)
except Exception:
    font = ImageFont.load_default()

def create_dummy_sheet(motion_name, frame_count, width, height, bg_color, border_color, text_color, output_path):
    sheet_width = width * frame_count
    sheet = Image.new("RGBA", (sheet_width, height), (0, 0, 0, 0))
    
    for i in range(frame_count):
        frame_num = i + 1
        frame_img = Image.new("RGBA", (width, height), bg_color)
        draw = ImageDraw.Draw(frame_img)
        
        # Draw border
        draw.rectangle([2, 2, width - 3, height - 3], outline=border_color, width=2)
        
        # Text label: MotionName_FrameNum
        text = f"{motion_name}_{frame_num}"
        
        # Calculate centered text position
        bbox = draw.textbbox((0, 0), text, font=font)
        text_w = bbox[2] - bbox[0]
        text_h = bbox[3] - bbox[1]
        x = (width - text_w) // 2
        y = (height - text_h) // 2
        
        # Draw background pill for text legibility
        draw.rectangle([x - 4, y - 2, x + text_w + 4, y + text_h + 2], fill=(20, 20, 30, 220))
        draw.text((x, y), text, fill=text_color, font=font)
        
        sheet.paste(frame_img, (i * width, 0))
        
    sheet.save(output_path, "PNG")
    print(f"[Dummy Generator] Saved: {output_path} ({frame_count} frames, {width}x{height})")

def main():
    print("=== Generating Dummy Placeholder Resources for Player and Garon ===")
    
    # Player Dummy Motions (Cyan / Dark Charcoal theme)
    player_bg = (15, 30, 45, 230)
    player_border = (0, 220, 255, 255)
    player_text = (0, 240, 255, 255)
    
    player_items = [
        {"name": "Player_Idle", "frames": 8},
        {"name": "Player_Run", "frames": 8},
        {"name": "Player_Parry", "frames": 16},
        {"name": "Player_Guard", "frames": 8},
        {"name": "Player_Dodge", "frames": 16},
        {"name": "Player_Jump", "frames": 16},
        {"name": "Player_Attack_Hit1", "frames": 16},
        {"name": "Player_Attack_Hit2", "frames": 16},
        {"name": "Player_Attack_Hit3", "frames": 16},
        {"name": "Player_Execution", "frames": 16}
    ]
    
    for item in player_items:
        out = os.path.join(PLAYER_DIR, f"{item['name']}.png")
        create_dummy_sheet(item["name"], item["frames"], 128, 128, player_bg, player_border, player_text, out)

    # Garon Dummy Motions (Crimson Red / Dark Charcoal theme)
    garon_bg = (45, 15, 20, 230)
    garon_border = (255, 50, 60, 255)
    garon_text = (255, 100, 110, 255)
    
    garon_items = [
        {"name": "Garon_Idle", "frames": 8},
        {"name": "Garon_Move", "frames": 8},
        {"name": "Garon_Jump", "frames": 8},
        {"name": "Garon_Death", "frames": 8},
        {"name": "Garon_Pattern_Charge", "frames": 8},
        {"name": "Garon_Pattern_ComboSlash", "frames": 8},
        {"name": "Garon_Pattern_OverheadSmash", "frames": 8},
        {"name": "Garon_Pattern_Shockwave", "frames": 8}
    ]
    
    for item in garon_items:
        out = os.path.join(GARON_DIR, f"{item['name']}.png")
        create_dummy_sheet(item["name"], item["frames"], 128, 128, garon_bg, garon_border, garon_text, out)

    print("\n=== Dummy Placeholder Resources Successfully Generated ===")

if __name__ == "__main__":
    main()
