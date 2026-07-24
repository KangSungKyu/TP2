import os, re

# Garon Map (8 All States)
garon_path = r"c:\Users\PC\Projects\TP2\Assets\Anims\Monster\GaronAnimatorController.controller"
garon_map = {
    1: "Garon_Idle",
    2: "Garon_Move",
    3: "Garon_Jump",
    4: "Garon_Pattern_OverheadSmash",
    5: "Garon_Pattern_ComboSlash",
    6: "Garon_Pattern_Charge",
    7: "Garon_Pattern_Shockwave",
    8: "Garon_Death"
}

with open(garon_path, 'r', encoding='utf-8') as f:
    content = f.read()

# Collect all state IDs
state_id_map = {}
for match in re.finditer(r'--- !u!1102 &(\d+)\nAnimatorState:\n\s+serializedVersion: \d+\n\s+m_ObjectHideFlags: \d+\n\s+m_CorrespondingSourceObject: \{fileID: \d+\}\n\s+m_PrefabInstance: \{fileID: \d+\}\n\s+m_PrefabAsset: \{fileID: \d+\}\n\s+m_Name: ([^\n]+)', content):
    file_id = match.group(1)
    state_name = match.group(2)
    state_id_map[state_name] = file_id

# Rebuild Transitions YAML
trans_blocks = ""
trans_ids = []
base_id = 1101000000000000000

for state_val, state_name in garon_map.items():
    if state_name not in state_id_map:
        continue
    target_file_id = state_id_map[state_name]
    trans_id = base_id + state_val * 10000
    trans_ids.append(trans_id)

    trans_blocks += f"""--- !u!1101 &{trans_id}
AnimatorStateTransition:
  m_ObjectHideFlags: 1
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_Name: 
  m_Conditions:
  - m_ConditionMode: 6
    m_ConditionEvent: State
    m_EventTreshold: {state_val}
  m_DstStateMachine: {{fileID: 0}}
  m_DstState: {{fileID: {target_file_id}}}
  m_Solo: 0
  m_Mute: 0
  m_IsExit: 0
  serializedVersion: 3
  m_TransitionDuration: 0.1
  m_TransitionOffset: 0
  m_ExitTime: 0.75
  m_HasExitTime: 0
  m_HasFixedDuration: 1
  m_InterruptionSource: 0
  m_OrderedInterruption: 1
  m_CanTransitionToSelf: 0
"""

# Strip old transitions
content = re.sub(r'--- !u!1101 &[\d]+[\s\S]*?(?=--- !u!|\Z)', '', content)

sm_match = re.search(r'--- !u!1107 &(\d+)\nAnimatorStateMachine:', content)
if sm_match:
    sm_id = sm_match.group(1)
    trans_refs = "\n".join([f"  - {{fileID: {tid}}}" for tid in trans_ids])
    content = re.sub(r'(--- !u!1107 &' + sm_id + r'[\s\S]*?m_AnyStateTransitions:)\s*\[.*?\]', r'\1\n' + trans_refs, content)

content += "\n" + trans_blocks

with open(garon_path, 'w', encoding='utf-8') as f:
    f.write(content)

print("Garon transitions updated completely!")
