using Cysharp.Threading.Tasks;
using UnityEngine;

/// <summary>
/// 최상위 기반 유닛 클래스.
/// Root 객체(Y=0 지면 피벗 중심점)와 Visual 자식 객체(Pure 2D SpriteRenderer + Animator)를 총괄 관리합니다.
/// 공용 마스터 데이터(UnitBaseData)를 소유합니다.
/// </summary>
[RequireComponent(typeof(CombatStats))]
public class UnitBase : MonoBehaviour
{
    // =========================================================================
    // 1. PUBLIC FIELDS & PROPERTIES (PascalCase)
    // =========================================================================

    public UnitBaseData UnitData { get; protected set; }
    public string UnitName { get; protected set; } = string.Empty;
    public uint UnitIdx { get; protected set; }
    public uint UnitId => UnitIdx;
    public CombatStats Stats => stats;
    public Animator Animator => animator;
    public FactionType Faction => UnitData != null ? (FactionType)UnitData.Faction :
        (this is Player ? FactionType.PlayerAlly : this is Monster ? FactionType.Enemy : FactionType.None);
    public virtual uint ActionGeneration => sharedActionGeneration;
    public bool IsFacingRight { get; private set; } = true;


    // =========================================================================
    // 2. PROTECTED & PRIVATE FIELDS (camelCase)
    // =========================================================================

    protected CombatStats stats;
    protected SkillExecutor skillExecutor;
    protected Animator animator;
    protected SpriteRenderer spriteRenderer;
    protected Transform visualTransform;
    protected CapsuleCollider2D hitCollider;
    protected Rigidbody2D rb2d;
    protected KinematicMotor2D motor;
    private uint sharedActionGeneration;

    protected bool isGrounded => motor != null ? motor.IsGrounded : false;


    // =========================================================================
    // 3. PUBLIC METHODS (PascalCase)
    // =========================================================================

    /// <summary>
    /// unitIdx (Type 3: 3001~)로 공용 데이터를 조회하고 유닛을 초기화합니다.
    /// async UniTask를 반환하여 상속 체인에서 await가 보장됩니다.
    /// </summary>
    public virtual async UniTask InitUnitAsync(uint unitIdx)
    {
        this.UnitIdx = unitIdx;
        await UniTask.Yield();
        if (DataTableManager.Instance != null)
        {
            await DataTableManager.Instance.EnsureDataLoadedAsync();
        }

        var unitDB = DataTableManager.Instance != null ? DataTableManager.Instance.GetDB<UnitBaseDataTable>(DataTableType.UnitBase) : null;
        if (unitDB != null && unitDB.TryGetUnitData(unitIdx, out var data))
        {
            UnitData = data;
            ApplyBaseStats(data);
            SetupHitbox(data);
            LoadResourceAndAnimator(data);
            Debug.Log($"<color=green>[UnitBase] '{gameObject.name}' Idx {unitIdx} 데이터 바인딩 완료! (HP: {data.MaxHp}, ATK: {data.Atk})</color>");
        }
        else
        {
            Debug.LogWarning($"[UnitBase] UnitBaseData에서 Idx {unitIdx}를 찾을 수 없습니다. (DataTableManager.isLoaded: {DataTableManager.Instance != null})");
        }
    }

    public virtual void SetFacingRight(bool isRight)
    {
        IsFacingRight = isRight;
        if (stats != null) stats.SetFacingRight(isRight);
        if (spriteRenderer != null)
        {
            spriteRenderer.flipX = isRight;
        }
        
        if (visualTransform != null)
        {
            float scaleY = visualTransform.localScale.y;
            float scaleZ = visualTransform.localScale.z;
            visualTransform.localScale = new Vector3(1f, scaleY, scaleZ);
        }
    }

