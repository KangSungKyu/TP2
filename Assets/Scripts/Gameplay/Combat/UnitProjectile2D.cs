using UnityEngine;
using System.Collections.Generic;

namespace Gameplay.Combat
{
    [RequireComponent(typeof(Collider2D))]
    public sealed class UnitProjectile2D : MonoBehaviour
    {
        private readonly List<UnitBase> sweepVictims = new();
        private readonly List<float> sweepFractions = new();
        private readonly RaycastHit2D[] hits = new RaycastHit2D[8];
        private Collider2D projectileCollider;
        private uint resourceIdx;
        private uint ownerGeneration;
        private Vector2 direction;
        private float speed;
        private float maxDistance;
        private float travelled;
        private float damage;
        private uint hitTick;
        [SerializeField] private bool reflectable;
        private bool reflected;
        private bool returned = true;

        private const float ReflectedDamageMultiplier = 0.5f;

        public UnitBase Owner { get; private set; }
        public float Speed => speed;
        public float MaxDistance => maxDistance;
        public float TravelledDistance => travelled;
        public bool IsReflectable => reflectable;
        public bool IsReflected => reflected;

        private void Awake()
        {
            projectileCollider = GetComponent<Collider2D>();
        }

        public void Activate(uint resourceDataIdx, UnitBase owner, uint generation, Vector2 position,
            Vector2 forward, float moveSpeed, float distance, float patternDamage)
        {
            resourceIdx = resourceDataIdx;
            Owner = owner;
            ownerGeneration = generation;
            speed = moveSpeed;
            maxDistance = distance;
            travelled = 0f;
            damage = patternDamage;
            hitTick = 0;
            reflected = false;
            returned = false;
            transform.position = position;
            SetDirection(forward);
            gameObject.SetActive(true);
        }

        private void FixedUpdate()
        {
            if (returned) return;
            if (Owner == null || !Owner.IsActionGenerationCurrent(ownerGeneration))
            {
                ReturnToPool();
                return;
            }

            float step = Mathf.Min(speed * Time.fixedDeltaTime, maxDistance - travelled);
            if (step <= 0f)
            {
                ReturnToPool();
                return;
            }

            if (projectileCollider != null)
            {
                var intendedSweep = new CombatStats.AttackSweep2D(transform.position,
                    (Vector2)transform.position + direction * step, projectileCollider.bounds.extents,
                    GetInstanceID(), ownerGeneration, hitTick, hasExteriorPose: true);
                if (CombatStats.CollectAttackSweepVictims(Owner, intendedSweep, sweepVictims,
                    sweepFractions) > 0)
                {
                    UnitBase sweptTarget = sweepVictims[0];
                    float sweptFraction = sweepFractions[0];
                    Vector2 contact = Vector2.Lerp(intendedSweep.Previous, intendedSweep.Current, sweptFraction);
                    bool canReflect = TryGetReflectionDirection(sweptTarget, out Vector2 reflectedDirection);
                    CombatStats.DamageResolution result = sweptTarget.Stats.ResolveDamage(damage, false, false, Owner.Stats,
                        contact, attackSweep: intendedSweep, suppressParryPosture: canReflect);
                    hitTick++;
                    if (result == CombatStats.DamageResolution.Parry && canReflect)
                    {
                        ReflectFrom(sweptTarget, reflectedDirection, contact, step * sweptFraction);
                        return;
                    }
                    ReturnToPool();
                    return;
                }

                var filter = new ContactFilter2D { useTriggers = true, useLayerMask = false };
                int count = projectileCollider.Cast(direction, filter, hits, step);
                for (int i = 0; i < count; i++)
                {
                    Transform hitTransform = hits[i].collider != null ? hits[i].collider.transform : null;
                    if (hitTransform == null || hitTransform == Owner.transform || hitTransform.IsChildOf(Owner.transform)) continue;
                    ReturnToPool();
                    return;
                }
            }

            transform.position += (Vector3)(direction * step);
            travelled += step;
            if (travelled >= maxDistance) ReturnToPool();
        }

        private bool TryGetReflectionDirection(UnitBase defender, out Vector2 reflectedDirection)
        {
            reflectedDirection = Vector2.zero;
            if (!reflectable || reflected || Owner == null || defender == null ||
                Owner.Faction == FactionType.None || Owner.Faction == FactionType.Neutral ||
                defender.Faction == FactionType.None || defender.Faction == FactionType.Neutral)
                return false;
            Collider2D ownerBody = Owner.Stats != null ? Owner.Stats.DefenseBodyCollider : null;
            Collider2D defenderBody = defender.Stats != null ? defender.Stats.DefenseBodyCollider : null;
            if (ownerBody == null || !ownerBody.enabled || !ownerBody.gameObject.activeInHierarchy ||
                defenderBody == null || !defenderBody.enabled || !defenderBody.gameObject.activeInHierarchy)
                return false;
            Vector2 delta = (Vector2)ownerBody.bounds.center - (Vector2)defenderBody.bounds.center;
            if (delta.sqrMagnitude <= Mathf.Epsilon) return false;
            reflectedDirection = delta.normalized;
            return true;
        }

        private void ReflectFrom(UnitBase defender, Vector2 reflectedDirection, Vector2 position,
            float consumedDistance)
        {
            transform.position = position;
            travelled = Mathf.Min(maxDistance, travelled + Mathf.Max(0f, consumedDistance));
            Owner = defender;
            ownerGeneration = defender.ActionGeneration;
            SetDirection(reflectedDirection);
            damage *= ReflectedDamageMultiplier;
            reflected = true;
        }

        private void SetDirection(Vector2 value)
        {
            direction = value.sqrMagnitude > 0f ? value.normalized : Vector2.right;
            transform.right = direction;
        }

        public void ReturnToPool()
        {
            if (returned) return;
            returned = true;
            Owner = null;
            direction = Vector2.zero;
            speed = 0f;
            maxDistance = 0f;
            travelled = 0f;
            damage = 0f;
            hitTick = 0;
            reflected = false;
            if (UnitPoolManager.Instance != null) UnitPoolManager.Instance.ReturnProjectile(resourceIdx, this);
            else gameObject.SetActive(false);
        }

        private void OnDisable()
        {
            if (!returned) ReturnToPool();
        }
    }
}
