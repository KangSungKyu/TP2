import os, glob, re

# GUID map for monster anim clips
clip_guids = {}
for meta_path in glob.glob(r"c:\Users\PC\Projects\TP2\Assets\Anims\Monster\*.anim.meta"):
    clip_name = os.path.basename(meta_path).replace(".anim.meta", "")
    with open(meta_path, "r", encoding="utf-8") as f:
        meta_content = f.read()
    guid_match = re.search(r"guid: ([a-f0-9]+)", meta_content)
    if guid_match:
        clip_guids[clip_name] = guid_match.group(1)

def build_monster_controller_yaml(mname, state_map):
    filepath = f"c:/Users/PC/Projects/TP2/Assets/Anims/Monster/{mname}AnimatorController.controller"
    
    # Generate complete YAML with States and Transitions
    state_blocks = ""
    state_refs = ""
    trans_blocks = ""
    trans_refs = ""
    
    base_state_id = 1102000000000000000 + hash(mname) % 1000000
    base_trans_id = 1101000000000000000 + hash(mname) % 1000000
    sm_id = 1107000000000000000 + hash(mname) % 1000000

    for state_val, action in state_map.items():
        anim_name = f"{mname}_{action}"
        sid = base_state_id + state_val * 1000
        tid = base_trans_id + state_val * 1000
        guid = clip_guids.get(anim_name, "")

        state_refs += f"  - serializedVersion: 1\n    m_State: {{fileID: {sid}}}\n    m_Position: {{x: 300, y: {state_val * 60}, z: 0}}\n"
        trans_refs += f"  - {{fileID: {tid}}}\n"

        motion_field = f"{{fileID: 7400000, guid: {guid}, type: 2}}" if guid else "{fileID: 0}"

        state_blocks += f"""--- !u!1102 &{sid}
AnimatorState:
  serializedVersion: 6
  m_ObjectHideFlags: 1
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_Name: {anim_name}
  m_Speed: 1
  m_CycleOffset: 0
  m_Transitions: []
  m_StateMachineBehaviours: []
  m_Position: {{x: 50, y: 50, z: 0}}
  m_IKOnFeet: 0
  m_WriteDefaultValues: 1
  m_Mirror: 0
  m_SpeedParameterActive: 0
  m_MirrorParameterActive: 0
  m_CycleOffsetParameterActive: 0
  m_TimeParameterActive: 0
  m_Motion: {motion_field}
  m_Tag: 
  m_SpeedParameter: 
  m_MirrorParameter: 
  m_CycleOffsetParameter: 
  m_TimeParameter: 
"""

        trans_blocks += f"""--- !u!1101 &{tid}
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
  m_DstState: {{fileID: {sid}}}
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

    yaml_content = f"""%YAML 1.1
%TAG !u! tag:unity3d.com,2011:
--- !u!91 &9100000
AnimatorController:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_Name: {mname}AnimatorController
  serializedVersion: 5
  m_AnimatorParameters:
  - m_Name: State
    m_Type: 3
    m_DefaultFloat: 0
    m_DefaultInt: 0
    m_DefaultBool: 0
    m_Controller: {{fileID: 9100000}}
  m_AnimatorLayers:
  - serializedVersion: 5
    m_Name: Base Layer
    m_StateMachine: {{fileID: {sm_id}}}
    m_Mask: {{fileID: 0}}
    m_Motions: []
    m_Behaviours: []
    m_BlendingMode: 0
    m_SyncedLayerIndex: -1
    m_DefaultWeight: 0
    m_IKPass: 0
    m_SyncedLayerAffectsTiming: 0
    m_Controller: {{fileID: 9100000}}
{state_blocks}
{trans_blocks}
--- !u!1107 &{sm_id}
AnimatorStateMachine:
  serializedVersion: 6
  m_ObjectHideFlags: 1
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_Name: Base Layer
  m_ChildStates:
{state_refs}
  m_ChildStateMachines: []
  m_AnyStateTransitions:
{trans_refs}
  m_EntryTransitions: []
  m_StateMachineTransitions: {{}}
  m_StateMachineBehaviours: []
  m_AnyStatePosition: {{x: 50, y: 20, z: 0}}
  m_EntryPosition: {{x: 50, y: 120, z: 0}}
  m_ExitPosition: {{x: 800, y: 120, z: 0}}
  m_ParentStateMachinePosition: {{x: 800, y: 20, z: 0}}
  m_DefaultState: {{fileID: {base_state_id + 1000}}}
"""
    with open(filepath, "w", encoding="utf-8") as f:
        f.write(yaml_content)
    print(f"Generated complete YAML for monster: {mname}")

monster_map = {1: "Idle", 2: "Move", 3: "Jump", 7: "Attack", 8: "Death"}
for mname in ["SpearSentry", "ShadowStalker", "WaveHeavy"]:
    build_monster_controller_yaml(mname, monster_map)
