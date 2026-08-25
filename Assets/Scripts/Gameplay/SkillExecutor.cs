using Cysharp.Threading.Tasks;
using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;

/// <summary>
/// 스킬 실행 및 이펙트 스폰 로직을 담당합니다.
/// SimplePoolManager를 통한 비동기 Addressables 오브젝트 풀링을 전면 적용합니다.
/// </summary>
public class SkillExecutor : MonoBehaviour
{
    private const uint SpearSentryAttackEffectIdx = 8015u;
    private const float SpearSentryActiveFrameNormalizedTime = 0.5f;
    public static SkillExecutor Instance { get; private set; }

    // =========================================================================
    // 1. CONST & PRIVATE FIELDS
    // =========================================================================

    private const float BaseDamage = 10f;
    private CombatStats stats;
    private GameObject particlePrefab;
    private uint particleLoadGeneration;
    private bool particleLoadPending;
    private bool particleLoadFailureLogged;
    private readonly Dictionary<int, float> nextAvailable = new Dictionary<int, float>();
    private readonly HashSet<SkillEffect> activeSkillEffects = new HashSet<SkillEffect>();
#if UNITY_EDITOR || DEVELOPMENT_BUILD
    private LineRenderer effectBoundsDebugLine;
#endif

    // =========================================================================
    // 2. PUBLIC METHODS (PascalCase)
    // =========================================================================

    public void ExecuteSkill(int skillId, UnitBase caster, UnitBase target)
    {
        var skillTable = DataTableManager.Instance != null ? DataTableManager.Instance.GetDB<SkillDataTable>(DataTableType.Skill) : null;
        if (skillTable == null || !skillTable.TryGetSkill(skillId, out var skill))
        {
            Debug.LogWarning($"SkillExecutor: DataTableManager에서 Skill ID {skillId}를 찾을 수 없습니다.");
            return;
        }

        if (nextAvailable.TryGetValue(skillId, out var readyTime) && Time.time < readyTime)
        {
            return;
        }

        if (!(stats?.ConsumeMp(skill.MpCost) ?? true))
        {
            Debug.Log("SkillExecutor: MP가 부족합니다.");
            return;
        }

        if (caster == null || target == null || Vector2.Distance(caster.transform.position, target.transform.position) > skill.Range)
        {
            Debug.Log("SkillExecutor: 사거리를 벗어났습니다.");
            return;
        }

        CastAsync(skill, caster, target, this.GetCancellationTokenOnDestroy()).Forget();
    }

    public SkillEffect SpawnSkillEffect(string effectName, Vector3 position, Vector2 size, float damage, float lifetime, FactionType faction, Color color)
    {
        SkillEffect effectComp = EffectPoolManager.Instance?.GetPooledSkillEffect(effectName, position);
        if (effectComp == null)
        {
            var effectObj = new GameObject(effectName);
            effectObj.transform.position = position;
            var boxCol = effectObj.AddComponent<BoxCollider2D>();
            boxCol.isTrigger = true;
            effectComp = effectObj.AddComponent<SkillEffect>();
            EffectPoolManager.Instance?.TrackSkillEffect(effectComp, effectName);
        }

        effectComp.SetSize(size);
        activeSkillEffects.Add(effectComp);
        effectComp.InitEffect(effectName, damage, lifetime, faction, stats, color, effect => activeSkillEffects.Remove(effect));

        return effectComp;
    }

