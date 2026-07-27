using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 테스트 및 디버그 환경에서 플레이어의 상태 정보(이름, HP, MP, Posture, PlayerState)를
/// 화면 좌상단에 실시간으로 선명하게 표출해주는 테스트 HUD UI 컴포넌트.
/// </summary>
public class TestPlayerHUDUI : MonoBehaviour
{
    private static TestPlayerHUDUI instance;
    public static TestPlayerHUDUI Instance => instance;

    private Player targetPlayer;
    private CombatStats playerStats;

    // GUI 스타일 정의
    private GUIStyle headerStyle;
    private GUIStyle labelStyle;
    private GUIStyle stateStyle;
    private bool stylesInitialized = false;

    private void Awake()
    {
        if (instance == null)
        {
            instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else if (instance != this)
        {
            Destroy(gameObject);
            return;
        }
    }

    private void Update()
    {
        // 타겟 플레이어가 없을 경우 씬에서 자동으로 찾아 연결
        if (this.targetPlayer == null)
        {
            this.targetPlayer = FindObjectOfType<Player>();
            if (this.targetPlayer != null)
            {
                this.playerStats = this.targetPlayer.GetComponent<CombatStats>();
            }
        }
    }

    private void OnGUI()
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (this.targetPlayer == null) return;

        this.initStylesIfNeeded();

        // 좌상단 HUD 박스 영역 렌더링
        float width = 320f;
        float height = 215f;
        float margin = 10f;

        Rect boxRect = new Rect(margin, margin, width, height);

        // 검은색 반투명 배경 상자
        GUI.Box(boxRect, GUIContent.none, Texture2D.blackTexture != null ? GUI.skin.box : GUI.skin.box);
        
        GUILayout.BeginArea(new Rect(margin + 10f, margin + 10f, width - 20f, height - 20f));

        // 1. 헤더 (유닛 이름 및 타입)
        string playerTitle = string.IsNullOrEmpty(this.targetPlayer.UnitName) ? "PLAYER (Hero)" : this.targetPlayer.UnitName;
        GUILayout.Label($"<b>[ {playerTitle} ]</b>", this.headerStyle);
        GUILayout.Space(5f);

        // 2. HP (체력)
        float hp = this.playerStats != null ? this.playerStats.CurrentHp : 0f;
        float maxHp = this.playerStats != null ? this.playerStats.MaxHp : 100f;
        this.drawStatBar("HP (체력)", hp, maxHp, Color.green);

        // 3. MP (마나)
        float mp = this.playerStats != null ? this.playerStats.CurrentMp : 0f;
        float maxMp = this.playerStats != null ? this.playerStats.MaxMp : 50f;
        this.drawStatBar("MP (마나)", mp, maxMp, Color.cyan);

        // 4. Posture (체형/자세)
        float pos = this.playerStats != null ? this.playerStats.CurrentPosture : 0f;
        float maxPos = this.playerStats != null ? this.playerStats.MaxPosture : 100f;
        this.drawStatBar("Posture (자세)", pos, maxPos, Color.yellow);

        GUILayout.Space(6f);

        // 5. 현재 PlayerState 상태 표출
        string stateText = $"STATE: <color=yellow>{this.targetPlayer.CurrentState}</color>";
        GUILayout.Label(stateText, this.stateStyle);

        GUILayout.EndArea();
#endif
    }

    private void drawStatBar(string title, float current, float max, Color barColor)
    {
        float ratio = max > 0f ? Mathf.Clamp01(current / max) : 0f;
        string text = $"{title}: {current:F0} / {max:F0}";

        GUILayout.Label(text, this.labelStyle);

        // 게이지 바 렌더링
        Rect barRect = GUILayoutUtility.GetRect(280f, 10f);
        GUI.color = Color.gray;
        GUI.DrawTexture(barRect, Texture2D.whiteTexture);

        Rect fillRect = new Rect(barRect.x, barRect.y, barRect.width * ratio, barRect.height);
        GUI.color = barColor;
        GUI.DrawTexture(fillRect, Texture2D.whiteTexture);

        GUI.color = Color.white; // 색상 원복
    }

    private void initStylesIfNeeded()
    {
        if (this.stylesInitialized) return;

        this.headerStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 16,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleLeft
        };
        this.headerStyle.normal.textColor = Color.white;

        this.labelStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 12,
            alignment = TextAnchor.MiddleLeft
        };
        this.labelStyle.normal.textColor = Color.white;

        this.stateStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 14,
            fontStyle = FontStyle.Bold,
            richText = true,
            alignment = TextAnchor.MiddleLeft
        };
        this.stateStyle.normal.textColor = Color.white;

        this.stylesInitialized = true;
    }
}
