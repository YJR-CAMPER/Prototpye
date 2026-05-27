using UnityEngine;

/// <summary>
/// 적 베이스 클래스.
/// DamageableBase를 상속하여 체력/넉백/무적 등은 공통 처리,
/// 적 고유의 AI, 감지, 층계 스케일링을 담당합니다.
/// 
/// [필수 컴포넌트]
/// - Rigidbody2D (DamageableBase에서 RequireComponent)
/// - Collider2D
/// - SpriteRenderer (방향 전환용)
/// 
/// [사용법]
/// 1. 적 프리팹에 이 스크립트(또는 상속 클래스)를 부착합니다.
/// 2. Inspector에서 EnemyData, wallCheck, groundAheadCheck 등을 설정합니다.
/// 3. 스포너(또는 게임매니저)에서 SetPlayer()로 플레이어 참조를 주입합니다.
/// </summary>
public class EnemyBase : DamageableBase
{
    // ──────────────────────────────────
    // Inspector
    // ──────────────────────────────────

    [Header("데이터")]
    [SerializeField] private EnemyData enemyData;
    [SerializeField] private int currentFloor = 1;

    [Header("감지")]
    [SerializeField] private Transform wallCheck;
    [SerializeField] private Transform groundAheadCheck;
    [SerializeField] private LayerMask groundLayer;
    [SerializeField] private LayerMask playerLayer;

    [Header("공격")]
    [SerializeField] private Transform attackPoint;
    [SerializeField] private Vector2 attackHitboxSize = new Vector2(1.2f, 0.8f);

    // ──────────────────────────────────
    // 공개 프로퍼티
    // ──────────────────────────────────

    public EnemyData Data => enemyData;
    public EnemyStateMachine StateMachine { get; private set; }
    public Transform PlayerTransform { get; private set; }
    public float FacingDirection { get; private set; } = -1f;

    // 상태 인스턴스
    public EnemyIdleState IdleState { get; private set; }
    public EnemyPatrolState PatrolState { get; private set; }
    public EnemyChaseState ChaseState { get; private set; }
    public EnemyAttackState AttackState { get; private set; }
    public EnemyDeadState DeadState { get; private set; }

    // 비주얼
    private SpriteRenderer _spriteRenderer;

    // ──────────────────────────────────
    // 초기화
    // ──────────────────────────────────

    protected override void Awake()
    {
        base.Awake();

        _spriteRenderer = GetComponentInChildren<SpriteRenderer>();

        StateMachine = new EnemyStateMachine();
        IdleState = new EnemyIdleState();
        PatrolState = new EnemyPatrolState();
        ChaseState = new EnemyChaseState();
        AttackState = new EnemyAttackState();
        DeadState = new EnemyDeadState();
    }

    private void Start()
    {
        ApplyFloorScaling(currentFloor);

        // 플레이어 참조가 아직 주입되지 않은 경우 (에디터 테스트 등) 폴백
        if (PlayerTransform == null)
        {
            GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
            if (playerObj != null)
                PlayerTransform = playerObj.transform;
        }

        StateMachine.ChangeState(IdleState, this);
    }

    /// <summary>
    /// 스포너/게임매니저에서 호출하여 플레이어 참조를 주입합니다.
    /// FindGameObjectWithTag 대신 이 메서드를 사용하세요.
    /// </summary>
    public void SetPlayer(Transform playerTransform)
    {
        PlayerTransform = playerTransform;
    }

    /// <summary>
    /// 절차적 맵 생성 시 스포너에서 호출하여 층계 + 플레이어를 동시에 설정합니다.
    /// </summary>
    public void Initialize(int floor, Transform playerTransform)
    {
        SetFloor(floor);
        SetPlayer(playerTransform);
    }

    public void SetFloor(int floor)
    {
        currentFloor = floor;
        ApplyFloorScaling(floor);
    }

    private void ApplyFloorScaling(int floor)
    {
        SetMaxHealth(enemyData.GetScaledHealth(floor), true);
        SetDefense(enemyData.GetScaledDefense(floor));
    }

    // ──────────────────────────────────
    // Update
    // ──────────────────────────────────