    public async UniTask<bool> ExecuteSkillHitsAsync(uint skillId, UnitBase owner, UnitBase target,
        float patternDamage, CancellationToken cancellationToken = default,
        uint attackMotionProfileOverrideIdx = 0, Func<bool> canStartWindow = null,
        Action onFirstSuccessfulHit = null, uint attackPatternIdx = 0)
    {
        var table = DataTableManager.Instance != null
            ? DataTableManager.Instance.GetDB<SkillDataTable>(DataTableType.Skill) : null;
        if (owner == null || table == null ||
            !table.TryGetSkillData(skillId, out var skill) || skill.HitTimings == null || skill.HitTimings.Length == 0)
            return false;

        uint generation = owner.ActionGeneration;
        AttackMotionProfileData motion = ResolveAttackMotionProfile(skill, attackMotionProfileOverrideIdx);
        float motionStartX = owner.transform.position.x;
        Collider2D targetBody = target != null && target.Stats != null ? target.Stats.DefenseBodyCollider : null;
        float targetSnapshotX = targetBody != null ? targetBody.bounds.center.x : target != null ? target.transform.position.x : motionStartX;
        float targetSnapshotHalfWidth = targetBody != null ? targetBody.bounds.extents.x : 0f;
        float motionVelocityX = 0f;
        bool stepMotionBlocked = false;
        bool attackMotionComplete = false;
        int sourceId = owner.GetInstanceID() ^ (int)skillId;
        float elapsed = 0f;
        float firstWindowStart = Mathf.Max(0f, skill.HitTimings[0] - skill.HitWindowPre);
        float lastWindowEnd = skill.HitTimings[skill.HitTimings.Length - 1] + skill.HitWindowPost;
        var spawnedTicks = new HashSet<(uint ownerUnitIdx, uint generation, uint hitTick, uint effectIdx)>();
        try
        {
            for (uint tick = 0; tick < (uint)skill.HitTimings.Length; tick++)
            {
                var effectTable = DataTableManager.Instance != null
                    ? DataTableManager.Instance.GetDB<EffectDataTable>(DataTableType.EffectData) : null;
                EffectData attackEffectData = null;
                bool usesPass10Effect = effectTable != null && effectTable.TryResolveAttackEffect(
                    owner.UnitIdx, attackPatternIdx, skillId, tick, out attackEffectData);
                uint attackEffectIdx = usesPass10Effect ? attackEffectData.Idx : 0u;
                if (!usesPass10Effect)
                {
                    Debug.LogError($"[SkillExecutor] Unit {owner.UnitIdx}/Pattern {attackPatternIdx}/Skill {skillId} has no EffectData attack bounds; tick cancelled.");
                    return false;
                }
                else
                {
                    if (!attackEffectData.HasValidActiveBounds)
                    {
                        Debug.LogError($"[SkillExecutor] Unit {owner.UnitIdx}/Pattern {attackPatternIdx}/Skill {skillId} " +
                            $"has invalid EffectData {attackEffectIdx} active bounds; attack cancelled.");
                        return false;
                    }
                    var resourceTable = DataTableManager.Instance.GetDB<ResourceDataTable>(DataTableType.Resource);
                    if (resourceTable == null ||
                        !resourceTable.TryGetResource(attackEffectData.PrefabIdx, out ResourceData resource) ||
                        string.IsNullOrEmpty(resource.Path))
                    {
                        Debug.LogError($"[SkillExecutor] Attack EffectData {attackEffectIdx} has invalid ResourceData FK " +
                            $"{attackEffectData.PrefabIdx}; attack cancelled.");
                        return false;
                    }
                }
                float t = Mathf.Max(0f, skill.HitTimings[(int)tick]);
                float windowStart = Mathf.Max(0f, t - skill.HitWindowPre);
                float windowEnd = t + skill.HitWindowPost;
                if (!TryCalculateEffectPose(owner, attackEffectData, out Vector2 previousEffectCenter,
                    out Quaternion effectRotation)) return false;

                while (elapsed + Mathf.Epsilon < windowStart)
                {
                    float remaining = windowStart - elapsed;
                    bool tracking = motion.TargetPolicy == AttackTargetPolicy.TrackUntilActive && target != null;
                    targetBody = tracking && target.Stats != null ? target.Stats.DefenseBodyCollider : targetBody;
                    float targetX = tracking
                        ? (targetBody != null ? targetBody.bounds.center.x : target.transform.position.x) : targetSnapshotX;
                    float targetHalfWidth = tracking && targetBody != null ? targetBody.bounds.extents.x : targetSnapshotHalfWidth;
                    float targetVelocityX = tracking ? target.AttackMotionVelocityX : 0f;
                    bool facingRight = targetX >= owner.transform.position.x;
                    float attackCenterOffset = (facingRight ? 1f : -1f) *
                        attackEffectData.ActiveCenterX * attackEffectData.Scale;
                    float clampedTargetX = CalculateAttackAlignmentTargetX(motionStartX, owner.transform.position.x,
                        targetX, targetVelocityX, remaining, targetHalfWidth, attackCenterOffset,
                        owner.AttackMotionSkinWidth, motion.MaxDistance, tracking);
                    motionVelocityX = attackMotionComplete ? 0f : CalculateAttackMotionVelocity(motion,
                        owner.transform.position.x, clampedTargetX, remaining, motionVelocityX, Time.fixedDeltaTime);
                    if (!owner.IsActionGenerationCurrent(generation)) return true;
                    if (!stepMotionBlocked && motion.MotionType == AttackMotionType.Step &&
                        !owner.HasGroundSupportForAttackStep(motionVelocityX * Time.fixedDeltaTime))
                    {
                        stepMotionBlocked = true;
                        motionVelocityX = 0f;
                        owner.StopAttackMotionImmediately();
                    }
                    owner.SetAttackMotionStopPosition(clampedTargetX);
                    owner.SetAttackMotionVelocityX(stepMotionBlocked ? 0f : motionVelocityX);
                    await UniTask.Yield(PlayerLoopTiming.FixedUpdate, cancellationToken);
                    elapsed += Time.fixedDeltaTime;
                }
                owner.StopAttackMotionImmediately();
                attackMotionComplete = true;
                if (!owner.IsActionGenerationCurrent(generation)) return true;
                if (canStartWindow != null && !canStartWindow()) return false;

                if (!TryCreateEffectSweep(owner, attackEffectData, previousEffectCenter, sourceId,
                    generation, tick, out CombatStats.AttackSweep2D sweep, out effectRotation)) return false;
                previousEffectCenter = sweep.Current;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                DrawEffectBoundsDebug(owner, sweep);
#endif

                bool tickHitCommitted = ApplyAttackSweep(owner, target, patternDamage, sweep);
                if (tickHitCommitted) onFirstSuccessfulHit?.Invoke();

                if (usesPass10Effect && spawnedTicks.Add((owner.UnitIdx, generation, tick, attackEffectIdx)))
                {
                    float visualEnd = Mathf.Max(lastWindowEnd, firstWindowStart + attackEffectData.Duration);
                    SpawnAttackEffectForWindowAsync(attackEffectIdx, sweep.Current, effectRotation, owner,
                        generation, attackEffectData.Scale, owner.IsFacingRight,
                        Mathf.Max(0f, visualEnd - elapsed),
                        attackEffectIdx == SpearSentryAttackEffectIdx ? SpearSentryActiveFrameNormalizedTime : 0f,
                        cancellationToken).Forget();
                }

                while (elapsed + Mathf.Epsilon < windowEnd)
                {
                    await UniTask.Yield(PlayerLoopTiming.FixedUpdate, cancellationToken);
                    elapsed += Time.fixedDeltaTime;
                    if (!owner.IsActionGenerationCurrent(generation)) return true;
                    if (!TryCreateEffectSweep(owner, attackEffectData, previousEffectCenter, sourceId,
                        generation, tick, out CombatStats.AttackSweep2D movedSweep, out _)) return false;
                    previousEffectCenter = movedSweep.Current;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                    DrawEffectBoundsDebug(owner, movedSweep);
#endif
                    if (!tickHitCommitted && ApplyAttackSweep(owner, target, patternDamage, movedSweep))
                    {
                        tickHitCommitted = true;
                        onFirstSuccessfulHit?.Invoke();
                    }
                }
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                HideEffectBoundsDebug();
#endif

            }
            return true;
        }
        finally
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            HideEffectBoundsDebug();
#endif
            owner.StopAttackMotionImmediately();
        }
    }

    private static bool TryCalculateEffectPose(UnitBase owner, EffectData effect, out Vector2 position,
        out Quaternion rotation)
    {
        position = default;
        rotation = owner != null ? owner.transform.rotation : Quaternion.identity;
        Collider2D body = owner != null && owner.Stats != null ? owner.Stats.DefenseBodyCollider : null;
        if (body == null || !body.enabled || !body.gameObject.activeInHierarchy)
        {
            Debug.LogError($"[SkillExecutor] Unit {(owner != null ? owner.UnitIdx : 0u)} has no active DefenseBodyCollider; attack tick cancelled.");
            return false;
        }
        if (!float.IsFinite(effect.Scale) || effect.Scale <= 0f)
        {
            Debug.LogError($"[SkillExecutor] EffectData {effect.Idx} has invalid Scale {effect.Scale}; attack tick cancelled.");
            return false;
        }
        Vector2 local = new Vector2((owner.IsFacingRight ? 1f : -1f) * effect.ActiveCenterX,
            effect.ActiveCenterY) * effect.Scale;
        position = (Vector2)body.bounds.center + (Vector2)(rotation * (Vector3)local);
        return true;
    }

    private static bool TryCreateEffectSweep(UnitBase owner, EffectData effect, Vector2 previous,
        int sourceId, uint generation, uint tick, out CombatStats.AttackSweep2D sweep,
        out Quaternion rotation)
    {
        sweep = default;
        if (!TryCalculateEffectPose(owner, effect, out Vector2 current, out rotation)) return false;
        Vector2 size = new Vector2(effect.ActiveSizeX, effect.ActiveSizeY) * effect.Scale;
        float angle = rotation.eulerAngles.z;
        float radians = angle * Mathf.Deg2Rad;
        float c = Mathf.Abs(Mathf.Cos(radians));
        float s = Mathf.Abs(Mathf.Sin(radians));
        Vector2 half = size * .5f;
        Vector2 extents = effect.Shape == ActiveShape.Circle ? Vector2.one * half.x :
            new Vector2(c * half.x + s * half.y, s * half.x + c * half.y);
        sweep = new CombatStats.AttackSweep2D(previous, current, extents, sourceId, generation, tick,
            effect.Shape, size, angle);
        return true;
    }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    private void DrawEffectBoundsDebug(UnitBase owner, CombatStats.AttackSweep2D sweep)
    {
        if (effectBoundsDebugLine == null)
        {
            LineRenderer existing = owner != null ? owner.GetComponentInChildren<LineRenderer>(true) : null;
            Material material = existing != null ? existing.sharedMaterial :
                owner != null ? owner.GetComponentInChildren<SpriteRenderer>(true)?.sharedMaterial : null;
            if (owner == null || material == null) return;
            effectBoundsDebugLine = owner.gameObject.AddComponent<LineRenderer>();
            effectBoundsDebugLine.sharedMaterial = material;
            effectBoundsDebugLine.useWorldSpace = true;
            effectBoundsDebugLine.loop = true;
            effectBoundsDebugLine.startWidth = .04f;
            effectBoundsDebugLine.endWidth = .04f;
            effectBoundsDebugLine.sortingLayerID = existing != null ? existing.sortingLayerID : 0;
            effectBoundsDebugLine.sortingOrder = existing != null ? existing.sortingOrder + 1 : 100;
        }

        Color color = sweep.Shape == ActiveShape.Circle ? Color.cyan :
            sweep.Shape == ActiveShape.Capsule ? Color.yellow : Color.red;
        effectBoundsDebugLine.startColor = color;
        effectBoundsDebugLine.endColor = color;
        effectBoundsDebugLine.enabled = true;
        Quaternion rotation = Quaternion.Euler(0f, 0f, sweep.Angle);

        if (sweep.Shape == ActiveShape.Box)
        {
            Vector2 half = sweep.Size * .5f;
            effectBoundsDebugLine.positionCount = 4;
            for (int i = 0; i < 4; i++)
            {
                Vector2 local = i switch
                {
                    0 => new Vector2(-half.x, -half.y),
                    1 => new Vector2(-half.x, half.y),
                    2 => new Vector2(half.x, half.y),
                    _ => new Vector2(half.x, -half.y)
                };
                effectBoundsDebugLine.SetPosition(i,
                    sweep.Current + (Vector2)(rotation * (Vector3)local));
            }
            return;
        }

        const int segmentsPerCap = 12;
        effectBoundsDebugLine.positionCount = segmentsPerCap * 2;
        bool vertical = sweep.Shape == ActiveShape.Circle || sweep.Size.y >= sweep.Size.x;
        float radius = (vertical ? sweep.Size.x : sweep.Size.y) * .5f;
        float halfSegment = sweep.Shape == ActiveShape.Circle ? 0f :
            Mathf.Max(0f, (vertical ? sweep.Size.y : sweep.Size.x) - radius * 2f) * .5f;
        for (int i = 0; i < segmentsPerCap * 2; i++)
        {
            float radians;
            Vector2 capCenter;
            if (vertical)
            {
                bool top = i < segmentsPerCap;
                int capIndex = top ? i : i - segmentsPerCap;
                radians = (top ? capIndex : segmentsPerCap - 1 + capIndex) *
                    Mathf.PI / (segmentsPerCap - 1);
                capCenter = new Vector2(0f, top ? halfSegment : -halfSegment);
            }
            else
            {
                bool right = i < segmentsPerCap;
                int capIndex = right ? i : i - segmentsPerCap;
                radians = (-.5f + (right ? capIndex : segmentsPerCap - 1 + capIndex) /
                    (float)(segmentsPerCap - 1)) * Mathf.PI;
                capCenter = new Vector2(right ? halfSegment : -halfSegment, 0f);
            }
            Vector2 local = capCenter + new Vector2(Mathf.Cos(radians), Mathf.Sin(radians)) * radius;
            effectBoundsDebugLine.SetPosition(i,
                sweep.Current + (Vector2)(rotation * (Vector3)local));
        }
    }

    private void HideEffectBoundsDebug()
    {
        if (effectBoundsDebugLine != null) effectBoundsDebugLine.enabled = false;
    }
