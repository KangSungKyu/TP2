using UnityEngine;
using UnityEngine.Events;
using Cysharp.Threading.Tasks;

/// <summary>
/// HP, MP, Posture, SuperArmor 등 전투 관련 스탯을 총괄 관리합니다.
/// 언더스코어(_) 접두사 배제 및 this 키워드를 통한 멤버 접근 규칙을 준수합니다.
/// </summary>
public class CombatStats : MonoBehaviour
{
    // =========================================================================
    // 1. PUBLIC FIELDS & PROPERTIES (PascalCase)
    // =========================================================================

    [Header("Base Stats")]
    public float MaxHp = 100f;
    public float MaxMp = 50f;
    public float MaxPosture = 100f; // 자세 게이지 (100% 누적 시 무방비/그로기)
    public float Atk = 10f;          // 기본 공격력

    [Header("SuperArmor Setting")]
    public bool IsSuperArmorActive = false; // 공통 슈퍼아머 활성화 여부 (보스/특수 유닛 전용)

    [Header("Current Values (ReadOnly)")]
    public float CurrentHp { get; private set; }
    public float CurrentMp { get; private set; }
    public float CurrentPosture { get; private set; }

    // 4대 공수 선택 상태 플래그
    public bool IsGuarding { get; private set; }
    public bool IsDodging { get; private set; }
    public bool IsParrying { get; private set; }
    public bool IsJumped { get; private set; }
    public bool IsGroggy { get; private set; }

    // UI 및 전투 반응 이벤트
    public UnityEvent<float> OnHpChanged;      // 0~1
    public UnityEvent<float> OnMpChanged;      // 0~1
    public UnityEvent<float> OnPostureChanged; // 0~1
    public UnityEvent OnParrySuccess;
    public UnityEvent OnGroggyState;
    public UnityEvent OnGroggyEnded;


    // =========================================================================
    // 2. PRIVATE FIELDS (camelCase, No '_' prefix)
    // =========================================================================

    private float groggyTimer = 0f;
    private const float DefaultGroggyDuration = 3.0f; // 그로기 지속시간 3.0초
    private Rigidbody2D rb2d;


    // =========================================================================
    // 3. PUBLIC METHODS (PascalCase)
    // =========================================================================

    /// <summary>
    /// 데미지 입음 (패링, 가드, 회피 판정 포함)
    /// </summary>
    /// <returns>공격이 방어/패링/회피 되었는지 여부</returns>
    public bool TakeDamage(float amount, bool isGroundAttack = false, bool isJumped = false, CombatStats attacker = null)
    {
        if (this.IsGroggy)
        {
            // 그로기 상태에서는 추가 데미지 1.5배 적용
            amount *= 1.5f;
        }

        // 1. 회피 (Dodge) 체크
        if (this.IsDodging)
        {
            Debug.Log($"[{gameObject.name}] 공격 회피(Dodge) 성공! (No Damage)");
            this.spawnResponseEffect(8012); // Dodge 이펙트 스폰 (8012)
            return true;
        }

        // 2. 점프 충격파 회피 체크 (지면 판정 공격이고 점프 중일 때)
        if (isGroundAttack && isJumped)
        {
            Debug.Log($"[{gameObject.name}] 지면 공격 점프(Jump) 회피 성공!");
            this.spawnResponseEffect(8012); // Dodge 이펙트 스폰 (8012)
            return true;
        }

        // 3. 패링 (Parry) 체크
        if (this.IsParrying)
        {
            Debug.Log($"[{gameObject.name}] 패링(Parry) 성공!");
            this.OnParrySuccess?.Invoke();
            this.spawnResponseEffect(8010); // Parry 이펙트 스폰 (8010)

            // 공격한 상대의 Posture 증가 (패링 성공 시 상대 자세 40% 누적)
            if (attacker != null)
            {
                attacker.AddPosture(40f);
            }
            return true;
        }

        // 4. 가드 (Guard) 체크
        if (this.IsGuarding)
        {
            float guardCost = amount * 0.5f;
            this.AddPosture(guardCost); // 가드 시 자세 게이지 누적
            this.spawnResponseEffect(8011); // Guard 이펙트 스폰 (8011)

            // [가드 시 데미지 비례 넉백 적용]
            if (attacker != null && this.rb2d != null)
            {
                Vector2 pushDir = (transform.position - attacker.transform.position).normalized;
                float knockbackForce = (amount / 10f) * 3.0f; // 데미지에 비례한 넉백 세기
                this.rb2d.AddForce(pushDir * knockbackForce, ForceMode2D.Impulse);
            }

            amount = 0f; // 가드 성공 시 체력 피해 100% 감쇄 (0 피해)
            Debug.Log($"[{gameObject.name}] 가드(Guard) 성공! (체력 피해 100% 감쇄 & 자세 누적 & 데미지 비례 넉백 적용)");
            return true;
        }

        if (amount <= 0f) return false;

        // 5. 일반 피격 (Hit)
        this.spawnResponseEffect(8013); // Hit 이펙트 스폰 (8013)
        this.CurrentHp = Mathf.Max(this.CurrentHp - amount, 0f);
        this.OnHpChanged?.Invoke(this.CurrentHp / this.MaxHp);


        if (this.CurrentHp <= 0f)
        {
            Debug.Log($"[{gameObject.name}] 사망!");
        }

        return false;
    }

