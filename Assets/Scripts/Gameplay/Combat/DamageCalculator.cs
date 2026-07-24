// File: DamageCalculator.cs
using UnityEngine;

namespace Gameplay.Combat
{
    /// <summary>
    /// 기본 공격·스킬 데미지를 계산하는 유틸리티.
    /// AttackData 안의 베이스값, 퍼센트, 고정값을 합산하여 최종 데미지를 반환한다.
    /// </summary>
    public static class DamageCalculator
    {
        /// <summary>
        /// 최종 데미지를 반환합니다.
        /// final = (baseDamage + damageFlat) * (1 + damagePercent)
        /// </summary>
        public static float Calculate(AttackData data)
        {
            float final = (data.BaseDamage + data.DamageFlat) * (1f + data.DamagePercent);
            return Mathf.Max(0f, final);
        }
    }
}
