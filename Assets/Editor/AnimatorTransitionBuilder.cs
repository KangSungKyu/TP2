#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using System.IO;

/// <summary>
/// AnimatorController의 Int 파라미터 "State" (0=None, 1=Idle, 2=Run, 3=Jump, 4=Parry, 5=Guard, 6=Dodge, 7=Attack, 8=Execution)에 
/// 대응되는 State Transition(조건: State Equals N, ExitTime=false, Duration=0.1s)을 완벽하게 자동 연결 및 복구해 주는 유틸리티.
/// </summary>
public static class AnimatorTransitionBuilder
{
    [MenuItem("TP2/Rebuild Animator Transitions (State 파라미터 트랜지션 완벽 복구)")]
    public static void RebuildAllAnimatorTransitions()
    {
        Debug.Log("<color=yellow><b>[AnimatorTransitionBuilder] AnimatorController State 트랜지션 자동 복구 시작...</b></color>");

        // 1. Player AnimatorController 트랜지션 구축
        RebuildPlayerTransitions();

        // 2. Garon (보스) AnimatorController 트랜지션 구축
        RebuildGaronTransitions();

        // 3. 몬스터 3종 AnimatorController 트랜지션 구축
        RebuildMonsterTransitions();

        AssetDatabase.SaveAssets();
        Debug.Log("<color=green><b>[AnimatorTransitionBuilder] 모든 AnimatorController의 State 기반 Transition 복구 완료!</b></color>");
    }

    public static void RebuildPlayerTransitions()
    {
        string path = "Assets/Anims/Player/PlayerAnimatorController.controller";
        AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(path);
        if (controller == null)
        {
            controller = AnimatorController.CreateAnimatorControllerAtPath(path);
        }

        EnsureIntParameter(controller, "State");
        var stateMachine = controller.layers[0].stateMachine;

        // 기존 트랜지션 정제 후 맵핑
        var stateMap = new System.Collections.Generic.Dictionary<int, string>()
        {
            { 1, "Player_Idle" },
            { 2, "Player_Run" },
            { 3, "Player_Jump" },
            { 4, "Player_Parry" },
            { 5, "Player_Guard" },
            { 6, "Player_Dodge" },
            { 7, "Player_ComboAttack" },
            { 8, "Player_Execution" }
        };

        SetupStateMachineTransitions(controller, stateMachine, stateMap, "Assets/Anims/Player");
    }

    public static void RebuildGaronTransitions()
    {
        string path = "Assets/Anims/Monster/GaronAnimatorController.controller";
        AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(path);
        if (controller == null)
        {
            controller = AnimatorController.CreateAnimatorControllerAtPath(path);
        }

        EnsureIntParameter(controller, "State");
        var stateMachine = controller.layers[0].stateMachine;

        var stateMap = new System.Collections.Generic.Dictionary<int, string>()
        {
            { 1, "Garon_Idle" },
            { 2, "Garon_Move" },
            { 3, "Garon_Jump" },
            { 4, "Garon_Pattern_OverheadSmash" },
            { 5, "Garon_Pattern_ComboSlash" },
            { 6, "Garon_Pattern_Charge" },
            { 7, "Garon_Pattern_Shockwave" },
            { 8, "Garon_Death" }
        };

        SetupStateMachineTransitions(controller, stateMachine, stateMap, "Assets/Anims/Monster");
    }

    public static void RebuildMonsterTransitions()
    {
        string[] monsters = new string[] { "SpearSentry", "ShadowStalker", "WaveHeavy" };

        foreach (string mName in monsters)
        {
            string path = $"Assets/Anims/Monster/{mName}AnimatorController.controller";
            AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(path);
            if (controller == null)
            {
                controller = AnimatorController.CreateAnimatorControllerAtPath(path);
            }

            EnsureIntParameter(controller, "State");
            var stateMachine = controller.layers[0].stateMachine;

            var stateMap = new System.Collections.Generic.Dictionary<int, string>()
            {
                { 1, $"{mName}_Idle" },
                { 2, $"{mName}_Move" },
                { 3, $"{mName}_Jump" },
                { 7, $"{mName}_Attack" },
                { 8, $"{mName}_Death" }
            };

            SetupStateMachineTransitions(controller, stateMachine, stateMap, "Assets/Anims/Monster");
        }
    }

    private static void SetupStateMachineTransitions(AnimatorController controller, AnimatorStateMachine stateMachine, System.Collections.Generic.Dictionary<int, string> stateMap, string animFolder)
    {
        // 1. 기존 States 탐색 및 딕셔너리 구성
        System.Collections.Generic.Dictionary<string, AnimatorState> existingStates = new System.Collections.Generic.Dictionary<string, AnimatorState>();
        foreach (var childState in stateMachine.states)
        {
            existingStates[childState.state.name] = childState.state;
        }

        // 2. 필요 스테이트 생성 및 몽땅 바인딩
        foreach (var kvp in stateMap)
        {
            int stateVal = kvp.Key;
            string animName = kvp.Value;
            AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>($"{animFolder}/{animName}.anim");

            AnimatorState targetState;
            if (!existingStates.TryGetValue(animName, out targetState))
            {
                targetState = stateMachine.AddState(animName);
                existingStates[animName] = targetState;
            }

            if (clip != null)
            {
                targetState.motion = clip;
            }

            if (stateVal == 1)
            {
                stateMachine.defaultState = targetState;
            }

            // AnyState -> TargetState Transition 존재 여부 확인 후 추가
            bool hasTransition = false;
            foreach (var trans in stateMachine.anyStateTransitions)
            {
                if (trans.destinationState == targetState)
                {
                    hasTransition = true;
                    // 조건 갱신
                    trans.conditions = new AnimatorCondition[0];
                    trans.AddCondition(AnimatorConditionMode.Equals, stateVal, "State");
                    trans.hasExitTime = false;
                    trans.duration = 0.1f;
                    break;
                }
            }

            if (!hasTransition)
            {
                var newTrans = stateMachine.AddAnyStateTransition(targetState);
                newTrans.AddCondition(AnimatorConditionMode.Equals, stateVal, "State");
                newTrans.hasExitTime = false;
                newTrans.duration = 0.1f;
                newTrans.canTransitionToSelf = false;
            }
        }

        EditorUtility.SetDirty(controller);
    }

    private static void EnsureIntParameter(AnimatorController controller, string paramName)
    {
        foreach (var param in controller.parameters)
        {
            if (param.name == paramName && param.type == AnimatorControllerParameterType.Int)
            {
                return;
            }
        }
        controller.AddParameter(paramName, AnimatorControllerParameterType.Int);
    }
}
#endif
