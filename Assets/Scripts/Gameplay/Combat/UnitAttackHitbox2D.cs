using UnityEngine;
using UnityEngine.Serialization;

public sealed class UnitAttackHitbox2D : MonoBehaviour
{
    private const float MeleeSweepScale = 1.5f;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    public static bool DebugVisualizationEnabled { get; set; } = true;
#else
    public static bool DebugVisualizationEnabled { get; set; }
#endif

    [FormerlySerializedAs("attackCollider")]
    [SerializeField] private Collider2D weaponAttackCollider;
    [SerializeField] private Collider2D torsoAttackCollider;
    [SerializeField] private Transform attachRoot;
    [SerializeField] private Transform weaponVisual;
    [SerializeField] private SpriteRenderer debugHitboxSprite;
    [SerializeField] private SpriteRenderer debugSweepSprite;
    [SerializeField] private LineRenderer debugHitboxLine;
    [SerializeField] private Color activeColor = new Color(1f, 0.2f, 0.2f, 0.5f);
    [SerializeField] private Color inactiveColor = new Color(1f, 0.8f, 0.1f, 0.25f);
    [SerializeField] private Color sweepColor = new Color(0f, 1f, 1f, 0.3f);

    private UnitBase owner;
    private Vector2 previousCenter;
    private bool windowActive;
    private bool telegraphedActive;
    private uint hitGeneration;
    private Collider2D selectedAttackCollider;
    private Vector3 weaponVisualIdlePosition;
    private Quaternion weaponVisualIdleRotation;
    private Vector3 weaponVisualIdleScale;
    private bool weaponVisualPoseCached;
    private bool trackWeaponVisual;
    private BoxCollider2D effectBoundsCollider;
    private Vector2 effectBoundsOriginalOffset;
    private Vector2 effectBoundsOriginalSize;
    private bool effectBoundsActive;

