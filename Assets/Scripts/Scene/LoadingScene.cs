using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;
using TMPro;

public class LoadingScene : MonoBehaviour
{
    [Header("UI Elements")]
    [SerializeField] private Image progressBar;
    [SerializeField] private TextMeshProUGUI progressText;
    private CanvasGroup canvasGroup;

    private void Awake()
    {
        canvasGroup = GetComponent<CanvasGroup>();
        if (canvasGroup == null)
        {
            canvasGroup = gameObject.AddComponent<CanvasGroup>();
        }
        canvasGroup.alpha = 0f; // start invisible
    }

    private void OnEnable()
    {
        // fade‑in (0.3s)
        canvasGroup.DOFade(1f, 0.3f).SetEase(Ease.OutQuad);
        LoadingBarController.Instance?.Register(this);
    }

    public void SetProgress(float p)
    {
        if (progressBar != null) progressBar.fillAmount = Mathf.Clamp01(p);
        if (progressText != null) progressText.text = (p * 100f).ToString("F0") + "%";
    }

    private void OnDisable()
    {
        // fade‑out (0.2s)
        canvasGroup.DOFade(0f, 0.2f).SetEase(Ease.InQuad).OnComplete(() =>
        {
            LoadingBarController.Instance?.Unregister();
        });
    }
}
