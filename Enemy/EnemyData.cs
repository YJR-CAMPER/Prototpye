using UnityEngine;

/// <summary>
/// 적 등급. 층계가 깊어질수록 고등급 몬스터 출현 확률 상승.
/// </summary>
public enum EnemyGrade
{
    Common,
    Uncommon,
    Rare,
    Boss
}

/// <summary>
/// 적 AI 행동 패턴.
/// </summary>
public enum EnemyBehavior
{
    Patrol,     // 좌우 순찰 후 플레이어 발견 시 추적
    Stationary, // 제자리에서 플레이어 접근 시 공격
    Flying      // 공중 이동 (중력 무시)
}

/// <summary>
/// 적 데이터 ScriptableObject.
/// 기획서 변수: EnemyType, EnemyHealth, EnemyAtk, EnemyDef, EnemySpd
/// 
/// [사용법]
/// Assets > Create > Game/EnemyData 로 생성
/// </summary>
[CreateAssetMenu(fileName = "NewEnemy", menuName = "Game/EnemyData")]
public class EnemyData : ScriptableObject
{
    [Header("기본 정보 (EnemyType)")]
    public string enemyName = "New Enemy";
    public EnemyGrade grade = EnemyGrade.Common;
    public EnemyBehavior behavior = EnemyBehavior.Patrol;

    [Header("스탯")]
    [Tooltip("EnemyHealth: 기본 체력")]
    public float baseHealth = 30f;

    [Tooltip("EnemyAtk: 기본 공격력")]
    public float baseAttack = 8f;

    [Tooltip("EnemyDef: 기본 방어력")]
    public float baseDefense = 3f;

    [Tooltip("EnemySpd: 이동속도")]
    public float moveSpeed = 3f;

    [Header("전투")]
    public float attackRange = 1.5f;       // 공격 사거리
    public float attackCooldown = 1.2f;    // 공격 쿨타임
    public float detectionRange = 8f;      // 플레이어 탐지 범위

    [Header("순찰 (Patrol 전용)")]
    public float patrolDistance = 4f;      // 순찰 거리

    [Header("드롭")]
    [Tooltip("처치 시 지급 금액 (PlayerCrd)")]
    public int goldDrop = 10;

    /// <summary>
    /// 층계별 스케일링 적용된 스탯 반환.
    /// 기획서: "층계를 내려갈 때마다 체력, 공격력이 증가"
    /// </summary>
    public float GetScaledHealth(int floorLevel)
    {
        return baseHealth * (1f + 0.15f * (floorLevel - 1));
    }

    public float GetScaledAttack(int floorLevel)
    {
        return baseAttack * (1f + 0.12f * (floorLevel - 1));
    }

    public float GetScaledDefense(int floorLevel)
    {
        return baseDefense * (1f + 0.08f * (floorLevel - 1));
    }
}
