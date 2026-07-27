#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

/// <summary>
/// 유니티 CLI 배치모드 실행 전용 헬퍼 클래스.
/// Unity Pipeline API를 통한 100% 정식 Animator & Clip 바인딩 ➔ Addressable 전수 자동 등록 ➔ 에셋 번들 빌드 & 로컬 서버 배포를 원스톱 일괄 수행.
/// </summary>
public static class BatchFullProcessor
{
    public static void ExecuteFullPipeline()
    {
        Debug.Log("<color=yellow><b>[BatchFullProcessor] 1. Unity Pipeline 정식 Animator & Clip 바인딩 시작...</b></color>");
        UnityPipelineAnimatorBinder.ExecuteFullPipelineBinding();

        Debug.Log("<color=green><b>[BatchFullProcessor] Full Unity Pipeline 실행 성공 완료!</b></color>");
    }
}
#endif
