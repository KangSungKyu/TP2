using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// HP, MP, Posture 등 전투 관련 스탯을 관리합니다.
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

        [Header("Current Values (ReadOnly)")]
        public float CurrentHp { get; private set; }
        public float CurrentMp { get; private set; }
        public float CurrentPosture { get; private set; }

        // 4대 공수 선택 상태 플래그
        public bool IsGuarding { get; private set; }
        public bool IsDodging { get; private set; }
        public bool IsParrying { get; private set; }
        public bool IsGroggy { get; private set; }

        // UI 및 전투 반응 이벤트
        public UnityEvent<float> OnHpChanged;      // 0~1
        public UnityEvent<float> OnMpChanged;      // 0~1
        public UnityEvent<float> OnPostureChanged; // 0~1
        public UnityEvent OnParrySuccess;
        public UnityEvent OnGroggyState;


        // =========================================================================
        // 2. PRIVATE FIELDS (camelCase, No '_' prefix)
        // =========================================================================

        private float groggyTimer = 0f;


        // =========================================================================
        // 3. PUBLIC METHODS (PascalCase)
        // =========================================================================

        /// <summary>
        /// 데미지 입음 (패링, 가드, 회피 판정 포함)
        /// </summary>
        /// <returns>공격이 방어/패링되었는지 여부</returns>
        public bool TakeDamage(float amount, bool isGroundAttack = false, bool isJumped = false, CombatStats attacker = null)
        {
            if (this.IsGroggy)
            {
                // 그로기 상태에서는 추가 데미지
                amount *= 1.5f;
            }

            // 1. 회피 (Dodge) 체크
            if (this.IsDodging)
            {
                Debug.Log($"[{gameObject.name}] 공격 회피(Dodge) 성공!");
                return true;
            }

            // 2. 점프 충격파 회피 체크 (지면 판정 공격이고 점프 중일 때)
            if (isGroundAttack && isJumped)
            {
                Debug.Log($"[{gameObject.name}] 지면 공격 점프(Jump) 회피 성공!");
                return true;
            }

            // 3. 패링 (Parry) 체크 (타격 직전 윈도우 내 패링 입력)
            if (this.IsParrying)
            {
                Debug.Log($"[{gameObject.name}] 패링(Parry) 성공!");
                this.OnParrySuccess?.Invoke();

                // 공격한 상대의 Posture 증가 (기획서: 패링 성공 시 상대 자세 40% 누적)
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
                Debug.Log($"[{gameObject.name}] 가드(Guard) 성공! 피해 감소 및 자세 게이지 누적.");
                amount *= 0.2f; // 데미지 80% 감소
            }

            if (amount <= 0f) return false;

            this.CurrentHp = Mathf.Max(this.CurrentHp - amount, 0f);
            this.OnHpChanged?.Invoke(this.CurrentHp / this.MaxHp);

            if (this.CurrentHp <= 0f)
            {
                Debug.Log($"[{gameObject.name}] 사망!");
            }

            return false;
        }

        public void AddPosture(float amount)
        {
            if (this.IsGroggy) return;

            this.CurrentPosture = Mathf.Clamp(this.CurrentPosture + amount, 0f, this.MaxPosture);
            this.OnPostureChanged?.Invoke(this.CurrentPosture / this.MaxPosture);

            if (this.CurrentPosture >= this.MaxPosture)
            {
                this.TriggerGroggy(2.0f); // 2초간 그로기/무방비 상태
            }
        }

        public void TriggerGroggy(float duration)
        {
            this.IsGroggy = true;
            this.groggyTimer = duration;
            Debug.Log($"[{gameObject.name}] 자세 게이지 임계치 달성! 그로기(Groggy) 상태 진입 ({duration}초)");
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


        // =========================================================================
        // 4. PRIVATE METHODS (camelCase)
        // =========================================================================

        public void InitStats()
        {
            this.CurrentHp = this.MaxHp;
            this.CurrentMp = this.MaxMp;
            this.CurrentPosture = 0f;
        }

        private void Awake()
        {
            this.InitStats();
        }

        private void Update()
        {
            // 그로기 상태 타이머
            if (this.IsGroggy)
            {
                this.groggyTimer -= Time.deltaTime;
                if (this.groggyTimer <= 0f)
                {
                    this.IsGroggy = false;
                    this.CurrentPosture = 0f;
                    this.OnPostureChanged?.Invoke(0f);
                }
            }
        }
    }

