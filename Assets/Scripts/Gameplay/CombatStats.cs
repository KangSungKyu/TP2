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
    public readonly struct AttackSweep2D
    {
        public readonly Vector2 Previous;
        public readonly Vector2 Current;
        public readonly Vector2 HalfExtents;
        public readonly int SourceId;
        public readonly uint Generation;
        public readonly uint Tick;

        public AttackSweep2D(Vector2 previous, Vector2 current, Vector2 halfExtents,
            int sourceId, uint generation, uint tick)
        {
            Previous = previous;
            Current = current;
            HalfExtents = halfExtents;
            SourceId = sourceId;
            Generation = generation;
            Tick = tick;
        }
    }

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
    private Collider2D defenseBodyCollider;
    [SerializeField] private SpriteRenderer debugGuardSprite;
    private bool isFacingRight = true;
    private int lastAttackSourceId;
    private uint lastAttackGeneration;
    private uint lastAttackTick;
    private int parriedAttackSourceId;
    private uint parriedAttackGeneration;


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
        IsGuarding = IsDodging = IsParrying = false;
        IsGroggy = false;
        IsDead = false;
        groggyTimer = 0f;
        lastAttackSourceId = parriedAttackSourceId = 0;
        lastAttackGeneration = lastAttackTick = parriedAttackGeneration = 0;
        SetGuardDebugVisible(false);
    }

    public void SetGuarding(bool state)
    {
        IsGuarding = state;
        SetGuardDebugVisible();
    }
    public void SetDodging(bool state) => IsDodging = state;
    public void SetParrying(bool state)
    {
        IsParrying = state;
        SetGuardDebugVisible();
    }
    public void SetJumped(bool state) => IsJumped = state;
    public void SetFacingRight(bool state)
    {
        isFacingRight = state;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (debugGuardSprite != null)
        {
            Vector3 position = debugGuardSprite.transform.localPosition;
            position.x = Mathf.Abs(position.x) * (state ? 1f : -1f);
            debugGuardSprite.transform.localPosition = position;
        }
#endif
    }
    public void SetDefenseBodyCollider(Collider2D bodyCollider) => defenseBodyCollider = bodyCollider;

    private void OnDisable() => SetGuardDebugVisible(false);

    private void SetGuardDebugVisible(bool stateActive = true)
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (debugGuardSprite == null) return;
        bool visible = UnitAttackHitbox2D.DebugVisualizationEnabled && stateActive && (IsGuarding || IsParrying);
        debugGuardSprite.enabled = visible;
        if (visible) debugGuardSprite.color = IsParrying ? new Color(0f, 1f, 1f, 0.35f) : new Color(0f, 0.5f, 1f, 0.35f);
#else
        if (debugGuardSprite != null) debugGuardSprite.enabled = false;
