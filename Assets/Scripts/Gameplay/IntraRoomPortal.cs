using System.Collections.Generic;
using UnityEngine;

public sealed class IntraRoomPortal : MonoBehaviour
{
    [SerializeField] private uint portalIdx;
    [SerializeField] private uint chunkResourceIdx;
    [SerializeField] private uint sourceZoneIdx;
    [SerializeField] private uint destinationZoneIdx;
    [SerializeField] private uint portalPairIdx;
    [SerializeField] private Transform destinationEndpoint;

    private readonly HashSet<Collider2D> playerCandidates = new HashSet<Collider2D>();
    private bool transitionLocked;
    private bool requiresTriggerExit;
    private int lastInteractionFrame = -1;

    private void Update()
    {
        if (TryConsumeInteraction(RoomDoorPortal.WasInteractionPressedThisFrame()))
        {
            requiresTriggerExit = true;
            TryTeleport();
        }
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        Player player = Player.Instance;
        if (player != null && (collision.transform == player.transform || collision.transform.IsChildOf(player.transform)))
            playerCandidates.Add(collision);
    }

    private void OnTriggerExit2D(Collider2D collision)
    {
        playerCandidates.Remove(collision);
        if (playerCandidates.Count == 0) requiresTriggerExit = false;
    }

    private void OnDisable()
    {
        playerCandidates.Clear();
        transitionLocked = false;
        requiresTriggerExit = false;
    }

    public void Configure(uint idx, uint chunkIdx, uint sourceZone, uint destinationZone,
        uint pairIdx, Transform endpoint)
    {
        portalIdx = idx;
        chunkResourceIdx = chunkIdx;
        sourceZoneIdx = sourceZone;
        destinationZoneIdx = destinationZone;
        portalPairIdx = pairIdx;
        destinationEndpoint = endpoint;
    }

    public bool TryTeleport()
    {
        if (transitionLocked) return false;
        transitionLocked = true;
        try
        {
            StageManager stage = StageManager.Instance;
            Player player = Player.Instance;
            if (portalIdx == 0 || chunkResourceIdx == 0 || portalPairIdx == 0 || destinationEndpoint == null ||
                stage == null || player == null || player.Motor == null || !player.Motor.IsGrounded)
            {
                Debug.LogError($"[IntraRoomPortal] Invalid uint FK or endpoint at portal idx {portalIdx}.");
                requiresTriggerExit = false;
                return false;
            }
            if (Monster.ActiveMonsters.Count > 0)
            {
                requiresTriggerExit = false;
                return false;
            }
            if (!stage.TryEnterIntraRoomZone(chunkResourceIdx, sourceZoneIdx, destinationZoneIdx, portalPairIdx))
            {
                Debug.LogError($"[IntraRoomPortal] Zone transition rejected at portal idx {portalIdx}.");
                requiresTriggerExit = false;
                return false;
            }

            UnitPoolManager.Instance?.DespawnAllProjectiles();
            EffectPoolManager.Instance?.ClearAllActiveEffects();
            player.GetComponent<SkillExecutor>()?.CancelActiveEffects();
            player.Motor.Teleport(destinationEndpoint.position);
            player.Motor.SetGroundNormal(Vector2.up);
            MetroidvaniaCamera2D.Active?.BindAndSnap(player.transform);
            return true;
        }
        finally
        {
            transitionLocked = false;
        }
    }

    private bool TryConsumeInteraction(bool pressed)
    {
        Player player = Player.Instance;
        if (!pressed || !isActiveAndEnabled || transitionLocked || requiresTriggerExit ||
            playerCandidates.Count == 0 || player == null || player.Motor == null ||
            !player.Motor.IsGrounded || lastInteractionFrame == Time.frameCount)
            return false;
        lastInteractionFrame = Time.frameCount;
        return true;
    }
}
