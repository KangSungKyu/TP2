import os
from PIL import Image, ImageDraw, ImageFont

BASE_EFFECT_DIR = r"c:\Users\PC\Projects\TP2\Assets\Textures\Effects"

PLAYER_FX_DIR = os.path.join(BASE_EFFECT_DIR, "Player")
GARON_FX_DIR = os.path.join(BASE_EFFECT_DIR, "Bosses", "Garon")

os.makedirs(PLAYER_FX_DIR, exist_ok=True)
os.makedirs(GARON_FX_DIR, exist_ok=True)

try:
    font = ImageFont.truetype("arial.ttf", 11)
    font_large = ImageFont.truetype("arial.ttf", 14)
except Exception:
    font = ImageFont.load_default()
    font_large = ImageFont.load_default()

def create_effect_dummy_sheet(fx_name, frame_count, width, height, bg_color, arc_color, text_color, font, output_path):
    sheet_width = width * frame_count
    sheet = Image.new("RGBA", (sheet_width, height), (0, 0, 0, 0))
    
    for i in range(frame_count):
        frame_num = i + 1
        frame_img = Image.new("RGBA", (width, height), (0, 0, 0, 0))
        draw = ImageDraw.Draw(frame_img)
        
        # Translucent Background Container
        draw.rectangle([2, 2, width - 3, height - 3], fill=bg_color, outline=arc_color, width=2)
        
        # Decorative Attack Trail Arc / Impact Visual Graphic
        if "Hit1" in fx_name or "ComboSlash" in fx_name:
            # Crescent Slash Arc (Left-Facing)
            draw.arc([10, 10, width - 20, height - 10], start=120, end=300, fill=arc_color, width=6)
        elif "Hit2" in fx_name:
            # Rising Slash Arc
            draw.arc([15, 15, width - 15, height - 15], start=45, end=225, fill=arc_color, width=6)
        elif "Hit3" in fx_name or "Smash" in fx_name:
            # Heavy Vertical Slam Slash Line + Ground Burst
            draw.line([(width//2 + 20, 10), (width//2 - 20, height - 20)], fill=arc_color, width=8)
            draw.ellipse([width//2 - 30, height - 30, width//2 + 30, height - 5], fill=arc_color)
        elif "Shockwave" in fx_name:
            # Concentric Expanding Energy Rings
            draw.ellipse([width//4, height//4, width*3//4, height*3//4], outline=arc_color, width=5)
            draw.ellipse([width//6, height//6, width*5//6, height*5//6], outline=text_color, width=3)
        elif "Charge" in fx_name:
            # Fast Thrust Lines
            draw.line([(width - 20, height//2), (20, height//2)], fill=arc_color, width=10)
            draw.polygon([(20, height//2 - 15), (5, height//2), (20, height//2 + 15)], fill=arc_color)

        # Label: FX_Name_FrameNum
        text = f"{fx_name}_{frame_num}"
        subtext = f"({width}x{height}px)"
        
        bbox = draw.textbbox((0, 0), text, font=font)
        text_w = bbox[2] - bbox[0]
        text_h = bbox[3] - bbox[1]
        x = (width - text_w) // 2
        y = height // 2 - 10
        
        bbox_sub = draw.textbbox((0, 0), subtext, font=font)
        sub_w = bbox_sub[2] - bbox_sub[0]
        sub_h = bbox_sub[3] - bbox_sub[1]
        sub_x = (width - sub_w) // 2
        sub_y = y + text_h + 4
        
        draw.rectangle([min(x, sub_x) - 4, y - 3, max(x + text_w, sub_x + sub_w) + 4, sub_y + sub_h + 3], fill=(10, 10, 20, 220))
        draw.text((x, y), text, fill=text_color, font=font)
        draw.text((sub_x, sub_y), subtext, fill=(220, 240, 255, 255), font=font)
        
        sheet.paste(frame_img, (i * width, 0))
        
    sheet.save(output_path, "PNG")
    print(f"[Attack Effects Dummy Generator] Saved: {output_path} ({frame_count} frames, {width}x{height}px)")

def main():
    print("=== Generating Attack Collision Dummy Placeholder Effects (Independent) ===")
    
    # 1. Player Attack Combo Dummy Effects (Green / Neon Cyan Theme, PPU 128)
    player_effects = [
        {"name": "Player_Attack_Hit1_Effect", "w": 128, "h": 128, "frames": 8, "bg": (10, 50, 30, 180), "arc": (0, 255, 120, 255), "text": (150, 255, 180, 255)},
        {"name": "Player_Attack_Hit2_Effect", "w": 128, "h": 128, "frames": 8, "bg": (15, 55, 35, 180), "arc": (50, 255, 150, 255), "text": (180, 255, 200, 255)},
        {"name": "Player_Attack_Hit3_Effect", "w": 160, "h": 160, "frames": 8, "bg": (20, 60, 40, 180), "arc": (100, 255, 180, 255), "text": (200, 255, 220, 255)}
    ]
    for fx in player_effects:
        out = os.path.join(PLAYER_FX_DIR, f"{fx['name']}.png")
        create_effect_dummy_sheet(fx["name"], fx["frames"], fx["w"], fx["h"], fx["bg"], fx["arc"], fx["text"], font, out)

    # 2. Garon Boss Pattern Dummy Effects (Crimson Red / Yellow Theme, PPU 128)
    garon_effects = [
        {"name": "Garon_ComboSlash_Effect", "w": 256, "h": 256, "frames": 8, "bg": (60, 15, 20, 180), "arc": (255, 50, 60, 255), "text": (255, 150, 150, 255)},
        {"name": "Garon_OverheadSmash_Effect", "w": 256, "h": 128, "frames": 8, "bg": (65, 20, 15, 180), "arc": (255, 120, 30, 255), "text": (255, 180, 100, 255)},
        {"name": "Garon_Shockwave_Effect", "w": 128, "h": 128, "frames": 8, "bg": (55, 10, 25, 180), "arc": (255, 40, 90, 255), "text": (255, 160, 180, 255)},
        {"name": "Garon_Charge_Effect", "w": 256, "h": 256, "frames": 8, "bg": (70, 25, 15, 180), "arc": (255, 90, 20, 255), "text": (255, 190, 120, 255)}
    ]
    for fx in garon_effects:
        out = os.path.join(GARON_FX_DIR, f"{fx['name']}.png")
        create_effect_dummy_sheet(fx["name"], fx["frames"], fx["w"], fx["h"], fx["bg"], fx["arc"], fx["text"], font_large, out)

    print("\n=== All Attack Collision Dummy Placeholder Effects Successfully Built ===")

if __name__ == "__main__":
    main()
