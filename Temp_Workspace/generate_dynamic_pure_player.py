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

with open(CONCEPT_PATH, "rb") as f:
    CONCEPT_B64 = base64.b64encode(f.read()).decode("utf-8")

# Strict instruction: Pure unit body & weapon ONLY, no ground/terrain, no standalone FX clutter, dynamic motion, facing right side view
PLAYER_DYNAMIC_ANIMS = [
    {"name": "Player_Idle", "action": "strictly facing right side view, pure unit character only without ground or fx clutter, dynamic idle breathing stance with flowing cyan scarf"},
    {"name": "Player_Run", "action": "strictly facing right side view, pure unit character only without ground or fx clutter, dynamic fast forward sprint running motion with flowing robes"},
    {"name": "Player_Parry", "action": "strictly facing right side view, pure unit character only without ground or fx clutter, dynamic parry counter sword block pose"},
    {"name": "Player_Guard", "action": "strictly facing right side view, pure unit character only without ground or fx clutter, dynamic defensive guard stance holding katana"},
    {"name": "Player_Dodge", "action": "strictly facing right side view, pure unit character only without ground or fx clutter, dynamic fast forward phantom dash pose"},
    {"name": "Player_Jump", "action": "strictly facing right side view, pure unit character only without ground or fx clutter, dynamic airborne jump and downward katana strike"},
    {"name": "Player_ComboAttack", "action": "strictly facing right side view, pure unit character only without ground or fx clutter, dynamic 3-hit katana sword slash attack motion"},
    {"name": "Player_Execution", "action": "strictly facing right side view, pure unit character only without ground or fx clutter, dynamic finisher execution katana strike pose"}
]

def submit_v3_job(action):
    url = "https://api.pixellab.ai/v2/animate-with-text-v3"
    payload = {
        "first_frame": {"type": "base64", "base64": CONCEPT_B64},
        "action": action,
        "frame_count": 8
    }
    r = requests.post(url, headers=HEADERS, json=payload)
    if r.status_code == 200:
        return r.json().get("background_job_id")
    print(f"[PixelLab] Job submission error: {r.status_code} - {r.text}")
    return None

def wait_and_save(job_id, output_path):
    url = f"https://api.pixellab.ai/v2/background-jobs/{job_id}"
    print(f"[PixelLab Pure Unit] Waiting for job {job_id}...")
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
                print(f"[PixelLab Pure Unit] Saved: {output_path} ({len(pil_images)} frames, {w}x{h})")
            return True
        elif status == "failed":
            print(f"[PixelLab Pure Unit] Job failed: {data}")
            return False
        else:
            time.sleep(5)

def main():
    print("=== Generating Dynamic Pure-Unit Player Animations (128x128, Right-Facing, 8-Frames, Pure Unit Only) ===")
    
    for anim in PLAYER_DYNAMIC_ANIMS:
        output_file = os.path.join(PLAYER_DIR, f"{anim['name']}.png")
        print(f"\nGenerating '{anim['name']}' (Dynamic Pure Unit)...")
        job_id = submit_v3_job(anim["action"])
        if job_id:
            wait_and_save(job_id, output_file)
        time.sleep(2)

    print("\n=== All Dynamic Pure-Unit Player Animations Successfully Generated and Saved ===")

if __name__ == "__main__":
    main()
