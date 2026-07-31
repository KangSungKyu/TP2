using Cysharp.Threading.Tasks;
using System;
using System.Threading;
using UnityEngine;

/// <summary>
/// 1-Way 발판 하향 점프 (Drop Through) 헬퍼 컴포넌트.
/// </summary>
public class OneWayPlatformPassThrough : MonoBehaviour
{
    private Collider2D platformCollider;

    private void Awake()
    {
        platformCollider = GetComponent<Collider2D>();
    }

    public async UniTask PassThroughAsync(Collider2D playerCollider, float ignoreDurationSec = 0.25f, CancellationToken cancellationToken = default)
    {
        if (platformCollider == null || playerCollider == null) return;

        try
        {
            Physics2D.IgnoreCollision(playerCollider, platformCollider, true);
            await UniTask.Delay(TimeSpan.FromSeconds(ignoreDurationSec), cancellationToken: cancellationToken);
        }
        catch (OperationCanceledException) { }
        finally
        {
            if (platformCollider != null && playerCollider != null)
            {
                Physics2D.IgnoreCollision(playerCollider, platformCollider, false);
            }
        }
    }
}

