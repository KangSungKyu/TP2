import requests
import json
import base64
import os

API_KEY = "b8910d80-048e-42f7-8ff9-8c347b4d36bb"
HEADERS = {"Authorization": f"Bearer {API_KEY}", "Content-Type": "application/json"}
player_dir = r"c:\Users\PC\Projects\TP2\Assets\Textures\Characters\Player"
os.makedirs(player_dir, exist_ok=True)

prompts = {
    "Player_FullBody_Concept_1.png": "full body character sprite, head to toe visible, standing full body pose with feet and head completely visible in frame, sleek shadow cyber ronin wearing black tech conical straw hat, flowing cyan scarf, dark robes, holding katana sword facing right, 128x128 pixel art sprite, side view, no background",
    "Player_FullBody_Concept_2.png": "full body character sprite, complete head-to-feet visible standing pose, agile cyber samurai warrior with tech hat, glowing cyan scarf, katana, full body side view profile facing east, 128x128 pixel art, no background",
    "Player_FullBody_Concept_3.png": "full body character sprite, entire body from head to boots visible, dark oriental scifi shadow swordmaster with tech straw hat, glowing cyan scarf, katana stance, full body 128x128 pixel art, side view, no background"
}

def main():
    print("=== Generating Full-Body Head-to-Toe Player Concept Variations using PixelLab API ===")
    for filename, desc in prompts.items():
        print(f"Generating PixelLab Full-Body Concept: {filename}...")
        payload = {
            "description": desc,
            "image_size": {"width": 128, "height": 128},
            "no_background": True,
            "view": "side",
            "direction": "east"
        }
        r = requests.post("https://api.pixellab.ai/v2/create-image-pixen", headers=HEADERS, json=payload)
        if r.status_code == 200:
            b64 = r.json()["image"]["base64"]
            if "," in b64:
                b64 = b64.split(",")[1]
            save_path = os.path.join(player_dir, filename)
            with open(save_path, "wb") as f:
                f.write(base64.b64decode(b64))
            print(f"Successfully saved Full-Body Concept: {save_path}")
        else:
            print(f"Failed {filename}: {r.status_code} - {r.text}")

if __name__ == "__main__":
    main()