    public Color ActiveColor => activeColor;
    public Color InactiveColor => inactiveColor;
    public Color CurrentDebugColor => debugHitboxSprite != null ? debugHitboxSprite.color : (windowActive ? activeColor : inactiveColor);
    public bool IsWindowActive => windowActive && selectedAttackCollider != null && selectedAttackCollider.enabled;
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
        CacheWeaponVisualIdlePose();
        RestoreWeaponVisualIdlePose();
        SetWindowActive(false);
    }

    public void SetFacingRight(bool facingRight)
    {
        if (attachRoot == null) return;
        Vector3 scale = attachRoot.localScale;
        scale.x = Mathf.Abs(scale.x) * (facingRight ? 1f : -1f);
        attachRoot.localScale = scale;
    }

    public bool TryGetForwardReach(bool facingRight, out float reach)
    {
        return TryGetForwardReach(facingRight, AttackSubject.Weapon, BodyPartRole.None, out reach);
    }

    public bool TryGetForwardReach(bool facingRight, AttackSubject subject, BodyPartRole bodyPart, out float reach)
    {
        reach = 0f;
        Collider2D collider = GetAttackCollider(subject, bodyPart);
        if (owner == null || collider == null || attachRoot == null) return false;
        bool wasEnabled = collider.enabled;
        try
        {
            if (!wasEnabled) collider.enabled = true;
            Physics2D.SyncTransforms();
            Bounds bounds = GetMeleeSweepBounds(collider.bounds, facingRight);
            reach = facingRight ? bounds.max.x - owner.transform.position.x : owner.transform.position.x - bounds.min.x;
            return float.IsFinite(reach) && reach >= 0f;
        }
        finally
        {
            if (!wasEnabled) collider.enabled = false;
        }
    }

    public bool TryGetSweepCenterOffset(bool facingRight, out float offset)
    {
        return TryGetSweepCenterOffset(facingRight, AttackSubject.Weapon, BodyPartRole.None, out offset);
    }

    public bool TryGetSweepCenterOffset(bool facingRight, AttackSubject subject, BodyPartRole bodyPart, out float offset)
    {
        offset = 0f;
        Collider2D collider = GetAttackCollider(subject, bodyPart);
        if (owner == null || collider == null || attachRoot == null) return false;
        bool wasEnabled = collider.enabled;
        try
        {
            if (!wasEnabled) collider.enabled = true;
            Physics2D.SyncTransforms();
            Bounds bounds = GetMeleeSweepBounds(collider.bounds, facingRight);
            offset = bounds.center.x - owner.transform.position.x;
            return float.IsFinite(offset);
        }
        finally
        {
            if (!wasEnabled) collider.enabled = false;
        }
    }

    public void SetTelegraphed(bool telegraphed)
    {
        SetTelegraphed(telegraphed, AttackSubject.Weapon, BodyPartRole.None, true);
    }

    public void SetTelegraphed(bool telegraphed, AttackSubject subject, BodyPartRole bodyPart,
        bool allowWeaponTracking)
    {
        telegraphedActive = telegraphed;
        trackWeaponVisual = telegraphed && allowWeaponTracking &&
            subject == AttackSubject.Weapon && bodyPart == BodyPartRole.None;
        if (!trackWeaponVisual) RestoreWeaponVisualIdlePose();
        if (telegraphed && !windowActive) DisableAttackColliders();
        UpdateDebugVisualization();
    }

    public bool TryOpen(int sourceId, uint generation, uint tick, out CombatStats.AttackSweep2D sweep)
    {
        return TryOpen(sourceId, generation, tick, AttackSubject.Weapon, BodyPartRole.None, out sweep);
    }

    public bool TryOpen(int sourceId, uint generation, uint tick, AttackSubject subject, BodyPartRole bodyPart,
        out CombatStats.AttackSweep2D sweep)
    {
        return TryOpenInternal(sourceId, generation, tick, subject, bodyPart, default, default, false, out sweep);
    }

    public bool TryOpen(int sourceId, uint generation, uint tick, AttackSubject subject, BodyPartRole bodyPart,
        Vector2 activeCenter, Vector2 activeSize, out CombatStats.AttackSweep2D sweep)
    {
        return TryOpenInternal(sourceId, generation, tick, subject, bodyPart,
            activeCenter, activeSize, true, out sweep);
    }

    private bool TryOpenInternal(int sourceId, uint generation, uint tick, AttackSubject subject,
        BodyPartRole bodyPart, Vector2 activeCenter, Vector2 activeSize, bool useEffectBounds,
        out CombatStats.AttackSweep2D sweep)
    {
        sweep = default;
        Collider2D collider = GetAttackCollider(subject, bodyPart);
        if (owner == null || collider == null || attachRoot == null || !collider.isTrigger ||
            collider == owner.Stats?.DefenseBodyCollider)
        {
            Debug.LogError($"[UnitAttackHitbox2D] Unit idx {(owner != null ? owner.UnitIdx : 0u)} has no valid serialized attack collider/attach root.");
            SetWindowActive(false);
            return false;
        }

        if (useEffectBounds && (collider is not BoxCollider2D ||
            !float.IsFinite(activeCenter.x) || !float.IsFinite(activeCenter.y) ||
            !float.IsFinite(activeSize.x) || !float.IsFinite(activeSize.y) ||
            activeSize.x <= 0f || activeSize.y <= 0f))
        {
            Debug.LogError($"[UnitAttackHitbox2D] Unit idx {owner.UnitIdx} has invalid EffectData active bounds/collider; tick cancelled.");
            SetWindowActive(false);
            return false;
        }

        if (!windowActive) hitGeneration++;
        RestoreEffectBounds();
        DisableAttackColliders();
        selectedAttackCollider = collider;
        if (useEffectBounds)
        {
            BoxCollider2D box = (BoxCollider2D)collider;
            effectBoundsCollider = box;
            effectBoundsOriginalOffset = box.offset;
            effectBoundsOriginalSize = box.size;
            effectBoundsActive = true;
            box.offset = activeCenter;
            box.size = activeSize;
        }
        trackWeaponVisual = subject == AttackSubject.Weapon && bodyPart == BodyPartRole.None;
        if (!trackWeaponVisual) RestoreWeaponVisualIdlePose();
        selectedAttackCollider.enabled = true;
        windowActive = true;
        Physics2D.SyncTransforms();
        bool facingRight = attachRoot.lossyScale.x >= 0f;
        Bounds bounds = effectBoundsActive
            ? selectedAttackCollider.bounds
            : GetMeleeSweepBounds(selectedAttackCollider.bounds, facingRight);
        Vector2 current = bounds.center;
        if (previousCenter == default) previousCenter = owner.transform.position;
        sweep = new CombatStats.AttackSweep2D(previousCenter, current, bounds.extents, sourceId, hitGeneration, tick);
        previousCenter = current;
        UpdateDebugVisualization();
        return true;
    }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    private void FixedUpdate()
    {
        UpdateSweepDebugVisualization();
    }
