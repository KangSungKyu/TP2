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
CONCEPT_PATH = os.path.join(GARON_DIR, "Garon_Concept_RightFacing.png")
DEST_CONCEPT_PATH = os.path.join(GARON_DIR, "Garon_Concept.png")

# Overwrite Garon_Concept.png with strictly Right-Facing concept
if os.path.exists(CONCEPT_PATH):
    import shutil
    shutil.copyfile(CONCEPT_PATH, DEST_CONCEPT_PATH)
    print("Updated Garon_Concept.png to strictly Right-Facing concept.")

with open(DEST_CONCEPT_PATH, "rb") as f:
    CONCEPT_B64 = base64.b64encode(f.read()).decode("utf-8")

GARON_RIGHT_FACING_ANIMS = [
    {"name": "Garon_Idle", "action": "strictly facing right side view profile, idle breathing stance holding heavy odachi katana"},
    {"name": "Garon_Move", "action": "strictly facing right side view profile, heavy marching forward movement"},
    {"name": "Garon_Jump", "action": "strictly facing right side view profile, heavy boss leap jump"},
    {"name": "Garon_Death", "action": "strictly facing right side view profile, boss defeat collapse explosion"},
    {"name": "Garon_Pattern_Charge", "action": "strictly facing right side view profile, fast forward thrust charge with long odachi"},
    {"name": "Garon_Pattern_ComboSlash", "action": "strictly facing right side view profile, 4-hit heavy odachi slash combo attack"},
    {"name": "Garon_Pattern_OverheadSmash", "action": "strictly facing right side view profile, overhead sword stance hold and heavy downward slash smash"},
    {"name": "Garon_Pattern_Shockwave", "action": "strictly facing right side view profile, ground slam attack creating glowing red shockwave"}
]

def submit_job(anim_spec):
    url = "https://api.pixellab.ai/v2/animate-with-text-v2"
    payload = {
        "reference_image": {"type": "base64", "base64": CONCEPT_B64},
        "reference_image_size": {"width": 128, "height": 128},
        "action": anim_spec["action"],
        "image_size": {"width": 128, "height": 128},
        "view": "side",
        "direction": "east"
    }
    r = requests.post(url, headers=HEADERS, json=payload)
    if r.status_code == 202:
        job_id = r.json().get("background_job_id")
        print(f"[PixelLab - Garon Right-Facing] Job submitted for '{anim_spec['name']}': Job ID = {job_id}")
        return job_id
    else:
        print(f"[PixelLab - Garon Right-Facing] Failed '{anim_spec['name']}': {r.status_code} - {r.text}")
        return None

def wait_and_save(job_id, anim_name):
    url = f"https://api.pixellab.ai/v2/background-jobs/{job_id}"
    print(f"[PixelLab - Garon Right-Facing] Waiting for '{anim_name}' (Job ID: {job_id})...")
    
    while True:
        r = requests.get(url, headers=HEADERS)
        if r.status_code != 200:
            time.sleep(5)
            continue
            
        data = r.json()
        status = data.get("status")
        
        if status == "completed":
            images_data = data.get("last_response", {}).get("images", [])
            print(f"[PixelLab - Garon Right-Facing] Job '{anim_name}' COMPLETED! ({len(images_data)} frames)")
            
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
                print(f"[PixelLab - Garon Right-Facing] Saved SpriteSheet: {output_path}")
            return True
            
        elif status == "failed":
            print(f"[PixelLab - Garon Right-Facing] Job '{anim_name}' FAILED: {data}")
            return False
            
        else:
            prog = data.get("last_response", {}).get("progress", 0.0)
            print(f"[PixelLab - Garon Right-Facing] '{anim_name}' in progress... ({int(prog * 100)}%)")
            time.sleep(5)

def main():
    print("=== Rebuilding Iron Guard Garon Animations (Strictly Right-Facing Side View Profile) ===")
    
    for anim in GARON_RIGHT_FACING_ANIMS:
        job_id = submit_job(anim)
        if job_id:
            wait_and_save(job_id, anim["name"])
        time.sleep(2)

    print("=== All Garon Right-Facing Animations Successfully Rebuilt and Saved ===")

if __name__ == "__main__":
    main()
