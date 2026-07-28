using Cysharp.Threading.Tasks;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;

/// <summary>
/// 스킬 실행 및 이펙트 스폰 로직을 담당합니다.
/// SimplePoolManager를 통한 비동기 Addressables 오브젝트 풀링을 전면 적용합니다.
/// 언더스코어(_) 접두사 배제 규칙 및 this 키워드를 통한 멤버 접근 규칙을 준수합니다.
/// </summary>
public class SkillExecutor : MonoBehaviour
{
    // =========================================================================
    // 1. CONST & STATIC FIELDS
    // =========================================================================

    private const float BaseDamage = 10f; // 기본 데미지


    // =========================================================================
    // 2. PRIVATE FIELDS (camelCase, No '_' prefix)
    // =========================================================================

    private CombatStats stats;
    private GameObject particlePrefab;
    private readonly Dictionary<int, float> nextAvailable = new Dictionary<int, float>();


    // =========================================================================
    // 3. PUBLIC METHODS (PascalCase)
    // =========================================================================

    /// <summary>
    /// 스킬을 실행합니다.
    /// </summary>
    public void ExecuteSkill(int skillId, Transform caster, Transform target)
    {
        var skillTable = DataTableManager.Instance != null ? DataTableManager.Instance.GetDB<SkillDataTable>(DataTableType.Skill) : null;
        if (skillTable == null || !skillTable.TryGetSkill(skillId, out var skill))
        {
            Debug.LogWarning($"SkillExecutor: DataTableManager에서 Skill ID {skillId}를 찾을 수 없습니다.");
            return;
        }

        // 쿨다운 검사
        if (this.nextAvailable.TryGetValue(skillId, out var readyTime) && Time.time < readyTime)
        {
            return;
        }

        // MP 소비 검사
        if (!(this.stats?.ConsumeMp(skill.MpCost) ?? true))
        {
            Debug.Log("SkillExecutor: MP가 부족합니다.");
            return;
        }

        // 사거리 검사
        if (Vector2.Distance(caster.position, target.position) > skill.Range)
        {
            Debug.Log("SkillExecutor: 사거리를 벗어났습니다.");
            return;
        }

        // 시전 대기시간 (CastTime) 후 실제 실행 -> UniTask
        this.castAsync(skill, caster, target, this.GetCancellationTokenOnDestroy()).Forget();
    }


    // =========================================================================
    // 4. PRIVATE METHODS (camelCase)
    // =========================================================================

    private void Awake()
    {
        this.stats = GetComponent<CombatStats>();

        // ResourceManager를 사용하여 Addressable 파티클 프리팹 비동기 로드 (Key: "Particle")
        if (ResourceManager.Instance != null)
        {
            try
            {
                ResourceManager.Instance.LoadAssetAsync<GameObject>("Particle", prefab =>
                {
                    if (prefab != null)
                    {
                        this.particlePrefab = prefab;
                        Debug.Log("[SkillExecutor] ResourceManager를 통해 파티클 프리팹('Particle') 로드 완료.");
                    }
                });
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"[SkillExecutor] Addressable 'Particle' 키 로드 예외 발생, Fallback 로딩 시도: {ex.Message}");
            }
        }

