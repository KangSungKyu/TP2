using Cysharp.Threading.Tasks;
using System;
using System.Threading;
using UnityEngine;

/// <summary>
/// 100% 커스텀 Non-Physics 2D 운동학 모터 컴포넌트 (Swept BoxCast 사전 탐지 엔진).
/// 유니티 물리 solver를 타지 않고 이동 궤적 거리를 사전에 삭감하여
/// 지형 뚫림(Penetration), 튕김, 파묻힘 버그를 100% 원천 차단합니다.
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

    [Header("Collision Layers")]
    public LayerMask SolidGroundLayer;
    public LayerMask OneWayPlatformLayer;

    // 모터 내부 상태
    public Vector2 Velocity { get; private set; }
    public bool IsGrounded { get; private set; }
    public bool IsWalledLeft { get; private set; }
    public bool IsWalledRight { get; private set; }

    private Rigidbody2D rb;
    private Collider2D bodyCollider;

    private readonly RaycastHit2D[] hitBuffer = new RaycastHit2D[8];
    private bool isPassThroughActive = false;
    // 최근 착지한 표면 Y 좌표 (간헐적 파묻힘 보정용)
    private float lastPlatformTopY = float.NaN;
    // 이번 프레임에 새로 착지했는지 플래그
    private bool landedThisFrame = false;

    private void Awake()
    {
        this.rb = GetComponent<Rigidbody2D>();
        this.bodyCollider = GetComponent<Collider2D>();

        // Pure Kinematic 세팅
        this.rb.bodyType = RigidbodyType2D.Kinematic;
        this.rb.useFullKinematicContacts = true;
        this.rb.simulated = true;

        if (this.SolidGroundLayer == 0)
        {
            this.SolidGroundLayer = LayerMask.GetMask("Default", "Ground");
        }
        if (this.OneWayPlatformLayer == 0)
        {
            this.OneWayPlatformLayer = LayerMask.GetMask("OneWayPlatform");
        }
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
        catch (OperationCanceledException)
        {
        }
        finally
        {
            this.isPassThroughActive = false;
        }
    }

    /// <summary>
    /// 외부 조작 속도 설정
    /// </summary>
    public void SetVelocityX(float vx)
    {
        this.Velocity = new Vector2(vx, this.Velocity.y);
    }

    public void SetVelocityY(float vy)
    {
        this.Velocity = new Vector2(this.Velocity.x, vy);
    }

    /// <summary>
    /// 매 프레임 수평/수직 Non-Physics 이동 및 중력 처리 실행
    /// </summary>
    public void UpdateMotor(float deltaTime, bool isJumpingInput = false)
    {
        // 1. 가변 중력 연산
        this.updateGravity(deltaTime, isJumpingInput);

        // 2. 이번 프레임 이동하려는 궤적 벡터 계산
        Vector2 deltaPosition = this.Velocity * deltaTime;

        // 3. 수평 Swept BoxCast 탐지 및 벽 뚫기 방지
        deltaPosition.x = this.resolveHorizontalCollision(deltaPosition.x);

        // 4. 수직 Swept BoxCast 탐지 및 지면/천장 착지 밀착
        deltaPosition.y = this.resolveVerticalCollision(deltaPosition.y);

        // 5-a. 착지 보정: 새로 착지한 프레임에만 소량의 보정을 적용하여
        // 지속적인 오실레이션을 방지한다.
        if (this.landedThisFrame && !float.IsNaN(this.lastPlatformTopY) && deltaPosition.y <= 0f)
        {
            float feetYBefore = this.bodyCollider.bounds.min.y;
            float feetYAfter = feetYBefore + deltaPosition.y;

            // 플랫폼 상단과 이동 후 발바닥 간의 간격 (양수면 발이 플랫폼보다 아래에 있음)
            float penetration = this.lastPlatformTopY - feetYAfter;

            if (penetration > 0.0001f)
            {
                // 보정 허용치: SkinWidth 기반의 작은 값으로 제한
                float maxCorrection = Mathf.Max(this.SkinWidth * 2f, 0.02f);
                float correction = Mathf.Min(penetration, maxCorrection);

                // 발바닥을 플랫폼 상단으로 맞추기 위해 상향 보정(음수 delta를 덜 음수로 만듦)
                deltaPosition.y += correction;
            }

            // 보정은 한 번만 적용
            this.landedThisFrame = false;
        }

        // 5. 최종 100% 안전 이동 좌표 반영
        transform.position += (Vector3)deltaPosition;
    }

    private void updateGravity(float deltaTime, bool isJumpingInput)
    {
        if (this.IsGrounded)
        {
            if (this.Velocity.y <= 0f)
            {
                this.Velocity = new Vector2(this.Velocity.x, -0.1f); // 지면 밀착
            }
            return;
        }

        float currentGravityScale = 1.0f;

        if (this.Velocity.y < 0f)
        {
            currentGravityScale = this.FallGravityMultiplier; // 빠른 낙하
        }
        else if (Mathf.Abs(this.Velocity.y) < 1.5f && isJumpingInput)
        {
            currentGravityScale = this.ApexGravityMultiplier; // 점프 정점 부유감
        }

        float vy = this.Velocity.y - (this.Gravity * currentGravityScale * deltaTime);
        vy = Mathf.Max(vy, -this.MaxFallSpeed);

        this.Velocity = new Vector2(this.Velocity.x, vy);
    }

    private float resolveHorizontalCollision(float deltaX)
    {
        this.IsWalledLeft = false;
        this.IsWalledRight = false;

        if (Mathf.Abs(deltaX) < 0.0001f) return 0f;

        float directionX = Mathf.Sign(deltaX);
        float distance = Mathf.Abs(deltaX) + this.SkinWidth;

        Vector2 boxCenter = (Vector2)this.bodyCollider.bounds.center;
        Vector2 boxSize = new Vector2(this.bodyCollider.bounds.size.x - (this.SkinWidth * 2f), this.bodyCollider.bounds.size.y - (this.SkinWidth * 2f));

        int count = Physics2D.BoxCastNonAlloc(boxCenter, boxSize, 0f, new Vector2(directionX, 0f), this.hitBuffer, distance, this.SolidGroundLayer);

        for (int i = 0; i < count; i++)
        {
            var hit = this.hitBuffer[i];
            if (hit.collider != null && hit.collider != this.bodyCollider && !hit.collider.isTrigger)
            {
                if (Mathf.Abs(hit.normal.x) > 0.5f)
                {
                    float allowedDistance = Mathf.Max(0f, hit.distance - this.SkinWidth);
                    deltaX = directionX * allowedDistance;
                    this.Velocity = new Vector2(0f, this.Velocity.y);

                    if (directionX < 0) this.IsWalledLeft = true;
                    else this.IsWalledRight = true;

                    break;
                }
            }
        }

        return deltaX;
    }

    private float resolveVerticalCollision(float deltaY)
    {
        // 이전 상태를 보존하여 새로 착지한 프레임을 감지
        bool prevGrounded = this.IsGrounded;
        this.IsGrounded = false;
        this.landedThisFrame = false;

        float directionY = Mathf.Sign(deltaY);
        float distance = Mathf.Abs(deltaY) + this.SkinWidth;

        Vector2 boxCenter = (Vector2)this.bodyCollider.bounds.center;
        Vector2 boxSize = new Vector2(this.bodyCollider.bounds.size.x - (this.SkinWidth * 2f), this.bodyCollider.bounds.size.y - (this.SkinWidth * 2f));

        // 지면/천장 검사
        LayerMask targetLayer = this.SolidGroundLayer;
        if (directionY <= 0f && !this.isPassThroughActive)
        {
            targetLayer |= this.OneWayPlatformLayer; // 하강 중일 때만 1-Way 발판 레이어 포함
        }

        int count = Physics2D.BoxCastNonAlloc(boxCenter, boxSize, 0f, new Vector2(0f, directionY), this.hitBuffer, distance, targetLayer);

        for (int i = 0; i < count; i++)
        {
            var hit = this.hitBuffer[i];
            if (hit.collider != null && hit.collider != this.bodyCollider && !hit.collider.isTrigger)
            {
                // 1-Way 발판 높이 비교 검사 (이전 발바닥 위치가 발판 상단 표면 이상이었을 때만 착지)
                if (((1 << hit.collider.gameObject.layer) & this.OneWayPlatformLayer) != 0)
                {
                    float feetY = this.bodyCollider.bounds.min.y;
                    float platformTopY = hit.collider.bounds.max.y;

                    // 아래에서 위로 뚫거나 S+C 하향 점프 중이면 착지 생략
                    if (feetY < platformTopY - 0.15f || this.isPassThroughActive)
                    {
                        continue;
                    }
                }

                if (Mathf.Abs(hit.normal.y) > 0.5f)
                {
                    float allowedDistance = Mathf.Max(0f, hit.distance - this.SkinWidth);
                    deltaY = directionY * allowedDistance;
                    this.Velocity = new Vector2(this.Velocity.x, 0f);

                    if (directionY <= 0f)
                    {
                        this.IsGrounded = true;
                        // 착지 시 플랫폼 상단 Y를 기록, 이전 프레임에 비지상 이었다면 landedThisFrame=true
                        this.lastPlatformTopY = hit.collider.bounds.max.y;
                        if (!prevGrounded)
                        {
                            this.landedThisFrame = true;
                        }
                    }

                    break;
                }
            }
        }

        // 지상에 있지 않다면 기록된 플랫폼 정보를 초기화
        if (!this.IsGrounded)
        {
            this.lastPlatformTopY = float.NaN;
        }

        return deltaY;
    }
}
