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

    // 모터 상태 (외부 읽기 전용)
    public Vector2 Velocity { get; private set; }
    public bool IsGrounded { get; private set; }
    public bool IsWalledLeft { get; private set; }
    public bool IsWalledRight { get; private set; }

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
        this.body = GetComponent<Rigidbody2D>();

        // 물리 충돌용 non-trigger Collider2D 탐색 (trigger Hitbox 제외)
        this.physicsCollider = null;
        foreach (var col in GetComponents<Collider2D>())
        {
            if (!col.isTrigger)
            {
                this.physicsCollider = col;
                break;
            }
        }
        if (this.physicsCollider == null)
        {
            this.physicsCollider = GetComponent<Collider2D>();
        }

        this.body.bodyType = RigidbodyType2D.Kinematic;
        this.body.useFullKinematicContacts = true;
        this.body.simulated = true;

        if (this.SolidGroundLayer == 0)
        {
            this.SolidGroundLayer = LayerMask.GetMask("Default", "Ground");
        }
        if (this.OneWayPlatformLayer == 0)
        {
            this.OneWayPlatformLayer = LayerMask.GetMask("OneWayPlatform");
        }

        this.solidFilter = new ContactFilter2D();
        this.solidFilter.useTriggers = false;
        this.solidFilter.useLayerMask = true;
        this.solidFilter.SetLayerMask(this.SolidGroundLayer);

        this.groundWithPlatformFilter = new ContactFilter2D();
        this.groundWithPlatformFilter.useTriggers = false;
        this.groundWithPlatformFilter.useLayerMask = true;
        this.groundWithPlatformFilter.SetLayerMask(this.SolidGroundLayer | this.OneWayPlatformLayer);
    }

    // =========================================================================
    // 외부 API (Update에서 호출)
    // =========================================================================

    /// <summary>
    /// 수평 입력 속도 설정 (Update에서 매 프레임 호출, FixedUpdate에서 velocity.x에 적용)
    /// </summary>
    public void SetTargetVelocityX(float vx)
    {
        this.targetVelocityX = vx;
    }

    /// <summary>
    /// 수직 속도 즉시 설정 (점프 임펄스 등)
    /// </summary>
    public void SetVelocityY(float vy)
    {
        this.Velocity = new Vector2(this.Velocity.x, vy);
    }

    /// <summary>
    /// 점프 키 홀드 상태 전달 (가변 중력 - 점프 정점 부유감 용)
    /// </summary>
    public void SetJumpHeld(bool held)
    {
        this.isJumpHeld = held;
    }

    /// <summary>
    /// 1-Way 발판 하향 점프(Drop Through) 비동기 트리거
    /// </summary>
    public async UniTask PassThroughOneWayPlatformAsync(float durationSec = 0.25f, CancellationToken cancellationToken = default)
    {
        this.isPassThroughActive = true;
        this.IsGrounded = false;
        this.Velocity = new Vector2(this.Velocity.x, -2f);

        try
        {
            await UniTask.Delay(TimeSpan.FromSeconds(durationSec), cancellationToken: cancellationToken);
        }
        catch (OperationCanceledException) { }
        finally
        {
            this.isPassThroughActive = false;
        }
    }

    /// <summary>
    /// 텔레포트 (body.position + velocity 동시 초기화)
    /// </summary>
    public void Teleport(Vector3 position)
    {
        this.body.position = position;
        this.Velocity = Vector2.zero;
        this.targetVelocityX = 0f;
    }

    // =========================================================================
    // FixedUpdate 물리 루프 (tp_2dpm KinematicObject 패턴)
    // =========================================================================

    private void FixedUpdate()
    {
        float dt = Time.deltaTime; // FixedUpdate 내에서 Time.deltaTime == Time.fixedDeltaTime

        // 1. 가변 중력
        this.applyGravity(dt);

        // 2. 수평 속도 = Update에서 설정된 입력 기반 targetVelocityX
        this.Velocity = new Vector2(this.targetVelocityX, this.Velocity.y);

        // 3. 상태 초기화
        this.IsGrounded = false;
        this.IsWalledLeft = false;
        this.IsWalledRight = false;

        var deltaPosition = this.Velocity * dt;

        // 4. 수평 이동 (groundNormal 접선 벡터 기반 경사면 이동) → body.position 즉시 반영
        var moveAlongGround = new Vector2(this.groundNormal.y, -this.groundNormal.x);
        var horizontalMove = moveAlongGround * deltaPosition.x;
        this.performMovement(horizontalMove, false);

        // 5. 수직 이동 → 갱신된 body.position에서 탐지 → body.position 즉시 반영
        var verticalMove = Vector2.up * deltaPosition.y;
        this.performMovement(verticalMove, true);
    }

    private void applyGravity(float dt)
    {
        // 지면 밀착 상태에서는 미세 하향 속도만 유지 (경사면 밀착 보장)
        if (this.IsGrounded && this.Velocity.y <= 0f)
        {
            this.Velocity = new Vector2(this.Velocity.x, -0.1f);
            return;
        }

        float gravityScale = 1f;
        if (this.Velocity.y < 0f)
        {
            gravityScale = this.FallGravityMultiplier; // 빠른 낙하
        }
        else if (Mathf.Abs(this.Velocity.y) < 1.5f && this.isJumpHeld)
        {
            gravityScale = this.ApexGravityMultiplier; // 점프 정점 부유감
        }

        float vy = this.Velocity.y - (this.Gravity * gravityScale * dt);
        vy = Mathf.Max(vy, -this.MaxFallSpeed);
        this.Velocity = new Vector2(this.Velocity.x, vy);
    }

    // =========================================================================
    // 2-pass 이동 실행 (tp_2dpm PerformMovement 참고)
    // =========================================================================

    private void performMovement(Vector2 move, bool yMovement)
    {
        float distance = move.magnitude;
        if (distance < 0.001f) return;

        // 하강 수직 이동 + PassThrough 비활성 → 1-Way 발판 레이어 포함
        var filter = (yMovement && move.y <= 0f && !this.isPassThroughActive)
            ? this.groundWithPlatformFilter
            : this.solidFilter;

        // Collider2D.Cast: 자기 자신 자동 제외, 콜라이더 형상 자동 사용
        int count = this.physicsCollider.Cast(move.normalized, filter, this.hitBuffer, distance + this.SkinWidth);

        for (int i = 0; i < count; i++)
        {
            var hit = this.hitBuffer[i];
            var currentNormal = hit.normal;

            // 1-Way 발판 높이 비교 (발바닥이 발판 상단보다 아래면 통과 허용)
            if (((1 << hit.collider.gameObject.layer) & this.OneWayPlatformLayer) != 0)
            {
                float feetY = this.physicsCollider.bounds.min.y;
                float platformTopY = hit.collider.bounds.max.y;
                if (feetY < platformTopY - 0.15f || this.isPassThroughActive)
                {
                    continue;
                }
            }

            // 착지 판정: normal.y > MinGroundNormalY인 표면만 착지 가능
            if (currentNormal.y > this.MinGroundNormalY)
            {
                this.IsGrounded = true;
                if (yMovement)
                {
                    this.groundNormal = currentNormal;
                    currentNormal.x = 0; // 수직 이동 시 법선 X 성분 제거 (수직 밀착)
                }
            }

            // 벽 감지 (수평 이동 pass에서만)
            if (!yMovement && Mathf.Abs(currentNormal.x) > 0.5f)
            {
                if (currentNormal.x > 0) this.IsWalledLeft = true;
                else this.IsWalledRight = true;
            }

            // 속도 투영 (경사면에서 자연스러운 속도 조정)
            if (this.IsGrounded)
            {
                float projection = Vector2.Dot(this.Velocity, currentNormal);
                if (projection < 0)
                {
                    // 경사면 법선에 대한 속도 투영 → 경사면을 따라 감속
                    this.Velocity -= projection * currentNormal;
                }
            }
            else
            {
                // 공중에서 충돌: 해당 축 속도 제거
                if (!yMovement)
                {
                    this.Velocity = new Vector2(0f, this.Velocity.y);
                }
                else
                {
                    this.Velocity = new Vector2(this.Velocity.x, Mathf.Min(this.Velocity.y, 0f));
                }
            }

            // SkinWidth 차감하여 표면에 밀착 (뚫림 방지)
            float modifiedDistance = hit.distance - this.SkinWidth;
            distance = modifiedDistance < distance ? modifiedDistance : distance;
        }

        distance = Mathf.Max(0f, distance);
        this.body.position += move.normalized * distance;
    }
}
