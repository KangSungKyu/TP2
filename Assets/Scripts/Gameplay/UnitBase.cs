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


    // =========================================================================
    // 2. PROTECTED & PRIVATE FIELDS (camelCase, No '_' prefix)
    // =========================================================================

    protected CombatStats stats;
    protected SkillExecutor skillExecutor;
    protected Animator animator;
    protected SpriteRenderer spriteRenderer;
    protected Transform visualTransform;
    protected CapsuleCollider2D hitCollider;
    protected Rigidbody2D rb2d;


    // =========================================================================
    // 3. PUBLIC METHODS (PascalCase)
    // =========================================================================

    /// <summary>
    /// unitIdx (Type 3: 3001~)로 공용 데이터를 조회하고 유닛을 초기화합니다.
    /// async UniTask를 반환하여 상속 체인에서 await가 보장됩니다.
    /// </summary>
    public virtual async UniTask InitUnitAsync(uint unitIdx)
    {
        if (DataTableManager.Instance != null)
        {
            await DataTableManager.Instance.EnsureDataLoadedAsync();
        }

        var unitDB = DataTableManager.Instance != null ? DataTableManager.Instance.GetDB<UnitBaseDataTable>(DataTableType.UnitBase) : null;
        if (unitDB != null && unitDB.TryGetUnitData(unitIdx, out var data))
        {
            this.UnitData = data;
            this.applyBaseStats(data);
            this.setupHitbox(data);
            this.loadResourceAndAnimator(data);
            Debug.Log($"<color=green>[UnitBase] '{this.gameObject.name}' Idx {unitIdx} 데이터 바인딩 완료! (HP: {data.MaxHp}, ATK: {data.Atk})</color>");
        }
        else
        {
            Debug.LogWarning($"[UnitBase] UnitBaseData에서 Idx {unitIdx}를 찾을 수 없습니다. (DataTableManager.isLoaded: {DataTableManager.Instance != null})");
        }
    }

    public virtual void SetFacingRight(bool isRight)
    {
        // 정책: 원본 스프라이트(Texture)는 왼쪽을 바라보는 형태가 기본값.
        // 따라서 유닛이 오른쪽(isRight = true)을 바라보게 하려면 flipX를 true로 반전시켜야 함.
        if (this.spriteRenderer != null)
        {
            this.spriteRenderer.flipX = isRight;
        }
        
        // flipX로 방향을 처리하므로, visualTransform의 Scale은 1f로 유지 (이중 반전 방지)
        if (this.visualTransform != null)
        {
            float scaleY = this.visualTransform.localScale.y;
            float scaleZ = this.visualTransform.localScale.z;
            this.visualTransform.localScale = new Vector3(1f, scaleY, scaleZ);
        }
    }


    // =========================================================================
    // 4. PROTECTED & PRIVATE METHODS (camelCase)
    // =========================================================================

    protected virtual void Awake()
    {
        this.stats = GetComponent<CombatStats>();
        this.skillExecutor = GetComponent<SkillExecutor>();

        // [Root & Visual 계층 아키텍처 세팅]
        this.visualTransform = transform.Find("Visual");
        if (this.visualTransform == null)
        {
            GameObject visualObj = new GameObject("Visual");
            this.visualTransform = visualObj.transform;
            this.visualTransform.SetParent(transform, false);
            this.visualTransform.localPosition = Vector3.zero;
        }

        this.spriteRenderer = this.visualTransform.GetComponent<SpriteRenderer>();
        if (this.spriteRenderer == null)
        {
            this.spriteRenderer = this.visualTransform.gameObject.AddComponent<SpriteRenderer>();
            this.spriteRenderer.sortingOrder = 10;
        }

        this.animator = this.visualTransform.GetComponent<Animator>();
        if (this.animator == null)
        {
            this.animator = this.visualTransform.gameObject.AddComponent<Animator>();
        }
    }

    private void applyBaseStats(UnitBaseData data)
    {
        if (this.stats != null)
        {
            this.stats.MaxHp = data.MaxHp;
            this.stats.MaxMp = data.MaxMp;
            this.stats.MaxPosture = data.MaxPosture;
            this.stats.InitStats();
        }

        // 지면 피벗 오프셋: Sprite Sheet 피벗이 (0.5, 0.0)으로 가공되었으므로 항상 (0,0,0) 고정
        if (this.visualTransform != null)
        {
            this.visualTransform.localPosition = Vector3.zero;
        }

        // TextData 조회
        var textDB = DataTableManager.Instance != null ? DataTableManager.Instance.GetDB<TextDataTable>(DataTableType.Text) : null;
        if (textDB != null)
        {
            this.UnitName = textDB.GetText(data.NameTextIdx);
        }
    }

    private void loadResourceAndAnimator(UnitBaseData data)
    {
        var resourceDB = DataTableManager.Instance != null ? DataTableManager.Instance.GetDB<ResourceDataTable>(DataTableType.Resource) : null;
        if (resourceDB == null) return;

        string animatorKey = resourceDB.GetResourcePath(data.AnimatorId);
        if (!string.IsNullOrEmpty(animatorKey) && ResourceManager.Instance != null)
        {
            ResourceManager.Instance.LoadAssetAsync<RuntimeAnimatorController>(animatorKey, controller =>
            {
                if (controller != null && this.animator != null)
                {
                    this.animator.runtimeAnimatorController = controller;
                    // Controller 연결 직후 기본 Idle 애니메이션 즉시 재생을 강제하여 스프라이트 미출력 방지
                    this.animator.Play("Player_Idle", 0, 0f);
                    Debug.Log($"<color=green>[UnitBase] '{this.gameObject.name}' 하위 Visual 객체에 '{animatorKey}' 바인딩 및 애니메이션 재생 개시 완료!</color>");
                }
            });
        }
    }

    private void setupHitbox(UnitBaseData data)
    {
        // 1. Trigger 이벤트를 안정적으로 감지하기 위한 Kinematic Rigidbody2D 부착
        this.rb2d = GetComponent<Rigidbody2D>();
        if (this.rb2d == null)
        {
            this.rb2d = this.gameObject.AddComponent<Rigidbody2D>();
        }
        this.rb2d.bodyType = RigidbodyType2D.Kinematic;
        this.rb2d.simulated = true;

        // 2. 피격(Hitbox) 전용 CapsuleCollider2D 설정 (isTrigger = true 로 설정하여 물리 겹침 허용)
        this.hitCollider = GetComponent<CapsuleCollider2D>();
        if (this.hitCollider == null)
        {
            this.hitCollider = this.gameObject.AddComponent<CapsuleCollider2D>();
        }

        float radius = data.HitboxRadius > 0f ? data.HitboxRadius : 0.5f;
        float height = radius * 4f;

        this.hitCollider.isTrigger = true;
        this.hitCollider.size = new Vector2(radius * 2f, height);
        this.hitCollider.offset = new Vector2(0f, height * 0.5f); // 발바닥 피벗 기준 위쪽 오프셋
        this.hitCollider.direction = CapsuleDirection2D.Vertical;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        // 3. Game View에서도 항시 뚜렷하게 보이도록 LineRenderer 기반 Hitbox Visualizer 생성
        this.setupGameViewHitboxLine(data, radius * 2f, height, new Vector2(0f, height * 0.5f));
#endif

        Debug.Log($"<color=cyan>[Hitbox] '{this.gameObject.name}' Trigger Hitbox 생성 완료 (Radius: {radius}, Size: {this.hitCollider.size})</color>");
    }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    private void setupGameViewHitboxLine(UnitBaseData data, float width, float height, Vector2 offset)
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
    /// <summary>
    /// 테스트 및 디버그 환경에서 피격 Hitbox(CapsuleCollider2D)의 영역을 시각화합니다.
    /// </summary>
    protected virtual void OnDrawGizmos()
    {
        var col = this.hitCollider != null ? this.hitCollider : GetComponent<CapsuleCollider2D>();
        if (col == null) return;

        // 진영(Faction) 및 유닛 타입에 따른 기즈모 색상 구분
        // Player (1): 초록색, Enemy/Boss (2,3): 빨간색, 기타: 하늘색
        Color fillColor = new Color(0f, 1f, 1f, 0.25f);
        Color wireColor = new Color(0f, 1f, 1f, 0.9f);

        if (this.UnitData != null)
        {
            if (this.UnitData.Faction == 1) // Player
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
