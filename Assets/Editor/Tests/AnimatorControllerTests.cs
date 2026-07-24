using NUnit.Framework;
using System.IO;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace QA.Tests
{
    /// <summary>
    /// AnimatorController 클립 참조 및 State 전환 조건(Transition) 복구 상태 검증 NUnit 테스트 클래스
    /// </summary>
    public class AnimatorControllerTests
    {
        [Test]
        public void Test01_PlayerAnimatorController_ClipAndStateTransitions()
        {
            string path = "Assets/Anims/Player/PlayerAnimatorController.controller";
            Assert.IsTrue(File.Exists(path), $"PlayerAnimatorController.controller 파일이 존재하지 않습니다: {path}");

            AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(path);
            Assert.IsNotNull(controller, "PlayerAnimatorController를 LoadAssetAtPath으로 읽어오지 못했습니다.");

            Assert.Greater(controller.layers.Length, 0, "AnimatorController에 레이어가 존재하지 않습니다.");
            var stateMachine = controller.layers[0].stateMachine;
            Assert.GreaterOrEqual(stateMachine.states.Length, 8, "PlayerAnimatorController 상태 개수가 최소 8개 이상이어야 합니다.");

            // 8종 애니메이션 모션 클립 바인딩 및 Motion non-null 검증
            foreach (var childState in stateMachine.states)
            {
                Assert.IsNotNull(childState.state.motion, $"Player State '{childState.state.name}'의 AnimationClip Motion 참조가 null입니다!");
            }

            // State Int 파라미터 존재 확인
            bool hasStateParam = false;
            foreach (var param in controller.parameters)
            {
                if (param.name == "State" && param.type == AnimatorControllerParameterType.Int)
                {
                    hasStateParam = true;
                    break;
                }
            }
            Assert.IsTrue(hasStateParam, "PlayerAnimatorController에 'State' Int 파라미터가 존재해야 합니다.");
        }

        [Test]
        public void Test02_GaronAnimatorController_ClipAndStateTransitions()
        {
            string path = "Assets/Anims/Monster/GaronAnimatorController.controller";
            Assert.IsTrue(File.Exists(path), $"GaronAnimatorController.controller 파일이 존재하지 않습니다: {path}");

            AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(path);
            Assert.IsNotNull(controller, "GaronAnimatorController를 로드하지 못했습니다.");

            var stateMachine = controller.layers[0].stateMachine;
            Assert.GreaterOrEqual(stateMachine.states.Length, 8, "GaronAnimatorController 상태 개수가 8개 이상이어야 합니다.");

            foreach (var childState in stateMachine.states)
            {
                Assert.IsNotNull(childState.state.motion, $"Garon State '{childState.state.name}'의 Motion 참조가 null입니다!");
            }
        }

        [Test]
        public void Test03_NormalMonstersAnimatorControllers_StatesAndTransitions()
        {
            string[] controllers = new string[]
            {
                "Assets/Anims/Monster/SpearSentryAnimatorController.controller",
                "Assets/Anims/Monster/ShadowStalkerAnimatorController.controller",
                "Assets/Anims/Monster/WaveHeavyAnimatorController.controller"
            };

            foreach (string path in controllers)
            {
                Assert.IsTrue(File.Exists(path), $"몬스터 컨트롤러 미존재: {path}");

                AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(path);
                Assert.IsNotNull(controller, $"{path} 로드 실패");

                var stateMachine = controller.layers[0].stateMachine;
                Assert.GreaterOrEqual(stateMachine.states.Length, 5, $"{path}의 상태 개수가 최소 5개 이상이어야 합니다.");

                // State Int 파라미터 검증
                bool hasStateParam = false;
                foreach (var param in controller.parameters)
                {
                    if (param.name == "State" && param.type == AnimatorControllerParameterType.Int)
                    {
                        hasStateParam = true;
                        break;
                    }
                }
                Assert.IsTrue(hasStateParam, $"{path}에 'State' Int 파라미터가 구축되어 있어야 합니다.");
            }
        }
    }
}
