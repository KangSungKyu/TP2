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
    // 2. PROTECTED & PRIVATE FIELDS (camelCase, No '_' prefix)
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
            this.MonsterData = mData;
            this.loadPatterns(mData);
        }
    }


    // =========================================================================
    // 4. PROTECTED & PRIVATE METHODS (camelCase)
    // =========================================================================

    protected override void Awake()
    {
        base.Awake();
    }

    protected virtual void Start()
    {
        this.aiLoopAsync(this.GetCancellationTokenOnDestroy()).Forget();
    }

    protected virtual void Update()
    {
        if (this.playerTarget == null)
        {
            var player = GameObject.FindWithTag("Player");
            if (player != null) this.playerTarget = player.transform;
        }
        // 모터는 FixedUpdate에서 자체적으로 이동을 처리하므로 여기서는 아무 작업도 하지 않음.
    }

    private void loadPatterns(MonsterBaseData mData)
    {
        this.Patterns.Clear();
        if (mData.PatternIdxList == null) return;

        var patternDB = DataTableManager.Instance != null ? DataTableManager.Instance.GetDB<MonsterPatternDataTable>(DataTableType.MonsterPattern) : null;
        if (patternDB == null) return;

        foreach (var pIdx in mData.PatternIdxList)
        {
            if (patternDB.TryGetPatternData(pIdx, out var pattern))
            {
                this.Patterns.Add(pattern);
            }
        }
    }

    private async UniTaskVoid aiLoopAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            await UniTask.Delay(500, cancellationToken: cancellationToken);

            if (this.stats != null && this.stats.IsGroggy)
            {
                await UniTask.Delay(1000, cancellationToken: cancellationToken);
                continue;
            }

            if (this.playerTarget == null) continue;

            // [AI 모드 판단 및 동적 실행]
            if (this.Patterns == null || this.Patterns.Count == 0)
            {
                // [Mode A] Default Simple AI (패턴 데이터 없을 때)
                await this.executeSimpleAiAsync(cancellationToken);
            }
            else
            {
                // 패턴 데이터 존재 시: Trigger / Random / Sequence 판단
                MonsterPatternData selectedPattern = this.selectNextPattern();
                if (selectedPattern != null)
                {
                    await this.executePatternAsync(selectedPattern, cancellationToken);
                }
                else
                {
                    await this.executeSimpleAiAsync(cancellationToken);
                }
            }
        }
    }

    private MonsterPatternData selectNextPattern()
    {
        float distToPlayer = Vector3.Distance(transform.position, this.playerTarget.position);
        float hpRatio = this.stats != null ? (this.stats.CurrentHp / this.stats.MaxHp) : 1.0f;

        // 1. [Mode C-1] Trigger 조건부 패턴 우선 검색
        foreach (var pattern in this.Patterns)
        {
            if (pattern.ExecutionType == (uint)PatternExecutionType.Trigger)
            {
                if (this.isCooldown(pattern.Idx)) continue;

                bool isTriggered = ((PatternTriggerType)pattern.TriggerType) switch
                {
                    PatternTriggerType.HpRatioUnder => hpRatio <= pattern.TriggerValue,
                    PatternTriggerType.DistanceOver => distToPlayer >= pattern.TriggerValue,
                    PatternTriggerType.DistanceUnder => distToPlayer <= pattern.TriggerValue,
                    PatternTriggerType.TargetGroggy => this.playerTarget.GetComponent<CombatStats>()?.IsGroggy ?? false,
                    _ => false
                };

                if (isTriggered) return pattern;
            }
        }

        // 2. [Mode C-2] Random 가중치 선택 패턴 검색
        List<MonsterPatternData> validRandomPatterns = new List<MonsterPatternData>();
        int totalWeight = 0;
        foreach (var pattern in this.Patterns)
        {
            if (pattern.ExecutionType == (uint)PatternExecutionType.Random && !this.isCooldown(pattern.Idx))
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

        // 3. [Mode B] Sequence 순차 패턴 루프
        for (int i = 0; i < this.Patterns.Count; i++)
        {
            int index = (this.currentSequenceIndex + i) % this.Patterns.Count;
            var pattern = this.Patterns[index];
            if (pattern.ExecutionType == (uint)PatternExecutionType.Sequence && !this.isCooldown(pattern.Idx))
            {
                this.currentSequenceIndex = (index + 1) % this.Patterns.Count;
                return pattern;
            }
        }

        return null;
    }

    private bool isCooldown(uint patternIdx)
    {
        return this.patternCooldowns.TryGetValue(patternIdx, out var readyTime) && Time.time < readyTime;
    }

    private async UniTask executePatternAsync(MonsterPatternData pattern, CancellationToken cancellationToken)
    {
        if (this.playerTarget == null) return;

        // 1. 공격 사거리(attackRange) 판단
        bool isDistanceOverPattern = (PatternTriggerType)pattern.TriggerType == PatternTriggerType.DistanceOver;
        float attackRange = pattern.TriggerValue > 0f ? pattern.TriggerValue : 1.8f;
        float currentDist = Vector3.Distance(transform.position, this.playerTarget.position);

        // 2. CSV 데이터 기반 추격 제한 시간 (ChaseTimeout) 참조
        float chaseTimeout = pattern.ChaseTimeout > 0f ? pattern.ChaseTimeout : 1.0f;

        float chaseElapsed = 0f;
        bool chaseTimedOut = false;

        // 3. 사거리 밖이고 원거리 시전 패턴이 아닐 경우 ➔ 타겟을 향해 추적 이동 (State = Move)
        if (!isDistanceOverPattern && currentDist > attackRange)
        {
            this.SetFacingRight(this.playerTarget.position.x >= transform.position.x);
            if (this.animator != null)
            {
                this.animator.SetInteger("State", 2);
            }

            float moveSpeed = (this.UnitData != null && this.UnitData.MoveSpeed > 0f) ? this.UnitData.MoveSpeed : 3.5f;

            while (currentDist > attackRange && !cancellationToken.IsCancellationRequested)
            {
                chaseElapsed += Time.deltaTime;
                if (chaseElapsed >= chaseTimeout)
                {
                    chaseTimedOut = true;
                    Debug.Log($"<color=yellow>[Monster] '{this.gameObject.name}' 패턴 '{pattern.AnimClipName}' 추격 타임아웃 ({chaseTimeout:F1}s) 발생! 추격 중단!</color>");
                    break;
                }

                currentDist = Vector3.Distance(transform.position, this.playerTarget.position);
                Vector3 moveDir = (this.playerTarget.position - transform.position).normalized;
                this.SetFacingRight(moveDir.x >= 0);

                if (this.motor != null)
                {
                    // 수평 추격은 모터의 target 속도로 적용
                    this.motor.SetTargetVelocityX(moveDir.x * moveSpeed);
                }
                else
                {
                    transform.Translate(moveDir * moveSpeed * Time.deltaTime, Space.World);
                }
                await UniTask.Yield(PlayerLoopTiming.Update, cancellationToken);
            }

            // 추적 종료 시 모터 속도 정지
            if (this.motor != null)
            {
                this.motor.SetTargetVelocityX(0f);
            }
        }

        // 4. 추격 타임아웃 발생 시 ➔ 공격 포기 후 Idle 복귀 (State = 1)
        if (chaseTimedOut)
        {
            this.SetAnimState(1);
            return;
        }

        // 5. 사거리 진입 시 ➔ 타겟 바라보기 및 전조시간 (PreDelay) 대기
        this.SetFacingRight(this.playerTarget.position.x >= transform.position.x);
        if (pattern.PreDelay > 0f)
        {
            int preMs = Mathf.RoundToInt(pattern.PreDelay * 1000f);
            await UniTask.Delay(preMs, cancellationToken: cancellationToken);
        }

        // 6. CSV 데이터 기반 연동 SkillData Idx 참조 및 AnimState (int) 파라미터 검사 시전
        uint patternSkillId = pattern.SkillIdx > 0 ? pattern.SkillIdx : Util.CreateDataIdx(DataTableType.Skill, pattern.Idx % 1000);



        if (this.skillExecutor != null)
        {
            bool played = this.skillExecutor.TryPlaySkillAnimation(this.animator, patternSkillId);
            if (!played)
            {
                Debug.LogError($"[Monster Error] '{gameObject.name}' 유닛의 애니메이터에서 패턴 스킬 {patternSkillId} ({pattern.AnimClipName})의 모션/State를 찾을 수 없습니다!");
                return;
            }
        }


        if (this.skillExecutor != null)
        {
            Vector3 offset = (this.spriteRenderer != null && this.spriteRenderer.flipX) ? Vector3.right * 1.5f : Vector3.left * 1.5f;
            Vector3 spawnPos = transform.position + offset + Vector3.up * 1.0f;
            Color effectColor = new Color(1f, 0f, 0f, 0.4f); // 붉은 반투명 더미 이펙트
            this.skillExecutor.SpawnSkillEffect(pattern.AnimClipName, spawnPos, new Vector2(2.0f, 2.5f), pattern.Damage, 0.2f, FactionType.Enemy, effectColor);
        }

        // 7. 데미지 판정
        if (this.playerTarget != null)
        {
            var pStats = this.playerTarget.GetComponent<CombatStats>();
            if (pStats != null && Vector3.Distance(transform.position, this.playerTarget.position) <= (attackRange + 0.5f))
            {
                pStats.TakeDamage(pattern.Damage, isGroundAttack: false, isJumped: false, attacker: this.stats);
            }
        }

        // 8. 쿨다운 및 후딜레이 (PostDelay) 대기 후 Idle 복귀 (State = 1)
        if (pattern.Cooldown > 0f)
        {
            this.patternCooldowns[pattern.Idx] = Time.time + pattern.Cooldown;
        }

        if (pattern.PostDelay > 0f)
        {
            int postMs = Mathf.RoundToInt(pattern.PostDelay * 1000f);
            await UniTask.Delay(postMs, cancellationToken: cancellationToken);
        }

        this.SetAnimState(1); // Idle 복귀
    }


    protected virtual async UniTask executeSimpleAiAsync(CancellationToken cancellationToken)
    {
        if (this.playerTarget == null) return;

        float dist = Vector3.Distance(transform.position, this.playerTarget.position);
        float attackRange = (this.MonsterData != null && this.MonsterData.AttackRange > 0f) ? this.MonsterData.AttackRange : 2.0f;
        float detectRange = (this.MonsterData != null && this.MonsterData.DetectRange > 0f) ? this.MonsterData.DetectRange : 6.0f;

        if (dist <= attackRange)
        {
            // 기본 공격 (State = 7)
            this.SetAnimState(7);
            var pStats = this.playerTarget.GetComponent<CombatStats>();
            if (pStats != null)
            {
                pStats.TakeDamage(10f, isGroundAttack: false, isJumped: false, attacker: this.stats);
            }
            await UniTask.Delay(1500, cancellationToken: cancellationToken);
        }
        else if (dist <= detectRange)
        {
            // 추적 이동 (State = 2)
            Vector3 dir = (this.playerTarget.position - transform.position).normalized;
            this.SetFacingRight(dir.x >= 0);
            float moveSpeed = (this.UnitData != null && this.UnitData.MoveSpeed > 0f) ? this.UnitData.MoveSpeed : 3.0f;
            if (this.motor != null)
            {
                this.motor.SetTargetVelocityX(dir.x * moveSpeed);
            }
            else
            {
                transform.Translate(dir * moveSpeed * Time.deltaTime, Space.World);
            }
            this.SetAnimState(2);
        }
        else
        {
            // 대기 (State = 1)
            this.SetAnimState(1);
        }
    }

    /// <summary>
    /// Animator의 int 파라미터 'State'를 변경하여 트랜지션을 제어합니다.
    /// (1: Idle, 2: Move, 3: Jump, 4~6: Pattern, 7: Attack, 8: Death, 9: Groggy)
    /// </summary>
    protected void SetAnimState(int stateValue)
    {
        if (this.animator != null)
        {
            this.animator.SetInteger("State", stateValue);
        }
    }
}


