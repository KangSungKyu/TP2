using Cysharp.Threading.Tasks;
using UnityEngine;

/// <summary>
/// 보스 몬스터 클래스 (Monster 상속).
/// </summary>
public class BossMonster : Monster
{
    private bool completionRequested;

    protected override void Awake()
    {
        base.Awake();
        InitUnitAsync(3201).Forget();
    }

    protected override void Start()
    {
        base.Start();
        Debug.Log($"<color=red><b>[BossMonster] 보스 '{UnitName}' (철위병 가론) 참전 완료!</b></color>");
    }

    public override void OnDeath()
    {
        if (completionRequested) return;
        completionRequested = true;
        base.OnDeath();
        CompleteStageAfterDeathAsync(this.GetCancellationTokenOnDestroy()).Forget();
    }

    private static async UniTaskVoid CompleteStageAfterDeathAsync(System.Threading.CancellationToken cancellationToken)
    {
        await UniTask.Delay(System.TimeSpan.FromSeconds(1.5f), cancellationToken: cancellationToken);
        if (StageManager.Instance != null)
        {
            await StageManager.Instance.CompleteStage1Async(cancellationToken);
        }
    }
}

