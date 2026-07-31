using UnityEngine;

/// <summary>
/// NPC 유닛 클래스 (UnitBase 상속).
/// </summary>
public class NPC : UnitBase
{
    public uint DialogueTextIdx = 2001;

    public string GetDialogueText()
    {
        var textDB = DataTableManager.Instance != null ? DataTableManager.Instance.GetDB<TextDataTable>(DataTableType.Text) : null;
        return textDB != null ? textDB.GetText(DialogueTextIdx) : "반갑습니다!";
    }
}

