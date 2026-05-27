/// <summary>
/// 플레이어 상태 인터페이스.
/// 모든 상태(Idle, Move, Jump, Fall, Dash)는 이 인터페이스를 구현합니다.
/// </summary>
public interface IPlayerState
{
    void Enter(PlayerController player);
    void Update(PlayerController player);
    void FixedUpdate(PlayerController player);
    void Exit(PlayerController player);
}
