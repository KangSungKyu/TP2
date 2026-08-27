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
    public enum LandingAttackState : uint
    {
        WaitingForTakeoff = 0,
        AirborneObserved = 1,
        LandingCommitted = 2
    }

    private const float LandingAttackClearance = 2.01f;
    private const float AttackTelegraphVisualLeadSeconds = 1f;
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
    private readonly List<MonsterPatternData> patternChain = new List<MonsterPatternData>(16);
    private CancellationTokenSource patternCancellation;
    private MonsterPatternData currentPattern;
    private float patternStartedAt;
    private bool patternCleanupActive;
    private bool patternCooldownCommitted;
    private int selectionBlockedFrame = -1;
    private bool missingDistanceColliderWarned;
    private bool missingChaseColliderLogged;
    private uint failedReservationPatternIdx;
    private bool skipFailedReservationOnce;
    private int pendingSequenceIndex = -1;
    private EffectData reservationAttackEffect;
    private Vector2? reservationExteriorPose;
    private bool reservationExteriorLocked;
    private bool reservationExteriorFacing;
    private uint reservationExteriorGeneration;
    private UnitBase patternTargetSnapshot;
    private Vector2 patternTargetPointSnapshot;
    private float patternTargetHalfWidthSnapshot;
    private bool patternFacingRightSnapshot;
    private uint patternSnapshotGeneration;
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
        ClearLocalHitStop();
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

    protected virtual void LateUpdate()
    {
        if (patternSnapshotGeneration == actionGeneration)
            base.SetFacingRight(patternFacingRightSnapshot);
    }

    public override void SetFacingRight(bool isRight)
    {
        if (patternSnapshotGeneration == actionGeneration) isRight = patternFacingRightSnapshot;
        base.SetFacingRight(isRight);
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
        ClearLocalHitStop();
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
        ClearLocalHitStop();
        actionGeneration++;
        patternCancellation?.Dispose();
        patternCancellation = null;
        currentPattern = null;
        CurrentPatternState = PatternState.Idle;
        patternCleanupActive = false;
        pendingSequenceIndex = -1;
        failedReservationPatternIdx = 0u;
        skipFailedReservationOnce = false;
        ClearReservationExteriorPose();
        ClearPatternSnapshot();
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
            if (patternDB.IsChainChild(pIdx)) continue;
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
        MonsterPatternData currentBand = SelectNextPattern(requireCurrentBand: true, excludedPatternIdx);
        return currentBand ?? (UnitIdx == 3201u ? null :
            SelectNextPattern(requireCurrentBand: false, excludedPatternIdx));
    }

    private MonsterPatternData SelectNextPattern(bool requireCurrentBand, uint excludedPatternIdx)
    {
        float distToPlayer = GetAttackSurfaceGap();
        float hpRatio = stats != null ? (stats.CurrentHp / stats.MaxHp) : 1.0f;
        SkillDataTable skillTable = DataTableManager.Instance != null
            ? DataTableManager.Instance.GetDB<SkillDataTable>(DataTableType.Skill) : null;

        foreach (var pattern in Patterns)
        {
            if (pattern.Idx == excludedPatternIdx) continue;
            if (pattern.ExecutionType == (uint)PatternExecutionType.Trigger)
            {
                if (IsCooldown(pattern.Idx) || !CanSelectPattern(pattern, skillTable, requireCurrentBand) || !HasValidTriggerSubject(pattern)) continue;

                float triggerDistance = NormalizePatternStartDistance(pattern, distToPlayer,
                    motor != null ? motor.SkinWidth : Physics2D.defaultContactOffset);
                bool isTriggered = ((PatternTriggerType)pattern.TriggerType) switch
                {
                    PatternTriggerType.HpRatioUnder => hpRatio <= pattern.TriggerValue,
                    PatternTriggerType.DistanceOver => triggerDistance > pattern.TriggerValue,
                    PatternTriggerType.DistanceUnder => triggerDistance <= pattern.TriggerValue,
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
            return SelectWeightedPattern(randomPatternCandidates, UnityEngine.Random.Range(0, totalWeight));
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

    private static MonsterPatternData SelectWeightedPattern(List<MonsterPatternData> candidates, int roll)
    {
        int currentSum = 0;
        foreach (MonsterPatternData candidate in candidates)
        {
            currentSum += candidate.RandomWeight;
            if (roll < currentSum) return candidate;
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
            ? IsPatternStartDistanceValid(pattern, skill, GetAttackSurfaceGap(),
                motor != null ? motor.SkinWidth : Physics2D.defaultContactOffset)
            : CanReservePattern(pattern, skillTable);
    }

    private bool CanReservePattern(MonsterPatternData pattern, SkillDataTable skillTable)
    {
        if (!HasValidSkill(pattern, skillTable) ||
            !skillTable.TryGetSkillData(pattern.SkillIdx, out SkillData skill) ||
            !TryGetPatternStartDistanceBand(pattern, skill, out float min, out float max)) return false;
        float gap = NormalizePatternStartDistance(pattern, GetAttackSurfaceGap(),
            motor != null ? motor.SkinWidth : Physics2D.defaultContactOffset);
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

    private float GetAttackSurfaceGap()
    {
        if (!TryGetAttackGeometry(out float selfX, out float targetX, out float widths))
            return float.PositiveInfinity;
        float skinWidth = motor != null ? motor.SkinWidth : Physics2D.defaultContactOffset;
        return NormalizeAttackSurfaceGap(Mathf.Max(0f, Mathf.Abs(targetX - selfX) - widths), skinWidth);
    }

    private static float NormalizeAttackSurfaceGap(float gap, float skinWidth) =>
        skinWidth > 0f && float.IsFinite(gap) && gap < skinWidth ? 0f : gap;

    public static float NormalizePatternStartDistance(MonsterPatternData pattern, float distance, float skinWidth)
    {
        if (pattern == null || !float.IsFinite(distance) || skinWidth <= 0f) return distance;
        float tolerance = skinWidth + 0.000001f;
        if (Mathf.Abs(distance - pattern.MinStartDistance) <= tolerance) return pattern.MinStartDistance;
        if (pattern.MaxStartDistance > 0f && Mathf.Abs(distance - pattern.MaxStartDistance) <= tolerance)
            return pattern.MaxStartDistance;
        return Mathf.Abs(distance - pattern.TriggerValue) <= tolerance ? pattern.TriggerValue : distance;
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
        maxDistance = pattern != null && pattern.MaxStartDistance > 0f ? pattern.MaxStartDistance :
            pattern != null && (PatternTriggerType)pattern.TriggerType == PatternTriggerType.DistanceOver
                ? float.MaxValue : skill != null ? skill.Range : 0f;
        return pattern != null && float.IsFinite(minDistance) && float.IsFinite(maxDistance) &&
            minDistance >= 0f && maxDistance > 0f && minDistance <= maxDistance;
    }

    public static bool IsPatternStartDistanceValid(MonsterPatternData pattern, SkillData skill, float distance) =>
        float.IsFinite(distance) && TryGetPatternStartDistanceBand(pattern, skill, out float min, out float max) &&
        distance >= min && distance <= max;

    public static bool IsPatternStartDistanceValid(MonsterPatternData pattern, SkillData skill, float distance,
        float skinWidth) => IsPatternStartDistanceValid(pattern, skill,
            NormalizePatternStartDistance(pattern, distance, skinWidth));

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
        MonsterPatternDataTable patternTable = DataTableManager.Instance != null
            ? DataTableManager.Instance.GetDB<MonsterPatternDataTable>(DataTableType.MonsterPattern) : null;
        if (patternTable == null || !patternTable.TryBuildPatternChain(pattern.Idx, patternChain)) return;
        SkillDataTable skillTable = DataTableManager.Instance.GetDB<SkillDataTable>(DataTableType.Skill);
        for (int i = 0; i < patternChain.Count; i++)
        {
            if (skillTable == null || patternChain[i].SkillIdx == 0u ||
                !skillTable.TryGetSkillData(patternChain[i].SkillIdx, out _))
            {
                Debug.LogError($"[Monster] Pattern chain {pattern.Idx} has invalid Skill FK at {patternChain[i].Idx}; chain rejected.");
                return;
            }
        }
        actionGeneration++;
        currentPattern = patternChain[0];
        patternStartedAt = Time.time;
        patternCooldownCommitted = false;
        ClearReservationExteriorPose();
        CurrentPatternState = PatternState.Reserved;
        patternCancellation = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken, this.GetCancellationTokenOnDestroy());
        try
        {
            for (int step = 0; step < patternChain.Count; step++)
            {
                MonsterPatternData currentStep = patternChain[step];
                currentPattern = currentStep;
                patternStartedAt = Time.time;
                ClearReservationExteriorPose();
                CurrentPatternState = PatternState.Reserved;
                if (step == 0 && !await ChaseIntoStartBandAsync(currentStep, patternCancellation.Token))
                {
                    failedReservationPatternIdx = currentStep.Idx;
                    skipFailedReservationOnce = true;
                    CancelCurrentPattern(PatternCancelReason.Timeout);
                    return;
                }
                StopAttackMotionImmediately();
                if (!holdsAttackToken && !TryAcquireAttackToken(true)) return;
                CurrentPatternState = PatternState.Startup;
                await ExecutePatternCoreAsync(currentStep, patternChain[0], patternCancellation.Token);
                if (!CanAct(actionGeneration)) return;
                if (CurrentPatternState != PatternState.Recovery)
                {
                    CancelCurrentPattern(PatternCancelReason.TargetInvalid);
                    return;
                }
            }
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
            if (currentPattern != null)
            {
                patternCancellation?.Dispose();
                patternCancellation = null;
                currentPattern = null;
                ClearReservationExteriorPose();
                if (CurrentPatternState != PatternState.Returning) CurrentPatternState = PatternState.Idle;
            }
            ClearPatternSnapshot();
        }
    }

    private async UniTask<bool> ChaseIntoStartBandAsync(MonsterPatternData pattern, CancellationToken cancellationToken)
    {
        SkillDataTable table = DataTableManager.Instance != null
            ? DataTableManager.Instance.GetDB<SkillDataTable>(DataTableType.Skill) : null;
        if (table == null || !table.TryGetSkillData(pattern.SkillIdx, out SkillData skill) ||
            !TryGetPatternStartDistanceBand(pattern, skill, out float min, out float max)) return false;
        EffectDataTable effectTable = DataTableManager.Instance != null
            ? DataTableManager.Instance.GetDB<EffectDataTable>(DataTableType.EffectData) : null;
        effectTable?.TryResolveAttackEffect(UnitIdx, pattern.Idx, pattern.SkillIdx, 0u,
            out reservationAttackEffect);

        if (skill.HitTimings != null && skill.HitTimings.Length > 0)
        {
            GetAttackTiming(pattern.SkillIdx, pattern.ProjectileResourceIdx != 0,
                out float firstHitTiming, out float activeDuration, out float hitWindowPre);
            float windowStart = Mathf.Max(0f, firstHitTiming - hitWindowPre);
            float attackMotionTime = Mathf.Max(0f, skill.AttackMotionTime);
            float captureAt = Time.time + pattern.ChaseTimeout +
                CalculateEffectivePreDelay(pattern.PreDelay, windowStart);
            if (TryCalculateSkillTelegraphWindow(captureAt, windowStart, attackMotionTime,
                out float warningStartsAt, out float warningEndsAt, AttackTelegraphVisualLeadSeconds))
                BeginAttackTelegraph(actionGeneration, warningStartsAt, warningEndsAt, warningEndsAt,
                    skill.AttackSubject, skill.BodyPartRole);
        }

        float gap = NormalizePatternStartDistance(pattern, GetAttackSurfaceGap(),
            motor != null ? motor.SkinWidth : Physics2D.defaultContactOffset);
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
            if (patternSnapshotGeneration == actionGeneration)
                targetCenterX = patternTargetPointSnapshot.x;
            gap = NormalizePatternStartDistance(pattern,
                Mathf.Max(0f, Mathf.Abs(targetCenterX - selfCenterX) - combinedHalfWidths),
                motor != null ? motor.SkinWidth : Physics2D.defaultContactOffset);
            float toward = patternSnapshotGeneration == actionGeneration
                ? (patternFacingRightSnapshot ? 1f : -1f)
                : targetCenterX >= selfCenterX ? 1f : -1f;
            if (patternSnapshotGeneration == actionGeneration) ApplyPatternFacingSnapshot();
            else SetFacingRight(toward > 0f);
            SampleReservationExteriorPose();
            enteredStartBand |= gap >= min && gap <= max;
            if (enteredStartBand)
            {
                UnitBase target = Player.Instance;
                if (target == null || !TryGetAttackApproachStopX(target, out float contactStopX)) return false;
                float contactSpeed = CalculateReservationChaseSpeed(UnitData.MoveSpeed);
                float contactDirection = Mathf.Sign(contactStopX - transform.position.x);
                if (!TrySetReservationChaseVelocity(target, contactSpeed, contactDirection,
                    contactStopX)) return false;
                await UniTask.Yield(PlayerLoopTiming.FixedUpdate, cancellationToken);
                elapsed += Time.fixedDeltaTime;
                SampleReservationExteriorPose();
                continue;
            }

            bool retreating = gap < min;
            float direction = retreating ? -toward : toward;
            float boundary = retreating ? min : max;
            float desiredSelfCenterX = targetCenterX - toward * (combinedHalfWidths + boundary);
            float desiredRootX = transform.position.x + desiredSelfCenterX - selfCenterX;
            float speed = CalculateReservationChaseSpeed(UnitData.MoveSpeed);
            if (!TrySetReservationChaseVelocity(Player.Instance, speed, direction, desiredRootX))
                return false;
            await UniTask.Yield(PlayerLoopTiming.FixedUpdate, cancellationToken);
            elapsed += Time.fixedDeltaTime;
            SampleReservationExteriorPose();
        }
        StopAttackMotionImmediately();
        return enteredStartBand;
    }

    public static float CalculateReservationChaseSpeed(float moveSpeed) => Mathf.Max(0f, moveSpeed);

    private bool TrySetReservationChaseVelocity(UnitBase target, float speed, float direction,
        float desiredStopX)
    {
        Collider2D pursuerBody = Stats != null ? Stats.DefenseBodyCollider : null;
        Collider2D targetBody = target != null && target.Stats != null ? target.Stats.DefenseBodyCollider : null;
        if (!TryGetChaseSurfaceGap(pursuerBody, targetBody, out float gap, out float width, out _))
        {
            if (!missingChaseColliderLogged)
            {
                missingChaseColliderLogged = true;
                Debug.LogError($"[Monster] Unit {UnitIdx} reservation chase requires active pursuer and target DefenseBodyCollider.");
            }
            StopAttackMotionImmediately();
            return false;
        }
        missingChaseColliderLogged = false;
        float velocity = CalculateChaseVelocity(speed, Time.fixedDeltaTime, gap, width, direction);
        if (Mathf.Approximately(velocity, 0f))
        {
            StopAttackMotionImmediately();
            return true;
        }
        float colliderStopX = CalculateChaseStopX(transform.position.x, direction, gap, width);
        float stopX = Mathf.Abs(desiredStopX - transform.position.x) <
            Mathf.Abs(colliderStopX - transform.position.x) ? desiredStopX : colliderStopX;
        if (motor == null || velocity < 0f && motor.IsWalledLeft || velocity > 0f && motor.IsWalledRight ||
            hasSpawnArea && !movementBounds.Contains(new Vector3(stopX, transform.position.y, transform.position.z)))
        {
            StopAttackMotionImmediately();
            return false;
        }
        motor.SetHorizontalStopPosition(stopX);
        motor.SetTargetVelocityX(velocity);
        return true;
    }

    private async UniTask ExecutePatternCoreAsync(MonsterPatternData pattern,
        MonsterPatternData cooldownOwner, CancellationToken cancellationToken)
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

        bool IsLiveTargetValid() => Player.Instance != null && Player.Instance.isActiveAndEnabled &&
            Player.Instance.gameObject.activeInHierarchy && Player.Instance.Stats != null &&
            !Player.Instance.Stats.IsDead && CanAct(generation);
        bool IsConfirmedTargetValid() => patternSnapshotGeneration == generation &&
            patternTargetSnapshot != null && patternTargetSnapshot.gameObject.activeInHierarchy &&
            patternTargetSnapshot.isActiveAndEnabled && patternTargetSnapshot.Stats != null &&
            !patternTargetSnapshot.Stats.IsDead && CanAct(generation);
        bool rootStep = pattern.Idx == cooldownOwner.Idx;
        if (rootStep ? !IsLiveTargetValid() : !IsConfirmedTargetValid()) return;
        SetAttackMotionVelocityX(0f);
        CurrentPatternState = PatternState.Startup;
        try
        {
        ApplyPatternFacingSnapshot();
        GetAttackTiming(patternSkillId, pattern.ProjectileResourceIdx != 0,
            out float firstHitTiming, out float activeDuration, out float hitWindowPre);
        float windowStart = Mathf.Max(0f, firstHitTiming - hitWindowPre);
        float effectivePreDelay = CalculateEffectivePreDelay(pattern.PreDelay, windowStart);
        float attackMotionTime = skill != null ? Mathf.Max(0f, skill.AttackMotionTime) : 0f;
        float startupLead = effectivePreDelay + attackMotionTime;
        float attackSequenceStartedAt = Time.time;
        float captureAt = attackSequenceStartedAt + effectivePreDelay;
        if (skill != null && (!telegraphActive || telegraphGeneration != generation) &&
            TryCalculateSkillTelegraphWindow(captureAt, windowStart,
            attackMotionTime,
            out float telegraphStartsAt, out float telegraphEndsAt,
            AttackTelegraphVisualLeadSeconds))
        {
            BeginAttackTelegraph(generation, telegraphStartsAt, telegraphEndsAt, telegraphEndsAt,
                skill != null ? skill.AttackSubject : AttackSubject.Weapon,
                skill != null ? skill.BodyPartRole : BodyPartRole.None);
        }

        float schedulerLead = effectivePreDelay;
        if (schedulerLead > 0f)
        {
            float schedulerEndsAt = Time.time + schedulerLead;
            while (Time.time + Mathf.Epsilon < schedulerEndsAt)
            {
                if (!IsLiveTargetValid()) return;
                Collider2D ownerBody = Stats != null ? Stats.DefenseBodyCollider : null;
                Collider2D targetBody = Player.Instance.Stats.DefenseBodyCollider;
                if (ownerBody == null || targetBody == null || !ownerBody.isActiveAndEnabled ||
                    !targetBody.isActiveAndEnabled) return;
                float liveDeltaX = targetBody.bounds.center.x - ownerBody.bounds.center.x;
                float liveEpsilon = motor != null ? motor.SkinWidth : Physics2D.defaultContactOffset;
                if (Mathf.Abs(liveDeltaX) > liveEpsilon) SetFacingRight(liveDeltaX > 0f);
                await UniTask.Yield(PlayerLoopTiming.FixedUpdate, cancellationToken);
            }
        }
        SetAttackMotionVelocityX(0f);
        if (rootStep)
        {
            EndAttackTelegraph(generation);
            if (!TryCapturePatternSnapshot()) return;
            ClearReservationExteriorPose();
            SampleReservationExteriorPose();
        }
        else if (!IsConfirmedTargetValid()) return;

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
        if (pattern.JumpVelocityY > 0f)
        {
            await UniTask.Yield(PlayerLoopTiming.FixedUpdate, cancellationToken);
            if (!CanAct(generation)) return;
            Collider2D jumpBody = Stats != null ? Stats.DefenseBodyCollider : null;
            if (jumpBody == null || !jumpBody.isActiveAndEnabled || !TryGetGroundedAttackOrigin(out _) ||
                !TryStartAttackJump(pattern.JumpVelocityY, LandingAttackClearance)) return;
            float startBottomY = jumpBody.bounds.min.y;
            EndAttackTelegraph(generation);
            LandingAttackState landingState = LandingAttackState.WaitingForTakeoff;
            bool previousGrounded = true;
            float jumpElapsed = 0f;
            float jumpTimeout = GetAttackJumpTimeout(pattern.JumpVelocityY);
            while (CanAct(generation) && jumpElapsed <= jumpTimeout)
            {
                await UniTask.Yield(PlayerLoopTiming.FixedUpdate, cancellationToken);
                jumpElapsed += Time.fixedDeltaTime;
                bool grounded = IsMotorGrounded;
                bool hasSupport = grounded && TryGetGroundedAttackOrigin(out _);
                landingState = AdvanceLandingAttackState(landingState, startBottomY,
                    jumpBody.bounds.min.y, AttackMotionSkinWidth, previousGrounded, grounded,
                    AttackMotionVelocityY, hasSupport);
                previousGrounded = grounded;
                if (landingState != LandingAttackState.LandingCommitted) continue;
                CurrentPatternState = PatternState.Active;
                timedAttack = skillExecutor != null && skillExecutor.ExecuteLandingHit(
                    patternSkillId, this, pattern.Damage, pattern.Idx, cancellationToken);
                if (timedAttack) CommitPatternCooldown(cooldownOwner);
                break;
            }
        }
        else if (pattern.ProjectileResourceIdx != 0)
        {
            if (attackMotionTime > 0f)
            {
                await UniTask.Delay(Mathf.RoundToInt(attackMotionTime * 1000f), cancellationToken: cancellationToken);
                if (!CanAct(generation)) return;
            }
            if (windowStart > 0f)
            {
                await UniTask.Delay(Mathf.RoundToInt(windowStart * 1000f), cancellationToken: cancellationToken);
                if (!CanAct(generation)) return;
            }
            SetAttackMotionVelocityX(0f);
            EndAttackTelegraph(generation);
            if (!IsConfirmedTargetValid()) return;
            if (pattern.ProjectileSpeed <= 0f || pattern.ProjectileMaxDistance <= 0f)
            {
                Debug.LogError($"[Monster] Pattern idx {pattern.Idx} has invalid projectile speed/distance.");
                return;
            }
            if (UnitPoolManager.Instance == null) return;
            Vector2 aimDelta = patternTargetPointSnapshot - (Vector2)spawnPos;
            if (patternSnapshotGeneration != generation || aimDelta.sqrMagnitude <= Mathf.Epsilon)
            {
                Debug.LogError($"[Monster] Unit {UnitIdx}, Pattern {pattern.Idx} projectile target has no active DefenseBodyCollider.");
                return;
            }
            Vector2 direction = aimDelta.normalized;
            var projectile = await UnitPoolManager.Instance.SpawnUnitProjectileAsync(
                pattern.ProjectileResourceIdx, this, generation, spawnPos, direction,
                pattern.ProjectileSpeed, pattern.ProjectileMaxDistance, pattern.Damage);
            if (projectile == null || !CanAct(generation)) return;
            CurrentPatternState = PatternState.Active;
            CommitPatternCooldown(cooldownOwner);
            await UniTask.Yield(PlayerLoopTiming.Update, cancellationToken);
        }
        else if (skillExecutor != null)
        {
            timedAttack = await skillExecutor.ExecuteSkillHitsAsync(
                patternSkillId, this, patternTargetSnapshot,
                pattern.Damage, cancellationToken, pattern.AttackMotionProfileIdx, () =>
                {
                    EndAttackTelegraph(generation);
                    return IsConfirmedTargetValid();
                },
                () =>
                {
                    if (!CanAct(generation)) return;
                    CurrentPatternState = PatternState.Active;
                    CommitPatternCooldown(cooldownOwner);
                }, pattern.Idx, reservationExteriorFacing == IsFacingRight
                    && reservationExteriorGeneration == generation
                    ? reservationExteriorPose : null,
                () =>
                {
                    CurrentPatternState = PatternState.Recovery;
                    return skillExecutor.GetAttackRecoverySeconds(animator, animationStartedAt, 0f);
                }, patternFacingRightSnapshot, patternTargetPointSnapshot.x,
                patternTargetHalfWidthSnapshot);
            if (timedAttack && activeDuration > 0f)
            {
                float remainingWindow = attackSequenceStartedAt + startupLead + activeDuration - Time.time;
                if (remainingWindow > 0f)
                    await UniTask.Delay(Mathf.RoundToInt(remainingWindow * 1000f), cancellationToken: cancellationToken);
            }
        }

        if (!timedAttack && pattern.ProjectileResourceIdx == 0)
        {
            Debug.LogError($"[Monster] Unit idx {UnitIdx} has no valid attack hitbox window; attack cancelled.");
            return;
        }

        CurrentPatternState = PatternState.Recovery;
        float recoveryDuration = Mathf.Max(0f, pattern.PostDelay);
        if (recoveryDuration > 0f)
        {
            int postMs = Mathf.RoundToInt(recoveryDuration * 1000f);
            await UniTask.Delay(postMs, cancellationToken: cancellationToken);
            if (!CanAct(generation)) return;
        }

        SetAnimState(1);
        }
        finally
        {
            SetAttackMotionVelocityX(0f);
            EndAttackTelegraph(generation);
        }
    }

    public static LandingAttackState AdvanceLandingAttackState(LandingAttackState state,
        float startBottomY, float currentBottomY, float skinWidth, bool previousGrounded,
        bool currentGrounded, float velocityY, bool hasGroundSupport)
    {
        if (state == LandingAttackState.WaitingForTakeoff)
            return !currentGrounded && currentBottomY > startBottomY + Mathf.Max(0f, skinWidth)
                ? LandingAttackState.AirborneObserved : state;
        if (state == LandingAttackState.AirborneObserved && !previousGrounded && currentGrounded &&
            velocityY <= 0f && hasGroundSupport) return LandingAttackState.LandingCommitted;
        return state;
    }

    private static bool TryGetProjectileAimDirection(Vector2 spawnPosition, Collider2D targetBody,
        out Vector2 direction)
    {
        direction = Vector2.zero;
        if (targetBody == null || !targetBody.isActiveAndEnabled) return false;
        Vector2 delta = (Vector2)targetBody.bounds.center - spawnPosition;
        if (delta.sqrMagnitude <= Mathf.Epsilon) return false;
        direction = delta.normalized;
        return true;
    }

    public static float CalculatePatternRecoverySeconds(float animationRecovery,
        float postDelay) =>
        Mathf.Max(0f, animationRecovery) + Mathf.Max(0f, postDelay);

    public override bool IsAttackMotionPositionAllowed(float worldX) => !hasSpawnArea ||
        movementBounds.Contains(new Vector3(worldX, transform.position.y, transform.position.z));

    protected virtual UniTask ExecuteMovementAiAsync(CancellationToken _)
    {
        uint generation = actionGeneration;
        if (playerTarget == null || !CanAct(generation)) return UniTask.CompletedTask;

        UnitBase chaseTarget = Player.Instance;
        Collider2D pursuerBody = stats != null ? stats.DefenseBodyCollider : null;
        Collider2D targetBody = chaseTarget != null && chaseTarget.Stats != null
            ? chaseTarget.Stats.DefenseBodyCollider : null;
        if (!TryGetChaseSurfaceGap(pursuerBody, targetBody, out float chaseGap, out float targetWidth,
            out float chaseDirection))
        {
            if (!missingChaseColliderLogged)
            {
                missingChaseColliderLogged = true;
                Debug.LogError($"[Monster] Unit {UnitIdx} chase requires active pursuer and target DefenseBodyCollider.");
            }
            StopAttackMotionImmediately();
            SetAnimState(1);
            return UniTask.CompletedTask;
        }
        missingChaseColliderLogged = false;
        SetFacingRight(chaseDirection >= 0f);
        bool reachedColliderStop = chaseGap <= targetWidth;

        float detectRange = GetPatternEvaluationRange();
        bool hasAttackStop = TryGetAttackApproachStopX(chaseTarget, out float attackStopX) ||
            TryGetNearestApproachBandStopX(out attackStopX);
        float stopTolerance = (motor != null ? motor.SkinWidth : 0f) +
            ((UnitData != null ? UnitData.MoveSpeed : 0f) * Time.fixedDeltaTime);
        bool reachedAttackStop = hasAttackStop && Mathf.Abs(transform.position.x - attackStopX) <= stopTolerance;

        if (GetAttackSurfaceGap() <= detectRange && !reachedAttackStop && !reachedColliderStop)
        {
            float moveSpeed = (UnitData != null && UnitData.MoveSpeed > 0f) ? UnitData.MoveSpeed : 3.0f;
            if (motor != null)
            {
                float colliderStopX = CalculateChaseStopX(transform.position.x, chaseDirection,
                    chaseGap, targetWidth);
                float stopX = hasAttackStop &&
                    Mathf.Abs(attackStopX - transform.position.x) < Mathf.Abs(colliderStopX - transform.position.x)
                    ? attackStopX : colliderStopX;
                motor.SetHorizontalStopPosition(stopX);
                motor.SetTargetVelocityX(CalculateChaseVelocity(moveSpeed, Time.fixedDeltaTime,
                    chaseGap, targetWidth, chaseDirection));
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

    public static bool TryGetChaseSurfaceGap(Collider2D pursuer, Collider2D target,
        out float surfaceGap, out float targetWidth, out float direction)
    {
        surfaceGap = targetWidth = direction = 0f;
        if (pursuer == null || target == null || !pursuer.isActiveAndEnabled || !target.isActiveAndEnabled)
            return false;
        Bounds pursuerBounds = pursuer.bounds;
        Bounds targetBounds = target.bounds;
        direction = targetBounds.center.x >= pursuerBounds.center.x ? 1f : -1f;
        surfaceGap = direction > 0f
            ? Mathf.Max(0f, targetBounds.min.x - pursuerBounds.max.x)
            : Mathf.Max(0f, pursuerBounds.min.x - targetBounds.max.x);
        targetWidth = targetBounds.size.x;
        return targetWidth > 0f;
    }

    public static float CalculateChaseStopX(float currentX, float direction, float surfaceGap,
        float targetWidth) => currentX + Mathf.Sign(direction) * Mathf.Max(0f, surfaceGap - targetWidth);

    public static float CalculateChaseVelocity(float speed, float fixedDeltaTime, float surfaceGap,
        float targetWidth, float direction)
    {
        float allowedDistance = Mathf.Max(0f, surfaceGap - targetWidth);
        if (allowedDistance <= 0f || fixedDeltaTime <= 0f) return 0f;
        return Mathf.Sign(direction) * Mathf.Min(Mathf.Abs(speed), allowedDistance / fixedDeltaTime);
    }

    private bool TryGetNearestApproachBandStopX(out float stopX)
    {
        stopX = transform.position.x;
        if (currentPattern != null || !TryGetAttackGeometry(out float selfX, out float targetX, out float widths))
            return false;
        SkillDataTable skills = DataTableManager.Instance != null
            ? DataTableManager.Instance.GetDB<SkillDataTable>(DataTableType.Skill) : null;
        if (skills == null) return false;

        float gap = NormalizeAttackSurfaceGap(Mathf.Max(0f, Mathf.Abs(targetX - selfX) - widths),
            motor != null ? motor.SkinWidth : Physics2D.defaultContactOffset);
        float boundary = -1f;
        foreach (MonsterPatternData pattern in Patterns)
            if (skills.TryGetSkillData(pattern.SkillIdx, out SkillData skill) &&
                TryGetPatternStartDistanceBand(pattern, skill, out _, out float max) &&
                max < gap && max > boundary)
                boundary = max;
        if (boundary < 0f) return false;

        float direction = targetX >= selfX ? 1f : -1f;
        float desiredSelfCenter = targetX - direction * (widths + boundary);
        stopX = transform.position.x + desiredSelfCenter - selfX;
        return float.IsFinite(stopX) && IsAttackMotionPositionAllowed(stopX);
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
        float targetCenterX = patternSnapshotGeneration == actionGeneration
            ? patternTargetPointSnapshot.x
            : targetBody != null ? targetBody.bounds.center.x : target.transform.position.x;
        float targetHalfWidth = patternSnapshotGeneration == actionGeneration
            ? patternTargetHalfWidthSnapshot
            : targetBody != null ? targetBody.bounds.extents.x : 0f;
        bool facingRight = patternSnapshotGeneration == actionGeneration
            ? patternFacingRightSnapshot : targetCenterX >= transform.position.x;
        if (patternSnapshotGeneration != actionGeneration) SetFacingRight(facingRight);
        EffectDataTable effectTable = DataTableManager.Instance != null
            ? DataTableManager.Instance.GetDB<EffectDataTable>(DataTableType.EffectData) : null;
        if (currentPattern == null || effectTable == null || !effectTable.TryResolveAttackEffect(
            UnitIdx, currentPattern.Idx, currentPattern.SkillIdx, 0u, out EffectData effect)) return false;
        float centerOffset = (facingRight ? 1f : -1f) * effect.ActiveCenterX;
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
            ClearLocalHitStop();
            patternCancellation?.Cancel();
            patternCancellation?.Dispose();
            patternCancellation = null;
            StopAttackMotionImmediately();
            EndAttackTelegraph(cancelledGeneration);
            skillExecutor?.CancelActiveEffects();
            if (UnitPoolManager.Instance != null) UnitPoolManager.Instance.DespawnProjectilesOwnedBy(this);
            ReleaseAttackToken();
            currentPattern = null;
            patternCooldownCommitted = false;
            pendingSequenceIndex = -1;
            failedReservationPatternIdx = 0u;
            skipFailedReservationOnce = false;
            ClearReservationExteriorPose();
            ClearPatternSnapshot();
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

    private void SampleReservationExteriorPose()
    {
        if (reservationAttackEffect == null || patternTargetSnapshot == null) return;
        if (reservationExteriorGeneration != actionGeneration)
        {
            reservationExteriorPose = null;
            reservationExteriorLocked = false;
            reservationExteriorFacing = IsFacingRight;
            reservationExteriorGeneration = actionGeneration;
        }
        if (reservationExteriorFacing != IsFacingRight)
        {
            reservationExteriorPose = null;
            reservationExteriorLocked = false;
        }
        if (reservationExteriorLocked) return;
        reservationExteriorFacing = IsFacingRight;
        if (!SkillExecutor.TrySampleNonContactEffectPose(this, patternTargetSnapshot, reservationAttackEffect,
            reservationExteriorFacing, out Vector2 pose, out bool contacted))
        {
            ClearReservationExteriorPose();
            reservationExteriorLocked = true;
            return;
        }
        if (contacted)
        {
            reservationExteriorLocked = reservationExteriorPose.HasValue;
            return;
        }
        reservationExteriorPose = pose;
    }

    private void ClearReservationExteriorPose()
    {
        reservationAttackEffect = null;
        reservationExteriorPose = null;
        reservationExteriorLocked = false;
        reservationExteriorFacing = IsFacingRight;
        reservationExteriorGeneration = actionGeneration;
    }

    private bool TryCapturePatternSnapshot()
    {
        UnitBase target = Player.Instance;
        Collider2D ownerBody = Stats != null ? Stats.DefenseBodyCollider : null;
        Collider2D targetBody = target != null && target.Stats != null ? target.Stats.DefenseBodyCollider : null;
        if (target == null || !target.isActiveAndEnabled || !target.gameObject.activeInHierarchy ||
            target.Stats == null || target.Stats.IsDead || ownerBody == null || targetBody == null ||
            !ownerBody.isActiveAndEnabled || !targetBody.isActiveAndEnabled)
        {
            Debug.LogError($"[Monster] Unit {UnitIdx} pattern snapshot requires active owner and target DefenseBodyCollider.");
            ClearPatternSnapshot();
            return false;
        }
        float deltaX = targetBody.bounds.center.x - ownerBody.bounds.center.x;
        float epsilon = motor != null ? motor.SkinWidth : Physics2D.defaultContactOffset;
        patternFacingRightSnapshot = Mathf.Abs(deltaX) <= epsilon ? IsFacingRight : deltaX > 0f;
        patternTargetSnapshot = target;
        patternTargetPointSnapshot = targetBody.bounds.center;
        patternTargetHalfWidthSnapshot = targetBody.bounds.extents.x;
        patternSnapshotGeneration = actionGeneration;
        SetFacingRight(patternFacingRightSnapshot);
        return true;
    }

    private void ApplyPatternFacingSnapshot()
    {
        if (patternSnapshotGeneration == actionGeneration && IsFacingRight != patternFacingRightSnapshot)
            SetFacingRight(patternFacingRightSnapshot);
    }

    private void ClearPatternSnapshot()
    {
        patternTargetSnapshot = null;
        patternTargetPointSnapshot = default;
        patternTargetHalfWidthSnapshot = 0f;
        patternSnapshotGeneration = 0u;
    }

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

    public static float CalculateEffectivePreDelay(float configuredPreDelay, float windowStart) =>
        Mathf.Max(0f, Mathf.Max(0f, configuredPreDelay) - Mathf.Max(0f, windowStart));

    public static float CalculateSkillStartupSeconds(float configuredPreDelay, float windowStart,
        float attackMotionTime) => CalculateEffectivePreDelay(configuredPreDelay, windowStart) +
        Mathf.Max(0f, attackMotionTime) + Mathf.Max(0f, windowStart);

    public static bool TryCalculateSkillTelegraphWindow(float attackStartsAt, float preDuration,
        float attackMotionTime,
        out float displayStartsAt, out float displayEndsAt, float visualLeadSeconds = 0f)
    {
        displayEndsAt = attackStartsAt;
        float duration = Mathf.Max(0f, attackMotionTime) + Mathf.Max(0f, preDuration) +
            Mathf.Max(0f, visualLeadSeconds);
        displayStartsAt = displayEndsAt - duration;
        return duration > 0f;
    }

    private float GetPatternEvaluationRange()
    {
        float range = MonsterData != null && MonsterData.DetectRange > 0f ? MonsterData.DetectRange : 6f;
        SkillDataTable skills = DataTableManager.Instance != null
            ? DataTableManager.Instance.GetDB<SkillDataTable>(DataTableType.Skill) : null;
        if (skills == null) return range;
        foreach (MonsterPatternData pattern in Patterns)
            if (skills.TryGetSkillData(pattern.SkillIdx, out SkillData skill) &&
                TryGetPatternStartDistanceBand(pattern, skill, out _, out float max))
                range = Mathf.Max(range, max);
        return range;
    }

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