        // Fallback 로딩 (Resources / Editor)
        if (this.particlePrefab == null)
        {
            this.particlePrefab = Resources.Load<GameObject>("prefabs/Particle");
#if UNITY_EDITOR
            if (this.particlePrefab == null)
            {
                this.particlePrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/prefabs/Particle.prefab");
            }
#endif
        }
    }

    private async UniTaskVoid castAsync(SkillInfo skill, Transform caster, Transform target, CancellationToken cancellationToken)
    {
        if (skill.CastTime > 0f)
        {
            int castMs = Mathf.RoundToInt(skill.CastTime * 1000f);
            await UniTask.Delay(castMs, cancellationToken: cancellationToken);
        }

        var anim = caster.GetComponent<Animator>();
        if (anim != null)
        {
            anim.Play(skill.AnimationClip);
        }

        // 파티클 생성 및 이펙트 처리
        if (this.particlePrefab != null)
        {
            var particle = Instantiate(this.particlePrefab, caster.position, Quaternion.identity);
            Destroy(particle, 1.5f);
        }

        // 데미지 적용
        var targetStats = target.GetComponent<CombatStats>();
        if (targetStats != null)
        {
            float dmg = BaseDamage * skill.DamageMultiplier;
            targetStats.TakeDamage(dmg, isGroundAttack: false, isJumped: false, attacker: this.stats);
        }

        // 쿨다운 설정
        this.nextAvailable[skill.Id] = Time.time + skill.Cooldown;
    }

    /// <summary>
    /// 모션 타이밍에 맞춰 독립 공격/방어 충돌 매개체(SkillEffect)를 동적으로 스폰합니다.
    /// </summary>
    public SkillEffect SpawnSkillEffect(string effectName, Vector3 position, Vector2 size, float damage, float lifetime, FactionType faction, Color color)
    {
        GameObject effectObj = new GameObject($"SkillEffect_{effectName}");
        effectObj.transform.position = position;

        var boxCol = effectObj.AddComponent<BoxCollider2D>();
        boxCol.isTrigger = true;
        boxCol.size = size;

        var effectComp = effectObj.AddComponent<SkillEffect>();
        effectComp.InitEffect(effectName, damage, lifetime, faction, this.stats, color);

        return effectComp;
    }

    /// <summary>
    /// skillId 기반으로 SkillData -> EffectData -> ResourceData -> SimplePoolManager 풀링 체인을 통해
    /// 스킬 이펙트를 풀에서 꺼내어 활성화합니다.
    /// </summary>
    public async UniTask<GameObject> SpawnSkillEffectFromDataAsync(uint skillId, Vector3 position, Quaternion rotation = default)
    {
        var skillTable = DataTableManager.Instance != null ? DataTableManager.Instance.GetDB<SkillDataTable>(DataTableType.Skill) : null;
        if (skillTable == null || !skillTable.TryGetSkillData(skillId, out var skillData))
        {
            Debug.LogWarning($"[SkillExecutor] SkillId {skillId}에 대한 SkillData를 찾을 수 없습니다.");
            return null;
        }

        if (skillData.EffectIdx == 0)
        {
            return null;
        }

        return await this.SpawnEffectByEffectIdxAsync(skillData.EffectIdx, position, rotation);
    }

    /// <summary>
    /// SkillData의 HitTimings에 따라 스킬 이펙트를 타임스탬프별로 순차 스폰합니다.
    /// </summary>
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
            await this.SpawnSkillEffectFromDataAsync(skillId, position, rotation);
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

            this.SpawnSkillEffectFromDataAsync(skillId, position, rotation).Forget();
        }
    }

    /// <summary>
    /// SkillData에서 지정한 AnimState (int) 값을 기반으로 Animator의 'State' 파라미터를 검사 및 재생합니다.
    /// 해당 State 파라미터가 유효하지 않거나 모션을 찾을 수 없으면 strict 에러를 발생시키고 false를 반환합니다.
    /// </summary>
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

        int targetState = skillData.AnimState > 0 ? skillData.AnimState : 7; // 기본값 7 (Attack)

        // Animator에 State (int) 파라미터가 존재하는지 검사
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
            Debug.LogError($"[SkillExecutor Error] 유닛 '{animator.gameObject.name}'의 Animator에 'State' (int) 파라미터가 등록되어 있지 않아 Skill {skillId} (State: {targetState}) 모션을 실행할 수 없습니다!");
            return false;
        }

        animator.SetInteger("State", targetState);
        return true;
    }


    /// <summary>
    /// EffectIdx를 받아 SimplePoolManager(오브젝트 풀링)를 통해 이펙트를 꺼내어 활성화하고,
    /// Duration 후 자동 Release(풀 반환)를 수행합니다.
    /// </summary>
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

        // 1. SimplePoolManager 풀 미생성 시 비동기 생성 및 프리웜 (여유있게 10개 프리웜)
        if (SimplePoolManager.Instance != null && !SimplePoolManager.Instance.TryGetPool<Transform>(prefabKey, out _))
        {
            await SimplePoolManager.Instance.CreatePoolAsync<Transform>(prefabKey, capacity: 30, prewarmCount: 10);
        }

        // 2. 오브젝트 풀에서 꺼내기 (Get)
        Transform pooledTrans = SimplePoolManager.Instance != null ? SimplePoolManager.Instance.Get<Transform>(prefabKey) : null;
        GameObject effectObj = null;

        if (pooledTrans != null)
        {
            effectObj = pooledTrans.gameObject;
        }
        else if (ResourceManager.Instance != null)
        {
            // 풀 고갈 시 비상 직생성 Fallback
            effectObj = await ResourceManager.Instance.InstantiateAsyncTask(prefabKey, null, position, rotation);
            if (effectObj != null) pooledTrans = effectObj.transform;
        }

        if (effectObj != null)
        {
            effectObj.transform.position = position;
            effectObj.transform.rotation = rotation;
            effectObj.transform.localScale = Vector3.one * (effectData.Scale > 0f ? effectData.Scale : 1f);

            // [핵심] 활성화 켜기 (SetActive true 보장)
            effectObj.SetActive(true);

            // 자식 ParticleSystem / Animator 가 있다면 재생 강제
            var psList = effectObj.GetComponentsInChildren<ParticleSystem>();
            foreach (var ps in psList)
            {
                ps.Clear();
                ps.Play();
            }

            // 3. 지정된 Duration 후 풀로 자동 반환 (Release)
            float duration = effectData.Duration > 0f ? effectData.Duration : 1.0f;
            this.releaseEffectAfterDurationAsync(prefabKey, pooledTrans, duration, this.GetCancellationTokenOnDestroy()).Forget();

            return effectObj;
        }

        return null;

    }

    private async UniTaskVoid releaseEffectAfterDurationAsync(string key, Transform instance, float duration, CancellationToken cancellationToken)
    {
        int durationMs = Mathf.RoundToInt(duration * 1000f);
        await UniTask.Delay(durationMs, cancellationToken: cancellationToken);

        if (instance != null && SimplePoolManager.Instance != null)
        {
            SimplePoolManager.Instance.Release(key, instance);
        }
    }
}
