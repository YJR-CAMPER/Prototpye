/// <summary>
/// 적 상태 인터페이스.
/// </summary>
public interface IEnemyState
{
    void Enter(EnemyBase enemy);
    void Update(EnemyBase enemy);
    void FixedUpdate(EnemyBase enemy);
    void Exit(EnemyBase enemy);
}

/// <summary>
/// 적 상태머신.
/// </summary>
public class EnemyStateMachine
{
    public IEnemyState CurrentState { get; private set; }

    public void ChangeState(IEnemyState newState, EnemyBase enemy)
    {
        CurrentState?.Exit(enemy);
        CurrentState = newState;
        CurrentState.Enter(enemy);
    }

    public void Update(EnemyBase enemy)
    {
        CurrentState?.Update(enemy);
    }

    public void FixedUpdate(EnemyBase enemy)
    {
        CurrentState?.FixedUpdate(enemy);
    }
}
