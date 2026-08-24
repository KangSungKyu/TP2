using System;
using UnityEngine;

/// <summary>
/// 유닛 본체의 Hurtbox와 독립된 공격 판정 전용 충돌 매개체 클래스.
/// </summary>
public class SkillEffect : MonoBehaviour
{
    [SerializeField] private SpriteRenderer visualRenderer;

    private string poolKey;
    private float damage;
    private float lifetime;
    private float timer;
    private FactionType ownerFaction;
    private CombatStats attackerStats;

    private Collider2D triggerCollider;
    private Vector3 defaultVisualScale = Vector3.one;
    private Color defaultVisualColor = Color.white;
    private Action<SkillEffect> onReturned;
    private bool returned;

    private void Awake()
    {
        bool hasSerializedVisual = visualRenderer != null;
        triggerCollider = GetComponent<Collider2D>();
        if (triggerCollider == null && !hasSerializedVisual)
        {
            var box = gameObject.AddComponent<BoxCollider2D>();
            box.isTrigger = true;
            box.size = new Vector2(1.5f, 1.5f);
            triggerCollider = box;
        }

        if (visualRenderer == null) visualRenderer = GetComponent<SpriteRenderer>();
        if (visualRenderer == null)
        {
            visualRenderer = gameObject.AddComponent<SpriteRenderer>();
            visualRenderer.sortingOrder = 20;
        }
        defaultVisualScale = visualRenderer.transform.localScale;
        defaultVisualColor = visualRenderer.color;
    }

    public void InitEffect(string poolKey, float damage, float lifetime, FactionType faction, CombatStats attacker, Color effectColor, Action<SkillEffect> onReturned = null)
    {
        this.poolKey = poolKey;
        this.damage = damage;
        this.lifetime = lifetime;
        timer = 0f;
        ownerFaction = faction;
        attackerStats = attacker;
        this.onReturned = onReturned;
        returned = false;

        if (visualRenderer != null)
        {
            visualRenderer.color = effectColor;
        }

        gameObject.SetActive(true);
    }

    public void SetSize(Vector2 size)
    {
        if (triggerCollider is BoxCollider2D box) box.size = size;
    }

    public void ResetVisual()
    {
        if (visualRenderer == null) return;
        visualRenderer.transform.localScale = defaultVisualScale;
        visualRenderer.color = defaultVisualColor;
    }

    private void Update()
    {
        if (lifetime <= 0f || returned) return;
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

        targetStats.TakeDamage(damage, isGroundAttack: false, isJumped: false, attacker: attackerStats,
            attackOrigin: transform.position, guardAmountMultiplier: 0.2f);
        Debug.Log($"<color=red><b>[SkillEffect] '{targetStats.gameObject.name}' 피격! 데미지: {damage:F1}</b></color>");

        ReturnToPool();
    }

    public void ReturnToPool()
    {
        if (returned) return;
        returned = true;
        ResetVisual();
        onReturned?.Invoke(this);
        onReturned = null;
        gameObject.SetActive(false);
        if (!string.IsNullOrEmpty(poolKey) && EffectPoolManager.Instance != null)
            EffectPoolManager.Instance.DespawnEffect(gameObject);
    }
}
