import os
import time
import base64
import requests
from PIL import Image
import io

API_KEY = os.environ.get("PIXELLAB_API_TOKEN")
if not API_KEY:
    raise RuntimeError("PIXELLAB_API_TOKEN is required")
HEADERS = {
    "Authorization": f"Bearer {API_KEY}",
    "Content-Type": "application/json"
}

PLAYER_DIR = r"c:\Users\PC\Projects\TP2\Assets\Textures\Characters\Player"
CONCEPT_PATH = os.path.join(PLAYER_DIR, "Player_Concept_Gothic.png")

with open(CONCEPT_PATH, "rb") as f:
    CONCEPT_B64 = base64.b64encode(f.read()).decode("utf-8")

GROUP2_COMBAT_MOTIONS = [
    {"file": "Charge.png", "frames": 6, "action": "charging power with spinning sawblade katana held back in dynamic low stance"},
    {"file": "Guard_Start.png", "frames": 3, "action": "raising brass mechanical arm forward transitioning into defensive guard stance"},
    {"file": "Guard_Loop.png", "frames": 4, "action": "holding defensive guard posture with rotating brass mechanical arm shield"},
    {"file": "Guard_Hit.png", "frames": 4, "action": "recoiling slightly from blocked impact in guard stance with metallic sparks"},
    {"file": "Parry.png", "frames": 6, "action": "deflecting attack swiftly with brass mechanical arm in crescent upward parry arc with blue sparks"},
    {"file": "Dodge.png", "frames": 6, "action": "quick backward evasive roll and agile retreat step"},
    {"file": "Dash.png", "frames": 6, "action": "fast forward phantom slide dash emitting high pressure white steam burst"},
    {"file": "Execution.png", "frames": 8, "action": "lunging forward thrusting sawblade katana deep into target and detonating steam explosion"},
    {"file": "Executed.png", "frames": 8, "action": "being grabbed and slammed by heavy boss attack in helpless hit posture"}
]

def generate_motion_sheet_256(motion_info):
    filename = motion_info["file"]
    frame_count = motion_info["frames"]
    action = motion_info["action"]
    output_path = os.path.join(PLAYER_DIR, filename)

    print(f"\n--- Generating Player 256x256 Combat Motion: {filename} ({frame_count} frames) ---")
    url = "https://api.pixellab.ai/v2/animate-with-text-v3"
    payload = {
        "first_frame": {"type": "base64", "base64": CONCEPT_B64},
        "action": action,
        "frame_count": frame_count
    }

    r = requests.post(url, headers=HEADERS, json=payload)
    if r.status_code != 200:
        print(f"Submit error ({filename}): HTTP {r.status_code}")
        return False

    job_id = r.json().get("background_job_id")
    print(f"Job queued ({filename}): ID = {job_id}")

    job_url = f"https://api.pixellab.ai/v2/background-jobs/{job_id}"
    while True:
        r2 = requests.get(job_url, headers=HEADERS)
        if r2.status_code != 200:
            time.sleep(4)
            continue

        data = r2.json()
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
                img_256 = img.resize((256, 256), Image.Resampling.LANCZOS)
                pil_images.append(img_256)

            if pil_images:
                w, h = 256, 256
                sheet = Image.new("RGBA", (w * len(pil_images), h))
                for i, img in enumerate(pil_images):
                    sheet.paste(img, (i * w, 0))
                sheet.save(output_path, "PNG")
                print(f"Successfully saved 256x256 sheet: {output_path} ({len(pil_images)} frames)")
                return True
            break
        elif status == "failed":
            print(f"Job failed ({filename})")
            return False
        else:
            time.sleep(4)
    return False

def main():
    print("=== Generating Player 256x256 Group 2 Combat Optional 9 Motion Sheets ===")
    for item in GROUP2_COMBAT_MOTIONS:
        generate_motion_sheet_256(item)
        time.sleep(2)

if __name__ == "__main__":
    main()
