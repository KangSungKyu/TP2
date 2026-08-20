from PIL import Image, ImageDraw
import os
import math

POSE_DIR = r"C:\Users\PC\Projects\TP2\doc\images\pose_skeletons"

# OpenPose 18 keypoints colors & connections
# 0: Nose, 1: Neck, 2: RShoulder, 3: RElbow, 4: RWrist, 5: LShoulder, 6: LElbow, 7: LWrist,
# 8: RHip, 9: RKnee, 10: RAnkle, 11: LHip, 12: LKnee, 13: LAnkle, 14: REye, 15: LEye, 16: REar, 17: LEar

LIMB_COLORS = [
    (255, 0, 0), (255, 85, 0), (255, 170, 0), (255, 255, 0), (170, 255, 0), (85, 255, 0),
    (0, 255, 0), (0, 255, 85), (0, 255, 170), (0, 255, 255), (0, 170, 255), (0, 85, 255),
    (0, 0, 255), (85, 0, 255), (170, 0, 255), (255, 0, 255), (255, 0, 170), (255, 0, 85)
]

def draw_openpose_skeleton(keypoints, size=(256, 256)):
    img = Image.new("RGB", size, (0, 0, 0))
    draw = ImageDraw.Draw(img)
    
    # Define limbs (pairs of keypoint indices)
    limbs = [
        (1, 2), (1, 5), (2, 3), (3, 4), (5, 6), (6, 7), # Arms
        (1, 8), (8, 9), (9, 10), (1, 11), (11, 12), (12, 13), # Legs
        (1, 0), (0, 14), (14, 16), (0, 15), (15, 17) # Head
    ]
    
    # Draw limb bones
    for idx, (p1_idx, p2_idx) in enumerate(limbs):
        if p1_idx < len(keypoints) and p2_idx < len(keypoints):
            p1 = keypoints[p1_idx]
            p2 = keypoints[p2_idx]
            if p1 is not None and p2 is not None:
                color = LIMB_COLORS[idx % len(LIMB_COLORS)]
                draw.line([p1, p2], fill=color, width=4)
                
    # Draw keypoint joints
    for pt in keypoints:
        if pt is not None:
            r = 3
            draw.ellipse((pt[0] - r, pt[1] - r, pt[0] + r, pt[1] + r), fill=(255, 255, 255))
            
    return img

def create_attack01_skeletons():
    # 8 frames: Horizontal Slash
    os.makedirs(os.path.join(POSE_DIR, "Attack_01"), exist_ok=True)
    
    # Base Stance anchor: Neck (115, 80), Hip (115, 140), Feet (100, 230), (130, 230)
    for f in range(1, 9):
        # Progress 1..8
        if f == 1: # Wind-up back
            rwrist = (85, 110)
            relbow = (95, 95)
            lwrist = (125, 105)
            lelbow = (120, 95)
            neck = (110, 80)
            nose = (118, 65)
        elif f == 2: # Swing Start
            rwrist = (95, 115)
            relbow = (105, 100)
            lwrist = (130, 100)
            lelbow = (125, 90)
            neck = (112, 80)
            nose = (120, 65)
        elif f == 3: # Fast sweep
            rwrist = (125, 115)
            relbow = (120, 98)
            lwrist = (140, 95)
            lelbow = (130, 85)
            neck = (115, 80)
            nose = (123, 65)
        elif f == 4: # Impact Contact (Horizontal Slash across chest)
            rwrist = (175, 110)
            relbow = (145, 95)
            lwrist = (155, 90)
            lelbow = (135, 80)
            neck = (118, 80)
            nose = (126, 65)
        elif f == 5: # Follow-through extension
            rwrist = (195, 115)
            relbow = (155, 98)
            lwrist = (160, 95)
            lelbow = (138, 85)
            neck = (120, 80)
            nose = (128, 65)
        elif f == 6: # Deceleration
            rwrist = (185, 125)
            relbow = (150, 105)
            lwrist = (150, 105)
            lelbow = (135, 95)
            neck = (116, 80)
            nose = (124, 65)
        elif f == 7: # Recovery
            rwrist = (145, 135)
            relbow = (130, 110)
            lwrist = (135, 115)
            lelbow = (125, 100)
            neck = (114, 80)
            nose = (122, 65)
        else: # Return to Stance
            rwrist = (115, 140)
            relbow = (110, 110)
            lwrist = (125, 120)
            lelbow = (120, 100)
            neck = (112, 80)
            nose = (120, 65)

        kpts = [
            nose, neck, # 0, 1
            (100, 82), relbow, rwrist, # 2, 3, 4 (Right Arm)
            (124, 82), lelbow, lwrist, # 5, 6, 7 (Left Arm - Prosthetic)
            (106, 140), (102, 185), (98, 230), # 8, 9, 10 (Right Leg)
            (122, 140), (126, 185), (135, 230), # 11, 12, 13 (Left Leg)
            (nose[0]-2, nose[1]-3), (nose[0]+2, nose[1]-3), # 14, 15
            (nose[0]-6, nose[1]-1), (nose[0]+6, nose[1]-1)  # 16, 17
        ]
        
        skel = draw_openpose_skeleton(kpts)
        skel.save(os.path.join(POSE_DIR, "Attack_01", f"Pose_{f:02d}.png"))
        
    print("Generated 8 OpenPose skeletons for Attack_01!")

