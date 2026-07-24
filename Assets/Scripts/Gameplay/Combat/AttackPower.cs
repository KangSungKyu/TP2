// File: AttackPower.cs
using UnityEngine;

namespace Gameplay.Combat
{
    /// <summary>
    /// 공격 주체(플레이어, 몬스터, 보스)에게 부착되는 공격력 스탯 컴포넌트.
    /// DamageCalculator 가 사용하도록 데이터를 제공한다.
    /// </summary>
    [RequireComponent(typeof(Health))]
    public class AttackPower : MonoBehaviour
    {
        [Header("Attack Power Settings")]
        // 기본 공격력(절대값)
        public float BaseDamage = 10f;
        // 공격력 비율 보정(예: 0.2 = 20% 증가)
        public float DamagePercent = 0f;
        // 추가 고정 공격력 보정
        public float DamageFlat = 0f;

        /// <summary>
        /// 현재 AttackData 형태로 정보를 반환한다.
        /// 다른 시스템은 이 데이터를 그대로 DamageCalculator에 넘겨 사용한다.
        /// </summary>
        public AttackData GetAttackData()
        {
            return new AttackData
            {
                BaseDamage = BaseDamage,
                DamagePercent = DamagePercent,
                DamageFlat = DamageFlat,
                IsProjectile = false,
                TargetMask = LayerMask.GetMask("Enemy", "Player"), // 기본 타깃 레이어
                HitRadius = 0.5f,
                Range = 2f,
                ProjectilePrefab = null
            };
        }
    }
}
