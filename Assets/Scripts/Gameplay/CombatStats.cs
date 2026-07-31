using UnityEngine;
using UnityEngine.Events;
using Cysharp.Threading.Tasks;

/// <summary>
/// HP, MP, Posture, SuperArmor 등 전투 관련 스탯을 총괄 관리합니다.
/// </summary>
public class CombatStats : MonoBehaviour
{
    // =========================================================================
    // 1. PUBLIC FIELDS & PROPERTIES (PascalCase)
    // =========================================================================

    [Header("Base Stats")]
    public float MaxHp = 100f;
    public float MaxMp = 50f;
    public float MaxPosture = 100f;
    public float Atk = 10f;

    [Header("SuperArmor Setting")]
    public bool IsSuperArmorActive = false;

    [Header("Current Values (ReadOnly)")]
    public float CurrentHp { get; private set; }
    public float CurrentMp { get; private set; }
    public float CurrentPosture { get; private set; }

    public bool IsGuarding { get; private set; }
    public bool IsDodging { get; private set; }
    public bool IsParrying { get; private set; }
    public bool IsJumped { get; private set; }
    public bool IsGroggy { get; private set; }

    public UnityEvent<float> OnHpChanged;
    public UnityEvent<float> OnMpChanged;
    public UnityEvent<float> OnPostureChanged;
    public UnityEvent OnParrySuccess;
    public UnityEvent OnGroggyState;
    public UnityEvent OnGroggyEnded;


    // =========================================================================
    // 2. PRIVATE FIELDS (camelCase)
    // =========================================================================

    private float groggyTimer = 0f;
    private const float DefaultGroggyDuration = 3.0f;
    private Rigidbody2D rb2d;


    // =========================================================================
    // 3. PUBLIC METHODS (PascalCase)
    // =========================================================================

    public bool TakeDamage(float amount, bool isGroundAttack = false, bool isJumped = false, CombatStats attacker = null)
    {
        if (IsGroggy)
        {
            amount *= 1.5f;
        }

        if (IsDodging)
        {
            Debug.Log($"[{gameObject.name}] 공격 회피(Dodge) 성공!");
            SpawnResponseEffect(8012);
            return true;
        }

        if (isGroundAttack && isJumped)
        {
            Debug.Log($"[{gameObject.name}] 지면 공격 점프(Jump) 회피 성공!");
            SpawnResponseEffect(8012);
            return true;
        }

        if (IsParrying)
        {
            Debug.Log($"[{gameObject.name}] 패링(Parry) 성공!");
            OnParrySuccess?.Invoke();
            SpawnResponseEffect(8010);

            if (attacker != null)
            {
                attacker.AddPosture(40f);
            }
            return true;
        }

        if (IsGuarding)
        {
            float guardCost = amount * 0.5f;
            AddPosture(guardCost);
            SpawnResponseEffect(8011);

            if (attacker != null && rb2d != null)
            {
                Vector2 pushDir = (transform.position - attacker.transform.position).normalized;
                float knockbackForce = (amount / 10f) * 3.0f;
                rb2d.AddForce(pushDir * knockbackForce, ForceMode2D.Impulse);
            }

            Debug.Log($"[{gameObject.name}] 가드(Guard) 성공!");
            return true;
        }

        if (amount <= 0f) return false;

        SpawnResponseEffect(8013);
        CurrentHp = Mathf.Max(CurrentHp - amount, 0f);
        OnHpChanged?.Invoke(CurrentHp / MaxHp);

        if (CurrentHp <= 0f)
        {
            Debug.Log($"[{gameObject.name}] 사망!");
        }

        return false;
    }

    public void TakeExecutionDamage(float damage, CombatStats attacker = null)
    {
        IsGroggy = false;
        groggyTimer = 0f;
        CurrentPosture = 0f;
        OnPostureChanged?.Invoke(0f);
        OnGroggyEnded?.Invoke();

        CurrentHp = Mathf.Max(CurrentHp - damage, 0f);
        OnHpChanged?.Invoke(CurrentHp / MaxHp);

        Debug.Log($"<color=red>[Execution Impact] {gameObject.name} (이)가 {damage} 의 처형 피해를 입었습니다!</color>");
    }

    public void AddPosture(float amount)
    {
        if (IsGroggy) return;

        CurrentPosture = Mathf.Clamp(CurrentPosture + amount, 0f, MaxPosture);
        OnPostureChanged?.Invoke(CurrentPosture / MaxPosture);

        if (CurrentPosture >= MaxPosture)
        {
            TriggerGroggy(DefaultGroggyDuration);
        }
    }

    public void TriggerGroggy(float duration = DefaultGroggyDuration)
    {
        IsGroggy = true;
        groggyTimer = duration;
        Debug.Log($"[{gameObject.name}] 자세 게이지 임계치 달성! 무방비 그로기(Groggy) 진입 ({duration}초)");
        OnGroggyState?.Invoke();
    }

    public void Heal(float amount)
    {
        CurrentHp = Mathf.Min(CurrentHp + amount, MaxHp);
        OnHpChanged?.Invoke(CurrentHp / MaxHp);
    }

    public bool ConsumeMp(float cost)
    {
        if (CurrentMp < cost) return false;
        CurrentMp -= cost;
        OnMpChanged?.Invoke(CurrentMp / MaxMp);
        return true;
    }

    public void RestoreMp(float amount)
    {
        CurrentMp = Mathf.Min(CurrentMp + amount, MaxMp);
        OnMpChanged?.Invoke(CurrentMp / MaxMp);
    }

    public void SetGuarding(bool value) => IsGuarding = value;
    public void SetDodging(bool value) => IsDodging = value;
    public void SetParrying(bool value) => IsParrying = value;
    public void SetJumped(bool value) => IsJumped = value;

    public void InitStats()
    {
        CurrentHp = MaxHp;
        CurrentMp = MaxMp;
        CurrentPosture = 0f;
        IsGroggy = false;
    }


    // =========================================================================
    // 4. PRIVATE METHODS
    // =========================================================================

    private void Awake()
    {
        rb2d = GetComponent<Rigidbody2D>();
        InitStats();
    }

    private void Update()
    {
        if (IsGroggy)
        {
            groggyTimer -= Time.deltaTime;
            if (groggyTimer <= 0f)
            {
                IsGroggy = false;
                CurrentPosture = 0f;
                OnPostureChanged?.Invoke(0f);
                OnGroggyEnded?.Invoke();
            }
        }
    }

    private void SpawnResponseEffect(uint effectIdx)
    {
        var executor = GetComponent<SkillExecutor>();
        if (executor != null)
        {
            Vector3 pos = transform.position + Vector3.up * 1.0f;
            executor.SpawnEffectByEffectIdxAsync(effectIdx, pos).Forget();
        }
    }
}

