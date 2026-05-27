using UnityEngine;

/// <summary>
/// 적 대기 상태. Stationary 타입이거나 순찰 전 잠시 대기.
/// </summary>
public class EnemyIdleState : IEnemyState
{
    private float _idleTimer;

    public void Enter(EnemyBase enemy)
    {
        enemy.Rb.linearVelocityX = 0f;
        _idleTimer = Random.Range(1f, 2.5f);
    }

    public void Update(EnemyBase enemy)
    {
        // 플레이어 탐지 시 추적
        if (enemy.IsPlayerInRange())
        {
            enemy.StateMachine.ChangeState(enemy.ChaseState, enemy);
            return;
        }

        // Patrol 타입이면 일정 시간 후 순찰 시작
        if (enemy.Data.behavior == EnemyBehavior.Patrol)
        {
            _idleTimer -= Time.deltaTime;
            if (_idleTimer <= 0f)
            {
                enemy.StateMachine.ChangeState(enemy.PatrolState, enemy);
                return;
            }
        }
    }

    public void FixedUpdate(EnemyBase enemy) { }
    public void Exit(EnemyBase enemy) { }
}

/// <summary>
/// 적 순찰 상태. 일정 범위를 좌우로 왕복합니다.
/// </summary>
public class EnemyPatrolState : IEnemyState
{
    private float _patrolDir;
    private float _distanceTraveled;

    public void Enter(EnemyBase enemy)
    {
        _patrolDir = enemy.FacingDirection;
        _distanceTraveled = 0f;
    }

    public void Update(EnemyBase enemy)
    {
        if (enemy.IsPlayerInRange())
        {
            enemy.StateMachine.ChangeState(enemy.ChaseState, enemy);
            return;
        }

        // 벽 감지 또는 낭떠러지 감지 시 방향 전환
        if (enemy.IsWallAhead() || !enemy.IsGroundAhead())
        {
            _patrolDir *= -1f;
            enemy.Flip(_patrolDir);
        }

        // 순찰 거리 초과 시 방향 전환
        _distanceTraveled += enemy.Data.moveSpeed * Time.deltaTime;
        if (_distanceTraveled >= enemy.Data.patrolDistance)
        {
            enemy.StateMachine.ChangeState(enemy.IdleState, enemy);
            return;
        }
    }

    public void FixedUpdate(EnemyBase enemy)
    {
        enemy.Rb.linearVelocityX = _patrolDir * enemy.Data.moveSpeed;
    }

    public void Exit(EnemyBase enemy)
    {
        enemy.Rb.linearVelocityX = 0f;
    }
}

/// <summary>
/// 적 추적 상태. 플레이어를 향해 이동합니다.
/// 공격 사거리에 들어오면 Attack으로 전환합니다.
/// </summary>
public class EnemyChaseState : IEnemyState
{
    public void Enter(EnemyBase enemy)
    {
        // 추적 애니메이션
    }

    public void Update(EnemyBase enemy)
    {
        // 플레이어가 탐지 범위를 벗어나면 Idle로 복귀
        if (!enemy.IsPlayerInRange())
        {
            enemy.StateMachine.ChangeState(enemy.IdleState, enemy);
            return;
        }

        // 공격 사거리 진입 시 Attack으로 전환
        if (enemy.IsPlayerInAttackRange())
        {
            enemy.StateMachine.ChangeState(enemy.AttackState, enemy);
            return;
        }

        // 플레이어 방향으로 회전
        float dirToPlayer = Mathf.Sign(enemy.PlayerTransform.position.x - enemy.transform.position.x);
        enemy.Flip(dirToPlayer);
    }

    public void FixedUpdate(EnemyBase enemy)
    {
        // 플레이어 방향으로 이동
        float dir = Mathf.Sign(enemy.PlayerTransform.position.x - enemy.transform.position.x);
        enemy.Rb.linearVelocityX = dir * enemy.Data.moveSpeed * 1.3f; // 추적 시 약간 빠르게
    }

    public void Exit(EnemyBase enemy)
    {
        enemy.Rb.linearVelocityX = 0f;
    }
}

/// <summary>
/// 적 공격 상태. 쿨타임에 따라 공격을 반복합니다.
/// </summary>
public class EnemyAttackState : IEnemyState
{
    private float _attackTimer;

    public void Enter(EnemyBase enemy)
    {
        enemy.Rb.linearVelocityX = 0f;
        _attackTimer = 0f; // 진입 즉시 첫 공격
    }

    public void Update(EnemyBase enemy)
    {
        // 사거리 이탈 시 다시 추적
        if (!enemy.IsPlayerInAttackRange())
        {
            if (enemy.IsPlayerInRange())
                enemy.StateMachine.ChangeState(enemy.ChaseState, enemy);
            else
                enemy.StateMachine.ChangeState(enemy.IdleState, enemy);
            return;
        }

        _attackTimer -= Time.deltaTime;
        if (_attackTimer <= 0f)
        {
            enemy.PerformAttack();
            _attackTimer = enemy.Data.attackCooldown;
        }

        // 공격 중에도 플레이어 방향 추적
        float dirToPlayer = Mathf.Sign(enemy.PlayerTransform.position.x - enemy.transform.position.x);
        enemy.Flip(dirToPlayer);
    }

    public void FixedUpdate(EnemyBase enemy) { }
    public void Exit(EnemyBase enemy) { }
}

/// <summary>
/// 적 사망 상태. 드롭 처리 후 오브젝트 제거.
/// </summary>
public class EnemyDeadState : IEnemyState
{
    public void Enter(EnemyBase enemy)
    {
        enemy.Rb.linearVelocity = Vector2.zero;
        enemy.Rb.bodyType = RigidbodyType2D.Kinematic;
        enemy.GetComponent<Collider2D>().enabled = false;

        enemy.OnDeathDrop();

        // TODO: 사망 애니메이션 재생 후 Destroy
        Object.Destroy(enemy.gameObject, 0.5f);
    }

    public void Update(EnemyBase enemy) { }
    public void FixedUpdate(EnemyBase enemy) { }
    public void Exit(EnemyBase enemy) { }
}
