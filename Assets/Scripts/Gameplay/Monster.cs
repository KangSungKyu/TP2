using Cysharp.Threading.Tasks;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;

/// <summary>
/// 몬스터 유닛 클래스 (UnitBase 상속).
/// MonsterBaseData 및 MonsterPatternData 기반으로 3가지 AI 모드
/// [Default Simple AI, Sequence Pattern AI, Trigger/Random Pattern AI]를 완벽히 구동합니다.
/// </summary>
public class Monster : UnitBase
{
    // =========================================================================
    // 1. PUBLIC FIELDS & PROPERTIES (PascalCase)
    // =========================================================================

    public MonsterBaseData MonsterData { get; protected set; }
    public List<MonsterPatternData> Patterns { get; protected set; } = new List<MonsterPatternData>();


    // =========================================================================
    // 2. PROTECTED & PRIVATE FIELDS (camelCase)
    // =========================================================================

    protected Transform playerTarget;
    protected int currentSequenceIndex = 0;
    protected readonly Dictionary<uint, float> patternCooldowns = new Dictionary<uint, float>();


    // =========================================================================
    // 3. PUBLIC METHODS (PascalCase)
    // =========================================================================

    public override async UniTask InitUnitAsync(uint unitIdx)
    {
        await base.InitUnitAsync(unitIdx);

        var monsterDB = DataTableManager.Instance != null ? DataTableManager.Instance.GetDB<MonsterDataTable>(DataTableType.MonsterData) : null;
        if (monsterDB != null && monsterDB.TryGetMonsterData(unitIdx, out var mData))
        {
            MonsterData = mData;
            LoadPatterns(mData);
        }
    }


    // =========================================================================
    // 4. PROTECTED & PRIVATE METHODS
    // =========================================================================

    public static HashSet<Monster> ActiveMonsters { get; } = new HashSet<Monster>();

    protected virtual void OnEnable()
    {
        ActiveMonsters.Add(this);
    }

    protected virtual void OnDisable()
    {
        ActiveMonsters.Remove(this);
    }

    protected override void Awake()
    {
        base.Awake();
    }

    protected virtual void Start()
    {
        if (playerTarget == null && Player.Instance != null)
        {
            playerTarget = Player.Instance.transform;
        }

        var stats = GetComponent<CombatStats>();
        if (stats != null)
        {
            stats.OnHpZero.AddListener(OnDeath);
            stats.OnDeath.AddListener(OnDeath);
        }

        AiLoopAsync(this.GetCancellationTokenOnDestroy()).Forget();
    }

    protected virtual void Update()
    {
        if (playerTarget == null && Player.Instance != null)
        {
            playerTarget = Player.Instance.transform;
        }
    }

    public virtual void OnDeath()
    {
        Die();
    }

    public virtual async void Die()
    {
        SetAnimState(8);

        if (motor != null)
        {
            motor.SetTargetVelocityX(0f);
            motor.enabled = false;
        }

        var cols = GetComponentsInChildren<Collider2D>();
        foreach (var c in cols) c.enabled = false;

        ActiveMonsters.Remove(this);

        await UniTask.Delay(System.TimeSpan.FromSeconds(1.5f), cancellationToken: this.GetCancellationTokenOnDestroy());

        if (UnitPoolManager.Instance != null)
        {
            UnitPoolManager.Instance.DespawnUnit(this);
        }
        else if (gameObject != null)
        {
            if (Application.isPlaying) Destroy(gameObject);
            else DestroyImmediate(gameObject);
        }
    }

    private void LoadPatterns(MonsterBaseData mData)
    {
        Patterns.Clear();
        if (mData.PatternIdxList == null) return;

        var patternDB = DataTableManager.Instance != null ? DataTableManager.Instance.GetDB<MonsterPatternDataTable>(DataTableType.MonsterPattern) : null;
        if (patternDB == null) return;

        foreach (var pIdx in mData.PatternIdxList)
        {
            if (patternDB.TryGetPatternData(pIdx, out var pattern))
            {
                Patterns.Add(pattern);
            }
        }
    }

    private async UniTaskVoid AiLoopAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            await UniTask.Delay(500, cancellationToken: cancellationToken);

            if (stats != null && stats.IsGroggy)
            {
                await UniTask.Delay(1000, cancellationToken: cancellationToken);
                continue;
            }

            if (playerTarget == null) continue;

