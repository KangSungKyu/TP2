using Cysharp.Threading.Tasks;
using UnityEngine;

/// <summary>
/// 보스 몬스터 클래스 (Monster 상속).
/// 기획서 1번 보스 '철위병 가론'의 4대 대처법 시험 패턴 및 자세(Posture) 100% 무방비/처형을 제어합니다.
/// </summary>
public class BossMonster : Monster
{
    // =========================================================================
    // 1. PROTECTED & PRIVATE METHODS (camelCase)
    // =========================================================================

    protected override void Awake()
    {
        base.Awake();
        // 철위병 가론 보스 UnitBaseData Idx: 3201
        this.InitUnitAsync(3201).Forget();
    }

    protected override void Start()
    {
        base.Start();
        Debug.Log($"<color=red><b>[BossMonster] 보스 '{this.UnitName}' (철위병 가론) 참전 완료!</b></color>");
    }
}
