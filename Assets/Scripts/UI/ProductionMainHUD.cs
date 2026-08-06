using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class ProductionMainHUD : MonoBehaviour
{
    [Header("Player")]
    [SerializeField] private Image playerHpFill;
    [SerializeField] private Image playerPostureFill;
    [SerializeField] private Image playerMpFill;
    [Header("Monster")]
    [SerializeField] private CanvasGroup monsterGroup;
    [SerializeField] private Image monsterHpFill;
    [SerializeField] private Image monsterPostureFill;
    [Header("Boss")]
    [SerializeField] private CanvasGroup bossGroup;
    [SerializeField] private Image bossHpFill;
    [SerializeField] private Image bossPostureFill;
    [SerializeField] private TMP_Text bossNameText;
    [Header("Stage")]
    [SerializeField] private TMP_Text stageProgressText;
    [SerializeField] private AlertMessage alertMessage;

    private CombatStats playerStats;
    private Monster activeMonster;
    private CombatStats monsterStats;
    private BossMonster activeBoss;
    private CombatStats bossStats;
    private StageManager stageManager;

    private void OnEnable()
    {
        if (!Application.isPlaying) return;
        Player.Activated += OnPlayerActivated;
        Player.Deactivated += OnPlayerDeactivated;
        Monster.Activated += OnMonsterActivated;
        Monster.Deactivated += OnMonsterDeactivated;
        BindStageManager(StageManager.Instance);
    }

    private void OnDisable()
    {
        Player.Activated -= OnPlayerActivated;
        Player.Deactivated -= OnPlayerDeactivated;
        Monster.Activated -= OnMonsterActivated;
        Monster.Deactivated -= OnMonsterDeactivated;
        BindPlayer(null);
        BindMonster(null);
        BindBoss(null);
        BindStageManager(null);
    }

    public void BindSceneState()
    {
        BindPlayer(Player.Instance != null ? Player.Instance.Stats : null);
        BindStageManager(StageManager.Instance);
        if (stageManager != null) stageManager.PublishProgress();
        foreach (Monster monster in Monster.ActiveMonsters) OnMonsterActivated(monster);
    }

    public void BindPlayer(CombatStats target)
    {
        if (playerStats == target)
        {
            RefreshPlayer();
            return;
        }
        if (playerStats != null)
        {
            playerStats.OnHpChanged.RemoveListener(SetPlayerHp);
            playerStats.OnPostureChanged.RemoveListener(SetPlayerPosture);
            playerStats.OnMpChanged.RemoveListener(SetPlayerMp);
        }
        playerStats = target;
        if (playerStats == null) return;
        playerStats.OnHpChanged.AddListener(SetPlayerHp);
        playerStats.OnPostureChanged.AddListener(SetPlayerPosture);
        playerStats.OnMpChanged.AddListener(SetPlayerMp);
        RefreshPlayer();
    }

    private void OnPlayerActivated(Player player) => BindPlayer(player != null ? player.Stats : null);
    private void OnPlayerDeactivated(Player player)
    {
        if (player != null && player.Stats == playerStats) BindPlayer(null);
    }

    private void RefreshPlayer()
    {
        if (playerStats == null) return;
        SetPlayerHp(Ratio(playerStats.CurrentHp, playerStats.MaxHp));
        SetPlayerPosture(Ratio(playerStats.CurrentPosture, playerStats.MaxPosture));
        SetPlayerMp(Ratio(playerStats.CurrentMp, playerStats.MaxMp));
    }

    public bool ShowPrompt(uint textIdx, uint englishFallbackTextIdx = 0, float durationSeconds = 2f) =>
        alertMessage != null && alertMessage.Show(textIdx, englishFallbackTextIdx, durationSeconds);

    public bool ShowWarning(uint textIdx, uint englishFallbackTextIdx = 0, float durationSeconds = 2f) =>
        ShowPrompt(textIdx, englishFallbackTextIdx, durationSeconds);

    private void OnMonsterActivated(Monster monster)
    {
        if (monster == null) return;
        if (monster is BossMonster boss) BindBoss(boss);
        else if (activeMonster == null) BindMonster(monster);
    }

    private void OnMonsterDeactivated(Monster monster)
    {
        if (monster == activeBoss) BindBoss(null);
        if (monster != activeMonster) return;
        BindMonster(null);
        foreach (Monster candidate in Monster.ActiveMonsters)
        {
            if (candidate != null && !(candidate is BossMonster))
            {
                BindMonster(candidate);
                break;
            }
        }
    }

    private void BindMonster(Monster monster)
    {
        if (activeMonster == monster)
        {
            RefreshMonster();
            return;
        }
        if (monsterStats != null)
        {
            monsterStats.OnHpChanged.RemoveListener(SetMonsterHp);
            monsterStats.OnPostureChanged.RemoveListener(SetMonsterPosture);
        }
        activeMonster = monster;
        monsterStats = monster != null ? monster.Stats : null;
        SetVisible(monsterGroup, monsterStats != null);
        if (monsterStats == null) return;
        monsterStats.OnHpChanged.AddListener(SetMonsterHp);
        monsterStats.OnPostureChanged.AddListener(SetMonsterPosture);
        RefreshMonster();
    }

    private void RefreshMonster()
    {
        if (monsterStats == null) return;
        SetMonsterHp(Ratio(monsterStats.CurrentHp, monsterStats.MaxHp));
        SetMonsterPosture(Ratio(monsterStats.CurrentPosture, monsterStats.MaxPosture));
    }

    private void BindBoss(BossMonster boss)
    {
        if (activeBoss == boss)
        {
            RefreshBoss();
            return;
        }
        if (bossStats != null)
        {
            bossStats.OnHpChanged.RemoveListener(SetBossHp);
            bossStats.OnPostureChanged.RemoveListener(SetBossPosture);
        }
        activeBoss = boss;
        bossStats = boss != null ? boss.Stats : null;
        SetVisible(bossGroup, bossStats != null);
        if (bossNameText != null) bossNameText.SetText(boss != null ? boss.UnitName : string.Empty);
        if (bossStats == null) return;
        bossStats.OnHpChanged.AddListener(SetBossHp);
        bossStats.OnPostureChanged.AddListener(SetBossPosture);
        RefreshBoss();
    }

    private void RefreshBoss()
    {
        if (bossNameText != null) bossNameText.SetText(activeBoss != null ? activeBoss.UnitName : string.Empty);
        if (bossStats == null) return;
        SetBossHp(Ratio(bossStats.CurrentHp, bossStats.MaxHp));
        SetBossPosture(Ratio(bossStats.CurrentPosture, bossStats.MaxPosture));
    }

    private void BindStageManager(StageManager target)
    {
        if (stageManager == target) return;
        if (stageManager != null) stageManager.ProgressChanged -= SetStageProgress;
        stageManager = target;
        if (stageManager != null) stageManager.ProgressChanged += SetStageProgress;
    }

    private void SetPlayerHp(float value) => SetFill(playerHpFill, value);
    private void SetPlayerPosture(float value) => SetFill(playerPostureFill, value);
    private void SetPlayerMp(float value) => SetFill(playerMpFill, value);
    private void SetMonsterHp(float value) => SetFill(monsterHpFill, value);
    private void SetMonsterPosture(float value) => SetFill(monsterPostureFill, value);
    private void SetBossHp(float value) => SetFill(bossHpFill, value);
    private void SetBossPosture(float value) => SetFill(bossPostureFill, value);

    private void SetStageProgress(uint stageIdx, int visited, int total)
    {
        if (stageProgressText != null) stageProgressText.SetText("{0}  {1}/{2}", stageIdx, visited, total);
    }

    private static void SetFill(Image image, float value)
    {
        if (image != null) image.fillAmount = Mathf.Clamp01(value);
    }

    private static void SetVisible(CanvasGroup group, bool visible)
    {
        if (group != null) group.alpha = visible ? 1f : 0f;
    }

    private static float Ratio(float current, float maximum) => maximum > 0f ? Mathf.Clamp01(current / maximum) : 0f;
}
