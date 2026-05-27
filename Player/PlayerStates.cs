using UnityEngine;

/// <summary>
/// M_Idle: 정지 상태.
/// 지면에 서 있고 입력이 없을 때 유지됩니다.
/// </summary>
public class IdleState : IPlayerState
{
    public void Enter(PlayerController player)
    {
        // 속도 초기화, Idle 애니메이션 트리거
        player.Rb.linearVelocityX = 0f;
        // player.Animator.Play("Idle");
    }

    public void Update(PlayerController player)
    {
        // 대시 입력 체크 (최우선)
        if (player.DashInput && player.CanDash)
        {
            player.StateMachine.ChangeState(player.DashState, player);
            return;
        }

        // 점프 입력 체크
        if (player.JumpInput && player.IsGrounded)
        {
            player.StateMachine.ChangeState(player.JumpState, player);
            return;
        }

        // 이동 입력이 있으면 Move로 전환
        if (Mathf.Abs(player.MoveInput.x) > 0.01f)
        {
            player.StateMachine.ChangeState(player.MoveState, player);
            return;
        }

        // 지면에서 벗어나면 (낭떠러지 등) Fall로 전환
        if (!player.IsGrounded)
        {
            player.StateMachine.ChangeState(player.FallState, player);
            return;
        }
    }

    public void FixedUpdate(PlayerController player) { }
    public void Exit(PlayerController player) { }
}

/// <summary>
/// M_Move: 이동 상태.
/// 지면 위에서 좌우 이동 중일 때 유지됩니다.
/// </summary>
public class MoveState : IPlayerState
{
    public void Enter(PlayerController player)
    {
        // player.Animator.Play("Run");
    }

    public void Update(PlayerController player)
    {
        if (player.DashInput && player.CanDash)
        {
            player.StateMachine.ChangeState(player.DashState, player);
            return;
        }

        if (player.JumpInput && player.IsGrounded)
        {
            player.StateMachine.ChangeState(player.JumpState, player);
            return;
        }

        // 입력이 없으면 Idle로
        if (Mathf.Abs(player.MoveInput.x) < 0.01f)
        {
            player.StateMachine.ChangeState(player.IdleState, player);
            return;
        }

        if (!player.IsGrounded)
        {
            player.StateMachine.ChangeState(player.FallState, player);
            return;
        }

        // 캐릭터 방향 전환 (스프라이트 플립)
        player.FlipCheck();
    }

    public void FixedUpdate(PlayerController player)
    {
        // 수평 이동 적용
        player.Rb.linearVelocityX = player.MoveInput.x * player.MoveSpeed;
    }

    public void Exit(PlayerController player) { }
}

/// <summary>
/// M_Jump: 점프 상태.
/// 점프 키 입력 시 상승하는 동안 유지됩니다.
/// 상승이 끝나면(velocityY <= 0) Fall로 전환합니다.
/// </summary>
public class JumpState : IPlayerState
{
    public void Enter(PlayerController player)
    {
        // 점프력 적용
        player.Rb.linearVelocityY = player.JumpForce;
        player.ConsumeJumpInput();
        // player.Animator.Play("Jump");
    }

    public void Update(PlayerController player)
    {
        if (player.DashInput && player.CanDash)
        {
            player.StateMachine.ChangeState(player.DashState, player);
            return;
        }

        // 상승이 끝나면 Fall로 전환
        if (player.Rb.linearVelocityY <= 0f)
        {
            player.StateMachine.ChangeState(player.FallState, player);
            return;
        }

        // 점프 중에도 공중 수평 이동 허용
        player.FlipCheck();
    }

    public void FixedUpdate(PlayerController player)
    {
        // 공중 수평 이동 (지상보다 약간 감쇠 가능)
        player.Rb.linearVelocityX = player.MoveInput.x * player.MoveSpeed * player.AirControlMultiplier;
    }

    public void Exit(PlayerController player) { }
}

/// <summary>
/// M_Fall: 낙하 상태.
/// 공중에서 하강 중일 때 유지됩니다. 착지하면 Idle 또는 Move로 복귀합니다.
/// 기획서대로 낙하 시 중력 가중치를 적용합니다.
/// </summary>
public class FallState : IPlayerState
{
    public void Enter(PlayerController player)
    {
        // player.Animator.Play("Fall");
    }

    public void Update(PlayerController player)
    {
        if (player.DashInput && player.CanDash)
        {
            player.StateMachine.ChangeState(player.DashState, player);
            return;
        }

        // 착지 판정
        if (player.IsGrounded)
        {
            if (Mathf.Abs(player.MoveInput.x) > 0.01f)
                player.StateMachine.ChangeState(player.MoveState, player);
            else
                player.StateMachine.ChangeState(player.IdleState, player);
            return;
        }

        player.FlipCheck();
    }

    public void FixedUpdate(PlayerController player)
    {
        // 공중 수평 이동
        player.Rb.linearVelocityX = player.MoveInput.x * player.MoveSpeed * player.AirControlMultiplier;

        // 낙하 가속 (중력 가중치) - 기획서: "점프 후 낙하 시 가중치"
        if (player.Rb.linearVelocityY < 0f)
        {
            player.Rb.AddForce(Vector2.down * player.FallGravityMultiplier, ForceMode2D.Force);
        }
    }

    public void Exit(PlayerController player) { }
}

/// <summary>
/// M_Dash: 대시 상태.
/// 스태미나를 소모하며 짧은 시간 동안 빠르게 이동합니다.
/// 대시 중에는 중력을 무시합니다.
/// </summary>
public class DashState : IPlayerState
{
    private float _dashTimer;

    public void Enter(PlayerController player)
    {
        _dashTimer = player.DashDuration;
        player.ConsumeStamina(player.DashStaminaCost);
        player.SetDashCooldown();

        // 대시 방향 결정: 입력이 있으면 입력 방향, 없으면 캐릭터가 바라보는 방향
        float dashDir = Mathf.Abs(player.MoveInput.x) > 0.01f
            ? Mathf.Sign(player.MoveInput.x)
            : player.FacingDirection;

        player.Rb.linearVelocity = new Vector2(dashDir * player.DashSpeed, 0f);
        player.Rb.gravityScale = 0f; // 대시 중 중력 무시

        // player.Animator.Play("Dash");
    }

    public void Update(PlayerController player)
    {
        _dashTimer -= Time.deltaTime;

        if (_dashTimer <= 0f)
        {
            // 대시 종료 후 상태 판정
            if (!player.IsGrounded)
                player.StateMachine.ChangeState(player.FallState, player);
            else if (Mathf.Abs(player.MoveInput.x) > 0.01f)
                player.StateMachine.ChangeState(player.MoveState, player);
            else
                player.StateMachine.ChangeState(player.IdleState, player);
        }
    }

    public void FixedUpdate(PlayerController player) { }

    public void Exit(PlayerController player)
    {
        // 중력 복구
        player.Rb.gravityScale = player.DefaultGravityScale;
    }
}
