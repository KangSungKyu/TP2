from PIL import Image, ImageEnhance
import math
import os

COMFY_OUT = r"C:\Users\PC\AppData\Local\Comfy-Desktop\ComfyUI-Installs\ComfyUI\ComfyUI\output"
DOC_DIR = r"C:\Users\PC\Projects\TP2\doc\images\concepts"
PROJECT_DIR = r"C:\Users\PC\Projects\TP2\Assets\Textures\Characters\Monsters"
ARTIFACT_DIR = r"C:\Users\PC\.gemini\antigravity\brain\d4f1e2da-f7e5-4e86-b715-9979775531c1"

MONSTERS = [
    {
        "id": "3101",
        "name": "Monster_3101_SpearSentry",
        "src": "Monster_3101_SpearSentry_00001_.png",
        "frames": 8
    },
    {
        "id": "3102",
        "name": "Monster_3102_ShadowStalker",
        "src": "Monster_3102_ShadowStalker_00001_.png",
        "frames": 8
    },
    {
        "id": "3103",
        "name": "Monster_3103_WaveHeavy",
        "src": "Monster_3103_WaveHeavy_00001_.png",
        "frames": 8
    },
    {
        "id": "3104",
        "name": "Monster_3104_ShieldSentinel",
        "src": "Monster_3104_ShieldSentinel_00001_.png",
        "frames": 8
    },
    {
        "id": "3105",
        "name": "Monster_3105_OrbitalMarksman",
        "src": "Monster_3105_OrbitalMarksman_00001_.png",
        "frames": 8
    }
]

def generate_idle_sheet_uncropped(m_info):
    src_path = os.path.join(COMFY_OUT, m_info["src"])
    orig = Image.open(src_path).convert("RGBA")
    
    # Resize uncropped original to 256x256 base frame
    base_frame = orig.resize((256, 256), Image.Resampling.LANCZOS)
    
    num_frames = m_info["frames"]
    sheet_width = 256 * num_frames
    sheet_height = 256
    sheet = Image.new("RGBA", (sheet_width, sheet_height), (0, 0, 0, 0))
    
    for i in range(num_frames):
        # Calculate smooth sine-wave idle displacement (0 -> 1 -> 0 -> -1 -> 0)
        phase = 2.0 * math.pi * (i / float(num_frames))
        dy = int(round(math.sin(phase) * 2.0))
        dx = int(round(math.cos(phase) * 0.5))
        
        # Idle frame canvas
        frame = Image.new("RGBA", (256, 256), (0, 0, 0, 0))
        frame.paste(base_frame, (dx, dy))
        
        # Subtle lighting/steam pulsation for steam fantasy aesthetics
        brightness_factor = 1.0 + 0.04 * math.sin(phase)
        enhancer = ImageEnhance.Brightness(frame)
        frame_enhanced = enhancer.enhance(brightness_factor)
        
        sheet.paste(frame_enhanced, (i * 256, 0))
    
    out_name = f"{m_info['name']}_Idle_Sheet.png"
    
    doc_path = os.path.join(DOC_DIR, out_name)
    proj_path = os.path.join(PROJECT_DIR, out_name)
    art_path = os.path.join(ARTIFACT_DIR, out_name)
    
    sheet.save(doc_path)
    sheet.save(proj_path)
    sheet.save(art_path)
    print(f"Generated Idle Sheet for {m_info['name']} -> {doc_path} ({sheet_width}x{sheet_height})")

def main():
    os.makedirs(DOC_DIR, exist_ok=True)
    os.makedirs(PROJECT_DIR, exist_ok=True)
    for m in MONSTERS:
        generate_idle_sheet_uncropped(m)

if __name__ == "__main__":
    main()
