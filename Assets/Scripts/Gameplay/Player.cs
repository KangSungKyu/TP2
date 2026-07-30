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


    // =========================================================================
    // 2. PRIVATE FIELDS (camelCase, No '_' prefix)
    // =========================================================================

    private Vector3 currentMoveDir = Vector3.zero;
    private Vector3 facingDir = Vector3.right;

    // 콤보 공격 관련 변수
    private int comboStep = 0; // 0: 공격안함, 1: 1타, 2: 2타, 3: 3타
    private bool isAttacking = false;
    private bool hasQueuedAttack = false;
    private float comboWindow = 0.5f; // 각 타격 후 다음 입력을 기다리는 허용 시간 (기본값)

    // 메트로배니아 물리 점프 관련 변수
    [SerializeField]
    private float jumpForce = 11.5f;
    private float coyoteTime = 0.12f;
    private float coyoteTimeCounter = 0f;
    private float jumpBufferTime = 0.12f;
    private float jumpBufferCounter = 0f;


    // =========================================================================
    // 3. PUBLIC METHODS (PascalCase)
    // =========================================================================

    public override async UniTask InitUnitAsync(uint unitIdx)
    {
        await base.InitUnitAsync(unitIdx);

        if (this.UnitData != null && this.UnitData.MoveSpeed > 0f)
        {
            this.Speed = this.UnitData.MoveSpeed;
        }
    }

    public void SetState(PlayerState newState, bool forceUpdate = false)
    {
        if (this.CurrentState == newState && !forceUpdate) return;

        this.CurrentState = newState;

        if (this.animator != null && this.animator.runtimeAnimatorController != null)
        {
            this.animator.SetInteger("State", (int)newState);
            // 콤보 공격 상태일 때는 getAnimNameByState에서 예외처리 하지 않고 AttackAsync에서 직접 Play 호출
            if (newState != PlayerState.Attack)
            {
                string animName = this.getAnimNameByState(newState);
                this.animator.Play(animName);
            }
        }
    }


    // =========================================================================
    // 4. PROTECTED & PRIVATE METHODS (camelCase)
    // =========================================================================



    protected override void Awake()
    {
        base.Awake();
        this.motor = GetComponent<KinematicMotor2D>();
        if (this.motor == null)
        {
            this.motor = gameObject.AddComponent<KinematicMotor2D>();
        }
        this.InitUnitAsync(3001).Forget();
    }

    private void Update()
    {
        var keyboard = Keyboard.current;
        if (keyboard == null) return;

        bool isJumpingInput = keyboard.cKey.isPressed;
        this.handleMovement(keyboard);
        this.handleJump(keyboard);

        // 모터 상태 전달 (모터는 FixedUpdate에서 자체 구동, isGrounded는 UnitBase 프로퍼티가 motor에서 직접 읽음)
        if (this.motor != null)
        {
            this.motor.SetJumpHeld(isJumpingInput);
        }

        this.handleDefensiveActions(keyboard);
        this.handleBasicAttack(keyboard);
        this.handleExecutionAction(keyboard);
        this.handleSkills(keyboard);
        this.updateIdleState();
    }

    private OneWayPlatformPassThrough currentOneWayPlatform;


    private void handleJump(Keyboard keyboard)
    {
        if (this.stats.IsDodging || this.stats.IsGuarding || this.stats.IsParrying)
            return;

        bool isGroundedNow = this.motor != null ? this.motor.IsGrounded : this.isGrounded;

        // 1. 아래 키 (S / DownArrow) + 점프 키 (C / Space) 입력 시 1-Way 발판 하향 점프 (Drop Through)
        bool isDownPressed = keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed;
        if (isDownPressed && keyboard.cKey.wasPressedThisFrame && isGroundedNow)
        {
            if (this.motor != null)
            {
                this.motor.PassThroughOneWayPlatformAsync(0.25f, this.GetCancellationTokenOnDestroy()).Forget();
            }
            return;
        }

        // 2. 일반 점프 버퍼 타이머 (Pre-Input)
        if (keyboard.cKey.wasPressedThisFrame)
        {
            this.jumpBufferCounter = this.jumpBufferTime;
        }
        else
        {
            this.jumpBufferCounter -= Time.deltaTime;
        }

        if (isGroundedNow)
        {
            this.coyoteTimeCounter = this.coyoteTime;
            this.IsJumping = false;
        }
        else
        {
            this.coyoteTimeCounter -= Time.deltaTime;
        }

        // 3. 가변 점프 (Variable Jump Height): 상승 중 버튼을 떼면 속도 감쇄하여 소점프 구현
        if (keyboard.cKey.wasReleasedThisFrame && this.motor != null && this.motor.Velocity.y > 0f)
        {
            this.motor.SetVelocityY(this.motor.Velocity.y * 0.4f);
        }

        // 4. 점프 실행 (Coyote Time & Jump Buffer 조합)
        if (this.jumpBufferCounter > 0f && this.coyoteTimeCounter > 0f)
        {
            if (this.motor != null)
            {
                this.motor.SetVelocityY(this.jumpForce);
            }
            this.IsJumping = true;
            if (this.stats != null)
            {
                this.stats.SetJumped(true);
            }

            this.SetState(PlayerState.Jump);
            this.jumpBufferCounter = 0f;
            this.coyoteTimeCounter = 0f;
        }
    }


    private void handleExecutionAction(Keyboard keyboard)
    {
        // Left Ctrl 키 입력 시 사거리 내 Groggy 상태 몬스터 탐색 후 공용 처형 실행
        if (keyboard.leftCtrlKey.wasPressedThisFrame)
        {
            var monsters = GameObject.FindObjectsOfType<Monster>();
            foreach (var monster in monsters)
            {
                if (monster != null && Vector3.Distance(transform.position, monster.transform.position) <= 2.5f)
                {
                    if (this.TryExecuteTarget(monster, executionMultiplier: 5.0f))
                    {
                        this.SetState(PlayerState.Execution, forceUpdate: true);
                        break;
                    }
                }
            }
        }
    }


    private string getAnimNameByState(PlayerState state)
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

    private readonly RaycastHit2D[] wallHitBuffer = new RaycastHit2D[4];

    private void handleMovement(Keyboard keyboard)
    {
        if (this.stats.IsDodging || this.stats.IsGuarding || this.stats.IsParrying)
            return;

        if (this.isAttacking && !this.IsJumping)
            return;

        float moveX = 0f;
        if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed) moveX -= 1f;
        if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed) moveX += 1f;

        if (Mathf.Abs(moveX) > 0.01f)
        {
            Vector2 dir = new Vector3(moveX, 0f, 0f);
            this.currentMoveDir = dir;
            this.facingDir = dir;
            this.SetFacingRight(moveX >= 0);

            if (this.motor != null)
            {
                this.motor.SetTargetVelocityX(moveX * this.Speed);
            }

            if (!this.IsJumping && !this.isAttacking)
            {
                this.SetState(PlayerState.Run);
            }
        }
        else
        {
            this.currentMoveDir = Vector2.zero;

            if (this.motor != null)
            {
                this.motor.SetTargetVelocityX(0f);
            }
        }
    }

    private void updateIdleState()
    {
        if (this.currentMoveDir.sqrMagnitude <= 0.001f && 
            !this.IsJumping && 
            !this.stats.IsDodging && 
            !this.stats.IsGuarding && 
            !this.stats.IsParrying && 
            !this.isAttacking)
        {
            this.SetState(PlayerState.Idle);
        }
    }

    private void handleDefensiveActions(Keyboard keyboard)
    {
        if (this.isAttacking || this.IsJumping) return;

        // Space Bar 키: 가드 / 패링 통합 입력
        bool defenseKeyPressedThisFrame = keyboard.spaceKey.wasPressedThisFrame;
        if (defenseKeyPressedThisFrame)
        {
            this.guardParrySequenceAsync(keyboard, this.GetCancellationTokenOnDestroy()).Forget();
        }

        // Left Shift 키: 대시 / 회피 입력
        if (keyboard.leftShiftKey.wasPressedThisFrame)
        {
            // 이동 중: 이동 방향으로 회피 / 정지 중: 바라보는 방향 반대(뒤)로 회피
            Vector3 dodgeDir;
            if (this.currentMoveDir.sqrMagnitude > 0.001f)
            {
                dodgeDir = this.currentMoveDir.normalized;
            }
            else
            {
                dodgeDir = this.facingDir.x >= 0 ? Vector3.left : Vector3.right;
            }
            this.dodgeAsync(dodgeDir, this.GetCancellationTokenOnDestroy()).Forget();
        }
    }

    // =========================================================================
    // 5. ATTACK & COMBO SYSTEM (Ground & Air Attack)
    // =========================================================================

    private void handleBasicAttack(Keyboard keyboard)
    {
        // 방어/회피 중에는 공격 불가
        if (this.stats.IsDodging || this.stats.IsGuarding || this.stats.IsParrying) return;

        // X 키가 이번 프레임에 눌렸을 때
        if (keyboard.xKey.wasPressedThisFrame)
        {
            if (!this.isAttacking)
            {
                // 공격 중이 아니면 1타 시작 (지상/공중 공용)
                this.comboStep = 1;
                this.performAttackStepAsync(this.GetCancellationTokenOnDestroy()).Forget();
            }
            else
            {
                // 이미 공격 중이고 3타 미만이면 다음 타격 예약(선입력)
                if (this.comboStep < 3)
                {
                    this.hasQueuedAttack = true;
                }
            }
        }
    }

    private async UniTaskVoid performAttackStepAsync(CancellationToken cancellationToken)
    {
        this.isAttacking = true;
        this.hasQueuedAttack = false;
        
        PlayerState attackState = this.comboStep switch
        {
            1 => PlayerState.Attack,
            2 => PlayerState.Attack2,
            3 => PlayerState.Attack3,
            _ => PlayerState.Attack
        };

        // 공격 상태로 돌입 (지상/공중 공용 모션 재생)
        this.SetState(attackState, true);

        if (this.motor != null)
        {
            this.motor.SetTargetVelocityX(0);
        }

        uint currentSkillId = Util.CreateDataIdx(DataTableType.Skill, (uint)this.comboStep); // Util 유틸 함수로 DataTableType.Skill Idx 생성
        if (this.skillExecutor != null)
        {
            this.skillExecutor.TryPlaySkillAnimation(this.animator, currentSkillId);
        }

        // 1. 공격 모션 진행 및 타격 타이밍(0.12s)에 독립 SkillEffect 스폰
        await UniTask.Delay(System.TimeSpan.FromSeconds(0.12f), cancellationToken: cancellationToken);

        if (this.skillExecutor != null)
        {
            Vector3 spawnOffset = (this.spriteRenderer != null && this.spriteRenderer.flipX) ? Vector3.right * 1.0f : Vector3.left * 1.0f;
            Vector3 spawnPos = transform.position + spawnOffset + Vector3.up * 0.8f;
            Color effectColor = new Color(0f, 1f, 0.4f, 0.4f); // 초록 반투명 검기 이펙트

            // 1) 2D Trigger Hitbox 스폰 (데미지 및 충돌 판정)
            this.skillExecutor.SpawnSkillEffect($"Player_Hit{this.comboStep}", spawnPos, new Vector2(1.2f, 1.5f), 15f * this.comboStep, 0.15f, FactionType.PlayerAlly, effectColor);

            // 2) SkillData -> EffectData -> ResourceData -> InstantiateAsyncTask 비주얼 이펙트 비동기 스폰
            this.skillExecutor.SpawnSkillEffectFromDataAsync(currentSkillId, spawnPos).Forget();
        }

        await UniTask.Delay(System.TimeSpan.FromSeconds(0.28f), cancellationToken: cancellationToken);

        // 2. 공격 창(Combo Window) 대기 
        // 애니메이션이 끝나갈 무렵, 선입력(hasQueuedAttack)이 들어왔는지 확인
        float windowElapsed = 0f;
        bool nextComboTriggered = false;

        while (windowElapsed < this.comboWindow)
        {
            windowElapsed += Time.deltaTime;

            if (this.hasQueuedAttack)
            {
                nextComboTriggered = true;
                this.comboStep++;
                break;
            }

            await UniTask.Yield(PlayerLoopTiming.Update, cancellationToken);
        }

        // 3. 다음 콤보로 이행하거나 종료
        if (nextComboTriggered && this.comboStep <= 3)
        {
            // 재귀적으로 다음 타격 실행 (Forget 처리된 루프 형태)
            this.performAttackStepAsync(cancellationToken).Forget();
        }
        else
        {
            // 콤보 종료 및 초기화
            this.isAttacking = false;
            this.hasQueuedAttack = false;
            this.comboStep = 0;

            if (!this.IsJumping)
            {
                this.SetState(PlayerState.Idle, true);
            }
            else
            {
                this.SetState(PlayerState.Jump, true);
            }
        }
    }


    // =========================================================================
    // 6. SKILLS & DEFENSIVE TASKS
    // =========================================================================

    private async UniTaskVoid guardParrySequenceAsync(Keyboard keyboard, CancellationToken cancellationToken)
    {
        if (this.motor != null)
        {
            this.motor.SetTargetVelocityX(0);
        }

        this.stats.SetParrying(true);
        this.stats.SetGuarding(false);
        this.SetState(PlayerState.Parry);

        await UniTask.Delay(150, cancellationToken: cancellationToken);
        this.stats.SetParrying(false);

        bool isDefenseKeyPressed = keyboard.spaceKey.isPressed;
        if (isDefenseKeyPressed)
        {
            this.stats.SetGuarding(true);
            this.SetState(PlayerState.Guard);

            while (keyboard.spaceKey.isPressed && !cancellationToken.IsCancellationRequested)
            {
                await UniTask.Yield(PlayerLoopTiming.Update, cancellationToken);
            }

            this.stats.SetGuarding(false);
            this.SetState(PlayerState.Idle);
        }
        else
        {
            this.SetState(PlayerState.Idle);
        }
    }

    private async UniTaskVoid dodgeAsync(Vector3 dodgeDir, CancellationToken cancellationToken)
    {
        this.stats.SetDodging(true);
        this.SetState(PlayerState.Dodge);

        float elapsed = 0f;
        float duration = 0.3f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;

            // 모터를 경유하여 Swept BoxCast 충돌 검사를 거친 안전한 이동
            if (this.motor != null)
            {
                this.motor.SetTargetVelocityX(dodgeDir.x * this.DodgeDashSpeed);
            }

            await UniTask.Yield(PlayerLoopTiming.Update, cancellationToken);
        }

        // 회피 종료: 수평 속도 정지
        if (this.motor != null)
        {
            this.motor.SetTargetVelocityX(0f);
        }

        this.stats.SetDodging(false);
        this.SetState(PlayerState.Idle);
    }

    private void handleSkills(Keyboard keyboard)
    {
        if (this.isAttacking) return; // 공격 중 스킬 불가

        if (keyboard.digit1Key.wasPressedThisFrame || keyboard.fKey.wasPressedThisFrame)
        {
            this.SetState(PlayerState.Attack);
            var monster = GameObject.FindObjectOfType<Monster>();
            if (monster != null && this.skillExecutor != null)
            {
                this.skillExecutor.ExecuteSkill(1, transform, monster.transform);
            }
        }

        if (keyboard.digit2Key.wasPressedThisFrame || keyboard.rKey.wasPressedThisFrame)
        {
            this.SetState(PlayerState.Attack);
            var monster = GameObject.FindObjectOfType<Monster>();
            if (monster != null && this.skillExecutor != null)
            {
                this.skillExecutor.ExecuteSkill(2, transform, monster.transform);
            }
        }
    }
}
