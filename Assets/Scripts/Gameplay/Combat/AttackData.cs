// File: AttackData.cs
using UnityEngine;

namespace Gameplay.Combat
{
    /// <summary>
    /// 기본 공격·스킬에 사용되는 데이터 구조체
    /// </summary>
    public struct AttackData
    {
        // 기본 데미지(절대값)
        public float BaseDamage;
        // 데미지 비율 (예: 0.2 = 20% 증가)
        public float DamagePercent;
        // 추가 고정 데미지
        public float DamageFlat;
        // 투사체 여부
        public bool IsProjectile;
        // 타깃 레이어 마스크 (Enemy, Player 등)
        public LayerMask TargetMask;
        // 히트박스 반경 (SphereCast 등에 사용)
        public float HitRadius;
        // 최대 사거리
        public float Range;
        // 투사체 프리팹 (IsProjectile true일 때 사용)
        public GameObject ProjectilePrefab;
    }
}
