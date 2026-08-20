import os
import requests
import json
import base64
import shutil

API_KEY = "b8910d80-048e-42f7-8ff9-8c347b4d36bb"
HEADERS = {
    "Authorization": f"Bearer {API_KEY}",
    "Content-Type": "application/json"
}

MONSTER_DIR = r"c:\Users\PC\Projects\TP2\Assets\Textures\Characters\Monsters"
DOC_CONCEPTS_DIR = r"c:\Users\PC\Projects\TP2\doc\images\concepts"

os.makedirs(MONSTER_DIR, exist_ok=True)
os.makedirs(DOC_CONCEPTS_DIR, exist_ok=True)

MONSTERS_INFO = [
    {
        "filename": "Monster_3101_SpearSentry_Concept.png",
        "desc": "full body pixel art monster sprite, gothic clockwork piston spearman automaton with brass mask and worn guard uniform, holding long extendable mechanical piston spear, sharp black outline, 128x128 pixel art style, strictly facing right side view, transparent background"
    },
    {
        "filename": "Monster_3102_ShadowStalker_Concept.png",
        "desc": "full body pixel art monster sprite, lightweight gothic steam assassin automaton with gear wings and wire joints, dual-wielding clockwork saw-blade daggers, sharp black outline, 128x128 pixel art style, strictly facing right side view, transparent background"
    },
    {
        "filename": "Monster_3103_WaveHeavy_Concept.png",
        "desc": "full body pixel art monster sprite, massive gothic brass steam boiler golem automaton, holding huge steam crushing hammer, sharp black outline, 128x128 pixel art style, strictly facing right side view, transparent background"
    },
    {
        "filename": "Monster_3104_ShieldSentinel_Concept.png",
        "desc": "full body pixel art monster sprite, heavy brass steam sentinel automaton holding giant iron clocktower door shield, sharp black outline, 128x128 pixel art style, strictly facing right side view, transparent background"
    },
    {
        "filename": "Monster_3105_OrbitalMarksman_Concept.png",
        "desc": "full body pixel art monster sprite, clockwork sniper automaton with glowing lens eye and mechanical scope, holding long steam crossbow rifle, sharp black outline, 128x128 pixel art style, strictly facing right side view, transparent background"
    }
]

def generate_monster_concept(desc, filename):
    url = "https://api.pixellab.ai/v2/create-image-pixen"
    payload = {
        "description": desc,
        "image_size": {"width": 128, "height": 128},
        "no_background": True,
        "view": "side",
        "direction": "east"
    }
    r = requests.post(url, headers=HEADERS, json=payload)
    print(f"[{filename}] Status:", r.status_code)
    if r.status_code == 200:
        data = r.json()
        b64 = data.get("image", {}).get("base64", "")
        if "," in b64:
            b64 = b64.split(",")[1]
        img_bytes = base64.b64decode(b64)
        
        target_path1 = os.path.join(MONSTER_DIR, filename)
        target_path2 = os.path.join(DOC_CONCEPTS_DIR, filename)
        
        with open(target_path1, "wb") as f:
            f.write(img_bytes)
        shutil.copy(target_path1, target_path2)
        print(f"Successfully saved monster concept: {target_path1}")
        return True
    else:
        print("Error:", r.text)
        return False

def main():
    print("=== Generating Gothic Clockwork Style Unified 5 Normal Monster Concept Assets ===")
    for m in MONSTERS_INFO:
        generate_monster_concept(m["desc"], m["filename"])

if __name__ == "__main__":
    main()
