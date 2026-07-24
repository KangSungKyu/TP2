using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using Cysharp.Threading.Tasks;
using System.Threading;

[RequireComponent(typeof(CanvasGroup))]
public abstract class PanelBase : MonoBehaviour
{
    [SerializeField]
    protected bool isRegisterByName = true;
    [SerializeField]
    protected string registedName = string.Empty;
    [SerializeField]
    protected CanvasGroup canvasGroup = null;
    [SerializeField]
    protected Button exitBtn = null;

    protected bool isShow = false;
    // UniTask based panel handling; no Coroutine needed

    public void Show()
    {
        // Fire‑and‑forget async panel show
        OnPanelAsync(this.GetCancellationTokenOnDestroy()).Forget();
    }

    public void Hide()
    {
        // Fire‑and‑forget async panel hide
        OffPanelAsync(this.GetCancellationTokenOnDestroy()).Forget();
    }

    // Async versions for external callers to await panel show/hide operations
    public async UniTask ShowAsync(CancellationToken ct = default)
    {
        await OnPanelAsync(ct);
    }

    public async UniTask HideAsync(CancellationToken ct = default)
    {
        await OffPanelAsync(ct);
    }

    public void ForceHide()
    {
        // Immediately hide without awaiting
        if (OnPanelHide())
        {
            this.gameObject.SetActive(false);
        }
    }

    public void SetOnExitEvent(System.Action act)
    {
        if(exitBtn != null)
        {
            exitBtn.onClick.RemoveAllListeners();
            exitBtn.onClick.AddListener(() =>
            {
                act?.Invoke();
            });
        }
    }

    protected abstract bool OnPanelShow();
    protected abstract bool OnPanelHide();

    private void Start()
    {
        if(isRegisterByName)
        {
            PanelManager.RegisterPanel(registedName, this);
        }
        else
        {
            registedName = this.name;

            PanelManager.RegisterPanel(this);
        }

        SetOnExitEvent(Hide);

        if(canvasGroup == null)
        {
            canvasGroup = GetComponent<CanvasGroup>();
        }

        ForceHide();
    }

    private void OnDestroy()
    {
        if(isRegisterByName)
        {
            PanelManager.UnregisterPanel(registedName);
        }
        else
        {
            PanelManager.UnregisterPanel(this);
        }
    }

    private void OnPanel()
    {
        this.gameObject.SetActive(true);
        // Start async panel show without tracking
        OnPanelAsync(this.GetCancellationTokenOnDestroy()).Forget();
    }

    private void OffPanel()
    {
        // Start async panel hide without tracking
        OffPanelAsync(this.GetCancellationTokenOnDestroy()).Forget();
    }

    private void OnOffCanvasGroup(bool onoff)
    {
        canvasGroup.alpha = onoff ? 1f : 0f;
        canvasGroup.blocksRaycasts = onoff;
        canvasGroup.interactable = onoff;
    }

    private async UniTask OnPanelAsync(CancellationToken ct = default)
    {
        this.gameObject.SetActive(true);
        OnOffCanvasGroup(false);
        await UniTask.WaitUntil(() => OnPanelShow(), cancellationToken: ct);
        await UniTask.Yield(PlayerLoopTiming.Update, ct);
        OnOffCanvasGroup(true);
    }

    private async UniTask OffPanelAsync(CancellationToken ct = default)
    {
        await UniTask.WaitUntil(() => OnPanelHide(), cancellationToken: ct);
        OnOffCanvasGroup(false);
        await UniTask.Delay(System.TimeSpan.FromSeconds(0.5), cancellationToken: ct);
        DeactivePanel();
    }

    private void DeactivePanel()
    {
        this.gameObject.SetActive(false);
    }
}