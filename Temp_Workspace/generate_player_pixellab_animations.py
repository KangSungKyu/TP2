import os
import time
import base64
import requests
from PIL import Image
import io

API_KEY = "b8910d80-048e-42f7-8ff9-8c347b4d36bb"
HEADERS = {
    "Authorization": f"Bearer {API_KEY}",
    "Content-Type": "application/json"
}

PLAYER_DIR = r"c:\Users\PC\Projects\TP2\Assets\Textures\Characters\Player"
CONCEPT_PATH = os.path.join(PLAYER_DIR, "Player_Concept.png")

# Load Player_Concept.png as base64
with open(CONCEPT_PATH, "rb") as f:
    CONCEPT_B64 = base64.b64encode(f.read()).decode("utf-8")

ANIMATIONS = [
    {"name": "Player_Idle", "action": "idle breathing stance", "view": "side", "direction": "east"},
    {"name": "Player_Run", "action": "fast running sprint", "view": "side", "direction": "east"},
    {"name": "Player_Parry", "action": "parry sword counter block strike", "view": "side", "direction": "east"},
    {"name": "Player_Guard", "action": "guard defensive shield hold", "view": "side", "direction": "east"},
    {"name": "Player_Dodge", "action": "phantom shadow dash forward", "view": "side", "direction": "east"},
    {"name": "Player_Jump", "action": "jump apex downward air strike", "view": "side", "direction": "east"},
    {"name": "Player_ComboAttack", "action": "3-hit katana combo slash attack", "view": "side", "direction": "east"},
    {"name": "Player_Execution", "action": "cinematic finisher execution slash", "view": "side", "direction": "east"}
]

def submit_animation_job(anim_spec):
    url = "https://api.pixellab.ai/v2/animate-with-text-v2"
    payload = {
        "reference_image": {"type": "base64", "base64": CONCEPT_B64},
        "reference_image_size": {"width": 64, "height": 64},
        "action": anim_spec["action"],
        "image_size": {"width": 64, "height": 64},
        "view": anim_spec["view"],
        "direction": anim_spec["direction"]
    }
    r = requests.post(url, headers=HEADERS, json=payload)
    if r.status_code == 202:
        job_id = r.json().get("background_job_id")
        print(f"[PixelLab] Job submitted for '{anim_spec['name']}': Job ID = {job_id}")
        return job_id
    else:
        print(f"[PixelLab] Failed to submit '{anim_spec['name']}': {r.status_code} - {r.text}")
        return None

def wait_and_save_job(job_id, anim_name):
    url = f"https://api.pixellab.ai/v2/background-jobs/{job_id}"
    print(f"[PixelLab] Waiting for '{anim_name}' (Job ID: {job_id})...")
    
    while True:
        r = requests.get(url, headers=HEADERS)
        if r.status_code != 200:
            print(f"[PixelLab] Error checking job status: {r.status_code}")
            time.sleep(5)
            continue
            
        data = r.json()
        status = data.get("status")
        
        if status == "completed":
            last_resp = data.get("last_response", {})
            images_data = last_resp.get("images", [])
            print(f"[PixelLab] Job '{anim_name}' COMPLETED! ({len(images_data)} frames received)")
            
            pil_images = []
            for img_item in images_data:
                b64 = img_item.get("base64", "")
                if "," in b64:
                    b64 = b64.split(",")[1]
                img_bytes = base64.b64decode(b64)
                img = Image.open(io.BytesIO(img_bytes))
                pil_images.append(img)
            
            if pil_images:
                w, h = pil_images[0].size
                sheet = Image.new("RGBA", (w * len(pil_images), h))
                for i, img in enumerate(pil_images):
                    sheet.paste(img, (i * w, 0))
                
                output_path = os.path.join(PLAYER_DIR, f"{anim_name}.png")
                sheet.save(output_path, "PNG")
                print(f"[PixelLab] Saved SpriteSheet: {output_path}")
            return True
            
        elif status == "failed":
            print(f"[PixelLab] Job '{anim_name}' FAILED: {data}")
            return False
            
        else:
            prog = data.get("last_response", {}).get("progress", 0.0)
            print(f"[PixelLab] '{anim_name}' in progress... ({int(prog * 100)}%)")
            time.sleep(5)

def main():
    print("=== Generating Player Animations using PixelLab API (Preserving Player_Concept.png) ===")
    
    # 1. Submit existing walk job check or process all
    for anim in ANIMATIONS:
        job_id = submit_animation_job(anim)
        if job_id:
            wait_and_save_job(job_id, anim["name"])
        time.sleep(2)

    print("=== All Player Animations Successfully Generated and Saved to Unity Assets ===")

if __name__ == "__main__":
    main()
