using UnityEngine;

/// <summary>
/// 최적화 중심 몬스터 머리 위 HP/Posture UI 매니저.
/// 개별 World Space Canvas 생성 방식을 배제하고, Single Manager 기반으로 
/// 카메라 뷰포트 내 활성 몬스터만 수집하여 화면좌표(WorldToScreenPoint) 투영 렌더링을 수행합니다.
/// (Canvas Rebuild 및 DrawCall 분격 최소화 0-Allocation 설계)
/// </summary>
public class MonsterOverheadHUD : MonoBehaviour
{
    [Header("UI Overlay Settings")]
    public float YOffset = 2.2f;
    public float BarWidth = 64f;
    public float HpBarHeight = 6f;
    public float PostureBarHeight = 4f;

    private Camera mainCam;
    private Monster[] cachedMonsters;
    private float lastSearchTime = 0f;

    private GUIStyle nameStyle;
    private bool styleInited = false;

    private void Start()
    {
        mainCam = Camera.main;
    }

    private void Update()
    {
        if (mainCam == null) mainCam = Camera.main;

        // 0.5초 주기로 씬 내 몬스터 검색 (GC 및 연산 최적화)
        if (Time.time - lastSearchTime > 0.5f)
        {
            lastSearchTime = Time.time;
            cachedMonsters = FindObjectsByType<Monster>(FindObjectsSortMode.None);
        }
    }

    private void OnGUI()
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (mainCam == null || cachedMonsters == null || cachedMonsters.Length == 0) return;

        InitStyleIfNeeded();

        foreach (var monster in cachedMonsters)
        {
            if (monster == null || !monster.gameObject.activeInHierarchy) continue;

            // 보스 몬스터는 전용 화면 상단 대형 HUD를 사용하므로 머리 위 HUD 대상 제외
            if (monster is BossMonster) continue;

            var stats = monster.GetComponent<CombatStats>();
            if (stats == null || stats.CurrentHp <= 0f) continue; // 사망한 몬스터 오버레이 비활성화

            Vector3 worldHeadPos = monster.transform.position + Vector3.up * YOffset;
            Vector3 screenPos = mainCam.WorldToScreenPoint(worldHeadPos);

            // 카메라 뷰포트 절두체 프러스텀 컬링 (화면 밖 몬스터 렌더링 연산 차단)
            if (screenPos.z <= 0f) continue;
            if (screenPos.x < 0f || screenPos.x > Screen.width || screenPos.y < 0f || screenPos.y > Screen.height) continue;

            // GUI 좌표계 변환 (Y축 반전)
            float guiX = screenPos.x - BarWidth * 0.5f;
            float guiY = Screen.height - screenPos.y;

            // 1. 몬스터 이름 라벨
            string displayName = string.IsNullOrEmpty(monster.UnitName) ? monster.name : monster.UnitName;
            Rect nameRect = new Rect(guiX - 20f, guiY - 18f, BarWidth + 40f, 16f);
            GUI.Label(nameRect, displayName, nameStyle);

            // 2. HP 게이지 바 (Red/Crimson)
            Rect hpBarBackground = new Rect(guiX, guiY, BarWidth, HpBarHeight);
            GUI.color = new Color(0.1f, 0.1f, 0.1f, 0.8f);
            GUI.DrawTexture(hpBarBackground, Texture2D.whiteTexture);

            float hpRatio = stats.MaxHp > 0f ? Mathf.Clamp01(stats.CurrentHp / stats.MaxHp) : 0f;
            Rect hpBarFill = new Rect(guiX, guiY, BarWidth * hpRatio, HpBarHeight);
            GUI.color = new Color(0.9f, 0.2f, 0.2f, 1.0f);
            GUI.DrawTexture(hpBarFill, Texture2D.whiteTexture);

            // 3. Posture 게이지 바 (Gold/Yellow)
            float postureY = guiY + HpBarHeight + 2f;
            Rect postureBarBackground = new Rect(guiX, postureY, BarWidth, PostureBarHeight);
            GUI.color = new Color(0.1f, 0.1f, 0.1f, 0.8f);
            GUI.DrawTexture(postureBarBackground, Texture2D.whiteTexture);

            float postureRatio = stats.MaxPosture > 0f ? Mathf.Clamp01(stats.CurrentPosture / stats.MaxPosture) : 0f;
            Rect postureBarFill = new Rect(guiX, postureY, BarWidth * postureRatio, PostureBarHeight);
            GUI.color = stats.IsGroggy ? Color.red : new Color(1.0f, 0.8f, 0.1f, 1.0f);
            GUI.DrawTexture(postureBarFill, Texture2D.whiteTexture);

            // 색상 원복
            GUI.color = Color.white;
        }
#endif
    }

    private void InitStyleIfNeeded()
    {
        if (styleInited) return;

        nameStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 11,
            alignment = TextAnchor.MiddleCenter,
            fontStyle = FontStyle.Bold
        };
        nameStyle.normal.textColor = Color.white;

        styleInited = true;
    }
}
