import os
import glob
import re

print("=== ANIMATOR CONTROLLER & ANIMATION CLIP INSPECTION REPORT ===\n")

controller_files = glob.glob(r"c:\Users\PC\Projects\TP2\Assets\Anims\**\*.controller", recursive=True)

for path in controller_files:
    rel_path = os.path.relpath(path, r"c:\Users\PC\Projects\TP2")
    print(f"Controller: {rel_path}")
    
    with open(path, "r", encoding="utf-8") as f:
        content = f.read()
    
    # 1. Parameter check
    has_state_param = "m_Name: State" in content
    print(f"  - Parameter 'State': {'FOUND (Int)' if has_state_param else 'MISSING'}")
    
    # 2. States check
    state_blocks = re.findall(r"m_Name: ([\w_]+)\n\s+m_Speed:", content)
    print(f"  - States Count: {len(state_blocks)}")
    for s in state_blocks:
        print(f"    * State: {s}")
        
    # 3. Motion clips bound check
    motion_guids = re.findall(r"m_Motion: \{fileID: \d+, guid: ([a-f0-9]+)", content)
    print(f"  - AnimationClip GUID References: {len(motion_guids)} bound")
    
    # 4. Transitions check
    transitions = re.findall(r"m_ConditionMode: 6\n\s+m_ConditionEvent: State\n\s+m_EventTreshold: (\d+)", content)
    print(f"  - Transitions ('State' == N): {len(transitions)} conditions (Values: {sorted(list(set(map(int, transitions))))})")
    print("-" * 60)
