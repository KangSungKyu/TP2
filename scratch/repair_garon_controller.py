import os, glob, re

garon_dir = r"c:\Users\PC\Projects\TP2\Assets\Anims\Monster"
controller_path = os.path.join(garon_dir, "GaronAnimatorController.controller")

# 1. Collect all Garon anim clip GUIDs
clip_guids = {}
for meta_path in glob.glob(os.path.join(garon_dir, "Garon_*.anim.meta")):
    clip_name = os.path.basename(meta_path).replace(".anim.meta", "")
    with open(meta_path, "r", encoding="utf-8") as f:
        meta_txt = f.read()
    m = re.search(r"guid: ([a-f0-9]+)", meta_txt)
    if m:
        clip_guids[clip_name] = m.group(1)

print("Found Garon Anim GUIDs:", clip_guids)

# 2. Build clean, error-free Unity YAML for GaronAnimatorController
garon_states_config = [
    (1, "Garon_Idle"),
    (2, "Garon_Move"),
    (3, "Garon_Jump"),
    (4, "Garon_Pattern_OverheadSmash"),
    (5, "Garon_Pattern_ComboSlash"),
    (6, "Garon_Pattern_Charge"),
    (7, "Garon_Pattern_Shockwave"),
    (8, "Garon_Death")
]

state_blocks = ""
trans_blocks = ""
state_refs = ""
trans_refs = ""

# Standard Unity fileIDs (Safe ranges)
base_state_id = 1102000000000000000
base_trans_id = 1101000000000000000
sm_id = 1107000000000000000

for val, name in garon_states_config:
    sid = base_state_id + val * 10000
    tid = base_trans_id + val * 10000
    guid = clip_guids.get(name, "")

    state_refs += f"  - serializedVersion: 1\n    m_State: {{fileID: {sid}}}\n    m_Position: {{x: 300, y: {val * 60}, z: 0}}\n"
    trans_refs += f"  - {{fileID: {tid}}}\n"

    motion_yaml = f"{{fileID: 7400000, guid: {guid}, type: 2}}" if guid else "{fileID: 0}"

    state_blocks += f"""--- !u!1102 &{sid}
AnimatorState:
  serializedVersion: 6
  m_ObjectHideFlags: 1
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_Name: {name}
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
  m_Motion: {motion_yaml}
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
    m_EventTreshold: {val}
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

clean_garon_yaml = f"""%YAML 1.1
%TAG !u! tag:unity3d.com,2011:
--- !u!91 &9100000
AnimatorController:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_Name: GaronAnimatorController
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
  m_DefaultState: {{fileID: {base_state_id + 10000}}}
"""

with open(controller_path, "w", encoding="utf-8") as f:
    f.write(clean_garon_yaml)

print("Successfully repaired GaronAnimatorController.controller YAML PPtr references!")
