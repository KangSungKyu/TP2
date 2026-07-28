import os
from PIL import Image, ImageDraw, ImageFont

EFFECTS_DIR = r"c:\Users\PC\Projects\TP2\Assets\Textures\Effects"
os.makedirs(EFFECTS_DIR, exist_ok=True)

try:
    font = ImageFont.truetype("arial.ttf", 13)
    font_bold = ImageFont.truetype("arial.ttf", 16)
except Exception:
    font = ImageFont.load_default()
    font_bold = ImageFont.load_default()

def create_common_effect_sheet(fx_name, label_text, bg_color, ring_color, text_color, output_path):
    width, height = 128, 128
    frame_count = 8
    sheet_width = width * frame_count
    sheet = Image.new("RGBA", (sheet_width, height), (0, 0, 0, 0))
    
    for i in range(frame_count):
        frame_num = i + 1
        frame_img = Image.new("RGBA", (width, height), (0, 0, 0, 0))
        draw = ImageDraw.Draw(frame_img)
        
        # Outer Translucent Container
        draw.rectangle([2, 2, width - 3, height - 3], fill=bg_color, outline=ring_color, width=2)
        
        # Inner Circle Visual Effect Graphic
        margin = 15 - (i % 3) * 2  # Subtle pulsing animation simulation
        draw.ellipse([margin, margin, width - margin, height - margin], outline=ring_color, width=4)
        draw.ellipse([margin + 10, margin + 10, width - margin - 10, height - margin - 10], outline=(255, 255, 255, 180), width=2)
        
        # Centered Text Label: PARRY, GUARD, DODGE, HIT
        bbox = draw.textbbox((0, 0), label_text, font=font_bold)
        text_w = bbox[2] - bbox[0]
        text_h = bbox[3] - bbox[1]
        x = (width - text_w) // 2
        y = (height - text_h) // 2 - 4
        
        # Background Pill for Text Readability
        draw.rectangle([x - 6, y - 3, x + text_w + 6, y + text_h + 3], fill=(10, 10, 20, 220))
        draw.text((x, y), label_text, fill=text_color, font=font_bold)
        
        # Subtext: Frame Num
        subtext = f"FX_{frame_num}"
        bbox_sub = draw.textbbox((0, 0), subtext, font=font)
        sub_w = bbox_sub[2] - bbox_sub[0]
        sub_x = (width - sub_w) // 2
        sub_y = y + text_h + 6
        draw.text((sub_x, sub_y), subtext, fill=(220, 230, 255, 255), font=font)
        
        sheet.paste(frame_img, (i * width, 0))
        
    sheet.save(output_path, "PNG")
    print(f"[Common Effects Generator] Saved: {output_path} (8 frames, 128x128px)")

def main():
    print("=== Generating Common Combat Response Placeholder Effects (Independent) ===")
    
    effects = [
        {
            "filename": "Placeholder_Parry.png",
            "name": "PARRY",
            "bg": (50, 45, 10, 180),
            "ring": (255, 220, 0, 255),
            "text": (255, 240, 100, 255)
        },
        {
            "filename": "Placeholder_Guard.png",
            "name": "GUARD",
            "bg": (10, 35, 60, 180),
            "ring": (0, 180, 255, 255),
            "text": (100, 220, 255, 255)
        },
        {
            "filename": "Placeholder_Dodge.png",
            "name": "DODGE",
            "bg": (35, 10, 50, 180),
            "ring": (180, 80, 240, 255),
            "text": (220, 150, 255, 255)
        },
        {
            "filename": "Placeholder_Hit.png",
            "name": "HIT",
            "bg": (55, 10, 15, 180),
            "ring": (255, 50, 60, 255),
            "text": (255, 120, 130, 255)
        }
    ]
    
    for fx in effects:
        out = os.path.join(EFFECTS_DIR, fx["filename"])
        create_common_effect_sheet(fx["filename"], fx["name"], fx["bg"], fx["ring"], fx["text"], out)

    print("\n=== All Common Combat Response Placeholder Effects Successfully Built ===")

if __name__ == "__main__":
    main()
