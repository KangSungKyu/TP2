using System;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Gameplay.Combat
{
    public class Projectile : MonoBehaviour
    {
        [Header("Projectile Settings")]
        public float Speed = 15f;
        public float MaxDistance = 25f;

        private AttackData data;
        private Vector3 direction;
        private Vector3 origin;
        private string poolKey;
        private uint generation;
        private bool returned;
        private Action<Projectile> onReturned;

        public void Initialize(AttackData attackData, Vector3 startPosition, Vector3 forward, Action<Projectile> returnCallback = null)
        {
            data = attackData;
            origin = startPosition;
            direction = forward.normalized;
            poolKey = attackData.ProjectilePrefab != null ? attackData.ProjectilePrefab.name : string.Empty;
            onReturned = returnCallback;
            returned = false;
            uint currentGeneration = ++generation;
            transform.SetPositionAndRotation(startPosition, Quaternion.LookRotation(direction));
            if (!gameObject.activeSelf) gameObject.SetActive(true);
            MoveAsync(currentGeneration).Forget();
        }

        private async UniTaskVoid MoveAsync(uint currentGeneration)
        {
            while (currentGeneration == generation && !returned && gameObject.activeInHierarchy &&
                   Vector3.Distance(origin, transform.position) < MaxDistance)
            {
                transform.position += direction * Speed * Time.deltaTime;
                if (Physics.SphereCast(transform.position, data.HitRadius, direction, out RaycastHit hit,
                    Speed * Time.deltaTime, data.TargetMask, QueryTriggerInteraction.Ignore))
                {
                    var health = hit.collider.GetComponent<Health>();
                    if (health != null) health.ApplyDamage(DamageCalculator.Calculate(data));
                    ReturnToPool(currentGeneration);
                    return;
                }
                await UniTask.Yield();
            }
            ReturnToPool(currentGeneration);
        }

        public void ReturnToPool()
        {
            ReturnToPool(generation);
        }

        private void ReturnToPool(uint currentGeneration)
        {
            if (returned || currentGeneration != generation) return;
            returned = true;
            onReturned?.Invoke(this);
            onReturned = null;
            if (!string.IsNullOrEmpty(poolKey) && SimplePoolManager.Instance != null)
                SimplePoolManager.Instance.Release(poolKey, this);
            else
                gameObject.SetActive(false);
        }

        private void OnDisable()
        {
            generation++;
        }
    }
}
