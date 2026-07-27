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
MAIN_CONCEPT_PATH = os.path.join(PLAYER_DIR, "Player_Concept.png")

with open(MAIN_CONCEPT_PATH, "rb") as f:
    CONCEPT_B64 = base64.b64encode(f.read()).decode("utf-8")

# Strict specifications: Full body head to toe, pure unit body & katana only, no ground, no terrain, no fx clutter, facing right side view
# 1타 (8프레임), 2타 (8프레임), 3타 (8프레임), 처형 연출 (12프레임)
PLAYER_CUSTOM_ANIMS = [
    {"name": "Player_Idle", "action": "strictly facing right side view, full body head to toe visible standing pose, pure unit character only without ground or fx clutter, dynamic idle breathing stance with flowing cyan scarf", "frames": 8},
    {"name": "Player_Run", "action": "strictly facing right side view, full body head to toe visible running pose, pure unit character only without ground or fx clutter, fast forward sprint running motion with flowing robes", "frames": 8},
    {"name": "Player_Parry", "action": "strictly facing right side view, full body head to toe visible pose, pure unit character only without ground or fx clutter, dynamic parry counter sword block pose", "frames": 8},
    {"name": "Player_Guard", "action": "strictly facing right side view, full body head to toe visible pose, pure unit character only without ground or fx clutter, defensive guard stance holding katana", "frames": 8},
    {"name": "Player_Dodge", "action": "strictly facing right side view, full body head to toe visible pose, pure unit character only without ground or fx clutter, fast forward phantom dash pose", "frames": 8},
    {"name": "Player_Jump", "action": "strictly facing right side view, full body head to toe visible pose in air, pure unit character only without ground or fx clutter, airborne jump and downward katana strike", "frames": 8},
    
    # 기본 공격 3연타 (1타 8프레임, 2타 8프레임, 3타 8프레임)
    {"name": "Player_Attack_Hit1", "action": "strictly facing right side view, full body head to toe visible pose, pure unit character only without ground or fx clutter, 1st hit horizontal katana slash attack stance", "frames": 8},
    {"name": "Player_Attack_Hit2", "action": "strictly facing right side view, full body head to toe visible pose, pure unit character only without ground or fx clutter, 2nd hit upward diagonal katana slash attack stance", "frames": 8},
    {"name": "Player_Attack_Hit3", "action": "strictly facing right side view, full body head to toe visible pose, pure unit character only without ground or fx clutter, 3rd hit spinning heavy downward katana slash attack stance", "frames": 8},
    
    # 처형 연출 (12프레임)
    {"name": "Player_Execution", "action": "strictly facing right side view, full body head to toe visible pose, pure unit character only without ground or fx clutter, 12 frame cinematic execution finisher katana strike pose", "frames": 12}
]

def submit_v3_job(action, frame_count):
    url = "https://api.pixellab.ai/v2/animate-with-text-v3"
    payload = {
        "first_frame": {"type": "base64", "base64": CONCEPT_B64},
        "action": action,
        "frame_count": frame_count
    }
    r = requests.post(url, headers=HEADERS, json=payload)
    if r.status_code == 200:
        return r.json().get("background_job_id")
    print(f"[PixelLab] Job submission error: {r.status_code} - {r.text}")
    return None

def wait_and_save(job_id, output_path, frame_count):
    url = f"https://api.pixellab.ai/v2/background-jobs/{job_id}"
    print(f"[PixelLab Player Custom ({frame_count} frames)] Waiting for job {job_id}...")
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
                print(f"[PixelLab Player Custom] Saved: {output_path} ({len(pil_images)} frames, {w}x{h})")
            return True
        elif status == "failed":
            print(f"[PixelLab Player Custom] Job failed: {data}")
            return False
        else:
            time.sleep(5)

def main():
    print("=== Generating Player Custom Frame Animations (Hit 1/2/3 each 8 frames, Execution 12 frames) ===")
    
    for anim in PLAYER_CUSTOM_ANIMS:
        output_file = os.path.join(PLAYER_DIR, f"{anim['name']}.png")
        print(f"\nGenerating '{anim['name']}' ({anim['frames']} frames)...")
        job_id = submit_v3_job(anim["action"], anim["frames"])
        if job_id:
            wait_and_save(job_id, output_file, anim["frames"])
        time.sleep(2)

    print("\n=== All Player Custom Frame Animations Successfully Generated and Saved ===")

if __name__ == "__main__":
    main()
