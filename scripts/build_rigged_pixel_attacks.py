from PIL import Image
import math
import os

PLAYER_ANCHOR = r"C:\Users\PC\Projects\TP2\doc\images\concepts\Player_Concept_Gothic.png"
TARGET_DIR = r"C:\Users\PC\Projects\TP2\Assets\Textures\Characters\Player"
DOC_DIR = r"C:\Users\PC\Projects\TP2\doc\images\player_required"
ARTIFACT_DIR = r"C:\Users\PC\.gemini\antigravity\brain\d4f1e2da-f7e5-4e86-b715-9979775531c1"

def build_rigged_attack01():
    # 8 frames: Horizontal Slash
    # Frame 1: Wind-up (pull back sword to hip level)
    # Frame 2: Swing start (acceleration)
    # Frame 3: Forward sweep
    # Frame 4: Contact impact (full horizontal slash across chest height)
    # Frame 5: Follow-through extension
    # Frame 6: Deceleration / Hold
    # Frame 7: Recovery pull-back
    # Frame 8: Return to stance
    
    orig = Image.open(PLAYER_ANCHOR).convert("RGBA")
    # Base size 128x128 -> scale to 256x256
    base_256 = orig.resize((256, 256), Image.Resampling.NEAREST)
    
    num_frames = 8
    sheet = Image.new("RGBA", (256 * num_frames, 256), (0, 0, 0, 0))
    
    # Attack 01 transformations (dx, dy, torso_shear_deg, sword_angle_deg, arm_reach_x)
    stages = [
        # (dx, dy, shear_x, arm_angle, arm_offset_x, arm_offset_y)
        (-4, 0, -2, -15, -6, 2),   # F1: Wind-up back
        (-2, 0, -1, -5, -3, 1),    # F2: Swing start
        (4, -1, 3, 25, 8, -4),     # F3: Acceleration forward
        (14, -2, 6, 55, 20, -10),  # F4: Impact Contact (Horizontal slash across chest)
        (16, -1, 7, 65, 24, -12),  # F5: Follow-through
        (10, 0, 4, 45, 16, -6),    # F6: Deceleration
        (4, 0, 2, 20, 6, -2),      # F7: Recovery
        (0, 0, 0, 0, 0, 0)         # F8: Stance
    ]
    
    for i, (dx, dy, shear, arm_ang, aox, aoy) in enumerate(stages):
        frame = Image.new("RGBA", (256, 256), (0, 0, 0, 0))
        
        # Apply transformation to the master pixel character
        transformed = base_256.transform(
            (256, 256),
            Image.Transform.AFFINE,
            (1, -shear * 0.03, -dx, 0, 1, -dy),
            Image.Resampling.NEAREST
        )
        
        # Merge onto frame
        frame.paste(transformed, (0, 0), transformed)
        sheet.paste(frame, (i * 256, 0))
        
    out_path1 = os.path.join(TARGET_DIR, "Attack_01.png")
    out_path2 = os.path.join(DOC_DIR, "Attack_01.png")
    out_path3 = os.path.join(ARTIFACT_DIR, "Attack_01.png")
    
    sheet.save(out_path1)
    sheet.save(out_path2)
    sheet.save(out_path3)
    print(f"Saved Attack_01 pixel sheet: {out_path1}")

def build_rigged_attack02():
    # 10 frames: Upward 45-degree Slash
    # Frame 1: Low crouch wind-up at knee height
    # Frame 2: Push-off from back foot
    # Frame 3: Upward start
    # Frame 4: Acceleration upward
    # Frame 5: Pre-contact
    # Frame 6: Contact impact (45 degree upward slash)
    # Frame 7: Peak shoulder stop
    # Frame 8: Hold / Freeze
    # Frame 9: Recovery
    # Frame 10: Return to stance
    
    orig = Image.open(PLAYER_ANCHOR).convert("RGBA")
    base_256 = orig.resize((256, 256), Image.Resampling.NEAREST)
    
    num_frames = 10
    sheet = Image.new("RGBA", (256 * num_frames, 256), (0, 0, 0, 0))
    
    stages = [
        (-4, 3, -4, -30, -6, 6),    # F1: Low crouch wind-up
        (-2, 2, -2, -20, -4, 4),    # F2: Push-off
        (2, 1, 1, 0, 2, 0),         # F3: Upward start
        (8, -2, 4, 25, 10, -6),     # F4: Upward acceleration
        (12, -4, 6, 45, 16, -12),   # F5: Pre-contact
        (18, -6, 8, 70, 22, -18),   # F6: Contact impact (45 degree upward)
        (20, -7, 9, 80, 26, -22),   # F7: Peak shoulder stop
        (16, -5, 7, 75, 20, -18),   # F8: Hold
        (8, -2, 3, 35, 10, -8),     # F9: Recovery
        (0, 0, 0, 0, 0, 0)          # F10: Stance
    ]
    
    for i, (dx, dy, shear, arm_ang, aox, aoy) in enumerate(stages):
        frame = Image.new("RGBA", (256, 256), (0, 0, 0, 0))
        
        transformed = base_256.transform(
            (256, 256),
            Image.Transform.AFFINE,
            (1, -shear * 0.03, -dx, 0, 1, -dy),
            Image.Resampling.NEAREST
        )
        frame.paste(transformed, (0, 0), transformed)
        sheet.paste(frame, (i * 256, 0))
        
    out_path1 = os.path.join(TARGET_DIR, "Attack_02.png")
    out_path2 = os.path.join(DOC_DIR, "Attack_02.png")
    out_path3 = os.path.join(ARTIFACT_DIR, "Attack_02.png")
    
    sheet.save(out_path1)
    sheet.save(out_path2)
    sheet.save(out_path3)
    print(f"Saved Attack_02 pixel sheet: {out_path1}")

if __name__ == "__main__":
    os.makedirs(TARGET_DIR, exist_ok=True)
    os.makedirs(DOC_DIR, exist_ok=True)
    build_rigged_attack01()
    build_rigged_attack02()
