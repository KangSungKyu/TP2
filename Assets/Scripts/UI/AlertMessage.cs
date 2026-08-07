using System;
using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;

[RequireComponent(typeof(CanvasGroup))]
public sealed class AlertMessage : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI messageText;
    [SerializeField] private CanvasGroup canvasGroup;

    public uint CurrentTextIdx { get; private set; }
    public bool IsVisible { get; private set; }

    private uint generation;

    private void OnEnable()
    {
        HideImmediate();
    }

    private void OnDisable()
    {
        generation++;
        HideImmediate();
    }

    public bool Show(uint textIdx, float durationSeconds = 2f)
    {
        if (textIdx == 0 || messageText == null || canvasGroup == null) return false;
        if (IsVisible && CurrentTextIdx == textIdx) return true;

        string message = ResolveText(textIdx);
        if (string.IsNullOrEmpty(message))
        {
            Debug.LogWarning($"[AlertMessage] Missing TextData idx {textIdx}.");
        }
        else if (messageText.font != null && !messageText.font.HasCharacters(message))
        {
            Debug.LogWarning($"[AlertMessage] TextData idx {textIdx} contains unsupported glyphs.");
        }

        if (string.IsNullOrEmpty(message)) return false;

        CurrentTextIdx = textIdx;
        messageText.SetText(message);
        canvasGroup.alpha = 1f;
        IsVisible = true;

        uint currentGeneration = ++generation;
        if (durationSeconds > 0f) HideAfterDelayAsync(currentGeneration, durationSeconds).Forget();
        return true;
    }

    public bool ShowDevelopmentFallback(string message, float durationSeconds = 2f)
    {
        if (string.IsNullOrEmpty(message) || messageText == null || canvasGroup == null) return false;
        CurrentTextIdx = 0;
        messageText.SetText(message);
        canvasGroup.alpha = 1f;
        IsVisible = true;
        uint currentGeneration = ++generation;
        if (durationSeconds > 0f) HideAfterDelayAsync(currentGeneration, durationSeconds).Forget();
        return true;
    }

    private static string ResolveText(uint textIdx)
    {
        if (textIdx == 0 || DataTableManager.Instance == null) return string.Empty;
        var table = DataTableManager.Instance.GetDB<TextDataTable>(DataTableType.Text);
        return table != null ? table.GetText(textIdx) : string.Empty;
    }

    private async UniTaskVoid HideAfterDelayAsync(uint currentGeneration, float durationSeconds)
    {
        try
        {
            await UniTask.Delay(TimeSpan.FromSeconds(durationSeconds), cancellationToken: this.GetCancellationTokenOnDestroy());
            if (currentGeneration != generation || !isActiveAndEnabled) return;
            HideImmediate();
        }
        catch (OperationCanceledException)
        {
        }
    }

    private void HideImmediate()
    {
        IsVisible = false;
        CurrentTextIdx = 0;
        if (canvasGroup != null) canvasGroup.alpha = 0f;
        if (messageText != null) messageText.SetText(string.Empty);
    }
}
