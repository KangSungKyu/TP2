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
    public readonly struct PatternSnapshot
    {
        public readonly PatternState State;
        public readonly uint PatternIdx;
        public readonly uint SkillIdx;
        public readonly uint Generation;
        public readonly float Elapsed;
        public readonly bool TokenHeld;

        public PatternSnapshot(PatternState state, uint patternIdx, uint skillIdx, uint generation,
            float elapsed, bool tokenHeld)
        {
            State = state;
            PatternIdx = patternIdx;
            SkillIdx = skillIdx;
            Generation = generation;
            Elapsed = elapsed;
            TokenHeld = tokenHeld;
        }
    }
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
    private readonly List<MonsterPatternData> randomPatternCandidates = new List<MonsterPatternData>(16);
    private CancellationTokenSource patternCancellation;
    private MonsterPatternData currentPattern;
    private float patternStartedAt;
    private bool patternCleanupActive;
    private bool patternCooldownCommitted;
    private int selectionBlockedFrame = -1;
    private bool missingDistanceColliderWarned;
    private uint failedReservationPatternIdx;
    private bool skipFailedReservationOnce;
    private int pendingSequenceIndex = -1;
    public static int ActiveAttackTokens => activeAttackTokens;
    public override uint ActionGeneration => actionGeneration;
    public LeashState CurrentLeashState { get; private set; }
    public Vector3 SpawnOrigin => spawnOrigin;
    public PatternState CurrentPatternState { get; private set; }
    public bool SupportsPatternQueue => false;
    public PatternSnapshot CurrentPatternSnapshot => new PatternSnapshot(CurrentPatternState,
        currentPattern != null ? currentPattern.Idx : 0u, currentPattern != null ? currentPattern.SkillIdx : 0u,
        actionGeneration, currentPattern != null ? Mathf.Max(0f, Time.time - patternStartedAt) : 0f,
        holdsAttackToken);


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

    public void EnqueuePattern(uint _) => throw new NotSupportedException(
        "Monster pattern queue is not supported; the serial AI scheduler owns pattern execution.");


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
        CancelCurrentPattern(PatternCancelReason.Disabled);
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
        CancelCurrentPattern(PatternCancelReason.Death);
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
        patternCancellation?.Dispose();
        patternCancellation = null;
        currentPattern = null;
        CurrentPatternState = PatternState.Idle;
        patternCleanupActive = false;
        pendingSequenceIndex = -1;
        failedReservationPatternIdx = 0u;
        skipFailedReservationOnce = false;
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
        if (mData.PatternIdxList == null || mData.PatternIdxList.Length < 1 || mData.PatternIdxList.Length > 16)
        {
            Debug.LogError($"[Monster] Unit {UnitIdx} PatternIdxList count must be 1..16.");
            return;
        }

        var patternDB = DataTableManager.Instance != null ? DataTableManager.Instance.GetDB<MonsterPatternDataTable>(DataTableType.MonsterPattern) : null;
        var skillDB = DataTableManager.Instance != null ? DataTableManager.Instance.GetDB<SkillDataTable>(DataTableType.Skill) : null;
        if (patternDB == null || skillDB == null) return;

        var unique = new HashSet<uint>();
        foreach (var pIdx in mData.PatternIdxList)
        {
            if (!unique.Add(pIdx) || !patternDB.TryGetPatternData(pIdx, out var pattern) ||
                pattern.SkillIdx == 0 || !skillDB.TryGetSkillData(pattern.SkillIdx, out _))
            {
                Patterns.Clear();
                Debug.LogError($"[Monster] Unit {UnitIdx} rejected invalid/duplicate Pattern FK {pIdx}.");
                return;
            }
            Patterns.Add(pattern);
        }
    }

    private async UniTaskVoid AiLoopAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            await UniTask.Delay(100, cancellationToken: cancellationToken);

            if (!CanAct(actionGeneration))
            {
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
                await ExecuteMovementAiAsync(cancellationToken);
            }
            else
            {
                MonsterPatternData selectedPattern = selectionBlockedFrame == Time.frameCount ? null : SelectNextPattern();
                if (selectedPattern != null)
                {
                    await ExecutePatternAsync(selectedPattern, cancellationToken);
                }
                else
                {
                    await ExecuteMovementAiAsync(cancellationToken);
                }
            }
        }
    }

    private MonsterPatternData SelectNextPattern()
    {
        uint excludedPatternIdx = skipFailedReservationOnce ? failedReservationPatternIdx : 0u;
        skipFailedReservationOnce = false;
        pendingSequenceIndex = -1;
        return SelectNextPattern(requireCurrentBand: true, excludedPatternIdx) ??
            SelectNextPattern(requireCurrentBand: false, excludedPatternIdx);
    }

    private MonsterPatternData SelectNextPattern(bool requireCurrentBand, uint excludedPatternIdx)
    {
        float distToPlayer = GetDetectionDistance();
        float hpRatio = stats != null ? (stats.CurrentHp / stats.MaxHp) : 1.0f;
        SkillDataTable skillTable = DataTableManager.Instance != null
            ? DataTableManager.Instance.GetDB<SkillDataTable>(DataTableType.Skill) : null;

        foreach (var pattern in Patterns)
        {
            if (pattern.Idx == excludedPatternIdx) continue;
            if (pattern.ExecutionType == (uint)PatternExecutionType.Trigger)
            {
                if (IsCooldown(pattern.Idx) || !CanSelectPattern(pattern, skillTable, requireCurrentBand) || !HasValidTriggerSubject(pattern)) continue;

                bool isTriggered = ((PatternTriggerType)pattern.TriggerType) switch
                {
                    PatternTriggerType.HpRatioUnder => hpRatio <= pattern.TriggerValue,
                    PatternTriggerType.DistanceOver => distToPlayer >= pattern.TriggerValue,
                    PatternTriggerType.DistanceUnder => distToPlayer <= pattern.TriggerValue,
                    PatternTriggerType.TargetGroggy => Player.Instance != null && Player.Instance.Stats != null && Player.Instance.Stats.IsGroggy,
                    _ => false
                };

                if (isTriggered) return pattern;
            }
        }

        randomPatternCandidates.Clear();
        int totalWeight = 0;
        foreach (var pattern in Patterns)
        {
            if (pattern.Idx == excludedPatternIdx) continue;
            if (pattern.ExecutionType == (uint)PatternExecutionType.Random && !IsCooldown(pattern.Idx) &&
                CanSelectPattern(pattern, skillTable, requireCurrentBand))
            {
                randomPatternCandidates.Add(pattern);
                totalWeight += pattern.RandomWeight;
            }
        }

        if (randomPatternCandidates.Count > 0 && totalWeight > 0)
        {
            int randVal = UnityEngine.Random.Range(0, totalWeight);
            int currentSum = 0;
            foreach (var p in randomPatternCandidates)
            {
                currentSum += p.RandomWeight;
                if (randVal < currentSum) return p;
            }
        }

        for (int i = 0; i < Patterns.Count; i++)
        {
            int index = (currentSequenceIndex + i) % Patterns.Count;
            var pattern = Patterns[index];
            if (pattern.Idx == excludedPatternIdx) continue;
            if (pattern.ExecutionType == (uint)PatternExecutionType.Sequence && !IsCooldown(pattern.Idx) &&
                CanSelectPattern(pattern, skillTable, requireCurrentBand))
            {
                pendingSequenceIndex = (index + 1) % Patterns.Count;
                return pattern;
            }
        }

        foreach (var pattern in Patterns)
        {
            if (pattern.Idx == excludedPatternIdx) continue;
            if (pattern.ExecutionType == (uint)PatternExecutionType.Simple && !IsCooldown(pattern.Idx) &&
                CanSelectPattern(pattern, skillTable, requireCurrentBand))
                return pattern;
        }

        return null;
    }

    private static bool HasValidSkill(MonsterPatternData pattern, SkillDataTable skillTable) =>
        pattern != null && pattern.SkillIdx != 0 && skillTable != null && skillTable.TryGetSkillData(pattern.SkillIdx, out _);

    private bool CanSelectPattern(MonsterPatternData pattern, SkillDataTable skillTable, bool requireCurrentBand)
    {
        if (!HasValidSkill(pattern, skillTable) ||
            !skillTable.TryGetSkillData(pattern.SkillIdx, out SkillData skill)) return false;
        return requireCurrentBand
            ? IsPatternStartDistanceValid(pattern, skill, GetAttackSurfaceGap())
            : CanReservePattern(pattern, skillTable);
    }

    private bool CanReservePattern(MonsterPatternData pattern, SkillDataTable skillTable)
    {
        if (!HasValidSkill(pattern, skillTable) ||
            !skillTable.TryGetSkillData(pattern.SkillIdx, out SkillData skill) ||
            !TryGetPatternStartDistanceBand(pattern, skill, out float min, out float max)) return false;
        float gap = GetAttackSurfaceGap();
        if (gap >= min && gap <= max) return true;
        if (pattern.ChaseTimeout <= 0f || UnitData == null || UnitData.MoveSpeed <= 0f) return false;
        float correction = gap > max ? gap - max : min - gap;
        return correction / UnitData.MoveSpeed <= pattern.ChaseTimeout;
    }

    private static bool HasValidTriggerSubject(MonsterPatternData pattern)
    {
        PatternTriggerSubject expected = (PatternTriggerType)pattern.TriggerType == PatternTriggerType.HpRatioUnder
            ? PatternTriggerSubject.Self : PatternTriggerSubject.CurrentTarget;
        return !pattern.TriggerSubjectValue.HasValue || pattern.TriggerSubject == expected;
    }

    private float GetDetectionDistance()
    {
        Collider2D self = stats != null ? stats.DefenseBodyCollider : null;
        Collider2D target = Player.Instance != null && Player.Instance.Stats != null
            ? Player.Instance.Stats.DefenseBodyCollider : null;
        return Vector2.Distance(self != null ? self.bounds.center : transform.position,
            target != null ? target.bounds.center : playerTarget.position);
    }

    private float GetAttackSurfaceGap()
    {
        return TryGetAttackGeometry(out float selfX, out float targetX, out float widths)
            ? Mathf.Max(0f, Mathf.Abs(targetX - selfX) - widths) : float.PositiveInfinity;
    }

    private bool TryGetAttackGeometry(out float selfX, out float targetX, out float combinedHalfWidths)
    {
        selfX = transform.position.x;
        targetX = playerTarget != null ? playerTarget.position.x : selfX;
        combinedHalfWidths = 0f;
        if (playerTarget == null) return false;
        Collider2D self = stats != null ? stats.DefenseBodyCollider : null;
        Collider2D target = Player.Instance != null && Player.Instance.Stats != null
            ? Player.Instance.Stats.DefenseBodyCollider : null;
        if ((self == null || target == null) && !missingDistanceColliderWarned)
        {
            missingDistanceColliderWarned = true;
            Debug.LogWarning($"[Monster] Unit {UnitIdx} attack distance uses transform fallback because a defense body collider is missing.");
        }
        selfX = self != null ? self.bounds.center.x : transform.position.x;
        targetX = target != null ? target.bounds.center.x : playerTarget.position.x;
        combinedHalfWidths = (self != null ? self.bounds.extents.x : 0f) +
            (target != null ? target.bounds.extents.x : 0f);
        return true;
    }

    public static bool TryGetPatternStartDistanceBand(MonsterPatternData pattern, SkillData skill,
        out float minDistance, out float maxDistance)
    {
        minDistance = pattern != null ? pattern.MinStartDistance : 0f;
        maxDistance = pattern != null && pattern.MaxStartDistance > 0f
            ? pattern.MaxStartDistance : skill != null ? skill.Range : 0f;
        return pattern != null && float.IsFinite(minDistance) && float.IsFinite(maxDistance) &&
            minDistance >= 0f && maxDistance > 0f && minDistance <= maxDistance;
    }

    public static bool IsPatternStartDistanceValid(MonsterPatternData pattern, SkillData skill, float distance) =>
        float.IsFinite(distance) && TryGetPatternStartDistanceBand(pattern, skill, out float min, out float max) &&
        distance >= min && distance <= max;

    private static bool IsPatternStartDistanceValid(MonsterPatternData pattern, SkillDataTable skillTable,
        float distance) => pattern != null && skillTable != null && skillTable.TryGetSkillData(pattern.SkillIdx, out SkillData skill) &&
        IsPatternStartDistanceValid(pattern, skill, distance);

    private bool IsCooldown(uint patternIdx)
    {
        return patternCooldowns.TryGetValue(patternIdx, out var readyTime) && Time.time < readyTime;
    }

    private async UniTask ExecutePatternAsync(MonsterPatternData pattern, CancellationToken cancellationToken)
    {
        if (pattern == null || playerTarget == null || currentPattern != null) return;
        currentPattern = pattern;
        patternStartedAt = Time.time;
        patternCooldownCommitted = false;
        CurrentPatternState = PatternState.Reserved;
        patternCancellation = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken, this.GetCancellationTokenOnDestroy());
        try
        {
            if (!await ChaseIntoStartBandAsync(pattern, patternCancellation.Token))
            {
                CancelCurrentPattern(PatternCancelReason.Timeout);
                failedReservationPatternIdx = pattern.Idx;
                skipFailedReservationOnce = true;
                return;
            }
            StopAttackMotionImmediately();
            CurrentPatternState = PatternState.Startup;
            await ExecutePatternCoreAsync(pattern, patternCancellation.Token);
        }
        catch (OperationCanceledException) when (patternCancellation == null || patternCancellation.IsCancellationRequested || cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            selectionBlockedFrame = Time.frameCount;
            Debug.LogError($"[Monster] Unit {UnitIdx}, Pattern {pattern.Idx}, Skill {pattern.SkillIdx} failed: {exception.Message}");
            CancelCurrentPattern(PatternCancelReason.Exception);
        }
        finally
        {
            StopAttackMotionImmediately();
            ReleaseAttackToken();
            if (currentPattern == pattern)
            {
                patternCancellation?.Dispose();
                patternCancellation = null;
                currentPattern = null;
                if (CurrentPatternState != PatternState.Returning) CurrentPatternState = PatternState.Idle;
            }
        }
    }

    private async UniTask<bool> ChaseIntoStartBandAsync(MonsterPatternData pattern, CancellationToken cancellationToken)
    {
        SkillDataTable table = DataTableManager.Instance != null
            ? DataTableManager.Instance.GetDB<SkillDataTable>(DataTableType.Skill) : null;
        if (table == null || !table.TryGetSkillData(pattern.SkillIdx, out SkillData skill) ||
            !TryGetPatternStartDistanceBand(pattern, skill, out float min, out float max)) return false;

        float gap = GetAttackSurfaceGap();
        if (pattern.ChaseTimeout <= 0f) return gap >= min && gap <= max;
        if (UnitData == null || UnitData.MoveSpeed <= 0f) return false;

        CurrentPatternState = PatternState.Chase;
        float elapsed = 0f;
        bool enteredStartBand = false;
        while (elapsed + Mathf.Epsilon < pattern.ChaseTimeout)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!CanAct(actionGeneration) || playerTarget == null) return false;
            if (!TryGetAttackGeometry(out float selfCenterX, out float targetCenterX, out float combinedHalfWidths))
                return false;
            gap = Mathf.Max(0f, Mathf.Abs(targetCenterX - selfCenterX) - combinedHalfWidths);
            float toward = targetCenterX >= selfCenterX ? 1f : -1f;
            SetFacingRight(toward >= 0f);
            enteredStartBand |= gap >= min && gap <= max;
            if (enteredStartBand)
            {
                UnitBase target = Player.Instance;
                if (target == null || !TryGetAttackApproachStopX(target, out float contactStopX)) return false;
                float contactCorrection = Mathf.Abs(contactStopX - transform.position.x);
                float contactRemaining = Mathf.Max(Time.fixedDeltaTime, pattern.ChaseTimeout - elapsed);
                float contactSpeed = CalculateReservationChaseSpeed(contactCorrection, contactRemaining,
                    UnitData.MoveSpeed, Time.fixedDeltaTime);
                float contactDirection = Mathf.Sign(contactStopX - transform.position.x);
                float contactNextX = Mathf.MoveTowards(transform.position.x, contactStopX,
                    contactSpeed * Time.fixedDeltaTime);
                bool contactBlocked = motor == null || contactDirection < 0f && motor.IsWalledLeft ||
                    contactDirection > 0f && motor.IsWalledRight ||
                    hasSpawnArea && !movementBounds.Contains(new Vector3(contactNextX, transform.position.y, transform.position.z));
                if (contactBlocked) return false;
                motor.SetHorizontalStopPosition(contactStopX);
                motor.SetTargetVelocityX(contactDirection * contactSpeed);
                await UniTask.Yield(PlayerLoopTiming.FixedUpdate, cancellationToken);
                elapsed += Time.fixedDeltaTime;
                continue;
            }

            bool retreating = gap < min;
            float direction = retreating ? -toward : toward;
            float boundary = retreating ? min : max;
            float desiredSelfCenterX = targetCenterX - toward * (combinedHalfWidths + boundary);
            float desiredRootX = transform.position.x + desiredSelfCenterX - selfCenterX;
            float correction = Mathf.Abs(desiredRootX - transform.position.x);
            float remaining = Mathf.Max(Time.fixedDeltaTime, pattern.ChaseTimeout - elapsed);
            float speed = CalculateReservationChaseSpeed(correction, remaining, UnitData.MoveSpeed, Time.fixedDeltaTime);
            float nextX = Mathf.MoveTowards(transform.position.x, desiredRootX, speed * Time.fixedDeltaTime);
            bool movementBlocked = motor == null || direction < 0f && motor.IsWalledLeft || direction > 0f && motor.IsWalledRight ||
                hasSpawnArea && !movementBounds.Contains(new Vector3(nextX, transform.position.y, transform.position.z));
            if (movementBlocked) return false;
            motor.SetHorizontalStopPosition(desiredRootX);
            motor.SetTargetVelocityX(direction * speed);
            await UniTask.Yield(PlayerLoopTiming.FixedUpdate, cancellationToken);
            elapsed += Time.fixedDeltaTime;
        }
        StopAttackMotionImmediately();
        return enteredStartBand;
    }

    public static float CalculateReservationChaseSpeed(float correction, float remaining,
        float moveSpeed, float fixedDeltaTime) => Mathf.Min(Mathf.Max(0f, moveSpeed),
        1.25f * Mathf.Max(0f, correction) / Mathf.Max(Mathf.Max(0f, fixedDeltaTime), remaining));

    private async UniTask ExecutePatternCoreAsync(MonsterPatternData pattern, CancellationToken cancellationToken)
    {
        uint generation = actionGeneration;
        if (playerTarget == null || !CanAct(generation)) return;
        if (playerTarget != null && CanAct(generation)) { }
        uint patternSkillId = pattern.SkillIdx;
        if (patternSkillId == 0)
        {
            Debug.LogError($"[Monster] Pattern idx {pattern.Idx} has no SkillData FK; attack cancelled.");
            return;
        }
        var skillTable = DataTableManager.Instance != null
            ? DataTableManager.Instance.GetDB<SkillDataTable>(DataTableType.Skill) : null;
        SkillData skill = null;
        int animState = skillTable != null && skillTable.TryGetSkillData(patternSkillId, out skill)
            ? skill.AnimState : 0;
        if (pattern.ProjectileResourceIdx != 0 && skill != null && skill.AttackSubject != AttackSubject.Weapon)
        {
            Debug.LogError($"[Monster] Pattern idx {pattern.Idx} projectile skill {patternSkillId} cannot use body-part attack subject.");
            return;
        }

        bool IsInsideStartBand() => playerTarget != null &&
            IsPatternStartDistanceValid(pattern, skill, GetAttackSurfaceGap());
        bool stationaryAttack = SkillExecutor.ResolveAttackMotionProfile(skill, pattern.AttackMotionProfileIdx).MotionType ==
            AttackMotionType.Stationary;
        if (!CanAct(generation) || stationaryAttack && !IsInsideStartBand()) return;
        SetAttackMotionVelocityX(0f);
        bool CanStartActiveWindow() => playerTarget != null && CanAct(generation) &&
            (!stationaryAttack || IsInsideStartBand());
        if (!TryAcquireAttackToken(stationaryAttack)) return;
        CurrentPatternState = PatternState.Startup;
        try
        {
        SetFacingRight(playerTarget.position.x >= transform.position.x);
        GetAttackTiming(patternSkillId, pattern.ProjectileResourceIdx != 0,
            out float firstHitTiming, out float activeDuration, out float hitWindowPre);
        float windowStart = Mathf.Max(0f, firstHitTiming - hitWindowPre);
        float effectivePreDelay = CalculateEffectivePreDelay(pattern.PreDelay, windowStart);
        float attackSequenceStartedAt = Time.time;
        float impactAt = attackSequenceStartedAt + effectivePreDelay + windowStart;
        BeginAttackTelegraph(generation, impactAt - AttackTelegraphLeadSeconds,
            impactAt, attackSequenceStartedAt + effectivePreDelay + activeDuration,
            skill != null ? skill.AttackSubject : AttackSubject.Weapon,
            skill != null ? skill.BodyPartRole : BodyPartRole.None);

        if (effectivePreDelay > 0f)
        {
            int preMs = Mathf.RoundToInt(effectivePreDelay * 1000f);
            await UniTask.Delay(preMs, cancellationToken: cancellationToken);
            if (!CanAct(generation)) return;
        }
        SetAttackMotionVelocityX(0f);

        bool timedAttack = false;

        float animationStartedAt = Time.time;
        if (skillExecutor != null && CanAct(generation))
        {
            bool played = skillExecutor.TryPlaySkillAnimation(animator, patternSkillId);
            if (!played)
            {
                Debug.LogError($"[Monster] Unit {UnitIdx}, Pattern {pattern.Idx}, Skill {patternSkillId}, AnimState {animState} transition failed.");
                return;
            }
        }

        Vector3 offset = (spriteRenderer != null && spriteRenderer.flipX) ? Vector3.right * 1.5f : Vector3.left * 1.5f;
        Vector3 spawnPos = transform.position + offset + Vector3.up * 1.0f;
        if (pattern.ProjectileResourceIdx != 0)
        {
            if (windowStart > 0f)
            {
                await UniTask.Delay(Mathf.RoundToInt(windowStart * 1000f), cancellationToken: cancellationToken);
                if (!CanAct(generation)) return;
            }
            SetAttackMotionVelocityX(0f);
            if (!IsInsideStartBand()) return;
            if (pattern.ProjectileSpeed <= 0f || pattern.ProjectileMaxDistance <= 0f)
            {
                Debug.LogError($"[Monster] Pattern idx {pattern.Idx} has invalid projectile speed/distance.");
                return;
            }
            if (UnitPoolManager.Instance == null) return;
            Vector2 direction = playerTarget != null
                ? ((Vector2)playerTarget.position - (Vector2)spawnPos).normalized
                : (spriteRenderer != null && spriteRenderer.flipX ? Vector2.right : Vector2.left);
            var projectile = await UnitPoolManager.Instance.SpawnMonsterProjectileAsync(
                pattern.ProjectileResourceIdx, this, generation, spawnPos, direction,
                pattern.ProjectileSpeed, pattern.ProjectileMaxDistance, pattern.Damage);
            if (projectile == null || !CanAct(generation)) return;
            CurrentPatternState = PatternState.Active;
            CommitPatternCooldown(pattern);
            await UniTask.Yield(PlayerLoopTiming.Update, cancellationToken);
        }
        else if (skillExecutor != null)
        {
            timedAttack = await skillExecutor.ExecuteSkillHitsAsync(
                patternSkillId, this, Player.Instance,
                pattern.Damage, cancellationToken, pattern.AttackMotionProfileIdx, CanStartActiveWindow,
                () =>
                {
                    CurrentPatternState = PatternState.Active;
                    CommitPatternCooldown(pattern);
                }, pattern.Idx);
            if (!timedAttack && !IsInsideStartBand()) return;
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

        ReleaseAttackToken();
        CurrentPatternState = PatternState.Recovery;
        float recoveryDuration = skillExecutor != null
            ? skillExecutor.GetAttackRecoverySeconds(animator, animationStartedAt, pattern.PostDelay)
            : Mathf.Max(0f, pattern.PostDelay);
        SetTelegraphedAttackHitbox(recoveryDuration > 0f,
            skill != null ? skill.AttackSubject : AttackSubject.Weapon,
            skill != null ? skill.BodyPartRole : BodyPartRole.None);
        if (recoveryDuration > 0f)
        {
            int postMs = Mathf.RoundToInt(recoveryDuration * 1000f);
            await UniTask.Delay(postMs, cancellationToken: cancellationToken);
            if (!CanAct(generation)) return;
        }
        SetTelegraphedAttackHitbox(false);

        SetAnimState(1);
        }
        finally
        {
            SetAttackMotionVelocityX(0f);
            EndAttackTelegraph(generation);
            SetTelegraphedAttackHitbox(false);
            ReleaseAttackToken();
        }
    }

    protected virtual UniTask ExecuteMovementAiAsync(CancellationToken _)
    {
        uint generation = actionGeneration;
        if (playerTarget == null || !CanAct(generation)) return UniTask.CompletedTask;

        float dist = Vector3.Distance(transform.position, playerTarget.position);
        float detectRange = (MonsterData != null && MonsterData.DetectRange > 0f) ? MonsterData.DetectRange : 6.0f;
        bool hasAttackStop = TryGetAttackApproachStopX(playerTarget.GetComponent<UnitBase>(), out float attackStopX);
        float stopTolerance = (motor != null ? motor.SkinWidth : 0f) +
            ((UnitData != null ? UnitData.MoveSpeed : 0f) * Time.fixedDeltaTime);
        bool reachedAttackStop = hasAttackStop && Mathf.Abs(transform.position.x - attackStopX) <= stopTolerance;

        if (dist <= detectRange && !reachedAttackStop)
        {
            Vector3 dir = (playerTarget.position - transform.position).normalized;
            SetFacingRight(dir.x >= 0);
            float moveSpeed = (UnitData != null && UnitData.MoveSpeed > 0f) ? UnitData.MoveSpeed : 3.0f;
            if (motor != null)
            {
                if (hasAttackStop) motor.SetHorizontalStopPosition(attackStopX);
                motor.SetTargetVelocityX(dir.x * moveSpeed);
            }
            else
            {
                float targetX = hasAttackStop ? attackStopX : playerTarget.position.x;
                transform.position = new Vector3(Mathf.MoveTowards(transform.position.x, targetX,
                    moveSpeed * Time.deltaTime), transform.position.y, transform.position.z);
            }
            SetAnimState(2);
        }
        else
        {
            StopAttackMotionImmediately();
            SetAnimState(1);
        }
        return UniTask.CompletedTask;
    }

    private void CommitPatternCooldown(MonsterPatternData pattern)
    {
        if (patternCooldownCommitted) return;
        patternCooldownCommitted = true;
        if (pendingSequenceIndex >= 0)
        {
            currentSequenceIndex = pendingSequenceIndex;
            pendingSequenceIndex = -1;
        }
        if (pattern.Cooldown > 0f) patternCooldowns[pattern.Idx] = Time.time + pattern.Cooldown;
    }

    public bool TryGetAttackApproachStopX(UnitBase target, out float stopX)
    {
        stopX = transform.position.x;
        if (target == null || target.Stats == null) return false;
        Collider2D targetBody = target.Stats.DefenseBodyCollider;
        float targetCenterX = targetBody != null ? targetBody.bounds.center.x : target.transform.position.x;
        float targetHalfWidth = targetBody != null ? targetBody.bounds.extents.x : 0f;
        bool facingRight = targetCenterX >= transform.position.x;
        SetFacingRight(facingRight);
        if (!TryGetAttackSweepCenterOffset(facingRight, out float centerOffset)) return false;
        stopX = SkillExecutor.CalculateAttackAlignmentTargetX(transform.position.x, transform.position.x,
            targetCenterX, 0f, 0f, targetHalfWidth, centerOffset,
            motor != null ? motor.SkinWidth : Physics2D.defaultContactOffset, float.MaxValue, false);
        return float.IsFinite(stopX);
    }

    private bool TryAcquireAttackToken(bool stopHorizontalImmediately = false)
    {
        if (!CanAct(actionGeneration)) return false;
        if (holdsAttackToken)
        {
            if (stopHorizontalImmediately) StopAttackMotionImmediately();
            return true;
        }
        if (activeAttackTokens >= MaximumAttackTokens) return false;
        activeAttackTokens++;
        holdsAttackToken = true;
        if (stopHorizontalImmediately) StopAttackMotionImmediately();
        return true;
    }

    private void ReleaseAttackToken()
    {
        if (!holdsAttackToken) return;
        holdsAttackToken = false;
        activeAttackTokens = Mathf.Max(0, activeAttackTokens - 1);
    }

    internal void CancelCurrentPattern(PatternCancelReason reason)
    {
        if (patternCleanupActive) return;
        patternCleanupActive = true;
        uint cancelledGeneration = actionGeneration++;
        try
        {
            patternCancellation?.Cancel();
            patternCancellation?.Dispose();
            patternCancellation = null;
            StopAttackMotionImmediately();
            CancelAttackHitbox();
            EndAttackTelegraph(cancelledGeneration);
            SetTelegraphedAttackHitbox(false);
            skillExecutor?.CancelActiveEffects();
            if (UnitPoolManager.Instance != null) UnitPoolManager.Instance.DespawnProjectilesOwnedBy(this);
            ReleaseAttackToken();
            currentPattern = null;
            patternCooldownCommitted = false;
            pendingSequenceIndex = -1;
            failedReservationPatternIdx = 0u;
            skipFailedReservationOnce = false;
            CurrentPatternState = reason == PatternCancelReason.Returning
                ? PatternState.Returning : PatternState.Idle;
        }
        finally
        {
            patternCleanupActive = false;
        }
    }

    private bool CanAct(uint generation) => generation == actionGeneration && !deathSequenceActive &&
        CurrentLeashState != LeashState.Returning && stats != null && !stats.IsDead &&
        !stats.IsGroggy && isActiveAndEnabled;

    private void BeginReturn()
    {
        if (CurrentLeashState == LeashState.Returning) return;
        CancelCurrentPattern(PatternCancelReason.Returning);
        if (motor != null)
        {
            motor.ApplyKnockback(Vector2.zero);
            motor.SetTargetVelocityX(0f);
        }
        CurrentLeashState = LeashState.Returning;
        CurrentPatternState = PatternState.Returning;
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
        CurrentPatternState = PatternState.Idle;
        returnTeleported = false;
        SetAnimState(1);
    }

    private void OnGroggyStarted()
    {
        CancelCurrentPattern(PatternCancelReason.Groggy);
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
        out float activeDuration, out float hitWindowPre)
    {
        firstHitTiming = 0f;
        activeDuration = projectile ? Time.fixedDeltaTime : FallbackAttackEffectDuration;
        hitWindowPre = 0f;

        SkillDataTable table = DataTableManager.Instance != null
            ? DataTableManager.Instance.GetDB<SkillDataTable>(DataTableType.Skill) : null;
        if (table == null || !table.TryGetSkillData(skillId, out SkillData skill) ||
            skill.HitTimings == null || skill.HitTimings.Length == 0) return;

        firstHitTiming = Mathf.Max(0f, skill.HitTimings[0]);
        hitWindowPre = skill.HitWindowPre;
        float lastHitTiming = Mathf.Max(0f, skill.HitTimings[skill.HitTimings.Length - 1]);
        activeDuration = projectile
            ? Mathf.Max(0f, firstHitTiming - hitWindowPre) + Time.fixedDeltaTime
            : lastHitTiming + skill.HitWindowPost;
    }

    private void BeginAttackTelegraph(uint generation, float warningStartsAt, float impactAt,
        float activeEndsAt, AttackSubject subject, BodyPartRole bodyPart)
    {
        if (!CanAct(generation)) return;
        telegraphGeneration = generation;
        telegraphActive = true;
        SetTelegraphedAttackHitbox(true, subject, bodyPart);
        AttackTelegraphStarted?.Invoke(new AttackTelegraph(this, generation, warningStartsAt,
            impactAt, Mathf.Max(impactAt, activeEndsAt)));
    }

    private void EndAttackTelegraph(uint generation)
    {
        if (!telegraphActive || telegraphGeneration != generation) return;
        telegraphActive = false;
        SetTelegraphedAttackHitbox(false);
        AttackTelegraphEnded?.Invoke(this, generation);
    }
}


