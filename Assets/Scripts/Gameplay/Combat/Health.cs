// File: Health.cs
using UnityEngine;

namespace Gameplay.Combat
{
    /// <summary>
    /// Entity(플레이어/몬스터/보스)의 체력 관리 컴포넌트.
    /// DamageCalculator 로 계산된 값을 받아 현재 체력을 차감한다.
    /// </summary>
    public class Health : MonoBehaviour
    {
        [Header("Health Settings")]
        public float MaxHealth = 100f;
        public float CurrentHealth { get; private set; }

        private void Awake()
        {
            CurrentHealth = MaxHealth;
        }

        /// <summary>
        /// 입힌 데미지를 받아 차감하고, 체력이 0 이하가 되면 죽음 처리.
        /// </summary>
        public void ApplyDamage(float damage)
        {
            if (damage <= 0f) return;
            CurrentHealth = Mathf.Max(CurrentHealth - damage, 0f);
            if (CurrentHealth <= 0f)
            {
                OnDeath();
            }
        }

        private void OnDeath()
        {
            // 기본 죽음 로직 – 필요 시 이벤트/시스템 연동
            // 현재는 비활성화만 수행
            Debug.Log($"{name} died.");
            gameObject.SetActive(false);
        }
    }
}
