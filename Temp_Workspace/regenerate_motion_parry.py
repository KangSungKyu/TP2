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

def main():
    print("=== Regenerating Player_Parry with Swinging Katana Deflect Slash Motion (8 Frames) ===")
    
    url = "https://api.pixellab.ai/v2/animate-with-text-v3"
    payload = {
        "first_frame": {"type": "base64", "base64": CONCEPT_B64},
        "action": "swinging katana wide across body to deflect and slash incoming attack",
        "frame_count": 8
    }
    
    r = requests.post(url, headers=HEADERS, json=payload)
    print("Submit status:", r.status_code)
    
    if r.status_code == 200:
        job_id = r.json().get("background_job_id")
        print(f"[PixelLab] Job Submitted (Regenerate Player_Parry): Job ID = {job_id}")
        
        job_url = f"https://api.pixellab.ai/v2/background-jobs/{job_id}"
        while True:
            r2 = requests.get(job_url, headers=HEADERS)
            if r2.status_code != 200:
                time.sleep(5)
                continue
                
            data = r2.json()
            status = data.get("status")
            if status == "completed":
                images_data = data.get("last_response", {}).get("images", [])
                print(f"[PixelLab] Job COMPLETED! Received {len(images_data)} frames.")
                
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
                        
                    output_path = os.path.join(PLAYER_DIR, "Player_Parry.png")
                    sheet.save(output_path, "PNG")
                    print(f"[PixelLab] Successfully saved regenerated Player_Parry: {output_path} ({len(pil_images)} frames, {w}x{h})")
                break
            elif status == "failed":
                print(f"[PixelLab] Job FAILED: {data}")
                break
            else:
                prog = data.get("last_response", {}).get("progress", 0.0)
                print(f"[PixelLab] Regenerating Player_Parry... ({int(prog * 100)}%)")
                time.sleep(4)
    else:
        print("Submission failed:", r.status_code, r.text)

if __name__ == "__main__":
    main()