            if (Patterns == null || Patterns.Count == 0)
            {
                await ExecuteSimpleAiAsync(cancellationToken);
            }
            else
            {
                MonsterPatternData selectedPattern = SelectNextPattern();
                if (selectedPattern != null)
                {
                    await ExecutePatternAsync(selectedPattern, cancellationToken);
                }
                else
                {
                    await ExecuteSimpleAiAsync(cancellationToken);
                }
            }
        }
    }

    private MonsterPatternData SelectNextPattern()
    {
        float distToPlayer = Vector3.Distance(transform.position, playerTarget.position);
        float hpRatio = stats != null ? (stats.CurrentHp / stats.MaxHp) : 1.0f;

        foreach (var pattern in Patterns)
        {
            if (pattern.ExecutionType == (uint)PatternExecutionType.Trigger)
            {
                if (IsCooldown(pattern.Idx)) continue;

                bool isTriggered = ((PatternTriggerType)pattern.TriggerType) switch
                {
                    PatternTriggerType.HpRatioUnder => hpRatio <= pattern.TriggerValue,
                    PatternTriggerType.DistanceOver => distToPlayer >= pattern.TriggerValue,
                    PatternTriggerType.DistanceUnder => distToPlayer <= pattern.TriggerValue,
                    PatternTriggerType.TargetGroggy => playerTarget.GetComponent<CombatStats>()?.IsGroggy ?? false,
                    _ => false
                };

                if (isTriggered) return pattern;
            }
        }

        List<MonsterPatternData> validRandomPatterns = new List<MonsterPatternData>();
        int totalWeight = 0;
        foreach (var pattern in Patterns)
        {
            if (pattern.ExecutionType == (uint)PatternExecutionType.Random && !IsCooldown(pattern.Idx))
            {
                validRandomPatterns.Add(pattern);
                totalWeight += pattern.RandomWeight;
            }
        }

        if (validRandomPatterns.Count > 0 && totalWeight > 0)
        {
            int randVal = UnityEngine.Random.Range(0, totalWeight);
            int currentSum = 0;
            foreach (var p in validRandomPatterns)
            {
                currentSum += p.RandomWeight;
                if (randVal < currentSum) return p;
            }
        }

        for (int i = 0; i < Patterns.Count; i++)
        {
            int index = (currentSequenceIndex + i) % Patterns.Count;
            var pattern = Patterns[index];
            if (pattern.ExecutionType == (uint)PatternExecutionType.Sequence && !IsCooldown(pattern.Idx))
            {
                currentSequenceIndex = (index + 1) % Patterns.Count;
                return pattern;
            }
        }

        return null;
    }

    private bool IsCooldown(uint patternIdx)
    {
        return patternCooldowns.TryGetValue(patternIdx, out var readyTime) && Time.time < readyTime;
    }

    private async UniTask ExecutePatternAsync(MonsterPatternData pattern, CancellationToken cancellationToken)
    {
        if (playerTarget == null) return;

        bool isDistanceOverPattern = (PatternTriggerType)pattern.TriggerType == PatternTriggerType.DistanceOver;
        float attackRange = pattern.TriggerValue > 0f ? pattern.TriggerValue : 1.8f;
        float currentDist = Vector3.Distance(transform.position, playerTarget.position);

        float chaseTimeout = pattern.ChaseTimeout > 0f ? pattern.ChaseTimeout : 1.0f;
        float chaseElapsed = 0f;
        bool chaseTimedOut = false;

        if (!isDistanceOverPattern && currentDist > attackRange)
        {
            SetFacingRight(playerTarget.position.x >= transform.position.x);
            if (animator != null && animator.runtimeAnimatorController != null)
            {
                animator.SetInteger("State", 2);
            }

            float moveSpeed = (UnitData != null && UnitData.MoveSpeed > 0f) ? UnitData.MoveSpeed : 3.5f;

            while (currentDist > attackRange && !cancellationToken.IsCancellationRequested)
            {
                chaseElapsed += Time.deltaTime;
                if (chaseElapsed >= chaseTimeout)
                {
                    chaseTimedOut = true;
                    Debug.Log($"<color=yellow>[Monster] '{gameObject.name}' 패턴 '{pattern.AnimClipName}' 추격 타임아웃 ({chaseTimeout:F1}s) 발생!</color>");
                    break;
                }

                currentDist = Vector3.Distance(transform.position, playerTarget.position);
                Vector3 moveDir = (playerTarget.position - transform.position).normalized;
                SetFacingRight(moveDir.x >= 0);

                if (motor != null)
                {
                    // 메트로배니아 몬스터 AI: 장애물/지형 조우 시 자동 도약(Auto-Hop) 처리
                    if (motor.IsGrounded && ((moveDir.x > 0 && motor.IsWalledRight) || (moveDir.x < 0 && motor.IsWalledLeft)))
                    {
                        motor.SetVelocityY(7.5f);
                    }
                    motor.SetTargetVelocityX(moveDir.x * moveSpeed);
                }
                else
                {
                    transform.Translate(moveDir * moveSpeed * Time.deltaTime, Space.World);
                }
                await UniTask.Yield(PlayerLoopTiming.Update, cancellationToken);
            }

            if (motor != null)
            {
                motor.SetTargetVelocityX(0f);
            }
        }

        if (chaseTimedOut)
        {
            SetAnimState(1);
            return;
        }

        SetFacingRight(playerTarget.position.x >= transform.position.x);
        if (pattern.PreDelay > 0f)
        {
            int preMs = Mathf.RoundToInt(pattern.PreDelay * 1000f);
            await UniTask.Delay(preMs, cancellationToken: cancellationToken);
        }

        uint patternSkillId = pattern.SkillIdx > 0 ? pattern.SkillIdx : Util.CreateDataIdx(DataTableType.Skill, pattern.Idx % 1000);

        if (skillExecutor != null)
        {
            bool played = skillExecutor.TryPlaySkillAnimation(animator, patternSkillId);
            if (!played)
            {
                Debug.LogError($"[Monster Error] '{gameObject.name}' 유닛의 애니메이터에서 패턴 스킬 {patternSkillId} ({pattern.AnimClipName})의 모션/State를 찾을 수 없습니다!");
                return;
            }
        }

        if (skillExecutor != null)
        {
            Vector3 offset = (spriteRenderer != null && spriteRenderer.flipX) ? Vector3.right * 1.5f : Vector3.left * 1.5f;
            Vector3 spawnPos = transform.position + offset + Vector3.up * 1.0f;
            Color effectColor = new Color(1f, 0f, 0f, 0.4f);
            skillExecutor.SpawnSkillEffect(pattern.AnimClipName, spawnPos, new Vector2(2.0f, 2.5f), pattern.Damage, 0.2f, FactionType.Enemy, effectColor);
        }

        if (playerTarget != null)
        {
            var pStats = playerTarget.GetComponent<CombatStats>();
            if (pStats != null && Vector3.Distance(transform.position, playerTarget.position) <= (attackRange + 0.5f))
            {
                pStats.TakeDamage(pattern.Damage, isGroundAttack: false, isJumped: false, attacker: stats);
            }
        }

        if (pattern.Cooldown > 0f)
        {
            patternCooldowns[pattern.Idx] = Time.time + pattern.Cooldown;
        }

        if (pattern.PostDelay > 0f)
        {
            int postMs = Mathf.RoundToInt(pattern.PostDelay * 1000f);
            await UniTask.Delay(postMs, cancellationToken: cancellationToken);
        }

        SetAnimState(1);
    }

    protected virtual async UniTask ExecuteSimpleAiAsync(CancellationToken cancellationToken)
    {
        if (playerTarget == null) return;

        float dist = Vector3.Distance(transform.position, playerTarget.position);
        float attackRange = (MonsterData != null && MonsterData.AttackRange > 0f) ? MonsterData.AttackRange : 2.0f;
        float detectRange = (MonsterData != null && MonsterData.DetectRange > 0f) ? MonsterData.DetectRange : 6.0f;

        if (dist <= attackRange)
        {
            SetAnimState(7);
            var pStats = playerTarget.GetComponent<CombatStats>();
            if (pStats != null)
            {
                pStats.TakeDamage(10f, isGroundAttack: false, isJumped: false, attacker: stats);
            }
            await UniTask.Delay(1500, cancellationToken: cancellationToken);
        }
        else if (dist <= detectRange)
        {
            Vector3 dir = (playerTarget.position - transform.position).normalized;
            SetFacingRight(dir.x >= 0);
            float moveSpeed = (UnitData != null && UnitData.MoveSpeed > 0f) ? UnitData.MoveSpeed : 3.0f;
            if (motor != null)
            {
                motor.SetTargetVelocityX(dir.x * moveSpeed);
            }
            else
            {
                transform.Translate(dir * moveSpeed * Time.deltaTime, Space.World);
            }
            SetAnimState(2);
        }
        else
        {
            SetAnimState(1);
        }
    }

    protected void SetAnimState(int stateValue)
    {
        if (animator == null || animator.runtimeAnimatorController == null) return;
        animator.SetInteger("State", stateValue);
    }
}


