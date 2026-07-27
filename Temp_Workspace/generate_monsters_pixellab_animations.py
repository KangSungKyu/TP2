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

MONSTERS_BASE_DIR = r"c:\Users\PC\Projects\TP2\Assets\Textures\Characters\Monsters"

MONSTER_SPECS = [
    {
        "folder": "SpearSentry",
        "concept_file": "Monster_Concept_1_SpearSentry.png",
        "prefix": "SpearSentry",
        "anims": [
            {"name": "Idle", "action": "idle stance holding cyber spear"},
            {"name": "Move", "action": "walking forward with cyber spear"},
            {"name": "Jump", "action": "jump up leap in mid air"},
            {"name": "Attack", "action": "forward spear stab attack"},
            {"name": "Death", "action": "enemy defeat collapse explosion"}
        ]
    },
    {
        "folder": "ShadowStalker",
        "concept_file": "Monster_Concept_2_ShadowStalker.png",
        "prefix": "ShadowStalker",
        "anims": [
            {"name": "Idle", "action": "idle assassin stance holding dual daggers"},
            {"name": "Move", "action": "fast shadow dash stealth movement"},
            {"name": "Jump", "action": "flip jump in mid air"},
            {"name": "Attack", "action": "shadow teleport dual dagger strike"},
            {"name": "Death", "action": "shadow dissolve disintegration death"}
        ]
    },
    {
        "folder": "WaveHeavy",
        "concept_file": "Monster_Concept_3_WaveHeavy.png",
        "prefix": "WaveHeavy",
        "anims": [
            {"name": "Idle", "action": "heavy idle stance holding energy hammer"},
            {"name": "Move", "action": "heavy marching movement"},
            {"name": "Jump", "action": "heavy leap jump"},
            {"name": "Attack", "action": "hammer ground slam attack creating shockwave"},
            {"name": "Death", "action": "heavy armor explosion breakdown"}
        ]
    }
]

def submit_job(concept_b64, action):
    url = "https://api.pixellab.ai/v2/animate-with-text-v2"
    payload = {
        "reference_image": {"type": "base64", "base64": concept_b64},
        "reference_image_size": {"width": 64, "height": 64},
        "action": action,
        "image_size": {"width": 64, "height": 64},
        "view": "side",
        "direction": "east"
    }
    r = requests.post(url, headers=HEADERS, json=payload)
    if r.status_code == 202:
        return r.json().get("background_job_id")
    print(f"Error submitting job: {r.status_code} - {r.text}")
    return None

def wait_and_save(job_id, output_path):
    url = f"https://api.pixellab.ai/v2/background-jobs/{job_id}"
    while True:
        r = requests.get(url, headers=HEADERS)
        if r.status_code != 200:
            time.sleep(5)
            continue
        data = r.json()
        status = data.get("status")
        if status == "completed":
            images_data = data.get("last_response", {}).get("images", [])
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
                sheet.save(output_path, "PNG")
                print(f"[PixelLab Monster] Saved: {output_path}")
            return True
        elif status == "failed":
            print(f"[PixelLab Monster] Job failed: {data}")
            return False
        else:
            time.sleep(5)

def main():
    print("=== Generating Animations for 3 Regular Monsters using PixelLab API ===")
    
    for spec in MONSTER_SPECS:
        folder_path = os.path.join(MONSTERS_BASE_DIR, spec["folder"])
        os.makedirs(folder_path, exist_ok=True)
        
        concept_path = os.path.join(MONSTERS_BASE_DIR, spec["concept_file"])
        with open(concept_path, "rb") as f:
            concept_b64 = base64.b64encode(f.read()).decode("utf-8")
            
        print(f"\n--- Generating for Monster: {spec['prefix']} ---")
        for anim in spec["anims"]:
            out_file = os.path.join(folder_path, f"{spec['prefix']}_{anim['name']}.png")
            print(f"Generating {spec['prefix']}_{anim['name']}...")
            job_id = submit_job(concept_b64, anim["action"])
            if job_id:
                wait_and_save(job_id, out_file)
            time.sleep(2)

    print("\n=== All Regular Monster Animations Completed ===")

if __name__ == "__main__":
    main()
