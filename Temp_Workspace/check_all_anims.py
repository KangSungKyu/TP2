import os, glob, re

anims_player = glob.glob(r"c:\Users\PC\Projects\TP2\Assets\Anims\Player\*.anim")
anims_monster = glob.glob(r"c:\Users\PC\Projects\TP2\Assets\Anims\Monster\*.anim")
controllers = glob.glob(r"c:\Users\PC\Projects\TP2\Assets\Anims\**\*.controller", recursive=True)

print("=== ANIMATION CLIPS & CONTROLLER INTEGRITY REPORT ===\n")
print(f"1. Player Animation Clips (.anim) Count: {len(anims_player)}")
for p in anims_player:
    print(f"   - {os.path.basename(p)}")

print(f"\n2. Monster Animation Clips (.anim) Count: {len(anims_monster)}")
for m in anims_monster:
    print(f"   - {os.path.basename(m)}")

print(f"\n3. AnimatorControllers Count: {len(controllers)}")
for c in controllers:
    rel_path = os.path.relpath(c, r"c:\Users\PC\Projects\TP2")
    with open(c, "r", encoding="utf-8") as f:
        content = f.read()
    
    # State names
    states = re.findall(r"m_Name: ([\w_]+)\n\s+m_Speed:", content)
    # Motion clips GUID
    motions = re.findall(r"m_Motion: \{fileID: \d+, guid: ([a-f0-9]+)", content)
    empty_motions = content.count("m_Motion: {fileID: 0}")
    # Transitions
    transitions = re.findall(r"m_ConditionMode: 6\n\s+m_ConditionEvent: State\n\s+m_EventTreshold: (\d+)", content)
    
    print(f"\nController: {rel_path}")
    print(f"   - States ({len(states)}): {states}")
    print(f"   - Motion Clips Bound (Valid GUIDs): {len(motions)}")
    print(f"   - Empty Motion References: {empty_motions}")
    print(f"   - Transitions ('State' == N): {len(transitions)} -> {sorted(list(set(map(int, transitions))))}")
