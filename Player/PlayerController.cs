using UnityEngine;

/// <summary>
/// 플레이어 컨트롤러.
/// DamageableBase를 상속하여 체력/넉백/무적은 공통 처리,
/// 입력 처리, 물리 연산, 상태머신 등 플레이어 고유 로직을 담당합니다.
/// 
/// [필수 컴포넌트]
/// - Rigidbody2D (DamageableBase에서 RequireComponent)
/// - BoxCollider2D 또는 CapsuleCollider2D
/// 
/// [사용법]
/// 1. 빈 GameObject에 이 스크립트를 부착합니다.
/// 2. Inspector에서 groundCheck 위치(발 아래), groundLayer를 설정합니다.
/// 3. 각 수치를 게임에 맞게 조절합니다.
/// </summary>
public class PlayerController : DamageableBase
{
    // ──────────────────────────────────
    // Inspector 노출 변수
    // ──────────────────────────────────

    [Header("스태미나 (PlayerStam)")]
    [SerializeField] private float playerStamina = 100f;
    [SerializeField] private float staminaRegenRate = 15f; // 초당 회복량

    [Header("이동")]
    [SerializeField] private float moveSpeed = 7f;
    [SerializeField] private float airControlMultiplier = 0.85f;

    [Header("점프")]
    [SerializeField] private float jumpForce = 14f;
    [SerializeField] private float fallGravityMultiplier = 30f; // 낙하 가중치 (Force)
    [SerializeField] private float maxFallSpeed = -20f;

    [Header("대시")]
    [SerializeField] private float dashSpeed = 20f;
    [SerializeField] private float dashDuration = 0.15f;
    [SerializeField] private float dashCooldown = 0.8f;
    [SerializeField] private float dashStaminaCost = 20f;

    [Header("지면 판정 (레이캐스트)")]
    [SerializeField] private Transform groundCheck;
    [SerializeField] private Vector2 groundCheckSize = new Vector2(0.5f, 0.05f);
    [SerializeField] private LayerMask groundLayer;

    // ──────────────────────────────────
    // 공개 프로퍼티 (상태에서 접근)
    // ──────────────────────────────────

    // Rb는 DamageableBase에서 상속
    public PlayerStateMachine StateMachine { get; private set; }

    // 상태 인스턴스 (싱글톤 패턴으로 GC 방지)
    public IdleState IdleState { get; private set; }
    public MoveState MoveState { get; private set; }
    public JumpState JumpState { get; private set; }
    public FallState FallState { get; private set; }
    public DashState DashState { get; private set; }

    // 입력값
    public Vector2 MoveInput { get; private set; }
    public bool JumpInput { get; private set; }
    public bool DashInput { get; private set; }

    // 물리/상태
    public bool IsGrounded { get; private set; }
    public float FacingDirection { get; private set; } = 1f; // 1 = 오른쪽, -1 = 왼쪽
    public float DefaultGravityScale { get; private set; }

    // 스탯 접근자
    public float MoveSpeed => moveSpeed;
    public float JumpForce => jumpForce;
    public float AirControlMultiplier => airControlMultiplier;
    public float FallGravityMultiplier => fallGravityMultiplier;
    public float DashSpeed => dashSpeed;
    public float DashDuration => dashDuration;
    public float DashStaminaCost => dashStaminaCost;
    public float CurrentStamina => _currentStamina;
    public float MaxStamina => playerStamina;

    public bool CanDash => _dashCooldownTimer <= 0f && _currentStamina >= dashStaminaCost;

    // ──────────────────────────────────
    // 내부 변수
    // ──────────────────────────────────

    private float _currentStamina;
    private float _dashCooldownTimer;
    private bool _jumpInputBuffered;

    // ──────────────────────────────────
    // Unity 라이프사이클
    // ──────────────────────────────────

    protected override void Awake()
    {
        base.Awake(); // DamageableBase: Rb 할당, 체력 초기화

        DefaultGravityScale = Rb.gravityScale;

        // 상태 인스턴스 생성
        StateMachine = new PlayerStateMachine();
        IdleState = new IdleState();
        MoveState = new MoveState();
        JumpState = new JumpState();
        FallState = new FallState();
        DashState = new DashState();

        // 스태미나 초기화
        _currentStamina = playerStamina;
    }

    private void Start()
    {
        // 초기 상태: Idle
        StateMachine.ChangeState(IdleState, this);
    }