#endif
    }

    public bool TryGetBodySweepFraction(AttackSweep2D sweep, out float fraction)
    {
        if (defenseBodyCollider == null || !defenseBodyCollider.enabled)
        {
            fraction = 0f;
            return false;
        }
        Bounds body = defenseBodyCollider.bounds;
        body.Expand(new Vector3(sweep.HalfExtents.x * 2f, sweep.HalfExtents.y * 2f, 0f));
        return TryGetSweepFraction(sweep.Previous, sweep.Current, body, out fraction);
    }

    public bool TryGetAttackSweepFraction(AttackSweep2D sweep, out float fraction)
    {
        bool bodyHit = TryGetBodySweepFraction(sweep, out float bodyFraction);
        bool guardHit = TryGetGuardSweepFraction(sweep, out float guardFraction);
        fraction = bodyHit && guardHit ? Mathf.Min(bodyFraction, guardFraction) :
            bodyHit ? bodyFraction : guardFraction;
        return bodyHit || guardHit;
    }

    public bool ConsumeMp(float amount)
    {
        if (CurrentMp < amount) return false;
        CurrentMp = Mathf.Max(CurrentMp - amount, 0f);
        OnMpChanged?.Invoke(CurrentMp / MaxMp);
        return true;
    }

    public bool TakeDamage(float amount, bool isGroundAttack = false, bool isJumped = false,
        CombatStats attacker = null, Vector2? attackOrigin = null, float guardAmountMultiplier = 1f,
        AttackSweep2D? attackSweep = null)
    {
        if (IsDead) return false;
        if (attackSweep.HasValue)
        {
            AttackSweep2D sweep = attackSweep.Value;
            if (sweep.SourceId == parriedAttackSourceId && sweep.Generation == parriedAttackGeneration) return true;
            if (sweep.SourceId == lastAttackSourceId && sweep.Generation == lastAttackGeneration && sweep.Tick == lastAttackTick) return true;
            lastAttackSourceId = sweep.SourceId;
            lastAttackGeneration = sweep.Generation;
            lastAttackTick = sweep.Tick;
        }
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

        bool canDefend = attackSweep.HasValue
            ? DoesGuardIntersectFirst(attackSweep.Value)
            : IsAttackInFront(attackOrigin ?? (attacker != null ? (Vector2?)attacker.transform.position : null));
        if (IsParrying && canDefend)
        {
            if (attackSweep.HasValue)
            {
                parriedAttackSourceId = attackSweep.Value.SourceId;
                parriedAttackGeneration = attackSweep.Value.Generation;
            }
            Debug.Log($"[{gameObject.name}] 패링(Parry) 성공!");
            OnParrySuccess?.Invoke();
            SpawnResponseEffect(8010);

            if (attacker != null)
            {
                attacker.AddPosture(40f);
            }
            return true;
        }

        if (IsGuarding && canDefend)
        {
            float guardCost = amount * Mathf.Max(0f, guardAmountMultiplier) * 0.5f;
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
        ApplyHpDamage(amount, attacker);

        // 몬스터 / 유닛 피격 반응 연출 (스프라이트 적색 플래시 & 넉백)

        if (CurrentHp <= 0f && !IsDead)
        {
            IsDead = true;
            Debug.Log($"[{gameObject.name}] 사망!");
            OnHpZero?.Invoke();
            OnDeath?.Invoke();
        }

        return false;
    }

    private bool IsAttackInFront(Vector2? attackOrigin)
    {
        if (!attackOrigin.HasValue) return true;
        float deltaX = attackOrigin.Value.x - transform.position.x;
        if (Mathf.Approximately(deltaX, 0f)) return true;
        Vector2 facing = isFacingRight ? Vector2.right : Vector2.left;
        return Vector2.Dot(facing, new Vector2(deltaX, 0f).normalized) >= 0f;
    }

    private bool DoesGuardIntersectFirst(AttackSweep2D sweep)
    {
        if (!TryGetGuardSweepFraction(sweep, out float guardFraction)) return false;
        if (!TryGetBodySweepFraction(sweep, out float bodyFraction)) return true;

        float epsilon = motor != null ? motor.SkinWidth : Physics2D.defaultContactOffset;
        float sweepLength = Vector2.Distance(sweep.Previous, sweep.Current);
        return sweepLength > 0f && guardFraction + epsilon / sweepLength < bodyFraction;
    }

    private bool TryGetGuardSweepFraction(AttackSweep2D sweep, out float fraction)
    {
        if ((!IsGuarding && !IsParrying) || defenseBodyCollider == null || !defenseBodyCollider.enabled)
        {
            fraction = 0f;
            return false;
        }
        Bounds guard = defenseBodyCollider.bounds;
        guard.center += Vector3.right * (isFacingRight ? guard.size.x : -guard.size.x);
        guard.Expand(new Vector3(Mathf.Max(0f, sweep.HalfExtents.x) * 2f,
            Mathf.Max(0f, sweep.HalfExtents.y) * 2f, 0f));
        return TryGetSweepFraction(sweep.Previous, sweep.Current, guard, out fraction);
    }

    private static bool TryGetSweepFraction(Vector2 start, Vector2 end, Bounds bounds, out float fraction)
    {
        Vector2 delta = end - start;
        float enter = 0f;
        float exit = 1f;
        if (!ClipAxis(start.x, delta.x, bounds.min.x, bounds.max.x, ref enter, ref exit) ||
            !ClipAxis(start.y, delta.y, bounds.min.y, bounds.max.y, ref enter, ref exit))
        {
            fraction = 0f;
            return false;
        }
        fraction = enter;
        return true;
    }

    private static bool ClipAxis(float start, float delta, float min, float max, ref float enter, ref float exit)
    {
        if (Mathf.Approximately(delta, 0f)) return start >= min && start <= max;
        float first = (min - start) / delta;
        float second = (max - start) / delta;
        if (first > second) (first, second) = (second, first);
        enter = Mathf.Max(enter, first);
        exit = Mathf.Min(exit, second);
        return enter <= exit && exit >= 0f && enter <= 1f;
    }

    public void TakeExecutionDamage(float damage, CombatStats attacker = null)
    {
        if (IsDead || damage <= 0f) return;
        IsGroggy = false;
        groggyTimer = 0f;
        CurrentPosture = 0f;
        OnPostureChanged?.Invoke(0f);
        OnGroggyEnded?.Invoke();

        ApplyHpDamage(damage, attacker);

        Debug.Log($"<color=red>[Execution Impact] {gameObject.name} (이)가 {damage} 의 처형 피해를 입었습니다!</color>");
    }

    private void ApplyHpDamage(float damage, CombatStats attacker)
    {
        if (IsDead || damage <= 0f) return;
        CurrentHp = Mathf.Max(CurrentHp - damage, 0f);
        OnHpChanged?.Invoke(MaxHp > 0f ? CurrentHp / MaxHp : 0f);
        TriggerHitVisualFeedback(attacker, damage);
        if (CurrentHp > 0f) return;

        IsDead = true;
        Debug.Log($"[{gameObject.name}] died.");
        OnHpZero?.Invoke();
        OnDeath?.Invoke();
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
        if (IsSuperArmorActive)
        {
            motor.ApplyKnockback(Vector2.zero);
            return;
        }
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
