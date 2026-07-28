using Cysharp.Threading.Tasks;
using System;
using System.Threading;
using UnityEngine;

/// <summary>
/// 1-Way 발판 하향 점프 (Drop Through) 헬퍼 컴포넌트.
/// 플레이어가 1-Way 발판 위에서 아래 방향키(S / DownArrow) + 점프 키(C / Space) 입력 시
/// 플레이어와 발판 Collider 간 Physics2D.IgnoreCollision을 일시 활성화(0.25s)하여 아래로 부드럽게 통과시킵니다.
/// </summary>
public class OneWayPlatformPassThrough : MonoBehaviour
{
    private Collider2D platformCollider;

    private void Awake()
    {
        this.platformCollider = GetComponent<Collider2D>();
    }

    /// <summary>
    /// 하향 점프 비동기 실행: 플레이어 충돌체와 0.25초간 충돌 무시 후 복원
    /// </summary>
    public async UniTask PassThroughAsync(Collider2D playerCollider, float ignoreDurationSec = 0.25f, CancellationToken cancellationToken = default)
    {
        if (this.platformCollider == null || playerCollider == null) return;

        try
        {
            // 충돌 무시 설정 (하향 통과 허용)
            Physics2D.IgnoreCollision(playerCollider, this.platformCollider, true);

            // 지정된 시간(기본 0.25초) 대기
            await UniTask.Delay(TimeSpan.FromSeconds(ignoreDurationSec), cancellationToken: cancellationToken);
        }
        catch (OperationCanceledException)
        {
            // CancellationToken 취소 시 안전 처리
        }
        finally
        {
            // 충돌 무시 해제 (원복)
            if (this.platformCollider != null && playerCollider != null)
            {
                Physics2D.IgnoreCollision(playerCollider, this.platformCollider, false);
            }
        }
    }
}
