// File: Projectile.cs
using UnityEngine;
using Cysharp.Threading.Tasks;
using Gameplay.Combat;

namespace Gameplay.Combat
{
    /// <summary>
    /// 풀링된 투사체. AttackHandler 로부터 초기화 데이터를 받아 이동하고, 적을 맞추면
    /// DamageCalculator 로 데미지를 계산 후 Health에 적용한다. 사용이 끝나면 SimplePool에
    /// 반환한다.
    /// </summary>
    public class Projectile : MonoBehaviour
    {
        // 이동 속도 (필요에 따라 인스펙터에서 조정 가능)
        [Header("Projectile Settings")]
        public float Speed = 15f;
        // 최대 사거리 (사거리 초과 시 자동 반환)
        public float MaxDistance = 25f;
        // 내부 사용 데이터
        private AttackData _data;
        private Vector3 _direction;
        private Vector3 _origin;
        private string _poolKey; // SimplePool 에서 사용되는 addressable key

        /// <summary>
        /// AttackHandler 가 호출하는 초기화 메서드.
        /// </summary>
        public void Initialize(AttackData data, Vector3 startPosition, Vector3 forward)
        {
            _data = data;
            _origin = startPosition;
            _direction = forward.normalized;
            // 풀키는 프리팹 이름(예: Addressable 키)과 동일하게 가정
            _poolKey = data.ProjectilePrefab != null ? data.ProjectilePrefab.name : "";

            // 위치와 회전 초기화
            transform.position = startPosition;
            transform.rotation = Quaternion.LookRotation(_direction);
            // 비활성화 상태라면 활성화
            if (!gameObject.activeSelf) gameObject.SetActive(true);

            // 비동기 이동 시작 (UniTask 사용)
            MoveAsync().Forget();
        }

        private async UniTaskVoid MoveAsync()
        {
            // 사거리 제한까지 이동하면서 충돌 검사
            while (Vector3.Distance(_origin, transform.position) < MaxDistance)
            {
                // 이동
                transform.position += _direction * Speed * Time.deltaTime;

                // 충돌 검사 – SphereCast 로 히트박스와 레이어 마스크 활용
                if (Physics.SphereCast(transform.position, _data.HitRadius, _direction, out RaycastHit hit,
                    Speed * Time.deltaTime, _data.TargetMask, QueryTriggerInteraction.Ignore))
                {
                    // 히트된 객체에 Health 가 있으면 데미지 적용
                    var health = hit.collider.GetComponent<Health>();
                    if (health != null)
                    {
                        float dmg = DamageCalculator.Calculate(_data);
                        health.ApplyDamage(dmg);
                    }
                    // 충돌 후 바로 풀에 반환
                    ReturnToPool();
                    return;
                }

                // 프레임 대기 (UniTask.Yield 로 코루틴 없이 비동기 흐름 유지)
                await UniTask.Yield();
            }

            // 최대 사거리 도달 시 풀에 반환
            ReturnToPool();
        }

        private void ReturnToPool()
        {
            // SimplePoolManager 로 반환. 키가 없으면 그냥 비활성화.
            if (!string.IsNullOrEmpty(_poolKey))
            {
                SimplePoolManager.Instance.Release<Projectile>(_poolKey, this);
            }
            else
            {
                gameObject.SetActive(false);
            }
        }

        // 풀에 반환될 때 혹시 남아 있는 코루틴/비동기 작업을 정리하고 싶다면
        private void OnDisable()
        {
            // 현재 UniTask 흐름은 자동으로 중단되므로 여기서는 별도 처리 불필요.
        }
    }
}
