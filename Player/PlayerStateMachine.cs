/// <summary>
/// 상태머신 관리자. 현재 상태를 추적하고 전환을 처리합니다.
/// </summary>
public class PlayerStateMachine
{
    public IPlayerState CurrentState { get; private set; }

    public void ChangeState(IPlayerState newState, PlayerController player)
    {
        CurrentState?.Exit(player);
        CurrentState = newState;
        CurrentState.Enter(player);
    }

    public void Update(PlayerController player)
    {
        CurrentState?.Update(player);
    }

    public void FixedUpdate(PlayerController player)
    {
        CurrentState?.FixedUpdate(player);
    }
}