#endif

    public static bool TryResolvePass10AttackEffectIdx(uint ownerUnitIdx, uint patternIdx, uint skillIdx,
        out uint effectIdx) => TryResolvePass10AttackEffectIdx(ownerUnitIdx, patternIdx, skillIdx, 0u, out effectIdx);

    public static bool TryResolvePass10AttackEffectIdx(uint ownerUnitIdx, uint patternIdx, uint skillIdx,
        uint hitTick, out uint effectIdx)
    {
        EffectDataTable table = DataTableManager.Instance != null
            ? DataTableManager.Instance.GetDB<EffectDataTable>(DataTableType.EffectData) : null;
        EffectData effect = null;
        bool found = table != null && table.TryResolveAttackEffect(ownerUnitIdx, patternIdx, skillIdx,
            hitTick, out effect);
        effectIdx = found ? effect.Idx : 0u;
        return found;
    }

    private async UniTask SpawnAttackEffectForWindowAsync(uint effectIdx, Vector2 position,
        Quaternion rotation, UnitBase owner, uint generation, float scale, bool facingRight, float duration,
        float normalizedStartTime, CancellationToken cancellationToken)
    {
        GameObject effect = await SpawnEffectByEffectIdxAsync(effectIdx, position, rotation, duration);
        if (effect == null) return;
        if (cancellationToken.IsCancellationRequested || owner == null ||
            !owner.IsActionGenerationCurrent(generation))
        {
            EffectPoolManager.Instance?.DespawnEffect(effect);
            return;
        }

        Animator animator = effect.GetComponent<Animator>();
        if (animator != null && animator.runtimeAnimatorController != null)
        {
            animator.Play(0, 0, normalizedStartTime);
            animator.Update(0f);
        }
        ApplyEffectVisualTransform(effect, scale, facingRight);

        try
        {
            float elapsed = 0f;
            while (elapsed < duration && owner != null && owner.IsActionGenerationCurrent(generation))
            {
                if (await UniTask.Yield(PlayerLoopTiming.LastPostLateUpdate, cancellationToken).SuppressCancellationThrow()) break;
                ApplyEffectVisualTransform(effect, scale, facingRight);
                elapsed += Time.deltaTime;
            }
        }
        finally
        {
            EffectPoolManager.Instance?.DespawnEffect(effect);
        }
    }

    private static bool ApplyAttackSweep(UnitBase owner, UnitBase target, float damage,
        CombatStats.AttackSweep2D sweep)
    {
        if (target != null)
        {
            return ApplyAttackSweepToTarget(owner, target, damage, sweep);
        }
        if (!(owner is Player)) return false;

        bool hit = false;
        var targets = new List<Monster>(Monster.ActiveMonsters);
        foreach (Monster candidate in targets) hit |= ApplyAttackSweepToTarget(owner, candidate, damage, sweep);
        return hit;
    }

    private static bool ApplyAttackSweepToTarget(UnitBase owner, UnitBase target, float damage,
        CombatStats.AttackSweep2D sweep)
    {
        if (target == null || !target.isActiveAndEnabled || target.Stats == null || owner.Faction == target.Faction ||
            !target.Stats.TryGetAttackSweepFraction(sweep, out _)) return false;
        target.Stats.TakeDamage(damage, attacker: owner.Stats, attackOrigin: sweep.Previous, attackSweep: sweep);
        return true;
    }

    public bool TryPlaySkillAnimation(Animator animator, uint skillId)
    {
        var skillTable = DataTableManager.Instance != null ? DataTableManager.Instance.GetDB<SkillDataTable>(DataTableType.Skill) : null;
        if (skillTable == null || !skillTable.TryGetSkillData(skillId, out var skillData))
        {
            Debug.LogError($"[SkillExecutor Error] SkillId {skillId}에 대한 SkillData를 찾을 수 없습니다.");
            return false;
        }

        if (animator == null)
        {
            Debug.LogError($"[SkillExecutor Error] SkillId {skillId} 시전 대상 유닛의 Animator가 null입니다.");
            return false;
        }

        int targetState = skillData.AnimState > 0 ? skillData.AnimState : 7;

        bool hasStateParam = false;
        foreach (var param in animator.parameters)
        {
            if (param.name == "State" && param.type == AnimatorControllerParameterType.Int)
            {
                hasStateParam = true;
                break;
            }
        }

        if (!hasStateParam)
        {
            Debug.LogError($"[SkillExecutor Error] 유닛 '{animator.gameObject.name}'의 Animator에 'State' (int) 파라미터가 등록되어 있지 않습니다.");
            return false;
        }

        animator.SetInteger("State", targetState);
        return true;
    }

    public static AttackMotionProfileData ResolveAttackMotionProfile(SkillData skill, uint patternOverrideIdx = 0)
    {
        uint requestedIdx = ResolveAttackMotionProfileIdx(skill, patternOverrideIdx);
        var table = DataTableManager.Instance != null
            ? DataTableManager.Instance.GetDB<AttackMotionProfileDataTable>(DataTableType.AttackMotionProfile) : null;
        if (table != null && table.TryGetValid(requestedIdx, out AttackMotionProfileData profile)) return profile;
        if (requestedIdx != AttackMotionProfileDataTable.StationaryProfileIdx)
            Debug.LogWarning($"[SkillExecutor] Invalid AttackMotionProfile idx {requestedIdx}; Stationary fallback applied.");
        return new AttackMotionProfileData
        {
            Idx = AttackMotionProfileDataTable.StationaryProfileIdx,
            MotionType = AttackMotionType.Stationary,
            TargetPolicy = AttackTargetPolicy.SnapshotAtStartup,
            MaxDistance = 0f,
            MaxSpeed = 0f,
            Acceleration = 0f,
            Enabled = true
        };
    }

    public static uint ResolveAttackMotionProfileIdx(SkillData skill, uint patternOverrideIdx = 0) =>
        patternOverrideIdx != 0 ? patternOverrideIdx :
        skill != null && skill.AttackMotionProfileIdx != 0 ? skill.AttackMotionProfileIdx :
        AttackMotionProfileDataTable.StationaryProfileIdx;

    public static float CalculateAttackMotionVelocity(AttackMotionProfileData profile, float currentX,
        float targetX, float remainingSeconds, float currentVelocityX, float fixedDeltaTime)
    {
        if (profile == null || profile.MotionType == AttackMotionType.Stationary || remainingSeconds <= 0f)
            return 0f;
        float delta = targetX - currentX;
        if (Mathf.Approximately(delta, 0f)) return 0f;
        float required = Mathf.Min(profile.MaxSpeed, Mathf.Abs(delta) / remainingSeconds) * Mathf.Sign(delta);
        return profile.MotionType == AttackMotionType.Step ? required :
            Mathf.MoveTowards(currentVelocityX, required, profile.Acceleration * fixedDeltaTime);
    }

    public static float CalculateAttackAlignmentTargetX(float motionStartX, float ownerX, float targetCenterX,
        float targetVelocityX, float remainingSeconds, float targetHalfWidth, float attackCenterOffset,
        float skinWidth, float maxDistance, bool trackUntilActive)
    {
        float predictedCenterX = targetCenterX + (trackUntilActive ? targetVelocityX * Mathf.Max(0f, remainingSeconds) : 0f);
        float facing = predictedCenterX >= ownerX ? 1f : -1f;
        float signedCenterOffset = facing * Mathf.Abs(attackCenterOffset);
        float stopX = predictedCenterX - facing * (Mathf.Max(0f, targetHalfWidth) +
            Mathf.Max(0f, skinWidth)) - signedCenterOffset;
        return motionStartX + Mathf.Clamp(stopX - motionStartX, -Mathf.Max(0f, maxDistance), Mathf.Max(0f, maxDistance));
    }

    public float GetAttackRecoverySeconds(Animator animator, float animationStartedAt, float configuredRecovery)
    {
        if (animator == null || animator.runtimeAnimatorController == null) return Mathf.Max(0f, configuredRecovery);
        float elapsed = Mathf.Max(0f, Time.time - animationStartedAt);
        return CalculateAttackRecoverySeconds(animator.GetCurrentAnimatorStateInfo(0).length, elapsed, configuredRecovery);
    }

    public static float CalculateAttackRecoverySeconds(float animationLength, float elapsed, float configuredRecovery) =>
        Mathf.Max(Mathf.Max(0f, configuredRecovery), Mathf.Max(0f, animationLength - Mathf.Max(0f, elapsed)));

    public async UniTask<GameObject> SpawnEffectByEffectIdxAsync(uint effectIdx, Vector3 position,
        Quaternion rotation = default, float durationOverride = -1f)
    {
        if (effectIdx == 0) return null;

        var effectTable = DataTableManager.Instance != null ? DataTableManager.Instance.GetDB<EffectDataTable>(DataTableType.EffectData) : null;
        if (effectTable == null || !effectTable.TryGetEffectData(effectIdx, out var effectData))
        {
            Debug.LogWarning($"[SkillExecutor] EffectIdx {effectIdx}에 대한 EffectData를 찾을 수 없습니다.");
            return null;
        }

        var resourceTable = DataTableManager.Instance.GetDB<ResourceDataTable>(DataTableType.Resource);
        if (resourceTable == null || !resourceTable.TryGetResource(effectData.PrefabIdx, out var resourceData))
        {
            Debug.LogWarning($"[SkillExecutor] PrefabIdx {effectData.PrefabIdx}에 대한 ResourceData를 찾을 수 없습니다.");
            return null;
        }

        string prefabKey = resourceData.Path;
        if (string.IsNullOrEmpty(prefabKey))
        {
            Debug.LogWarning($"[SkillExecutor] PrefabIdx {effectData.PrefabIdx}의 에셋 경로가 비어있습니다.");
            return null;
        }

        float duration = durationOverride >= 0f ? durationOverride :
            (effectData.Duration > 0f ? effectData.Duration : 1.0f);
        if (EffectPoolManager.Instance != null)
        {
            GameObject effect = await EffectPoolManager.Instance.SpawnEffect(prefabKey, position, rotation, duration);
            ApplyEffectVisualTransform(effect, effectData.Scale, true);
            return effect;
        }

        return null;
    }

    private static void ApplyEffectVisualTransform(GameObject effect, float scale, bool facingRight)
    {
        SpriteRenderer visual = effect != null ? effect.GetComponentInChildren<SpriteRenderer>(true) : null;
        if (visual == null) return;
        visual.flipX = false;
        visual.transform.localScale = new Vector3((facingRight ? 1f : -1f) * scale, scale, 1f);
    }


    // =========================================================================
    // 3. PRIVATE METHODS
    // =========================================================================

    private void Awake()
    {
        if (Instance == null) Instance = this;
        stats = GetComponent<CombatStats>();
    }

    private void OnEnable()
    {
        StartParticleLoad();
    }

    private void StartParticleLoad()
    {
        if (particlePrefab != null || particleLoadPending || ResourceManager.Instance == null) return;
        uint generation = ++particleLoadGeneration;
        particleLoadPending = true;

        if (ResourceManager.Instance != null)
        {
            try
            {
                ResourceManager.Instance.LoadAssetAsync<GameObject>("Particle", prefab =>
                {
                    CompleteParticleLoad(generation, prefab);
                });
            }
            catch (System.Exception ex)
            {
                particleLoadPending = false;
                Debug.LogWarning($"[SkillExecutor] Addressable 'Particle' 키 로드 예외 발생: {ex.Message}");
            }
        }
    }

    private void CompleteParticleLoad(uint generation, GameObject prefab)
    {
        if (generation != particleLoadGeneration || !isActiveAndEnabled) return;
        particleLoadPending = false;
        if (prefab != null)
        {
            particlePrefab = prefab;
            return;
        }

        if (particleLoadFailureLogged) return;
        particleLoadFailureLogged = true;
        Debug.LogError("[ResourceManager Error] 'Particle' resource completed with null.");
    }

    private void OnDisable()
    {
        particleLoadGeneration++;
        particleLoadPending = false;
        CancelActiveEffects();
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        HideEffectBoundsDebug();
#endif
    }

    public void CancelActiveEffects()
    {
        if (activeSkillEffects.Count == 0) return;
        var effects = new List<SkillEffect>(activeSkillEffects);
        foreach (var effect in effects) if (effect != null) effect.ReturnToPool();
        activeSkillEffects.Clear();
    }

    private async UniTaskVoid CastAsync(SkillInfo skill, UnitBase caster, UnitBase target, CancellationToken cancellationToken)
    {
        if (skill.CastTime > 0f)
        {
            int castMs = Mathf.RoundToInt(skill.CastTime * 1000f);
            await UniTask.Delay(castMs, cancellationToken: cancellationToken);
        }

        var anim = caster.Animator;
        if (anim != null)
        {
            anim.SetInteger("State", skill.AnimState);
        }

        await ExecuteSkillHitsAsync((uint)skill.Id, caster, target,
            BaseDamage * skill.DamageMultiplier, cancellationToken);

        nextAvailable[skill.Id] = Time.time + skill.Cooldown;
    }

    private async UniTaskVoid ReleaseEffectAfterDurationAsync(string key, Transform instance, float duration, CancellationToken cancellationToken)
    {
        int durationMs = Mathf.RoundToInt(duration * 1000f);
        await UniTask.Delay(durationMs, cancellationToken: cancellationToken);

        if (instance != null && SimplePoolManager.Instance != null)
        {
            SimplePoolManager.Instance.Release(key, instance);
        }
    }
}

