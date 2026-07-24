using Cysharp.Threading.Tasks;
using System.Threading;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 플레이어 유닛 클래스 (UnitBase 상속).
/// Unity InputSystem 조작, 4대 공수 선택지, 스킬 연동 및 PlayerState 동기화를 전담합니다.
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

    /// <summary>
    /// 플레이어 상태(PlayerState)를 변경하고 Animator 상태 파라미터("State") 및 애니메이션을 동기화합니다.
    /// </summary>
    public void SetState(PlayerState newState, bool forceUpdate = false)
    {
        if (this.CurrentState == newState && !forceUpdate) return;

        this.CurrentState = newState;

        if (this.animator != null && this.animator.runtimeAnimatorController != null)
        {
            this.animator.SetInteger("State", (int)newState);
            string animName = this.getAnimNameByState(newState);
            this.animator.Play(animName);
        }
    }


    // =========================================================================
    // 4. PROTECTED & PRIVATE METHODS (camelCase)
    // =========================================================================

    protected override void Awake()
    {
        base.Awake();
        // 플레이어 기본 UnitBaseData Idx: 3001
        this.InitUnitAsync(3001).Forget();
    }

    private void Update()
    {
        var keyboard = Keyboard.current;
        if (keyboard == null) return;

        this.handleMovement(keyboard);
        this.handleDefensiveActions(keyboard);
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
            PlayerState.Attack => "Player_ComboAttack",
            PlayerState.Execution => "Player_Execution",
            _ => "Player_Idle"
        };
    }

    private void handleMovement(Keyboard keyboard)
    {
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

            if (!this.IsJumping && !this.stats.IsDodging && !this.stats.IsGuarding && !this.stats.IsParrying)
            {
                this.SetState(PlayerState.Run);
            }
        }
    }

    private void updateIdleState()
    {
        if (this.currentMoveDir.sqrMagnitude <= 0.001f && !this.IsJumping && !this.stats.IsDodging && !this.stats.IsGuarding && !this.stats.IsParrying && this.CurrentState != PlayerState.Attack)
        {
            this.SetState(PlayerState.Idle);
        }
    }

    private void handleDefensiveActions(Keyboard keyboard)
    {
        bool defenseKeyPressedThisFrame = keyboard.leftShiftKey.wasPressedThisFrame || keyboard.jKey.wasPressedThisFrame || keyboard.qKey.wasPressedThisFrame;
        if (defenseKeyPressedThisFrame)
        {
            this.guardParrySequenceAsync(keyboard, this.GetCancellationTokenOnDestroy()).Forget();
        }

        if (keyboard.leftCtrlKey.wasPressedThisFrame || keyboard.lKey.wasPressedThisFrame)
        {
            Vector3 dodgeDir = this.currentMoveDir.sqrMagnitude > 0.001f ? this.currentMoveDir : -this.facingDir;
            this.dodgeAsync(dodgeDir, this.GetCancellationTokenOnDestroy()).Forget();
        }
    }

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
