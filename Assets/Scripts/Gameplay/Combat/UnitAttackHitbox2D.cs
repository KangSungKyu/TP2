using UnityEngine;

public sealed class UnitAttackHitbox2D : MonoBehaviour
{
#if UNITY_EDITOR || DEVELOPMENT_BUILD
    public static bool DebugVisualizationEnabled { get; set; } = true;
#else
    public static bool DebugVisualizationEnabled { get; set; }
#endif

    [SerializeField] private Collider2D attackCollider;
    [SerializeField] private Transform attachRoot;
    [SerializeField] private SpriteRenderer debugHitboxSprite;
    [SerializeField] private LineRenderer debugHitboxLine;

    private UnitBase owner;
    private Vector2 previousCenter;
    private bool windowActive;
    private uint hitGeneration;

    public bool IsWindowActive => windowActive && attackCollider != null && attackCollider.enabled;
    public bool IsDebugVisualizationActive
    {
        get
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            return DebugVisualizationEnabled && IsWindowActive;
#else
            return false;
#endif
        }
    }

    public void Bind(UnitBase unit)
    {
        owner = unit;
        SetWindowActive(false);
    }

    public void SetFacingRight(bool facingRight)
    {
        if (attachRoot == null) return;
        Vector3 scale = attachRoot.localScale;
        scale.x = Mathf.Abs(scale.x) * (facingRight ? 1f : -1f);
        attachRoot.localScale = scale;
    }

    public bool TryOpen(int sourceId, uint generation, uint tick, out CombatStats.AttackSweep2D sweep)
    {
        sweep = default;
        if (owner == null || attackCollider == null || attachRoot == null || !attackCollider.isTrigger)
        {
            Debug.LogError($"[UnitAttackHitbox2D] Unit idx {(owner != null ? owner.UnitIdx : 0u)} has no valid serialized attack collider/attach root.");
            SetWindowActive(false);
            return false;
        }

        if (!windowActive) hitGeneration++;
        attackCollider.enabled = true;
        windowActive = true;
        Physics2D.SyncTransforms();
        Bounds bounds = attackCollider.bounds;
        Vector2 current = bounds.center;
        if (previousCenter == default) previousCenter = owner.transform.position;
        sweep = new CombatStats.AttackSweep2D(previousCenter, current, bounds.extents, sourceId, hitGeneration, tick);
        previousCenter = current;
        SetDebugVisible(true);
        return true;
    }

    public void Close()
    {
        SetWindowActive(false);
        previousCenter = default;
    }

    private void OnDisable()
    {
        windowActive = false;
        if (attackCollider != null) attackCollider.enabled = false;
        SetDebugVisible(false);
    }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    private void LateUpdate()
    {
        if (!IsDebugVisualizationActive || attackCollider == null ||
            debugHitboxSprite != null || debugHitboxLine != null) return;
        Bounds bounds = attackCollider.bounds;
        Vector3 bottomLeft = new Vector3(bounds.min.x, bounds.min.y, transform.position.z);
        Vector3 topLeft = new Vector3(bounds.min.x, bounds.max.y, transform.position.z);
        Vector3 topRight = new Vector3(bounds.max.x, bounds.max.y, transform.position.z);
        Vector3 bottomRight = new Vector3(bounds.max.x, bounds.min.y, transform.position.z);
        Debug.DrawLine(bottomLeft, topLeft, Color.red);
        Debug.DrawLine(topLeft, topRight, Color.red);
        Debug.DrawLine(topRight, bottomRight, Color.red);
        Debug.DrawLine(bottomRight, bottomLeft, Color.red);
    }
#endif

    private void SetWindowActive(bool active)
    {
        windowActive = active;
        if (attackCollider != null) attackCollider.enabled = active;
        SetDebugVisible(active);
    }

    private void SetDebugVisible(bool windowVisible)
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        bool visible = DebugVisualizationEnabled && windowVisible;
        if (debugHitboxSprite != null) debugHitboxSprite.enabled = visible;
        if (debugHitboxLine != null) debugHitboxLine.enabled = debugHitboxSprite == null && visible;
#else
        if (debugHitboxSprite != null) debugHitboxSprite.enabled = false;
        if (debugHitboxLine != null) debugHitboxLine.enabled = false;
#endif
    }
}
