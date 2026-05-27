using System.Diagnostics;
using UnityEngine;

/// <summary>
/// 월드에 떨어진 아이템.
/// 플레이어가 접근하여 상호작용하면 인벤토리에 추가됩니다.
/// 
/// [프리팹 구성]
/// - SpriteRenderer (아이콘 표시)
/// - Collider2D (Is Trigger = true, 플레이어 감지)
/// - 이 스크립트
/// </summary>
public class WorldItem : MonoBehaviour
{
    [Header("아이템 정보")]
    [SerializeField] private ItemData itemData;
    [SerializeField] private int count = 1;

    [Header("픽업 설정")]
    [SerializeField] private KeyCode pickupKey = KeyCode.E;  // 게임패드: Fire2 등으로 변경 가능
    [SerializeField] private bool autoPickup = false;         // 자동 줍기 여부

    private bool _playerInRange;
    private InventoryManager _playerInventory;

    public ItemData Data => itemData;

    /// <summary>
    /// 코드로 월드 아이템을 생성할 때 사용합니다. (적 드롭 등)
    /// </summary>
    public void Initialize(ItemData data, int amount = 1)
    {
        itemData = data;
        count = amount;

        // 아이콘 설정
        SpriteRenderer sr = GetComponent<SpriteRenderer>();
        if (sr != null && data.icon != null)
            sr.sprite = data.icon;
    }

    private void Update()
    {
        if (!_playerInRange || _playerInventory == null) return;

        if (Input.GetKeyDown(pickupKey))
        {
            TryPickup();
        }
    }

    private void TryPickup()
    {
        // InventoryManager.AddItem도 int(남은 개수)를 반환하도록 랩핑해야 합니다.
        int remaining = _playerInventory.AddItems(itemData, count);

        if (remaining == 0)
        {
            // 전부 다 주웠음
            Destroy(gameObject);
        }
        else if (remaining < count)
        {
            // 일부만 주웠음 (남은 개수로 갱신)
            count = remaining;
            Debug.Log($"인벤토리가 가득 차서 {remaining}개 남음!");
        }
        else
        {
            // 하나도 못 주웠음
            Debug.Log("인벤토리 공간 부족!");
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.CompareTag("Player")) return;

        _playerInventory = other.GetComponent<InventoryManager>();
        if (_playerInventory == null) return;

        _playerInRange = true;

        if (autoPickup)
        {
            TryPickup();
        }
        else
        {
            // TODO: 줍기 프롬프트 UI 표시 ("[E] 줍기")
        }
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (!other.CompareTag("Player")) return;

        _playerInRange = false;
        _playerInventory = null;

        // TODO: 프롬프트 UI 숨기기
    }
}
