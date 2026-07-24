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

GARON_DIR = r"c:\Users\PC\Projects\TP2\Assets\Textures\Characters\Bosses\Garon"
CONCEPT_PATH = os.path.join(GARON_DIR, "Garon_Concept.png")

# Load Garon_Concept.png as base64
with open(CONCEPT_PATH, "rb") as f:
    CONCEPT_B64 = base64.b64encode(f.read()).decode("utf-8")

GARON_ANIMATIONS = [
    # 공용 애니메이션 (Common Animations)
    {"name": "Garon_Idle", "action": "idle breathing stance holding heavy odachi", "view": "side", "direction": "east"},
    {"name": "Garon_Move", "action": "heavy march walking cycle", "view": "side", "direction": "east"},
    {"name": "Garon_Jump", "action": "heavy boss leap jump apex hover", "view": "side", "direction": "east"},
    {"name": "Garon_Death", "action": "boss death defeat collapse disintegration explosion", "view": "side", "direction": "east"},

    # 보스 특수 시험 패턴 (Boss Special Test Patterns)
    {"name": "Garon_Pattern_Charge", "action": "fast forward thrust charge attack with long odachi", "view": "side", "direction": "east"},
    {"name": "Garon_Pattern_ComboSlash", "action": "4-hit heavy odachi slash combo attack", "view": "side", "direction": "east"},
    {"name": "Garon_Pattern_OverheadSmash", "action": "overhead sword stance hold and heavy downward slash smash", "view": "side", "direction": "east"},
    {"name": "Garon_Pattern_Shockwave", "action": "ground slam attack creating glowing red shockwave wave", "view": "side", "direction": "east"}
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
        print(f"[PixelLab - Garon] Job submitted for '{anim_spec['name']}': Job ID = {job_id}")
        return job_id
    else:
        print(f"[PixelLab - Garon] Failed to submit '{anim_spec['name']}': {r.status_code} - {r.text}")
        return None

def wait_and_save_job(job_id, anim_name):
    url = f"https://api.pixellab.ai/v2/background-jobs/{job_id}"
    print(f"[PixelLab - Garon] Waiting for '{anim_name}' (Job ID: {job_id})...")
    
    while True:
        r = requests.get(url, headers=HEADERS)
        if r.status_code != 200:
            print(f"[PixelLab - Garon] Error checking job status: {r.status_code}")
            time.sleep(5)
            continue
            
        data = r.json()
        status = data.get("status")
        
        if status == "completed":
            last_resp = data.get("last_response", {})
            images_data = last_resp.get("images", [])
            print(f"[PixelLab - Garon] Job '{anim_name}' COMPLETED! ({len(images_data)} frames received)")
            
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
                
                output_path = os.path.join(GARON_DIR, f"{anim_name}.png")
                sheet.save(output_path, "PNG")
                print(f"[PixelLab - Garon] Saved SpriteSheet: {output_path}")
            return True
            
        elif status == "failed":
            print(f"[PixelLab - Garon] Job '{anim_name}' FAILED: {data}")
            return False
            
        else:
            prog = data.get("last_response", {}).get("progress", 0.0)
            print(f"[PixelLab - Garon] '{anim_name}' in progress... ({int(prog * 100)}%)")
            time.sleep(5)

def main():
    print("=== Generating Iron Guard Garon Animations using PixelLab API (Cyber Oni + Odachi 128x128) ===")
    
    for anim in GARON_ANIMATIONS:
        job_id = submit_animation_job(anim)
        if job_id:
            wait_and_save_job(job_id, anim["name"])
        time.sleep(2)

    print("=== All Iron Guard Garon Animations Successfully Generated and Saved ===")

if __name__ == "__main__":
    main()
