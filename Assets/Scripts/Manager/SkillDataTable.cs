using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// DataTableManager에서 관리하는 스킬 테이블 데이터클래스입니다.
/// IDataLoad 인터페이스를 구현하며, CSV를 읽어 SkillInfo 딕셔너리를 관리합니다.
/// 언더스코어(_) 접두사 배제 규칙 및 this 키워드를 준수합니다.
/// </summary>
public class SkillDataTable : IDataLoad
{
    private readonly Dictionary<int, SkillInfo> skillDict = new Dictionary<int, SkillInfo>();

    public int GetDataCount()
    {
        return this.skillDict.Count;
    }

    public void LoadData(string csvText)
    {
        this.skillDict.Clear();

        var lines = csvText.Split(new[] { '\r', '\n' }, System.StringSplitOptions.RemoveEmptyEntries);
        // 첫 줄은 헤더 (1,Name,AnimationClip,Range,CastTime,CooldownSec,MpCost,DamageMultiplier,IsBasicAttack)
        for (int i = 1; i < lines.Length; i++)
        {
            var cols = lines[i].Split(',');
            if (cols.Length < 9) continue;

            if (int.TryParse(cols[0], out int id))
            {
                var info = new SkillInfo
                {
                    Id = id,
                    Name = cols[1],
                    AnimationClip = cols[2],
                    Range = float.Parse(cols[3]),
                    CastTime = float.Parse(cols[4]),
                    Cooldown = float.Parse(cols[5]),
                    MpCost = float.Parse(cols[6]),
                    DamageMultiplier = float.Parse(cols[7]),
                    IsBasicAttack = bool.Parse(cols[8])
                };
                this.skillDict[info.Id] = info;
            }
        }

        Debug.Log($"[SkillDataTable] 총 {this.skillDict.Count}개의 스킬 데이터가 로드되었습니다.");
    }

    public bool TryGetSkill(int skillId, out SkillInfo info)
    {
        return this.skillDict.TryGetValue(skillId, out info);
    }

    public void Release()
    {
        this.skillDict.Clear();
    }
}