    public void SetGuarding(bool state) => stats?.SetGuarding(state);
    public void SetParrying(bool state) => stats?.SetParrying(state);
    public void SetDodging(bool state) => stats?.SetDodging(state);
    public void SetAttackMotionVelocityX(float velocityX) => motor?.SetTargetVelocityX(velocityX);
    public void SetAttackMotionStopPosition(float worldX) => motor?.SetHorizontalStopPosition(worldX);
    public void StopAttackMotionImmediately() => motor?.StopHorizontalImmediately();
    public bool HasGroundSupportForAttackStep(float deltaX) =>
        motor != null && motor.HasGroundSupportForHorizontalStep(deltaX);
    public float AttackMotionVelocityX => motor != null ? motor.Velocity.x : 0f;
    public float AttackMotionSkinWidth => motor != null ? motor.SkinWidth : Physics2D.defaultContactOffset;
    public virtual bool IsActionGenerationCurrent(uint generation) => generation == sharedActionGeneration && isActiveAndEnabled;

    /// <summary>
    /// 대상 유닛(target)이 Groggy 상태일 때 공용 처형(Execution) 공격을 가합니다.
    /// </summary>
    public virtual bool TryExecuteTarget(UnitBase target, float executionMultiplier = 5.0f)
    {
        if (target == null || target.stats == null || stats == null) return false;

        if (!target.stats.IsGroggy)
        {
            Debug.Log($"[TryExecuteTarget] 대상 '{target.gameObject.name}' 은 Groggy 상태가 아닙니다.");
            return false;
        }

        float executionDamage = stats.Atk * executionMultiplier;
        target.stats.TakeExecutionDamage(executionDamage, attacker: stats);

        Debug.Log($"<color=yellow>[Execution Success] '{gameObject.name}' 이(가) '{target.gameObject.name}' 에 처형 공격 성공! (Damage: {executionDamage})</color>");
        return true;
    }


    // =========================================================================
    // 4. PROTECTED & PRIVATE METHODS
    // =========================================================================

    protected virtual void Awake()
    {
        stats = GetComponent<CombatStats>();
        skillExecutor = GetComponent<SkillExecutor>();
        motor = GetComponent<KinematicMotor2D>();
        if (motor == null)
        {
            motor = gameObject.AddComponent<KinematicMotor2D>();
        }

        visualTransform = transform.Find("Visual");
        if (visualTransform == null)
        {
            GameObject visualObj = new GameObject("Visual");
            visualTransform = visualObj.transform;
            visualTransform.SetParent(transform, false);
            visualTransform.localPosition = Vector3.zero;
        }

        spriteRenderer = visualTransform.GetComponent<SpriteRenderer>();
        if (spriteRenderer == null)
        {
            spriteRenderer = visualTransform.gameObject.AddComponent<SpriteRenderer>();
            spriteRenderer.sortingOrder = 10;
        }

        animator = visualTransform.GetComponent<Animator>();
        if (animator == null)
        {
            animator = visualTransform.gameObject.AddComponent<Animator>();
        }
    }

    protected virtual void updateGroundCheck()
    {
        // KinematicMotor2D가 FixedUpdate에서 자체 구동하므로 외부 호출 불필요
    }

    private void ApplyBaseStats(UnitBaseData data)
    {
        if (stats != null)
        {
            stats.MaxHp = data.MaxHp;
            stats.MaxMp = data.MaxMp;
            stats.MaxPosture = data.MaxPosture;
            stats.InitStats();
        }

        if (visualTransform != null)
        {
            visualTransform.localPosition = Vector3.zero;
        }

        var textDB = DataTableManager.Instance != null ? DataTableManager.Instance.GetDB<TextDataTable>(DataTableType.Text) : null;
        if (textDB != null)
        {
            UnitName = textDB.GetText(data.NameTextIdx);
        }
    }

    private void LoadResourceAndAnimator(UnitBaseData data)
    {
        var resourceDB = DataTableManager.Instance != null ? DataTableManager.Instance.GetDB<ResourceDataTable>(DataTableType.Resource) : null;
        if (resourceDB == null) return;

        string animatorKey = resourceDB.GetResourcePath(data.AnimatorId);
        if (!string.IsNullOrEmpty(animatorKey) && ResourceManager.Instance != null)
        {
            ResourceManager.Instance.LoadAssetAsync<RuntimeAnimatorController>(animatorKey, controller =>
            {
                if (controller != null && animator != null)
                {
                    animator.runtimeAnimatorController = controller;
                    if (animator.HasState(0, Animator.StringToHash("Player_Idle")))
                    {
                        animator.Play("Player_Idle", 0, 0f);
                    }
                    else
                    {
                        animator.Play(0, 0, 0f);
                    }
                    Debug.Log($"<color=green>[UnitBase] '{gameObject.name}' 하위 Visual 객체에 '{animatorKey}' 바인딩 및 애니메이션 재생 개시 완료!</color>");
                }
            });
        }
    }

