import os
import shutil
from PIL import Image, ImageDraw, ImageFont

PLAYER_DIR = r"c:\Users\PC\Projects\TP2\Assets\Textures\Characters\Player"
ARTIFACT_DIR = r"C:\Users\PC\.gemini\antigravity\brain\63c3f691-87f9-4235-a4f6-b78479705ddb"

os.makedirs(PLAYER_DIR, exist_ok=True)

try:
    font = ImageFont.truetype("arial.ttf", 12)
    font_large = ImageFont.truetype("arial.ttf", 18)
except Exception:
    font = ImageFont.load_default()
    font_large = ImageFont.load_default()

def generate_idle_left_arrow_sheet():
    width, height = 128, 256
    frame_count = 8
    sheet_width = width * frame_count
    
    bg_color = (15, 35, 55, 230)
    border_color = (0, 220, 255, 255)
    text_color = (0, 245, 255, 255)
    arrow_color = (255, 220, 0, 255)  # Bright Yellow/Cyan arrow for high visibility
    
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
        
        # Text label: Player_Idle_Num
        text = f"Player_Idle_{frame_num}"
        subtext = "(128x256px)"
        
        bbox = draw.textbbox((0, 0), text, font=font)
        text_w = bbox[2] - bbox[0]
        text_h = bbox[3] - bbox[1]
        x = (width - text_w) // 2
        y = height - 60
        
        draw.rectangle([x - 4, y - 2, x + text_w + 4, y + text_h + 2], fill=(15, 15, 25, 230))
        draw.text((x, y), text, fill=text_color, font=font)
        
        # === Left-Pointing Arrow (⬅️) ===
        # Arrow Shaft: From x=85 to x=45 (pointing left)
        # Arrow Head: Polygon pointing to x=30
        arrow_y = 50
        draw.line([(85, arrow_y), (45, arrow_y)], fill=arrow_color, width=6)
        draw.polygon([(45, arrow_y - 12), (30, arrow_y), (45, arrow_y + 12)], fill=arrow_color)
        
        # Arrow Text Label: "LEFT ⬅"
        arrow_label = "FACING LEFT ⬅"
        bbox_arr = draw.textbbox((0, 0), arrow_label, font=font)
        arr_w = bbox_arr[2] - bbox_arr[0]
        arr_x = (width - arr_w) // 2
        draw.rectangle([arr_x - 4, arrow_y + 18, arr_x + arr_w + 4, arrow_y + 34], fill=(20, 20, 30, 220))
        draw.text((arr_x, arrow_y + 20), arrow_label, fill=(255, 240, 100, 255), font=font)
        
        # Bottom-Center Pivot Marker
        pivot_x = width // 2
        pivot_y = height - 6
        draw.ellipse([pivot_x - 5, pivot_y - 5, pivot_x + 5, pivot_y + 5], fill=(255, 50, 50, 255), outline=(255, 255, 255, 255))
        
        sheet.paste(frame_img, (i * width, 0))
        
    output_path = os.path.join(PLAYER_DIR, "Player_Idle.png")
    sheet.save(output_path, "PNG")
    print(f"Successfully generated Player_Idle with Left-pointing Arrow: {output_path}")
    
    # Copy to artifact directory for visual rendering
    artifact_copy = os.path.join(ARTIFACT_DIR, "Player_Idle_LeftArrow.png")
    shutil.copy(output_path, artifact_copy)
    print(f"Copied draft sheet to artifact dir: {artifact_copy}")

if __name__ == "__main__":
    generate_idle_left_arrow_sheet()
