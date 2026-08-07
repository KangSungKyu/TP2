using Cysharp.Threading.Tasks;
using System;
using System.Threading;
using UnityEngine;

/// <summary>
/// 커스텀 Non-Physics 2D 운동학 모터 (Unity 공식 2D Platformer KinematicObject 참고 개선).
/// FixedUpdate 기반 2-pass 이동, Collider2D.Cast 자동 충돌 탐지, groundNormal 경사면 이동.
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Collider2D))]
public class KinematicMotor2D : MonoBehaviour
{
    [Header("Motor Settings")]
    public float Gravity = 30f;
    public float MaxFallSpeed = 25f;
    public float FallGravityMultiplier = 2.2f;
    public float ApexGravityMultiplier = 0.5f;
    public float SkinWidth = 0.01f;

    /// <summary>
    /// 착지 가능한 최소 지면 법선 Y값 (0.65 ≈ 약 50° 이하 경사만 착지)
    /// </summary>
    public float MinGroundNormalY = 0.65f;

    [Header("Collision Layers")]
    public LayerMask SolidGroundLayer;
    public LayerMask OneWayPlatformLayer;

    [Header("Wall Settings")]
    public float MaxWallSlideSpeed = 3.5f;

    // 모터 상태 (외부 읽기 전용)
    public Vector2 Velocity { get; private set; }
    public bool IsGrounded { get; private set; }
    public bool IsWalledLeft { get; private set; }
    public bool IsWalledRight { get; private set; }
    public int WallDir => IsWalledLeft ? -1 : (IsWalledRight ? 1 : 0);
    public Collider2D WallCollider { get; private set; }
    public WallJumpSurface WallSurface { get; private set; }
    public bool IsPassingThrough => isPassThroughActive;

    private Rigidbody2D body;
    private Collider2D physicsCollider;
    private ContactFilter2D solidFilter;
    private ContactFilter2D groundWithPlatformFilter;
    private readonly RaycastHit2D[] hitBuffer = new RaycastHit2D[16];

    private Vector2 groundNormal = Vector2.up;
    private bool hasGroundNormalOverride;
    private float targetVelocityX;
    private float knockbackVelocityX;
    private int knockbackStepsRemaining;
    private int knockbackGeneration;
    private bool isPassThroughActive;
    private bool isJumpHeld;

    public void InitMotor()
    {
        if (body == null) body = GetComponent<Rigidbody2D>();
        if (body == null) body = gameObject.AddComponent<Rigidbody2D>();

        if (physicsCollider == null)
        {
            foreach (var col in GetComponents<Collider2D>())
            {
                if (!col.isTrigger)
                {
                    physicsCollider = col;
                    break;
                }
            }
        }
        if (physicsCollider == null)
        {
            physicsCollider = gameObject.AddComponent<BoxCollider2D>();
        }

        body.bodyType = RigidbodyType2D.Kinematic;
        body.useFullKinematicContacts = true;
        body.simulated = true;

        if (SolidGroundLayer == 0)
        {
            SolidGroundLayer = LayerMask.GetMask("Default", "Ground");
        }
        if (OneWayPlatformLayer == 0)
        {
            OneWayPlatformLayer = LayerMask.GetMask("OneWayPlatform");
        }

        solidFilter = new ContactFilter2D();
        solidFilter.useTriggers = false;
        solidFilter.useLayerMask = true;
        solidFilter.SetLayerMask(SolidGroundLayer);

        groundWithPlatformFilter = new ContactFilter2D();
        groundWithPlatformFilter.useTriggers = false;
        groundWithPlatformFilter.useLayerMask = true;
        groundWithPlatformFilter.SetLayerMask(SolidGroundLayer | OneWayPlatformLayer);
    }

    private void Awake()
    {
        InitMotor();
    }

    // =========================================================================
    // 외부 API
    // =========================================================================

    public void SetTargetVelocityX(float vx)
    {
        targetVelocityX = vx;
    }

    public void SetVelocityY(float vy)
    {
        Velocity = new Vector2(Velocity.x, vy);
        if (vy > 0f)
        {
            IsGrounded = false;
            groundNormal = Vector2.up;
        }
    }

    public void ApplyKnockback(Vector2 velocity, float duration = 0f)
    {
        knockbackGeneration++;
        knockbackVelocityX = velocity.x;
        knockbackStepsRemaining = duration > 0f
            ? Mathf.Max(1, Mathf.CeilToInt(duration / Time.fixedDeltaTime))
            : 0;
        Velocity = new Vector2(velocity.x, IsGrounded ? 0f : Mathf.Max(0f, velocity.y));
        if (Velocity.y > 0f) IsGrounded = false;
    }

