from PIL import Image
import os
import math

PLAYER_ANCHOR = r"C:\Users\PC\Projects\TP2\doc\images\concepts\Player_Concept_Gothic.png"
TARGET_DIR = r"C:\Users\PC\Projects\TP2\Assets\Textures\Characters\Player"
DOC_DIR = r"C:\Users\PC\Projects\TP2\doc\images\player_required"
ARTIFACT_DIR = r"C:\Users\PC\.gemini\antigravity\brain\d4f1e2da-f7e5-4e86-b715-9979775531c1"

def build_dynamic_attack_01():
    orig = Image.open(PLAYER_ANCHOR).convert("RGBA")
    # 128x128 -> 256x256
    base = orig.resize((256, 256), Image.Resampling.NEAREST)
    
    # Isolate Body and Sword Arm
    # In Player_Concept_Gothic: 
    # Sword & Right hand starts around x: 20..120 (in 128x128 -> x: 40..240, y: 100..240 in 256x256)
    # We can crop the sword and arm cleanly
    w, h = base.size
    
    # Body without sword (mask out sword region)
    # The sword extends diagonally down-right from (75, 135) to (240, 220)
    body = base.copy()
    body_pixels = body.load()
    sword_img = Image.new("RGBA", (256, 256), (0, 0, 0, 0))
    sword_pixels = sword_img.load()
    
    for y in range(h):
        for x in range(w):
            r, g, b, a = base.getpixel((x, y))
            if a > 10:
                # Detect sword blade & handle region
                is_sword = False
                # Sword line approx: y = 0.52 * x + 96 for x in [70, 245], y in [130, 225]
                # Also handle at (65..90, 125..150)
                dist_to_sword_line = abs(0.52 * x - y + 96)
                if dist_to_sword_line < 18 and x > 65 and y > 120:
                    is_sword = True
                elif x > 90 and y > 140 and y < 225 and dist_to_sword_line < 25:
                    is_sword = True
                    
                if is_sword:
                    sword_pixels[x, y] = (r, g, b, a)
                    # Erase from body
                    body_pixels[x, y] = (0, 0, 0, 0)
    
    num_frames = 8
    sheet = Image.new("RGBA", (256 * num_frames, 256), (0, 0, 0, 0))
    
    # 8-Frame Slash Choreography:
    # 1: Windup back (sword pulled back to hip)
    # 2: Swing start (acceleration)
    # 3: Mid swing (sweep forward)
    # 4: Contact frame (full horizontal slash across chest height, 0 deg)
    # 5: Follow-through (sword sweeps to the left/down)
    # 6: Deceleration
    # 7: Recovery
    # 8: Stance
    
    # (body_dx, body_dy, sword_rot_deg, sword_dx, sword_dy)
    keyframes = [
        (-4, 0, -20, -10, 5),   # F1: Windup back
        (-2, 0, -5, -4, 2),     # F2: Swing start
        (6, -2, 25, 12, -15),   # F3: Mid swing
        (18, -4, 55, 36, -38),  # F4: CONTACT (Horizontal slash across chest)
        (20, -2, 45, 38, -32),  # F5: Follow-through
        (12, 0, 25, 20, -18),   # F6: Deceleration
        (4, 0, 10, 8, -6),      # F7: Recovery
        (0, 0, 0, 0, 0)         # F8: Stance
    ]
    
    for i, (bdx, bdy, rot, sdx, sdy) in enumerate(keyframes):
        frame = Image.new("RGBA", (256, 256), (0, 0, 0, 0))
        
        # Paste body
        frame.paste(body, (bdx, bdy), body)
        
        # Rotate sword around pivot (approx shoulder/elbow at 80, 135)
        pivot = (80, 135)
        # Rotate image around pivot
        rotated_sword = sword_img.rotate(rot, resample=Image.Resampling.NEAREST, center=pivot)
        
        # Merge rotated sword onto frame
        frame.paste(rotated_sword, (bdx + sdx, bdy + sdy), rotated_sword)
        
        sheet.paste(frame, (i * 256, 0))
        
    out1 = os.path.join(TARGET_DIR, "Attack_01.png")
    out2 = os.path.join(DOC_DIR, "Attack_01.png")
    out3 = os.path.join(ARTIFACT_DIR, "Attack_01.png")
    sheet.save(out1)
    sheet.save(out2)
    sheet.save(out3)
    print(f"Generated Dynamic Attack_01 sheet: {out1}")

