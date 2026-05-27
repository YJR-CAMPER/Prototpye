/// <summary>
/// 데미지를 받을 수 있는 오브젝트의 공통 인터페이스.
/// 플레이어, 적, 파괴 가능한 오브젝트 등이 구현합니다.
/// </summary>
public interface IDamageable
{
    float Defense { get; }
    void TakeDamage(float damage);
}
