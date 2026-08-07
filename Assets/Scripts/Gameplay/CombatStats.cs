using Cysharp.Threading.Tasks;
using System.Threading;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// HP, MP, Posture, SuperArmor 등 전투 관련 스탯을 총괄 관리합니다.
/// 몬스터 및 플레이어 피격 연출 (적색 플래시, 넉백, 이펙트 생성)을 담당합니다.
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
    public bool IsDead { get; private set; }

    public UnityEvent<float> OnHpChanged;
    public UnityEvent<float> OnMpChanged;
    public UnityEvent<float> OnPostureChanged;
    public UnityEvent OnParrySuccess;
    public UnityEvent OnGroggyState;
    public UnityEvent OnGroggyEnded;
    public UnityEvent OnDeath;
    public UnityEvent OnHpZero;


    // =========================================================================
    // 2. PRIVATE FIELDS (camelCase)
    // =========================================================================

    private float groggyTimer = 0f;
    private const float DefaultGroggyDuration = 3.0f;
    private const float HitReactionDuration = 0.15f;
    private KinematicMotor2D motor;


    // =========================================================================
    // 3. PUBLIC METHODS (PascalCase)
    // =========================================================================

    private void Awake()
    {
        motor = GetComponent<KinematicMotor2D>();
        InitStats();
    }

    public void InitStats()
    {
        CurrentHp = MaxHp;
        CurrentMp = MaxMp;
        CurrentPosture = 0f;
        IsGroggy = false;
        groggyTimer = 0f;
    }

    public void SetGuarding(bool state) => IsGuarding = state;
    public void SetDodging(bool state) => IsDodging = state;
    public void SetParrying(bool state) => IsParrying = state;
    public void SetJumped(bool state) => IsJumped = state;

    public bool ConsumeMp(float amount)
    {
        if (CurrentMp < amount) return false;
        CurrentMp = Mathf.Max(CurrentMp - amount, 0f);
        OnMpChanged?.Invoke(CurrentMp / MaxMp);
        return true;
    }

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

            if (attacker != null)
            {
                float knockbackForce = (amount / 10f) * 3.0f;
                ApplyKnockback(attacker, knockbackForce);
            }

            Debug.Log($"[{gameObject.name}] 가드(Guard) 성공!");
            return true;
        }

        if (amount <= 0f) return false;

        SpawnResponseEffect(8013);
        CurrentHp = Mathf.Max(CurrentHp - amount, 0f);
        OnHpChanged?.Invoke(CurrentHp / MaxHp);

        // 몬스터 / 유닛 피격 반응 연출 (스프라이트 적색 플래시 & 넉백)
        TriggerHitVisualFeedback(attacker, amount);

        if (CurrentHp <= 0f && !IsDead)
        {
            IsDead = true;
            Debug.Log($"[{gameObject.name}] 사망!");
            OnHpZero?.Invoke();
            OnDeath?.Invoke();
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

        TriggerHitVisualFeedback(attacker, damage);

        Debug.Log($"<color=red>[Execution Impact] {gameObject.name} (이)가 {damage} 의 처형 피해를 입었습니다!</color>");
    }

    public void AddPosture(float amount)
    {
        if (IsGroggy) return;

        CurrentPosture = Mathf.Clamp(CurrentPosture + amount, 0f, MaxPosture);
        OnPostureChanged?.Invoke(CurrentPosture / MaxPosture);

        if (CurrentPosture >= MaxPosture)
        {
            TriggerGroggyState();
        }
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

    private void TriggerGroggyState()
    {
        IsGroggy = true;
        groggyTimer = DefaultGroggyDuration;
        OnGroggyState?.Invoke();
        Debug.Log($"<color=red><b>[{gameObject.name}] 그로기(Groggy) 상태 돌입!</b></color>");
    }

    private void TriggerHitVisualFeedback(CombatStats attacker, float dmg)
    {
        var rend = GetComponentInChildren<SpriteRenderer>();
        if (rend != null)
        {
            FlashSpriteRedAsync(rend, this.GetCancellationTokenOnDestroy()).Forget();
        }

        if (attacker != null)
        {
            float knockbackForce = Mathf.Clamp(dmg * 0.15f, 1.5f, 4.0f);
            ApplyKnockback(attacker, knockbackForce);
        }
    }

    private void ApplyKnockback(CombatStats attacker, float force)
    {
        if (motor == null) return;
        Vector2 pushDir = transform.position - attacker.transform.position;
        pushDir.x = Mathf.Approximately(pushDir.x, 0f) ? 1f : Mathf.Sign(pushDir.x);
        pushDir.y = Mathf.Max(pushDir.normalized.y, 0.2f);
        motor.ApplyKnockback(pushDir * force, HitReactionDuration);
    }

    private async UniTaskVoid FlashSpriteRedAsync(SpriteRenderer rend, CancellationToken cancellationToken)
    {
        if (rend == null) return;
        Color original = rend.color;
        rend.color = Color.red;
        await UniTask.Delay(System.TimeSpan.FromSeconds(HitReactionDuration), cancellationToken: cancellationToken);
        if (rend != null) rend.color = original;
    }

    private void SpawnResponseEffect(uint effectIdx)
    {
        if (SkillExecutor.Instance != null)
        {
            SkillExecutor.Instance.SpawnEffectByEffectIdxAsync(effectIdx, transform.position).Forget();
        }
    }
}
