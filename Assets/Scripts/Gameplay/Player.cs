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
        this.InitUnitAsync(3001).Forget();
    }

    private void Update()
    {
        var keyboard = Keyboard.current;
        if (keyboard == null) return;

        this.handleMovement(keyboard);
        this.handleDefensiveActions(keyboard);
        this.handleBasicAttack(keyboard);
        this.handleSkills(keyboard);
        this.updateIdleState();
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

    private void handleMovement(Keyboard keyboard)
    {
        // 공격 중이거나 방어 행동 중일 때는 이동 불가 처리
        if (this.isAttacking || this.stats.IsDodging || this.stats.IsGuarding || this.stats.IsParrying)
            return;

        this.currentMoveDir = Vector3.zero;

        if (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed) this.currentMoveDir.z += 1f;
        if (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed) this.currentMoveDir.z -= 1f;
        if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed) this.currentMoveDir.x -= 1f;
        if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed) this.currentMoveDir.x += 1f;

        if (this.currentMoveDir.sqrMagnitude > 0.001f)
        {
            this.currentMoveDir.Normalize();
            this.facingDir = this.currentMoveDir;

            this.SetFacingRight(this.facingDir.x >= 0);
            transform.Translate(this.currentMoveDir * this.Speed * Time.deltaTime, Space.World);

            if (!this.IsJumping)
            {
                this.SetState(PlayerState.Run);
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
        if (this.isAttacking) return; // 공격 중 방어 행동 차단 (또는 추후 캔슬 로직 추가 가능)

        bool defenseKeyPressedThisFrame = keyboard.leftShiftKey.wasPressedThisFrame || keyboard.jKey.wasPressedThisFrame || keyboard.qKey.wasPressedThisFrame;
        if (defenseKeyPressedThisFrame)
        {
            this.guardParrySequenceAsync(keyboard, this.GetCancellationTokenOnDestroy()).Forget();
        }

        if (keyboard.leftCtrlKey.wasPressedThisFrame || keyboard.lKey.wasPressedThisFrame)
        {
            Vector3 dodgeDir = this.currentMoveDir.sqrMagnitude > 0.001f ? this.currentMoveDir : (this.facingDir.x >= 0 ? Vector3.left : Vector3.right);
            this.dodgeAsync(dodgeDir, this.GetCancellationTokenOnDestroy()).Forget();
        }
    }

    // =========================================================================
    // 5. ATTACK & COMBO SYSTEM (New)
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
                // 공격 중이 아니면 1타 시작
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

        // 공격 상태로 돌입
        this.SetState(attackState, true);

        string animClipName = this.comboStep switch
        {
            1 => "Player_Attack_Hit1",
            2 => "Player_Attack_Hit2",
            3 => "Player_Attack_Hit3",
            _ => "Player_Attack_Hit1"
        };
        
        if (this.animator != null)
        {
            this.animator.Play(animClipName, 0, 0f);
        }

        // [TODO] 콤보 단계에 따라 데미지나 범위, 딜레이를 다르게 설정 가능
        float attackDuration = 0.4f; // 1,2,3타 애니메이션 지속 시간 및 딜레이
        
        // 1. 공격 모션 진행 대기
        await UniTask.Delay(System.TimeSpan.FromSeconds(attackDuration), cancellationToken: cancellationToken);

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
            this.SetState(PlayerState.Idle, true);
        }
    }


    // =========================================================================
    // 6. SKILLS & DEFENSIVE TASKS
    // =========================================================================

    private async UniTaskVoid guardParrySequenceAsync(Keyboard keyboard, CancellationToken cancellationToken)
    {
        this.stats.SetParrying(true);
        this.stats.SetGuarding(false);
        this.SetState(PlayerState.Parry);

        await UniTask.Delay(150, cancellationToken: cancellationToken);
        this.stats.SetParrying(false);

        bool isDefenseKeyPressed = keyboard.leftShiftKey.isPressed || keyboard.jKey.isPressed || keyboard.qKey.isPressed;
        if (isDefenseKeyPressed)
        {
            this.stats.SetGuarding(true);
            this.SetState(PlayerState.Guard);

            while ((keyboard.leftShiftKey.isPressed || keyboard.jKey.isPressed || keyboard.qKey.isPressed) && !cancellationToken.IsCancellationRequested)
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
            transform.Translate(dodgeDir * this.DodgeDashSpeed * Time.deltaTime, Space.World);

            await UniTask.Yield(PlayerLoopTiming.Update, cancellationToken);
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