    public void SetJumpHeld(bool held)
    {
        isJumpHeld = held;
    }

    public async UniTask PassThroughOneWayPlatformAsync(float durationSec = 0.35f, CancellationToken cancellationToken = default)
    {
        if (isPassThroughActive) return;
        isPassThroughActive = true;
        IsGrounded = false;
        Velocity = new Vector2(Velocity.x, -6.5f);

        try
        {
            await UniTask.Delay(TimeSpan.FromSeconds(durationSec), cancellationToken: cancellationToken);
        }
        catch (OperationCanceledException) { }
        finally
        {
            isPassThroughActive = false;
        }
    }

    public void Teleport(Vector3 position)
    {
        body.position = position;
        Velocity = Vector2.zero;
        targetVelocityX = 0f;
        knockbackVelocityX = 0f;
        knockbackStepsRemaining = 0;
        knockbackGeneration++;
    }

    public void SetGroundNormal(Vector2 normal)
    {
        if (normal.sqrMagnitude > 0.001f)
        {
            groundNormal = normal.normalized;
            IsGrounded = true;
            hasGroundNormalOverride = true;
        }
    }

    public void SimulateStep(float dt)
    {
        if (body == null || physicsCollider == null)
        {
            InitMotor();
        }

        bool hadGroundNormalOverride = hasGroundNormalOverride;
        IsGrounded = hadGroundNormalOverride;
        hasGroundNormalOverride = false;
        IsWalledLeft = false;
        IsWalledRight = false;
        WallCollider = null;
        WallSurface = null;

        ApplyGravity(dt);

        int activeKnockbackGeneration = knockbackGeneration;
        Velocity = new Vector2(knockbackStepsRemaining > 0 ? knockbackVelocityX : targetVelocityX, Velocity.y);

        var deltaPosition = Velocity * dt;

        var moveAlongGround = new Vector2(groundNormal.y, -groundNormal.x);
        var horizontalMove = moveAlongGround * deltaPosition.x;
        PerformMovement(horizontalMove, false);

        if (IsGrounded && deltaPosition.y < 0f)
        {
            if (!hadGroundNormalOverride)
            {
                IsGrounded = ProbeGround();
                hasGroundNormalOverride = IsGrounded;
            }
        }
        else
        {
            PerformMovement(Vector2.up * deltaPosition.y, true);
        }

        if (!IsGrounded && Velocity.y <= 0f)
        {
            groundNormal = Vector2.up;
        }

        if (knockbackStepsRemaining > 0 && activeKnockbackGeneration == knockbackGeneration)
        {
            knockbackStepsRemaining--;
            if (knockbackStepsRemaining == 0) knockbackVelocityX = 0f;
        }
    }

    private bool ProbeGround()
    {
        int count = physicsCollider.Cast(Vector2.down, groundWithPlatformFilter, hitBuffer, SkinWidth * 2f);
        for (int i = 0; i < count; i++)
        {
            if (hitBuffer[i].normal.y > MinGroundNormalY)
            {
                groundNormal = hitBuffer[i].normal;
                Velocity = new Vector2(Velocity.x, 0f);
                return true;
            }
        }
        return false;
    }

    // =========================================================================
    // FixedUpdate 물리 루프
    // =========================================================================

    private void FixedUpdate()
    {
        SimulateStep(Time.fixedDeltaTime);
    }

    private void ApplyGravity(float dt)
    {
        if (IsGrounded && Velocity.y <= 0f)
        {
            Velocity = new Vector2(Velocity.x, -0.1f);
            return;
        }

        float gravityScale = 1f;
        if (Velocity.y < 0f)
        {
            gravityScale = FallGravityMultiplier;
        }
        else if (Mathf.Abs(Velocity.y) < 1.5f && isJumpHeld)
        {
            gravityScale = ApexGravityMultiplier;
        }

        float vy = Velocity.y - (Gravity * gravityScale * dt);

        // 벽 슬라이딩 낙하 속도 완화 (벽 방향 키 입력을 유지 중이고 벽점프 지원 면일 때만 발동)
        bool isPushingWall = WallDir != 0 && ((WallDir < 0 && targetVelocityX < -0.1f) || (WallDir > 0 && targetVelocityX > 0.1f));
        bool canSlide = WallSurface != null && WallSurface.CanWallJump;
        if (!IsGrounded && isPushingWall && canSlide && vy < 0f)
        {
            float slideMult = WallSurface.SlideSpeedMultiplier;
            float maxSlide = MaxWallSlideSpeed * slideMult;
            vy = Mathf.Max(vy, -maxSlide);
        }

        vy = Mathf.Max(vy, -MaxFallSpeed);
        Velocity = new Vector2(Velocity.x, vy);
    }

