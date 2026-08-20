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
CONCEPT_PATH = os.path.join(PLAYER_DIR, "Player_Concept_Gothic.png")

with open(CONCEPT_PATH, "rb") as f:
    CONCEPT_B64 = base64.b64encode(f.read()).decode("utf-8")

GROUP1_REQUIRED_MOTIONS = [
    {"file": "Idle.png", "frames": 8, "action": "breathing softly in idle stance with subtle steam vent emissions"},
    {"file": "Run.png", "frames": 8, "action": "running forward fast in athletic stride with longcoat flowing behind"},
    {"file": "Jump_Start.png", "frames": 4, "action": "bending knees and launching high upward into midair"},
    {"file": "Jump_Loop.png", "frames": 4, "action": "airborne hovering at peak of jump holding sword downward"},
    {"file": "Fall.png", "frames": 4, "action": "falling downward fast in midair with coat fluttering upward"},
    {"file": "Land.png", "frames": 4, "action": "landing on ground absorbing impact in low crouching posture"},
    {"file": "Attack_01.png", "frames": 8, "action": "fast horizontal chest level slash with spinning sawblade katana"},
    {"file": "Attack_02.png", "frames": 8, "action": "rising diagonal upward slash from hip to shoulder with brass arm spark"},
    {"file": "Hit.png", "frames": 4, "action": "recoiling backward from impact with torso leaning back"},
    {"file": "Groggy.png", "frames": 8, "action": "staggering low on knees in exhausted groggy loop"},
    {"file": "Death.png", "frames": 8, "action": "collapsing onto ground as mechanical gear arm stops completely"}
]

def generate_motion_sheet_256(motion_info):
    filename = motion_info["file"]
    frame_count = motion_info["frames"]
    action = motion_info["action"]
    output_path = os.path.join(PLAYER_DIR, filename)
    
    print(f"\n--- Generating Player 256x256 Motion: {filename} ({frame_count} frames) ---")
    url = "https://api.pixellab.ai/v2/animate-with-text-v3"
    payload = {
        "first_frame": {"type": "base64", "base64": CONCEPT_B64},
        "action": action,
        "frame_count": frame_count
    }
    
    r = requests.post(url, headers=HEADERS, json=payload)
    if r.status_code != 200:
        print(f"Submit error ({filename}):", r.status_code, r.text)
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
                # High quality Lanczos resize to 256x256 per frame
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
            print(f"Job failed ({filename}):", data)
            return False
        else:
            time.sleep(4)
    return False

def main():
    print("=== Generating Player 256x256 Group 1 Required 11 Motion Sheets ===")
    for item in GROUP1_REQUIRED_MOTIONS:
        generate_motion_sheet_256(item)
        time.sleep(2)

if __name__ == "__main__":
    main()
