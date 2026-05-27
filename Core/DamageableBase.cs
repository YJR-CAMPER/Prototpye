using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// 데미지를 받을 수 있는 오브젝트의 공통 베이스 클래스.
/// 체력 관리, 사망 판정, 넉백, 무적 시간 등 공통 로직을 담습니다.
/// 
/// IDamageable 인터페이스를 구현하며, PlayerController와 EnemyBase가 이를 상속합니다.
/// 
/// [구조]
/// IDamageable (인터페이스)
///    ↑
/// DamageableBase (추상 클래스) ← 여기
///    ↑           ↑
/// EnemyBase    PlayerController
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
public abstract class DamageableBase : MonoBehaviour, IDamageable
{
    // ──────────────────────────────────
    // Inspector (상속 클래스에서 접근 가능)
    // ──────────────────────────────────

    [Header("체력")]
    [SerializeField] protected float maxHealth = 100f;

    [Header("방어력")]
    [SerializeField] protected float defense = 0f;

    [Header("넉백")]
    [SerializeField] protected float knockbackForce = 5f;
    [SerializeField] protected float knockbackDuration = 0.15f;

    [Header("무적 시간 (피격 후)")]
    [SerializeField] protected float invincibilityDuration = 0.2f;

    [Header("이벤트")]
    public UnityEvent<float, float> OnHealthChanged;  // (currentHP, maxHP)
    public UnityEvent OnDeath;
    public UnityEvent OnHit;

    // ──────────────────────────────────
    // 공개 프로퍼티
    // ──────────────────────────────────

    public Rigidbody2D Rb { get; private set; }

    public float MaxHealth => maxHealth;
    public float CurrentHealth => _currentHealth;
    public bool IsDead => _isDead;
    public bool IsInvincible => _invincibilityTimer > 0f;
    public bool IsKnockedBack => _knockbackTimer > 0f;

    // IDamageable
    public float Defense => defense;

    // ──────────────────────────────────
    // 내부 변수
    // ──────────────────────────────────

    protected float _currentHealth;
    protected bool _isDead;
    private float _invincibilityTimer;
    private float _knockbackTimer;

    // ──────────────────────────────────
    // 초기화
    // ──────────────────────────────────

    protected virtual void Awake()
    {
        Rb = GetComponent<Rigidbody2D>();
        _currentHealth = maxHealth;
    }

    protected virtual void Update()
    {
        // 타이머 갱신
        if (_invincibilityTimer > 0f)
            _invincibilityTimer -= Time.deltaTime;

        if (_knockbackTimer > 0f)
            _knockbackTimer -= Time.deltaTime;
    }

    // ──────────────────────────────────
    // IDamageable 구현
    // ──────────────────────────────────

    /// <summary>
    /// 데미지를 받습니다. 방어력은 이미 공격 측에서 계산된 최종 데미지가 들어옵니다.
    /// 실제로 데미지가 적용되었으면 true, 무적/사망 등으로 무시되면 false를 반환합니다.
    /// </summary>
    public virtual bool TakeDamage(float damage)
    {
        if (_isDead || IsInvincible) return false;

        _currentHealth -= damage;
        _currentHealth = Mathf.Clamp(_currentHealth, 0f, maxHealth);

        OnHit?.Invoke();
        OnHealthChanged?.Invoke(_currentHealth, maxHealth);

        // 무적 시간 시작
        _invincibilityTimer = invincibilityDuration;

        if (_currentHealth <= 0f)
        {
            _isDead = true;
            HandleDeath();
        }

        return true;
    }

    // ──────────────────────────────────
    // 회복
    // ──────────────────────────────────

    /// <summary>
    /// 체력을 회복합니다. 최대 체력을 넘지 않습니다.
    /// </summary>
    public virtual void Heal(float amount)
    {
        if (_isDead) return;

        _currentHealth = Mathf.Min(_currentHealth + amount, maxHealth);
        OnHealthChanged?.Invoke(_currentHealth, maxHealth);
    }

    // ──────────────────────────────────
    // 넉백
    // ──────────────────────────────────

    /// <summary>
    /// 피격 시 넉백을 적용합니다.
    /// </summary>
    /// <param name="sourcePosition">공격자의 위치 (밀려나는 방향 계산용)</param>
    public virtual void ApplyKnockback(Vector2 sourcePosition)
    {
        if (_isDead) return;

        Vector2 direction = ((Vector2)transform.position - sourcePosition).normalized;
        Rb.linearVelocity = Vector2.zero; // 기존 속도 초기화
        Rb.AddForce(direction * knockbackForce, ForceMode2D.Impulse);

        _knockbackTimer = knockbackDuration;
    }

    /// <summary>
    /// 데미지 + 넉백을 동시에 처리합니다.
    /// 데미지가 실제로 적용된 경우에만 넉백을 적용합니다.
    /// </summary>
    /// <param name="damage">최종 데미지</param>
    /// <param name="sourcePosition">데미지를 준 주체의 위치 (넉백 방향 계산용)</param>
    public virtual void TakeDamageWithKnockback(float damage, Vector2 sourcePosition)
    {
        bool damageTaken = TakeDamage(damage);

        // 데미지를 입지 않았거나(무적), 사망했으면 넉백 무시
        if (!damageTaken || _isDead) return;

        ApplyKnockback(sourcePosition);
    }

    // ──────────────────────────────────
    // 사망 (상속 클래스에서 구체적 구현)
    // ──────────────────────────────────

    /// <summary>
    /// 사망 시 호출됩니다. 상속 클래스에서 오버라이드하여
    /// 드롭, 익스트랙션 실패 등 고유 로직을 구현합니다.
    /// </summary>
    protected virtual void HandleDeath()
    {
        OnDeath?.Invoke();
        Debug.Log($"{gameObject.name} died!");
    }

    // ──────────────────────────────────
    // 체력 설정 (스케일링 등 외부에서 필요할 때)
    // ──────────────────────────────────

    /// <summary>
    /// 최대 체력과 현재 체력을 설정합니다.
    /// 적의 층계별 스케일링, 플레이어 장비 효과 등에 사용.
    /// </summary>
    public void SetMaxHealth(float newMax, bool fillToMax = true)
    {
        maxHealth = newMax;
        if (fillToMax)
            _currentHealth = maxHealth;

        OnHealthChanged?.Invoke(_currentHealth, maxHealth);
    }

    /// <summary>
    /// 방어력을 설정합니다.
    /// </summary>
    public void SetDefense(float newDefense)
    {
        defense = newDefense;
    }
}
