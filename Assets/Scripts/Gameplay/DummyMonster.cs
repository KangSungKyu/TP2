using Cysharp.Threading.Tasks;
using System.Threading;
using UnityEngine;

/// <summary>
/// 기획서 1번 보스 '철위병 가론' 스타일의 더미 몬스터.
/// 언더스코어(_) 접두사 배제 규칙 및 this 키워드를 준수합니다.
/// UniTask (Cysharp.Threading.Tasks) 기반으로 4가지 패턴을 순환합니다.
/// </summary>
[RequireComponent(typeof(CombatStats))]
public class DummyMonster : MonoBehaviour
    {
        // =========================================================================
        // 1. PUBLIC FIELDS (PascalCase)
        // =========================================================================

        [SerializeField]
        public float AttackInterval = 4.0f;


        // =========================================================================
        // 2. PRIVATE FIELDS (camelCase, No '_' prefix)
        // =========================================================================

        private CombatStats stats;
        private Transform playerTarget;


        // =========================================================================
        // 3. PRIVATE METHODS (camelCase)
        // =========================================================================

        private void Awake()
        {
            this.stats = GetComponent<CombatStats>();

            // Root는 발밑 지면 피벗(Y=0)을 담당하며, 렌더링은 하위 "Visual" 객체에서 전담합니다.
            Transform visualTransform = transform.Find("Visual");
            if (visualTransform == null)
            {
                GameObject visualObj = new GameObject("Visual");
                visualTransform = visualObj.transform;
                visualTransform.SetParent(transform, false);
                visualTransform.localPosition = new Vector3(0f, 0.75f, 0f); // 몬스터 렌더링 피벗 오프셋
            }

            var spriteRenderer = visualTransform.GetComponent<SpriteRenderer>();
            if (spriteRenderer == null)
            {
                spriteRenderer = visualTransform.gameObject.AddComponent<SpriteRenderer>();
                spriteRenderer.sortingOrder = 5;
            }

            var animator = visualTransform.GetComponent<Animator>();
            if (animator == null)
            {
                animator = visualTransform.gameObject.AddComponent<Animator>();
            }
        }

        private void Start()
        {
            this.patternLoopAsync(this.GetCancellationTokenOnDestroy()).Forget();
        }

        private void Update()
        {
            if (this.playerTarget == null)
            {
                var player = GameObject.FindWithTag("Player");
                if (player != null) this.playerTarget = player.transform;
            }
        }

        private async UniTaskVoid patternLoopAsync(CancellationToken cancellationToken)
        {
            int patternIndex = 0;
            while (!cancellationToken.IsCancellationRequested)
            {
                int delayMs = Mathf.RoundToInt(this.AttackInterval * 1000f);
                await UniTask.Delay(delayMs, cancellationToken: cancellationToken);

                if (this.stats.IsGroggy)
                {
                    Debug.Log("[DummyMonster] 그로기 상태로 인해 공격 불가!");
                    await UniTask.Delay(1500, cancellationToken: cancellationToken);
                    continue;
                }

                if (this.playerTarget == null) continue;

                patternIndex = (patternIndex % 4) + 1;

                switch (patternIndex)
                {
                    case 1:
                        await this.pattern1_DashThrustAsync(cancellationToken);
                        break;
                    case 2:
                        await this.pattern2_ContinuousFlurryAsync(cancellationToken);
                        break;
                    case 3:
                        await this.pattern3_HeavySlamAsync(cancellationToken);
                        break;
                    case 4:
                        await this.pattern4_ShockwaveSweepAsync(cancellationToken);
                        break;
                }
            }
        }

        #region Patterns (Garon Reference - UniTask)
        // Pattern 1: 돌진 찌르기 (회피 테스트 - 전조 0.8초, 15m/s 돌진)
        private async UniTask pattern1_DashThrustAsync(CancellationToken cancellationToken)
        {
            Debug.Log("<color=yellow>[DummyMonsterUniTask] [패턴 1] 돌진 찌르기 준비! (전조 0.8초 - 회피/대쉬 권장)</color>");
            await UniTask.Delay(800, cancellationToken: cancellationToken);

            Vector3 startPos = transform.position;
            Vector3 targetDir = (this.playerTarget.position - startPos).normalized;
            float dashTime = 0.3f;
            float elapsed = 0f;

            while (elapsed < dashTime)
            {
                elapsed += Time.deltaTime;
                transform.Translate(targetDir * 15f * Time.deltaTime, Space.World);

                if (Vector3.Distance(transform.position, this.playerTarget.position) < 1.2f)
                {
                    var playerStats = this.playerTarget.GetComponent<CombatStats>();
                    if (playerStats != null)
                    {
                        playerStats.TakeDamage(20f, isGroundAttack: false, isJumped: false, attacker: this.stats);
                    }
                    break;
                }

                await UniTask.Yield(PlayerLoopTiming.Update, cancellationToken);
            }
        }

        // Pattern 2: 연속 난타 (가드 테스트 - 전조 0.6초, 4연타)
        private async UniTask pattern2_ContinuousFlurryAsync(CancellationToken cancellationToken)
        {
            Debug.Log("<color=orange>[DummyMonsterUniTask] [패턴 2] 연속 난타 준비! (전조 0.6초 - 가드 권장)</color>");
            await UniTask.Delay(600, cancellationToken: cancellationToken);

            var playerStats = this.playerTarget.GetComponent<CombatStats>();
            for (int i = 0; i < 4; i++)
            {
                Debug.Log($"[DummyMonsterUniTask] 휘두르기 {i + 1}타!");
                if (playerStats != null && Vector3.Distance(transform.position, this.playerTarget.position) < 2.5f)
                {
                    playerStats.TakeDamage(10f, isGroundAttack: false, isJumped: false, attacker: this.stats);
                }
                await UniTask.Delay(350, cancellationToken: cancellationToken);
            }
        }

        // Pattern 3: 묵직한 내려찍기 (패링 테스트 - 전조 1.0초)
        private async UniTask pattern3_HeavySlamAsync(CancellationToken cancellationToken)
        {
            Debug.Log("<color=red>[DummyMonsterUniTask] [패턴 3] 묵직한 내려찍기 준비! (전조 1.0초 - 패링 권장)</color>");
            await UniTask.Delay(1000, cancellationToken: cancellationToken);

            var playerStats = this.playerTarget.GetComponent<CombatStats>();
            if (playerStats != null && Vector3.Distance(transform.position, this.playerTarget.position) < 2.5f)
            {
                bool parried = playerStats.TakeDamage(35f, isGroundAttack: false, isJumped: false, attacker: this.stats);
                if (parried && playerStats.IsParrying)
                {
                    Debug.Log("<color=cyan>[DummyMonsterUniTask] 플레이어의 패링에 막혀 1.5초간 대형 경직 발생!</color>");
                    this.stats.AddPosture(40f);
                    await UniTask.Delay(1500, cancellationToken: cancellationToken);
                }
            }
        }

        // Pattern 4: 충격파 바닥 쓸기 (점프 테스트 - 전조 0.8초, 지면 판정)
        private async UniTask pattern4_ShockwaveSweepAsync(CancellationToken cancellationToken)
        {
            Debug.Log("<color=green>[DummyMonsterUniTask] [패턴 4] 충격파 바닥 쓸기 준비! (전조 0.8초 - 점프 권장)</color>");
            await UniTask.Delay(800, cancellationToken: cancellationToken);

            var playerController = this.playerTarget.GetComponent<PlayerController>();
            var playerStats = this.playerTarget.GetComponent<CombatStats>();
            bool isJumped = playerController != null && playerController.IsJumping;

            if (playerStats != null && Vector3.Distance(transform.position, this.playerTarget.position) < 4.0f)
            {
                playerStats.TakeDamage(25f, isGroundAttack: true, isJumped: isJumped, attacker: this.stats);
            }
        }
        #endregion
    }

