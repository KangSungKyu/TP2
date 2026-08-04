using UnityEngine;

/// <summary>
/// 유닛 본체의 Hurtbox와 독립된 공격 판정 전용 충돌 매개체 클래스.
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
        triggerCollider = GetComponent<Collider2D>();
        if (triggerCollider == null)
        {
            var box = gameObject.AddComponent<BoxCollider2D>();
            box.isTrigger = true;
            box.size = new Vector2(1.5f, 1.5f);
            triggerCollider = box;
        }

        visualRenderer = GetComponent<SpriteRenderer>();
        if (visualRenderer == null)
        {
            visualRenderer = gameObject.AddComponent<SpriteRenderer>();
            visualRenderer.sortingOrder = 20;
        }
    }

    public void InitEffect(string poolKey, float damage, float lifetime, FactionType faction, CombatStats attacker, Color effectColor)
    {
        this.poolKey = poolKey;
        this.damage = damage;
        this.lifetime = lifetime;
        timer = 0f;
        ownerFaction = faction;
        attackerStats = attacker;

        if (visualRenderer != null)
        {
            visualRenderer.color = effectColor;
        }

        gameObject.SetActive(true);
    }

    private void Update()
    {
        timer += Time.deltaTime;
        if (timer >= lifetime)
        {
            ReturnToPool();
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        var targetUnit = other.GetComponentInParent<UnitBase>();
        CombatStats targetStats = targetUnit != null ? targetUnit.GetComponent<CombatStats>() : other.GetComponentInParent<CombatStats>();

        if (targetStats == null) return;
        if (attackerStats != null && targetStats.gameObject == attackerStats.gameObject) return;

        // Faction check
        if (targetUnit != null && targetUnit.UnitData != null)
        {
            if (targetUnit.UnitData.Faction == (uint)ownerFaction) return;
        }

        if (targetStats.IsParrying)
        {
            Debug.Log($"<color=magenta><b>[SkillEffect] '{targetStats.gameObject.name}' 패링 성공!</b></color>");
            ReturnToPool();
            return;
        }

        if (targetStats.IsGuarding)
        {
            float guardDamage = damage * 0.2f;
            targetStats.TakeDamage(guardDamage, isGroundAttack: false, isJumped: false, attacker: attackerStats);
            Debug.Log($"<color=yellow><b>[SkillEffect] '{targetStats.gameObject.name}' 가드 성공! 경감 데미지: {guardDamage:F1}</b></color>");
            ReturnToPool();
            return;
        }

        targetStats.TakeDamage(damage, isGroundAttack: false, isJumped: false, attacker: attackerStats);
        Debug.Log($"<color=red><b>[SkillEffect] '{targetStats.gameObject.name}' 피격! 데미지: {damage:F1}</b></color>");

        ReturnToPool();
    }

    public void ReturnToPool()
    {
        gameObject.SetActive(false);
        if (!string.IsNullOrEmpty(poolKey) && SimplePoolManager.Instance != null)
        {
            SimplePoolManager.Instance.Release<SkillEffect>(poolKey, this);
        }
    }
}