    protected override void Update()
    {
        base.Update();

        if (IsDead) return;

        // 넉백 중에는 AI 갱신 건너뜀
        if (!IsKnockedBack)
        {
            StateMachine.Update(this);
        }
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
    /// 피격 시: 넉백은 여기서 처리하지 않습니다.
    /// 공격 측에서 TakeDamageWithKnockback(damage, sourcePosition)을 호출하여
    /// 실제 데미지 원점 기준으로 넉백 방향이 결정됩니다.
    /// </summary>
    public override bool TakeDamage(float damage)
    {
        if (IsDead) return false;
        return base.TakeDamage(damage);
    }

    /// <summary>
    /// 사망 처리: 상태머신을 DeadState로 전환.
    /// </summary>
    protected override void HandleDeath()
    {
        base.HandleDeath();
        StateMachine.ChangeState(DeadState, this);
    }

    /// <summary>
    /// 사망 시 드롭 처리. DeadState에서 호출됩니다.
    /// </summary>
    public virtual void OnDeathDrop()
    {
        // TODO: 골드 드롭 (enemyData.goldDrop)
        // TODO: 아이템 드롭 테이블 참조
        Debug.Log($"{enemyData.enemyName} defeated! Gold: {enemyData.goldDrop}");
    }

    // ──────────────────────────────────
    // 감지 메서드 (상태에서 호출)
    // ──────────────────────────────────

    public bool IsPlayerInRange()
    {
        if (PlayerTransform == null) return false;
        float dist = Vector2.Distance(transform.position, PlayerTransform.position);
        return dist <= enemyData.detectionRange;
    }

    public bool IsPlayerInAttackRange()
    {
        if (PlayerTransform == null) return false;
        float dist = Vector2.Distance(transform.position, PlayerTransform.position);
        return dist <= enemyData.attackRange;
    }

    public bool IsWallAhead()
    {
        if (wallCheck == null) return false;
        return Physics2D.Raycast(
            wallCheck.position,
            Vector2.right * FacingDirection,
            0.3f,
            groundLayer
        );
    }

    public bool IsGroundAhead()
    {
        if (groundAheadCheck == null) return true;
        return Physics2D.Raycast(
            groundAheadCheck.position,
            Vector2.down,
            1.5f,
            groundLayer
        );
    }

    // ──────────────────────────────────
    // 전투
    // ──────────────────────────────────

    /// <summary>
    /// 근접 공격 실행. AttackState에서 호출됩니다.
    /// DamageableBase의 TakeDamageWithKnockback를 사용하여
    /// 공격자(이 적)의 위치 기준으로 넉백이 적용됩니다.
    /// </summary>
    public void PerformAttack()
    {
        if (attackPoint == null) return;

        Collider2D[] hits = Physics2D.OverlapBoxAll(
            attackPoint.position,
            attackHitboxSize,
            0f,
            playerLayer
        );

        float scaledAtk = enemyData.GetScaledAttack(currentFloor);

        foreach (Collider2D hit in hits)
        {
            DamageableBase target = hit.GetComponent<DamageableBase>();
            if (target != null)
            {
                float effectiveDefense = target.Defense;
                float finalDamage = Mathf.Max(1f, scaledAtk - effectiveDefense);
                target.TakeDamageWithKnockback(finalDamage, transform.position);
            }
        }
    }

    // ──────────────────────────────────
    // 유틸리티 (Fix 4: SpriteRenderer.flipX 사용)
    // ──────────────────────────────────

    /// <summary>
    /// 방향 전환. SpriteRenderer.flipX를 사용하여
    /// 자식 오브젝트(UI, 히트박스 등)에 영향을 주지 않습니다.
    /// </summary>
    public void Flip(float direction)
    {
        if (Mathf.Abs(direction) < 0.01f) return;

        FacingDirection = Mathf.Sign(direction);

        if (_spriteRenderer != null)
        {
            // 기본 스프라이트가 왼쪽(-1)을 향한다고 가정
            // 오른쪽(+1)을 바라볼 때 flipX = true
            _spriteRenderer.flipX = FacingDirection > 0f;
        }
    }

    // ──────────────────────────────────
    // 기즈모
    // ──────────────────────────────────

    private void OnDrawGizmosSelected()
    {
        if (enemyData == null) return;

        Gizmos.color = new Color(1f, 1f, 0f, 0.15f);
        Gizmos.DrawWireSphere(transform.position, enemyData.detectionRange);

        Gizmos.color = new Color(1f, 0f, 0f, 0.3f);
        Gizmos.DrawWireSphere(transform.position, enemyData.attackRange);

        if (attackPoint != null)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireCube(attackPoint.position, attackHitboxSize);
        }
    }
}
