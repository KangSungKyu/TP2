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

BASE_DIR = r"c:\Users\PC\Projects\TP2\Assets\Textures\Characters"

# Specs for 8-frame smooth animation upgrade
ALL_TARGETS = [
    # 1. Garon Boss (128x128px, 8 frames)
    {
        "name_tag": "Garon",
        "dir": os.path.join(BASE_DIR, "Bosses", "Garon"),
        "concept": os.path.join(BASE_DIR, "Bosses", "Garon", "Garon_Concept.png"),
        "width": 128, "height": 128, "frame_count": 8,
        "items": [
            {"file": "Garon_Idle.png", "action": "strictly facing right side view, idle breathing stance holding heavy odachi katana"},
            {"file": "Garon_Move.png", "action": "strictly facing right side view, heavy marching forward movement"},
            {"file": "Garon_Jump.png", "action": "strictly facing right side view, heavy boss leap jump in air clean transparent background without ground terrain"},
            {"file": "Garon_Death.png", "action": "strictly facing right side view, boss defeat collapse explosion"},
            {"file": "Garon_Pattern_Charge.png", "action": "strictly facing right side view, fast forward thrust charge with long odachi"},
            {"file": "Garon_Pattern_ComboSlash.png", "action": "strictly facing right side view, 4-hit heavy odachi slash combo attack"},
            {"file": "Garon_Pattern_OverheadSmash.png", "action": "strictly facing right side view, overhead sword stance hold and heavy downward slash smash"},
            {"file": "Garon_Pattern_Shockwave.png", "action": "strictly facing right side view, ground slam attack creating glowing red shockwave"}
        ]
    },
    # 2. Player (128x128px, 8 frames)
    {
        "name_tag": "Player",
        "dir": os.path.join(BASE_DIR, "Player"),
        "concept": os.path.join(BASE_DIR, "Player", "Player_Concept.png"),
        "width": 128, "height": 128, "frame_count": 8,
        "items": [
            {"file": "Player_Idle.png", "action": "strictly facing right side view, idle breathing stance"},
            {"file": "Player_Run.png", "action": "strictly facing right side view, agile running sprint"},
            {"file": "Player_Parry.png", "action": "strictly facing right side view, parry counter sword slash with glowing impact flash"},
            {"file": "Player_Guard.png", "action": "strictly facing right side view, guard defensive energy barrier shield hold"},
            {"file": "Player_Dodge.png", "action": "strictly facing right side view, phantom shadow dash forward"},
            {"file": "Player_Jump.png", "action": "strictly facing right side view, jump apex downward air katana strike"},
            {"file": "Player_ComboAttack.png", "action": "strictly facing right side view, 3-hit katana combo slash attack"},
            {"file": "Player_Execution.png", "action": "strictly facing right side view, cinematic finisher execution slash"}
        ]
    },
    # 3. SpearSentry Monster (64x64px, 8 frames)
    {
        "name_tag": "SpearSentry",
        "dir": os.path.join(BASE_DIR, "Monsters", "SpearSentry"),
        "concept": os.path.join(BASE_DIR, "Monsters", "Monster_Concept_1_SpearSentry.png"),
        "width": 64, "height": 64, "frame_count": 8,
        "items": [
            {"file": "SpearSentry_Idle.png", "action": "strictly facing right side view, idle stance holding cyber spear"},
            {"file": "SpearSentry_Move.png", "action": "strictly facing right side view, walking forward with cyber spear"},
            {"file": "SpearSentry_Jump.png", "action": "strictly facing right side view, jump up leap in mid air"},
            {"file": "SpearSentry_Attack.png", "action": "strictly facing right side view, forward spear stab attack"},
            {"file": "SpearSentry_Death.png", "action": "strictly facing right side view, enemy defeat collapse explosion"}
        ]
    },
    # 4. ShadowStalker Monster (64x64px, 8 frames)
    {
        "name_tag": "ShadowStalker",
        "dir": os.path.join(BASE_DIR, "Monsters", "ShadowStalker"),
        "concept": os.path.join(BASE_DIR, "Monsters", "Monster_Concept_2_ShadowStalker.png"),
        "width": 64, "height": 64, "frame_count": 8,
        "items": [
            {"file": "ShadowStalker_Idle.png", "action": "strictly facing right side view, idle assassin stance holding dual daggers"},
            {"file": "ShadowStalker_Move.png", "action": "strictly facing right side view, fast shadow dash stealth movement"},
            {"file": "ShadowStalker_Jump.png", "action": "strictly facing right side view, flip jump in mid air"},
            {"file": "ShadowStalker_Attack.png", "action": "strictly facing right side view, shadow teleport dual dagger strike"},
            {"file": "ShadowStalker_Death.png", "action": "strictly facing right side view, shadow dissolve death"}
        ]
    },
    # 5. WaveHeavy Monster (64x64px, 8 frames)
    {
        "name_tag": "WaveHeavy",
        "dir": os.path.join(BASE_DIR, "Monsters", "WaveHeavy"),
        "concept": os.path.join(BASE_DIR, "Monsters", "Monster_Concept_3_WaveHeavy.png"),
        "width": 64, "height": 64, "frame_count": 8,
        "items": [
            {"file": "WaveHeavy_Idle.png", "action": "strictly facing right side view, heavy idle stance holding energy hammer"},
            {"file": "WaveHeavy_Move.png", "action": "strictly facing right side view, heavy marching movement"},
            {"file": "WaveHeavy_Jump.png", "action": "strictly facing right side view, heavy leap jump"},
            {"file": "WaveHeavy_Attack.png", "action": "strictly facing right side view, hammer ground slam attack creating shockwave"},
            {"file": "WaveHeavy_Death.png", "action": "strictly facing right side view, heavy armor explosion breakdown"}
        ]
    }
]

def submit_v3_job(concept_b64, action, frame_count):
    url = "https://api.pixellab.ai/v2/animate-with-text-v3"
    payload = {
        "first_frame": {"type": "base64", "base64": concept_b64},
        "action": action,
        "frame_count": frame_count
    }
    r = requests.post(url, headers=HEADERS, json=payload)
    if r.status_code == 200:
        return r.json().get("background_job_id")
    return None

def wait_and_save_sheet(job_id, output_path):
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
                print(f"[PixelLab Smooth 8-Frame] Saved: {output_path} ({len(pil_images)} frames)")
            return True
        elif status == "failed":
            print(f"[PixelLab Smooth 8-Frame] Job failed: {data}")
            return False
        else:
            time.sleep(5)

def main():
    print("=== Generating Smooth 8-Frame Animations for ALL Units (Ground Cleanup & 8+ Frame Standard) ===")
    
    for group in ALL_TARGETS:
        os.makedirs(group["dir"], exist_ok=True)
        if not os.path.exists(group["concept"]):
            print(f"Concept missing: {group['concept']}")
            continue
            
        with open(group["concept"], "rb") as f:
            concept_b64 = base64.b64encode(f.read()).decode("utf-8")
            
        print(f"\n---> Processing {group['name_tag']} (8 Frames per Animation) <---")
        for item in group["items"]:
            output_file = os.path.join(group["dir"], item["file"])
            print(f"Generating smooth 8-frame animation for '{item['file']}'...")
            job_id = submit_v3_job(concept_b64, item["action"], group["frame_count"])
            if job_id:
                wait_and_save_sheet(job_id, output_file)
            time.sleep(2)

    print("\n=== All Units Successfully Upgraded to Smooth 8-Frame Animations with Clean Backgrounds ===")

if __name__ == "__main__":
    main()
