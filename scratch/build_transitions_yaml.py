import os, re

def update_controller_yaml(filepath, state_map):
    with open(filepath, 'r', encoding='utf-8') as f:
        content = f.read()

    # 1. Ensure State parameter exists
    if "m_Name: State" not in content:
        param_block = """  - m_Name: State
    m_Type: 3
    m_DefaultFloat: 0
    m_DefaultInt: 0
    m_DefaultBool: 0
    m_Controller: {fileID: 9100000}"""
        content = content.replace("m_AnimatorParameters: []", "m_AnimatorParameters:\n" + param_block)
        if "m_AnimatorParameters:\n  - m_Name:" not in content and "m_AnimatorParameters:" in content:
            content = re.sub(r'm_AnimatorParameters:', 'm_AnimatorParameters:\n' + param_block, content, count=1)

    # 2. Extract State fileIDs/names
    state_id_map = {}
    for match in re.finditer(r'--- !u!1102 &(\d+)\nAnimatorState:\n\s+serializedVersion: \d+\n\s+m_ObjectHideFlags: \d+\n\s+m_CorrespondingSourceObject: \{fileID: \d+\}\n\s+m_PrefabInstance: \{fileID: \d+\}\n\s+m_PrefabAsset: \{fileID: \d+\}\n\s+m_Name: ([^\n]+)', content):
        file_id = match.group(1)
        state_name = match.group(2)
        state_id_map[state_name] = file_id

    # 3. Find StateMachine ID
    sm_match = re.search(r'--- !u!1107 &(\d+)\nAnimatorStateMachine:', content)
    if not sm_match:
        print(f"StateMachine not found in {filepath}")
        return
    
    sm_id = sm_match.group(1)

    # Generate transitions
    new_transitions_yaml = ""
    trans_ids = []
    base_id = 1101000000000000000

    for state_val, state_name in state_map.items():
        if state_name not in state_id_map:
            continue
        
        target_file_id = state_id_map[state_name]
        trans_id = base_id + state_val * 1000 + int(sm_id[-4:])
        trans_ids.append(trans_id)

        new_transitions_yaml += f"""--- !u!1101 &{trans_id}
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

    # Add transitions to content
    content += "\n" + new_transitions_yaml

    # Link transitions to AnyStateTransitions array in StateMachine
    trans_refs = "\n".join([f"  - {{fileID: {tid}}}" for tid in trans_ids])
    content = re.sub(r'(--- !u!1107 &' + sm_id + r'\nAnimatorStateMachine:[\s\S]*?m_AnyStateTransitions:)\s*\[\]', r'\1\n' + trans_refs, content)
    
    with open(filepath, 'w', encoding='utf-8') as f:
        f.write(content)
    print(f"Updated YAML transitions for: {filepath}")

# Player Map
player_path = r"c:\Users\PC\Projects\TP2\Assets\Anims\Player\PlayerAnimatorController.controller"
player_map = {
    1: "Player_Idle",
    2: "Player_Run",
    3: "Player_Jump",
    4: "Player_Parry",
    5: "Player_Guard",
    6: "Player_Dodge",
    7: "Player_ComboAttack",
    8: "Player_Execution"
}
update_controller_yaml(player_path, player_map)

# Garon Map
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
update_controller_yaml(garon_path, garon_map)
