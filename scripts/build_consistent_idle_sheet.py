from PIL import Image
import math
import os

def create_procedural_idle_sheet():
    src_path = r"C:\Users\PC\Projects\TP2\Assets\Textures\Characters\Player\Player_Concept_Gothic.png"
    out_dir = r"C:\Users\PC\.gemini\antigravity\brain\d4f1e2da-f7e5-4e86-b715-9979775531c1"
    out_sheet_path = os.path.join(out_dir, "Player_256_Idle_Consistent_Sheet.png")

    img = Image.open(src_path).convert("RGBA")
    # Resize canvas to 256x256 while keeping crisp pixel aspect ratio
    # Target character height ~200px centered in 256x256
    w, h = img.size
    scale = 256.0 / max(w, h) * 0.88
    new_w = int(w * scale)
    new_h = int(h * scale)
    base_char = img.resize((new_w, new_h), Image.Resampling.NEAREST)

    frame_count = 6
    sheet_w = 256 * frame_count
    sheet_h = 256
    sheet = Image.new("RGBA", (sheet_w, sheet_h), (0, 0, 0, 0))

    # Center base offsets
    base_x = (256 - new_w) // 2
    base_y = (256 - new_h) // 2 + 10

    for i in range(frame_count):
        # Subtle organic breathing curve: Y-offset + torso scale stretch/squash
        t = (i / frame_count) * 2 * math.pi
        y_shift = int(round(math.sin(t) * 3))  # 0 -> +3 -> 0 -> -3 -> 0
        scale_y = 1.0 + math.sin(t) * 0.015

        frame_w = new_w
        frame_h = int(new_h * scale_y)
        frame_char = base_char.resize((frame_w, frame_h), Image.Resampling.NEAREST)

        # Composite onto 256x256 cell
        cell_img = Image.new("RGBA", (256, 256), (0, 0, 0, 0))
        pos_x = (256 - frame_w) // 2
        pos_y = base_y - y_shift - (frame_h - new_h)

        cell_img.paste(frame_char, (pos_x, pos_y), frame_char)

        # Paste cell to master sheet
        sheet.paste(cell_img, (i * 256, 0))

    sheet.save(out_sheet_path)
    print(f"Consistent 256x256 Idle Sprite Sheet saved to: {out_sheet_path}")

if __name__ == "__main__":
    create_procedural_idle_sheet()
