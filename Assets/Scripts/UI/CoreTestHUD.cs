using UnityEngine;
using Cysharp.Threading.Tasks;

/// <summary>
/// 코어 액션 가이드 및 보스 상태를 한눈에 확인할 수 있는 테스트용 OnGUI HUD.
/// </summary>
public class CoreTestHUD : MonoBehaviour
{
    private CombatStats monsterStats;
    private Monster monster;

    private void Start()
    {
        this.monster = GameObject.FindObjectOfType<Monster>();
        if (this.monster != null)
            this.monsterStats = this.monster.GetComponent<CombatStats>();
    }

    private void OnGUI()
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        GUI.skin.box.fontSize = 13;
        GUI.skin.label.fontSize = 12;

        // 1. 조작 설명 가이드 윈도우 (좌측 하단 배치: Rect(10, 235, 320, 290) - 플레이어 HUD 상자 바로 아래)
        GUILayout.BeginArea(new Rect(10, 235, 320, 290), "<b>[ 코어 테스트 조작 가이드 ]</b>", GUI.skin.window);
        GUILayout.Label("• <b>이동</b>: WASD / 화살표 키");
        GUILayout.Label("• <b>점프 (Jump)</b>: C 키");
        GUILayout.Label("• <b>통합 방어 (패링/가드)</b>: Space Bar");
        GUILayout.Label("  - 누르는 순간: 0.15초 패링 윈도우");
        GUILayout.Label("  - 계속 누름: 가드 전환 (손 뗄 때까지)");
        GUILayout.Label("• <b>회피/대시 (Dodge)</b>: Left Shift (방향 대시 / 백대시)");
        GUILayout.Label("• <b>기본공격</b>: X 키 (3타 콤보)");
        GUILayout.Label("• <b>스킬1</b>: 1 / F (Skill ID 1)");
        GUILayout.Label("• <b>스킬2</b>: 2 / R (Skill ID 2)");
        GUILayout.EndArea();

        // 2. 철위병 가론 보스 스탯 윈도우 (중앙 상단 배치: Rect(340, 10, 280, 190))
        if (this.monsterStats != null && this.monster != null)
        {
            GUILayout.BeginArea(new Rect(340, 10, 280, 190), $"<b>[ {this.monster.UnitName} ]</b>", GUI.skin.window);
            GUILayout.Label($"<b>HP</b>: {this.monsterStats.CurrentHp:F0} / {this.monsterStats.MaxHp:F0}");
            this.drawProgressBar(this.monsterStats.CurrentHp / this.monsterStats.MaxHp, Color.red);

            GUILayout.Label($"<b>자세(Posture) 게이지</b>: {this.monsterStats.CurrentPosture:F0} / {this.monsterStats.MaxPosture:F0}");
            this.drawProgressBar(this.monsterStats.CurrentPosture / this.monsterStats.MaxPosture, Color.yellow);

            GUILayout.Label($"<b>상태</b>: AI 패턴 순환 중");
            GUILayout.EndArea();
        }
        // 3. 룸 청크 선택/이동 윈도우 (우측 상단 배치: Rect(Screen.width - 340, 10, 330, 160))
        GUILayout.BeginArea(new Rect(Screen.width - 340, 10, 330, 160), "<b>[ 룸 청크 선택 & 관문 이동 ]</b>", GUI.skin.window);
        string currentKey = StageManager.Instance != null ? StageManager.Instance.CurrentRoomAddressableKey : "Unknown";
        GUILayout.Label($"<b>현재 룸</b>: {currentKey}");

        GUILayout.BeginHorizontal();
        if (GUILayout.Button("🚪 1040 Entry", GUILayout.Height(35)))
        {
            if (StageManager.Instance != null) StageManager.Instance.LoadNextRoomAsync(1040).Forget();
        }
        if (GUILayout.Button("⚔️ 1041 Battle", GUILayout.Height(35)))
        {
            if (StageManager.Instance != null) StageManager.Instance.LoadNextRoomAsync(1041).Forget();
        }
        if (GUILayout.Button("👹 1042 Boss", GUILayout.Height(35)))
        {
            if (StageManager.Instance != null) StageManager.Instance.LoadNextRoomAsync(1042).Forget();
        }
        GUILayout.EndHorizontal();

        GUILayout.Label("• 포탈 접촉 또는 위 버튼 클릭 시 비동기 이동");
        GUILayout.EndArea();
#endif
    }

    private void drawProgressBar(float value, Color color)
    {
        Rect rect = GUILayoutUtility.GetRect(260f, 15f);
        GUI.color = Color.gray;
        GUI.DrawTexture(rect, Texture2D.whiteTexture);

        Rect fillRect = new Rect(rect.x, rect.y, rect.width * Mathf.Clamp01(value), rect.height);
        GUI.color = color;
        GUI.DrawTexture(fillRect, Texture2D.whiteTexture);

        GUI.color = Color.white;
    }
}
