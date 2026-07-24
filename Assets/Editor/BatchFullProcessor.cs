#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

/// <summary>
/// 유니티 CLI 배치모드 실행 전용 헬퍼 클래스.
/// Animator State Transition 완벽 복구 ➔ Addressable 전수 자동 등록 ➔ 에셋 번들 빌드 & 로컬 서버 배포를 원스톱 일괄 수행.
/// </summary>
public static class BatchFullProcessor
{
    public static void ExecuteFullPipeline()
    {
        Debug.Log("<color=yellow><b>[BatchFullProcessor] 1. Animator State Transitions 복구 시작...</b></color>");
        AnimatorTransitionBuilder.RebuildAllAnimatorTransitions();

        Debug.Log("<color=yellow><b>[BatchFullProcessor] 2. Addressable 전수 자동 등록 시작...</b></color>");
        AddressableAutoRegister.RegisterAllAddressables();

        Debug.Log("<color=yellow><b>[BatchFullProcessor] 3. Addressables 번들 빌드 및 로컬 서버 배포 시작...</b></color>");
        AddressablesDeployer.BuildAndDeploy();

        Debug.Log("<color=green><b>[BatchFullProcessor] Full Pipeline 실행 성공 완료!</b></color>");
    }
}
#endif
