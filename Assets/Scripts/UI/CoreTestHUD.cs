using UnityEngine;

/// <summary>
/// 코어 액션 및 스탯(HP, MP, Posture, 4대 대처 상태)을 한눈에 확인할 수 있는 테스트용 OnGUI HUD.
/// 언더스코어(_) 접두사 배제 및 글로벌 네임스페이스 규칙을 준수합니다.
/// </summary>
public class CoreTestHUD : MonoBehaviour
{
    // =========================================================================
    // 1. PRIVATE FIELDS (camelCase, No '_' prefix)
    // =========================================================================

    private CombatStats playerStats;
    private CombatStats monsterStats;
    private Monster monster;


    // =========================================================================
    // 2. PRIVATE METHODS (camelCase)
    // =========================================================================

    private void Start()
    {
        var player = GameObject.FindWithTag("Player");
        if (player != null)
            this.playerStats = player.GetComponent<CombatStats>();

        this.monster = GameObject.FindObjectOfType<Monster>();
        if (this.monster != null)
            this.monsterStats = this.monster.GetComponent<CombatStats>();
    }

    private void OnGUI()
    {
        GUI.skin.box.fontSize = 13;
        GUI.skin.label.fontSize = 12;

        // 1. 조작 설명 가이드 윈도우 (Left Window)
        GUILayout.BeginArea(new Rect(10, 10, 320, 240), "<b>[ 코어 테스트 조작 가이드 ]</b>", GUI.skin.window);
        GUILayout.Label("• <b>이동</b>: WASD / 화살표 키");
        GUILayout.Label("• <b>점프 (Jump)</b>: Space");
        GUILayout.Label("• <b>통합 방어 (패링/가드)</b>: Left Shift / J / Q");
        GUILayout.Label("  - 누르는 순간: 0.15초 패링 윈도우");
        GUILayout.Label("  - 0.15초 후 유지: 가드 전환 (손 뗄 때까지)");
        GUILayout.Label("• <b>회피 (Dodge)</b>: Left Ctrl / L (이동 시 방향 대시 / 정지 시 백대시)");
        GUILayout.Label("• <b>기본공격</b>: 1 / F (Skill ID 1)");
        GUILayout.Label("• <b>파이어볼</b>: 2 / R (Skill ID 2)");
        GUILayout.EndArea();

        // 2. 플레이어 스탯 윈도우 (Middle Window)
        if (this.playerStats != null)
        {
            GUILayout.BeginArea(new Rect(340, 10, 280, 190), "<b>[ Player 스탯 & 상태 ]</b>", GUI.skin.window);
            GUILayout.Label($"<b>HP</b>: {this.playerStats.CurrentHp:F0} / {this.playerStats.MaxHp:F0}");
            this.drawProgressBar(this.playerStats.CurrentHp / this.playerStats.MaxHp, Color.green);

            GUILayout.Label($"<b>MP</b>: {this.playerStats.CurrentMp:F0} / {this.playerStats.MaxMp:F0}");
            this.drawProgressBar(this.playerStats.CurrentMp / this.playerStats.MaxMp, Color.cyan);

            GUILayout.Label($"<b>자세(Posture)</b>: {this.playerStats.CurrentPosture:F0} / {this.playerStats.MaxPosture:F0}");
            this.drawProgressBar(this.playerStats.CurrentPosture / this.playerStats.MaxPosture, Color.yellow);

            string stateStr = "";
            if (this.playerStats.IsDodging) stateStr += "<color=cyan>[회피 중]</color> ";
            if (this.playerStats.IsGuarding) stateStr += "<color=yellow>[가드 중]</color> ";
            if (this.playerStats.IsParrying) stateStr += "<color=magenta>[패링 타이밍]</color> ";
            if (this.playerStats.IsGroggy) stateStr += "<color=red>[그로기]</color> ";
            if (string.IsNullOrEmpty(stateStr)) stateStr = "평상시";

            GUILayout.Label($"<b>현재 상태</b>: {stateStr}");
            GUILayout.EndArea();
        }

        // 3. 몬스터/보스 스탯 윈도우 (Right Window)
        if (this.monsterStats != null)
        {
            string monsterTitle = this.monster != null ? this.monster.UnitName : "몬스터";
            GUILayout.BeginArea(new Rect(630, 10, 280, 190), $"<b>[ {monsterTitle} ]</b>", GUI.skin.window);
            GUILayout.Label($"<b>HP</b>: {this.monsterStats.CurrentHp:F0} / {this.monsterStats.MaxHp:F0}");
            this.drawProgressBar(this.monsterStats.CurrentHp / this.monsterStats.MaxHp, Color.red);

            GUILayout.Label($"<b>Posture 게이지</b>: {this.monsterStats.CurrentPosture:F0} / {this.monsterStats.MaxPosture:F0}");
            this.drawProgressBar(this.monsterStats.CurrentPosture / this.monsterStats.MaxPosture, Color.yellow);

            string bossState = this.monsterStats.IsGroggy ? "<color=red><b>[무방비/그로기(Execution)]</b></color>" : "AI 패턴 순환 중";
            GUILayout.Label($"<b>상태</b>: {bossState}");
            GUILayout.EndArea();
        }
    }

    private void drawProgressBar(float fillPercent, Color color)
    {
        Rect rect = GUILayoutUtility.GetRect(260, 16);
        GUI.Box(rect, "");
        Texture2D texture = new Texture2D(1, 1);
        texture.SetPixel(0, 0, color);
        texture.Apply();
        GUI.DrawTexture(new Rect(rect.x + 2, rect.y + 2, (rect.width - 4) * Mathf.Clamp01(fillPercent), rect.height - 4), texture);
    }
}
