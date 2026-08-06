using Cysharp.Threading.Tasks;
using System.Threading;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 플레이어 유닛 클래스 (UnitBase 상속).
/// Unity InputSystem 조작, 4대 공수 선택지, 3타 콤보 공격, 스킬 연동 및 PlayerState 동기화를 전담합니다.
/// </summary>
public class Player : UnitBase
{
    // =========================================================================
    // 1. PUBLIC FIELDS & PROPERTIES (PascalCase)
    // =========================================================================

    [SerializeField]
    public float Speed = 5f;

    [SerializeField]
    public float DodgeDashSpeed = 12f;

    public bool IsJumping { get; private set; }

    public PlayerState CurrentState { get; private set; } = PlayerState.Idle;
    public Collider2D MovementCollider => hitCollider;
    public KinematicMotor2D Motor => motor;


    // =========================================================================
    // 2. PRIVATE FIELDS (camelCase)
    // =========================================================================

    private Vector3 currentMoveDir = Vector3.zero;
    private Vector3 facingDir = Vector3.right;

    // 콤보 공격 관련 변수
    private int comboStep = 0; // 0: 공격안함, 1: 1타, 2: 2타, 3: 3타
    private bool isAttacking = false;
    private bool hasQueuedAttack = false;
    private float comboWindow = 0.5f;

    // 점프 및 벽점프 관련 변수
    [SerializeField]
    private float jumpForce = 11.5f;
    private float coyoteTime = 0.12f;
    private float coyoteTimeCounter = 0f;
    private float jumpBufferTime = 0.12f;
    private float jumpBufferCounter = 0f;

    [Header("Wall Jump Settings")]
    public Vector2 WallJumpForce = new Vector2(9.5f, 12.5f);
    public float WallJumpLockoutDuration = 0.18f;

    private float wallJumpLockoutTimer = 0f;
    private int lastWallDir = 0;


    // =========================================================================
    // 3. PUBLIC METHODS (PascalCase)
    // =========================================================================

    public override async UniTask InitUnitAsync(uint unitIdx)
    {
        await base.InitUnitAsync(unitIdx);

        if (UnitData != null && UnitData.MoveSpeed > 0f)
        {
            Speed = UnitData.MoveSpeed;
        }
    }

    public void SetState(PlayerState newState, bool forceUpdate = false)
    {
        if (CurrentState == newState && !forceUpdate) return;

        CurrentState = newState;

        if (animator != null && animator.runtimeAnimatorController != null)
        {
            animator.SetInteger("State", (int)newState);
            if (newState != PlayerState.Attack)
            {
                string animName = GetAnimNameByState(newState);
                animator.Play(animName);
            }
        }
    }


    // =========================================================================
    // 4. PROTECTED & PRIVATE METHODS
    // =========================================================================

    public static Player Instance { get; private set; }

