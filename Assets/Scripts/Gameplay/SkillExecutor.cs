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

    public async UniTask<GameObject> SpawnSkillEffectFromDataAsync(uint skillId, Vector3 position, Quaternion rotation = default)
    {
        var skillTable = DataTableManager.Instance != null ? DataTableManager.Instance.GetDB<SkillDataTable>(DataTableType.Skill) : null;
        if (skillTable == null || !skillTable.TryGetSkillData(skillId, out var skillData))
        {
            Debug.LogWarning($"[SkillExecutor] SkillId {skillId}에 대한 SkillData를 찾을 수 없습니다.");
            return null;
        }

        if (skillData.EffectIdx == 0) return null;

        return await SpawnEffectByEffectIdxAsync(skillData.EffectIdx, position, rotation);
    }

    public async UniTaskVoid ExecuteSkillDataAsync(uint skillId, Vector3 position, Quaternion rotation = default, CancellationToken cancellationToken = default)
    {
        var skillTable = DataTableManager.Instance != null ? DataTableManager.Instance.GetDB<SkillDataTable>(DataTableType.Skill) : null;
        if (skillTable == null || !skillTable.TryGetSkillData(skillId, out var skillData))
        {
            Debug.LogWarning($"[SkillExecutor] SkillId {skillId}에 대한 SkillData를 찾을 수 없습니다.");
            return;
        }

        if (skillData.HitTimings == null || skillData.HitTimings.Length == 0)
        {
            await SpawnSkillEffectFromDataAsync(skillId, position, rotation);
            return;
        }

        float prevTiming = 0f;
        foreach (float timing in skillData.HitTimings)
        {
            float delay = timing - prevTiming;
            if (delay > 0f)
            {
                int delayMs = Mathf.RoundToInt(delay * 1000f);
                await UniTask.Delay(delayMs, cancellationToken: cancellationToken);
            }
            prevTiming = timing;

            SpawnSkillEffectFromDataAsync(skillId, position, rotation).Forget();
        }
    }

    public async UniTask<bool> ExecuteSkillHitsAsync(uint skillId, UnitBase owner, UnitBase target,
        float patternDamage, CancellationToken cancellationToken = default,
        uint attackMotionProfileOverrideIdx = 0, Func<bool> canStartWindow = null,
        Action onFirstSuccessfulHit = null)
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
        int sourceId = owner.GetInstanceID() ^ (int)skillId;
        float elapsed = 0f;
        try
        {
            owner.SetTelegraphedAttackHitbox(true);

            for (uint tick = 0; tick < (uint)skill.HitTimings.Length; tick++)
            {
                float t = Mathf.Max(0f, skill.HitTimings[(int)tick]);
                float windowStart = Mathf.Max(0f, t - skill.HitWindowPre);
                float windowEnd = t + skill.HitWindowPost;

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
                    float attackReach = owner.TryGetAttackForwardReach(facingRight, out float reach) ? reach : 0f;
                    float clampedTargetX = CalculateAttackAlignmentTargetX(motionStartX, owner.transform.position.x,
                        targetX, targetVelocityX, remaining, targetHalfWidth, attackReach,
                        owner.AttackMotionSkinWidth, motion.MaxDistance, tracking);
                    motionVelocityX = CalculateAttackMotionVelocity(motion, owner.transform.position.x,
                        clampedTargetX, remaining, motionVelocityX, Time.fixedDeltaTime);
                    if (!owner.IsActionGenerationCurrent(generation)) return true;
                    owner.SetAttackMotionStopPosition(clampedTargetX);
                    owner.SetAttackMotionVelocityX(motionVelocityX);
                    await UniTask.Yield(PlayerLoopTiming.FixedUpdate, cancellationToken);
                    elapsed += Time.fixedDeltaTime;
                }
                owner.StopAttackMotionImmediately();
                if (!owner.IsActionGenerationCurrent(generation)) return true;
                if (canStartWindow != null && !canStartWindow()) return false;

                if (!owner.TryOpenAttackHitbox(sourceId, generation, tick, out CombatStats.AttackSweep2D sweep))
                    return false;

                Vector3 attackEffectPos = sweep.Current;
                if (target != null && target.Stats != null)
                {
                    Collider2D targetCol = target.Stats.DefenseBodyCollider != null ? target.Stats.DefenseBodyCollider : target.GetComponent<Collider2D>();
                    if (targetCol != null) attackEffectPos = targetCol.ClosestPoint(sweep.Current);
                }
                SpawnSkillEffectFromDataAsync(skillId, attackEffectPos).Forget();

                if (ApplyAttackSweep(owner, target, patternDamage, sweep)) onFirstSuccessfulHit?.Invoke();

                while (elapsed + Mathf.Epsilon < windowEnd)
                {
                    await UniTask.Yield(PlayerLoopTiming.FixedUpdate, cancellationToken);
                    elapsed += Time.fixedDeltaTime;
                    if (!owner.IsActionGenerationCurrent(generation)) return true;
                }

                owner.CloseAttackHitbox();
                owner.SetTelegraphedAttackHitbox(true);
            }
            return true;
        }
        finally
        {
            owner.StopAttackMotionImmediately();
            owner.CloseAttackHitbox();
            owner.SetTelegraphedAttackHitbox(false);
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
        float targetVelocityX, float remainingSeconds, float targetHalfWidth, float attackReach,
        float skinWidth, float maxDistance, bool trackUntilActive)
    {
        float predictedCenterX = targetCenterX + (trackUntilActive ? targetVelocityX * Mathf.Max(0f, remainingSeconds) : 0f);
        float facing = predictedCenterX >= ownerX ? 1f : -1f;
        float stopX = predictedCenterX - facing * (Mathf.Max(0f, targetHalfWidth) +
            Mathf.Max(0f, attackReach) + Mathf.Max(0f, skinWidth));
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

    public async UniTask<GameObject> SpawnEffectByEffectIdxAsync(uint effectIdx, Vector3 position, Quaternion rotation = default)
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

        float duration = effectData.Duration > 0f ? effectData.Duration : 1.0f;
        if (EffectPoolManager.Instance != null)
        {
            return await EffectPoolManager.Instance.SpawnEffect(prefabKey, position, rotation, duration);
        }

        return null;
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