    /// <summary>
    /// 공용 처형(Execution) 피해를 입습니다.
    /// </summary>
    public void TakeExecutionDamage(float damage, CombatStats attacker = null)
    {
        // Groggy 상태 해제 및 Posture 초기화
        this.IsGroggy = false;
        this.groggyTimer = 0f;
        this.CurrentPosture = 0f;
        this.OnPostureChanged?.Invoke(0f);
        this.OnGroggyEnded?.Invoke();

        // 대량 데미지 적용
        this.CurrentHp = Mathf.Max(this.CurrentHp - damage, 0f);
        this.OnHpChanged?.Invoke(this.CurrentHp / this.MaxHp);

        Debug.Log($"<color=red>[Execution Impact] {gameObject.name} (이)가 {damage} 의 처형 피해를 입었습니다!</color>");
    }

    public void AddPosture(float amount)
    {
        if (this.IsGroggy) return;

        this.CurrentPosture = Mathf.Clamp(this.CurrentPosture + amount, 0f, this.MaxPosture);
        this.OnPostureChanged?.Invoke(this.CurrentPosture / this.MaxPosture);

        if (this.CurrentPosture >= this.MaxPosture)
        {
            this.TriggerGroggy(DefaultGroggyDuration); // 3초간 그로기/무방비 상태
        }
    }

    public void TriggerGroggy(float duration = DefaultGroggyDuration)
    {
        this.IsGroggy = true;
        this.groggyTimer = duration;
        Debug.Log($"[{gameObject.name}] 자세 게이지 임계치 달성! 무방비 그로기(Groggy) 진입 ({duration}초)");
        this.OnGroggyState?.Invoke();
    }

    public void Heal(float amount)
    {
        this.CurrentHp = Mathf.Min(this.CurrentHp + amount, this.MaxHp);
        this.OnHpChanged?.Invoke(this.CurrentHp / this.MaxHp);
    }

    public bool ConsumeMp(float cost)
    {
        if (this.CurrentMp < cost) return false;
        this.CurrentMp -= cost;
        this.OnMpChanged?.Invoke(this.CurrentMp / this.MaxMp);
        return true;
    }

    public void RestoreMp(float amount)
    {
        this.CurrentMp = Mathf.Min(this.CurrentMp + amount, this.MaxMp);
        this.OnMpChanged?.Invoke(this.CurrentMp / this.MaxMp);
    }

    public void SetGuarding(bool value) => this.IsGuarding = value;
    public void SetDodging(bool value) => this.IsDodging = value;
    public void SetParrying(bool value) => this.IsParrying = value;
    public void SetJumped(bool value) => this.IsJumped = value;


    // =========================================================================
    // 4. PRIVATE METHODS (camelCase)
    // =========================================================================

    public void InitStats()
    {
        this.CurrentHp = this.MaxHp;
        this.CurrentMp = this.MaxMp;
        this.CurrentPosture = 0f;
        this.IsGroggy = false;
    }

    private void Awake()
    {
        this.rb2d = GetComponent<Rigidbody2D>();
        this.InitStats();
    }

    private void Update()
    {
        // 그로기 상태 타이머 (3.0초 후 자동 해제)
        if (this.IsGroggy)
        {
            this.groggyTimer -= Time.deltaTime;
            if (this.groggyTimer <= 0f)
            {
                this.IsGroggy = false;
                this.CurrentPosture = 0f;
                this.OnPostureChanged?.Invoke(0f);
                this.OnGroggyEnded?.Invoke();
            }
        }
    }

    private void spawnResponseEffect(uint effectIdx)

    {
        var executor = GetComponent<SkillExecutor>();
        if (executor != null)
        {
            Vector3 pos = transform.position + Vector3.up * 1.0f;
            executor.SpawnEffectByEffectIdxAsync(effectIdx, pos).Forget();
        }
    }
}

