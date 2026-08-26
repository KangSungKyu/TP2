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

    private sealed class MotionContext
    {
        public float StartX;
        public float EndpointX;
        public float VelocityX;
        public float MovedDistance;
        public uint Generation;
        public bool FacingRight;
        public bool Blocked;
    }

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
        Action onFirstSuccessfulHit = null, uint attackPatternIdx = 0,
        Vector2? initialExteriorPose = null, Func<float> getRecoverySeconds = null)
    {
        var table = DataTableManager.Instance != null
            ? DataTableManager.Instance.GetDB<SkillDataTable>(DataTableType.Skill) : null;
        if (owner == null || table == null ||
            !table.TryGetSkillData(skillId, out var skill) || skill.HitTimings == null || skill.HitTimings.Length == 0)
            return false;

        uint generation = owner.ActionGeneration;
        bool facingSnapshot = owner.IsFacingRight;
        AttackMotionProfileData motion = ResolveAttackMotionProfile(skill, attackMotionProfileOverrideIdx);
        bool overshootTarget = skill.AttackSubject == AttackSubject.BodyPart &&
            skill.BodyPartRole == BodyPartRole.Torso;
        float motionStartX = owner.transform.position.x;
        Collider2D ownerBody = owner.Stats != null ? owner.Stats.DefenseBodyCollider : null;
        float ownerSnapshotHalfWidth = ownerBody != null ? ownerBody.bounds.extents.x : 0f;
        Collider2D targetBody = target != null && target.Stats != null ? target.Stats.DefenseBodyCollider : null;
        float targetSnapshotX = targetBody != null ? targetBody.bounds.center.x : target != null ? target.transform.position.x : motionStartX;
        float targetSnapshotHalfWidth = targetBody != null ? targetBody.bounds.extents.x : 0f;
        int sourceId = unchecked(owner.GetInstanceID() ^ (int)skillId ^ ((int)attackPatternIdx * 397));
        float elapsed = -Mathf.Max(0f, skill.AttackMotionTime);
        float firstWindowStart = Mathf.Max(0f, skill.HitTimings[0] - skill.HitWindowPre);
        float lastWindowEnd = skill.HitTimings[skill.HitTimings.Length - 1] + skill.HitWindowPost;
        var effectTable = DataTableManager.Instance.GetDB<EffectDataTable>(DataTableType.EffectData);
        var resourceTable = DataTableManager.Instance.GetDB<ResourceDataTable>(DataTableType.Resource);
        var attackEffects = new EffectData[skill.HitTimings.Length];
        var spawnedTicks = new HashSet<(uint ownerUnitIdx, uint generation, uint hitTick, uint effectIdx)>();

        for (uint tick = 0; tick < (uint)attackEffects.Length; tick++)
        {
            if (effectTable == null || !effectTable.TryResolveAttackEffect(
                owner.UnitIdx, attackPatternIdx, skillId, tick, out EffectData effect))
            {
                Debug.LogError($"[SkillExecutor] Unit {owner.UnitIdx}/Pattern {attackPatternIdx}/Skill {skillId} has no EffectData attack bounds; tick cancelled.");
                return false;
            }
            if (!effect.HasValidActiveBounds)
            {
                Debug.LogError($"[SkillExecutor] Unit {owner.UnitIdx}/Pattern {attackPatternIdx}/Skill {skillId} " +
                    $"has invalid EffectData {effect.Idx} active bounds; attack cancelled.");
                return false;
            }
            if (resourceTable == null || !resourceTable.TryGetResource(effect.PrefabIdx, out ResourceData resource) ||
                string.IsNullOrEmpty(resource.Path))
            {
                Debug.LogError($"[SkillExecutor] Attack EffectData {effect.Idx} has invalid ResourceData FK " +
                    $"{effect.PrefabIdx}; attack cancelled.");
                return false;
            }

            attackEffects[tick] = effect;
        }

        if (overshootTarget)
        {
            if (ownerBody == null || !ownerBody.enabled || targetBody == null || !targetBody.enabled)
            {
                Debug.LogError($"[SkillExecutor] Unit {owner.UnitIdx}/Skill {skillId} torso motion requires active owner and target body colliders.");
                return false;
            }
            bool previousFacing = facingSnapshot;
            float deltaX = targetBody.bounds.center.x - ownerBody.bounds.center.x;
            float directionEpsilon = Mathf.Max(owner.AttackMotionSkinWidth, Physics2D.defaultContactOffset);
            if (Mathf.Abs(deltaX) > directionEpsilon) facingSnapshot = deltaX > 0f;
            owner.SetFacingRight(facingSnapshot);
            if (facingSnapshot != previousFacing) initialExteriorPose = null;
            targetSnapshotX = targetBody.bounds.center.x;
            targetSnapshotHalfWidth = targetBody.bounds.extents.x;
        }
        float facingSnapshotSign = facingSnapshot ? 1f : -1f;

        for (uint tick = 0; tick < (uint)attackEffects.Length; tick++)
        {
            EffectData effect = attackEffects[tick];
            bool deferredWildcard = effect.HitTick == 0u && attackEffects.Length > 1;
            if (!deferredWildcard && spawnedTicks.Add((owner.UnitIdx, generation, tick, effect.Idx)))
                SpawnAttackEffectForWindowAsync(effect, owner, generation, facingSnapshot,
                    effect.Duration, cancellationToken).Forget();
        }

        float? lockedOvershootTargetX = null;
        if (overshootTarget)
        {
            EffectData firstEffect = attackEffects[0];
            float attackCenterOffset = facingSnapshotSign *
                (firstEffect.SpawnPivotX + firstEffect.ActiveCenterX * firstEffect.Scale);
            lockedOvershootTargetX = CalculateAttackAlignmentTargetX(motionStartX, motionStartX,
                targetSnapshotX, 0f, 0f, targetSnapshotHalfWidth, attackCenterOffset,
                owner.AttackMotionSkinWidth, motion.MaxDistance, false, true,
                ownerSnapshotHalfWidth, facingSnapshotSign);
        }

        EffectData alignmentEffect = attackEffects[0];
        float alignmentOffset = facingSnapshotSign *
            (alignmentEffect.SpawnPivotX + alignmentEffect.ActiveCenterX * alignmentEffect.Scale);
        var motionContext = new MotionContext
        {
            StartX = motionStartX,
            EndpointX = lockedOvershootTargetX ?? CalculateAttackAlignmentTargetX(
                motionStartX, motionStartX, targetSnapshotX, 0f, 0f, targetSnapshotHalfWidth,
                alignmentOffset, owner.AttackMotionSkinWidth, motion.MaxDistance, false),
            Generation = generation,
            FacingRight = facingSnapshot
        };
        if (skill.MotionPhaseMask != SkillMotionPhase.None &&
            (motion == null || motion.MotionType == AttackMotionType.Stationary || !motion.Enabled))
            Debug.LogWarning($"[SkillExecutor] Skill {skillId} has motion phases but resolves to Stationary; movement disabled.");

        try
        {
            for (uint tick = 0; tick < (uint)skill.HitTimings.Length; tick++)
            {
                EffectData attackEffectData = attackEffects[tick];
                uint attackEffectIdx = attackEffectData.Idx;
                float t = Mathf.Max(0f, skill.HitTimings[(int)tick]);
                float windowStart = Mathf.Max(0f, t - skill.HitWindowPre);
                float windowEnd = t + skill.HitWindowPost;
                Vector2? exteriorPose = tick == 0u ? initialExteriorPose : null;
                bool exteriorLocked = false;
                bool tickHitCommitted = false;
                if (!TryCalculateEffectPoseForFacing(owner, attackEffectData, facingSnapshot,
                    out Vector2 sampledPose, out Quaternion effectRotation)) return false;
                TryUpdateExteriorPose(target, owner, attackEffectData, facingSnapshot, sampledPose,
                    sourceId, generation, tick, ref exteriorPose, ref exteriorLocked);

                while (elapsed + Mathf.Epsilon < windowStart)
                {
                    if (overshootTarget && owner.IsFacingRight != facingSnapshot)
                        owner.SetFacingRight(facingSnapshot);
                    SkillMotionPhase phase = elapsed < 0f ? SkillMotionPhase.AttackMotion :
                        elapsed < firstWindowStart ? SkillMotionPhase.Pre : SkillMotionPhase.Active;
                    bool phaseMoves = IsMotionPhaseEnabled(skill.MotionPhaseMask, phase);
                    float remaining = phase == SkillMotionPhase.AttackMotion &&
                        IsMotionPhaseEnabled(skill.MotionPhaseMask, SkillMotionPhase.Pre)
                        ? firstWindowStart - elapsed
                        : phase == SkillMotionPhase.AttackMotion ? -elapsed
                        : phase == SkillMotionPhase.Pre ? firstWindowStart - elapsed
                        : lastWindowEnd - elapsed;
                    if (!owner.IsActionGenerationCurrent(generation)) return true;
                    if (!PrepareMotionStep(owner, motion, motionContext, skill.MotionPhaseMask,
                        phase, remaining)) return false;
                    float previousOwnerX = owner.transform.position.x;
                    Vector2 previousSampledPose = sampledPose;
                    await UniTask.Yield(PlayerLoopTiming.FixedUpdate, cancellationToken);
                    elapsed += Time.fixedDeltaTime;
                    CompleteMotionStep(motionContext, previousOwnerX, owner.transform.position.x);
                    if (overshootTarget && owner.IsFacingRight != facingSnapshot)
                        owner.SetFacingRight(facingSnapshot);
                    if (owner.IsFacingRight != facingSnapshot)
                    {
                        exteriorPose = null;
                        exteriorLocked = true;
                    }
                    if (!TryCalculateEffectPoseForFacing(owner, attackEffectData, facingSnapshot,
                        out sampledPose, out _)) return false;
                    if (overshootTarget && phaseMoves && phase != SkillMotionPhase.Active &&
                        TryCreateEffectSweepForFacing(owner, attackEffectData, previousSampledPose,
                            sourceId, generation, tick, facingSnapshot, exteriorPose.HasValue,
                            out CombatStats.AttackSweep2D motionSweep, out _))
                    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                        DrawEffectBoundsDebug(owner, motionSweep);
#endif
                        if (!tickHitCommitted)
                        {
                            tickHitCommitted = ApplyAttackSweep(owner, patternDamage, motionSweep);
                            if (tickHitCommitted) onFirstSuccessfulHit?.Invoke();
                        }
                    }
                    TryUpdateExteriorPose(target, owner, attackEffectData, facingSnapshot, sampledPose,
                        sourceId, generation, tick, ref exteriorPose, ref exteriorLocked);
                }
                if (!IsMotionPhaseEnabled(skill.MotionPhaseMask, SkillMotionPhase.Active))
                {
                    owner.StopAttackMotionImmediately();
                    motionContext.VelocityX = 0f;
                }
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                if (overshootTarget) HideEffectBoundsDebug();
#endif
                if (overshootTarget && owner.IsFacingRight != facingSnapshot)
                    owner.SetFacingRight(facingSnapshot);
                if (!owner.IsActionGenerationCurrent(generation)) return true;
                if (canStartWindow != null && !canStartWindow()) return false;

                Vector2 activePrevious = overshootTarget ? sampledPose : exteriorPose ?? sampledPose;
                if (!TryCreateEffectSweepForFacing(owner, attackEffectData, activePrevious,
                    sourceId, generation, tick, facingSnapshot, !overshootTarget && exteriorPose.HasValue,
                    out CombatStats.AttackSweep2D sweep, out effectRotation)) return false;
                Vector2 previousEffectCenter = sweep.Current;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                DrawEffectBoundsDebug(owner, sweep);
#endif

                if (!overshootTarget || IsMotionPhaseEnabled(skill.MotionPhaseMask, SkillMotionPhase.Active))
                {
                    tickHitCommitted = ApplyAttackSweep(owner, patternDamage, sweep);
                    if (tickHitCommitted) onFirstSuccessfulHit?.Invoke();
                }

                bool deferredWildcard = attackEffectData.HitTick == 0u && attackEffects.Length > 1;
                if (deferredWildcard && spawnedTicks.Add((owner.UnitIdx, generation, tick, attackEffectIdx)))
                {
                    float visualEnd = Mathf.Max(lastWindowEnd, windowStart + attackEffectData.Duration);
                    SpawnAttackEffectForWindowAsync(attackEffectData, owner, generation, facingSnapshot,
                        Mathf.Max(0f, visualEnd - elapsed),
                        cancellationToken).Forget();
                }

                while (elapsed + Mathf.Epsilon < windowEnd)
                {
                    if (!PrepareMotionStep(owner, motion, motionContext, skill.MotionPhaseMask,
                        SkillMotionPhase.Active, lastWindowEnd - elapsed)) return false;
                    float previousOwnerX = owner.transform.position.x;
                    await UniTask.Yield(PlayerLoopTiming.FixedUpdate, cancellationToken);
                    elapsed += Time.fixedDeltaTime;
                    CompleteMotionStep(motionContext, previousOwnerX, owner.transform.position.x);
                    if (!owner.IsActionGenerationCurrent(generation)) return true;
                    if (!TryCreateEffectSweepForFacing(owner, attackEffectData, previousEffectCenter,
                        sourceId, generation, tick, facingSnapshot, true,
                        out CombatStats.AttackSweep2D movedSweep, out _)) return false;
                    previousEffectCenter = movedSweep.Current;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                    DrawEffectBoundsDebug(owner, movedSweep);
#endif
                    if ((!overshootTarget || IsMotionPhaseEnabled(skill.MotionPhaseMask, SkillMotionPhase.Active)) &&
                        !tickHitCommitted &&
                        ApplyAttackSweep(owner, patternDamage, movedSweep))
                    {
                        tickHitCommitted = true;
                        onFirstSuccessfulHit?.Invoke();
                    }
                }
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                HideEffectBoundsDebug();
#endif

            }
            float recoverySeconds = Mathf.Max(0f, getRecoverySeconds?.Invoke() ?? 0f);
            float recoveryElapsed = 0f;
            while (recoveryElapsed + Mathf.Epsilon < recoverySeconds)
            {
                if (!owner.IsActionGenerationCurrent(generation)) return true;
                if (!PrepareMotionStep(owner, motion, motionContext, skill.MotionPhaseMask,
                    SkillMotionPhase.Post, recoverySeconds - recoveryElapsed)) return false;
                float previousOwnerX = owner.transform.position.x;
                await UniTask.Yield(PlayerLoopTiming.FixedUpdate, cancellationToken);
                recoveryElapsed += Time.fixedDeltaTime;
                CompleteMotionStep(motionContext, previousOwnerX, owner.transform.position.x);
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
        => TryCalculateEffectPoseForFacing(owner, effect, owner != null && owner.IsFacingRight,
            out position, out rotation);

    private static bool TryCalculateEffectPoseForFacing(UnitBase owner, EffectData effect, bool facingRight,
        out Vector2 position, out Quaternion rotation)
    {
        position = default;
        rotation = owner != null ? owner.transform.rotation : Quaternion.identity;
        Collider2D body = owner != null && owner.Stats != null ? owner.Stats.DefenseBodyCollider : null;
        if (body == null || !body.enabled || !body.gameObject.activeInHierarchy)
        {
            Debug.LogError($"[SkillExecutor] Unit {(owner != null ? owner.UnitIdx : 0u)} has no active DefenseBodyCollider; attack tick cancelled.");
            return false;
        }
        if (!effect.HasValidSpawnPivot)
        {
            Debug.LogError($"[SkillExecutor] EffectData {effect.Idx} has invalid SpawnPivot; attack tick cancelled.");
            return false;
        }
        if (!float.IsFinite(effect.Scale) || effect.Scale <= 0f)
        {
            Debug.LogError($"[SkillExecutor] EffectData {effect.Idx} has invalid Scale {effect.Scale}; attack tick cancelled.");
            return false;
        }
        float faceSign = facingRight ? 1f : -1f;
        Vector2 local = new Vector2(faceSign * effect.SpawnPivotX, effect.SpawnPivotY) +
            new Vector2(faceSign * effect.ActiveCenterX, effect.ActiveCenterY) * effect.Scale;
        position = (Vector2)body.bounds.center + (Vector2)(rotation * (Vector3)local);
        return true;
    }

    private static bool TryCalculateEffectVisualPose(UnitBase owner, EffectData effect, out Vector2 position,
        out Quaternion rotation)
        => TryCalculateEffectVisualPoseForFacing(owner, effect, owner != null && owner.IsFacingRight,
            out position, out rotation);

    private static bool TryCalculateEffectVisualPoseForFacing(UnitBase owner, EffectData effect, bool facingRight,
        out Vector2 position, out Quaternion rotation)
    {
        if (!TryCalculateEffectPoseForFacing(owner, effect, facingRight, out position, out rotation)) return false;
        float faceSign = facingRight ? 1f : -1f;
        Vector2 activeCenter = new Vector2(faceSign * effect.ActiveCenterX, effect.ActiveCenterY) * effect.Scale;
        position -= (Vector2)(rotation * (Vector3)activeCenter);
        return true;
    }

    private static bool TryCreateEffectSweep(UnitBase owner, EffectData effect, Vector2 previous,
        int sourceId, uint generation, uint tick, out CombatStats.AttackSweep2D sweep,
        out Quaternion rotation)
        => TryCreateEffectSweepForFacing(owner, effect, previous, sourceId, generation, tick,
            owner != null && owner.IsFacingRight, false, out sweep, out rotation);

    private static bool TryCreateEffectSweepForFacing(UnitBase owner, EffectData effect, Vector2 previous,
        int sourceId, uint generation, uint tick, bool facingRight, bool hasExteriorPose,
        out CombatStats.AttackSweep2D sweep, out Quaternion rotation)
    {
        sweep = default;
        if (!TryCalculateEffectPoseForFacing(owner, effect, facingRight, out Vector2 current, out rotation)) return false;
        Vector2 size = new Vector2(effect.ActiveSizeX, effect.ActiveSizeY) * effect.Scale;
        float angle = rotation.eulerAngles.z;
        float radians = angle * Mathf.Deg2Rad;
        float c = Mathf.Abs(Mathf.Cos(radians));
        float s = Mathf.Abs(Mathf.Sin(radians));
        Vector2 half = size * .5f;
        Vector2 extents = effect.Shape == ActiveShape.Circle ? Vector2.one * half.x :
            new Vector2(c * half.x + s * half.y, s * half.x + c * half.y);
        sweep = new CombatStats.AttackSweep2D(previous, current, extents, sourceId, generation, tick,
            effect.Shape, size, angle, hasExteriorPose);
        return true;
    }

    private static void TryUpdateExteriorPose(UnitBase target, UnitBase owner, EffectData effect,
        bool facingRight, Vector2 sampledPose, int sourceId, uint generation, uint tick,
        ref Vector2? exteriorPose, ref bool locked)
    {
        if (locked || target == null || target.Stats == null) return;
        if (!TryCreateEffectSweepForFacing(owner, effect, sampledPose, sourceId, generation, tick,
            facingRight, false, out CombatStats.AttackSweep2D sample, out _))
        {
            locked = true;
            exteriorPose = null;
            return;
        }
        if (target.Stats.TryGetAttackSweepFraction(sample, out _))
        {
            locked = true;
            return;
        }
        exteriorPose = sampledPose;
    }

    internal static bool TrySampleNonContactEffectPose(UnitBase owner, UnitBase target, EffectData effect,
        bool facingRight, out Vector2 pose, out bool contacted)
    {
        contacted = false;
        if (!TryCalculateEffectPoseForFacing(owner, effect, facingRight, out pose, out _)) return false;
        if (target == null || target.Stats == null) return true;
        if (!TryCreateEffectSweepForFacing(owner, effect, pose, 0, owner.ActionGeneration, 0u,
            facingRight, false, out CombatStats.AttackSweep2D sample, out _)) return false;
        contacted = target.Stats.TryGetAttackSweepFraction(sample, out _);
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

    private async UniTask SpawnAttackEffectForWindowAsync(EffectData effectData,
        UnitBase owner, uint generation, bool facingRight, float duration,
        CancellationToken cancellationToken)
    {
        if (!TryCalculateEffectVisualPoseForFacing(owner, effectData, facingRight,
            out Vector2 position, out Quaternion rotation)) return;
        GameObject effect = await SpawnEffectByEffectIdxAsync(effectData.Idx, position, rotation, duration);
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
            animator.Play(0, 0, 0f);
            animator.Update(0f);
        }
        ApplyEffectVisualTransform(effect, effectData.Scale, facingRight);

        try
        {
            float elapsed = 0f;
            while (elapsed < duration && owner != null && owner.IsActionGenerationCurrent(generation))
            {
                if (await UniTask.Yield(PlayerLoopTiming.LastPostLateUpdate, cancellationToken).SuppressCancellationThrow()) break;
                if (!TryCalculateEffectVisualPoseForFacing(owner, effectData, facingRight,
                    out position, out rotation)) break;
                effect.transform.SetPositionAndRotation(position, rotation);
                ApplyEffectVisualTransform(effect, effectData.Scale, facingRight);
                elapsed += Time.deltaTime;
            }
        }
        finally
        {
            EffectPoolManager.Instance?.DespawnEffect(effect);
        }
    }

    private static bool ApplyAttackSweep(UnitBase owner, float damage, CombatStats.AttackSweep2D sweep)
    {
        var targets = new List<UnitBase>();
        CombatStats.CollectAttackSweepVictims(owner, sweep, targets);
        bool hit = false;
        foreach (UnitBase target in targets)
        {
            target.Stats.TakeDamage(damage, attacker: owner.Stats, attackOrigin: sweep.Previous,
                attackSweep: sweep);
            hit = true;
        }
        return hit;
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

    internal static bool IsMotionPhaseEnabled(SkillMotionPhase mask, SkillMotionPhase phase) =>
        (mask & phase) != 0;

    private static bool PrepareMotionStep(UnitBase owner, AttackMotionProfileData profile,
        MotionContext context, SkillMotionPhase mask, SkillMotionPhase phase, float remainingSeconds)
    {
        if (!IsMotionPhaseEnabled(mask, phase) || profile == null || !profile.Enabled ||
            profile.MotionType == AttackMotionType.Stationary)
        {
            owner.StopAttackMotionImmediately();
            context.VelocityX = 0f;
            return true;
        }

        float remainingDistance = Mathf.Max(0f, profile.MaxDistance - context.MovedDistance);
        if (remainingDistance <= Mathf.Epsilon)
        {
            owner.StopAttackMotionImmediately();
            context.VelocityX = 0f;
            return true;
        }
        context.VelocityX = CalculateAttackMotionVelocity(profile, owner.transform.position.x,
            context.EndpointX, remainingSeconds, context.VelocityX, Time.fixedDeltaTime);
        float maxVelocity = remainingDistance / Mathf.Max(Time.fixedDeltaTime, Mathf.Epsilon);
        context.VelocityX = Mathf.Clamp(context.VelocityX, -maxVelocity, maxVelocity);
        if (Mathf.Approximately(context.VelocityX, 0f)) return true;
        if (!owner.HasGroundSupportForAttackStep(context.VelocityX * Time.fixedDeltaTime) ||
            !owner.IsAttackMotionPositionAllowed(context.EndpointX) ||
            owner.IsAttackMotionBlocked(context.VelocityX))
        {
            context.Blocked = true;
            owner.StopAttackMotionImmediately();
            context.VelocityX = 0f;
            return false;
        }
        owner.SetAttackMotionStopPosition(context.EndpointX);
        owner.SetAttackMotionVelocityX(context.VelocityX);
        return true;
    }

    private static void CompleteMotionStep(MotionContext context, float previousX, float currentX) =>
        context.MovedDistance += Mathf.Abs(currentX - previousX);

    public static float CalculateAttackAlignmentTargetX(float motionStartX, float ownerX, float targetCenterX,
        float targetVelocityX, float remainingSeconds, float targetHalfWidth, float attackCenterOffset,
        float skinWidth, float maxDistance, bool trackUntilActive, bool overshootTarget = false,
        float ownerHalfWidth = 0f, float fixedFacingSign = 0f)
    {
        float predictedCenterX = targetCenterX + (trackUntilActive ? targetVelocityX * Mathf.Max(0f, remainingSeconds) : 0f);
        float facing = Mathf.Approximately(fixedFacingSign, 0f)
            ? (predictedCenterX >= ownerX ? 1f : -1f)
            : Mathf.Sign(fixedFacingSign);
        float signedCenterOffset = facing * Mathf.Abs(attackCenterOffset);
        float stopX = overshootTarget
            ? predictedCenterX + facing * (Mathf.Max(0f, targetHalfWidth) + Mathf.Max(0f, ownerHalfWidth))
            : predictedCenterX - facing * (Mathf.Max(0f, targetHalfWidth) + Mathf.Max(0f, skinWidth)) -
              signedCenterOffset;
        float limit = Mathf.Max(0f, maxDistance);
        return motionStartX + Mathf.Clamp(stopX - motionStartX, -limit, limit);
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

