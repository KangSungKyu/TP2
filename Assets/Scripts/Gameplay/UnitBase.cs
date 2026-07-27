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
            this.visualTransform.localPosition = new Vector3(0f, 0.6f, 0f);
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

        // 지면 피벗 오프셋 지정
        if (this.visualTransform != null && data.VisualOffsetY > 0f)
        {
            this.visualTransform.localPosition = new Vector3(0f, data.VisualOffsetY, 0f);
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
                    Debug.Log($"<color=green>[UnitBase] '{this.gameObject.name}' 하위 Visual 객체에 '{animatorKey}' 바인딩 완료!</color>");
                }
            });
        }
    }
}