    private void SetupHitbox(UnitBaseData data)
    {
        rb2d = GetComponent<Rigidbody2D>();
        if (rb2d == null)
        {
            rb2d = gameObject.AddComponent<Rigidbody2D>();
        }
        rb2d.bodyType = RigidbodyType2D.Kinematic;
        rb2d.simulated = true;

        hitCollider = GetComponent<CapsuleCollider2D>();
        if (hitCollider == null)
        {
            hitCollider = gameObject.AddComponent<CapsuleCollider2D>();
        }

        float radius = data.HitboxRadius > 0f ? data.HitboxRadius : 0.5f;
        float height = radius * 4f;

        hitCollider.isTrigger = true;
        hitCollider.size = new Vector2(radius * 2f, height);
        hitCollider.offset = new Vector2(0f, height * 0.5f);
        hitCollider.direction = CapsuleDirection2D.Vertical;
        stats?.SetDefenseBodyCollider(hitCollider);

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        SetupGameViewHitboxLine(data, radius * 2f, height, new Vector2(0f, height * 0.5f));
#endif

        Debug.Log($"<color=cyan>[Hitbox] '{gameObject.name}' Trigger Hitbox 생성 완료 (Radius: {radius}, Size: {hitCollider.size})</color>");
    }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    private void SetupGameViewHitboxLine(UnitBaseData data, float width, float height, Vector2 offset)
    {
        Transform debugObj = transform.Find("DebugHitboxLine");
        LineRenderer line;
        if (debugObj == null)
        {
            GameObject go = new GameObject("DebugHitboxLine");
            go.transform.SetParent(transform, false);
            line = go.AddComponent<LineRenderer>();
        }
        else
        {
            line = debugObj.GetComponent<LineRenderer>();
        }

        line.useWorldSpace = false;
        line.loop = true;
        line.positionCount = 4;
        line.startWidth = 0.05f;
        line.endWidth = 0.05f;
        line.material = new Material(Shader.Find("Sprites/Default"));

        Color col = (data != null && data.Faction == 1) ? Color.green : Color.red;
        line.startColor = col;
        line.endColor = col;

        float hw = width * 0.5f;
        float hh = height * 0.5f;

        Vector3 p1 = new Vector3(-hw, offset.y - hh, -0.1f);
        Vector3 p2 = new Vector3(-hw, offset.y + hh, -0.1f);
        Vector3 p3 = new Vector3(hw, offset.y + hh, -0.1f);
        Vector3 p4 = new Vector3(hw, offset.y - hh, -0.1f);

        line.SetPosition(0, p1);
        line.SetPosition(1, p2);
        line.SetPosition(2, p3);
        line.SetPosition(3, p4);
    }
#endif

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    protected virtual void OnDrawGizmos()
    {
        var col = hitCollider != null ? hitCollider : GetComponent<CapsuleCollider2D>();
        if (col == null) return;

        Color fillColor = new Color(0f, 1f, 1f, 0.25f);
        Color wireColor = new Color(0f, 1f, 1f, 0.9f);

        if (UnitData != null)
        {
            if (UnitData.Faction == 1)
            {
                fillColor = new Color(0f, 1f, 0f, 0.25f);
                wireColor = new Color(0f, 1f, 0f, 0.9f);
            }
            else if (this.UnitData.Faction == 2 || this.UnitData.UnitType == 3) // Enemy / Boss
            {
                fillColor = new Color(1f, 0f, 0f, 0.25f);
                wireColor = new Color(1f, 0f, 0f, 0.9f);
            }
        }

        Vector3 center = transform.position + (Vector3)col.offset;
        Vector3 size = new Vector3(col.size.x, col.size.y, 0.1f);

        // 1. Hitbox 채우기 영역 (반투명)
        Gizmos.color = fillColor;
        Gizmos.DrawCube(center, size);

        // 2. Hitbox 외곽 테두리 (선명함)
        Gizmos.color = wireColor;
        Gizmos.DrawWireCube(center, size);
    }
#endif
}
