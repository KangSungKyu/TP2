using TMPro;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public sealed class ProductionMainHUD : MonoBehaviour
{
    [Header("Player")]
    [SerializeField] private Image playerHpFill;
    [SerializeField] private Image playerPostureFill;
    [SerializeField] private Image playerMpFill;
    [SerializeField] private TMP_Text playerHpText;
    [SerializeField] private TMP_Text playerPostureText;
    [SerializeField] private TMP_Text playerMpText;
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
    [Header("Attack Telegraph")]
    [SerializeField] private CanvasGroup attackTelegraphGroup;
    [SerializeField] private Image attackTelegraphFill;
    [SerializeField] private Color attackTelegraphWarningColor = Color.white;
    [SerializeField] private Color attackTelegraphActiveColor = Color.red;

    private CombatStats playerStats;
    private Monster activeMonster;
    private CombatStats monsterStats;
    private BossMonster activeBoss;
    private CombatStats bossStats;
    private StageManager stageManager;
    private readonly Dictionary<Monster, Monster.AttackTelegraph> attackTelegraphs =
        new Dictionary<Monster, Monster.AttackTelegraph>();

    private void OnEnable()
    {
        if (stageProgressText != null) stageProgressText.gameObject.SetActive(false);
        if (!Application.isPlaying) return;
        if (transform.localScale == Vector3.zero) transform.localScale = Vector3.one;
        Player.Activated += OnPlayerActivated;
        Player.Deactivated += OnPlayerDeactivated;
        Monster.Activated += OnMonsterActivated;
        Monster.Deactivated += OnMonsterDeactivated;
        Monster.AttackTelegraphStarted += OnAttackTelegraphStarted;
        Monster.AttackTelegraphEnded += OnAttackTelegraphEnded;
        SetVisible(attackTelegraphGroup, false);
        BindSceneState();
    }

    private void OnDisable()
    {
        Player.Activated -= OnPlayerActivated;
        Player.Deactivated -= OnPlayerDeactivated;
        Monster.Activated -= OnMonsterActivated;
        Monster.Deactivated -= OnMonsterDeactivated;
        Monster.AttackTelegraphStarted -= OnAttackTelegraphStarted;
        Monster.AttackTelegraphEnded -= OnAttackTelegraphEnded;
        attackTelegraphs.Clear();
        SetVisible(attackTelegraphGroup, false);
        BindPlayer(null);
        BindMonster(null);
        BindBoss(null);
        BindStageManager(null);
    }

    private void Update()
    {
        Monster.AttackTelegraph selected = default;
        bool hasSelection = false;
        float now = Time.time;
        foreach (Monster.AttackTelegraph candidate in attackTelegraphs.Values)
        {
            if (candidate.Source == null || !candidate.Source.isActiveAndEnabled ||
                !candidate.Source.IsActionGenerationCurrent(candidate.Generation) ||
                now > candidate.ActiveEndsAt) continue;
            if (!hasSelection || candidate.ImpactAt < selected.ImpactAt)
            {
                selected = candidate;
                hasSelection = true;
            }
        }

        bool visible = hasSelection && now >= selected.WarningStartsAt && now <= selected.ActiveEndsAt;
        SetVisible(attackTelegraphGroup, visible);
        if (!visible || attackTelegraphFill == null) return;

        attackTelegraphFill.fillAmount = CalculateAttackTelegraphFill(
            now, selected.WarningStartsAt, selected.ImpactAt);
        attackTelegraphFill.color = now >= selected.ImpactAt
            ? attackTelegraphActiveColor : attackTelegraphWarningColor;
    }

    private void OnAttackTelegraphStarted(Monster.AttackTelegraph telegraph)
    {
        if (telegraph.Source != null) attackTelegraphs[telegraph.Source] = telegraph;
    }

    private void OnAttackTelegraphEnded(Monster source, uint generation)
    {
        if (source != null && attackTelegraphs.TryGetValue(source, out Monster.AttackTelegraph current) &&
            current.Generation == generation) attackTelegraphs.Remove(source);
    }

    public static float CalculateAttackTelegraphFill(float now, float warningStartsAt, float impactAt)
    {
        float duration = impactAt - warningStartsAt;
        return now >= impactAt || duration <= 0f
            ? 1f : Mathf.Clamp01((now - warningStartsAt) / duration);
    }

    public void BindSceneState()
    {
        SetVisible(monsterGroup, false);
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

    public bool ShowPrompt(uint textIdx, float durationSeconds = 2f) =>
        alertMessage != null && alertMessage.Show(textIdx, durationSeconds);

    public bool ShowWarning(uint textIdx, float durationSeconds = 2f) =>
        ShowPrompt(textIdx, durationSeconds);

    private void OnMonsterActivated(Monster monster)
    {
        if (monster is BossMonster boss && boss.UnitData != null && boss.isActiveAndEnabled) BindBoss(boss);
    }

    private void OnMonsterDeactivated(Monster monster)
    {
        if (monster == activeBoss) BindBoss(null);
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

    private void SetPlayerHp(float value)
    {
        SetFill(playerHpFill, value);
        SetStatText(playerHpText, playerStats != null ? playerStats.CurrentHp : 0f, playerStats != null ? playerStats.MaxHp : 0f);
    }

    private void SetPlayerPosture(float value)
    {
        SetFill(playerPostureFill, value);
        SetStatText(playerPostureText, playerStats != null ? playerStats.CurrentPosture : 0f, playerStats != null ? playerStats.MaxPosture : 0f);
    }

    private void SetPlayerMp(float value)
    {
        SetFill(playerMpFill, value);
        SetStatText(playerMpText, playerStats != null ? playerStats.CurrentMp : 0f, playerStats != null ? playerStats.MaxMp : 0f);
    }
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

    private static void SetStatText(TMP_Text text, float current, float maximum)
    {
        if (text != null) text.SetText("{0:0}/{1:0}", current, maximum);
    }

    private static void SetVisible(CanvasGroup group, bool visible)
    {
        if (group != null) group.alpha = visible ? 1f : 0f;
    }

    private static float Ratio(float current, float maximum) => maximum > 0f ? Mathf.Clamp01(current / maximum) : 0f;
}
