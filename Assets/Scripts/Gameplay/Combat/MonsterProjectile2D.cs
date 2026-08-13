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
        private bool returned = true;

        public Monster Owner { get; private set; }
        public float Speed => speed;
        public float MaxDistance => maxDistance;
        public float TravelledDistance => travelled;

        private void Awake()
        {
            projectileCollider = GetComponent<Collider2D>();
        }

        public void Activate(uint resourceDataIdx, Monster owner, uint generation, Vector2 position,
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

            Player player = Player.Instance;
            if (projectileCollider != null && player != null)
            {
                var filter = new ContactFilter2D { useTriggers = true, useLayerMask = false };
                int count = projectileCollider.Cast(direction, filter, hits, step);
                for (int i = 0; i < count; i++)
                {
                    Transform hitTransform = hits[i].collider != null ? hits[i].collider.transform : null;
                    if (hitTransform == null || hitTransform == Owner.transform || hitTransform.IsChildOf(Owner.transform)) continue;
                    if (hitTransform == player.transform || hitTransform.IsChildOf(player.transform))
                        player.Stats?.TakeDamage(damage, false, false, Owner.Stats, hits[i].point);
                    ReturnToPool();
                    return;
                }
            }

            transform.position += (Vector3)(direction * step);
            travelled += step;
            if (travelled >= maxDistance) ReturnToPool();
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
            if (UnitPoolManager.Instance != null) UnitPoolManager.Instance.ReturnProjectile(resourceIdx, this);
            else gameObject.SetActive(false);
        }

        private void OnDisable()
        {
            if (!returned) ReturnToPool();
        }
    }
}
