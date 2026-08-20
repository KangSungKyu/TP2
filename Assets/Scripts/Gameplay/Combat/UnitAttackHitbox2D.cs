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
    [SerializeField] private Color activeColor = new Color(1f, 0.2f, 0.2f, 0.5f);
    [SerializeField] private Color inactiveColor = new Color(1f, 0.8f, 0.1f, 0.25f);

    private UnitBase owner;
    private Vector2 previousCenter;
    private bool windowActive;
    private bool telegraphedActive;
    private uint hitGeneration;

    public Color ActiveColor => activeColor;
    public Color InactiveColor => inactiveColor;
    public Color CurrentDebugColor => debugHitboxSprite != null ? debugHitboxSprite.color : (windowActive ? activeColor : inactiveColor);
    public bool IsWindowActive => windowActive && attackCollider != null && attackCollider.enabled;
    public bool IsTelegraphedActive => telegraphedActive;
    public bool IsDebugVisualizationActive
    {
        get
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            return DebugVisualizationEnabled && (IsWindowActive || telegraphedActive);
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

    public void SetTelegraphed(bool telegraphed)
    {
        telegraphedActive = telegraphed;
        UpdateDebugVisualization();
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
        UpdateDebugVisualization();
        return true;
    }

    public void Close()
    {
        windowActive = false;
        if (attackCollider != null) attackCollider.enabled = false;
        UpdateDebugVisualization();
        previousCenter = default;
    }

    private void OnDisable()
    {
        windowActive = false;
        telegraphedActive = false;
        if (attackCollider != null) attackCollider.enabled = false;
        UpdateDebugVisualization();
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
        Color drawColor = windowActive ? activeColor : inactiveColor;
        Debug.DrawLine(bottomLeft, topLeft, drawColor);
        Debug.DrawLine(topLeft, topRight, drawColor);
        Debug.DrawLine(topRight, bottomRight, drawColor);
        Debug.DrawLine(bottomRight, bottomLeft, drawColor);
    }
#endif

    private void SetWindowActive(bool active)
    {
        windowActive = active;
        if (!active) telegraphedActive = false;
        if (attackCollider != null) attackCollider.enabled = active;
        UpdateDebugVisualization();
    }

    private void UpdateDebugVisualization()
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        bool visible = DebugVisualizationEnabled && (windowActive || telegraphedActive);
        Color currentColor = windowActive ? activeColor : inactiveColor;
        if (debugHitboxSprite != null)
        {
            debugHitboxSprite.color = currentColor;
            debugHitboxSprite.enabled = visible;
        }
        if (debugHitboxLine != null)
        {
            debugHitboxLine.startColor = currentColor;
            debugHitboxLine.endColor = currentColor;
            debugHitboxLine.enabled = debugHitboxSprite == null && visible;
        }
#else
        if (debugHitboxSprite != null) debugHitboxSprite.enabled = false;
        if (debugHitboxLine != null) debugHitboxLine.enabled = false;
#endif
    }
}
