using UnityEngine;

/// <summary>
/// Tilemap 및 지형 콜라이더 전용 벽점프 속성 컴포넌트.
/// Tilemap GameObject에 부착하여 개별 지형의 벽점프 규칙을 제어합니다.
/// </summary>
public class WallJumpSurface : MonoBehaviour
{
    [Header("Wall Jump Settings")]
    public bool CanWallJump = true;
    public bool AllowSameWall = true;
    public float SlideSpeedMultiplier = 1.0f;
}
