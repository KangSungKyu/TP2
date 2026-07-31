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
    public float FallGravityMultiplier = 1.7f;
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

    private Rigidbody2D body;
    private Collider2D physicsCollider;
    private ContactFilter2D solidFilter;
    private ContactFilter2D groundWithPlatformFilter;
    private readonly RaycastHit2D[] hitBuffer = new RaycastHit2D[16];

    private Vector2 groundNormal = Vector2.up;
    private float targetVelocityX;
    private bool isPassThroughActive;
    private bool isJumpHeld;

    private void Awake()
    {
        body = GetComponent<Rigidbody2D>();

        physicsCollider = null;
        foreach (var col in GetComponents<Collider2D>())
        {
            if (!col.isTrigger)
            {
                physicsCollider = col;
                break;
            }
        }
        if (physicsCollider == null)
        {
            physicsCollider = GetComponent<Collider2D>();
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
    }

    public void SetJumpHeld(bool held)
    {
        isJumpHeld = held;
    }

    public async UniTask PassThroughOneWayPlatformAsync(float durationSec = 0.25f, CancellationToken cancellationToken = default)
    {
        isPassThroughActive = true;
        IsGrounded = false;
        Velocity = new Vector2(Velocity.x, -2f);

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
    }

    // =========================================================================
    // FixedUpdate 물리 루프
    // =========================================================================

    private void FixedUpdate()
    {
        float dt = Time.deltaTime;

        ApplyGravity(dt);

        Velocity = new Vector2(targetVelocityX, Velocity.y);

        IsGrounded = false;
        IsWalledLeft = false;
        IsWalledRight = false;
        WallCollider = null;
        WallSurface = null;

        var deltaPosition = Velocity * dt;

        var moveAlongGround = new Vector2(groundNormal.y, -groundNormal.x);
        var horizontalMove = moveAlongGround * deltaPosition.x;
        PerformMovement(horizontalMove, false);

        var verticalMove = Vector2.up * deltaPosition.y;
        PerformMovement(verticalMove, true);
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

        // 벽 슬라이딩 낙하 속도 완화
        if (!IsGrounded && WallDir != 0 && vy < 0f)
        {
            float slideMult = WallSurface != null ? WallSurface.SlideSpeedMultiplier : 1.0f;
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

        var filter = (yMovement && move.y <= 0f && !isPassThroughActive)
            ? groundWithPlatformFilter
            : solidFilter;

        int count = physicsCollider.Cast(move.normalized, filter, hitBuffer, distance + SkinWidth);

        for (int i = 0; i < count; i++)
        {
            var hit = hitBuffer[i];
            var currentNormal = hit.normal;

            if (((1 << hit.collider.gameObject.layer) & OneWayPlatformLayer) != 0)
            {
                float feetY = physicsCollider.bounds.min.y;
                float platformTopY = hit.collider.bounds.max.y;
                if (feetY < platformTopY - 0.15f || isPassThroughActive)
                {
                    continue;
                }
            }

            if (currentNormal.y > MinGroundNormalY)
            {
                IsGrounded = true;
                if (yMovement)
                {
                    groundNormal = currentNormal;
                    currentNormal.x = 0;
                }
            }

            if (!yMovement && Mathf.Abs(currentNormal.x) > 0.5f)
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
