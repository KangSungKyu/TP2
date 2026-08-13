using UnityEngine;

namespace Gameplay.Combat
{
    [RequireComponent(typeof(Collider2D))]
    public sealed class MonsterProjectile2D : MonoBehaviour
    {
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
        private bool returned = true;

        public UnitBase Owner { get; private set; }
        public float Speed => speed;
        public float MaxDistance => maxDistance;
        public float TravelledDistance => travelled;

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
            direction = forward.sqrMagnitude > 0f ? forward.normalized : Vector2.right;
            speed = moveSpeed;
            maxDistance = distance;
            travelled = 0f;
            damage = patternDamage;
            hitTick = 0;
            returned = false;
            transform.SetPositionAndRotation(position, Quaternion.identity);
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
                    GetInstanceID(), ownerGeneration, hitTick);
                UnitBase sweptTarget = null;
                float sweptFraction = float.MaxValue;
                Player player = Player.Instance;
                if (player != null && player.Stats != null && IsHostile(Owner, player) &&
                    player.Stats.TryGetBodySweepFraction(intendedSweep, out float playerFraction))
                {
                    sweptTarget = player;
                    sweptFraction = playerFraction;
                }
                foreach (Monster monster in Monster.ActiveMonsters)
                {
                    if (monster == null || monster.Stats == null || !IsHostile(Owner, monster) ||
                        !monster.Stats.TryGetBodySweepFraction(intendedSweep, out float monsterFraction) ||
                        monsterFraction >= sweptFraction) continue;
                    sweptTarget = monster;
                    sweptFraction = monsterFraction;
                }
                if (sweptTarget != null)
                {
                    sweptTarget.Stats.TakeDamage(damage, false, false, Owner.Stats,
                        Vector2.Lerp(intendedSweep.Previous, intendedSweep.Current, sweptFraction),
                        attackSweep: intendedSweep);
                    hitTick++;
                    ReturnToPool();
                    return;
                }

                var filter = new ContactFilter2D { useTriggers = true, useLayerMask = false };
                int count = projectileCollider.Cast(direction, filter, hits, step);
                for (int i = 0; i < count; i++)
                {
                    Transform hitTransform = hits[i].collider != null ? hits[i].collider.transform : null;
                    if (hitTransform == null || hitTransform == Owner.transform || hitTransform.IsChildOf(Owner.transform)) continue;
                    UnitBase target = hits[i].collider.GetComponentInParent<UnitBase>();
                    if (target != null && target != Owner && IsHostile(Owner, target))
                    {
                        var sweep = new CombatStats.AttackSweep2D(transform.position,
                            hits[i].centroid, projectileCollider.bounds.extents,
                            GetInstanceID(), ownerGeneration, hitTick++);
                        target.Stats?.TakeDamage(damage, false, false, Owner.Stats, hits[i].point,
                            attackSweep: sweep);
                    }
                    ReturnToPool();
                    return;
                }
            }

            transform.position += (Vector3)(direction * step);
            travelled += step;
            if (travelled >= maxDistance) ReturnToPool();
        }

        private static bool IsHostile(UnitBase owner, UnitBase target)
        {
            FactionType ownerFaction = owner.Faction;
            FactionType targetFaction = target.Faction;
            return ownerFaction != FactionType.None && targetFaction != FactionType.None &&
                ownerFaction != FactionType.Neutral && targetFaction != FactionType.Neutral &&
                ownerFaction != targetFaction;
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
            if (UnitPoolManager.Instance != null) UnitPoolManager.Instance.ReturnProjectile(resourceIdx, this);
            else gameObject.SetActive(false);
        }

        private void OnDisable()
        {
            if (!returned) ReturnToPool();
        }
    }
}
