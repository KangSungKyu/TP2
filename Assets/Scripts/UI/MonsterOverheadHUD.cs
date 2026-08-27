using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class MonsterOverheadHUD : MonoBehaviour
{
    [SerializeField] private Monster owner;
    [SerializeField] private CanvasGroup group;
    [SerializeField] private Image hpFill;
    [SerializeField] private Image postureFill;
    [SerializeField] private TMP_Text nameText;
    [Header("Attack Telegraph")]
    [SerializeField] private CanvasGroup attackTelegraphGroup;
    [SerializeField] private Image attackTelegraphFill;
    [SerializeField] private Color attackTelegraphWarningColor = Color.white;
    [SerializeField] private Color attackTelegraphActiveColor = Color.red;

    private CombatStats stats;
    private Image attackTelegraphBackground;
    private Monster.AttackTelegraph attackTelegraph;
    private bool hasAttackTelegraph;

    private void OnEnable()
    {
        EnsureTelegraphBackground();
        EnsureFillBackground(hpFill);
        EnsureFillBackground(postureFill);
        Monster.AttackTelegraphStarted += OnAttackTelegraphStarted;
        Monster.AttackTelegraphEnded += OnAttackTelegraphEnded;
        SetTelegraphVisible(false);
        if (owner == null || owner is BossMonster)
        {
            SetVisible(false);
            return;
        }

        Bind(owner.Stats);
    }

    private void OnDisable()
    {
        Monster.AttackTelegraphStarted -= OnAttackTelegraphStarted;
        Monster.AttackTelegraphEnded -= OnAttackTelegraphEnded;
        hasAttackTelegraph = false;
        SetTelegraphVisible(false);
        Bind(null);
    }

    private void Update()
    {
        if (!hasAttackTelegraph || owner == null || !owner.isActiveAndEnabled ||
            !owner.IsActionGenerationCurrent(attackTelegraph.Generation) ||
            Time.time > attackTelegraph.ActiveEndsAt)
        {
            hasAttackTelegraph = false;
            SetTelegraphVisible(false);
            return;
        }

        bool visible = Time.time >= attackTelegraph.WarningStartsAt;
        SetTelegraphVisible(visible);
        if (!visible || attackTelegraphFill == null) return;
        attackTelegraphFill.fillAmount = ProductionMainHUD.CalculateAttackTelegraphFill(
            Time.time, attackTelegraph.WarningStartsAt, attackTelegraph.ImpactAt);
        attackTelegraphFill.color = Time.time >= attackTelegraph.ImpactAt
            ? attackTelegraphActiveColor : attackTelegraphWarningColor;
    }

    private void OnAttackTelegraphStarted(Monster.AttackTelegraph telegraph)
    {
        if (telegraph.Source != owner || owner is BossMonster) return;
        attackTelegraph = telegraph;
        hasAttackTelegraph = true;
    }

    private void OnAttackTelegraphEnded(Monster source, uint generation)
    {
        if (!hasAttackTelegraph || source != owner || attackTelegraph.Generation != generation) return;
        hasAttackTelegraph = false;
        SetTelegraphVisible(false);
    }

    public void Bind(CombatStats target)
    {
        if (stats != null)
        {
            stats.OnHpChanged.RemoveListener(SetHp);
            stats.OnPostureChanged.RemoveListener(SetPosture);
        }

        stats = target;
        SetVisible(stats != null && owner != null && !(owner is BossMonster));
        if (stats == null) return;

        stats.OnHpChanged.AddListener(SetHp);
        stats.OnPostureChanged.AddListener(SetPosture);
        if (nameText != null) nameText.SetText(owner.UnitName);
        SetHp(Ratio(stats.CurrentHp, stats.MaxHp));
        SetPosture(Ratio(stats.CurrentPosture, stats.MaxPosture));
    }

    private void SetHp(float value)
    {
        SetFill(hpFill, value);
        SetVisible(value > 0f && stats != null && owner != null && !(owner is BossMonster));
    }
    private void SetPosture(float value) => SetFill(postureFill, value);
    private void SetVisible(bool visible)
    {
        if (group != null) group.alpha = visible ? 1f : 0f;
    }
    private void SetTelegraphVisible(bool visible)
    {
        if (attackTelegraphGroup != null) attackTelegraphGroup.alpha = visible ? 1f : 0f;
    }

    private void EnsureTelegraphBackground()
    {
        if (attackTelegraphGroup == null || attackTelegraphFill == null) return;
        attackTelegraphBackground = attackTelegraphGroup.GetComponent<Image>();
        if (attackTelegraphBackground == null)
            attackTelegraphBackground = attackTelegraphGroup.gameObject.AddComponent<Image>();
        attackTelegraphBackground.sprite = attackTelegraphFill.sprite;
        attackTelegraphBackground.type = Image.Type.Sliced;
        attackTelegraphBackground.color = new Color(0f, 0f, 0f, .9f);
        attackTelegraphBackground.raycastTarget = false;
        attackTelegraphFill.transform.SetAsLastSibling();
    }

    private void EnsureFillBackground(Image fill)
    {
        if (fill == null || fill.transform.parent == null || fill.transform.GetSiblingIndex() <= 0) return;
        Transform backgroundTransform = fill.transform.parent.GetChild(fill.transform.GetSiblingIndex() - 1);
        Image background = backgroundTransform.GetComponent<Image>();
        if (background == null) return;
        background.sprite = fill.sprite;
        background.material = fill.material;
        background.type = Image.Type.Sliced;
        background.color = new Color(0f, 0f, 0f, .9f);
        background.raycastTarget = false;
        if (background.rectTransform != null && fill.rectTransform != null)
        {
            background.rectTransform.anchorMin = fill.rectTransform.anchorMin;
            background.rectTransform.anchorMax = fill.rectTransform.anchorMax;
            background.rectTransform.pivot = fill.rectTransform.pivot;
            background.rectTransform.anchoredPosition = fill.rectTransform.anchoredPosition;
            background.rectTransform.sizeDelta = fill.rectTransform.sizeDelta;
        }
        backgroundTransform.SetSiblingIndex(fill.transform.GetSiblingIndex() - 1);
    }

    private static void SetFill(Image image, float value)
    {
        if (image != null) image.fillAmount = Mathf.Clamp01(value);
    }

    private static float Ratio(float current, float maximum) =>
        maximum > 0f ? Mathf.Clamp01(current / maximum) : 0f;
}