    private void PerformMovement(Vector2 move, bool yMovement)
    {
        float distance = move.magnitude;
        if (distance < 0.001f) return;

        var filter = (!isPassThroughActive)
            ? groundWithPlatformFilter
            : solidFilter;

        int count = physicsCollider.Cast(move.normalized, filter, hitBuffer, distance + SkinWidth);

        for (int i = 0; i < count; i++)
        {
            var hit = hitBuffer[i];
            var currentNormal = hit.normal;

            bool isOneWayPlatform = ((1 << hit.collider.gameObject.layer) & OneWayPlatformLayer) != 0 ||
                                   hit.collider.GetComponent<PlatformEffector2D>() != null ||
                                   hit.collider.GetComponent<OneWayPlatformPassThrough>() != null;

            if (isOneWayPlatform)
            {
                float feetY = physicsCollider.bounds.min.y;
                float platformTopY = hit.collider.bounds.max.y;

                // 1) 점프 도달 정점(Apex Y) 미리 계산 ➔ 최대 도달 높이가 발판 상단보다 낮으면 무조건 통과 (중간 걸침/붙음 100% 방지!)
                float vy = Velocity.y;
                float apexY = feetY;
                if (vy > 0f)
                {
                    apexY = feetY + (vy * vy) / (2f * Mathf.Max(1f, Gravity));
                }

                bool canReachTop = apexY >= platformTopY - 0.05f;
                if (!canReachTop)
                {
                    continue; // 도달 높이 미달 시 무조건 충돌 무시하고 하단 통과
                }

                // 2) 위로 상승 중일 때 (점프 상승) ➔ 발판 상향 관통
                if (yMovement && move.y > 0f)
                {
                    continue;
                }

                // 3) 하향 통과 키 입력 중 ➔ 발판 아래로 하향 통과
                if (isPassThroughActive)
                {
                    continue;
                }

                // 4) 발판 상단을 넘을 수 있고, 하강/낙하 중 (move.y <= 0f)일 때 발 위치가 발판 상단면 근처이면 착지!
                if (yMovement && move.y <= 0f)
                {
                    if (feetY < platformTopY - 0.40f)
                    {
                        continue;
                    }
                }

                // 5) 수평 이동 중 (!yMovement): 발 위치가 발판 상단보다 낮으면 측면 걸림 무시
                if (!yMovement && feetY < platformTopY - 0.15f)
                {
                    continue;
                }
            }

            if (yMovement && move.y <= 0f && currentNormal.y > MinGroundNormalY && !isPassThroughActive)
            {
                IsGrounded = true;
                groundNormal = currentNormal;
                currentNormal.x = 0;
            }

            if (!yMovement && Mathf.Abs(currentNormal.x) > 0.5f)
            {
                // 1-Way 발판 옆면은 벽점프/벽붙기 대상에서 완벽 제외
                if (!isOneWayPlatform)
                {
                    if (currentNormal.x > 0) IsWalledLeft = true;
                    else IsWalledRight = true;

                    WallCollider = hit.collider;
                    WallSurface = hit.collider.GetComponent<WallJumpSurface>();
                    if (WallSurface == null)
                    {
                        WallSurface = hit.collider.GetComponentInParent<WallJumpSurface>();
                    }
                }
            }

            if (IsGrounded)
            {
                float projection = Vector2.Dot(Velocity, currentNormal);
                if (projection < 0)
                {
                    Velocity -= projection * currentNormal;
                }
            }
            else
            {
                if (!yMovement)
                {
                    Velocity = new Vector2(0f, Velocity.y);
                }
                else
                {
                    Velocity = new Vector2(Velocity.x, Mathf.Min(Velocity.y, 0f));
                }
            }

            float modifiedDistance = hit.distance - SkinWidth;
            distance = modifiedDistance < distance ? modifiedDistance : distance;
        }

        distance = Mathf.Max(0f, distance);
        body.position += move.normalized * distance;
    }
}
