using System;
using UnityEngine;

public sealed class ProductionMinimap : MonoBehaviour
{
    public enum RoomViewState : byte { Unknown, Visited, Cleared, Current, Boss }

    [Serializable]
    public sealed class RoomView
    {
        public RectTransform Root;
        public GameObject Unknown;
        public GameObject Visited;
        public GameObject Cleared;
        public GameObject Current;
        public GameObject Boss;
        public RoomViewState CurrentState { get; private set; }

        public void SetState(RoomViewState state)
        {
            CurrentState = state;
            if (Unknown != null) Unknown.SetActive(state == RoomViewState.Unknown);
            if (Visited != null) Visited.SetActive(state == RoomViewState.Visited);
            if (Cleared != null) Cleared.SetActive(state == RoomViewState.Cleared);
            if (Current != null) Current.SetActive(state == RoomViewState.Current);
            if (Boss != null) Boss.SetActive(state == RoomViewState.Boss);
        }
    }

    [SerializeField] private CanvasGroup minimapRoot;
    [SerializeField] private RoomView[] roomViews;
    [SerializeField] private Vector2 cellSize = new Vector2(48f, 48f);
    private StageManager stageManager;

    private void OnEnable()
    {
        if (!Application.isPlaying) return;
        Player.MinimapToggleRequested += Toggle;
        BindStage(StageManager.Instance);
        Hide();
    }

    private void OnDisable()
    {
        Player.MinimapToggleRequested -= Toggle;
        BindStage(null);
        Hide();
    }

    public void BindStage(StageManager target)
    {
        if (stageManager == target) return;
        if (stageManager != null) stageManager.ProgressChanged -= OnProgressChanged;
        stageManager = target;
        if (stageManager != null) stageManager.ProgressChanged += OnProgressChanged;
    }

    public void Toggle()
    {
        if (minimapRoot == null) return;
        bool visible = minimapRoot.alpha <= 0f;
        minimapRoot.alpha = visible ? 1f : 0f;
        minimapRoot.interactable = false;
        minimapRoot.blocksRaycasts = false;
        if (visible) Refresh();
    }

    public void Hide()
    {
        if (minimapRoot == null) return;
        minimapRoot.alpha = 0f;
        minimapRoot.interactable = false;
        minimapRoot.blocksRaycasts = false;
    }

    public void Refresh()
    {
        if (roomViews == null) return;
        StageRunData run = stageManager != null ? stageManager.CurrentRun : null;
        for (int i = 0; i < roomViews.Length; i++)
        {
            RoomView view = roomViews[i];
            if (view == null) continue;
            if (view.Root != null && run != null && run.Columns > 0)
                view.Root.anchoredPosition = new Vector2((i % run.Columns) * cellSize.x, -(i / run.Columns) * cellSize.y);
            if (run == null || run.Columns == 0 || !run.TryGetSlot((byte)i, out ChunkSlotData slot)) view.SetState(RoomViewState.Unknown);
            else if (slot.SlotIdx == run.CurrentSlotIdx) view.SetState(RoomViewState.Current);
            else if (slot.SlotIdx == run.BossGateSlotIdx) view.SetState(RoomViewState.Boss);
            else if (slot.Cleared) view.SetState(RoomViewState.Cleared);
            else view.SetState(slot.Visited ? RoomViewState.Visited : RoomViewState.Unknown);
        }
    }

    private void OnProgressChanged(uint stageIdx, int visited, int total)
    {
        if (minimapRoot != null && minimapRoot.alpha > 0f) Refresh();
    }
}
