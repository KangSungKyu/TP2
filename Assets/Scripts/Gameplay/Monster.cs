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

    public override async void InitUnit(uint unitIdx)
    {
        base.InitUnit(unitIdx);

        if (DataTableManager.Instance != null)
        {
            await DataTableManager.Instance.EnsureDataLoadedAsync();
        }

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
        // 전조시간 (PreDelay)
        if (pattern.PreDelay > 0f)
        {
            int preMs = Mathf.RoundToInt(pattern.PreDelay * 1000f);
            await UniTask.Delay(preMs, cancellationToken: cancellationToken);
        }

        // 타겟 바라보기
        if (this.playerTarget != null)
        {
            this.SetFacingRight(this.playerTarget.position.x >= transform.position.x);
        }

        // 애니메이션 재생
        if (this.animator != null && !string.IsNullOrEmpty(pattern.AnimClipName))
        {
            this.animator.Play(pattern.AnimClipName);
        }

        // 데미지 판정
        if (this.playerTarget != null)
        {
            var pStats = this.playerTarget.GetComponent<CombatStats>();
            if (pStats != null && Vector3.Distance(transform.position, this.playerTarget.position) <= pattern.Damage)
            {
                pStats.TakeDamage(pattern.Damage, isGroundAttack: false, isJumped: false, attacker: this.stats);
            }
        }

        // 쿨다운 등록
        if (pattern.Cooldown > 0f)
        {
            this.patternCooldowns[pattern.Idx] = Time.time + pattern.Cooldown;
        }

        // 후딜레이 (PostDelay)
        if (pattern.PostDelay > 0f)
        {
            int postMs = Mathf.RoundToInt(pattern.PostDelay * 1000f);
            await UniTask.Delay(postMs, cancellationToken: cancellationToken);
        }
    }

    protected virtual async UniTask executeSimpleAiAsync(CancellationToken cancellationToken)
    {
        if (this.playerTarget == null) return;

        float dist = Vector3.Distance(transform.position, this.playerTarget.position);
        float attackRange = (this.MonsterData != null && this.MonsterData.AttackRange > 0f) ? this.MonsterData.AttackRange : 2.0f;
        float detectRange = (this.MonsterData != null && this.MonsterData.DetectRange > 0f) ? this.MonsterData.DetectRange : 6.0f;

        if (dist <= attackRange)
        {
            // 기본 공격
            if (this.animator != null) this.animator.Play("Attack");
            var pStats = this.playerTarget.GetComponent<CombatStats>();
            if (pStats != null)
            {
                pStats.TakeDamage(10f, isGroundAttack: false, isJumped: false, attacker: this.stats);
            }
            await UniTask.Delay(1500, cancellationToken: cancellationToken);
        }
        else if (dist <= detectRange)
        {
            // 추적 이동
            Vector3 dir = (this.playerTarget.position - transform.position).normalized;
            this.SetFacingRight(dir.x >= 0);
            float moveSpeed = (this.UnitData != null && this.UnitData.MoveSpeed > 0f) ? this.UnitData.MoveSpeed : 3.0f;
            transform.Translate(dir * moveSpeed * Time.deltaTime, Space.World);
            if (this.animator != null) this.animator.Play("Move");
        }
    }
}
