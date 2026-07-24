using UnityEngine;

/// <summary>
/// NPC 유닛 클래스 (UnitBase 상속).
/// 비전투 대기 및 대화/퀘스트 텍스트 인터랙션을 전담합니다.
/// </summary>
public class NPC : UnitBase
{
    // =========================================================================
    // 1. PUBLIC FIELDS (PascalCase)
    // =========================================================================

    public uint DialogueTextIdx = 2001;


    // =========================================================================
    // 2. PUBLIC METHODS (PascalCase)
    // =========================================================================

    public string GetDialogueText()
    {
        var textDB = DataTableManager.Instance != null ? DataTableManager.Instance.GetDB<TextDataTable>(DataTableType.Text) : null;
        return textDB != null ? textDB.GetText(this.DialogueTextIdx) : "반갑습니다!";
    }


    // =========================================================================
    // 3. PROTECTED METHODS (camelCase)
    // =========================================================================

    protected override void Awake()
    {
        base.Awake();
    }
}
