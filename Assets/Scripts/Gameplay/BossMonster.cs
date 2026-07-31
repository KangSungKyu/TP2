using Cysharp.Threading.Tasks;
using UnityEngine;

/// <summary>
/// 보스 몬스터 클래스 (Monster 상속).
/// </summary>
public class BossMonster : Monster
{
    protected override void Awake()
    {
        base.Awake();
        InitUnitAsync(3201).Forget();
    }

    protected override void Start()
    {
        base.Start();
        Debug.Log($"<color=red><b>[BossMonster] 보스 '{UnitName}' (철위병 가론) 참전 완료!</b></color>");
    }
}

