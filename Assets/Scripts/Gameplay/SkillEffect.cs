using UnityEngine;

/// <summary>
/// 유닛 본체의 Hurtbox와 독립된 공격 판정 전용 충돌 매개체 클래스.
/// SimplePoolManager와 연동하여 풀링을 지원하며, 
/// OnTriggerEnter2D 시 타겟 유닛의 패링/가드/데미지 판정을 수행합니다.
/// </summary>
public class SkillEffect : MonoBehaviour
{
    private string poolKey;
    private float damage;
    private float lifetime;
    private float timer;
    private FactionType ownerFaction;
    private CombatStats attackerStats;

    private Collider2D triggerCollider;
    private SpriteRenderer visualRenderer;

    private void Awake()
    {
        this.triggerCollider = GetComponent<Collider2D>();
        if (this.triggerCollider == null)
        {
            var box = gameObject.AddComponent<BoxCollider2D>();
            box.isTrigger = true;
            box.size = new Vector2(1.5f, 1.5f);
            this.triggerCollider = box;
        }

        // 시각화용 SpriteRenderer
        this.visualRenderer = GetComponent<SpriteRenderer>();
        if (this.visualRenderer == null)
        {
            this.visualRenderer = gameObject.AddComponent<SpriteRenderer>();
            this.visualRenderer.sortingOrder = 20;
        }
    }

    /// <summary>
    /// 풀링 또는 생성 시 매개체 초기화
    /// </summary>
    public void InitEffect(string poolKey, float damage, float lifetime, FactionType faction, CombatStats attacker, Color effectColor)
    {
        this.poolKey = poolKey;
        this.damage = damage;
        this.lifetime = lifetime;
        this.timer = 0f;
        this.ownerFaction = faction;
        this.attackerStats = attacker;

        if (this.visualRenderer != null)
        {
            // 더미 반투명 이펙트 시각화
            this.visualRenderer.color = effectColor;
        }

        gameObject.SetActive(true);
    }

    private void Update()
    {
        this.timer += Time.deltaTime;
        if (this.timer >= this.lifetime)
        {
            this.ReturnToPool();
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        // 피격 타겟 유닛 검사
        var targetUnit = other.GetComponentInParent<UnitBase>();
        if (targetUnit == null || targetUnit.UnitData == null) return;

        // 동일 피아 진영 간 공격 무효화
        if (targetUnit.UnitData.Faction == (uint)this.ownerFaction) return;

        var targetStats = targetUnit.GetComponent<CombatStats>();
        if (targetStats == null) return;

        // 1. 타겟 패링 타이밍 검사 (IsParrying)
        if (targetStats.IsParrying)
        {
            Debug.Log($"<color=magenta><b>[SkillEffect] '{targetUnit.gameObject.name}' 패링 성공! 공격 캔슬 및 피격 무효화!</b></color>");
            this.ReturnToPool();
            return;
        }

        // 2. 타겟 가드 유지 검사 (IsGuarding)
        if (targetStats.IsGuarding)
        {
            float guardDamage = this.damage * 0.2f; // 80% 데미지 감소
            targetStats.TakeDamage(guardDamage, isGroundAttack: false, isJumped: false, attacker: this.attackerStats);
            Debug.Log($"<color=yellow><b>[SkillEffect] '{targetUnit.gameObject.name}' 가드 성공! 경감 데미지 적용: {guardDamage:F1}</b></color>");
            this.ReturnToPool();
            return;
        }

        // 3. 평상시 완전 데미지 적용
        targetStats.TakeDamage(this.damage, isGroundAttack: false, isJumped: false, attacker: this.attackerStats);
        Debug.Log($"<color=red><b>[SkillEffect] '{targetUnit.gameObject.name}' 피격! 데미지: {this.damage:F1}</b></color>");

        // 1회성 근거리 매개체는 충돌 후 반환
        this.ReturnToPool();
    }

    public void ReturnToPool()
    {
        gameObject.SetActive(false);
        if (!string.IsNullOrEmpty(this.poolKey) && SimplePoolManager.Instance != null)
        {
            SimplePoolManager.Instance.Release<SkillEffect>(this.poolKey, this);
        }
    }
}