#endif

    private static Bounds GetMeleeSweepBounds(Bounds source, bool facingRight)
    {
        float addedWidth = source.size.x * (MeleeSweepScale - 1f);
        source.center += Vector3.right * (facingRight ? addedWidth * .5f : -addedWidth * .5f);
        source.extents = new Vector3(source.extents.x * MeleeSweepScale,
            source.extents.y * MeleeSweepScale, source.extents.z);
        return source;
    }

    public void Close()
    {
        windowActive = false;
        trackWeaponVisual = false;
        RestoreWeaponVisualIdlePose();
        RestoreEffectBounds();
        DisableAttackColliders();
        UpdateDebugVisualization();
        previousCenter = default;
    }

    private void OnDisable()
    {
        windowActive = false;
        telegraphedActive = false;
        trackWeaponVisual = false;
        RestoreWeaponVisualIdlePose();
        RestoreEffectBounds();
        DisableAttackColliders();
        UpdateDebugVisualization();
    }

    private void LateUpdate()
    {
        SyncWeaponVisualToCollider();
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (!IsDebugVisualizationActive || selectedAttackCollider == null ||
            debugHitboxSprite != null || debugHitboxLine != null) return;
        Bounds bounds = selectedAttackCollider.bounds;
        Vector3 bottomLeft = new Vector3(bounds.min.x, bounds.min.y, transform.position.z);
        Vector3 topLeft = new Vector3(bounds.min.x, bounds.max.y, transform.position.z);
        Vector3 topRight = new Vector3(bounds.max.x, bounds.max.y, transform.position.z);
        Vector3 bottomRight = new Vector3(bounds.max.x, bounds.min.y, transform.position.z);
        Color drawColor = windowActive ? activeColor : inactiveColor;
        Debug.DrawLine(bottomLeft, topLeft, drawColor);
        Debug.DrawLine(topLeft, topRight, drawColor);
        Debug.DrawLine(topRight, bottomRight, drawColor);
        Debug.DrawLine(bottomRight, bottomLeft, drawColor);
#endif
    }

    private void SetWindowActive(bool active)
    {
        windowActive = active;
        if (!active) telegraphedActive = false;
        if (!active)
        {
            trackWeaponVisual = false;
            RestoreWeaponVisualIdlePose();
            RestoreEffectBounds();
            DisableAttackColliders();
        }
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
        UpdateSweepDebugVisualization();
#else
        if (debugHitboxSprite != null) debugHitboxSprite.enabled = false;
        if (debugSweepSprite != null) debugSweepSprite.enabled = false;
        if (debugHitboxLine != null) debugHitboxLine.enabled = false;
#endif
    }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    private void UpdateSweepDebugVisualization()
    {
        if (debugSweepSprite == null) return;

        bool visible = DebugVisualizationEnabled && IsWindowActive && debugSweepSprite.sprite != null;
        debugSweepSprite.enabled = visible;
        if (!visible) return;

        Physics2D.SyncTransforms();
        Bounds bounds = effectBoundsActive
            ? selectedAttackCollider.bounds
            : GetMeleeSweepBounds(selectedAttackCollider.bounds, attachRoot.lossyScale.x >= 0f);
        Transform visual = debugSweepSprite.transform;
        Vector2 spriteSize = debugSweepSprite.sprite.bounds.size;
        Vector3 parentScale = visual.parent != null ? visual.parent.lossyScale : Vector3.one;
        visual.position = new Vector3(bounds.center.x, bounds.center.y, visual.position.z);
        visual.localScale = new Vector3(
            bounds.size.x / (spriteSize.x * Mathf.Max(Mathf.Abs(parentScale.x), Mathf.Epsilon)),
            bounds.size.y / (spriteSize.y * Mathf.Max(Mathf.Abs(parentScale.y), Mathf.Epsilon)),
            visual.localScale.z);
        debugSweepSprite.color = sweepColor;
    }
#endif

    private Collider2D GetAttackCollider(AttackSubject subject, BodyPartRole bodyPart)
    {
        if (subject == AttackSubject.Weapon && bodyPart == BodyPartRole.None) return weaponAttackCollider;
        if (subject == AttackSubject.BodyPart && bodyPart == BodyPartRole.Torso) return torsoAttackCollider;
        return null;
    }

    private void DisableAttackColliders()
    {
        if (weaponAttackCollider != null) weaponAttackCollider.enabled = false;
        if (torsoAttackCollider != null) torsoAttackCollider.enabled = false;
        selectedAttackCollider = null;
    }

    private void RestoreEffectBounds()
    {
        if (!effectBoundsActive || effectBoundsCollider == null) return;
        effectBoundsCollider.offset = effectBoundsOriginalOffset;
        effectBoundsCollider.size = effectBoundsOriginalSize;
        effectBoundsCollider = null;
        effectBoundsActive = false;
    }

    private void CacheWeaponVisualIdlePose()
    {
        if (weaponVisual == null || weaponVisualPoseCached) return;
        weaponVisualIdlePosition = weaponVisual.localPosition;
        weaponVisualIdleRotation = weaponVisual.localRotation;
        weaponVisualIdleScale = weaponVisual.localScale;
        weaponVisualPoseCached = true;
    }

    private void SyncWeaponVisualToCollider()
    {
        if (!trackWeaponVisual || weaponVisual == null || weaponAttackCollider == null) return;
        Transform colliderTransform = weaponAttackCollider.transform;
        Vector3 worldPosition = colliderTransform.TransformPoint(weaponAttackCollider.offset);
        Quaternion worldRotation = colliderTransform.rotation;
        Transform parent = weaponVisual.parent;
        weaponVisual.localPosition = parent != null ? parent.InverseTransformPoint(worldPosition) : worldPosition;
        weaponVisual.localRotation = parent != null
            ? Quaternion.Inverse(parent.rotation) * worldRotation
            : worldRotation;
        weaponVisual.localScale = weaponVisualIdleScale;
    }

    private void RestoreWeaponVisualIdlePose()
    {
        if (!weaponVisualPoseCached || weaponVisual == null) return;
        weaponVisual.localPosition = weaponVisualIdlePosition;
        weaponVisual.localRotation = weaponVisualIdleRotation;
        weaponVisual.localScale = weaponVisualIdleScale;
    }
}