def create_attack02_skeletons():
    # 10 frames: Upward 45-degree Slash
    os.makedirs(os.path.join(POSE_DIR, "Attack_02"), exist_ok=True)
    
    for f in range(1, 11):
        if f == 1: # Low crouch wind-up
            rwrist = (85, 150)
            relbow = (95, 130)
            lwrist = (115, 120)
            lelbow = (115, 100)
            neck = (110, 90)
            nose = (118, 75)
        elif f == 2: # Push-off
            rwrist = (90, 155)
            relbow = (100, 135)
            lwrist = (120, 115)
            lelbow = (120, 95)
            neck = (112, 88)
            nose = (120, 73)
        elif f == 3: # Upward start
            rwrist = (105, 145)
            relbow = (110, 125)
            lwrist = (125, 105)
            lelbow = (122, 90)
            neck = (114, 85)
            nose = (122, 70)
        elif f == 4: # Upward acceleration
            rwrist = (125, 125)
            relbow = (120, 110)
            lwrist = (135, 95)
            lelbow = (128, 85)
            neck = (116, 82)
            nose = (124, 67)
        elif f == 5: # Pre-contact
            rwrist = (145, 100)
            relbow = (130, 95)
            lwrist = (145, 85)
            lelbow = (132, 78)
            neck = (118, 80)
            nose = (126, 65)
        elif f == 6: # Impact Contact (45 degree upward slash)
            rwrist = (165, 75)
            relbow = (140, 80)
            lwrist = (155, 75)
            lelbow = (135, 70)
            neck = (120, 78)
            nose = (128, 63)
        elif f == 7: # Peak Shoulder Stop
            rwrist = (175, 60)
            relbow = (145, 70)
            lwrist = (160, 65)
            lelbow = (138, 62)
            neck = (120, 78)
            nose = (128, 63)
        elif f == 8: # Hold & Freeze
            rwrist = (170, 65)
            relbow = (142, 72)
            lwrist = (155, 70)
            lelbow = (135, 65)
            neck = (118, 80)
            nose = (126, 65)
        elif f == 9: # Recovery
            rwrist = (140, 105)
            relbow = (125, 95)
            lwrist = (135, 95)
            lelbow = (125, 85)
            neck = (115, 82)
            nose = (123, 67)
        else: # Return to Stance
            rwrist = (115, 140)
            relbow = (110, 110)
            lwrist = (125, 120)
            lelbow = (120, 100)
            neck = (112, 80)
            nose = (120, 65)

        kpts = [
            nose, neck,
            (100, 82), relbow, rwrist,
            (124, 82), lelbow, lwrist,
            (106, 140), (102, 185), (98, 230),
            (122, 140), (126, 185), (135, 230),
            (nose[0]-2, nose[1]-3), (nose[0]+2, nose[1]-3),
            (nose[0]-6, nose[1]-1), (nose[0]+6, nose[1]-1)
        ]
        skel = draw_openpose_skeleton(kpts)
        skel.save(os.path.join(POSE_DIR, "Attack_02", f"Pose_{f:02d}.png"))
        
    print("Generated 10 OpenPose skeletons for Attack_02!")

if __name__ == "__main__":
    create_attack01_skeletons()
    create_attack02_skeletons()
