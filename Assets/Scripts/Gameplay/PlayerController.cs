using Cysharp.Threading.Tasks;
using System.Threading;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 플레이어 컨트롤러.
/// 언더스코어(_) 접두사 배제 규칙 및 this 키워드를 통한 멤버 접근 규칙을 준수합니다.
/// </summary>
[RequireComponent(typeof(CombatStats))]
public class PlayerController : MonoBehaviour
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

        private CombatStats stats;
        private SkillExecutor skillExecutor;
        private Animator animator;
        private SpriteRenderer spriteRenderer;

        private Vector3 currentMoveDir = Vector3.zero;
        private Vector3 facingDir = Vector3.right; // 기본 바라보는 방향


        // =========================================================================
        // 3. PUBLIC METHODS (PascalCase)
        // =========================================================================

        /// <summary>
        /// 플레이어 상태(PlayerState)를 변경하고 Animator 상태 파라미터("State") 및 애니메이션을 동기화합니다.
        /// </summary>
        public void SetState(PlayerState newState, bool forceUpdate = false)
        {
            if (this.CurrentState == newState && !forceUpdate) return;

            this.CurrentState = newState;

            if (this.animator != null && this.animator.runtimeAnimatorController != null)
            {
                // Animator "State" Int 파라미터 업데이트
                this.animator.SetInteger("State", (int)newState);

                // 상태에 따른 애니메이션 클립 명시적 재생
                string animName = this.getAnimNameByState(newState);
                this.animator.Play(animName);
            }
        }


        // =========================================================================
        // 4. PRIVATE METHODS (camelCase)
        // =========================================================================

        private void Awake()
        {
            this.stats = GetComponent<CombatStats>();
            this.skillExecutor = GetComponent<SkillExecutor>();

            // Root는 발밑 지면 피벗(Y=0)을 담당하며, 렌더링은 하위 "Visual" 객체에서 전담합니다.
            Transform visualTransform = transform.Find("Visual");
            if (visualTransform == null)
            {
                GameObject visualObj = new GameObject("Visual");
                visualTransform = visualObj.transform;
                visualTransform.SetParent(transform, false);
                visualTransform.localPosition = new Vector3(0f, 0.5f, 0f); // 렌더링 피벗 오프셋
            }

            this.spriteRenderer = visualTransform.GetComponent<SpriteRenderer>();
            if (this.spriteRenderer == null)
            {
                this.spriteRenderer = visualTransform.gameObject.AddComponent<SpriteRenderer>();
                this.spriteRenderer.sortingOrder = 10;
            }

            this.animator = visualTransform.GetComponent<Animator>();
            if (this.animator == null)
            {
                this.animator = visualTransform.gameObject.AddComponent<Animator>();
            }

            // ResourceManager를 사용하여 Addressable 키 "PlayerAnimatorController"로 컨트롤러 동적 로드
            if (ResourceManager.Instance != null)
            {
                ResourceManager.Instance.LoadAssetAsync<RuntimeAnimatorController>("PlayerAnimatorController", controller =>
                {
                    if (controller != null)
                    {
                        this.animator.runtimeAnimatorController = controller;
                        Debug.Log("<color=green>[PlayerController] ResourceManager를 통해 하위 'Visual' 객체에 'PlayerAnimatorController' 바인딩 완료!</color>");
                        
                        // 현재 상태로 초기 애니메이션 세팅
                        this.SetState(this.CurrentState, forceUpdate: true);
                    }
                });
            }
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
                this.facingDir = this.currentMoveDir; // 이동 시 바라보는 방향 갱신

                // 스프라이트 및 3D Visual 방향 좌우 반전
                if (this.spriteRenderer != null)
                {
                    this.spriteRenderer.flipX = this.facingDir.x < 0;
                }
                var visualTransform = transform.Find("Visual");
                if (visualTransform != null)
                {
                    visualTransform.localScale = new Vector3(this.facingDir.x < 0 ? -1f : 1f, 1.2f, 1f);
                }

                transform.Translate(this.currentMoveDir * this.Speed * Time.deltaTime, Space.World);

                if (!this.IsJumping && !this.stats.IsDodging && !this.stats.IsGuarding && !this.stats.IsParrying)
                {
                    this.SetState(PlayerState.Run);
                }
            }

            // 점프 (Space) -> UniTask 기반 실행
            if (keyboard.spaceKey.wasPressedThisFrame && !this.IsJumping)
            {
                this.jumpAsync(this.GetCancellationTokenOnDestroy()).Forget();
            }
        }

        private void updateIdleState()
        {
            if (this.currentMoveDir.sqrMagnitude <= 0.001f && !this.IsJumping && !this.stats.IsDodging && !this.stats.IsGuarding && !this.stats.IsParrying && this.CurrentState != PlayerState.Attack)
            {
                this.SetState(PlayerState.Idle);
            }
        }

        private async UniTaskVoid jumpAsync(CancellationToken cancellationToken)
        {
            this.IsJumping = true;
            this.SetState(PlayerState.Jump);

            float elapsed = 0f;
            float duration = 0.6f;
            Vector3 startPos = transform.position;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float height = Mathf.Sin((elapsed / duration) * Mathf.PI) * 1.5f;
                transform.position = new Vector3(transform.position.x, startPos.y + height, transform.position.z);

                await UniTask.Yield(PlayerLoopTiming.Update, cancellationToken);
            }

            transform.position = new Vector3(transform.position.x, startPos.y, transform.position.z);
            this.IsJumping = false;
            this.SetState(PlayerState.Idle);
        }

        private void handleDefensiveActions(Keyboard keyboard)
        {
            // 1. 통합 방어 키 (LeftShift 또는 J 키 입력 시)
            bool defenseKeyPressedThisFrame = keyboard.leftShiftKey.wasPressedThisFrame || keyboard.jKey.wasPressedThisFrame || keyboard.qKey.wasPressedThisFrame;
            if (defenseKeyPressedThisFrame)
            {
                this.guardParrySequenceAsync(keyboard, this.GetCancellationTokenOnDestroy()).Forget();
            }

            // 2. Dodge (LeftCtrl 또는 L 키 입력 시 회피 대시) -> UniTask
            if (keyboard.leftCtrlKey.wasPressedThisFrame || keyboard.lKey.wasPressedThisFrame)
            {
                Vector3 dodgeDir = this.currentMoveDir.sqrMagnitude > 0.001f ? this.currentMoveDir : -this.facingDir;
                this.dodgeAsync(dodgeDir, this.GetCancellationTokenOnDestroy()).Forget();
            }
        }

        private async UniTaskVoid guardParrySequenceAsync(Keyboard keyboard, CancellationToken cancellationToken)
        {
            // [Phase 1] 누르는 순간: 0.15초 패링 윈도우 (Parry Window)
            this.stats.SetParrying(true);
            this.stats.SetGuarding(false);
            this.SetState(PlayerState.Parry);
            Debug.Log("<color=magenta>[DefenseSystem] 방어 키 눌림 -> 패링(PlayerState.Parry) 상태 전환!</color>");

            await UniTask.Delay(150, cancellationToken: cancellationToken);
            this.stats.SetParrying(false);

            // [Phase 2] 0.15초 이후에도 방어 키가 계속 눌려 있다면 -> 가드(Guard) 상태로 전환
            bool isDefenseKeyPressed = keyboard.leftShiftKey.isPressed || keyboard.jKey.isPressed || keyboard.qKey.isPressed;
            if (isDefenseKeyPressed)
            {
                this.stats.SetGuarding(true);
                this.SetState(PlayerState.Guard);
                Debug.Log("<color=yellow>[DefenseSystem] 패링 종료 -> 가드(PlayerState.Guard) 상태 전환!</color>");

                // 키를 뗄 때까지 가드 유지
                while ((keyboard.leftShiftKey.isPressed || keyboard.jKey.isPressed || keyboard.qKey.isPressed) && !cancellationToken.IsCancellationRequested)
                {
                    await UniTask.Yield(PlayerLoopTiming.Update, cancellationToken);
                }

                this.stats.SetGuarding(false);
                Debug.Log("[DefenseSystem] 방어 키 뗌 -> 가드 해제.");
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
            string dodgeType = this.currentMoveDir.sqrMagnitude > 0.001f ? "이동 방향 대시" : "백 대시(Back Dash)";
            Debug.Log($"<color=cyan>[PlayerUniTask] 회피(PlayerState.Dodge) - {dodgeType}! (0.3초 무적)</color>");

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
            // 1. 기본 공격 (1 키 또는 F 키)
            if (keyboard.digit1Key.wasPressedThisFrame || keyboard.fKey.wasPressedThisFrame)
            {
                this.SetState(PlayerState.Attack);
                var monster = GameObject.FindObjectOfType<DummyMonster>();
                if (monster != null && this.skillExecutor != null)
                {
                    this.skillExecutor.ExecuteSkill(1, transform, monster.transform);
                }
            }

            // 2. 파이어볼 스킬 (2 키 또는 R 키)
            if (keyboard.digit2Key.wasPressedThisFrame || keyboard.rKey.wasPressedThisFrame)
            {
                this.SetState(PlayerState.Attack);
                var monster = GameObject.FindObjectOfType<DummyMonster>();
                if (monster != null && this.skillExecutor != null)
                {
                    this.skillExecutor.ExecuteSkill(2, transform, monster.transform);
                }
            }
        }
    }

