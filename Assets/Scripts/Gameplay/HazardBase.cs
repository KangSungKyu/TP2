using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 공용 2D 사이드뷰 함정/장애물 기반 클래스 (Hazards & Traps).
/// OnTriggerEnter2D / OnTriggerStay2D 물리 판정 및 연속 다중 피격(Tick Melt) 방지 쿨다운 관리.
/// </summary>
public abstract class HazardBase : MonoBehaviour
{
    [Header("Hazard Base Settings")]
    [SerializeField] protected uint hazardId = 1070;
    [SerializeField] protected int damage = 15;
    [SerializeField] protected float knockbackForce = 8.0f;
    [SerializeField] protected float cooldownBetweenHits = 0.5f;
    [SerializeField] protected LayerMask targetMask = ~0;

    private readonly Dictionary<int, float> lastHitTimestamps = new Dictionary<int, float>();

    public uint HazardId => hazardId;
    public int Damage => damage;
    public float KnockbackForce => knockbackForce;
    public float CooldownBetweenHits => cooldownBetweenHits;

    protected virtual void OnTriggerEnter2D(Collider2D col)
    {
        TryProcessHazardHit(col);
    }

    protected virtual void OnTriggerStay2D(Collider2D col)
    {
        TryProcessHazardHit(col);
    }

    protected virtual void TryProcessHazardHit(Collider2D col)
    {
        if (col == null) return;
        if (((1 << col.gameObject.layer) & targetMask) == 0) return;

        var stats = col.GetComponentInParent<CombatStats>();
        if (stats == null) stats = col.GetComponent<CombatStats>();
        if (stats == null || stats.IsDead) return;

        int targetId = stats.GetInstanceID();
        if (lastHitTimestamps.TryGetValue(targetId, out float lastTime))
        {
            if (Time.time - lastTime < cooldownBetweenHits)
            {
                return; // ponytail: prevent tick melt via cooldown check
            }
        }

        lastHitTimestamps[targetId] = Time.time;
        Vector2 hitNormal = CalculateHitNormal(col);
        ApplyHazardDamage(stats, hitNormal);
    }

    protected virtual Vector2 CalculateHitNormal(Collider2D col)
    {
        Vector2 dir = (col.transform.position - transform.position).normalized;
        return dir == Vector2.zero ? Vector2.up : dir;
    }

    protected virtual void ApplyHazardDamage(CombatStats stats, Vector2 hitNormal)
    {
        if (stats == null) return;

        Vector2 knockbackImpulse = hitNormal * knockbackForce;
        stats.TakeDamage(damage, knockbackImpulse);

        var motor = stats.GetComponent<KinematicMotor2D>();
        if (motor != null && motor.enabled)
        {
            motor.ApplyKnockback(knockbackImpulse);
        }
        else
        {
            var rb = stats.GetComponent<Rigidbody2D>();
            if (rb != null && !rb.isKinematic)
            {
                rb.AddForce(knockbackImpulse, ForceMode2D.Impulse);
            }
        }
    }
}
