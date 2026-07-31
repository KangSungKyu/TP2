using NUnit.Framework;
using System.IO;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace QA.Tests
{
    public class AnimatorControllerTests
    {
        [Test]
        public void Test01_PlayerAnimatorController_ClipAndStateTransitions()
        {
            string path = "Assets/Anims/Player/PlayerAnimatorController.controller";
            Assert.IsTrue(File.Exists(path), $"PlayerAnimatorController.controller 파일 존재 확인: {path}");

            AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(path);
            Assert.IsNotNull(controller, "PlayerAnimatorController 로드 실패");

            var stateMachine = controller.layers[0].stateMachine;
            Assert.GreaterOrEqual(stateMachine.states.Length, 10, "PlayerAnimatorController 상태 10개 이상");

            foreach (var childState in stateMachine.states)
            {
                Assert.IsNotNull(childState.state.motion, $"Player State '{childState.state.name}'의 Motion 참조가 null입니다!");
            }
        }

        [Test]
        public void Test02_GaronAnimatorController_ClipAndStateTransitions()
        {
            string path = "Assets/Anims/Monster/GaronAnimatorController.controller";
            Assert.IsTrue(File.Exists(path), $"GaronAnimatorController.controller 파일 존재 확인: {path}");

            AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(path);
            Assert.IsNotNull(controller, "GaronAnimatorController 로드 실패");

            var stateMachine = controller.layers[0].stateMachine;
            Assert.GreaterOrEqual(stateMachine.states.Length, 8, "GaronAnimatorController 상태 8개 이상");

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
                if (!File.Exists(path)) continue;

                AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(path);
                if (controller == null) continue;

                var stateMachine = controller.layers[0].stateMachine;
                Assert.GreaterOrEqual(stateMachine.states.Length, 5, $"{path}의 상태 개수 5개 이상");
            }
        }
    }
}
