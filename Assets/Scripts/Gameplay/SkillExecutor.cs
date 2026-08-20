using Cysharp.Threading.Tasks;
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
        float patternDamage, CancellationToken cancellationToken = default)
    {
        var table = DataTableManager.Instance != null
            ? DataTableManager.Instance.GetDB<SkillDataTable>(DataTableType.Skill) : null;
        if (owner == null || table == null ||
            !table.TryGetSkillData(skillId, out var skill) || skill.HitTimings == null || skill.HitTimings.Length == 0)
            return false;

        uint generation = owner.ActionGeneration;
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
                    await UniTask.Yield(PlayerLoopTiming.FixedUpdate, cancellationToken);
                    elapsed += Time.fixedDeltaTime;
                }
                if (!owner.IsActionGenerationCurrent(generation)) return true;

                if (!owner.TryOpenAttackHitbox(sourceId, generation, tick, out CombatStats.AttackSweep2D sweep))
                    return false;

                Vector3 attackEffectPos = sweep.Current;
                if (target != null && target.Stats != null)
                {
                    Collider2D targetCol = target.Stats.DefenseBodyCollider != null ? target.Stats.DefenseBodyCollider : target.GetComponent<Collider2D>();
                    if (targetCol != null) attackEffectPos = targetCol.ClosestPoint(sweep.Current);
                }
                SpawnSkillEffectFromDataAsync(skillId, attackEffectPos).Forget();

                ApplyAttackSweep(owner, target, patternDamage, sweep);

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
            owner.CloseAttackHitbox();
            owner.SetTelegraphedAttackHitbox(false);
        }
    }

    private static void ApplyAttackSweep(UnitBase owner, UnitBase target, float damage,
        CombatStats.AttackSweep2D sweep)
    {
        if (target != null)
        {
            ApplyAttackSweepToTarget(owner, target, damage, sweep);
            return;
        }
        if (!(owner is Player)) return;

        var targets = new List<Monster>(Monster.ActiveMonsters);
        foreach (Monster candidate in targets) ApplyAttackSweepToTarget(owner, candidate, damage, sweep);
    }

    private static void ApplyAttackSweepToTarget(UnitBase owner, UnitBase target, float damage,
        CombatStats.AttackSweep2D sweep)
    {
        if (target == null || !target.isActiveAndEnabled || target.Stats == null || owner.Faction == target.Faction ||
            !target.Stats.TryGetAttackSweepFraction(sweep, out _)) return;
        target.Stats.TakeDamage(damage, attacker: owner.Stats, attackOrigin: sweep.Previous, attackSweep: sweep);
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

