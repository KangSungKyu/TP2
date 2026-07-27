using Cysharp.Threading.Tasks;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;

/// <summary>
/// 스킬 실행 로직을 담당합니다.
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
        /// SimplePoolManager를 활용하여 풀링 생성합니다.
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
    }

