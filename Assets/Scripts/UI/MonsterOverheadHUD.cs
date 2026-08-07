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

    private CombatStats stats;

    private void OnEnable()
    {
        if (owner == null || owner is BossMonster)
        {
            SetVisible(false);
            return;
        }

        Bind(owner.Stats);
    }

    private void OnDisable() => Bind(null);

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

    private static void SetFill(Image image, float value)
    {
        if (image != null) image.fillAmount = Mathf.Clamp01(value);
    }

    private static float Ratio(float current, float maximum) =>
        maximum > 0f ? Mathf.Clamp01(current / maximum) : 0f;
}