    protected override void Awake()
    {
        if (Instance != null && Instance != this)
        {
            if (Application.isPlaying) Destroy(gameObject);
            else DestroyImmediate(gameObject);
            return;
        }

        base.Awake();
        Instance = this;
        motor = GetComponent<KinematicMotor2D>();
        if (motor == null)
        {
            motor = gameObject.AddComponent<KinematicMotor2D>();
        }
        InitUnitAsync(3001).Forget();
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    private void Start()
    {
        var stats = GetComponent<CombatStats>();
        if (stats != null)
        {
            stats.OnDeath.AddListener(Die);
        }
    }

    public void Die()
    {
        SetState(PlayerState.Hit, true);
        if (motor != null) motor.SetTargetVelocityX(0f);
        var cols = GetComponentsInChildren<Collider2D>();
        foreach (var c in cols) c.enabled = false;

        Debug.Log("<color=red><b>[Player] 플레이어 사망! 2.0초 후 1스테이지 리로드...</b></color>");
        ReloadStageAsync().Forget();
    }

    private async UniTaskVoid ReloadStageAsync()
    {
        await UniTask.Delay(System.TimeSpan.FromSeconds(2.0f), cancellationToken: this.GetCancellationTokenOnDestroy());
        if (StageManager.Instance != null)
        {
            await StageManager.Instance.LoadNextRoomAsync(0, this.GetCancellationTokenOnDestroy());
        }
    }

    private void Update()
    {
        var keyboard = Keyboard.current;
        if (keyboard == null) return;

        if (wallJumpLockoutTimer > 0f)
        {
            wallJumpLockoutTimer -= Time.deltaTime;
        }

        bool isJumpingInput = keyboard.cKey.isPressed;
        HandleMovement(keyboard);
        HandleJump(keyboard);

        if (motor != null)
        {
            motor.SetJumpHeld(isJumpingInput);
        }

        HandleDefensiveActions(keyboard);
        HandleBasicAttack(keyboard);
        HandleExecutionAction(keyboard);
        HandleSkills(keyboard);
        UpdateIdleState();
    }

    private void HandleJump(Keyboard keyboard)
    {
        if (stats.IsDodging || stats.IsGuarding || stats.IsParrying)
            return;

        bool isGroundedNow = motor != null ? motor.IsGrounded : isGrounded;

        // 1. 하향 점프 (S/Down + C/Space)
        bool isDownPressed = keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed;
        bool isJumpPressedThisFrame = keyboard.cKey.wasPressedThisFrame || keyboard.spaceKey.wasPressedThisFrame;
        if (isDownPressed && isJumpPressedThisFrame && isGroundedNow)
        {
            if (motor != null)
            {
                motor.PassThroughOneWayPlatformAsync(0.35f, this.GetCancellationTokenOnDestroy()).Forget();
            }
            return;
        }

        // 2. 점프 버퍼 타이머
        if (keyboard.cKey.wasPressedThisFrame)
        {
            jumpBufferCounter = jumpBufferTime;
        }
        else
        {
            jumpBufferCounter -= Time.deltaTime;
        }

        if (isGroundedNow)
        {
            coyoteTimeCounter = coyoteTime;
            IsJumping = false;
            lastWallDir = 0; // 지상 착지 시 벽점프 기록 초기화
        }
        else
        {
            coyoteTimeCounter -= Time.deltaTime;
        }

        // 3. 공중 벽점프 시도
        if (!isGroundedNow && motor != null && motor.WallDir != 0 && jumpBufferCounter > 0f)
        {
            if (TryPerformWallJump())
            {
                jumpBufferCounter = 0f;
                coyoteTimeCounter = 0f;
                return;
            }
        }

        // 4. 가변 점프 (버튼 감쇄)
        if (keyboard.cKey.wasReleasedThisFrame && motor != null && motor.Velocity.y > 0f)
        {
            motor.SetVelocityY(motor.Velocity.y * 0.4f);
        }

        // 5. 일반 지상 점프 실행
        if (jumpBufferCounter > 0f && coyoteTimeCounter > 0f)
        {
            if (motor != null)
            {
                motor.SetVelocityY(jumpForce);
            }
            IsJumping = true;
            if (stats != null)
            {
                stats.SetJumped(true);
            }

            SetState(PlayerState.Jump);
            jumpBufferCounter = 0f;
            coyoteTimeCounter = 0f;
        }
    }

    private bool TryPerformWallJump()
    {
        if (motor == null || motor.IsGrounded || motor.WallDir == 0) return false;

        var surface = motor.WallSurface;
        if (surface != null && !surface.CanWallJump) return false;
        if (surface != null && !surface.AllowSameWall && lastWallDir == motor.WallDir) return false;

        int wallDir = motor.WallDir;
        motor.SetTargetVelocityX(-wallDir * WallJumpForce.x);
        motor.SetVelocityY(WallJumpForce.y);

        wallJumpLockoutTimer = WallJumpLockoutDuration;
        lastWallDir = wallDir;
        IsJumping = true;

        if (stats != null)
        {
            stats.SetJumped(true);
        }

        SetFacingRight(-wallDir > 0);
        SetState(PlayerState.Jump, forceUpdate: true);

        Debug.Log($"<color=cyan>[WallJump] 벽점프 성공! Dir: {-wallDir}, Force: {WallJumpForce}</color>");
        return true;
    }

    private void HandleExecutionAction(Keyboard keyboard)
    {
        if (keyboard.leftCtrlKey.wasPressedThisFrame)
        {
            foreach (var monster in Monster.ActiveMonsters)
            {
                if (monster != null && Vector3.Distance(transform.position, monster.transform.position) <= 2.5f)
                {
                    if (TryExecuteTarget(monster, executionMultiplier: 5.0f))
                    {
                        SetState(PlayerState.Execution, forceUpdate: true);
                        break;
                    }
                }
            }
        }
    }

    private string GetAnimNameByState(PlayerState state)
    {
        return state switch
        {
            PlayerState.None => "Player_Idle",
            PlayerState.Idle => "Player_Idle",
            PlayerState.Run => "Player_Run",
            PlayerState.Jump => "Player_Jump",
            PlayerState.Parry => "Player_Parry",
            PlayerState.Guard => "Player_Guard",
            PlayerState.Dodge => "Player_Dodge",
            PlayerState.Execution => "Player_Execution",
            PlayerState.Attack => "Player_Attack_Hit1",
            PlayerState.Attack2 => "Player_Attack_Hit2",
            PlayerState.Attack3 => "Player_Attack_Hit3",
            _ => "Player_Idle"
        };
    }

    private void HandleMovement(Keyboard keyboard)
    {
        if (stats.IsDodging || stats.IsGuarding || stats.IsParrying)
            return;

        if (isAttacking && !IsJumping)
            return;

        // 벽점프 반동 사각 이동 동안 수평 입력 잠금
        if (wallJumpLockoutTimer > 0f)
            return;

        float moveX = 0f;
        if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed) moveX -= 1f;
        if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed) moveX += 1f;

