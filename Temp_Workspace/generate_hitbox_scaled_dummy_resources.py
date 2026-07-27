import os
from PIL import Image, ImageDraw, ImageFont

BASE_DIR = r"c:\Users\PC\Projects\TP2\Assets\Textures\Characters"

PLAYER_DIR = os.path.join(BASE_DIR, "Player")
GARON_DIR = os.path.join(BASE_DIR, "Bosses", "Garon")

os.makedirs(PLAYER_DIR, exist_ok=True)
os.makedirs(GARON_DIR, exist_ok=True)

try:
    font_player = ImageFont.truetype("arial.ttf", 12)
    font_garon = ImageFont.truetype("arial.ttf", 18)
except Exception:
    font_player = ImageFont.load_default()
    font_garon = ImageFont.load_default()

def create_hitbox_dummy_sheet(motion_name, frame_count, width, height, bg_color, border_color, text_color, font, output_path):
    sheet_width = width * frame_count
    sheet = Image.new("RGBA", (sheet_width, height), (0, 0, 0, 0))
    
    for i in range(frame_count):
        frame_num = i + 1
        frame_img = Image.new("RGBA", (width, height), bg_color)
        draw = ImageDraw.Draw(frame_img)
        
        # Outer Bounding Rectangle (Hitbox boundary)
        draw.rectangle([2, 2, width - 3, height - 3], outline=border_color, width=3)
        
        # Inner Capsule Collider visual guideline
        margin_x = int(width * 0.15)
        margin_y = int(height * 0.05)
        draw.rounded_rectangle([margin_x, margin_y, width - margin_x, height - margin_y], radius=int(width*0.2), outline=(255, 255, 255, 180), width=2)
        
        # Text label: MotionName_FrameNum
        text = f"{motion_name}_{frame_num}"
        subtext = f"({width}x{height}px)"
        
        # Main text bbox
        bbox = draw.textbbox((0, 0), text, font=font)
        text_w = bbox[2] - bbox[0]
        text_h = bbox[3] - bbox[1]
        x = (width - text_w) // 2
        y = (height - text_h) // 2 - 10
        
        # Sub text bbox
        bbox_sub = draw.textbbox((0, 0), subtext, font=font)
        sub_w = bbox_sub[2] - bbox_sub[0]
        sub_h = bbox_sub[3] - bbox_sub[1]
        sub_x = (width - sub_w) // 2
        sub_y = y + text_h + 8
        
        # Draw background pill for text readability
        draw.rectangle([min(x, sub_x) - 6, y - 4, max(x + text_w, sub_x + sub_w) + 6, sub_y + sub_h + 4], fill=(15, 15, 25, 230))
        draw.text((x, y), text, fill=text_color, font=font)
        draw.text((sub_x, sub_y), subtext, fill=(200, 220, 240, 255), font=font)
        
        # Pivot point visual marker (Bottom-Center)
        pivot_x = width // 2
        pivot_y = height - 6
        draw.ellipse([pivot_x - 5, pivot_y - 5, pivot_x + 5, pivot_y + 5], fill=(255, 50, 50, 255), outline=(255, 255, 255, 255))
        
        sheet.paste(frame_img, (i * width, 0))
        
    sheet.save(output_path, "PNG")
    print(f"[Hitbox Dummy Generator] Saved: {output_path} ({frame_count} frames, {width}x{height}px)")

def main():
    print("=== Generating Hitbox-Scaled Dummy Placeholder Resources (Player 128x256px, Garon 256x512px) ===")
    
    # 1. Player Dummy Motions (128 x 256 px Hitbox)
    player_bg = (15, 35, 55, 230)
    player_border = (0, 220, 255, 255)
    player_text = (0, 245, 255, 255)
    
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
        create_hitbox_dummy_sheet(item["name"], item["frames"], 128, 256, player_bg, player_border, player_text, font_player, out)

    # 2. Garon Boss Dummy Motions (256 x 512 px Hitbox)
    garon_bg = (55, 15, 25, 230)
    garon_border = (255, 60, 70, 255)
    garon_text = (255, 110, 120, 255)
    
    garon_items = [
        {"name": "Garon_Idle", "frames": 8},
        {"name": "Garon_Move", "frames": 8},
        {"name": "Garon_Jump", "frames": 8},
        {"name": "Garon_Death", "frames": 8},
        {"name": "Garon_Pattern_Charge", "frames": 16},
        {"name": "Garon_Pattern_ComboSlash", "frames": 16},
        {"name": "Garon_Pattern_OverheadSmash", "frames": 16},
        {"name": "Garon_Pattern_Shockwave", "frames": 16}
    ]
    
    for item in garon_items:
        out = os.path.join(GARON_DIR, f"{item['name']}.png")
        create_hitbox_dummy_sheet(item["name"], item["frames"], 256, 512, garon_bg, garon_border, garon_text, font_garon, out)

    print("\n=== All Hitbox-Scaled Dummy Placeholder Resources Successfully Built ===")

if __name__ == "__main__":
    main()
