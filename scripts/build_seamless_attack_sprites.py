from PIL import Image
import os

PLAYER_ANCHOR = r"C:\Users\PC\Projects\TP2\doc\images\concepts\Player_Concept_Gothic.png"
TARGET_DIR = r"C:\Users\PC\Projects\TP2\Assets\Textures\Characters\Player"
DOC_DIR = r"C:\Users\PC\Projects\TP2\doc\images\player_required"
ARTIFACT_DIR = r"C:\Users\PC\.gemini\antigravity\brain\d4f1e2da-f7e5-4e86-b715-9979775531c1"

def build_seamless_attack01():
    orig = Image.open(PLAYER_ANCHOR).convert("RGBA")
    base = orig.resize((256, 256), Image.Resampling.NEAREST)
    w, h = base.size
    
    # 1. Extract sword cleanly
    sword_img = Image.new("RGBA", (256, 256), (0, 0, 0, 0))
    sword_pix = sword_img.load()
    body_img = base.copy()
    body_pix = body_img.load()
    
    # Coat color palette for in-painting behind the sword
    coat_color = (46, 31, 26, 255)
    coat_dark = (26, 18, 15, 255)
    
    for y in range(h):
        for x in range(w):
            r, g, b, a = base.getpixel((x, y))
            if a > 10:
                dist = abs(0.52 * x - y + 96)
                if dist < 16 and x > 65 and y > 120:
                    sword_pix[x, y] = (r, g, b, a)
                    # If this is on the body/coat region (x < 150), fill with coat color
                    if x < 150 and y < 220:
                        body_pix[x, y] = coat_color if (x + y) % 2 == 0 else coat_dark
                    else:
                        body_pix[x, y] = (0, 0, 0, 0)
                elif x > 90 and y > 140 and y < 225 and dist < 22:
                    sword_pix[x, y] = (r, g, b, a)
                    if x < 150 and y < 220:
                        body_pix[x, y] = coat_color if (x + y) % 2 == 0 else coat_dark
                    else:
                        body_pix[x, y] = (0, 0, 0, 0)

    num_frames = 8
    sheet = Image.new("RGBA", (256 * num_frames, 256), (0, 0, 0, 0))
    
    # Keyframe stages:
    # 1: Wind-up (pull back sword to hip)
    # 2: Swing start (acceleration)
    # 3: Mid swing
    # 4: CONTACT (Full horizontal slash across chest height)
    # 5: Follow-through extension
    # 6: Deceleration
    # 7: Recovery
    # 8: Return to stance
    keyframes = [
        (-4, 0, -18, -8, 4),    # F1: Windup back
        (-2, 0, -5, -4, 2),     # F2: Swing start
        (6, -2, 22, 10, -12),   # F3: Mid swing
        (18, -4, 52, 32, -34),  # F4: CONTACT (Horizontal slash across chest)
        (20, -2, 42, 34, -28),  # F5: Follow-through
        (12, 0, 22, 18, -14),   # F6: Deceleration
        (4, 0, 8, 6, -4),       # F7: Recovery
        (0, 0, 0, 0, 0)         # F8: Stance
    ]
    
    pivot = (80, 135)
    for i, (bdx, bdy, rot, sdx, sdy) in enumerate(keyframes):
        frame = Image.new("RGBA", (256, 256), (0, 0, 0, 0))
        frame.paste(body_img, (bdx, bdy), body_img)
        rotated_sword = sword_img.rotate(rot, resample=Image.Resampling.NEAREST, center=pivot)
        frame.paste(rotated_sword, (bdx + sdx, bdy + sdy), rotated_sword)
        sheet.paste(frame, (i * 256, 0))
        
    out1 = os.path.join(TARGET_DIR, "Attack_01.png")
    out2 = os.path.join(DOC_DIR, "Attack_01.png")
    out3 = os.path.join(ARTIFACT_DIR, "Attack_01.png")
    sheet.save(out1)
    sheet.save(out2)
    sheet.save(out3)
    print(f"Generated Seamless Attack_01: {out1}")

def build_seamless_attack02():
    orig = Image.open(PLAYER_ANCHOR).convert("RGBA")
    base = orig.resize((256, 256), Image.Resampling.NEAREST)
    w, h = base.size
    
    sword_img = Image.new("RGBA", (256, 256), (0, 0, 0, 0))
    sword_pix = sword_img.load()
    body_img = base.copy()
    body_pix = body_img.load()
    
    coat_color = (46, 31, 26, 255)
    coat_dark = (26, 18, 15, 255)
    
    for y in range(h):
        for x in range(w):
            r, g, b, a = base.getpixel((x, y))
            if a > 10:
                dist = abs(0.52 * x - y + 96)
                if dist < 16 and x > 65 and y > 120:
                    sword_pix[x, y] = (r, g, b, a)
                    if x < 150 and y < 220:
                        body_pix[x, y] = coat_color if (x + y) % 2 == 0 else coat_dark
                    else:
                        body_pix[x, y] = (0, 0, 0, 0)
                elif x > 90 and y > 140 and y < 225 and dist < 22:
                    sword_pix[x, y] = (r, g, b, a)
                    if x < 150 and y < 220:
                        body_pix[x, y] = coat_color if (x + y) % 2 == 0 else coat_dark
                    else:
                        body_pix[x, y] = (0, 0, 0, 0)

    num_frames = 10
    sheet = Image.new("RGBA", (256 * num_frames, 256), (0, 0, 0, 0))
    
    # 10-frame 45-degree upward slash
    keyframes = [
        (-6, 4, -32, -10, 12),   # F1: Low crouch windup
        (-3, 2, -22, -5, 6),     # F2: Push-off
        (2, 1, -8, 2, 2),        # F3: Upward start
        (8, -1, 15, 12, -10),    # F4: Acceleration
        (14, -3, 38, 22, -24),   # F5: Pre-contact
        (22, -6, 70, 38, -48),   # F6: CONTACT (45 deg upward slash)
        (24, -8, 85, 42, -60),   # F7: Peak shoulder stop
        (20, -6, 80, 36, -54),   # F8: Hold / Freeze
        (10, -2, 35, 16, -22),   # F9: Recovery
        (0, 0, 0, 0, 0)          # F10: Stance
    ]
    
    pivot = (80, 135)
    for i, (bdx, bdy, rot, sdx, sdy) in enumerate(keyframes):
        frame = Image.new("RGBA", (256, 256), (0, 0, 0, 0))
        frame.paste(body_img, (bdx, bdy), body_img)
        rotated_sword = sword_img.rotate(rot, resample=Image.Resampling.NEAREST, center=pivot)
        frame.paste(rotated_sword, (bdx + sdx, bdy + sdy), rotated_sword)
        sheet.paste(frame, (i * 256, 0))
        
    out1 = os.path.join(TARGET_DIR, "Attack_02.png")
    out2 = os.path.join(DOC_DIR, "Attack_02.png")
    out3 = os.path.join(ARTIFACT_DIR, "Attack_02.png")
    sheet.save(out1)
    sheet.save(out2)
    sheet.save(out3)
    print(f"Generated Seamless Attack_02: {out1}")

if __name__ == "__main__":
    build_seamless_attack01()
    build_seamless_attack02()