    protected override void Update()
    {
        base.Update(); // DamageableBase: 무적/넉백 타이머

        if (IsDead) return;

        HandleInput();
        UpdateTimers();
        CheckGround();
        ClampFallSpeed();

        // 넉백 중에는 상태머신 갱신을 건너뜀
        if (!IsKnockedBack)
        {
            StateMachine.Update(this);
        }

#if UNITY_EDITOR
        Debug.Log($"State: {StateMachine.CurrentState?.GetType().Name} | " +
                  $"Grounded: {IsGrounded} | HP: {CurrentHealth:F0}/{MaxHealth:F0} | " +
                  $"Stamina: {_currentStamina:F0}");
#endif
    }

    private void FixedUpdate()
    {
        if (IsDead || IsKnockedBack) return;
        StateMachine.FixedUpdate(this);
    }

    // ──────────────────────────────────
    // DamageableBase 오버라이드
    // ──────────────────────────────────

    /// <summary>
    /// 피격 시: DamageableBase 공통 처리 + 플레이어 고유 피격 연출.
    /// </summary>
    public override void TakeDamage(float damage)
    {
        if (IsDead) return;

        base.TakeDamage(damage);

        // TODO: 피격 이펙트 (화면 흔들기, 깜빡임 등)
    }

    /// <summary>
    /// 사망 처리: 익스트랙션 실패, 아이템 로스트 등.
    /// </summary>
    protected override void HandleDeath()
    {
        base.HandleDeath(); // 이벤트 발동

        // TODO: 사망 UI, 아이템 로스트 로직, 리스폰 또는 메인 메뉴 이동
        Debug.Log("Player Died! Extraction failed.");
    }

    // ──────────────────────────────────
    // 입력 처리
    // ──────────────────────────────────

    /// <summary>
    /// 키보드+마우스 / 게임패드 입력을 처리합니다.
    /// [참고] 새 Input System 사용 시 이 메서드를 교체하면 됩니다.
    /// </summary>
    private void HandleInput()
    {
        float horizontal = Input.GetAxisRaw("Horizontal");
        MoveInput = new Vector2(horizontal, 0f);

        if (Input.GetButtonDown("Jump"))
        {
            _jumpInputBuffered = true;
        }
        JumpInput = _jumpInputBuffered;

        DashInput = Input.GetKeyDown(KeyCode.LeftShift) ||
                    Input.GetButtonDown("Fire3");
    }

    // ──────────────────────────────────
    // 물리 판정
    // ──────────────────────────────────

    private void CheckGround()
    {
        if (groundCheck == null) return;

        IsGrounded = Physics2D.OverlapBox(
            groundCheck.position,
            groundCheckSize,
            0f,
            groundLayer
        );
    }

    private void ClampFallSpeed()
    {
        if (Rb.linearVelocityY < maxFallSpeed)
        {
            Rb.linearVelocityY = maxFallSpeed;
        }
    }

    // ──────────────────────────────────
    // 타이머 & 리소스 관리
    // ──────────────────────────────────

    private void UpdateTimers()
    {
        // 대시 쿨다운
        if (_dashCooldownTimer > 0f)
            _dashCooldownTimer -= Time.deltaTime;

        // 스태미나 자연 회복
        if (_currentStamina < playerStamina)
        {
            _currentStamina += staminaRegenRate * Time.deltaTime;
            _currentStamina = Mathf.Min(_currentStamina, playerStamina);
        }
    }

    // ──────────────────────────────────
    // 상태에서 호출하는 Public 메서드
    // ──────────────────────────────────

    /// <summary> 점프 입력 버퍼 소비 </summary>
    public void ConsumeJumpInput()
    {
        _jumpInputBuffered = false;
        JumpInput = false;
    }

    /// <summary> 스태미나 소모 (음수 값 전달 시 회복) </summary>
    public void ConsumeStamina(float amount)
    {
        _currentStamina = Mathf.Clamp(_currentStamina - amount, 0f, playerStamina);
    }

    /// <summary> 대시 쿨다운 시작 </summary>
    public void SetDashCooldown()
    {
        _dashCooldownTimer = dashCooldown;
    }

    /// <summary> 이동 입력에 따라 스프라이트 방향 전환 </summary>
    public void FlipCheck()
    {
        if (MoveInput.x > 0.01f)
            FacingDirection = 1f;
        else if (MoveInput.x < -0.01f)
            FacingDirection = -1f;

        Vector3 scale = transform.localScale;
        scale.x = Mathf.Abs(scale.x) * FacingDirection;
        transform.localScale = scale;
    }

    // ──────────────────────────────────
    // 기즈모 (에디터 디버깅용)
    // ──────────────────────────────────

    private void OnDrawGizmosSelected()
    {
        if (groundCheck == null) return;

        Gizmos.color = IsGrounded ? Color.green : Color.red;
        Gizmos.DrawWireCube(groundCheck.position, groundCheckSize);
    }
}
