import os
from PIL import Image, ImageDraw, ImageFont

ENV_FX_DIR = r"c:\Users\PC\Projects\TP2\Assets\Textures\Effects\Environment"
os.makedirs(ENV_FX_DIR, exist_ok=True)

try:
    font_small = ImageFont.truetype("arial.ttf", 10)
    font_med = ImageFont.truetype("arial.ttf", 12)
except Exception:
    font_small = ImageFont.load_default()
    font_med = ImageFont.load_default()

def create_physics_fx_sheet(fx_name, frame_count, width, height, bg_color, ring_color, text_color, font, output_path):
    sheet_width = width * frame_count
    sheet = Image.new("RGBA", (sheet_width, height), (0, 0, 0, 0))
    
    for i in range(frame_count):
        frame_num = i + 1
        frame_img = Image.new("RGBA", (width, height), (0, 0, 0, 0))
        draw = ImageDraw.Draw(frame_img)
        
        # Outer Container
        draw.rectangle([2, 2, width - 3, height - 3], fill=bg_color, outline=ring_color, width=2)
        
        # Specific Physics FX Graphic
        if "Jump_Launch" in fx_name:
            # Upward Dust Burst Arc
            draw.arc([10, 10, width - 10, height*2], start=180, end=360, fill=ring_color, width=4)
            draw.line([(width//2, height - 5), (width//2, 10)], fill=text_color, width=3)
        elif "Land_Shockwave" in fx_name:
            # Horizontal Dust Spread Waves
            draw.line([(10, height - 10), (width - 10, height - 10)], fill=ring_color, width=5)
            draw.ellipse([width//4, height//2, width*3//4, height - 5], outline=text_color, width=2)
        elif "OneWay" in fx_name:
            # Downward Arrow (DOWN ⬇)
            draw.line([(width//2, 10), (width//2, height - 20)], fill=ring_color, width=5)
            draw.polygon([(width//2 - 12, height - 20), (width//2, height - 5), (width//2 + 12, height - 20)], fill=ring_color)

        # Label: FX_Name_FrameNum
        text = f"{fx_name}_{frame_num}"
        bbox = draw.textbbox((0, 0), text, font=font)
        tw = bbox[2] - bbox[0]
        th = bbox[3] - bbox[1]
        tx = (width - tw) // 2
        ty = (height - th) // 2
        
        draw.rectangle([tx - 4, ty - 2, tx + tw + 4, ty + th + 2], fill=(10, 10, 20, 220))
        draw.text((tx, ty), text, fill=text_color, font=font)
        
        sheet.paste(frame_img, (i * width, 0))
        
    sheet.save(output_path, "PNG")
    print(f"[Physics FX Generator] Saved: {output_path} ({frame_count} frames, {width}x{height}px)")

def main():
    print("=== Generating Metroidvania Physics Enhancement Dummy Effects (Independent) ===")
    
    effects = [
        {"name": "VFX_Jump_Launch", "w": 128, "h": 64, "frames": 8, "bg": (15, 45, 55, 180), "ring": (0, 220, 255, 255), "text": (180, 245, 255, 255)},
        {"name": "VFX_Land_Shockwave", "w": 128, "h": 64, "frames": 8, "bg": (45, 35, 15, 180), "ring": (255, 180, 50, 255), "text": (255, 220, 120, 255)},
        {"name": "VFX_OneWay_DownPass_Indicator", "w": 64, "h": 64, "frames": 8, "bg": (55, 15, 35, 180), "ring": (255, 80, 180, 255), "text": (255, 160, 220, 255)}
    ]
    
    for fx in effects:
        out = os.path.join(ENV_FX_DIR, f"{fx['name']}.png")
        create_physics_fx_sheet(fx["name"], fx["frames"], fx["w"], fx["h"], fx["bg"], fx["ring"], fx["text"], font_med if fx["w"] > 64 else font_small, out)

    print("\n=== All Metroidvania Physics Enhancement Dummy Effects Built ===")

if __name__ == "__main__":
    main()