def build_dynamic_attack_02():
    orig = Image.open(PLAYER_ANCHOR).convert("RGBA")
    base = orig.resize((256, 256), Image.Resampling.NEAREST)
    w, h = base.size
    
    body = base.copy()
    body_pixels = body.load()
    sword_img = Image.new("RGBA", (256, 256), (0, 0, 0, 0))
    sword_pixels = sword_img.load()
    
    for y in range(h):
        for x in range(w):
            r, g, b, a = base.getpixel((x, y))
            if a > 10:
                dist_to_sword_line = abs(0.52 * x - y + 96)
                if dist_to_sword_line < 18 and x > 65 and y > 120:
                    sword_pixels[x, y] = (r, g, b, a)
                    body_pixels[x, y] = (0, 0, 0, 0)
                elif x > 90 and y > 140 and y < 225 and dist_to_sword_line < 25:
                    sword_pixels[x, y] = (r, g, b, a)
                    body_pixels[x, y] = (0, 0, 0, 0)

    num_frames = 10
    sheet = Image.new("RGBA", (256 * num_frames, 256), (0, 0, 0, 0))
    
    # 10-Frame 45-degree Upward Slash Choreography:
    # 1: Low crouch windup at knee height
    # 2: Push-off from back foot
    # 3: Upward swing start from ground
    # 4: Acceleration upward
    # 5: Pre-contact
    # 6: CONTACT (45 degree upward slash at chest/chin height)
    # 7: Peak shoulder stop (high overhead finish)
    # 8: Hold / Freeze
    # 9: Recovery
    # 10: Stance
    
    keyframes = [
        (-6, 4, -35, -12, 14),   # F1: Low crouch
        (-3, 2, -25, -6, 8),     # F2: Push-off
        (2, 1, -10, 2, 2),       # F3: Upward start
        (8, -1, 15, 14, -12),    # F4: Acceleration
        (14, -3, 40, 24, -26),   # F5: Pre-contact
        (22, -6, 75, 42, -52),   # F6: CONTACT (45 deg upward slash)
        (24, -8, 90, 46, -65),   # F7: Peak shoulder stop
        (20, -6, 85, 40, -58),   # F8: Hold / Freeze
        (10, -2, 40, 18, -24),   # F9: Recovery
        (0, 0, 0, 0, 0)          # F10: Stance
    ]
    
    pivot = (80, 135)
    for i, (bdx, bdy, rot, sdx, sdy) in enumerate(keyframes):
        frame = Image.new("RGBA", (256, 256), (0, 0, 0, 0))
        frame.paste(body, (bdx, bdy), body)
        rotated_sword = sword_img.rotate(rot, resample=Image.Resampling.NEAREST, center=pivot)
        frame.paste(rotated_sword, (bdx + sdx, bdy + sdy), rotated_sword)
        sheet.paste(frame, (i * 256, 0))
        
    out1 = os.path.join(TARGET_DIR, "Attack_02.png")
    out2 = os.path.join(DOC_DIR, "Attack_02.png")
    out3 = os.path.join(ARTIFACT_DIR, "Attack_02.png")
    sheet.save(out1)
    sheet.save(out2)
    sheet.save(out3)
    print(f"Generated Dynamic Attack_02 sheet: {out1}")

if __name__ == "__main__":
    build_dynamic_attack_01()
    build_dynamic_attack_02()
