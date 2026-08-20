using Cysharp.Threading.Tasks;
using System.Collections.Generic;
using System;
using System.Threading;
using UnityEngine;

/// <summary>
/// 몬스터 유닛 클래스 (UnitBase 상속).
/// MonsterBaseData 및 MonsterPatternData 기반으로 3가지 AI 모드
/// [Default Simple AI, Sequence Pattern AI, Trigger/Random Pattern AI]를 완벽히 구동합니다.
/// </summary>
public class Monster : UnitBase
{
    public enum LeashState { Idle, Combat, Returning }
    public readonly struct AttackTelegraph
    {
        public readonly Monster Source;
        public readonly uint Generation;
        public readonly float WarningStartsAt;
        public readonly float ImpactAt;
        public readonly float ActiveEndsAt;

        public AttackTelegraph(Monster source, uint generation, float warningStartsAt,
            float impactAt, float activeEndsAt)
        {
            Source = source;
            Generation = generation;
            WarningStartsAt = warningStartsAt;
            ImpactAt = impactAt;
            ActiveEndsAt = activeEndsAt;
        }
    }

    public const float AttackTelegraphLeadSeconds = 1.5f;
    private const float FallbackAttackEffectDuration = 0.2f;
    public static event Action<AttackTelegraph> AttackTelegraphStarted;
    public static event Action<Monster, uint> AttackTelegraphEnded;
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
    private bool deathSequenceActive;
    private Collider2D[] deathColliders;
    private const int MaximumAttackTokens = 2;
    private static int activeAttackTokens;
    private bool holdsAttackToken;
    private uint actionGeneration;
    private uint telegraphGeneration;
    private bool telegraphActive;
    private Vector3 spawnOrigin;
    private Bounds movementBounds;
    private bool hasSpawnArea;
    private bool arenaOnly;
    private bool returnTeleported;
    private float returnStartedAt;
    private uint spawnRoomGeneration;
    private uint spawnZoneGeneration;
    public static int ActiveAttackTokens => activeAttackTokens;
    public override uint ActionGeneration => actionGeneration;
    public LeashState CurrentLeashState { get; private set; }
    public Vector3 SpawnOrigin => spawnOrigin;


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
        if (this != null && isActiveAndEnabled) Activated?.Invoke(this);
    }

    public void SetHorizontalMovementBounds(Bounds bounds)
    {
        if (motor != null) motor.SetHorizontalMovementBounds(bounds);
    }

    public bool ConfigureSpawnArea(Vector3 origin, Bounds bounds, bool bossArena)
    {
        if (!bounds.Contains(origin)) return false;
        spawnOrigin = origin;
        movementBounds = bounds;
        hasSpawnArea = true;
        arenaOnly = bossArena;
        returnTeleported = false;
        CurrentLeashState = LeashState.Idle;
        StageManager stage = StageManager.Instance;
        spawnRoomGeneration = stage != null ? stage.RoomGeneration : 0u;
        spawnZoneGeneration = stage != null ? stage.ZoneGeneration : 0u;
        SetHorizontalMovementBounds(bounds);
        return true;
    }


    // =========================================================================
    // 4. PROTECTED & PRIVATE METHODS
    // =========================================================================

    public static HashSet<Monster> ActiveMonsters { get; } = new HashSet<Monster>();
    public static event Action<Monster> Activated;
    public static event Action<Monster> Deactivated;

    protected virtual void OnEnable()
    {
        ActiveMonsters.Add(this);
        Activated?.Invoke(this);
    }

    protected virtual void OnDisable()
    {
        CancelAttackHitbox();
        EndAttackTelegraph(actionGeneration);
        actionGeneration++;
        if (UnitPoolManager.Instance != null) UnitPoolManager.Instance.DespawnProjectilesOwnedBy(this);
        ReleaseAttackToken();
        ActiveMonsters.Remove(this);
        Deactivated?.Invoke(this);
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
            stats.OnDeath.AddListener(OnDeath);
            stats.OnGroggyState.AddListener(OnGroggyStarted);
            stats.OnGroggyEnded.AddListener(OnGroggyEnded);
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

    protected virtual void FixedUpdate()
    {
        if (!hasSpawnArea || deathSequenceActive || stats == null || stats.IsDead) return;
        StageManager stage = StageManager.Instance;
        Player player = Player.Instance;
        bool lifecycleChanged = stage != null &&
            (stage.RoomGeneration != spawnRoomGeneration || stage.ZoneGeneration != spawnZoneGeneration);
        bool playerUnavailable = player == null || !player.isActiveAndEnabled || player.Stats == null ||
            player.Stats.IsDead || !movementBounds.Contains(player.transform.position);
        bool outsideSpawnArea = !movementBounds.Contains(transform.position);
        if (outsideSpawnArea || CurrentLeashState == LeashState.Combat && (lifecycleChanged || playerUnavailable))
            BeginReturn();
        if (CurrentLeashState == LeashState.Returning) UpdateReturn();
    }

    public virtual void OnDeath()
    {
        Die();
    }

    public virtual async void Die()
    {
        if (deathSequenceActive) return;
        deathSequenceActive = true;
        CancelAttackHitbox();
        EndAttackTelegraph(actionGeneration);
        actionGeneration++;
        if (UnitPoolManager.Instance != null) UnitPoolManager.Instance.DespawnProjectilesOwnedBy(this);
        ReleaseAttackToken();
        if (skillExecutor != null) skillExecutor.CancelActiveEffects();
        SetAnimState(8);

        if (motor != null)
        {
            motor.ApplyKnockback(Vector2.zero);
            motor.enabled = false;
        }

        deathColliders = GetComponentsInChildren<Collider2D>();
        foreach (var col in deathColliders) col.enabled = false;

        ActiveMonsters.Remove(this);

        const float fadeDuration = 1.5f;
        float fadeStartedAt = Time.realtimeSinceStartup;
        float elapsed = 0f;
        Color startColor = spriteRenderer != null ? spriteRenderer.color : Color.white;
        while (elapsed < fadeDuration)
        {
            elapsed = Time.realtimeSinceStartup - fadeStartedAt;
            if (spriteRenderer != null)
            {
                Color color = startColor;
                color.a = Mathf.Lerp(startColor.a, 0f, elapsed / fadeDuration);
                spriteRenderer.color = color;
            }
            await UniTask.Yield(PlayerLoopTiming.Update, this.GetCancellationTokenOnDestroy());
        }

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

    public void ResetAfterDeath(Vector3 position)
    {
        actionGeneration++;
        CloseAttackHitbox();
        deathSequenceActive = false;
        hasSpawnArea = false;
        CurrentLeashState = LeashState.Idle;
        if (spriteRenderer != null)
        {
            Color color = spriteRenderer.color;
            color.a = 1f;
            spriteRenderer.color = color;
        }
        if (deathColliders != null)
        {
            foreach (var col in deathColliders) if (col != null) col.enabled = true;
        }
        if (motor != null)
        {
            motor.enabled = true;
            motor.Teleport(position);
            motor.SetTargetVelocityX(0f);
            motor.SetVelocityY(0f);
        }
        else
        {
            transform.position = position;
        }
    }

    public override bool IsActionGenerationCurrent(uint generation)
    {
        return generation == actionGeneration && isActiveAndEnabled && !deathSequenceActive;
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

            if (!CanAct(actionGeneration))
            {
                await UniTask.Delay(1000, cancellationToken: cancellationToken);
                continue;
            }

            if (playerTarget == null) continue;

            if (hasSpawnArea)
            {
                Player player = Player.Instance;
                if (CurrentLeashState == LeashState.Returning || player == null ||
                    !player.isActiveAndEnabled || player.Stats == null || player.Stats.IsDead ||
                    !movementBounds.Contains(player.transform.position)) continue;
                CurrentLeashState = LeashState.Combat;
            }

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
        uint generation = actionGeneration;
        if (playerTarget == null || !CanAct(generation)) return;

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

            while (currentDist > attackRange && !cancellationToken.IsCancellationRequested && CanAct(generation))
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

        if (!CanAct(generation)) return;
        if (chaseTimedOut)
        {
            SetAnimState(1);
            return;
        }

        if (!TryAcquireAttackToken()) return;
        try
        {
        SetFacingRight(playerTarget.position.x >= transform.position.x);
        uint patternSkillId = pattern.SkillIdx > 0 ? pattern.SkillIdx : Util.CreateDataIdx(DataTableType.Skill, pattern.Idx % 1000);
        GetAttackTiming(patternSkillId, pattern.ProjectileResourceIdx != 0,
            out float firstHitTiming, out float activeDuration);
        float effectivePreDelay = CalculateEffectivePreDelay(pattern.PreDelay, firstHitTiming);
        float attackSequenceStartedAt = Time.time;
        float impactAt = attackSequenceStartedAt + effectivePreDelay + firstHitTiming;
        BeginAttackTelegraph(generation, impactAt - AttackTelegraphLeadSeconds,
            impactAt, attackSequenceStartedAt + effectivePreDelay + activeDuration);

        if (effectivePreDelay > 0f)
        {
            int preMs = Mathf.RoundToInt(effectivePreDelay * 1000f);
            await UniTask.Delay(preMs, cancellationToken: cancellationToken);
            if (!CanAct(generation)) return;
        }

        bool timedAttack = false;

        if (skillExecutor != null && CanAct(generation))
        {
            bool played = skillExecutor.TryPlaySkillAnimation(animator, patternSkillId);
            if (!played)
            {
                Debug.LogError($"[Monster Error] '{gameObject.name}' 유닛의 애니메이터에서 패턴 스킬 {patternSkillId} ({pattern.AnimClipName})의 모션/State를 찾을 수 없습니다!");
                return;
            }
        }

        Vector3 offset = (spriteRenderer != null && spriteRenderer.flipX) ? Vector3.right * 1.5f : Vector3.left * 1.5f;
        Vector3 spawnPos = transform.position + offset + Vector3.up * 1.0f;
        if (pattern.ProjectileResourceIdx != 0)
        {
            if (pattern.ProjectileSpeed <= 0f || pattern.ProjectileMaxDistance <= 0f)
            {
                Debug.LogError($"[Monster] Pattern idx {pattern.Idx} has invalid projectile speed/distance.");
                return;
            }
            if (UnitPoolManager.Instance == null) return;
            Vector2 direction = playerTarget != null
                ? ((Vector2)playerTarget.position - (Vector2)spawnPos).normalized
                : (spriteRenderer != null && spriteRenderer.flipX ? Vector2.right : Vector2.left);
            await UnitPoolManager.Instance.SpawnMonsterProjectileAsync(
                pattern.ProjectileResourceIdx, this, generation, spawnPos, direction,
                pattern.ProjectileSpeed, pattern.ProjectileMaxDistance, pattern.Damage);
            if (!CanAct(generation)) return;
            await UniTask.Yield(PlayerLoopTiming.Update, cancellationToken);
        }
        else if (skillExecutor != null)
        {
            skillExecutor.SpawnSkillEffectFromDataAsync(patternSkillId, spawnPos).Forget();
            timedAttack = await skillExecutor.ExecuteSkillHitsAsync(
                patternSkillId, this, playerTarget != null ? playerTarget.GetComponent<UnitBase>() : null,
                pattern.Damage, cancellationToken);
            if (timedAttack && activeDuration > 0f)
            {
                float remainingWindow = attackSequenceStartedAt + effectivePreDelay + activeDuration - Time.time;
                if (remainingWindow > 0f)
                    await UniTask.Delay(Mathf.RoundToInt(remainingWindow * 1000f), cancellationToken: cancellationToken);
            }
        }

        if (!timedAttack && pattern.ProjectileResourceIdx == 0)
            Debug.LogError($"[Monster] Unit idx {UnitIdx} has no valid attack hitbox window; attack cancelled.");

        EndAttackTelegraph(generation);

        if (pattern.Cooldown > 0f)
        {
            patternCooldowns[pattern.Idx] = Time.time + pattern.Cooldown;
        }

        if (pattern.PostDelay > 0f)
        {
            int postMs = Mathf.RoundToInt(pattern.PostDelay * 1000f);
            await UniTask.Delay(postMs, cancellationToken: cancellationToken);
            if (!CanAct(generation)) return;
        }

        SetAnimState(1);
        }
        finally
        {
            EndAttackTelegraph(generation);
            ReleaseAttackToken();
        }
    }

    protected virtual async UniTask ExecuteSimpleAiAsync(CancellationToken cancellationToken)
    {
        uint generation = actionGeneration;
        if (playerTarget == null || !CanAct(generation)) return;

        float dist = Vector3.Distance(transform.position, playerTarget.position);
        float attackRange = (MonsterData != null && MonsterData.AttackRange > 0f) ? MonsterData.AttackRange : 2.0f;
        float detectRange = (MonsterData != null && MonsterData.DetectRange > 0f) ? MonsterData.DetectRange : 6.0f;

        if (dist <= attackRange)
        {
            if (!TryAcquireAttackToken()) return;
            try
            {
            SetAnimState(7);
            float impactAt = Time.time + AttackTelegraphLeadSeconds;
            BeginAttackTelegraph(generation, Time.time, impactAt, impactAt + Time.fixedDeltaTime);
            await UniTask.Delay(Mathf.RoundToInt(AttackTelegraphLeadSeconds * 1000f),
                cancellationToken: cancellationToken);
            if (!CanAct(generation)) return;
            if (skillExecutor == null)
                Debug.LogError($"[Monster] Unit idx {UnitIdx} has no SkillExecutor; attack cancelled.");
            else
                await skillExecutor.ExecuteSkillHitsAsync(
                    Util.CreateDataIdx(DataTableType.Skill, 1), this,
                    playerTarget != null ? playerTarget.GetComponent<UnitBase>() : null, 10f, cancellationToken);
            EndAttackTelegraph(generation);
            await UniTask.Delay(1500, cancellationToken: cancellationToken);
            }
            finally
            {
                EndAttackTelegraph(generation);
                ReleaseAttackToken();
            }
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

    private bool TryAcquireAttackToken()
    {
        if (!CanAct(actionGeneration)) return false;
        if (holdsAttackToken) return true;
        if (activeAttackTokens >= MaximumAttackTokens) return false;
        activeAttackTokens++;
        holdsAttackToken = true;
        return true;
    }

    private void ReleaseAttackToken()
    {
        if (!holdsAttackToken) return;
        holdsAttackToken = false;
        activeAttackTokens = Mathf.Max(0, activeAttackTokens - 1);
    }

    private bool CanAct(uint generation) => generation == actionGeneration && !deathSequenceActive &&
        CurrentLeashState != LeashState.Returning && stats != null && !stats.IsDead &&
        !stats.IsGroggy && isActiveAndEnabled;

    private void BeginReturn()
    {
        if (CurrentLeashState == LeashState.Returning) return;
        CancelAttackHitbox();
        EndAttackTelegraph(actionGeneration);
        actionGeneration++;
        ReleaseAttackToken();
        if (skillExecutor != null) skillExecutor.CancelActiveEffects();
        if (UnitPoolManager.Instance != null) UnitPoolManager.Instance.DespawnProjectilesOwnedBy(this);
        if (motor != null)
        {
            motor.ApplyKnockback(Vector2.zero);
            motor.SetTargetVelocityX(0f);
        }
        CurrentLeashState = LeashState.Returning;
        returnTeleported = false;
        returnStartedAt = Time.time;
        SetAnimState(1);
    }

    private void UpdateReturn()
    {
        if (!hasSpawnArea || !movementBounds.Contains(spawnOrigin))
        {
            if (UnitPoolManager.Instance != null) UnitPoolManager.Instance.DespawnUnit(this);
            else gameObject.SetActive(false);
            return;
        }
        float moveSpeed = UnitData != null && UnitData.MoveSpeed > 0f ? UnitData.MoveSpeed : 0f;
        float stopDistance = (motor != null ? motor.SkinWidth : 0f) + moveSpeed * Time.fixedDeltaTime;
        float deltaX = spawnOrigin.x - transform.position.x;
        bool reached = Mathf.Abs(deltaX) <= stopDistance &&
            Mathf.Abs(spawnOrigin.y - transform.position.y) <= stopDistance;
        bool blocked = motor == null || !movementBounds.Contains(transform.position) ||
            (deltaX < 0f && motor.IsWalledLeft) || (deltaX > 0f && motor.IsWalledRight);
        float returnTimeout = moveSpeed > 0f ? movementBounds.size.x / moveSpeed : 0f;
        if (!reached && (blocked || returnTimeout <= 0f || Time.time - returnStartedAt >= returnTimeout) && !returnTeleported)
        {
            motor?.Teleport(spawnOrigin);
            returnTeleported = true;
            reached = true;
        }
        if (!reached)
        {
            motor.SetTargetVelocityX(Mathf.Sign(deltaX) * moveSpeed);
            SetFacingRight(deltaX >= 0f);
            SetAnimState(2);
            return;
        }
        motor?.SetTargetVelocityX(0f);
        if (!arenaOnly) stats.InitStats();
        CurrentLeashState = LeashState.Idle;
        returnTeleported = false;
        SetAnimState(1);
    }

    private void OnGroggyStarted()
    {
        CancelAttackHitbox();
        EndAttackTelegraph(actionGeneration);
        actionGeneration++;
        ReleaseAttackToken();
        if (skillExecutor != null) skillExecutor.CancelActiveEffects();
        if (motor != null)
        {
            motor.ApplyKnockback(Vector2.zero);
            motor.SetTargetVelocityX(0f);
        }
        SetAnimState(1);
    }

    private void OnGroggyEnded()
    {
        if (!deathSequenceActive) SetAnimState(1);
    }

    protected void SetAnimState(int stateValue)
    {
        if (animator == null || animator.runtimeAnimatorController == null) return;
        animator.SetInteger("State", stateValue);
    }

    public static float CalculateEffectivePreDelay(float configuredPreDelay, float firstHitTiming) =>
        Mathf.Max(Mathf.Max(0f, configuredPreDelay), AttackTelegraphLeadSeconds - Mathf.Max(0f, firstHitTiming));

    private static void GetAttackTiming(uint skillId, bool projectile, out float firstHitTiming,
        out float activeDuration)
    {
        firstHitTiming = 0f;
        activeDuration = projectile ? Time.fixedDeltaTime : FallbackAttackEffectDuration;
        if (projectile) return;

        SkillDataTable table = DataTableManager.Instance != null
            ? DataTableManager.Instance.GetDB<SkillDataTable>(DataTableType.Skill) : null;
        if (table == null || !table.TryGetSkillData(skillId, out SkillData skill) ||
            skill.HitTimings == null || skill.HitTimings.Length == 0) return;

        firstHitTiming = Mathf.Max(0f, skill.HitTimings[0]);
        float lastHitTiming = Mathf.Max(0f, skill.HitTimings[skill.HitTimings.Length - 1]);
        activeDuration = lastHitTiming + skill.HitWindowPost;
    }

    private void BeginAttackTelegraph(uint generation, float warningStartsAt, float impactAt,
        float activeEndsAt)
    {
        if (!CanAct(generation)) return;
        telegraphGeneration = generation;
        telegraphActive = true;
        AttackTelegraphStarted?.Invoke(new AttackTelegraph(this, generation, warningStartsAt,
            impactAt, Mathf.Max(impactAt, activeEndsAt)));
    }

    private void EndAttackTelegraph(uint generation)
    {
        if (!telegraphActive || telegraphGeneration != generation) return;
        telegraphActive = false;
        AttackTelegraphEnded?.Invoke(this, generation);
    }
}


