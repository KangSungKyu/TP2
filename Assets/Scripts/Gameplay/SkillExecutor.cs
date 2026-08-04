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
    private readonly Dictionary<int, float> nextAvailable = new Dictionary<int, float>();

    // =========================================================================
    // 2. PUBLIC METHODS (PascalCase)
    // =========================================================================

    public void ExecuteSkill(int skillId, Transform caster, Transform target)
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

        if (Vector2.Distance(caster.position, target.position) > skill.Range)
        {
            Debug.Log("SkillExecutor: 사거리를 벗어났습니다.");
            return;
        }

        CastAsync(skill, caster, target, this.GetCancellationTokenOnDestroy()).Forget();
    }

    public SkillEffect SpawnSkillEffect(string effectName, Vector3 position, Vector2 size, float damage, float lifetime, FactionType faction, Color color)
    {
        GameObject effectObj = new GameObject($"SkillEffect_{effectName}");
        effectObj.transform.position = position;

        var boxCol = effectObj.AddComponent<BoxCollider2D>();
        boxCol.isTrigger = true;
        boxCol.size = size;

        var effectComp = effectObj.AddComponent<SkillEffect>();
        effectComp.InitEffect(effectName, damage, lifetime, faction, stats, color);

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

        if (SimplePoolManager.Instance != null && !SimplePoolManager.Instance.TryGetPool<Transform>(prefabKey, out _))
        {
            await SimplePoolManager.Instance.CreatePoolAsync<Transform>(prefabKey, capacity: 30, prewarmCount: 10);
        }

        Transform pooledTrans = SimplePoolManager.Instance != null ? SimplePoolManager.Instance.Get<Transform>(prefabKey) : null;
        GameObject effectObj = null;

        if (pooledTrans != null)
        {
            effectObj = pooledTrans.gameObject;
        }
        else if (ResourceManager.Instance != null)
        {
            effectObj = await ResourceManager.Instance.InstantiateAsyncTask(prefabKey, null, position, rotation);
            if (effectObj != null) pooledTrans = effectObj.transform;
        }

        if (effectObj != null)
        {
            effectObj.transform.position = position;
            effectObj.transform.rotation = rotation;
            effectObj.transform.localScale = Vector3.one * (effectData.Scale > 0f ? effectData.Scale : 1f);
            effectObj.SetActive(true);

            var psList = effectObj.GetComponentsInChildren<ParticleSystem>();
            foreach (var ps in psList)
            {
                ps.Clear();
                ps.Play();
            }

            float duration = effectData.Duration > 0f ? effectData.Duration : 1.0f;
            ReleaseEffectAfterDurationAsync(prefabKey, pooledTrans, duration, this.GetCancellationTokenOnDestroy()).Forget();

            return effectObj;
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

        if (ResourceManager.Instance != null)
        {
            try
            {
                ResourceManager.Instance.LoadAssetAsync<GameObject>("Particle", prefab =>
                {
                    if (prefab != null)
                    {
                        particlePrefab = prefab;
                        Debug.Log("[SkillExecutor] ResourceManager를 통해 파티클 프리팹('Particle') 로드 완료.");
                    }
                });
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"[SkillExecutor] Addressable 'Particle' 키 로드 예외 발생: {ex.Message}");
            }
        }

        if (particlePrefab == null)
        {
            particlePrefab = Resources.Load<GameObject>("prefabs/Particle");
#if UNITY_EDITOR
            if (particlePrefab == null)
            {
                particlePrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/prefabs/Particle.prefab");
            }
#endif
        }
    }

    private async UniTaskVoid CastAsync(SkillInfo skill, Transform caster, Transform target, CancellationToken cancellationToken)
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

        if (particlePrefab != null)
        {
            var particle = Instantiate(particlePrefab, caster.position, Quaternion.identity);
            Destroy(particle, 1.5f);
        }

        var targetStats = target.GetComponent<CombatStats>();
        if (targetStats != null)
        {
            float dmg = BaseDamage * skill.DamageMultiplier;
            targetStats.TakeDamage(dmg, isGroundAttack: false, isJumped: false, attacker: stats);
        }

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

