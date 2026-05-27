using UnityEngine;

/// <summary>
/// 적 베이스 클래스.
/// DamageableBase를 상속하여 체력/넉백/무적 등은 공통 처리,
/// 적 고유의 AI, 감지, 층계 스케일링을 담당합니다.
/// 
/// [필수 컴포넌트]
/// - Rigidbody2D (DamageableBase에서 RequireComponent)
/// - Collider2D
/// 
/// [사용법]
/// 1. 적 프리팹에 이 스크립트(또는 상속 클래스)를 부착합니다.
/// 2. Inspector에서 EnemyData, wallCheck, groundAheadCheck 등을 설정합니다.
/// 3. 플레이어에 "Player" 태그를 부여합니다.
/// </summary>
public class EnemyBase : DamageableBase
{
    // ──────────────────────────────────
    // Inspector
    // ──────────────────────────────────

    [Header("데이터")]
    [SerializeField] private EnemyData enemyData;
    [SerializeField] private int currentFloor = 1; // 현재 층계 (스케일링용)

    [Header("감지")]
    [SerializeField] private Transform wallCheck;        // 전방 벽 감지 위치
    [SerializeField] private Transform groundAheadCheck; // 전방 낭떠러지 감지 위치
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

    // ──────────────────────────────────
    // 초기화
    // ──────────────────────────────────

    protected override void Awake()
    {
        base.Awake(); // DamageableBase: Rb 할당, 체력 초기화

        StateMachine = new EnemyStateMachine();
        IdleState = new EnemyIdleState();
        PatrolState = new EnemyPatrolState();
        ChaseState = new EnemyChaseState();
        AttackState = new EnemyAttackState();
        DeadState = new EnemyDeadState();
    }

    private void Start()
    {
        // 층계 스케일링 적용
        ApplyFloorScaling(currentFloor);

        // 플레이어 참조
        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj != null)
            PlayerTransform = playerObj.transform;

        // 초기 상태
        StateMachine.ChangeState(IdleState, this);
    }

    /// <summary>
    /// 절차적 맵 생성 시 스포너에서 호출하여 층계를 설정합니다.
    /// </summary>
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
        base.Update(); // DamageableBase: 무적/넉백 타이머

        if (IsDead) return;
        StateMachine.Update(this);
    }

    private void FixedUpdate()
    {
        if (IsDead) return;
        StateMachine.FixedUpdate(this);
    }

    // ──────────────────────────────────
    // DamageableBase 오버라이드
    // ──────────────────────────────────

    /// <summary>
    /// 피격 시: 넉백도 같이 처리하도록 오버라이드.
    /// </summary>
    public override void TakeDamage(float damage)
    {
        if (IsDead) return;

        base.TakeDamage(damage);

        // 넉백 (플레이어 위치로부터 밀려남)
        if (PlayerTransform != null && !IsDead)
        {
            ApplyKnockback(PlayerTransform.position);
        }
    }

    /// <summary>
    /// 사망 처리: 상태머신을 DeadState로 전환하고 드롭 처리.
    /// </summary>
    protected override void HandleDeath()
    {
        base.HandleDeath(); // 이벤트 발동

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
    /// IDamageable 인터페이스를 통해 플레이어에게 데미지.
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
            IDamageable target = hit.GetComponent<IDamageable>();
            if (target != null)
            {
                // PlayerCombat.CalculateDamage와 동일한 공식 적용
                float effectiveDefense = target.Defense; // 적 공격엔 관통력 없음
                float finalDamage = Mathf.Max(1f, scaledAtk - effectiveDefense);
                target.TakeDamage(finalDamage);
            }
        }
    }

    // ──────────────────────────────────
    // 유틸리티
    // ──────────────────────────────────

    public void Flip(float direction)
    {
        if (Mathf.Abs(direction) < 0.01f) return;

        FacingDirection = Mathf.Sign(direction);
        Vector3 scale = transform.localScale;
        scale.x = Mathf.Abs(scale.x) * FacingDirection;
        transform.localScale = scale;
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
