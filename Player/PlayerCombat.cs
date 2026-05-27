using UnityEngine;

/// <summary>
/// 플레이어 전투 시스템.
/// 기획서: 근거리는 히트박스, 원거리는 투사체 생성.
/// 마우스 커서 위치로 조준합니다.
/// 
/// [사용법]
/// PlayerController와 같은 오브젝트에 부착합니다.
/// Inspector에서 attackPoint(무기 발사/히트박스 기준점)를 설정합니다.
/// </summary>
public class PlayerCombat : MonoBehaviour
{
    [Header("참조")]
    [SerializeField] private Transform attackPoint;   // 공격 기준점 (캐릭터 앞)
    [SerializeField] private LayerMask enemyLayer;     // 적 레이어

    [Header("장착 무기")]
    [SerializeField] private WeaponData equippedWeapon;

    // 내부 변수
    private float _attackCooldownTimer;
    private PlayerController _player;
    private Camera _mainCam;

    // 외부 접근
    public WeaponData EquippedWeapon => equippedWeapon;
    public bool IsAttacking { get; private set; }

    private void Awake()
    {
        _player = GetComponent<PlayerController>();
        _mainCam = Camera.main;
    }

    private void Update()
    {
        // 쿨타임 감소
        if (_attackCooldownTimer > 0f)
            _attackCooldownTimer -= Time.deltaTime;

        // 공격 입력: 마우스 좌클릭 또는 게임패드 Fire1
        if (Input.GetButtonDown("Fire1") && _attackCooldownTimer <= 0f && equippedWeapon != null)
        {
            Attack();
        }
    }

    /// <summary>
    /// 무기 타입에 따라 근접/원거리 공격을 실행합니다.
    /// </summary>
    private void Attack()
    {
        _attackCooldownTimer = equippedWeapon.attackCooldown;
        IsAttacking = true;

        switch (equippedWeapon.weaponType)
        {
            case WeaponType.Melee:
                MeleeAttack();
                break;
            case WeaponType.Ranged:
                RangedAttack();
                break;
        }

        // 공격 애니메이션 트리거 (추후 연결)
        // _player.Animator.SetTrigger("Attack");

        // 공격 상태 플래그 해제 (애니메이션 이벤트로 교체 가능)
        Invoke(nameof(ResetAttack), equippedWeapon.attackCooldown * 0.5f);
    }

    /// <summary>
    /// 근접 공격: 플레이어 앞에 히트박스를 생성하여 범위 내 적에게 데미지.
    /// 기획서: "일정 거리 내에서 히트박스를 생성하고, 그 안에 들어오면 명중 판정"
    /// </summary>
    private void MeleeAttack()
    {
        // 히트박스 중심 = attackPoint 위치 + 바라보는 방향으로 offset
        Vector2 hitboxCenter = (Vector2)attackPoint.position
            + Vector2.right * _player.FacingDirection * equippedWeapon.meleeRange;

        // OverlapBox로 범위 내 적 탐색
        Collider2D[] hits = Physics2D.OverlapBoxAll(
            hitboxCenter,
            equippedWeapon.meleeHitboxSize,
            0f,
            enemyLayer
        );

        foreach (Collider2D hit in hits)
        {
            DamageableBase target = hit.GetComponent<DamageableBase>();
            if (target != null)
            {
                float finalDamage = CalculateDamage(equippedWeapon, target.Defense);
                target.TakeDamageWithKnockback(finalDamage, transform.position);
            }
        }
    }

    /// <summary>
    /// 원거리 공격: 마우스 커서 방향으로 투사체를 생성.
    /// 기획서: "마우스 커서 위치로 방향 벡터를 계산하여 무기 발사체 인스턴스 생성"
    /// </summary>
    private void RangedAttack()
    {
        if (equippedWeapon.projectilePrefab == null) return;

        // 마우스 방향 계산
        Vector2 mouseWorldPos = _mainCam.ScreenToWorldPoint(Input.mousePosition);
        Vector2 firePos = attackPoint.position;
        Vector2 direction = (mouseWorldPos - firePos).normalized;

        // 발사체 생성
        GameObject projObj = Instantiate(
            equippedWeapon.projectilePrefab,
            firePos,
            Quaternion.identity
        );

        Projectile proj = projObj.GetComponent<Projectile>();
        if (proj != null)
        {
            proj.Initialize(
                direction,
                equippedWeapon.projectileSpeed,
                equippedWeapon.damage,
                equippedWeapon.penetration,
                equippedWeapon.projectilePierceCount,
                equippedWeapon.projectileLifetime,
                enemyLayer
            );
        }
    }

    /// <summary>
    /// 데미지 계산: 관통력(WPenetration)만큼 방어력 무시.
    /// 기본 공식: finalDamage = WDamage - (EnemyDef × (1 - WPenetration))
    /// 최소 1 데미지 보장.
    /// </summary>
    public static float CalculateDamage(WeaponData weapon, float targetDefense)
    {
        float effectiveDefense = targetDefense * (1f - weapon.penetration);
        return Mathf.Max(1f, weapon.damage - effectiveDefense);
    }

    private void ResetAttack()
    {
        IsAttacking = false;
    }

    /// <summary>
    /// 런타임 무기 교체
    /// </summary>
    public void EquipWeapon(WeaponData newWeapon)
    {
        equippedWeapon = newWeapon;
        _attackCooldownTimer = 0f;
    }

    // 에디터 디버그: 근접 히트박스 시각화
    private void OnDrawGizmosSelected()
    {
        if (attackPoint == null || equippedWeapon == null) return;
        if (equippedWeapon.weaponType != WeaponType.Melee) return;

        float facing = Application.isPlaying ? _player.FacingDirection : 1f;
        Vector2 center = (Vector2)attackPoint.position
            + Vector2.right * facing * equippedWeapon.meleeRange;

        Gizmos.color = Color.yellow;
        Gizmos.DrawWireCube(center, equippedWeapon.meleeHitboxSize);
    }
}
