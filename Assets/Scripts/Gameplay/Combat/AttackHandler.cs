// File: AttackHandler.cs
using UnityEngine;
using Cysharp.Threading.Tasks;
using Gameplay.Combat;

namespace Gameplay.Combat
{
    /// <summary>
    /// 공격 실행 담당 컴포넌트.
    /// 기본 공격(히트박스) 혹은 투사체 공격 모두 SimplePool와 UniTask를 사용합니다.
    /// </summary>
    [RequireComponent(typeof(AttackPower))]
    public class AttackHandler : MonoBehaviour
    {
        private AttackPower _attackPower;
        private const string DefaultProjectileKey = "ProjectilePrefab"; // Addressable key 이름 (예시)

        private void Awake()
        {
            _attackPower = GetComponent<AttackPower>();
        }

        /// <summary>
        /// 외부에서 호출되는 비동기 공격 메서드.
        /// AttackPower 설정에 따라 히트박스 검사 혹은 풀링된 투사체를 발사합니다.
        /// </summary>
        public async UniTask PerformAttackAsync()
        {
            AttackData data = _attackPower.GetAttackData();
            if (data.IsProjectile && data.ProjectilePrefab != null)
            {
                // SimplePool에서 오브젝트 획득 (AddressableKey는 프리팹 이름 사용)
                string key = data.ProjectilePrefab.name; // 프리팹 이름을 주소키로 사용
                // 풀에 아직 없으면 사전 생성 (capacity 10 기준)
                if (!SimplePoolManager.Instance.TryGetPool<Projectile>(key, out _))
                {
                    // 간단히 CreatePoolAsync 호출 (프리팹을 Addressable 로드한다 가정)
                    await SimplePoolManager.Instance.CreatePoolAsync<Projectile>(key, 10, 5, null);
                }
                var projectile = SimplePoolManager.Instance.Get<Projectile>(key);
                if (projectile != null)
                {
                    projectile.Initialize(data, this.transform.position, this.transform.forward);
                }
            }
            else
            {
                // 비투사체 – 히트박스 검사 후 데미지 적용
                await PerformMeleeAttackAsync(data);
            }
        }

        private async UniTask PerformMeleeAttackAsync(AttackData data)
        {
            // SphereCast 로 타깃 탐색 (UniTask 로 래핑 – 비동기 대기 없이 바로 실행)
            RaycastHit hit;
            Vector3 origin = transform.position;
            Vector3 direction = transform.forward;

            bool hasHit = Physics.SphereCast(
                origin,
                data.HitRadius,
                direction,
                out hit,
                data.Range,
                data.TargetMask,
                QueryTriggerInteraction.Ignore);

            if (hasHit && hit.collider != null)
            {
                var health = hit.collider.GetComponent<Health>();
                if (health != null)
                {
                    float finalDamage = DamageCalculator.Calculate(data);
                    health.ApplyDamage(finalDamage);
                    Debug.Log($"{name} melee hit {hit.collider.name} for {finalDamage} dmg.");
                }
            }
            // 비동기 흐름을 유지하기 위해 잠시 프레임 뒤로 넘김 (옵션)
            await UniTask.Yield();
        }
    }
}
