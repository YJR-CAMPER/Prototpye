using UnityEngine;

/// <summary>
/// 무기 타입 (기획서: WeaponType)
/// </summary>
public enum WeaponType
{
    Melee,  // 근접
    Ranged  // 원거리
}

/// <summary>
/// 무기 데이터 ScriptableObject.
/// 기획서 변수: WDamage, WSpeed, WPenetration, WAbility
/// 
/// [사용법]
/// Assets > Create > Game/WeaponData 로 생성 후 Inspector에서 값 설정
/// </summary>
[CreateAssetMenu(fileName = "NewWeapon", menuName = "Game/WeaponData")]
public class WeaponData : ScriptableObject
{
    [Header("기본 정보")]
    public string weaponName = "New Weapon";
    public Sprite weaponSprite;
    public WeaponType weaponType = WeaponType.Melee;

    [Header("무기 스탯 (WeaponStat)")]
    [Tooltip("WDamage: 기본 공격력")]
    public float damage = 10f;

    [Tooltip("WSpeed: 공격속도 (초 단위 쿨타임)")]
    public float attackCooldown = 0.5f;

    [Tooltip("WPenetration: 관통력 (방어력 무시 비율 0~1)")]
    [Range(0f, 1f)]
    public float penetration = 0f;

    [Header("근접 무기 전용")]
    [Tooltip("히트박스 크기")]
    public Vector2 meleeHitboxSize = new Vector2(1.5f, 1f);

    [Tooltip("히트박스 오프셋 (캐릭터 기준 전방)")]
    public float meleeRange = 1f;

    [Header("원거리 무기 전용 (WeaponProj)")]
    [Tooltip("발사체 프리팹")]
    public GameObject projectilePrefab;

    [Tooltip("발사체 속도")]
    public float projectileSpeed = 15f;

    [Tooltip("발사체 수명 (초)")]
    public float projectileLifetime = 3f;

    [Tooltip("관통 횟수 (0이면 첫 적 명중 시 소멸)")]
    public int projectilePierceCount = 0;
}