        if (Mathf.Abs(moveX) > 0.01f)
        {
            Vector2 dir = new Vector3(moveX, 0f, 0f);
            currentMoveDir = dir;
            facingDir = dir;
            SetFacingRight(moveX >= 0);

            if (motor != null)
            {
                motor.SetTargetVelocityX(moveX * Speed);
            }

            if (!IsJumping && !isAttacking)
            {
                SetState(PlayerState.Run);
            }
        }
        else
        {
            currentMoveDir = Vector2.zero;

            if (motor != null)
            {
                motor.SetTargetVelocityX(0f);
            }
        }
    }

    private void UpdateIdleState()
    {
        if (currentMoveDir.sqrMagnitude <= 0.001f && 
            !IsJumping && 
            !stats.IsDodging && 
            !stats.IsGuarding && 
            !stats.IsParrying && 
            !isAttacking)
        {
            SetState(PlayerState.Idle);
        }
    }

    private void HandleDefensiveActions(Keyboard keyboard)
    {
        if (isAttacking || stats.IsDodging || stats.IsGuarding || stats.IsParrying ||
            (motor != null && motor.IsPassingThrough)) return;

        if (keyboard.spaceKey.wasPressedThisFrame && !IsJumping)
        {
            GuardParrySequenceAsync(keyboard, this.GetCancellationTokenOnDestroy()).Forget();
        }

        if (keyboard.leftShiftKey.wasPressedThisFrame)
        {
            Vector3 dodgeDir = currentMoveDir.sqrMagnitude > 0.001f
                ? currentMoveDir.normalized
                : (facingDir.x >= 0 ? Vector3.left : Vector3.right);
                
            DodgeAsync(dodgeDir, this.GetCancellationTokenOnDestroy()).Forget();
        }
    }

    private void HandleBasicAttack(Keyboard keyboard)
    {
        if (stats.IsDodging || stats.IsGuarding || stats.IsParrying) return;

        if (keyboard.xKey.wasPressedThisFrame)
        {
            if (!isAttacking)
            {
                comboStep = 1;
                PerformAttackStepAsync(this.GetCancellationTokenOnDestroy()).Forget();
            }
            else if (comboStep < 3)
            {
                hasQueuedAttack = true;
            }
        }
    }

    private async UniTaskVoid PerformAttackStepAsync(CancellationToken cancellationToken)
    {
        isAttacking = true;
        hasQueuedAttack = false;
        
        PlayerState attackState = comboStep switch
        {
            1 => PlayerState.Attack,
            2 => PlayerState.Attack2,
            3 => PlayerState.Attack3,
            _ => PlayerState.Attack
        };

        SetState(attackState, true);

        if (!IsJumping && motor != null)
        {
            currentMoveDir = Vector2.zero;
            motor.SetTargetVelocityX(0f);
        }

        uint currentSkillId = Util.CreateDataIdx(DataTableType.Skill, (uint)comboStep);
        if (skillExecutor != null)
        {
            skillExecutor.TryPlaySkillAnimation(animator, currentSkillId);
        }

        await UniTask.Delay(System.TimeSpan.FromSeconds(0.12f), cancellationToken: cancellationToken);

        if (skillExecutor != null)
        {
            Vector3 spawnOffset = (spriteRenderer != null && spriteRenderer.flipX) ? Vector3.right * 1.0f : Vector3.left * 1.0f;
            Vector3 spawnPos = transform.position + spawnOffset + Vector3.up * 0.8f;
            Color effectColor = new Color(0f, 1f, 0.4f, 0.4f);

            skillExecutor.SpawnSkillEffect($"Player_Hit{comboStep}", spawnPos, new Vector2(1.2f, 1.5f), 15f * comboStep, 0.15f, FactionType.PlayerAlly, effectColor);
            skillExecutor.SpawnSkillEffectFromDataAsync(currentSkillId, spawnPos).Forget();
        }

        await UniTask.Delay(System.TimeSpan.FromSeconds(0.28f), cancellationToken: cancellationToken);

        float windowElapsed = 0f;
        bool nextComboTriggered = false;

        while (windowElapsed < comboWindow)
        {
            windowElapsed += Time.deltaTime;

            if (hasQueuedAttack)
            {
                nextComboTriggered = true;
                comboStep++;
                break;
            }

            await UniTask.Yield(PlayerLoopTiming.Update, cancellationToken);
        }

        if (nextComboTriggered && comboStep <= 3)
        {
            PerformAttackStepAsync(cancellationToken).Forget();
        }
        else
        {
            isAttacking = false;
            hasQueuedAttack = false;
            comboStep = 0;

            SetState(IsJumping ? PlayerState.Jump : PlayerState.Idle, true);
        }
    }

    private async UniTaskVoid GuardParrySequenceAsync(Keyboard keyboard, CancellationToken cancellationToken)
    {
        if (motor != null)
        {
            motor.SetTargetVelocityX(0);
        }

        stats.SetParrying(true);
        stats.SetGuarding(false);
        SetState(PlayerState.Parry);

        await UniTask.Delay(150, cancellationToken: cancellationToken);
        stats.SetParrying(false);

        if (keyboard.spaceKey.isPressed)
        {
            stats.SetGuarding(true);
            SetState(PlayerState.Guard);

            while (keyboard.spaceKey.isPressed && !cancellationToken.IsCancellationRequested)
            {
                await UniTask.Yield(PlayerLoopTiming.Update, cancellationToken);
            }

            stats.SetGuarding(false);
            SetState(PlayerState.Idle);
        }
        else
        {
            SetState(PlayerState.Idle);
        }
    }

    private async UniTaskVoid DodgeAsync(Vector3 dodgeDir, CancellationToken cancellationToken)
    {
        if (stats.IsDodging || stats.IsGuarding || stats.IsParrying) return;
        stats.SetDodging(true);
        SetState(PlayerState.Dodge);

        float elapsed = 0f;
        float duration = 0.3f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;

            if (motor != null)
            {
                motor.SetTargetVelocityX(dodgeDir.x * DodgeDashSpeed);

                // 공중 회피 중 벽에 접촉하거나 점프 입력 시 회피 캔슬 & 벽점프 연계
                if (!motor.IsGrounded && motor.WallDir != 0)
                {
                    stats.SetDodging(false);
                    if (TryPerformWallJump())
                    {
                        return;
                    }
                }
            }

            await UniTask.Yield(PlayerLoopTiming.Update, cancellationToken);
        }

        if (motor != null)
        {
            motor.SetTargetVelocityX(0f);
        }

        stats.SetDodging(false);
        SetState(PlayerState.Idle);
    }

    private void HandleSkills(Keyboard keyboard)
    {
        if (isAttacking) return;

        uint skillNum = 0;
        if (keyboard.digit1Key.wasPressedThisFrame || keyboard.fKey.wasPressedThisFrame) skillNum = 1;
        else if (keyboard.digit2Key.wasPressedThisFrame || keyboard.rKey.wasPressedThisFrame) skillNum = 2;

        if (skillNum > 0)
        {
            SetState(PlayerState.Attack);
            Monster monsterTarget = null;
            foreach (var m in Monster.ActiveMonsters)
            {
                if (m != null && m.gameObject.activeInHierarchy)
                {
                    monsterTarget = m;
                    break;
                }
            }
            if (monsterTarget != null && skillExecutor != null)
            {
                skillExecutor.ExecuteSkill((int)skillNum, transform, monsterTarget.transform);
            }
        }
    }
}
