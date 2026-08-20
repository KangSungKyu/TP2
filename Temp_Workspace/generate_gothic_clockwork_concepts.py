import os
import requests
import json
import base64

API_KEY = "b8910d80-048e-42f7-8ff9-8c347b4d36bb"
HEADERS = {
    "Authorization": f"Bearer {API_KEY}",
    "Content-Type": "application/json"
}

PLAYER_DIR = r"c:\Users\PC\Projects\TP2\Assets\Textures\Characters\Player"
GARON_DIR = r"c:\Users\PC\Projects\TP2\Assets\Textures\Characters\Bosses\Garon"

os.makedirs(PLAYER_DIR, exist_ok=True)
os.makedirs(GARON_DIR, exist_ok=True)

def generate_pixellab_concept(description, output_path):
    url = "https://api.pixellab.ai/v2/create-image-pixen"
    payload = {
        "description": description,
        "image_size": {"width": 128, "height": 128},
        "no_background": True,
        "view": "side",
        "direction": "east"
    }
    r = requests.post(url, headers=HEADERS, json=payload)
    print(f"[{output_path}] Status:", r.status_code)
    if r.status_code == 200:
        data = r.json()
        b64 = data.get("image", {}).get("base64", "")
        if "," in b64:
            b64 = b64.split(",")[1]
        img_bytes = base64.b64decode(b64)
        with open(output_path, "wb") as f:
            f.write(img_bytes)
        print(f"Successfully saved concept asset: {output_path}")
        return True
    else:
        print("Error:", r.text)
        return False

def main():
    print("=== Generating Gothic Clockwork Steam Concept Assets ===")
    
    # 1. Player (Puppet Hunter - 인형 사냥꾼)
    player_desc = "full body pixel art character sprite, gothic dark fantasy puppet hunter warrior in dark leather trenchcoat, polished brass mechanical cybernetic arm on left shoulder, holding clockwork gear saw-blade katana sword, strictly facing right side view, 128x128 pixel art style, transparent background"
    player_out = os.path.join(PLAYER_DIR, "Player_Concept_Gothic.png")
    generate_pixellab_concept(player_desc, player_out)
    
    # Also update main Player_Concept.png
    main_player_out = os.path.join(PLAYER_DIR, "Player_Concept.png")
    generate_pixellab_concept(player_desc, main_player_out)

    # 2. Boss (Clockwork Commander Garon - 태엽 사령관 가론)
    garon_desc = "full body pixel art boss character sprite, towering gothic clockwork steam commander knight in dark brass plate armor, giant 3-pipe steam boiler mounted on back, wielding massive steam broadsword, strictly facing right side view, 128x128 pixel art style, transparent background"
    garon_out = os.path.join(GARON_DIR, "Garon_Concept_Gothic.png")
    generate_pixellab_concept(garon_desc, garon_out)
    
    # Also update main Garon_Concept.png
    main_garon_out = os.path.join(GARON_DIR, "Garon_Concept.png")
    generate_pixellab_concept(garon_desc, main_garon_out)

if __name__ == "__main__":
    main()
