using UnityEngine;

/// <summary>
/// 원거리 무기 발사체.
/// 기획서: "마우스 커서 위치로 방향 벡터를 계산하여 무기 발사체 인스턴스 생성 및 탄속, 관통력 적용"
/// 
/// [프리팹 구성]
/// - Rigidbody2D (Gravity Scale = 0, Collision Detection = Continuous)
/// - CircleCollider2D (Is Trigger = true)
/// - 이 스크립트
/// - SpriteRenderer (발사체 비주얼)
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
public class Projectile : MonoBehaviour
{
    private float _damage;
    private float _penetration;
    private int _pierceRemaining;
    private float _lifetime;
    private LayerMask _targetLayer;
    private Rigidbody2D _rb;

    /// <summary>
    /// PlayerCombat에서 생성 직후 호출하여 발사체를 초기화합니다.
    /// </summary>
    public void Initialize(Vector2 direction, float speed, float damage,
                           float penetration, int pierceCount, float lifetime,
                           LayerMask targetLayer)
    {
        _rb = GetComponent<Rigidbody2D>();
        _rb.gravityScale = 0f;
        _rb.linearVelocity = direction.normalized * speed;

        _damage = damage;
        _penetration = penetration;
        _pierceRemaining = pierceCount;
        _lifetime = lifetime;
        _targetLayer = targetLayer;

        // 발사체 회전 (진행 방향을 바라보도록)
        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
        transform.rotation = Quaternion.Euler(0f, 0f, angle);

        // 수명이 다하면 자동 소멸
        Destroy(gameObject, _lifetime);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        // 타겟 레이어가 아닌 경우 무시
        if (((1 << other.gameObject.layer) & _targetLayer) == 0)
        {
            // 벽/지형에 충돌하면 소멸 (Ground 레이어 등)
            if (other.gameObject.CompareTag("Ground"))
            {
                Destroy(gameObject);
            }
            return;
        }

        DamageableBase target = other.GetComponent<DamageableBase>();
        if (target != null)
        {
            // 관통력 적용 데미지 계산
            float effectiveDefense = target.Defense * (1f - _penetration);
            float finalDamage = Mathf.Max(1f, _damage - effectiveDefense);
            // 투사체 위치 기준으로 넉백 방향 결정 (뒤통수에 맞으면 앞으로 밀림)
            target.TakeDamageWithKnockback(finalDamage, transform.position);
        }

        // 관통 처리
        if (_pierceRemaining <= 0)
        {
            Destroy(gameObject);
        }
        else
        {
            _pierceRemaining--;
        }
    }
}
