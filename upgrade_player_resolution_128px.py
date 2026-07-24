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
CONCEPT_PATH = os.path.join(PLAYER_DIR, "Player_Concept_128px.png")

# Load 128px Player Concept as base64
with open(CONCEPT_PATH, "rb") as f:
    CONCEPT_B64 = base64.b64encode(f.read()).decode("utf-8")

ANIMATIONS_128 = [
    {"name": "Player_Idle", "action": "idle breathing stance", "view": "side", "direction": "east"},
    {"name": "Player_Run", "action": "agile running sprint", "view": "side", "direction": "east"},
    {"name": "Player_Parry", "action": "parry counter sword slash with glowing crimson cyan impact flash", "view": "side", "direction": "east"},
    {"name": "Player_Guard", "action": "guard defensive energy barrier shield hold", "view": "side", "direction": "east"},
    {"name": "Player_Dodge", "action": "phantom shadow dash forward with cyan trail", "view": "side", "direction": "east"},
    {"name": "Player_Jump", "action": "jump apex downward air katana strike", "view": "side", "direction": "east"},
    {"name": "Player_ComboAttack", "action": "3-hit katana combo slash attack with cyan neon arcs", "view": "side", "direction": "east"},
    {"name": "Player_Execution", "action": "cinematic finisher execution slash with delayed explosion", "view": "side", "direction": "east"}
]

def submit_animation_job(anim_spec):
    url = "https://api.pixellab.ai/v2/animate-with-text-v2"
    payload = {
        "reference_image": {"type": "base64", "base64": CONCEPT_B64},
        "reference_image_size": {"width": 128, "height": 128},
        "action": anim_spec["action"],
        "image_size": {"width": 128, "height": 128},
        "view": anim_spec["view"],
        "direction": anim_spec["direction"]
    }
    r = requests.post(url, headers=HEADERS, json=payload)
    if r.status_code == 202:
        job_id = r.json().get("background_job_id")
        print(f"[PixelLab 128px Player] Job submitted for '{anim_spec['name']}': Job ID = {job_id}")
        return job_id
    else:
        print(f"[PixelLab 128px Player] Failed '{anim_spec['name']}': {r.status_code} - {r.text}")
        return None

def wait_and_save_job(job_id, anim_name):
    url = f"https://api.pixellab.ai/v2/background-jobs/{job_id}"
    print(f"[PixelLab 128px Player] Waiting for '{anim_name}' (Job ID: {job_id})...")
    
    while True:
        r = requests.get(url, headers=HEADERS)
        if r.status_code != 200:
            time.sleep(5)
            continue
            
        data = r.json()
        status = data.get("status")
        
        if status == "completed":
            images_data = data.get("last_response", {}).get("images", [])
            print(f"[PixelLab 128px Player] Job '{anim_name}' COMPLETED! ({len(images_data)} frames)")
            
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
                print(f"[PixelLab 128px Player] Saved 128x128 SpriteSheet: {output_path}")
            return True
            
        elif status == "failed":
            print(f"[PixelLab 128px Player] Job '{anim_name}' FAILED: {data}")
            return False
            
        else:
            prog = data.get("last_response", {}).get("progress", 0.0)
            print(f"[PixelLab 128px Player] '{anim_name}' in progress... ({int(prog * 100)}%)")
            time.sleep(5)

def main():
    print("=== Upgrading Player Animations Resolution to 128x128px using PixelLab API ===")
    
    # Overwrite Player_Concept.png with 128px concept
    os.replace(CONCEPT_PATH, os.path.join(PLAYER_DIR, "Player_Concept.png"))
    print("Updated Player_Concept.png to 128x128px base concept.")
    
    for anim in ANIMATIONS_128:
        job_id = submit_animation_job(anim)
        if job_id:
            wait_and_save_job(job_id, anim["name"])
        time.sleep(2)

    print("=== All 128x128px Player Animations Successfully Regenerated and Saved ===")

if __name__ == "__main__":
    main()
