using Cysharp.Threading.Tasks;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// HP, MP, Posture, SuperArmor 등 전투 관련 스탯을 총괄 관리합니다.
/// 몬스터 및 플레이어 피격 연출 (적색 플래시, 넉백, 이펙트 생성)을 담당합니다.
/// </summary>
public class CombatStats : MonoBehaviour
{
    private static readonly Dictionary<int, CombatStats> DefenseBodies = new();
    public enum DamageResolution : uint
    {
        None = 0,
        Body = 1,
        Guard = 2,
        Parry = 3,
        Dodge = 4,
        Ignored = 5
    }

    public readonly struct AttackSweep2D
    {
        public readonly Vector2 Previous;
        public readonly Vector2 Current;
        public readonly Vector2 HalfExtents;
        public readonly int SourceId;
        public readonly uint Generation;
        public readonly uint Tick;
        public readonly ActiveShape Shape;
        public readonly Vector2 Size;
        public readonly float Angle;
        public readonly bool HasExteriorPose;

        public AttackSweep2D(Vector2 previous, Vector2 current, Vector2 halfExtents,
            int sourceId, uint generation, uint tick, ActiveShape shape = ActiveShape.Box,
            Vector2 size = default, float angle = 0f, bool hasExteriorPose = false)
        {
            Previous = previous;
            Current = current;
            HalfExtents = halfExtents;
            SourceId = sourceId;
            Generation = generation;
            Tick = tick;
            Shape = shape;
            Size = size == default ? halfExtents * 2f : size;
            Angle = angle;
            HasExteriorPose = hasExteriorPose;
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
    public Collider2D DefenseBodyCollider => defenseBodyCollider;

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
    private UnitBase unit;
    [SerializeField] private SpriteRenderer debugGuardSprite;
    private bool isFacingRight = true;
    private int lastAttackSourceId;
    private uint lastAttackGeneration;
    private uint lastAttackTick;
    private int parriedAttackSourceId;
    private uint parriedAttackGeneration;
    private SpriteRenderer hitFlashRenderer;
    private Color hitFlashOriginalColor;
    private uint hitFlashGeneration;


    // =========================================================================
    // 3. PUBLIC METHODS (PascalCase)
    // =========================================================================

    private void Awake()
    {
        unit = GetComponent<UnitBase>();
        motor = GetComponent<KinematicMotor2D>();
        InitStats();
    }

    private void OnEnable() => RegisterDefenseBody();

    public void InitStats()
    {
        RestoreHitFlash();
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
    public void SetDefenseBodyCollider(Collider2D bodyCollider)
    {
        UnregisterDefenseBody();
        defenseBodyCollider = bodyCollider;
        RegisterDefenseBody();
    }

    public void BindUnit(UnitBase owner)
    {
        UnregisterDefenseBody();
        unit = owner;
        RegisterDefenseBody();
    }

    private void OnDisable()
    {
        UnregisterDefenseBody();
        RestoreHitFlash();
        IsGroggy = false;
        groggyTimer = 0f;
        SetGuardDebugVisible(false);
    }

    private void RegisterDefenseBody()
    {
        if (unit != null && defenseBodyCollider != null)
            DefenseBodies[defenseBodyCollider.GetInstanceID()] = this;
    }

    private void UnregisterDefenseBody()
    {
        if (defenseBodyCollider == null) return;
        int id = defenseBodyCollider.GetInstanceID();
        if (DefenseBodies.TryGetValue(id, out CombatStats registered) && registered == this)
            DefenseBodies.Remove(id);
    }

    public static int CollectAttackSweepVictims(UnitBase owner, AttackSweep2D sweep,
        List<UnitBase> victims, List<float> fractions = null)
    {
        victims.Clear();
        fractions?.Clear();
        if (owner == null || !owner.IsActionGenerationCurrent(sweep.Generation)) return 0;

        Vector2 displacement = sweep.Current - sweep.Previous;
        float distance = displacement.magnitude;
        if (distance <= Mathf.Epsilon)
        {
            Collider2D[] overlaps = sweep.Shape switch
            {
                ActiveShape.Circle => Physics2D.OverlapCircleAll(sweep.Current, sweep.Size.x * .5f),
                ActiveShape.Capsule => Physics2D.OverlapCapsuleAll(sweep.Current, sweep.Size,
                    GetCapsuleDirection(sweep.Size), sweep.Angle),
                _ => Physics2D.OverlapBoxAll(sweep.Current, sweep.Size, sweep.Angle)
            };
            foreach (Collider2D collider in overlaps) AddContact(owner, collider, 0f, victims, fractions);
        }
        else
        {
            Vector2 direction = displacement / distance;
            RaycastHit2D[] hits = sweep.Shape switch
            {
                ActiveShape.Circle => Physics2D.CircleCastAll(sweep.Previous, sweep.Size.x * .5f,
                    direction, distance),
                ActiveShape.Capsule => Physics2D.CapsuleCastAll(sweep.Previous, sweep.Size,
                    GetCapsuleDirection(sweep.Size), sweep.Angle, direction, distance),
                _ => Physics2D.BoxCastAll(sweep.Previous, sweep.Size, sweep.Angle, direction, distance)
            };
            foreach (RaycastHit2D hit in hits) AddContact(owner, hit.collider, hit.fraction, victims, fractions);
        }
        return victims.Count;
    }

    private static void AddContact(UnitBase owner, Collider2D collider, float fraction,
        List<UnitBase> victims, List<float> fractions)
    {
        if (collider == null || !DefenseBodies.TryGetValue(collider.GetInstanceID(), out CombatStats stats)) return;
        UnitBase victim = stats.unit;
        if (!IsHostile(owner, victim) || !victim.isActiveAndEnabled || stats.IsDead ||
            stats.defenseBodyCollider == null || !stats.defenseBodyCollider.enabled) return;

        int existing = victims.IndexOf(victim);
        if (existing >= 0)
        {
            if (fractions != null && fraction < fractions[existing]) fractions[existing] = fraction;
            return;
        }

        int insert = 0;
        while (insert < victims.Count && (fractions == null || fractions[insert] < fraction ||
            Mathf.Approximately(fractions[insert], fraction) &&
            CompareVictims(victims[insert], victim) <= 0)) insert++;
        victims.Insert(insert, victim);
        fractions?.Insert(insert, fraction);
    }

    private static int CompareVictims(UnitBase left, UnitBase right)
    {
        int idx = left.UnitIdx.CompareTo(right.UnitIdx);
        return idx != 0 ? idx : left.GetInstanceID().CompareTo(right.GetInstanceID());
    }

    internal static bool IsHostile(UnitBase owner, UnitBase victim)
    {
        if (owner == null || victim == null || owner == victim) return false;
        return owner.Faction == FactionType.PlayerAlly && victim.Faction == FactionType.Enemy ||
            owner.Faction == FactionType.Enemy && victim.Faction == FactionType.PlayerAlly;
    }

    private void SetGuardDebugVisible(bool stateActive = true)
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (debugGuardSprite == null) return;
        bool visible = stateActive && (IsGuarding || IsParrying);
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
        return TryGetNativeShapeFraction(sweep, defenseBodyCollider, out fraction);
    }

    private static bool TryGetNativeShapeFraction(AttackSweep2D sweep, Collider2D target,
        out float fraction)
    {
        fraction = 0f;
        Vector2 displacement = sweep.Current - sweep.Previous;
        float distance = displacement.magnitude;
        int layerMask = 1 << target.gameObject.layer;
        if (distance <= Mathf.Epsilon)
        {
            Collider2D[] overlaps = sweep.Shape switch
            {
                ActiveShape.Circle => Physics2D.OverlapCircleAll(sweep.Current, sweep.Size.x * .5f, layerMask),
                ActiveShape.Capsule => Physics2D.OverlapCapsuleAll(sweep.Current, sweep.Size,
                    GetCapsuleDirection(sweep.Size), sweep.Angle, layerMask),
                _ => Physics2D.OverlapBoxAll(sweep.Current, sweep.Size, sweep.Angle, layerMask)
            };
            foreach (Collider2D overlap in overlaps)
                if (overlap == target) return true;
            return false;
        }

        Vector2 direction = displacement / distance;
        RaycastHit2D[] hits = sweep.Shape switch
        {
            ActiveShape.Circle => Physics2D.CircleCastAll(sweep.Previous, sweep.Size.x * .5f,
                direction, distance, layerMask),
            ActiveShape.Capsule => Physics2D.CapsuleCastAll(sweep.Previous, sweep.Size,
                GetCapsuleDirection(sweep.Size), sweep.Angle, direction, distance, layerMask),
            _ => Physics2D.BoxCastAll(sweep.Previous, sweep.Size, sweep.Angle, direction, distance, layerMask)
        };
        bool found = false;
        float nearest = 1f;
        foreach (RaycastHit2D hit in hits)
        {
            if (hit.collider != target) continue;
            nearest = Mathf.Min(nearest, hit.fraction);
            found = true;
        }
        fraction = nearest;
        return found;
    }

    private static CapsuleDirection2D GetCapsuleDirection(Vector2 size) =>
        size.y >= size.x ? CapsuleDirection2D.Vertical : CapsuleDirection2D.Horizontal;

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
        DamageResolution result = ResolveDamage(amount, isGroundAttack, isJumped, attacker,
            attackOrigin, guardAmountMultiplier, attackSweep);
        return result != DamageResolution.None && result != DamageResolution.Body;
    }

    public DamageResolution ResolveDamage(float amount, bool isGroundAttack = false, bool isJumped = false,
        CombatStats attacker = null, Vector2? attackOrigin = null, float guardAmountMultiplier = 1f,
        AttackSweep2D? attackSweep = null, bool suppressParryAttackerConsequences = false)
    {
        if (IsDead) return DamageResolution.Ignored;
        if (attackSweep.HasValue)
        {
            AttackSweep2D sweep = attackSweep.Value;
            if (sweep.SourceId == parriedAttackSourceId && sweep.Generation == parriedAttackGeneration) return DamageResolution.Ignored;
            if (sweep.SourceId == lastAttackSourceId && sweep.Generation == lastAttackGeneration && sweep.Tick == lastAttackTick) return DamageResolution.Ignored;
            lastAttackSourceId = sweep.SourceId;
            lastAttackGeneration = sweep.Generation;
            lastAttackTick = sweep.Tick;
        }
        if (IsGroggy)
        {
            amount *= 1.5f;
        }

        Collider2D targetCollider = defenseBodyCollider != null ? defenseBodyCollider : GetComponent<Collider2D>();
        Vector3 contactPoint;
        if (attackSweep.HasValue)
        {
            contactPoint = targetCollider != null
                ? (Vector3)targetCollider.ClosestPoint(attackSweep.Value.Current)
                : (transform.position + Vector3.up * 1.0f);
        }
        else if (attackOrigin.HasValue)
        {
            contactPoint = targetCollider != null
                ? (Vector3)targetCollider.ClosestPoint(attackOrigin.Value)
                : (transform.position + Vector3.up * 1.0f);
        }
        else if (attacker != null)
        {
            contactPoint = targetCollider != null
                ? (Vector3)targetCollider.ClosestPoint(attacker.transform.position)
                : (transform.position + Vector3.up * 1.0f);
        }
        else
        {
            contactPoint = transform.position + Vector3.up * 1.0f;
        }

        if (IsDodging)
        {
            Debug.Log($"[{gameObject.name}] 공격 회피(Dodge) 성공!");
            SpawnResponseEffect(8012, contactPoint);
            return DamageResolution.Dodge;
        }

        if (isGroundAttack && isJumped)
        {
            Debug.Log($"[{gameObject.name}] 지면 공격 점프(Jump) 회피 성공!");
            SpawnResponseEffect(8012, contactPoint);
            return DamageResolution.Dodge;
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
            SpawnResponseEffect(8010, contactPoint);

            if (attacker != null)
            {
                if (!suppressParryAttackerConsequences)
                {
                    attacker.AddPosture(40f);
                    attacker.GetComponent<Monster>()?.CancelCurrentPattern(PatternCancelReason.Cancelled);
                }
            }
            return DamageResolution.Parry;
        }

        if (IsGuarding && canDefend)
        {
            float guardCost = amount * Mathf.Max(0f, guardAmountMultiplier) * 0.5f;
            AddPosture(guardCost);
            SpawnResponseEffect(8011, contactPoint);

            if (attacker != null)
            {
                float knockbackForce = (amount / 10f) * 3.0f;
                ApplyKnockback(attacker, knockbackForce);
            }

            Debug.Log($"[{gameObject.name}] 가드(Guard) 성공!");
            return DamageResolution.Guard;
        }

        if (amount <= 0f) return DamageResolution.None;

        SpawnResponseEffect(8013, contactPoint);
        ApplyHpDamage(amount, attacker);

        if (CurrentHp <= 0f && !IsDead)
        {
            IsDead = true;
            Debug.Log($"[{gameObject.name}] 사망!");
            OnHpZero?.Invoke();
            OnDeath?.Invoke();
        }

        return DamageResolution.Body;
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
        if (!sweep.HasExteriorPose)
        {
            float fallbackFacing = isFacingRight ? 1f : -1f;
            float deltaX = sweep.Current.x - sweep.Previous.x;
            return IsAttackInFront(sweep.Current) &&
                (Mathf.Approximately(deltaX, 0f) || deltaX * fallbackFacing < 0f);
        }
        if (!IsAttackInFront(sweep.Previous)) return false;
        if (!TryGetGuardSweepFraction(sweep, out float guardFraction)) return false;
        if (!TryGetBodySweepFraction(sweep, out float bodyFraction)) return true;

        float epsilon = motor != null ? motor.SkinWidth : Physics2D.defaultContactOffset;
        float sweepLength = Vector2.Distance(sweep.Previous, sweep.Current);
        float facing = isFacingRight ? 1f : -1f;
        return ShouldDefenseWin(sweep.HasExteriorPose, IsAttackInFront(sweep.Previous),
            (sweep.Current.x - sweep.Previous.x) * facing < 0f,
            guardFraction * sweepLength, bodyFraction * sweepLength, epsilon, sweepLength);
    }

    private static bool ShouldDefenseWin(bool hasExteriorPose, bool attackStartsInFront,
        bool directionMatches, float guardDistance, float bodyDistance, float epsilon, float sweepLength) =>
        hasExteriorPose && attackStartsInFront && directionMatches && sweepLength > 0f &&
        guardDistance <= bodyDistance + Mathf.Max(0f, epsilon);

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
            if (hitFlashRenderer != rend)
            {
                RestoreHitFlash();
                hitFlashRenderer = rend;
                hitFlashOriginalColor = rend.color;
            }
            FlashSpriteRedAsync(rend, ++hitFlashGeneration, this.GetCancellationTokenOnDestroy()).Forget();
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

    private async UniTaskVoid FlashSpriteRedAsync(SpriteRenderer rend, uint generation,
        CancellationToken cancellationToken)
    {
        if (rend == null) return;
        rend.color = Color.red;
        try
        {
            await UniTask.Delay(System.TimeSpan.FromSeconds(HitReactionDuration), cancellationToken: cancellationToken);
        }
        catch (System.OperationCanceledException)
        {
        }
        finally
        {
            if (generation == hitFlashGeneration) RestoreHitFlash();
        }
    }

    private void RestoreHitFlash()
    {
        hitFlashGeneration++;
        if (hitFlashRenderer != null) hitFlashRenderer.color = hitFlashOriginalColor;
        hitFlashRenderer = null;
    }

    private void SpawnResponseEffect(uint effectIdx, Vector3 spawnPos)
    {
        SkillExecutor.SpawnEffectByEffectIdxAsync(effectIdx, spawnPos).Forget();
    }
}
